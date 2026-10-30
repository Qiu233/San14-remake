using San14.Core;

namespace San14.Data.AI.Original;

/// <summary>Identity in the normal overseas-country definition table.</summary>
public readonly record struct OriginalAiTradeCountry(int Id);

public sealed partial class OriginalAiOperation
{
    private readonly Dictionary<uint, OriginalAiTradeCountry> tradeCountries = [];

    public bool TryTradeCountry(uint key, out OriginalAiTradeCountry value)
    {
        if (integers.ContainsKey(key) || entities.ContainsKey(key) || candidateLists.ContainsKey(key) ||
            candidateNullEntities.Contains(key) || TryHex(key, out _) || troops.ContainsKey(key))
        { value = default; return false; }
        return tradeCountries.TryGetValue(key, out value);
    }

    public OriginalAiNodeStatus SetTradeCountry(uint key, OriginalAiTradeCountry value)
    {
        integers.Remove(key); entities.Remove(key); candidateLists.Remove(key);
        candidateNullEntities.Remove(key); ClearRegionVariable(key); ClearHexVariable(key); ClearTroopsVariable(key); ClearHireOfferVariable(key); tradeCountries[key] = value;
        return OriginalAiNodeStatus.Success;
    }

    internal void ClearTradeCountryVariable(uint key) => tradeCountries.Remove(key);

    internal bool TryCopyTradeCountryVariable(uint source, uint destination)
    {
        if (!TryTradeCountry(source, out var value)) return false;
        SetTradeCountry(destination, value); return true;
    }

    internal bool TryCompareTradeCountryVariables(uint left, uint right, out bool equal)
    {
        bool hasLeft = TryTradeCountry(left, out var a), hasRight = TryTradeCountry(right, out var b);
        equal = hasLeft && hasRight && a == b;
        return hasLeft || hasRight;
    }

    internal OriginalAiNodeStatus Merchant(uint cityKey, int sourceMode, uint goldKey, uint foodKey, uint peopleKey)
    {
        if (!TryEntity(cityKey, out var city) || city.Kind != WorldEntityKind.City ||
            !TryCandidateList(peopleKey, WorldEntityKind.Officer, out var people) || people.Values.Count == 0)
            return OriginalAiNodeStatus.Failure;
        // 02D800 initializes both scalars to zero and ignores a missing scalar reader.
        TryInteger(goldKey, out int gold); TryInteger(foodKey, out int food);
        var prepared = Adapter.CreateOriginalAiMerchantCurrent(Current, city.Id, sourceMode, gold, food,
            people.Values.Select(person => person.Id).ToArray());
        if (!prepared.Success) return OriginalAiNodeStatus.Failure;
        return prepared.Order is { } order ? ApplyNativeCommand(order) : OriginalAiNodeStatus.Success;
    }

    internal OriginalAiNodeStatus Trade(uint countryKey, uint peopleKey, uint goldKey, uint foodKey, uint troopsKey)
    {
        if (!TryTradeCountry(countryKey, out var country) ||
            !TryCandidateList(peopleKey, WorldEntityKind.Officer, out var people)) return OriginalAiNodeStatus.Failure;
        // 053DB0 likewise permits absent scalar values through its initialized zero locals.
        TryInteger(goldKey, out int gold); TryInteger(foodKey, out int food); TryInteger(troopsKey, out int troops);
        var prepared = Adapter.CreateOriginalAiForeignTradeCurrent(Current, InputsCurrent, Actor, country.Id,
            people.Values.Select(person => person.Id).ToArray(), gold, food, troops);
        if (!prepared.Success) return OriginalAiNodeStatus.Failure;
        return prepared.Order is { } order ? ApplyNativeCommand(order) : OriginalAiNodeStatus.Success;
    }
}
