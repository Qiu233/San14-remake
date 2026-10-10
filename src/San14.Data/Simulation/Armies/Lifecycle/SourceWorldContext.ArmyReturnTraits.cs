namespace San14.Data;

/// <summary>Actual current Army64/AC/F4 words and nine descriptor pointers.
/// A null descriptor identity is a known native null; an absent whole record is unknown.</summary>
public sealed class ArmyReturnTraitFields
{
    public IReadOnlyList<ushort> Traits64 { get; }
    public IReadOnlyList<ushort> PersonsAC { get; }
    public IReadOnlyList<ushort> AuxiliaryF4 { get; }
    public IReadOnlyList<RegistryListHandle> Descriptors160 { get; }
    public ArmyReturnTraitFields(IEnumerable<ushort> traits64, IEnumerable<ushort> personsAC,
        IEnumerable<ushort> auxiliaryF4, IEnumerable<RegistryListHandle> descriptors160)
    {
        var a = traits64.ToArray(); var b = personsAC.ToArray(); var c = auxiliaryF4.ToArray(); var d = descriptors160.ToArray();
        if (a.Length != 36 || b.Length != 36 || c.Length != 36 || d.Length != 9 || d.Any(x => x is null))
            throw new InvalidDataException("Current Army trait windows must contain 36 word triples and nine known descriptor pointers.");
        Traits64 = Array.AsReadOnly(a); PersonsAC = Array.AsReadOnly(b); AuxiliaryF4 = Array.AsReadOnly(c); Descriptors160 = Array.AsReadOnly(d);
    }
    internal ArmyReturnTraitFields ClearSlot(int index)
    {
        var a = Traits64.ToArray(); var b = PersonsAC.ToArray(); var c = AuxiliaryF4.ToArray();
        a[index] = b[index] = c[index] = 0; return new(a, b, c, Descriptors160);
    }
}

public sealed partial class SourceWorldContext
{
    private readonly Dictionary<int, ArmyReturnTraitFields> _armyReturnTraits = [];
    public void BindArmyReturnTraitFields(int physical, ArmyReturnTraitFields fields)
    { Check(physical, 501, "return Army trait receiver"); ArgumentNullException.ThrowIfNull(fields); _armyReturnTraits[physical] = fields; }
    public void WriteArmyReturnTraitFields(int physical, ArmyReturnTraitFields fields)
    {
        BindArmyReturnTraitFields(physical, fields);
        _armyReturnImageChanges?.WriteTraits(physical, fields);
    }
    public ArmyReturnTraitFields? ReadArmyReturnTraitFields(ArmyUnitReference army)
        => _armyReturnTraits.GetValueOrDefault(MapPhysical(army.PhysicalSlot, 501, "return Army trait receiver"));

    internal void CopyArmyReturnTraitInputsTo(SourceWorldContext draft)
    {
        foreach (var row in _armyReturnTraits) draft._armyReturnTraits.Add(row.Key, row.Value);
        foreach (var row in _personTraitMasks1BC) draft._personTraitMasks1BC.Add(row.Key, row.Value);
        foreach (var row in _personTraitExtras1BE) draft._personTraitExtras1BE.Add(row.Key, row.Value);
    }
    internal void CommitArmyReturnTraitFields(SourceWorldContext draft)
    {
        Copy(draft._armyReturnTraits, _armyReturnTraits); Copy(draft._personTraitMasks1BC, _personTraitMasks1BC);
        Copy(draft._personTraitExtras1BE, _personTraitExtras1BE);
        static void Copy<K,V>(Dictionary<K,V> source, Dictionary<K,V> target) where K : notnull
        { target.Clear(); foreach (var row in source) target.Add(row.Key, row.Value); }
    }
    internal void UnlinkArmyReturnTraitNode(RegistryListHandle descriptor, RegistryListNodeReference node)
    {
        int key = RegistryKey(descriptor);
        if (NativeListKeyInRange(key)) _orderedLists.Remove(key, NodeIdentity(new(node.Identity)));
    }
}
