using San14.Data;
using San14.Core;
using Godot;
using NVector4 = System.Numerics.Vector4;

internal partial class D3D11MapMarkers
{
    private sealed record BaseAction(UnitActionTacticNameView Name, UnitActionTacticNameView Number, float Center);
    private readonly Dictionary<(Kind, int), BaseAction> tacticBaseActions = [];
    private readonly Node2D tacticBaseCanvas = new() { Name = "BaseUnitActions", ZIndex = 40 };
    private GameResourceRepository tacticRepository = null!;

    internal void StartArmyTacticName(int unit, OrdinaryTacticNamePublication name) => _armyLabels?.StartTacticName(unit, name);

    internal void StartBaseTacticName(OrdinaryTacticViewIdentity view, string name, int palette)
    {
        var action = BaseTacticAction(view);
        action?.Name.StartName(name, palette);
    }

    internal void PublishTacticNumbers(OrdinaryTacticResultPublication result)
    {
        foreach (var group in result.NumberGroups)
            foreach (var (receiver, amount) in group.Amounts)
                if (group.TargetKind == TacticResultTargetKind.ArmyReceiver)
                    _armyLabels?.StartTacticNumber(receiver, amount, group.Palette);
                else
                    BaseTacticAction(receiver <= 51 ? new(OrdinaryTacticViewKind.City, receiver)
                        : new(OrdinaryTacticViewKind.Gate, receiver - 51))?.Number.TryStartNumber(amount, group.Palette);
    }

    private BaseAction? BaseTacticAction(OrdinaryTacticViewIdentity view)
    {
        var kind = view.Kind == OrdinaryTacticViewKind.City ? Kind.City : Kind.Gate;
        var key = (kind, view.Id);
        if (!_markers.TryGetValue(key, out var marker) || !marker.PositionReady) return null;
        if (tacticBaseActions.TryGetValue(key, out var action)) return action;
        if (tacticBaseCanvas.GetParent() is null) AddChild(tacticBaseCanvas);
        var name = new UnitActionTacticNameView { Name = "BaseTactic_" + view.Id };
        var number = new UnitActionTacticNameView { Name = "BaseDamage_" + view.Id, ZIndex = 1 };
        tacticBaseCanvas.AddChild(name); tacticBaseCanvas.AddChild(number);
        name.Configure(tacticRepository, _font!); number.Configure(tacticRepository, _font!, true);
        var full = new UiSourceLayout(new LoadedLayout(_citySources!.Full.Layout, _citySources.Full.Source));
        int mount = kind == Kind.City ? 25 : 44;
        float width = (kind == Kind.City ? _citySources.CityFlag : _citySources.Gate).Nodes[0].Value44;
        //1AAF30 centers the installed flag control, then1A1470 mounts the
        //borrowed UnitAction at(-111,-56). It is not the owned speech offset.
        float center = (int)full.Rectangle(mount).Position.X - (int)(width * -.5f);
        action = new(name, number, center); tacticBaseActions.Add(key, action);
        return action;
    }

    public override void _Process(double delta)
    {
        if (tacticBaseActions.Count == 0) return;
        tacticBaseCanvas.Scale = Size / CanonicalSize;
        foreach (var (key, action) in tacticBaseActions.ToArray())
        {
            if (!_markers.TryGetValue(key, out var marker))
            {
                action.Name.QueueFree(); action.Number.QueueFree(); tacticBaseActions.Remove(key); continue;
            }
            var origin = new Vector2((int)marker.Screen.X - action.Center - 111, (int)marker.Screen.Y - 96);
            action.Name.Position = origin;
            action.Number.Position = origin + new Vector2((action.Name.Size.X - action.Number.Size.X) / 2, 0);
            bool visible = _baseInformationVisible && marker.PositionReady && marker.OnScreen;
            action.Name.Visible = visible && action.Name.IsPlaying;
            action.Number.Visible = visible && action.Number.IsPlaying;
        }
    }

    internal OrdinaryTacticViewPose TacticViewPose(OrdinaryTacticViewIdentity view)
    {
        if (view.Kind == OrdinaryTacticViewKind.Army)
            return _armyLabels?.TacticViewPose(view.Id) ?? new(false, null);
        var key = (view.Kind == OrdinaryTacticViewKind.City ? Kind.City : Kind.Gate, view.Id);
        if (!_markers.TryGetValue(key, out var marker)) return new(false, null);
        // A position comes only from the current native Primary publication.
        // Marker Hex/terrain positions are never a substitute for a view.
        return marker.PositionReady ? new(true, new NVector4(marker.CityUnitPosition, 1)) : new(true, null);
    }
}
