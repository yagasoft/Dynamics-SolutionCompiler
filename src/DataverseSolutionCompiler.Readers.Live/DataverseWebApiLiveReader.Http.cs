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

    private static string FormatGuid(Guid guid) =>
        $"guid'{guid:D}'";

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
            "6" => "card",
            "7" => "quick",
            "11" => "main",
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

    private static SiteMapSummary SummarizeSiteMapXml(string? siteMapXml)
    {
        if (string.IsNullOrWhiteSpace(siteMapXml))
        {
            return new SiteMapSummary(0, 0, 0, 0);
        }

        var root = XDocument.Parse(siteMapXml).Root;
        if (root is null)
        {
            return new SiteMapSummary(0, 0, 0, 0);
        }

        var subAreas = root.Descendants().Where(element => element.Name.LocalName.Equals("SubArea", StringComparison.OrdinalIgnoreCase)).ToArray();
        return new SiteMapSummary(
            root.Descendants().Count(element => element.Name.LocalName.Equals("Area", StringComparison.OrdinalIgnoreCase)),
            root.Descendants().Count(element => element.Name.LocalName.Equals("Group", StringComparison.OrdinalIgnoreCase)),
            subAreas.Length,
            subAreas.Count(subArea => (subArea.AttributeValue("Url") ?? string.Empty).StartsWith("$webresource:", StringComparison.OrdinalIgnoreCase)));
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
            return "quick-form";
        }

        if (IsSubgridControl(control))
        {
            return "subgrid";
        }

        return "field";
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
