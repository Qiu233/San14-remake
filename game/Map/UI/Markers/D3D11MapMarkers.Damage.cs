using San14.Core;
using San14.Data;
using Godot;

internal partial class D3D11MapMarkers
{
    private sealed class BaseDamageSlots(MapObjectReceiverId receiver, BaseAction action)
    {
        internal readonly MapObjectReceiverId Receiver = receiver;
        internal readonly BaseAction Action = action;
        internal readonly UnitActionDamageView?[] Anchors = new UnitActionDamageView?[3];
        internal readonly List<(UnitActionDamageView View, int Anchor)> Views = [];
        internal int Next;
    }
    private readonly Dictionary<(Kind, int), BaseDamageSlots> baseDamageSlots = [];
    private Shader? damageShader;
    private Vector2? baseDamageAnchor;

    internal bool TryStartArmyDamage(ArmyReceiverId receiver, int gross, sbyte modifier,
        int style, bool alternate, out string? missing)
    {
        missing = "damage.Army_info_controller";
        return _armyLabels is not null && _armyLabels.TryStartDamage(receiver, gross, modifier,
            style, alternate, out missing);
    }

    internal bool TryStartBaseDamage(SettlementBase target, MapObjectReceiverId receiver, int gross,
        sbyte modifier, out string? missing)
    {
        missing = null;
        if (_citySources is null || _font is null || _assets is null)
        { missing = "damage.Base_info_controller"; return false; }
        var view = target.Kind == SettlementBaseKind.City
            ? new OrdinaryTacticViewIdentity(OrdinaryTacticViewKind.City, target.Id)
            : new(OrdinaryTacticViewKind.Gate, target.Id - 51);
        var key = (target.Kind == SettlementBaseKind.City ? Kind.City : Kind.Gate, view.Id);
        if (!_markers.TryGetValue(key, out var marker)) return true;
        var action = BaseTacticAction(view);
        if (action is null || !_baseInformationVisible || !marker.OnScreen) return true;
        if (baseDamageSlots.TryGetValue(key, out var slots) && slots.Receiver != receiver)
        {
            foreach (var row in slots.Views) row.View.QueueFree();
            baseDamageSlots.Remove(key); slots = null;
        }
        if (slots is null) baseDamageSlots.Add(key, slots = new(receiver, action));
        int index = Array.FindIndex(slots.Anchors, candidate => candidate is not null && !candidate.Active);
        if (index < 0) index = slots.Next;
        var number = slots.Anchors[index];
        if (number is null || number.Active)
        {
            damageShader ??= UiSourceSprite.CreateShader();
            number = new() { Name = "BaseGrossDamage_" + target.Id + "_" + index, ZIndex = 2 };
            tacticBaseCanvas.AddChild(number); number.Configure(tacticRepository, _assets, damageShader, _font);
            slots.Anchors[index] = number; slots.Views.Add((number, index));
        }
        slots.Next = index == 2 ? 0 : index + 1;
        number.Start(gross, modifier, style: 2); PositionBaseDamage();
        return true;
    }

    internal void AdvanceDamageFeedback(uint mode)
    {
        AdvanceFacilityDamageFeedback(mode);
        foreach (var slots in baseDamageSlots.Values)
            foreach (var row in slots.Views) row.View.AdvanceSourceTick(mode);
        PositionBaseDamage();
    }

    private void PositionBaseDamage()
    {
        if (baseDamageSlots.Count == 0) return;
        baseDamageAnchor ??= new UiSourceLayout(tacticRepository, 0x47FB32BE).Rectangle(17).Position;
        tacticBaseCanvas.Scale = Size / CanonicalSize;
        foreach (var (key, slots) in baseDamageSlots.ToArray())
        {
            if (!_markers.TryGetValue(key, out var marker))
            {
                foreach (var row in slots.Views) row.View.QueueFree();
                baseDamageSlots.Remove(key); continue;
            }
            var origin = new Vector2((int)marker.Screen.X - slots.Action.Center - 111, (int)marker.Screen.Y - 96);
            foreach (var (number, index) in slots.Views.ToArray())
            {
                if (!number.Active && !slots.Anchors.Contains(number))
                { number.QueueFree(); slots.Views.Remove((number, index)); continue; }
                var offset = index switch { 0 => new Vector2(30, 0), 1 => new Vector2(-10, -10), _ => new Vector2(60, -10) };
                number.Position = origin + baseDamageAnchor.Value + offset;
                number.Visible = _baseInformationVisible && marker.PositionReady && marker.OnScreen && number.Active;
            }
        }
    }

    internal void RetireDamageFeedback()
    {
        _armyLabels?.RetireDamageFeedback();
        foreach (var slots in baseDamageSlots.Values)
            foreach (var row in slots.Views) row.View.QueueFree();
        baseDamageSlots.Clear(); damageShader?.Dispose(); damageShader = null;
    }
}
