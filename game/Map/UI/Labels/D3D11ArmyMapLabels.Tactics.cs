using Godot;
using San14.Data;

internal partial class D3D11ArmyMapLabels
{
    private sealed record TacticAction(ArmyTacticNameView Name, UnitActionTacticNameView Number);
    private readonly Dictionary<int, TacticAction> tacticActions = [];
    private Vector2? tacticActionOffset;

    internal void StartTacticName(int receiver, OrdinaryTacticNamePublication name) => Action(receiver)?.Name.StartName(name);
    internal void StartTacticNumber(int receiver, uint amount, int palette) => Action(receiver)?.Number.TryStartNumber(amount, palette);

    private bool DirectionSuppressesUnit(int unit) =>
        _armyReceiversByUnitId.TryGetValue(unit, out int receiver) &&
        directionSuppression?.ContainsKey(new(OrdinaryTacticViewKind.Army, receiver)) == true;

    private TacticAction? Action(int receiver)
    {
        if (_armyUnitIdsByReceiver is null || !_armyUnitIdsByReceiver.TryGetValue(receiver, out int unit)) return null;
        if (!_rows.TryGetValue(unit, out var row) || row.ArmyValid != true || !_cards.ContainsKey(unit)) return null;
        if (tacticActions.TryGetValue(unit, out var existing)) return existing;
        var name = new ArmyTacticNameView { Name = "Tactic_" + unit, ZIndex = 40 };
        var number = new UnitActionTacticNameView { Name = "Damage_" + unit, ZIndex = 41 };
        _root.AddChild(name); _root.AddChild(number);
        name.Configure(_repository, _assets, _speechShader); number.Configure(_repository, _font, true);
        if (tacticActionOffset is null)
        {
            var normal = new UiSourceLayout(new LoadedLayout(_source.Normal.Layout, _source.Normal.Source));
            var action = new UiSourceLayout(new LoadedLayout(_source.UnitAction.Layout, _source.UnitAction.Source));
            var root = new UiSourceLayout(new LoadedLayout(_source.TopAndUi.Layout, _source.TopAndUi.Source));
            //19F940 first installs UnitAction at Normal.node5, then1BC4E0
            //subtracts its mounted MapFace's position; v18 is additive.
            tacticActionOffset = root.Rectangle(27).Position + normal.Rectangle(5).Position - action.Rectangle(3).Position;
        }
        var created = new TacticAction(name, number); tacticActions.Add(unit, created); return created;
    }

    public override void _Process(double delta)
    {
        foreach (var (unit, action) in tacticActions.ToArray())
        {
            if (!_cards.TryGetValue(unit, out var card))
            {
                action.Name.QueueFree(); action.Number.QueueFree(); tacticActions.Remove(unit); continue;
            }
            var origin = card.Position + tacticActionOffset!.Value;
            var parentSize = new Vector2(_source.UnitAction.Nodes[0].Value44, _source.UnitAction.Nodes[0].Value46);
            action.Name.Position = origin + new Vector2((parentSize.X - action.Name.Size.X) / 2, -100 - action.Name.Size.Y / 2);
            action.Number.Position = origin + new Vector2((parentSize.X - action.Number.Size.X) / 2, 0);
            //The info suppression counter hides the Army card, not its
            //independent UnitAction result child in Direction's final group.
            action.Name.Visible = card.ProjectedVisible && action.Name.IsPlaying;
            action.Number.Visible = card.ProjectedVisible && action.Number.IsPlaying;
        }
    }
}
