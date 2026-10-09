using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using San14.Core;

namespace San14.Data;

public readonly record struct CityVisualOpeningRecord(int Slot, int ObjectSlot, int HexId,
    int SizeCode, int ForceSlot, int Durability, int DamageTier, int Variant, int Category);

/// <summary>A version and setting bound opening observation; never a capacity table.</summary>
public sealed class CityVisualOpeningEvidence
{
    public string EvidenceUri { get; }
    public string EvidenceSha256 { get; }
    public string CaptureSha256 { get; }
    public string ImplementationVersion { get; }
    public IReadOnlyDictionary<int, CityVisualOpeningRecord> Cities { get; }

    private CityVisualOpeningEvidence(string uri, string evidenceSha, string captureSha,
        string implementationVersion, Dictionary<int, CityVisualOpeningRecord> cities)
        => (EvidenceUri, EvidenceSha256, CaptureSha256, ImplementationVersion, Cities) =
            (uri, evidenceSha, captureSha, implementationVersion,
                new ReadOnlyDictionary<int, CityVisualOpeningRecord>(cities));

    public static CityVisualOpeningEvidence? LoadJson(string json, string evidenceUri,
        ScenarioCityPreview source, string scenarioSourceSha256, NewGameOptions? options,
        CeCityVariantTable variants)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(variants);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceUri);
        if (source.Number != 1 || source.Locale is not ("SC" or "TC") || options is null) return null;
        if (variants.Locale != source.Locale)
            throw new InvalidDataException("City opening observation and variant table locales differ.");
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1)
            throw new InvalidDataException("City opening visual observation schema changed.");
        if (!root.GetProperty("profiles").TryGetProperty(source.Locale, out JsonElement profile))
            return null;
        JsonElement settings = root.GetProperty("settings");
        if (options != new NewGameOptions(settings.GetProperty("difficultyId").GetString()!,
                settings.GetProperty("initialCreditEnabled").GetBoolean(),
                settings.GetProperty("capturedOfficersEnabled").GetBoolean())) return null;
        string uri = profile.GetProperty("scenarioUri").GetString()!;
        if (uri != GameResourceId.File(source.SourcePath).ToString() ||
            !scenarioSourceSha256.Equals(profile.GetProperty("scenarioSha256").GetString(),
                StringComparison.OrdinalIgnoreCase) ||
            root.GetProperty("capturedLocale").GetString() != source.Locale)
            throw new InvalidDataException("City opening visual observation differs from the active original source.");
        JsonElement date = root.GetProperty("observedDate");
        if (date.GetProperty("year").GetInt32() != 184 ||
            date.GetProperty("month").GetInt32() != 2 || date.GetProperty("day").GetInt32() != 1)
            throw new InvalidDataException("City opening observation has an unsupported captured date.");
        string captureHash = root.GetProperty("captureSha256").GetString()!;
        if (captureHash.Length != 64 || !captureHash.All(Uri.IsHexDigit))
            throw new InvalidDataException("City opening capture hash is malformed.");
        var cities = new Dictionary<int, CityVisualOpeningRecord>();
        foreach (JsonElement item in root.GetProperty("cities").EnumerateArray())
        {
            var record = new CityVisualOpeningRecord(item.GetProperty("slot").GetInt32(),
                item.GetProperty("objectSlot").GetInt32(), item.GetProperty("hexId").GetInt32(),
                item.GetProperty("sizeCode").GetInt32(), item.GetProperty("forceSlot").GetInt32(),
                item.GetProperty("openingDurability").GetInt32(), item.GetProperty("damageTier").GetInt32(),
                item.GetProperty("variant").GetInt32(), item.GetProperty("category").GetInt32());
            if (record.Slot is < 1 or > 51 || record.ObjectSlot != record.Slot ||
                record.SizeCode is < 1 or > 4 || record.Durability < 0 || record.DamageTier is < 0 or > 3 ||
                source.ObjectSourceRecords is null ||
                record.ObjectSlot >= source.ObjectSourceRecords.Objects.Count)
                throw new InvalidDataException("City opening visual observation has an invalid source object slot.");
            CityPreviewRecord[] matches = source.Cities.Where(city => city.Slot == record.Slot).ToArray();
            if (matches.Length != 1)
                throw new InvalidDataException($"City {record.Slot} is not unique in the source scenario.");
            CityPreviewRecord native = matches[0];
            var nativeObject = source.ObjectSourceRecords.Objects[record.ObjectSlot];
            if (native.HexId != record.HexId || native.SizeCode != record.SizeCode ||
                native.ForceSlot != record.ForceSlot || nativeObject.PhysicalSlot != record.ObjectSlot ||
                nativeObject.RawField10 != record.HexId || nativeObject.RawField12 != 1 ||
                nativeObject.RawField13 != record.ForceSlot || nativeObject.RawField14 != record.Durability ||
                variants.VariantForCitySlot(record.Slot) != record.Variant ||
                record.Category != CeCityModels.CategoryFor(record.Variant, record.SizeCode, record.DamageTier) ||
                !cities.TryAdd(record.Slot, record))
                throw new InvalidDataException($"City {record.Slot} opening visual observation differs from source.");
        }
        if (!cities.Keys.Order().SequenceEqual(Enumerable.Range(1, 51)))
            throw new InvalidDataException("City opening visual observation omits one of the 51 source cities.");
        return new(evidenceUri, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))),
            captureHash, variants.ImplementationVersion, cities);
    }

    public bool TryMatch(CityVisualState city, string implementationVersion,
        out CityVisualOpeningRecord record)
    {
        if (ImplementationVersion.Equals(implementationVersion, StringComparison.Ordinal) &&
            Cities.TryGetValue(city.Slot, out record) && record.HexId == city.HexId &&
            record.SizeCode == city.SizeCode && record.Durability == city.Durability &&
            (record.ForceSlot == 0 ? null : record.ForceSlot) == city.ForceId)
            return true;
        record = default;
        return false;
    }
}
