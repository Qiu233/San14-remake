using System.Globalization;
using System.Text.Json;

namespace San14.Data;

/// <summary>Validated keyed text with optional partial overrides.</summary>
public sealed class LocalizedText
{
    private readonly IReadOnlyDictionary<string, string> _values;
    public IReadOnlyCollection<string> Keys => _values.Keys.ToArray();

    private LocalizedText(IReadOnlyDictionary<string, string> values) => _values = values;

    public static LocalizedText Parse(string bundledJson, string? overrideJson = null)
    {
        Dictionary<string, string> values = ParseObject(bundledJson, "bundled");
        if (overrideJson is not null)
        {
            foreach ((string key, string value) in ParseObject(overrideJson, "override"))
            {
                if (!values.ContainsKey(key))
                    throw new InvalidDataException($"Unknown localized text override key: {key}");
                values[key] = value;
            }
        }
        return new LocalizedText(values);
    }

    public string Get(string key) => _values.TryGetValue(key, out string? value)
        ? value : throw new InvalidDataException($"Localized text key is missing: {key}");

    public string Format(string key, params object?[] args)
    {
        try { return string.Format(CultureInfo.CurrentCulture, Get(key), args); }
        catch (FormatException error)
        {
            throw new InvalidDataException($"Localized text format is invalid for key '{key}'.", error);
        }
    }

    private static Dictionary<string, string> ParseObject(string json, string origin)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"{origin} localized text must be a JSON object.");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            if (property.Name.Length == 0 || property.Value.ValueKind != JsonValueKind.String ||
                property.Value.GetString() is not string value || value.Length == 0 ||
                !values.TryAdd(property.Name, value))
                throw new InvalidDataException($"Invalid or duplicate {origin} localized text key: {property.Name}");
        }
        return values;
    }
}
