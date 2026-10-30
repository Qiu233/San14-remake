using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public AreaAdministrationQuery<DeploymentDefinitionSelection> PrepareDeploymentFormationSelection(
        GameState state, int officerId, int selectedFormationId)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            var choices = QueryDeploymentLandFormationChoices(state, officerId);
            if (choices.Choices is not { } rows) throw new InvalidDataException(choices.UnresolvedInput);
            var current = CaptureOfficerCommandCapacityCurrent(state);
            int physical = _definitions.ResolveOfficerSource(officerId) ?? throw DeploymentDefinitionMissing("person_identity");
            if (current.ReadPersonIdentity(physical) != officerId) throw DeploymentDefinitionMissing("current_person_identity");
            return AreaAdministrationQuery<DeploymentDefinitionSelection>.Known(
                new FormationInformationCurrent(this, state).DeploymentPages(state, current, physical,
                    rows.Select(row => row.FormationId).ToArray(), selectedFormationId));
        }
        catch (InvalidDataException error) { return AreaAdministrationQuery<DeploymentDefinitionSelection>.Unknown(error.Message); }
    }

    public AreaAdministrationQuery<DeploymentDefinitionSelection> PrepareDeploymentTacticSelection(
        GameState state, int officerId, int landFormationId, IReadOnlyList<int> selectedTactics)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(selectedTactics);
        try
        {
            var choices = QueryDeploymentTacticChoices(state, officerId, landFormationId, selectedTactics);
            if (choices.Choices is not { } rows) throw new InvalidDataException(choices.UnresolvedInput);
            var current = CaptureOfficerCommandCapacityCurrent(state);
            int force = state.Officers.GetValueOrDefault(officerId)?.ForceId ?? throw DeploymentDefinitionMissing("person_force_owner");
            return AreaAdministrationQuery<DeploymentDefinitionSelection>.Known(
                new DefinitionInformationCurrent(this, state, force, current).DeploymentTacticPages(rows));
        }
        catch (InvalidDataException error) { return AreaAdministrationQuery<DeploymentDefinitionSelection>.Unknown(error.Message); }
    }

    /// <summary>The actual child accept clears three local slots, copies the
    /// checked body order, then normalizes once. Cancel never calls this.</summary>
    public DeploymentLandFormationEditResult QueryDeploymentTacticSelectionEdit(GameState state,
        int officerId, DeploymentLandFormationEditState previous, IReadOnlyList<int> selectedTactics)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(selectedTactics);
        try
        {
            if (selectedTactics.Count > 3) throw DeploymentDefinitionMissing("maximum_three_tactics");
            var choices = QueryDeploymentTacticChoices(state, officerId, previous.LandFormationId, previous.Tactics.ToArray());
            if (choices.Choices is not { } rows) throw new InvalidDataException(choices.UnresolvedInput);
            // Disabled and preselected are independent source lists. Retain
            // an existing disabled check; no newly disabled occurrence can
            // enter this child result.
            var remaining = rows.Where(row => row.Compatible || row.Selected).GroupBy(row => row.TacticId).ToDictionary(group => group.Key, group => group.Count());
            foreach (int id in selectedTactics)
            {
                if (!remaining.TryGetValue(id, out int count) || count == 0) throw DeploymentDefinitionMissing("tactic_caller_member");
                remaining[id] = count - 1;
            }
            int[] slots = new int[3]; for (int index = 0; index < selectedTactics.Count; index++) slots[index] = selectedTactics[index];
            var current = CaptureOfficerCommandCapacityCurrent(state);
            DeploymentTacticCurrentInputs.BindCurrent(state, current);
            var tactics = DeploymentTacticSelectionSource.NormalizeThree(current, DeploymentTacticSlots.From(slots));
            return new(previous with { Tactics = tactics, BackupTactics = unchecked((uint)(previous.LandFormationId - 8)) > 2 ? tactics : previous.BackupTactics }, null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    private static InvalidDataException DeploymentDefinitionMissing(string field) => new("deployment.definition_selection." + field);

    private sealed partial class FormationInformationCurrent
    {
        internal DeploymentDefinitionSelection DeploymentPages(GameState state, SourceWorldContext current, int physical, int[] ids, int selected)
        {
            var groups = FormationInformationColumnsSource.Groups.Select((group, index) =>
                new DefinitionInformationGroup(index, index == 0 ? 0x3CD : group.CaptionId,
                    Data(index == 0 ? 0x3CD : group.CaptionId), group.ColumnIds.Select(id =>
                    {
                        var row = FormationInformationColumnsSource.Rows[id];
                        return new DefinitionInformationColumn(id, row.CaptionId, Data(row.CaptionId), row.Width,
                            (row.Flags & 1) != 0 ? 3 : (row.Flags & 2) != 0 ? 5 : 4,
                            (row.Flags & 0x20000000) == 0, true, true, row.SortMode != 2);
                    }).ToImmutableArray())).ToImmutableArray();
            var thresholds = new Dictionary<int, (int First, int Second)>();
            PreparedCell Read(int id, int column)
            {
                if (column is not (10 or 11)) return Cell(id, column);
                try
                {
                    if (!thresholds.TryGetValue(id, out var value))
                    {
                        var read = DeploymentTroopClassificationSource.Read(state, current, physical, id, 0);
                        thresholds.Add(id, value = (read.PrimaryThreshold, read.SecondaryThreshold));
                    }
                    int key = column == 10 ? value.First : value.Second;
                    return new(new(key == 0 ? "" : Number(key), null, null), key, key == 0, null);
                }
                catch (InvalidDataException error) { return new(new(null, null, error.Message), 0, false, error.Message); }
            }
            int selectedSlot = Array.IndexOf(ids, selected);
            return new(0x5FD, groups, ids, ids.Select(_ => true).ToArray(), selectedSlot < 0 ? [] : [selectedSlot], false,
                (id, column) => { var value = Read(id, column); return new(value.Display.Text, value.Display.PresetIndex, value.Display.UnresolvedOwner) { SortUnresolvedOwner = value.Missing }; },
                (id, column) => Read(id, column).Missing,
                (left, right, column, ascending) => Before(Read(left, column), Read(right, column), ascending));
        }
    }

    private sealed partial class DefinitionInformationCurrent
    {
        internal DefinitionInformationCurrent(ArmyCampaignRuleAdapter rules, GameState state, int playerForceId, SourceWorldContext current)
        {
            this.rules = rules; this.state = state; this.playerForceId = playerForceId; ownOnly = false;
            captured = new(() => current);
        }
        internal DeploymentDefinitionSelection DeploymentTacticPages(IReadOnlyList<DeploymentTacticChoice> choices)
        {
            var profile = TacticInformationColumnsSource.Profile;
            var groups = profile.Groups.Select((group, index) => new DefinitionInformationGroup(index,
                index == 0 ? 0x3CD : group.CaptionId, Data(1, index == 0 ? 0x3CD : group.CaptionId),
                group.ColumnIds.Select(id =>
                {
                    var row = profile.Rows[id];
                    return new DefinitionInformationColumn(id, row.CaptionId, Data(1, row.CaptionId), row.Width,
                        (row.Flags & 1) != 0 ? 3 : (row.Flags & 2) != 0 ? 5 : 4,
                        (row.Flags & 0x20000000) == 0, true, row.Visible, row.SortMode != 2);
                }).ToImmutableArray())).ToImmutableArray();
            return new(0x5BF, groups, choices.Select(row => row.TacticId).ToArray(),
                choices.Select(row => row.Compatible).ToArray(), choices.Select((row, index) => (row, index)).Where(item => item.row.Selected).Select(item => item.index).ToArray(), true,
                (id, column) => Cell(InformationListKind.Tactics, id, column).Display,
                (id, column) => Cell(InformationListKind.Tactics, id, column).SortUnresolvedOwner,
                (left, right, column, ascending) => (Cell(InformationListKind.Tactics, left, column).Key ?? throw Missing("sort_key"))
                    .Before(Cell(InformationListKind.Tactics, right, column).Key ?? throw Missing("sort_key"), ascending));
        }
    }
}
