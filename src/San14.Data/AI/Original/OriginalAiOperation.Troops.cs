namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    //Data-only CTroopsData identity. It does not alias a Core District or Army.
    private readonly Dictionary<uint, OriginalAiTroopsReference> troops = [];

    public bool TryTroops(uint key, out OriginalAiTroopsReference value)
    {
        if (integers.ContainsKey(key) || entities.ContainsKey(key) ||
            candidateLists.ContainsKey(key) || candidateNullEntities.Contains(key) ||
            hexes.ContainsKey(key) || tradeCountries.ContainsKey(key))
        { value = default; return false; }
        return troops.TryGetValue(key, out value);
    }

    public OriginalAiNodeStatus SetTroops(uint key, OriginalAiTroopsReference value)
    {
        integers.Remove(key); entities.Remove(key); candidateLists.Remove(key);
        ClearCandidateNull(key); troops[key] = value;
        return OriginalAiNodeStatus.Success;
    }

    internal void ClearTroopsVariable(uint key) => troops.Remove(key);

    internal bool TryCopyTroopsVariable(uint source, uint destination)
    {
        if (!TryTroops(source, out var value)) return false;
        SetTroops(destination, value); return true;
    }

    internal bool TryCompareTroopsVariables(uint left, uint right, out bool equal)
    {
        bool hasLeft = TryTroops(left, out var a), hasRight = TryTroops(right, out var b);
        equal = hasLeft && hasRight && a == b;
        return hasLeft || hasRight;
    }

    internal void ReplaceTroopsCandidates(uint key, IReadOnlyList<OriginalAiTroopsReference> values)
    {
        var list = GetOrCreateTypedCandidates(key, CandidateListKind.Troops);
        var incoming = ReferenceEquals(values, list.TroopsValues) ? values.ToArray() : values;
        list.TroopsValues.Clear(); list.TroopsValues.AddRange(incoming);
    }
}
