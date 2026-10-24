using Godot;
using San14.Core;
using San14.Data;

public partial class Main
{
    private sealed class FalseReportTargetPage(ArmyFalseReportTargetView view, MapSceneView map,
        Control parent, ArmyFalseReportTargetQuery query, Func<bool> current, Action resume, Action<int> accepted)
    {
        internal readonly ArmyFalseReportTargetView View = view;
        internal readonly MapSceneView Map = map;
        internal readonly Control Parent = parent;
        internal readonly ArmyFalseReportTargetQuery Query = query;
        internal readonly Func<bool> Current = current;
        internal readonly Action Resume = resume;
        internal readonly Action<int> Accepted = accepted;
        internal int? PreviewHex;
        internal DeploymentDraftRouteSegment? PreviewRoute;
    }
    private FalseReportTargetPage? _falseReportTarget;

    private bool OpenFalseReportTarget(GameState world, MapSceneView map, int force, int targetUnit,
        Control parent, Func<bool> current, Action resume, Action<int> accepted)
    {
        if (_falseReportTarget is not null || _repository is null || !current()) return false;
        var query = _campaignComposition!.PrepareArmyQueries(world)
            .CreateArmyFalseReportTargetQuery(world, force, targetUnit);
        if (query.Selection is not { TargetAllowed: true, Destinations: { } destinations } || query.StartHexId is null)
            return false;
        var view = new ArmyFalseReportTargetView();
        var page = new FalseReportTargetPage(view, map, parent, query, current, resume, accepted);
        _falseReportTarget = page;
        parent.Hide(); AddChild(view);
        try
        {
            view.Configure(_repository, _ui, _sourceHeaders, () => RunUiAction(RequestFalseReportBack));
            map.SetUnitSelectionPicker();
            map.BeginFalseReportMapDisplay(destinations.Select(value => value.HexId));
            map.SetCommandTargetPicker(Pick, PointerAllowed, Hover);
            return true;
        }
        catch { CloseFalseReportTarget(parent, resumeParent: true); throw; }

        bool Current() => _falseReportTarget == page && page.Current();
        bool PointerAllowed(int hex) => Current() && query.QueryPointer(hex).CanPreviewRoute == true;
        void Hover(int? hex)
        {
            if (!Current()) return;
            if (hex is not int physical || page.PreviewHex == physical) return;
            var pointer = query.QueryPointer(physical);
            if (pointer.ValidSurface != true || pointer.CanPreviewRoute != true) return;
            page.PreviewHex = physical;
            page.PreviewRoute = query.QueryRoute(physical);
            //The source's page-local route is enemy-current -> hover, with no
            //numbered marker producer. Do not invent a UI line or marker1.
            view.ApplyHover(pointer.IsCandidate);
        }
        void Pick(int hex)
        {
            if (!Current()) return;
            Hover(hex);
            if (page.PreviewHex != hex || page.PreviewRoute is null ||
                page.PreviewRoute.Status == DeploymentDraftRouteStatus.Unresolved ||
                query.QueryPointer(hex).IsCandidate != true) return;
            CloseFalseReportTarget(parent, resumeParent: true);
            if (page.Current()) page.Accepted(hex);
        }
    }

    //The normal Gihou group is reset to index0 and this page has no source
    //Shift-append action. Its first ordinary cancel therefore uses Return.
    private void RequestFalseReportBack()
    {
        if (_falseReportTarget is { } page) CloseFalseReportTarget(page.Parent, resumeParent: true);
    }

    private bool TryCancelFalseReportTarget(Control? parent)
    {
        if (_falseReportTarget is not { } page || page.Parent != parent) return false;
        RequestFalseReportBack(); return true;
    }

    private void CloseFalseReportTarget(Control? parent, bool resumeParent = false)
    {
        if (_falseReportTarget is not { } page || page.Parent != parent) return;
        bool current = page.Current(); _falseReportTarget = null;
        if (GodotObject.IsInstanceValid(page.Map))
        {
            page.Map.EndFalseReportMapDisplay();
            page.Map.SetCommandTargetPicker(null); page.Map.SetUnitSelectionPicker();
        }
        page.View.Abandon();
        if (page.View.GetParent() is Node owner) owner.RemoveChild(page.View);
        page.View.QueueFree();
        if (resumeParent && current && GodotObject.IsInstanceValid(page.Parent) && !page.Parent.IsQueuedForDeletion())
        { page.Parent.Show(); page.Resume(); }
    }
}
