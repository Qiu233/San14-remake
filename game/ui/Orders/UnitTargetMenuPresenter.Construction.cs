using San14.Core;

internal sealed partial class UnitTargetMenuPresenter
{
    internal IReadOnlyList<int> RetainedConstructionUnitIds => Selection.RetainedUnitIds;

    /// <summary>Retain source row order and each active route group while the
    /// definition/placement child owns the page. Selection is locked by its host.</summary>
    internal IReadOnlyList<ArmyFacilityConstructionSelection> CaptureConstructionSelection()
    {
        var rows = new List<ArmyFacilityConstructionSelection>();
        foreach (int id in SelectedUnitIds)
        {
            var group = Selection.Groups.FirstOrDefault(value => value.UnitIds.Contains(id))
                ?? throw new InvalidDataException("unit.construction.active_group");
            rows.Add(new(id, group.Points.Skip(1).ToArray()));
        }
        return rows.ToArray();
    }
}
