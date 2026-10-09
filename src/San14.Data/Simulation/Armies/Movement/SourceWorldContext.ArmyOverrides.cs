using San14.Core;

namespace San14.Data;

public sealed partial class SourceWorldContext
{
    private readonly Dictionary<int, ArmyOverrideTargetState> _armyOverrides = [];
    private readonly Dictionary<int, ArmyOverrideTargetSourceResidue> _armyOverrideResidues = [];
    private ArmyReceiverSourceDirectory? _overrideReceivers;

    internal void BindArmyOverrides(GameState state)
    {
        _armyOverrides.Clear(); _armyOverrideResidues.Clear();
        _overrideReceivers = state.NativeArmyLifecycle is { } arena ? new(arena) : null;
        foreach (var slot in state.NativeArmyLifecycle?.Slots ?? [])
        {
            var image = slot.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) : slot.PendingImage;
            var target = image?.OverrideTarget ?? slot.OverrideTarget;
            if (target is null) continue;
            _armyOverrides.Add(slot.PhysicalSlot, target);
            var owner = image is null ? new ArmyOverrideSourceOwner(slot.ReceiverId, null) : ArmyOverrideTargetSourceAdapter.Owner(state, image);
            if ((state.ResourceState as CampaignSourceKnowledge)?.FindArmyOverride(owner) is { } residue)
                _armyOverrideResidues.Add(slot.PhysicalSlot, residue);
        }
    }
    private ArmyOverrideTargetSourceFields ReadOverrideSource(int? ordinal) => ordinal is int index &&
        _armyOverrides.TryGetValue(index, out var target)
            ? ArmyOverrideTargetSourceAdapter.Encode(target, _armyOverrideResidues.GetValueOrDefault(index), _overrideReceivers)
            : new(null, null);
    private bool HasOverrideDestination(int? ordinal) => ordinal is int index &&
        _armyOverrides.GetValueOrDefault(index)?.Destination is not null;
    private void WriteOverrideSource(int? ordinal, ArmyOverrideTargetSourceFields fields)
    {
        int index = MapPhysical(ordinal, 501, "army override source writer");
        var target = ArmyOverrideTargetSourceAdapter.Decode(fields, _overrideReceivers);
        _armyOverrides[index] = target;
        // This local format view need not invent a campaign identity when it is
        // used independently of an imported world. Binding supplies identities.
        var owner = _armyOverrideResidues.GetValueOrDefault(index)?.Owner ?? default;
        _armyOverrideResidues[index] = ArmyOverrideTargetSourceAdapter.Residue(owner, fields, target);
    }
    private void CopyArmyOverridesTo(SourceWorldContext other)
    {
        other._overrideReceivers = _overrideReceivers;
        other._armyOverrides.Clear(); other._armyOverrideResidues.Clear();
        foreach (var pair in _armyOverrides) other._armyOverrides.Add(pair.Key, pair.Value);
        foreach (var pair in _armyOverrideResidues) other._armyOverrideResidues.Add(pair.Key, pair.Value);
    }
}
