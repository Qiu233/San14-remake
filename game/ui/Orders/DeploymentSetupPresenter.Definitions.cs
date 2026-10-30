using Godot;
using San14.Data;

internal sealed partial class DeploymentSetupPresenter
{
    private DeploymentUnitDefinitionSelectionView? definitionModal;
    private bool definitionIsTactic;
    internal bool IsFormationDefinitionOpen => !disposed && !definitionIsTactic && definitionModal?.IsVisibleInTree() == true;
    internal bool IsTacticDefinitionOpen => !disposed && definitionIsTactic && definitionModal?.IsVisibleInTree() == true;
    internal long FormationDefinitionAcceptedSequence { get; private set; }
    internal long TacticDefinitionAcceptedSequence { get; private set; }
    internal long DefinitionSelectionCancelledSequence { get; private set; }
    internal DeploymentDraftRow? LastAcceptedTacticDefinitionRow { get; private set; }

    private void OpenFormationDefinitions() => OpenDefinitionSelection(tactic: false);
    private void OpenTacticDefinitions() => OpenDefinitionSelection(tactic: true);

    private void OpenDefinitionSelection(bool tactic)
    {
        if (disposed || definitionModal is not null || modal is not null || batchCalculator is not null ||
            automaticMenu is not null || view.UnitInformation.HasCalculator ||
            !view.UnitInformation.IsVisibleInTree() || !view.UnitInformation.CanProcess() ||
            Draft.FocusedOfficerId is not int officer || Draft.WorkingRow is not { } working) return;
        var query = tactic
            ? rules.PrepareDeploymentTacticSelection(world, officer, working.Request.FormationId, working.Request.TacticIds)
            : rules.PrepareDeploymentFormationSelection(world, officer, working.Request.FormationId);
        if (query.Value is not { } model)
        { unavailable(query.UnresolvedInput ?? "deployment.definition_selection.unavailable"); return; }
        var child = new DeploymentUnitDefinitionSelectionView();
        definitionModal = child; definitionIsTactic = tactic;
        view.AddChild(child);
        try
        {
            view.BeginDefinitionSelection(child);
            RefreshDecisionAdmission();
            child.Configure(repository, text, model,
                () => !disposed && ReferenceEquals(definitionModal, child) &&
                    GodotObject.IsInstanceValid(view) && !view.IsQueuedForDeletion() && view.IsVisibleInTree() && view.CanProcess(),
                selected => CompleteDefinitionSelection(child, officer, tactic, selected), () => scrollSpeed?.Invoke() ?? 0);
        }
        catch
        {
            RetireDefinitionSelection(child);
            if (!disposed && GodotObject.IsInstanceValid(view) && !view.IsQueuedForDeletion()) RefreshDecisionAdmission();
            throw;
        }
    }

    private void CompleteDefinitionSelection(DeploymentUnitDefinitionSelectionView child, int officer,
        bool tactic, IReadOnlyList<int>? selected)
    {
        if (disposed || !ReferenceEquals(definitionModal, child)) return;
        RetireDefinitionSelection(child);
        if (selected is null)
        {
            DefinitionSelectionCancelledSequence++;
            RefreshDecisionAdmission();
            return;
        }
        if (Draft.FocusedOfficerId != officer || Draft.WorkingRow is null)
        { RefreshDecisionAdmission(); unavailable("deployment.definition_selection.current_draft_row"); return; }
        var previous = Draft.Rows;
        string? error;
        if (tactic) error = Draft.SetTactics(selected);
        else if (selected.Count == 1) error = Draft.SetLandFormation(selected[0]);
        else error = "deployment.definition_selection.single_formation";
        if (error is not null)
        {
            RefreshAll(); unavailable(error); return;
        }
        if (tactic)
        {
            LastAcceptedTacticDefinitionRow = Draft.Rows.Single(row => row.Request.OfficerId == officer);
            TacticDefinitionAcceptedSequence++;
        }
        else
        {
            LastAcceptedLandFormationRow = Draft.Rows.Single(row => row.Request.OfficerId == officer);
            LandFormationAcceptedSequence++; FormationDefinitionAcceptedSequence++;
        }
        RecordSettingsChange(previous); RefreshAll();
    }

    // Parent visibility belongs to the same real child. Configure failure,
    // normal return and parent disposal all use this single retirement path.
    private void RetireDefinitionSelection(DeploymentUnitDefinitionSelectionView? expected = null)
    {
        if (definitionModal is not { } child || expected is not null && !ReferenceEquals(expected, child)) return;
        definitionModal = null; definitionIsTactic = false;
        if (GodotObject.IsInstanceValid(child))
        {
            child.Abandon();
            if (GodotObject.IsInstanceValid(view)) view.EndDefinitionSelection(child);
            if (!child.IsQueuedForDeletion()) child.QueueFree();
        }
        else if (GodotObject.IsInstanceValid(view)) view.EndDefinitionSelection(child);
    }
}
