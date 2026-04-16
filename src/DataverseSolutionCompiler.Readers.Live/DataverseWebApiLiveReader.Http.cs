using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Azure.Core;
using Azure.Identity;
using DataverseSolutionCompiler.Domain.Model;

namespace DataverseSolutionCompiler.Readers.Live;

internal sealed partial class DataverseWebApiLiveReader
{
    private const string SolutionScope = "solution-components";
    private const string EntityFallbackScope = "entity-fallback";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private async Task<JsonNode?> GetJsonAsync(string relativePath, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildRequestUri(relativePath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false));

        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new DataverseWebApiException(
                "live-readback-http-failure",
                response.StatusCode,
                relativePath,
                $"Dataverse Web API request failed with {(int)response.StatusCode} {response.StatusCode}: {body}");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(content) ? null : JsonNode.Parse(content);
    }

    private async Task<JsonObject?> GetSingleObjectAsync(string relativePath, CancellationToken cancellationToken)
    {
        var node = await GetJsonAsync(relativePath, cancellationToken).ConfigureAwait(false);
        return node switch
        {
            JsonObject jsonObject when GetProperty(jsonObject, "value") is JsonArray array => array.OfType<JsonObject>().FirstOrDefault(),
            JsonObject jsonObject => jsonObject,
            JsonArray jsonArray => jsonArray.OfType<JsonObject>().FirstOrDefault(),
            _ => null
        };
    }

    private async Task<JsonArray> GetCollectionAsync(string relativePath, CancellationToken cancellationToken)
    {
        var aggregate = new JsonArray();
        string? next = relativePath;

        while (!string.IsNullOrWhiteSpace(next))
        {
            var node = await GetJsonAsync(next, cancellationToken).ConfigureAwait(false);
            switch (node)
            {
                case JsonObject jsonObject when GetProperty(jsonObject, "value") is JsonArray valueArray:
                    foreach (var item in valueArray)
                    {
                        aggregate.Add(item?.DeepClone());
                    }

                    next = StringValue(GetProperty(jsonObject, "@odata.nextLink"));
                    if (!string.IsNullOrWhiteSpace(next) && Uri.TryCreate(next, UriKind.Absolute, out var absoluteNext))
                    {
                        next = _serviceRoot is not null && absoluteNext.AbsoluteUri.StartsWith(_serviceRoot.AbsoluteUri, StringComparison.OrdinalIgnoreCase)
                            ? absoluteNext.AbsoluteUri[_serviceRoot.AbsoluteUri.Length..]
                            : absoluteNext.ToString();
                    }

                    break;
                case JsonArray jsonArray:
                    foreach (var item in jsonArray)
                    {
                        aggregate.Add(item?.DeepClone());
                    }

                    next = null;
                    break;
                case JsonObject singleObject:
                    aggregate.Add(singleObject.DeepClone());
                    next = null;
                    break;
                default:
                    next = null;
                    break;
            }
        }

        return aggregate;
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_accessToken))
        {
            return _accessToken;
        }

        if (_serviceRoot is null)
        {
            throw new InvalidOperationException("Service root must be initialized before acquiring an access token.");
        }

        var resourceRoot = $"{_serviceRoot.Scheme}://{_serviceRoot.Host}";
        var scopes = new[]
        {
            $"{resourceRoot}/.default",
            $"{resourceRoot}/user_impersonation"
        };

        AuthenticationFailedException? lastFailure = null;
        foreach (var scope in scopes)
        {
            try
            {
                var token = await _credential.GetTokenAsync(new TokenRequestContext([scope]), cancellationToken).ConfigureAwait(false);
                _accessToken = token.Token;
                return token.Token;
            }
            catch (AuthenticationFailedException exception)
            {
                lastFailure = exception;
            }
        }

        throw lastFailure ?? new AuthenticationFailedException("Failed to acquire a Dataverse Web API access token.");
    }

    private Uri BuildRequestUri(string relativePath) =>
        _serviceRoot is null
            ? throw new InvalidOperationException("Service root must be initialized before issuing requests.")
            : Uri.TryCreate(relativePath, UriKind.Absolute, out var absolute)
                ? absolute
                : new Uri(_serviceRoot, relativePath);

    private static Uri BuildServiceRoot(Uri dataverseUrl, string apiVersion)
    {
        var baseUri = dataverseUrl.ToString().TrimEnd('/') + "/";
        return new Uri($"{baseUri}api/data/{apiVersion}/", UriKind.Absolute);
    }

    private static IReadOnlyDictionary<string, string>? CreateProperties(params (string Key, string? Value)[] properties)
    {
        var dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in properties)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            dictionary[key] = value;
        }

        return dictionary.Count == 0 ? null : dictionary;
    }

    private static string SerializeJson<T>(T value) =>
        JsonSerializer.Serialize(value, JsonOptions);

    private static string ComputeSignature(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ComputeByteHash(byte[] value)
    {
        var hash = SHA256.HashData(value);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool TryDecodeBase64Content(string? value, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            bytes = Convert.FromBase64String(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string NormalizeBoolean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "false";
        }

        return value.Trim() switch
        {
            "1" => "true",
            "0" => "false",
            var text when text.Equals("true", StringComparison.OrdinalIgnoreCase) => "true",
            var text when text.Equals("false", StringComparison.OrdinalIgnoreCase) => "false",
            _ => value.Trim().ToLowerInvariant()
        };
    }

    private static string? NormalizeLogicalName(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static string NormalizeGuid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim().Trim('{', '}');
        return Guid.TryParse(trimmed, out var guid)
            ? guid.ToString("D")
            : trimmed.ToLowerInvariant();
    }

    private static string EscapeODataLiteral(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);

    private static string? DescribeWebResourceType(string? value) =>
        value switch
        {
            "1" => "html",
            "2" => "css",
            "3" => "script",
            "4" => "xml",
            "5" => "png",
            "6" => "jpg",
            "7" => "gif",
            "8" => "xap",
            "9" => "xsl",
            "10" => "ico",
            "11" => "svg",
            "12" => "resx",
            _ => value
        };

    private static string FormatGuid(Guid guid) =>
        guid.ToString("D");

    private static string BuildGuidFilter(string fieldName, IEnumerable<Guid> ids) =>
        string.Join(" or ", ids.Distinct().OrderBy(id => id).Select(id => $"{fieldName} eq {FormatGuid(id)}"));

    private static JsonNode? GetProperty(JsonNode? node, params string[] names)
    {
        if (node is not JsonObject jsonObject)
        {
            return null;
        }

        foreach (var name in names)
        {
            foreach (var property in jsonObject)
            {
                if (string.Equals(property.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return property.Value;
                }
            }
        }

        return null;
    }

    private static string? GetString(JsonNode? node, params string[] names) =>
        StringValue(GetProperty(node, names));

    private static string? StringValue(JsonNode? node) =>
        node switch
        {
            null => null,
            JsonValue value => value.TryGetValue<string>(out var stringValue)
                ? stringValue
                : value.TryGetValue<bool>(out var boolValue)
                    ? NormalizeBoolean(boolValue ? "true" : "false")
                    : value.TryGetValue<int>(out var intValue)
                        ? intValue.ToString(CultureInfo.InvariantCulture)
                        : value.TryGetValue<long>(out var longValue)
                            ? longValue.ToString(CultureInfo.InvariantCulture)
                            : value.TryGetValue<double>(out var doubleValue)
                                ? doubleValue.ToString(CultureInfo.InvariantCulture)
                                : value.ToJsonString(),
            JsonObject jsonObject => GetString(jsonObject, "Value")
                ?? GetString(GetProperty(jsonObject, "UserLocalizedLabel"), "Label")
                ?? ReadArray(jsonObject, "LocalizedLabels").OfType<JsonObject>().Select(label => GetString(label, "Label")).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
            _ => node.ToJsonString()
        };

    private static Guid? GetGuid(JsonNode? node, params string[] names) =>
        Guid.TryParse(StringValue(GetProperty(node, names))?.Trim('{', '}'), out var value) ? value : null;

    private static int? GetInt32(JsonNode? node, params string[] names)
    {
        var value = StringValue(GetProperty(node, names));
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private static JsonArray ReadArray(JsonNode? node, params string[] names) =>
        GetProperty(node, names) as JsonArray ?? new JsonArray();

    private static IEnumerable<JsonObject> ReadObjects(JsonNode? node, params string[] names) =>
        ReadArray(node, names).OfType<JsonObject>();

    private static string? NormalizeJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var parsed = JsonNode.Parse(raw);
        return parsed is null ? null : NormalizeJsonNode(parsed).ToJsonString(JsonOptions);
    }

    private static JsonNode NormalizeJsonNode(JsonNode node) =>
        node switch
        {
            JsonObject jsonObject => NormalizeJsonObject(jsonObject),
            JsonArray jsonArray => NormalizeJsonArray(jsonArray),
            _ => node.DeepClone()
        };

    private static JsonObject NormalizeJsonObject(JsonObject jsonObject)
    {
        var normalized = new JsonObject();
        foreach (var property in jsonObject.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            normalized[property.Key] = property.Value is null ? null : NormalizeJsonNode(property.Value);
        }

        return normalized;
    }

    private static JsonArray NormalizeJsonArray(JsonArray jsonArray)
    {
        var normalized = new JsonArray();
        foreach (var item in jsonArray)
        {
            normalized.Add(item is null ? null : NormalizeJsonNode(item));
        }

        return normalized;
    }

    private static object[] NormalizeOptionEntries(JsonArray options) =>
        options
            .OfType<JsonObject>()
            .Select(option => new
            {
                value = GetString(option, "value") ?? string.Empty,
                label = GetString(option, "label", "Label") ?? string.Empty,
                isHidden = NormalizeBoolean(GetString(option, "is_hidden", "IsHidden"))
            })
            .OrderBy(option => option.value, StringComparer.OrdinalIgnoreCase)
            .ToArray<object>();

    private static string MapFormType(string? value) =>
        value?.Trim() switch
        {
            "2" => "main",
            "6" => "quick",
            "7" => "quick",
            "11" => "card",
            "12" => "dashboard",
            { Length: > 0 } text => text.ToLowerInvariant(),
            _ => "main"
        };

    private static FormSummary SummarizeFormXml(string? formXml, string formType, string formId)
    {
        if (string.IsNullOrWhiteSpace(formXml))
        {
            return new FormSummary(formType, formId, 0, 0, 0, 0, 0, 0, 0, []);
        }

        var root = XDocument.Parse(formXml).Root;
        if (root is null)
        {
            return new FormSummary(formType, formId, 0, 0, 0, 0, 0, 0, 0, []);
        }

        var controls = root.Descendants().Where(element => element.Name.LocalName.Equals("control", StringComparison.OrdinalIgnoreCase)).ToArray();
        var controlDescriptions = controls
            .Select(control => new ControlDescription(
                control.AttributeValue("id") ?? string.Empty,
                control.AttributeValue("datafieldname") ?? string.Empty,
                DescribeControlRole(control)))
            .OrderBy(control => control.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(control => control.DataFieldName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new FormSummary(
            formType,
            formId,
            root.Descendants().Count(element => element.Name.LocalName.Equals("tab", StringComparison.OrdinalIgnoreCase)),
            root.Descendants().Count(element => element.Name.LocalName.Equals("section", StringComparison.OrdinalIgnoreCase)),
            controls.Length,
            controls.Count(IsQuickFormControl),
            controls.Count(IsSubgridControl),
            root.ElementLocal("header")?.Descendants().Count(element => element.Name.LocalName.Equals("control", StringComparison.OrdinalIgnoreCase)) ?? 0,
            root.ElementLocal("footer")?.Descendants().Count(element => element.Name.LocalName.Equals("control", StringComparison.OrdinalIgnoreCase)) ?? 0,
            controlDescriptions);
    }

    private static ViewSummary SummarizeViewXml(string? fetchXml, string? layoutXml, string targetEntity)
    {
        var layoutRoot = string.IsNullOrWhiteSpace(layoutXml) ? null : XDocument.Parse(layoutXml).Root;
        var fetchRoot = string.IsNullOrWhiteSpace(fetchXml) ? null : XDocument.Parse(fetchXml).Root;
        var fetchEntity = fetchRoot?.Descendants().FirstOrDefault(element => element.Name.LocalName.Equals("entity", StringComparison.OrdinalIgnoreCase));
        var layoutColumns = layoutRoot?
            .Descendants()
            .Where(element => element.Name.LocalName.Equals("cell", StringComparison.OrdinalIgnoreCase))
            .Select(element => element.AttributeValue("name") ?? string.Empty)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray()
            ?? [];
        var fetchAttributes = fetchEntity?
            .Elements()
            .Where(element => element.Name.LocalName.Equals("attribute", StringComparison.OrdinalIgnoreCase))
            .Select(element => element.AttributeValue("name") ?? string.Empty)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray()
            ?? [];
        var filters = fetchEntity?
            .Descendants()
            .Where(element => element.Name.LocalName.Equals("condition", StringComparison.OrdinalIgnoreCase))
            .Select(condition => new ViewFilter(
                condition.AttributeValue("attribute") ?? string.Empty,
                condition.AttributeValue("operator") ?? string.Empty,
                condition.AttributeValue("value") ?? string.Empty))
            .OrderBy(condition => condition.Attribute, StringComparer.OrdinalIgnoreCase)
            .ThenBy(condition => condition.Operator, StringComparer.OrdinalIgnoreCase)
            .ThenBy(condition => condition.Value, StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];
        var orders = fetchEntity?
            .Elements()
            .Where(element => element.Name.LocalName.Equals("order", StringComparison.OrdinalIgnoreCase))
            .Select(order => new ViewOrder(
                order.AttributeValue("attribute") ?? string.Empty,
                NormalizeBoolean(order.AttributeValue("descending"))))
            .ToArray()
            ?? [];

        return new ViewSummary(targetEntity, layoutColumns, fetchAttributes, filters, orders);
    }

    private static VisualizationSummary SummarizeVisualizationXml(string? dataDescriptionXml, string? presentationDescriptionXml, string targetEntity)
    {
        var dataRoot = ParseVisualizationFragment(dataDescriptionXml, "datadescription");
        var presentationRoot = ParseVisualizationFragment(presentationDescriptionXml, "presentationdescription");
        var dataDefinition = dataRoot?.Name.LocalName.Equals("datadefinition", StringComparison.OrdinalIgnoreCase) == true
            ? dataRoot
            : dataRoot?.Descendants().FirstOrDefault(element => element.Name.LocalName.Equals("datadefinition", StringComparison.OrdinalIgnoreCase));
        var fetchEntity = dataDefinition?.Descendants().FirstOrDefault(element => element.Name.LocalName.Equals("entity", StringComparison.OrdinalIgnoreCase));
        var normalizedTargetEntity = NormalizeLogicalName(fetchEntity?.AttributeValue("name")) ?? targetEntity;
        var chartTypes = presentationRoot?
            .Descendants()
            .Where(element => element.Name.LocalName.Equals("Series", StringComparison.OrdinalIgnoreCase))
            .Select(element => element.AttributeValue("ChartType") ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];
        var groupByColumns = fetchEntity?
            .Elements()
            .Where(element => element.Name.LocalName.Equals("attribute", StringComparison.OrdinalIgnoreCase)
                && element.AttributeValue("groupby")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true)
            .Select(element => element.AttributeValue("name") ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];
        var measureAliases = dataDefinition?
            .Descendants()
            .Where(element => element.Name.LocalName.Equals("measure", StringComparison.OrdinalIgnoreCase))
            .Select(element => element.AttributeValue("alias") ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];
        var titleNames = presentationRoot?
            .Descendants()
            .Where(element => element.Name.LocalName.Equals("Title", StringComparison.OrdinalIgnoreCase))
            .Select(element => element.AttributeValue("Name") ?? string.Empty)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];

        return new VisualizationSummary(normalizedTargetEntity, chartTypes, groupByColumns, measureAliases, titleNames);
    }

    private static SiteMapSummary SummarizeSiteMapXml(string? siteMapXml)
    {
        if (string.IsNullOrWhiteSpace(siteMapXml))
        {
            return new SiteMapSummary(0, 0, 0, 0, SerializeJson(new { areas = Array.Empty<object>() }));
        }

        var root = XDocument.Parse(siteMapXml).Root;
        if (root is null)
        {
            return new SiteMapSummary(0, 0, 0, 0, SerializeJson(new { areas = Array.Empty<object>() }));
        }

        var subAreas = root.Descendants().Where(element => element.Name.LocalName.Equals("SubArea", StringComparison.OrdinalIgnoreCase)).ToArray();
        return new SiteMapSummary(
            root.Descendants().Count(element => element.Name.LocalName.Equals("Area", StringComparison.OrdinalIgnoreCase)),
            root.Descendants().Count(element => element.Name.LocalName.Equals("Group", StringComparison.OrdinalIgnoreCase)),
            subAreas.Length,
            subAreas.Count(subArea => (subArea.AttributeValue("Url") ?? string.Empty).StartsWith("$webresource:", StringComparison.OrdinalIgnoreCase)),
            SerializeJson(BuildSiteMapDefinition(root)));
    }

    private static string? NormalizeVisualizationFragment(string? xml, string wrapperElementName)
    {
        var root = ParseVisualizationFragment(xml, wrapperElementName);
        return root?.ToString(SaveOptions.DisableFormatting);
    }

    private static XElement? ParseVisualizationFragment(string? xml, string wrapperElementName)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        try
        {
            var root = XDocument.Parse(xml).Root;
            if (root is null)
            {
                return null;
            }

            return root.Name.LocalName.Equals(wrapperElementName, StringComparison.OrdinalIgnoreCase)
                ? root
                : new XElement(wrapperElementName, root);
        }
        catch
        {
            try
            {
                return XDocument.Parse($"<{wrapperElementName}>{xml}</{wrapperElementName}>").Root;
            }
            catch
            {
                return null;
            }
        }
    }

    private static object BuildSiteMapDefinition(XElement? siteMap)
    {
        var areas = siteMap?
            .Elements()
            .Where(element => element.Name.LocalName.Equals("Area", StringComparison.OrdinalIgnoreCase))
            .Select((area, areaIndex) => new
            {
                id = area.AttributeValue("Id") ?? area.AttributeValue("id") ?? $"area_{areaIndex + 1}",
                title = ReadSiteMapTitle(area) ?? area.AttributeValue("Title") ?? area.AttributeValue("title") ?? $"Area {areaIndex + 1}",
                groups = area
                    .Elements()
                    .Where(element => element.Name.LocalName.Equals("Group", StringComparison.OrdinalIgnoreCase))
                    .Select((group, groupIndex) => new
                    {
                        id = group.AttributeValue("Id") ?? group.AttributeValue("id") ?? $"group_{groupIndex + 1}",
                        title = ReadSiteMapTitle(group) ?? group.AttributeValue("Title") ?? group.AttributeValue("title") ?? $"Group {groupIndex + 1}",
                        subAreas = group
                            .Elements()
                            .Where(element => element.Name.LocalName.Equals("SubArea", StringComparison.OrdinalIgnoreCase))
                            .Select((subArea, subAreaIndex) =>
                            {
                                var rawUrl = subArea.AttributeValue("Url") ?? subArea.AttributeValue("url");
                                var entityList = TryParseEntityListTarget(rawUrl);
                                var entityRecord = TryParseEntityRecordTarget(rawUrl);
                                var dashboard = TryParseDashboardTarget(rawUrl);
                                var customPage = TryParseCustomPageTarget(rawUrl);
                                return new
                                {
                                    id = subArea.AttributeValue("Id") ?? subArea.AttributeValue("id") ?? $"subarea_{subAreaIndex + 1}",
                                    title = ReadSiteMapTitle(subArea) ?? subArea.AttributeValue("Title") ?? subArea.AttributeValue("title") ?? $"Sub Area {subAreaIndex + 1}",
                                    entity = entityList?.LogicalName
                                        ?? entityRecord?.LogicalName
                                        ?? NormalizeLogicalName(subArea.AttributeValue("Entity") ?? subArea.AttributeValue("entity")),
                                    viewId = entityList?.ViewId,
                                    viewType = entityList?.ViewType,
                                    recordId = entityRecord?.RecordId,
                                    formId = entityRecord?.FormId,
                                    url = rawUrl is { Length: > 0 } url && !url.StartsWith("$webresource:", StringComparison.OrdinalIgnoreCase)
                                        && entityList is null
                                        && entityRecord is null
                                        && dashboard is null
                                        && customPage is null
                                        ? NormalizeSiteMapRawUrl(url)
                                        : null,
                                    webResource = rawUrl is { Length: > 0 } encodedUrl && encodedUrl.StartsWith("$webresource:", StringComparison.OrdinalIgnoreCase)
                                        ? encodedUrl["$webresource:".Length..]
                                        : null,
                                    dashboard = dashboard?.DashboardId,
                                    customPage = customPage?.LogicalName,
                                    customPageEntityName = customPage?.ContextEntityName,
                                    customPageRecordId = customPage?.ContextRecordId,
                                    appId = dashboard?.AppId ?? customPage?.AppId ?? entityList?.AppId ?? entityRecord?.AppId,
                                    client = subArea.AttributeValue("Client") ?? subArea.AttributeValue("client"),
                                    passParams = ParseOptionalBoolean(subArea.AttributeValue("PassParams") ?? subArea.AttributeValue("passparams")),
                                    availableOffline = ParseOptionalBoolean(subArea.AttributeValue("AvailableOffline") ?? subArea.AttributeValue("availableoffline")),
                                    icon = subArea.AttributeValue("Icon") ?? subArea.AttributeValue("icon"),
                                    vectorIcon = subArea.AttributeValue("VectorIcon") ?? subArea.AttributeValue("vectoricon")
                                };
                            })
                            .ToArray()
                    })
                    .ToArray()
            })
            .ToArray()
            ?? [];

        return new
        {
            areas
        };
    }

    private static string? ReadSiteMapTitle(XElement element) =>
        element
            .Elements()
            .FirstOrDefault(child => child.Name.LocalName.Equals("Titles", StringComparison.OrdinalIgnoreCase))?
            .Elements()
            .FirstOrDefault(child => child.Name.LocalName.Equals("Title", StringComparison.OrdinalIgnoreCase))?
            .AttributeValue("Title")
        ?? element
            .Elements()
            .FirstOrDefault(child => child.Name.LocalName.Equals("Titles", StringComparison.OrdinalIgnoreCase))?
            .Elements()
            .FirstOrDefault(child => child.Name.LocalName.Equals("Title", StringComparison.OrdinalIgnoreCase))?
            .AttributeValue("title");

    private static bool? ParseOptionalBoolean(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : string.Equals(NormalizeBoolean(value), "true", StringComparison.OrdinalIgnoreCase);

    private readonly record struct SiteMapDashboardTarget(string DashboardId, string? AppId);

    private readonly record struct SiteMapEntityListTarget(string LogicalName, string ViewId, string? ViewType, string? AppId);

    private readonly record struct SiteMapEntityRecordTarget(string LogicalName, string RecordId, string? FormId, string? AppId);

    private readonly record struct SiteMapCustomPageTarget(string LogicalName, string? AppId, string? ContextEntityName, string? ContextRecordId);

    private static SiteMapDashboardTarget? TryParseDashboardTarget(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl)
            || rawUrl.StartsWith("$webresource:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var query = ExtractQueryString(rawUrl);
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var parameters = ParseQueryString(query);
        if (parameters.Keys.Any(key =>
                !key.Equals("appid", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("pagetype", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("id", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        if (!parameters.TryGetValue("pagetype", out var pageType)
            || !pageType.Equals("dashboard", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!parameters.TryGetValue("id", out var dashboardId))
        {
            return null;
        }

        var normalizedDashboardId = NormalizeGuid(dashboardId);
        if (string.IsNullOrWhiteSpace(normalizedDashboardId))
        {
            return null;
        }

        string? normalizedAppId = null;
        if (parameters.TryGetValue("appid", out var appId)
            && TryNormalizeGuid(appId) is not { Length: > 0 } parsedAppId)
        {
            return null;
        }
        else if (!string.IsNullOrWhiteSpace(appId))
        {
            normalizedAppId = TryNormalizeGuid(appId);
        }

        return new SiteMapDashboardTarget(normalizedDashboardId, normalizedAppId);
    }

    private static SiteMapEntityListTarget? TryParseEntityListTarget(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl)
            || rawUrl.StartsWith("$webresource:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var query = ExtractQueryString(rawUrl);
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var parameters = ParseQueryString(query);
        if (parameters.Keys.Any(key =>
                !key.Equals("appid", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("pagetype", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("etn", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("viewid", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("viewtype", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        if (!parameters.TryGetValue("pagetype", out var pageType)
            || !pageType.Equals("entitylist", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!parameters.TryGetValue("etn", out var entityLogicalName)
            || NormalizeLogicalName(entityLogicalName) is not { Length: > 0 } normalizedEntityLogicalName)
        {
            return null;
        }

        if (!parameters.TryGetValue("viewid", out var viewId)
            || TryNormalizeGuid(viewId) is not { Length: > 0 } normalizedViewId)
        {
            return null;
        }

        string? normalizedViewType = null;
        if (parameters.TryGetValue("viewtype", out var viewType)
            && NormalizeSiteMapViewType(viewType) is not { Length: > 0 } parsedViewType)
        {
            return null;
        }
        else if (!string.IsNullOrWhiteSpace(viewType))
        {
            normalizedViewType = NormalizeSiteMapViewType(viewType);
        }

        string? normalizedAppId = null;
        if (parameters.TryGetValue("appid", out var appId)
            && TryNormalizeGuid(appId) is not { Length: > 0 } parsedAppId)
        {
            return null;
        }
        else if (!string.IsNullOrWhiteSpace(appId))
        {
            normalizedAppId = TryNormalizeGuid(appId);
        }

        return new SiteMapEntityListTarget(normalizedEntityLogicalName, normalizedViewId, normalizedViewType, normalizedAppId);
    }

    private static SiteMapEntityRecordTarget? TryParseEntityRecordTarget(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl)
            || rawUrl.StartsWith("$webresource:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var query = ExtractQueryString(rawUrl);
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var parameters = ParseQueryString(query);
        if (parameters.Keys.Any(key =>
                !key.Equals("appid", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("pagetype", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("etn", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("id", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("extraqs", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        if (!parameters.TryGetValue("pagetype", out var pageType)
            || !pageType.Equals("entityrecord", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!parameters.TryGetValue("etn", out var entityLogicalName)
            || NormalizeLogicalName(entityLogicalName) is not { Length: > 0 } normalizedEntityLogicalName)
        {
            return null;
        }

        if (!parameters.TryGetValue("id", out var recordId)
            || TryNormalizeGuid(recordId) is not { Length: > 0 } normalizedRecordId)
        {
            return null;
        }

        string? normalizedFormId = null;
        if (parameters.TryGetValue("extraqs", out var extraQueryString))
        {
            normalizedFormId = TryParseSiteMapFormId(extraQueryString);
            if (string.IsNullOrWhiteSpace(normalizedFormId))
            {
                return null;
            }
        }

        string? normalizedAppId = null;
        if (parameters.TryGetValue("appid", out var appId)
            && TryNormalizeGuid(appId) is not { Length: > 0 } parsedAppId)
        {
            return null;
        }
        else if (!string.IsNullOrWhiteSpace(appId))
        {
            normalizedAppId = TryNormalizeGuid(appId);
        }

        return new SiteMapEntityRecordTarget(normalizedEntityLogicalName, normalizedRecordId, normalizedFormId, normalizedAppId);
    }

    private static SiteMapCustomPageTarget? TryParseCustomPageTarget(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl)
            || rawUrl.StartsWith("$webresource:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var query = ExtractQueryString(rawUrl);
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var parameters = ParseQueryString(query);
        if (parameters.Keys.Any(key =>
                !key.Equals("appid", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("pagetype", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("name", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("entityname", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("recordid", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        if (!parameters.TryGetValue("pagetype", out var pageType)
            || !pageType.Equals("custom", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!parameters.TryGetValue("name", out var logicalName))
        {
            return null;
        }

        var normalizedLogicalName = NormalizeLogicalName(logicalName);
        if (string.IsNullOrWhiteSpace(normalizedLogicalName))
        {
            return null;
        }

        string? normalizedContextEntityName = null;
        if (parameters.TryGetValue("entityname", out var contextEntityName)
            && NormalizeLogicalName(contextEntityName) is not { Length: > 0 } parsedContextEntityName)
        {
            return null;
        }
        else if (!string.IsNullOrWhiteSpace(contextEntityName))
        {
            normalizedContextEntityName = NormalizeLogicalName(contextEntityName);
        }

        string? normalizedContextRecordId = null;
        if (parameters.TryGetValue("recordid", out var contextRecordId)
            && TryNormalizeGuid(contextRecordId) is not { Length: > 0 } parsedContextRecordId)
        {
            return null;
        }
        else if (!string.IsNullOrWhiteSpace(contextRecordId))
        {
            normalizedContextRecordId = TryNormalizeGuid(contextRecordId);
        }

        if (!parameters.TryGetValue("appid", out var appId))
        {
            return new SiteMapCustomPageTarget(normalizedLogicalName, null, normalizedContextEntityName, normalizedContextRecordId);
        }

        var normalizedAppId = TryNormalizeGuid(appId);
        return string.IsNullOrWhiteSpace(normalizedAppId)
            ? null
            : new SiteMapCustomPageTarget(normalizedLogicalName, normalizedAppId, normalizedContextEntityName, normalizedContextRecordId);
    }

    private static string? ExtractQueryString(string rawUrl)
    {
        var separatorIndex = rawUrl.IndexOf('?', StringComparison.Ordinal);
        if (separatorIndex < 0 || separatorIndex == rawUrl.Length - 1)
        {
            return null;
        }

        return rawUrl[(separatorIndex + 1)..];
    }

    private static string NormalizeSiteMapRawUrl(string rawUrl)
    {
        var trimmed = rawUrl.Trim();
        var separatorIndex = trimmed.IndexOf('?', StringComparison.Ordinal);
        if (separatorIndex < 0 || separatorIndex == trimmed.Length - 1)
        {
            return trimmed;
        }

        var path = trimmed[..separatorIndex];
        var parameters = ParseQueryString(trimmed[(separatorIndex + 1)..]);
        if (parameters.Count == 0)
        {
            return path;
        }

        var normalizedQuery = string.Join("&", parameters
            .Select(pair => new KeyValuePair<string, string>(pair.Key.Trim(), NormalizeSiteMapRawQueryValue(pair.Key, pair.Value)))
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(pair => pair.Value, StringComparer.OrdinalIgnoreCase)
            .Select(FormatSiteMapRawQueryPair));

        return $"{path}?{normalizedQuery}";
    }

    private static string NormalizeSiteMapRawQueryValue(string key, string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return string.Empty;
        }

        return key.Trim().ToLowerInvariant() switch
        {
            "appid" or "id" or "recordid" or "viewid" or "formid" => TryNormalizeGuid(trimmed) ?? trimmed,
            "etn" or "entityname" or "name" or "pagetype" => trimmed.ToLowerInvariant(),
            "extraqs" => NormalizeSiteMapEmbeddedRawQuery(trimmed),
            _ => NormalizeSiteMapRawBoolean(trimmed) ?? trimmed
        };
    }

    private static string NormalizeSiteMapEmbeddedRawQuery(string value)
    {
        var parameters = ParseQueryString(Uri.UnescapeDataString(value));
        if (parameters.Count == 0)
        {
            return value.Trim();
        }

        return string.Join("&", parameters
            .Select(pair => new KeyValuePair<string, string>(pair.Key.Trim(), NormalizeSiteMapRawQueryValue(pair.Key, pair.Value)))
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(pair => pair.Value, StringComparer.OrdinalIgnoreCase)
            .Select(pair => string.IsNullOrEmpty(pair.Value)
                ? Uri.EscapeDataString(pair.Key)
                : $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
    }

    private static string? NormalizeSiteMapRawBoolean(string value) =>
        value.Trim() switch
        {
            "1" => "true",
            "0" => "false",
            var text when text.Equals("true", StringComparison.OrdinalIgnoreCase) => "true",
            var text when text.Equals("false", StringComparison.OrdinalIgnoreCase) => "false",
            _ => null
        };

    private static string FormatSiteMapRawQueryPair(KeyValuePair<string, string> pair) =>
        string.IsNullOrEmpty(pair.Value)
            ? Uri.EscapeDataString(pair.Key)
            : $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}";

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = segment.IndexOf('=', StringComparison.Ordinal);
            var key = separatorIndex >= 0 ? segment[..separatorIndex] : segment;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var value = separatorIndex >= 0 ? segment[(separatorIndex + 1)..] : string.Empty;
            values[Uri.UnescapeDataString(key)] = Uri.UnescapeDataString(value);
        }

        return values;
    }

    private static string? NormalizeSiteMapViewType(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "1039" => "savedquery",
            "4230" => "userquery",
            "savedquery" => "savedquery",
            "userquery" => "userquery",
            _ => null
        };

    private static string? TryParseSiteMapFormId(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var extraParameters = ParseEmbeddedQueryString(Uri.UnescapeDataString(rawValue));
        if (extraParameters.Count != 1
            || !extraParameters.TryGetValue("formid", out var formId))
        {
            return null;
        }

        return TryNormalizeGuid(formId);
    }

    private static Dictionary<string, string> ParseEmbeddedQueryString(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var segment in query.Split(['&', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = segment.IndexOf('=', StringComparison.Ordinal);
            var key = separatorIndex >= 0 ? segment[..separatorIndex] : segment;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var value = separatorIndex >= 0 ? segment[(separatorIndex + 1)..] : string.Empty;
            values[Uri.UnescapeDataString(key)] = Uri.UnescapeDataString(value);
        }

        return values;
    }

    private static string? TryNormalizeGuid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim().Trim('{', '}');
        return Guid.TryParse(trimmed, out var guid)
            ? guid.ToString("D")
            : null;
    }

    private static bool IsQuickFormControl(XElement control) =>
        !string.IsNullOrWhiteSpace(control.ElementLocal("parameters")?.ElementLocal("QuickForms")?.Value);

    private static bool IsSubgridControl(XElement control) =>
        control.AttributeValue("indicationOfSubgrid")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true
        || !string.IsNullOrWhiteSpace(control.ElementLocal("parameters")?.ElementLocal("RelationshipName")?.Value);

    private static string DescribeControlRole(XElement control)
    {
        if (IsQuickFormControl(control))
        {
            return "quickView";
        }

        if (IsSubgridControl(control))
        {
            return "subgrid";
        }

        return !string.IsNullOrWhiteSpace(control.AttributeValue("datafieldname"))
            ? "field"
            : "unsupported";
    }
}

internal static class HashSetExtensions
{
    public static void AddIfNotEmpty(this ICollection<string> values, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            values.Add(value);
        }
    }

    public static void AddIfNotEmpty(this ICollection<Guid> values, Guid? value)
    {
        if (value.HasValue && value.Value != Guid.Empty)
        {
            values.Add(value.Value);
        }
    }
}

internal static class XElementExtensions
{
    public static XElement? ElementLocal(this XElement? element, string localName) =>
        element?.Elements().FirstOrDefault(child => child.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));

    public static string? AttributeValue(this XElement? element, string attributeName) =>
        element?.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName.Equals(attributeName, StringComparison.OrdinalIgnoreCase))?.Value;
}
