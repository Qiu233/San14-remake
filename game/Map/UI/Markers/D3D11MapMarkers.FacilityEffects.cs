using San14.Core;
using San14.Data;

internal partial class D3D11MapMarkers
{
    internal IReadOnlyDictionary<int, ArmyFireViewSnapshot?>? CaptureArmyRenderViews(
        IReadOnlyList<D3D11ArmyLabelUnitPosition> positions) => _armyLabels?.CaptureArmyRenderViews(positions);
    internal void AdvanceFacilityDamageFeedback(uint mode) => _armyLabels?.AdvanceFacilityDamageFeedback(mode);
    internal bool TryStartFacilityDamage(ArmyReceiverId receiver, int gross, sbyte modifier, out string? missing)
    {
        missing = "facility.damage.info_controller";
        return _armyLabels is not null && _armyLabels.TryStartFacilityDamage(receiver, gross, modifier, out missing);
    }
    internal bool TryDeferFacilityDanger(ArmyReceiverId receiver, out string? missing)
    {
        missing = "facility.danger.info_controller";
        return _armyLabels is not null && _armyLabels.TryDeferFacilityDanger(receiver, out missing);
    }
}
