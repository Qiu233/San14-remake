using San14.Core;

namespace San14.Data;

public sealed record ArmyLifecycleCreationAdmission(NativeArmyLifecycleState Lifecycle, ArmyMapOccupancyState Occupancy);

/// <summary>Operation-local translation between semantic map bindings and the
/// resource facade's receiver table. The typed map is the only cell authority.</summary>
internal sealed class ArmyMapOccupancyAdapter
{
    private readonly Dictionary<int, int> _bindingByReceiver = [];
    private readonly Dictionary<int, int> _receiverByBinding = [];
    private readonly bool _standalone;
    internal ArmyMapOccupancyState? State { get; private set; }

    internal ArmyMapOccupancyAdapter(GameState state)
    {
        State = state.ArmyMapOccupancy;
        if (state.NativeArmyLifecycle is not { } lifecycle) return;
        foreach (var slot in lifecycle.Slots)
            if (slot.OccupancyBindingId is int binding)
            {
                _bindingByReceiver.Add(slot.PhysicalSlot, binding);
                _receiverByBinding.Add(binding, slot.PhysicalSlot);
            }
    }

    // Standalone source contexts can stage unknown cells without constructing
    // a campaign or implying a fresh map. Their bindings are never save owners.
    internal ArmyMapOccupancyAdapter() { State = new(); _standalone = true; }
    private ArmyMapOccupancyAdapter(ArmyMapOccupancyAdapter other)
    {
        State = other.State?.Copy();
        _standalone = other._standalone;
        foreach (var row in other._bindingByReceiver) _bindingByReceiver.Add(row.Key, row.Value);
        foreach (var row in other._receiverByBinding) _receiverByBinding.Add(row.Key, row.Value);
    }
    internal ArmyMapOccupancyAdapter Copy() => new(this);

    internal ushort? ReadSourceWord(int hex) => State?.ReadCell(hex) is { } cell ? cell.Kind switch
    {
        ArmyMapCellKind.Unknown => null,
        ArmyMapCellKind.Empty => 0,
        ArmyMapCellKind.UnsupportedTargetNegative => ushort.MaxValue,
        ArmyMapCellKind.Binding => cell.BindingId is int binding && _receiverByBinding.TryGetValue(binding, out int receiver)
            && receiver is >= 1 and <= 500 ? checked((ushort)receiver) : null,
        _ => throw new InvalidDataException("Map occupancy cell kind is invalid.")
    } : null;

    internal void WriteSourceWord(int hex, ushort? raw)
    {
        if (State is null) throw new InvalidDataException("Current map occupancy owner is unknown.");
        ArmyMapCell cell;
        if (raw is null) cell = ArmyMapCell.Unknown;
        else if (unchecked((short)raw.Value) < 0) cell = ArmyMapCell.UnsupportedTargetNegative;
        else if (raw.Value is >= 1 and <= 500)
        {
            if (!_bindingByReceiver.TryGetValue(raw.Value, out int binding))
            {
                if (!_standalone) throw new InvalidDataException("Current occupancy receiver binding is unknown.");
                binding = 1;
                while (State.Bindings.ContainsKey(binding)) binding = checked(binding + 1);
                State = ArmyMapOccupancyRules.CreateBinding(State, binding, ArmyOccupancyUnitReference.Unknown);
                _bindingByReceiver.Add(raw.Value, binding); _receiverByBinding.Add(binding, raw.Value);
            }
            cell = ArmyMapCell.ForBinding(binding);
        }
        else cell = ArmyMapCell.Empty;
        State = ArmyMapOccupancyRules.SetCell(State, hex, cell)
            ?? throw new InvalidDataException("Current occupancy binding is unavailable.");
    }

    internal bool TryTraverse(int receiver, int from, int to, out ArmyMapOccupancyWrite[] writes)
    {
        writes = [];
        if (State is null || !_bindingByReceiver.TryGetValue(receiver, out int binding)) return false;
        var result = ArmyMapOccupancyRules.TryApplyTraversal(State, binding, from, [to]);
        if (result is null) return false;
        State = result.State; writes = result.Writes; return true;
    }

    internal void ReleaseReceiver(int receiver)
    {
        if (State is null || !_bindingByReceiver.TryGetValue(receiver, out int binding))
            throw new InvalidDataException("Current retiring occupancy binding is unknown.");
        State = ArmyMapOccupancyRules.ReleaseBinding(State, binding)
            ?? throw new InvalidDataException("Current retiring occupancy binding is unavailable.");
    }

    internal static GameState InstallFresh(GameState state, NativeArmyLifecycleState lifecycle)
    {
        var occupancy = new ArmyMapOccupancyState { DefaultCellKnowledge = ArmyMapDefaultCellKnowledge.Empty };
        var slots = lifecycle.Slots.ToArray();
        int nextBinding = 1;
        foreach (var slot in lifecycle.Slots.Where(slot => slot.PhysicalSlot != 0))
        {
            int binding = nextBinding++;
            occupancy.Bindings.Add(binding, ArmyOccupancyUnitReference.Vacant);
            slots[slot.PhysicalSlot] = slot with { OccupancyBindingId = binding };
        }
        return state with { ArmyMapOccupancy = occupancy, NativeArmyLifecycle = lifecycle with { Slots = slots } };
    }
}

public sealed partial class SourceWorldContext
{
    private ArmyMapOccupancyAdapter? _armyMapOccupancy;
    private bool _armyMapOccupancyBoundToWorld;
    internal void BindArmyMapOccupancy(GameState state, bool replace = false)
    {
        if (_armyMapOccupancyBoundToWorld && !replace) return;
        _armyMapOccupancy = new(state); _armyMapOccupancyBoundToWorld = true;
    }
    internal ArmyMapOccupancyState? ExportArmyMapOccupancy() => _armyMapOccupancy?.State;
    internal void ReleaseArmyOccupancyReceiver(int receiver) =>
        (_armyMapOccupancy ?? throw new InvalidDataException("Current map occupancy view is unknown.")).ReleaseReceiver(receiver);
    internal bool TryApplyArmyOccupancyTraversal(int receiver, int from, int to, out ArmyMapOccupancyWrite[] writes)
    {
        writes = [];
        return _armyMapOccupancy?.TryTraverse(receiver, from, to, out writes) == true;
    }
    private void CopyArmyMapOccupancyTo(SourceWorldContext draft)
    { draft._armyMapOccupancy = _armyMapOccupancy?.Copy(); draft._armyMapOccupancyBoundToWorld = _armyMapOccupancyBoundToWorld; }
    private void CommitArmyMapOccupancyFrom(SourceWorldContext draft)
    { _armyMapOccupancy = draft._armyMapOccupancy?.Copy(); _armyMapOccupancyBoundToWorld = draft._armyMapOccupancyBoundToWorld; }
}
