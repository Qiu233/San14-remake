namespace San14.Data;

public sealed record OriginalArmyReturnResourceFields(ushort? Ready16, ushort? Wounded18, byte? Morale1A,
    int? Gold5C, int? Supplies60);
public sealed partial class OriginalEventInitializationContext
{
    private readonly Dictionary<int, OriginalArmyReturnResourceFields> _armyReturnResourceFields = [];
    private readonly Dictionary<OriginalBaseReference, int?> _armyReturnBaseWounded40 = [];
    public void SetArmyReturnResourceFields(int physical, OriginalArmyReturnResourceFields fields)
    { Check(physical, 501, "return Army resources"); ArgumentNullException.ThrowIfNull(fields); _armyReturnResourceFields[physical] = fields; }
    public OriginalArmyReturnResourceFields? ReadArmyReturnResourceFields(OriginalArmyUnitReference army) =>
        _armyReturnResourceFields.GetValueOrDefault(MapPhysical(army.PhysicalSlot, 501, "return Army resources"));
    public void SetArmyReturnBaseWounded40(OriginalBaseReference target, int? value)
    { _ = BasePhysical(target); _armyReturnBaseWounded40[target] = value; }
    public int? ReadArmyReturnBaseWounded40(OriginalBaseReference target)
    { _ = BasePhysical(target); return _armyReturnBaseWounded40.GetValueOrDefault(target); }

    internal void CopyArmyReturnResourceInputsTo(OriginalEventInitializationContext draft)
    {
        Copy(_armyReturnResourceFields, draft._armyReturnResourceFields); Copy(_armyReturnBaseWounded40, draft._armyReturnBaseWounded40);
        Copy(_baseResources, draft._baseResources); Copy(_baseMorale44, draft._baseMorale44);
        Copy(_citySizesB8, draft._citySizesB8); Copy(_baseFields46, draft._baseFields46);
        Copy(_basePostLoadParameters, draft._basePostLoadParameters);
        draft.BasePostLoadParameterSource = BasePostLoadParameterSource;
        draft.BasePostLoadParameterReader = BasePostLoadParameterReader; draft.BaseRuntimeTroopsExtra = BaseRuntimeTroopsExtra;
        draft.BaseRuntimeTroopsExtraReader = BaseRuntimeTroopsExtraReader;
        draft.BaseForcePolicyEffectReader = BaseForcePolicyEffectReader; draft.BaseForceModifierReader = BaseForceModifierReader;
        draft.BaseCapacityPercentageEnabledReader = BaseCapacityPercentageEnabledReader;
        draft.BaseCapacityPercentageReader = BaseCapacityPercentageReader;
        static void Copy<TKey, TValue>(Dictionary<TKey, TValue> from, Dictionary<TKey, TValue> to) where TKey : notnull
        { foreach (var value in from) to[value.Key] = value.Value; }
    }
}
