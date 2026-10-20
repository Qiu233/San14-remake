using System.Text;
using System.Text.Json;
using San14.Core;

namespace San14.Data;

/// <summary>Project-selected CE default profile, extracted offline. This is
/// immutable rule data, never a reconstruction of a loaded campaign's values.</summary>
public sealed record CityDomesticParameterCatalog(string Locale, IReadOnlyDictionary<string, int> Values)
{
    public const string Profile = "ce-city-domestic-default-v1";
    public static string PathFor(string locale) => locale is "TC" or "SC"
        ? $"rules/ce/city-domestic-defaults/v1/{locale}.json" : throw new InvalidDataException("city-domestic.catalog_locale");
    public static string UriFor(string locale) => GameResourceId.File(PathFor(locale)).ToString();
    private static readonly (string Name, int Value)[] Defaults =
    [
        ("recruitBaseCost", 50), ("trainingBaseCost", 300), ("costPolicyScale", 1),
        ("trainingBaseGain", 1), ("trainingTroopsDivisor", 3000), ("trainingMoraleSqrtScale", 3),
        ("trainingDenominatorOffset", 10), ("trainingTraitScale", 1), ("trainingPolicyScale", 1),
        ("recruiterCommandCost", 1), ("trainerCommandCost", 1), ("policyCommandCost", 1)
    ];
    internal static byte[] BuiltInBytes(string locale)
    {
        _ = PathFor(locale);
        return Encoding.UTF8.GetBytes("{\"profile\":\"" + Profile + "\",\"locale\":\"" + locale +
            "\",\"ruleVersion\":\"" + SimulationRuleIdentity.ForLocale(locale) + "\",\"values\":{" +
            string.Join(',', Defaults.Select(x => "\"" + x.Name + "\":" + x.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))) + "}}");
    }
    internal static CityDomesticParameterCatalog Parse(byte[] bytes, string locale)
    {
        _ = PathFor(locale);
        try
        {
            using var doc = JsonDocument.Parse(bytes);
            var root = Unique(doc.RootElement);
            if (root.Count != 4 || !root.TryGetValue("profile", out var profile) || profile.GetString() != Profile ||
                !root.TryGetValue("locale", out var language) || language.GetString() != locale ||
                !root.TryGetValue("ruleVersion", out var rules) || rules.GetString() != SimulationRuleIdentity.ForLocale(locale) ||
                !root.TryGetValue("values", out var fields)) throw new InvalidDataException("city-domestic.catalog_profile");
            var values = Unique(fields);
            if (values.Count != Defaults.Length) throw new InvalidDataException("city-domestic.catalog_fields");
            var parsed = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (name, _) in Defaults)
            {
                if (!values.TryGetValue(name, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int integer))
                    throw new InvalidDataException("city-domestic.catalog_value." + name);
                parsed.Add(name, integer);
            }
            return new(locale, new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(parsed));
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or ArgumentException)
        { throw new InvalidDataException("city-domestic.catalog_format", error); }
    }
    private static Dictionary<string, JsonElement> Unique(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new InvalidDataException("city-domestic.catalog_object");
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
            if (!result.TryAdd(property.Name, property.Value)) throw new InvalidDataException("city-domestic.catalog_duplicate." + property.Name);
        return result;
    }
    internal static void RequireScenario(LoadedScenario scenario, string locale)
    {
        if (scenario.Metadata.FormatMarker != 0x66 || scenario.Snapshot.Locale != locale ||
            !System.IO.Path.GetFileName(scenario.Metadata.SourcePath).StartsWith("scedaexce", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("city-domestic.unsupported_source_profile");
    }
}

public sealed record LoadedCityDomesticParameters(CityDomesticParameterCatalog Catalog, SourceProvenance Source);
