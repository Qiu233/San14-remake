using San14.Data.Formats;

namespace San14.Data;

public sealed partial class OriginalEventInitializationContext
{
    private sealed class PolicyEffectValues
    {
        public ushort?[] Raw = new ushort?[10];
        public List<int> Encoded = [];
        public int Capacity;
    }
    private readonly Dictionary<int, PolicyEffectValues> _currentPolicyEffectValues = [];
    public SourceProvenance? PolicyEffectDefinitionSource { get; private set; }
    public string? PolicyEffectDefinitionDecodedSha256 { get; private set; }
    public Func<int, int, bool?>? PolicyEffectVectorReservation { get; set; }

    /// <summary>Explicit normal successful220B30 construction/226E30 reset.
    /// Pointer-table allocation and loading remain separate stages.</summary>
    public void InitializePolicyEffectStorage()
    { for (int physical = 0; physical < 101; physical++) ResetPolicyEffectValues(physical); }
    public void ResetPolicyEffectValues(int physical)
    {
        Check(physical, 101, "policy effect reset receiver");
        var values = new PolicyEffectValues();
        Array.Fill(values.Raw, (ushort?)0);
        _currentPolicyEffectValues[physical] = values;
        ReservePolicyEffectVector(physical, values, 10);
    }
    public void MaterializeFixedPolicyEffects(LoadedPolicies loaded)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        S14PolicyCatalog catalog = loaded.Catalog;
        if (catalog.FormatMarker != 0x66 || catalog.EffectRecordStride != 134 || catalog.Effects.Count != 101 ||
            catalog.Effects.Where((row, slot) => row.Slot != slot || row.RawBytes.Length != 134 || row.RawValues.Count != 10).Any())
            throw new InvalidDataException("Current policy effect source requires the complete inspected CE101x134 profile.");
        foreach (S14PolicyEffectDefinition row in catalog.Effects)
        {
            OriginalStaticTableReference reference = ReadStaticTableReference(0x80880, row.Slot)
                ?? throw new InvalidDataException("Current fixed policy effect table pointer is unknown.");
            int physical = MapPhysical(reference.PhysicalSlot, 101, "fixed policy effect receiver");
            PolicyEffectValues values = PolicyEffectValuesAt(physical);
            for (int index = 0; index < 10; index++)
            {
                ushort raw = row.RawValues[index];
                ushort mixed = (ushort)(raw ^ (ushort)physical);
                int encoded = (ushort)((mixed >> 11) | (mixed << 5));
                if (values.Encoded.Count == values.Capacity)
                    ReservePolicyEffectVector(physical, values, Math.Max(checked(values.Encoded.Count + 1), checked(values.Capacity + values.Capacity / 2)));
                values.Encoded.Add(encoded);
                //23E5A0 writes the scalar only after the corresponding append.
                values.Raw[index] = raw;
            }
        }
        PolicyEffectDefinitionSource = loaded.Source;
        PolicyEffectDefinitionDecodedSha256 = catalog.DecodedSha256;
    }
    public int ReadCurrentPolicyEffectValue(int physical, int level)
    {
        PolicyEffectValues values = PolicyEffectValuesAt(physical);
        // Preserve LEA wrap before the signed clamp, including int.MinValue.
        int index = Math.Clamp(unchecked(level - 1), 0, 9);
        if (values.Encoded.Count == 10)
        {
            ushort encoded = unchecked((ushort)values.Encoded[index]);
            ushort rotated = (ushort)((encoded << 11) | (encoded >> 5));
            return (ushort)(rotated ^ (ushort)physical);
        }
        return values.Raw[index] ?? throw new InvalidDataException("Current policy effect raw word82 is unknown.");
    }
    public void PostProcessPolicyEffect(int physical)
    {
        PolicyEffectValues values = PolicyEffectValuesAt(physical);
        for (int index = 0; index < 10; index++)
            values.Raw[index] = (ushort)Math.Clamp(ReadCurrentPolicyEffectValue(physical, index + 1), 0, 1000);
    }
    public ushort? ReadPolicyEffectRawWord(int physical, int index)
    { Check(index, 10, "policy effect raw value index"); return PolicyEffectValuesAt(physical).Raw[index]; }
    public void SetPolicyEffectRawWord(int physical, int index, ushort? value)
    { Check(index, 10, "policy effect raw value index"); PolicyEffectValuesAt(physical).Raw[index] = value; }
    public IReadOnlyList<int> ReadPolicyEffectEncodedValues(int physical) => PolicyEffectValuesAt(physical).Encoded.AsReadOnly();
    public int ReadPolicyEffectVectorCapacity(int physical) => PolicyEffectValuesAt(physical).Capacity;
    private PolicyEffectValues PolicyEffectValuesAt(int physical)
    {
        Check(physical, 101, "policy effect current receiver");
        return _currentPolicyEffectValues.GetValueOrDefault(physical)
            ?? throw new InvalidDataException("Current policy effect98 vector/raw storage is unknown; construction must precede loading.");
    }
    private void ReservePolicyEffectVector(int physical, PolicyEffectValues values, int capacity)
    {
        if (PolicyEffectVectorReservation is not null && !(PolicyEffectVectorReservation(physical, capacity)
            ?? throw new InvalidDataException("Current policy effect vector reservation outcome is unknown.")))
            throw new InvalidDataException("Current policy effect vector reservation failed.");
        var replacement = new List<int>(capacity);
        replacement.AddRange(values.Encoded);
        values.Encoded = replacement;
        values.Capacity = capacity;
    }
}
