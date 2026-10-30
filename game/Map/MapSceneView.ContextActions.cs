using Godot;

public partial class MapSceneView
{
    private MapContextActions? _mapContextActions;
    private Control? _contextActionsHost;
    private Func<bool>? _contextActionsAdmitted;

    //These five existing actions belong to the selected map entity, not to
    //the resident commander card underneath them. Their business handlers
    //remain on this Map owner after the controls are mounted in the HUD tree.
    private MapContextActions ContextActions()
    {
        if (_mapContextActions is { } existing) return existing;
        var actions = new MapContextActions { Name = "MapContextActions", MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 2, Visible = false };
        _mapContextActions = actions;
        AddChild(actions);
        actions.Synchronize = SynchronizeContextActions;
        TreeExiting += RetireContextActions;
        return actions;
    }

    internal void MountContextActions(Control host, Control residentCards, Func<bool> admitted)
    {
        var actions = ContextActions();
        if (residentCards.GetParent() != host)
            throw new InvalidOperationException("Map context actions require their actual resident HUD owner.");
        _contextActionsHost = host;
        _contextActionsAdmitted = admitted;
        if (actions.GetParent() != host) actions.Reparent(host, keepGlobalTransform: false);
        //ZIndex alone changes rendering, not Godot GUI hit order. A rebuilt
        //card view must also precede the retained action group in this tree.
        int after = residentCards.GetIndex();
        if (actions.GetIndex() > after) after++;
        if (actions.GetIndex() != after) host.MoveChild(actions, after);
        SynchronizeContextActions();
    }

    private void SynchronizeContextActions()
    {
        if (_mapContextActions is not { } actions || _contextActionsHost is not { } host) return;
        bool admitted = IsInsideTree() && IsVisibleInTree() && !_detailsOpen && _contextActionsAdmitted?.Invoke() == true;
        actions.Visible = admitted;
        if (!admitted) return;
        //Both owners use Main's ordinary canvas. Preserve the map's actual
        //rectangle, including resizing, rather than relocating individual hits.
        var transform = host.GetGlobalTransformWithCanvas().AffineInverse() * GetGlobalTransformWithCanvas();
        if (actions.Position != transform.Origin) actions.Position = transform.Origin;
        if (actions.Rotation != transform.Rotation) actions.Rotation = transform.Rotation;
        if (actions.Scale != transform.Scale) actions.Scale = transform.Scale;
        if (actions.Size != Size) actions.Size = Size;
    }

    private void RetireContextActions()
    {
        var actions = _mapContextActions; _mapContextActions = null;
        _contextActionsHost = null; _contextActionsAdmitted = null;
        if (actions is null || !GodotObject.IsInstanceValid(actions)) return;
        actions.Synchronize = null;
        actions.Hide();
        actions.QueueFree();
    }

    private sealed partial class MapContextActions : Control
    {
        internal Action? Synchronize;
        public override void _Process(double delta) => Synchronize?.Invoke();
    }
}
