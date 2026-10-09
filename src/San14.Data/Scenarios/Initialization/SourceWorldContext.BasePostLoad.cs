using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class SourceWorldContext : IBasePostLoadState
{
    private readonly Dictionary<int, byte?> _citySizesB8 = [];
    private readonly Dictionary<BaseReference, ushort?> _baseFields46 = [];
    private readonly Dictionary<string, int?> _basePostLoadParameters = [];
    public SourceProvenance? BasePostLoadParameterSource { get; private set; }
    public Func<string, int?>? BasePostLoadParameterReader { get; set; }
    public int? BaseRuntimeTroopsExtra { get; set; }
    public Func<int?>? BaseRuntimeTroopsExtraReader { get; set; }
    public Func<ForceReference, int, int?>? BaseForcePolicyEffectReader { get; set; }
    public Func<ForceReference, int, int?>? BaseForceModifierReader { get; set; }
    public Func<bool?>? BaseCapacityPercentageEnabledReader { get; set; }
    public Func<int?>? BaseCapacityPercentageReader { get; set; }

    public byte? ReadCitySizeB8(BaseReference reference)
    {
        if (reference.Kind != BaseKind.City) throw new InvalidDataException("Gate receiver cannot provide a city sizeB8 field.");
        return _citySizesB8.GetValueOrDefault(BasePhysical(reference));
    }
    public void SetCitySizeB8(int physical, byte? value)
    { Check(physical, 52, "city size receiver"); _citySizesB8[physical] = value; }
    public ushort? ReadBaseField46(BaseReference reference)
    { _ = BasePhysical(reference); return _baseFields46.GetValueOrDefault(reference); }
    public void SetBaseField46(BaseReference reference, ushort? value)
    { _ = BasePhysical(reference); _baseFields46[reference] = value; }
    public void MaterializeBasePostLoadParameters(LoadedBaseParameters loaded)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        foreach (string key in BaseParameterReader.ParameterKeys)
            _basePostLoadParameters[key] = loaded.Catalog.Values.TryGetValue(key, out int value) ? value
                : throw new InvalidDataException($"Original base parameter{key} is missing from the loaded catalog.");
        BasePostLoadParameterSource = loaded.Source;
    }
    public int? ReadBasePostLoadParameter(string key)
        => BasePostLoadParameterReader is null ? _basePostLoadParameters.GetValueOrDefault(key) : BasePostLoadParameterReader(key);
    public void SetBasePostLoadParameter(string key, int? value) => _basePostLoadParameters[key] = value;
    public int? ReadBaseRuntimeTroopsExtra() => BaseRuntimeTroopsExtraReader is null ? BaseRuntimeTroopsExtra : BaseRuntimeTroopsExtraReader();
    public int? ReadBaseForcePolicyEffect(ForceReference force, int effect) => BaseForcePolicyEffectReader?.Invoke(force, effect);
    public int? ReadBaseForceModifier(ForceReference force, int modifier) => BaseForceModifierReader?.Invoke(force, modifier);
    public bool? ReadBaseCapacityPercentageEnabled() => BaseCapacityPercentageEnabledReader?.Invoke();
    public int? ReadBaseCapacityPercentage() => BaseCapacityPercentageReader?.Invoke();

    public bool ReadSpecialBaseCapacity(BaseReference reference)
    {
        ushort link = StartupKnown(ReadBaseObjectLink(reference), "special capacity base4E");
        if (link > 51) return false;
        ForceReference first = BasePostLoadForce(reference);
        int firstPhysical = StartupKnown(ReadForcePhysicalSlot(first), "special capacity force physical slot");
        if (unchecked((uint)(firstPhysical - 46)) > 4) return false;
        BaseReference related = CurrentGateMapQueries.FindCity(reference, this);
        int relatedPhysical = BasePhysical(related);
        int secondRawForce = StartupKnown(ReadBaseForceSlot(reference), "special capacity repeated raw force slot22");
        return relatedPhysical is >= 47 and <= 51 && secondRawForce == relatedPhysical - 1;
    }

    public void WriteObjectPostLoadField14(ObjectReference reference, int input)
    {
        int physical = MapPhysical(reference.PhysicalSlot, 3001, "object field14 write receiver");
        int cap = 9999;
        if (physical is >= 1 and <= 51)
        {
            BaseReference city = ReadCityTableReference(physical)
                ?? throw new InvalidDataException("Current object field14 city pointer is unknown.");
            if (ReadSpecialBaseCapacity(city)) cap = StartupKnown(ReadBasePostLoadParameter(SimulationParameterKeys.BaseCapacity.MaximumDurability), "special object field14 cap");
        }
        SetObjectRawField14(physical, unchecked((ushort)(input < 0 ? 0 : Math.Min(input, cap))));
    }
    public void PostProcessBase(BaseReference reference) => BasePostLoad.Apply(reference, this);
    private ForceReference BasePostLoadForce(BaseReference reference)
    {
        int raw = StartupKnown(ReadBaseForceSlot(reference), "current base force slot22");
        if (raw < 0) throw new InvalidDataException("Negative base force index is unsafe for original pointer lookup.");
        return ReadForceTableReference(raw <= 51 ? raw : 0)
            ?? throw new InvalidDataException("Current base force pointer is unknown.");
    }
}
