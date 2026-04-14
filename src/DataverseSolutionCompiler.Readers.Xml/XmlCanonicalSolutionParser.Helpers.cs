using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace DataverseSolutionCompiler.Readers.Xml;

internal sealed partial class XmlCanonicalSolutionParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

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

    private static XElement LoadRoot(string path) =>
        XDocument.Load(path).Root ?? throw new InvalidOperationException($"XML file '{path}' does not contain a root element.");

    private static string? LocalizedDescription(XElement? container)
    {
        if (container is null)
        {
            return null;
        }

        if (container.Attribute("description") is not null)
        {
            return container.Attribute("description")?.Value;
        }

        if (container.Attribute("default") is not null)
        {
            return container.Attribute("default")?.Value;
        }

        foreach (var descendant in container.DescendantsAndSelf())
        {
            var description = descendant.Attribute("description")?.Value;
            if (!string.IsNullOrWhiteSpace(description))
            {
                return description;
            }

            var defaultValue = descendant.Attribute("default")?.Value;
            if (!string.IsNullOrWhiteSpace(defaultValue))
            {
                return defaultValue;
            }
        }

        return null;
    }

    private static string? Text(XElement? element) =>
        string.IsNullOrWhiteSpace(element?.Value) ? null : element.Value.Trim();

    private static string? NormalizeXml(XElement? element) =>
        element is null ? null : element.ToString(SaveOptions.DisableFormatting);

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

    private static string SerializeJson<T>(T value) =>
        JsonSerializer.Serialize(value, JsonOptions);

    private static string? NormalizeJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var parsed = JsonNode.Parse(raw);
        if (parsed is null)
        {
            return null;
        }

        return NormalizeJsonNode(parsed).ToJsonString(JsonOptions);
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

    private static string ComputeSignature(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ComputeFileHash(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

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
}

internal static class XmlElementExtensions
{
    public static XElement? ElementLocal(this XElement? element, string localName) =>
        element?.Elements().FirstOrDefault(child => child.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));

    public static string? AttributeValue(this XElement? element, string attributeName) =>
        element?.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName.Equals(attributeName, StringComparison.OrdinalIgnoreCase))?.Value;

    public static TResult? Let<TSource, TResult>(this TSource? source, Func<TSource, TResult?> selector)
        where TSource : class =>
        source is null ? default : selector(source);
}
