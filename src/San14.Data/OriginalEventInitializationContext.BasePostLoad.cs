using San14.Data.Formats;

namespace San14.Data;

public sealed partial class OriginalEventInitializationContext : IOriginalBasePostLoadState
{
    private readonly Dictionary<int, byte?> _citySizesB8 = [];
    private readonly Dictionary<OriginalBaseReference, ushort?> _baseFields46 = [];
    private readonly Dictionary<int, int?> _basePostLoadParameters = [];
    public SourceProvenance? BasePostLoadParameterSource { get; private set; }
    public Func<int, int?>? BasePostLoadParameterReader { get; set; }
    public int? BaseRuntimeTroopsExtra { get; set; }
    public Func<int?>? BaseRuntimeTroopsExtraReader { get; set; }
    public Func<OriginalForceReference, int, int?>? BaseForcePolicyEffectReader { get; set; }
    public Func<OriginalForceReference, int, int?>? BaseForceModifierReader { get; set; }
    public Func<bool?>? BaseCapacityPercentageEnabledReader { get; set; }
    public Func<int?>? BaseCapacityPercentageReader { get; set; }

    public byte? ReadCitySizeB8(OriginalBaseReference reference)
    {
        if (reference.Kind != OriginalBaseKind.City) throw new InvalidDataException("Gate receiver cannot provide a city sizeB8 field.");
        return _citySizesB8.GetValueOrDefault(BasePhysical(reference));
    }
    public void SetCitySizeB8(int physical, byte? value)
    { Check(physical, 52, "city size receiver"); _citySizesB8[physical] = value; }
    public ushort? ReadBaseField46(OriginalBaseReference reference)
    { _ = BasePhysical(reference); return _baseFields46.GetValueOrDefault(reference); }
    public void SetBaseField46(OriginalBaseReference reference, ushort? value)
    { _ = BasePhysical(reference); _baseFields46[reference] = value; }
    public void MaterializeBasePostLoadParameters(LoadedBaseParameters loaded)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        foreach (int rva in OriginalBaseParameterReader.ParameterRvas)
            _basePostLoadParameters[rva] = loaded.Catalog.RawValues.TryGetValue(rva, out int value) ? value
                : throw new InvalidDataException($"Original base parameter{rva:X} is missing from the loaded catalog.");
        BasePostLoadParameterSource = loaded.Source;
    }
    public int? ReadBasePostLoadParameter(int rva)
        => BasePostLoadParameterReader is null ? _basePostLoadParameters.GetValueOrDefault(rva) : BasePostLoadParameterReader(rva);
    public void SetBasePostLoadParameter(int rva, int? value) => _basePostLoadParameters[rva] = value;
    public int? ReadBaseRuntimeTroopsExtra() => BaseRuntimeTroopsExtraReader is null ? BaseRuntimeTroopsExtra : BaseRuntimeTroopsExtraReader();
    public int? ReadBaseForcePolicyEffect(OriginalForceReference force, int effect) => BaseForcePolicyEffectReader?.Invoke(force, effect);
    public int? ReadBaseForceModifier(OriginalForceReference force, int modifier) => BaseForceModifierReader?.Invoke(force, modifier);
    public bool? ReadBaseCapacityPercentageEnabled() => BaseCapacityPercentageEnabledReader?.Invoke();
    public int? ReadBaseCapacityPercentage() => BaseCapacityPercentageReader?.Invoke();

    public bool ReadSpecialBaseCapacity(OriginalBaseReference reference)
    {
        ushort link = StartupKnown(ReadBaseObjectLink(reference), "special capacity base4E");
        if (link > 51) return false;
        OriginalForceReference first = BasePostLoadForce(reference);
        int firstPhysical = StartupKnown(ReadForcePhysicalSlot(first), "special capacity force physical slot");
        if (unchecked((uint)(firstPhysical - 46)) > 4) return false;
        OriginalBaseReference related = OriginalCurrentGateMapQueries.FindCity(reference, this);
        int relatedPhysical = BasePhysical(related);
        int secondRawForce = StartupKnown(ReadBaseForceSlot(reference), "special capacity repeated raw force slot22");
        return relatedPhysical is >= 47 and <= 51 && secondRawForce == relatedPhysical - 1;
    }

    public void WriteObjectPostLoadField14(OriginalObjectReference reference, int input)
    {
        int physical = MapPhysical(reference.PhysicalSlot, 3001, "object field14 write receiver");
        int cap = 9999;
        if (physical is >= 1 and <= 51)
        {
            OriginalBaseReference city = ReadCityTableReference(physical)
                ?? throw new InvalidDataException("Current object field14 city pointer is unknown.");
            if (ReadSpecialBaseCapacity(city)) cap = StartupKnown(ReadBasePostLoadParameter(0x1A4BCB8), "special object field14 cap");
        }
        SetObjectRawField14(physical, unchecked((ushort)(input < 0 ? 0 : Math.Min(input, cap))));
    }
    public void PostProcessBase(OriginalBaseReference reference) => OriginalBasePostLoad.Apply(reference, this);
    private OriginalForceReference BasePostLoadForce(OriginalBaseReference reference)
    {
        int raw = StartupKnown(ReadBaseForceSlot(reference), "current base force slot22");
        if (raw < 0) throw new InvalidDataException("Negative base force index is unsafe for original pointer lookup.");
        return ReadForceTableReference(raw <= 51 ? raw : 0)
            ?? throw new InvalidDataException("Current base force pointer is unknown.");
    }
}
