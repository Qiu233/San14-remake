using System.Collections.ObjectModel;
using San14.Core;
using San14.Data;
using Godot;

internal partial class D3D11ArmyMapLabels
{
    private sealed class FacilityDamageSlots(ArmyReceiverId receiver)
    {
        internal readonly ArmyReceiverId Receiver = receiver;
        internal readonly UnitActionDamageView?[] Views = new UnitActionDamageView?[3];
        internal readonly List<(UnitActionDamageView View, int Anchor)> Active = [];
        internal int Next;
    }
    private readonly Dictionary<int, FacilityDamageSlots> facilityDamageSlots = [];
    private Vector2? facilityDamageMount;

    internal bool TryStartFacilityDamage(ArmyReceiverId receiver, int gross, sbyte modifier, out string? missing)
        => TryStartDamage(receiver, gross, modifier, 3, false, out missing);

    internal bool TryStartDamage(ArmyReceiverId receiver, int gross, sbyte modifier, int style,
        bool alternate, out string? missing)
    {
        missing = null;
        if (_armyUnitIdsByReceiver is null) { missing = "facility.damage.Army_receiver_owner"; return false; }
        if (!_armyUnitIdsByReceiver.TryGetValue(receiver.Value, out int unit) || !_cards.TryGetValue(unit, out var card)) return true;
        if (!card.SpeechLayout.IsInsideTree()) { missing = "facility.damage.UnitAction_owner"; return false; }
        //1B8190 skips a hidden UnitAction; it does not enqueue a future float.
        if (!card.ProjectedVisible) return true;
        if (facilityDamageSlots.TryGetValue(unit, out var slots) && slots.Receiver != receiver)
        {
            foreach (var row in slots.Active) row.View.QueueFree();
            facilityDamageSlots.Remove(unit); slots = null;
        }
        if (slots is null) facilityDamageSlots.Add(unit, slots = new(receiver));
        int index = Array.FindIndex(slots.Views, candidate => candidate is not null && !candidate.Active);
        if (index < 0) index = slots.Next;
        var view = slots.Views[index];
        if (view is null || view.Active)
        {
            view = new() { Name = "FacilityDamage_" + unit + "_" + index, ZIndex = 42 };
            _root.AddChild(view); view.Configure(_repository, _assets, _speechShader, _font);
            slots.Views[index] = view;
            slots.Active.Add((view, index));
        }
        slots.Next = index == 2 ? 0 : index + 1;
        view.Start(gross, modifier, style, alternate); PositionFacilityDamage();
        return true;
    }

    internal bool TryDeferFacilityDanger(ArmyReceiverId receiver, out string? missing)
    {
        missing = null;
        if (_armyUnitIdsByReceiver is null) { missing = "facility.danger.Army_receiver_owner"; return false; }
        if (!_armyUnitIdsByReceiver.TryGetValue(receiver.Value, out int unit) || !_cards.TryGetValue(unit, out var card)) return true;
        if (!GodotObject.IsInstanceValid(card.SpeechLayout)) { missing = "facility.danger.UnitAction_owner"; return false; }
        //The user has deferred UI animation fidelity. This records that exact
        //danger work, without claiming a numeric/model refresh or playing a substitute.
        card.SpeechLayout.SetMeta("facility_danger_animation_deferred", true);
        return true;
    }

    internal void AdvanceFacilityDamageFeedback(uint mode)
    {
        foreach (var slots in facilityDamageSlots.Values)
            foreach (var row in slots.Active) row.View.AdvanceSourceTick(mode);
        PositionFacilityDamage();
    }

    internal void RetireDamageFeedback()
    {
        foreach (var slots in facilityDamageSlots.Values)
            foreach (var row in slots.Active) row.View.QueueFree();
        facilityDamageSlots.Clear();
    }

    private void PositionFacilityDamage()
    {
        if (facilityDamageMount is null)
        {
            var normal = new UiSourceLayout(new LoadedLayout(_source.Normal.Layout, _source.Normal.Source));
            var action = new UiSourceLayout(new LoadedLayout(_source.UnitAction.Layout, _source.UnitAction.Source));
            var root = new UiSourceLayout(new LoadedLayout(_source.TopAndUi.Layout, _source.TopAndUi.Source));
            facilityDamageMount = root.Rectangle(27).Position + normal.Rectangle(5).Position -
                action.Rectangle(3).Position + action.Rectangle(17).Position;
        }
        foreach (var (unit, slots) in facilityDamageSlots.ToArray())
        {
            if (!_cards.TryGetValue(unit, out var card) ||
                !_armyReceiversByUnitId.TryGetValue(unit, out int receiver) || receiver != slots.Receiver.Value)
            {
                foreach (var row in slots.Active) row.View.QueueFree();
                facilityDamageSlots.Remove(unit); continue;
            }
            foreach (var (view, index) in slots.Active.ToArray())
            {
                if (!view.Active && !slots.Views.Contains(view))
                { view.QueueFree(); slots.Active.Remove((view, index)); continue; }
                var offset = index switch { 0 => new Vector2(30, 0), 1 => new Vector2(-10, -10), _ => new Vector2(60, -10) };
                view.Position = card.Position + facilityDamageMount.Value + offset;
                view.Visible = card.ProjectedVisible && view.Active;
            }
        }
    }

    internal IReadOnlyDictionary<int, ArmyFireViewSnapshot?>? CaptureArmyRenderViews(
        IReadOnlyList<D3D11ArmyLabelUnitPosition> positions)
    {
        PositionFacilityDamage();
        if (_armyUnitIdsByReceiver is null) return null;
        var result = new Dictionary<int, ArmyFireViewSnapshot?>();
        var byUnit = positions.ToDictionary(row => row.UnitId, row => row.Position);
        foreach (var (receiver, unit) in _armyUnitIdsByReceiver)
        {
            if (!_rows.TryGetValue(unit, out var row) || row.ArmyValid is null) result.Add(receiver, null);
            else result.Add(receiver, row.ArmyValid == true && byUnit.TryGetValue(unit, out var point)
                ? new(true, point.X, point.Y, point.Z, point.W) : new(false, 0, 0, 0, 1));
        }
        return new ReadOnlyDictionary<int, ArmyFireViewSnapshot?>(result);
    }
}
