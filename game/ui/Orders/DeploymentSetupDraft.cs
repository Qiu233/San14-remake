using San14.Core;
using San14.Data;

/// <summary>The unsubmitted setup page owns these rows and its current pane's
/// tactic backup. All rule calculations remain in the campaign query adapter.</summary>
internal sealed class DeploymentSetupDraft(GameState world,int sourceLocationId,ArmyCampaignRuleAdapter rules)
{
    private IReadOnlyList<DeploymentDraftRow> rows=Array.AsReadOnly(Array.Empty<DeploymentDraftRow>());
    private DeploymentLandFormationEditState? edit;
    internal IReadOnlyList<DeploymentDraftRow> Rows=>rows;
    internal int? FocusedOfficerId {get;private set;}
    internal string? PaneUnresolvedInput {get;private set;}
    internal DeploymentDraftRow? WorkingRow
    {
        get
        {
            if(FocusedOfficerId is not int officer||edit is not { } pane)return null;
            var row=rows.Single(row=>row.Request.OfficerId==officer);
            return row with{Request=row.Request with{FormationId=pane.LandFormationId,
                TacticIds=Array.AsReadOnly(pane.Tactics.ToArray())}};
        }
    }

    internal void RestoreAcceptedRows(IReadOnlyList<DeploymentDraftRow> accepted)
    {
        // The city caller owns accepted Make packets independently of Target's
        // working copies. Restoring Make does not import Target routes/actions.
        rows=DeploymentCityDraft.CopyRows(accepted);
        FocusedOfficerId=rows.Count==0?null:rows[0].Request.OfficerId;InitializePane();
    }

    internal string? SelectOfficers(IReadOnlyList<int> selected)
    {
        var result=rules.QueryDeploymentDraft(world,sourceLocationId,selected,rows,FocusedOfficerId);
        if(result.Rows is null)return result.UnresolvedInput??"deployment.draft.unavailable";
        var next=result.Rows;
        rows=next;FocusedOfficerId=result.FocusedOfficerId;
        InitializePane();
        return null;
    }

    internal string? RemoveOfficer(int officer)=>SelectOfficers(rows.Where(row=>row.Request.OfficerId!=officer)
        .Select(row=>row.Request.OfficerId).ToArray());

    internal void FocusOfficer(int officer)
    {
        if(!rows.Any(row=>row.Request.OfficerId==officer))throw new ArgumentOutOfRangeException(nameof(officer));
        if(FocusedOfficerId==officer)return;
        FocusedOfficerId=officer;InitializePane();
    }

    internal string? SetTroops(int troops)=>ApplyEdit(edit?.LandFormationId,troops);
    internal string? SetLandFormation(int formation)=>ApplyEdit(formation,null);

    internal string? SetAllTroops(int troops)=>AcceptBatch(
        rules.ApplyDeploymentBatchTroops(world,rows,troops,FocusedOfficerId));
    internal string? MaximizeTroops()=>AcceptBatch(
        rules.ApplyDeploymentMaximumTroops(world,sourceLocationId,rows,FocusedOfficerId));

    internal string? ComposeAutomatically(DeploymentAutomaticStyle style)
    {
        var result=rules.QueryDeploymentAutomaticDraft(world,sourceLocationId,style);
        if(result.Rows is not { } replacement)return result.UnresolvedInput??"deployment.automatic.unavailable";
        // This caller replaces the complete parent packets. Its selected row
        // comes from the new final ordering, independently of the old pane.
        rows=replacement;FocusedOfficerId=result.FocusedOfficerId;InitializePane();return null;
    }

    private string? AcceptBatch(DeploymentDraftQuery result)
    {
        if(result.Rows is not { } replacement)return result.UnresolvedInput??"deployment.batch.unavailable";
        // Both bulk troop callers preserve original row order and focus. The
        // source rebinds the pane after writing troops, without editing tactics.
        rows=replacement;InitializePane();return null;
    }

    private string? ApplyEdit(int? formation,int? troops)
    {
        if(FocusedOfficerId is not int officer)
            throw new InvalidOperationException("A selected draft row is required.");
        if(edit is null||formation is not int nextFormation)return PaneUnresolvedInput??"deployment.land_edit.unavailable";
        var result=rules.QueryDeploymentLandFormationEdit(world,officer,edit,nextFormation);
        if(result.Value is not { } next)return result.UnresolvedInput??"deployment.land_edit.unavailable";
        rows=Array.AsReadOnly(rows.Select(row=>row.Request.OfficerId!=officer?row:row with
        {
            Request=row.Request with {FormationId=next.LandFormationId,TacticIds=Array.AsReadOnly(next.Tactics.ToArray()),
                Troops=troops??row.Request.Troops}
        }).ToArray());
        edit=next;PaneUnresolvedInput=null;return null;
    }

    private void InitializePane()
    {
        PaneUnresolvedInput=null;
        if(FocusedOfficerId is not int officer){edit=null;return;}
        var request=rows.Single(row=>row.Request.OfficerId==officer).Request;
        if(request.TacticIds.Count!=3)throw new InvalidDataException("A setup row must own three tactic slots.");
        var tactics=new DeploymentTacticSlots(request.TacticIds[0],request.TacticIds[1],request.TacticIds[2]);
        var initial=new DeploymentLandFormationEditState(request.FormationId,tactics,tactics,false);
        var result=rules.QueryDeploymentLandFormationEdit(world,officer,initial,request.FormationId);
        // Binding the source pane normalizes only its working copy. The
        // parent's row changes later, on an actual pane edit notification.
        edit=result.Value;PaneUnresolvedInput=result.UnresolvedInput;
    }
}
