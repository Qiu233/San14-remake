using San14.Core;

namespace San14.Data;

public sealed record CityVisualEvidence(string Uri, string Sha256, string Basis)
{
    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(Uri) || string.IsNullOrWhiteSpace(Basis) ||
            Sha256.Length != 64 || !Sha256.All(System.Uri.IsHexDigit))
            throw new InvalidDataException("City visual input requires its evidence URI, SHA-256 and value basis.");
    }
}

/// <summary>
/// Explicit current inputs. Capacity means the final capacity for this city,
/// owner, size and rule state, never the source City+46 base value.
/// </summary>
public sealed record CityVisualDamageInput(int? DamageTier, int? Capacity,
    CityVisualEvidence Evidence)
{
    public static CityVisualDamageInput FromTier(int tier, CityVisualEvidence evidence)
        => new(tier, null, evidence);
    public static CityVisualDamageInput FromCapacity(int capacity, CityVisualEvidence evidence)
        => new(null, capacity, evidence);
}

public readonly record struct CityVisualState(int Slot, int? HexId, int? SizeCode,
    int? ForceId, int? Durability)
{
    public static CityVisualState FromCity(CityState city) => new(city.Id, city.HexId,
        city.Size switch
        {
            CitySize.Small => 1, CitySize.Medium => 2, CitySize.Large => 3,
            CitySize.Giant => 4, _ => null
        }, city.ForceId, city.Durability);
}

public sealed record CityVisualSelection(int Slot, int HexId, int SizeCode,
    int? ForceId, int Variant, int DamageTier, int Category,
    string ImplementationVersion, CityVisualEvidence Evidence, string SelectionBasis)
{
    public string? RuntimeCaptureSha256 { get; init; }
    public bool CurrentCategoryObserved { get; init; }
    // The verified native category selector consumes slot/size/damage, with no
    // season argument. This does not prove seasonal materials or shaders equal.
    public bool GeometryCategoryUsesSeason => false;
    public bool SeasonalAppearanceVerified => false;
}

public sealed record CityVisualSelectionResult(CityVisualSelection? Selection,
    string? UnavailableReason)
{
    public bool Available => Selection is not null;
}

/// <summary>Source category selection independent of rendering, commands and saves.</summary>
public static class CityVisualSelector
{
    public static CityVisualSelectionResult Select(CeCityVariantTable variants,
        CityVisualState city, CityVisualDamageInput? currentDamage = null,
        CityVisualOpeningEvidence? opening = null, bool isSourceOpeningFrame = false)
    {
        ArgumentNullException.ThrowIfNull(variants);
        if (city.Slot is < 1 or > 51) return Missing("unsupported-city-slot");
        if (city.HexId is not int hex || (uint)hex >= 220 * 220)
            return Missing("city-hex-unresolved");
        if (city.SizeCode is not int size || size is < 1 or > 4)
            return Missing("city-size-unresolved");

        int tier;
        string basis;
        CityVisualEvidence evidence;
        string? capture = null;
        bool observed = false;
        if (currentDamage is not null)
        {
            currentDamage.Evidence.Validate();
            if (currentDamage.DamageTier is int supplied && supplied is < 0 or > 3 ||
                currentDamage.Capacity is int maximum && maximum <= 0)
                throw new InvalidDataException($"City {city.Slot}: invalid explicit visual damage input.");
            int? derived = currentDamage.Capacity is int capacity && city.Durability is int durability
                ? CeCityModels.DamageTier(durability, capacity) : null;
            if (currentDamage.DamageTier is int explicitTier)
            {
                if (derived is int derivedTier && derivedTier != explicitTier)
                    throw new InvalidDataException($"City {city.Slot}: damage tier and current capacity disagree.");
                tier = explicitTier;
                basis = "explicit-current-damage-tier";
            }
            else if (derived is int derivedTier)
            {
                tier = derivedTier;
                basis = "current-durability-and-explicit-current-capacity";
            }
            else return Missing("current-damage-input-incomplete");
            evidence = currentDamage.Evidence;
        }
        else if (opening is not null && opening.TryMatch(city, variants.ImplementationVersion,
                     out CityVisualOpeningRecord record))
        {
            tier = record.DamageTier;
            basis = "matching-original-opening-display-descriptor";
            evidence = new(opening.EvidenceUri, opening.EvidenceSha256, basis);
            capture = opening.CaptureSha256;
            observed = isSourceOpeningFrame;
        }
        else return Missing("current-damage-tier-and-capacity-unresolved");

        int variant = variants.VariantForCitySlot(city.Slot);
        return new(new(city.Slot, hex, size, city.ForceId, variant, tier,
            CeCityModels.CategoryFor(variant, size, tier), variants.ImplementationVersion,
            evidence, basis)
        { RuntimeCaptureSha256 = capture, CurrentCategoryObserved = observed }, null);
    }

    private static CityVisualSelectionResult Missing(string reason) => new(null, reason);
}
