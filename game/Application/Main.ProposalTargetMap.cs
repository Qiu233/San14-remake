using Godot;

public partial class Main
{
    private MapInformationReturnView? _proposalTargetMap;
    private bool _proposalTargetPriorFocus;

    private void OpenProposalTargetMap(int hex)
    {
        if (!CanEditProposals || _repository is null || _proposalsView is not { } parent || _proposalMap is not { } map) return;
        //452B10(hex,1,500,-1,-1,1) is the same source focus request already
        //used by the commander inspector. No ruler or entity is substituted.
        var view = new MapInformationReturnView();
        if (!map.BeginInformationMap(view, hex)) { view.Free(); ProposalUnavailable("proposal.detail.map_camera"); return; }
        _proposalTargetMap = view; _proposalTargetPriorFocus = _mapFocused;
        AddChild(view);
        try
        {
            view.Configure(_repository, _ui.SourceLocale,
                () => _proposalTargetMap == view && IsProposalsCurrent,
                () => RunUiAction(() => CloseProposalTargetMap()));
            view.SetMeta("target_hex", hex);
            parent.Hide(); parent.ProcessMode = ProcessModeEnum.Disabled;
            map.SetDetailsOpen(false); SetMapFocus(true); HideDeploymentHud();
        }
        catch { CloseProposalTargetMap(); throw; }
    }

    private void CloseProposalTargetMap(bool resume = true)
    {
        var view = _proposalTargetMap; _proposalTargetMap = null;
        if (view is not null && _proposalMap is { } map && GodotObject.IsInstanceValid(map)) map.EndInformationMap(view);
        if (view is not null) { view.Retire(); view.GetParent()?.RemoveChild(view); view.QueueFree(); }
        if (view is not null && resume && IsProposalsCurrent && _proposalsView is { } parent)
        {
            _proposalMap!.SetDetailsOpen(true, keepMapVisible: true);
            SetMapFocus(_proposalTargetPriorFocus);
            parent.ProcessMode = ProcessModeEnum.Inherit; parent.Show();
        }
    }
}
