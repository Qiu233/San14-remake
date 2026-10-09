using San14.Core;

namespace San14.Data;

public static class OwnedArmyLifecycleTargets
{
    public static GameState InstallFresh(GameState state)
    {
        if (state.ReadArmyLifecycle() is not { Targets: null } arena ||
            state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            CampaignSourceKnowledge.From(state.ResourceState).ArmyTargets.Count != 0)
            throw new InvalidDataException("Army targets require the unused fresh constructor allocation.");
        var directory = new ArmyReceiverSourceDirectory(arena);
        var rows = arena.Slots.Select(slot => (slot.ReceiverId,
            Decoded: ArmyTargetSourceAdapter.Decode(slot.ReceiverId, new(255, 0), directory))).ToArray();
        return (state with { ResourceState = CampaignSourceKnowledge.From(state.ResourceState).ReplaceArmyTargets(rows.Select(row => row.Decoded.Residue)) }).WithArmyLifecycle(arena with { Targets = new(rows.Select(row => new ArmyTargetAssignment(row.ReceiverId, row.Decoded.Target)).ToArray()) });
    }
    public static GameState Write(GameState state, ArmyReceiverId receiverId, ArmyTargetSourceFields fields)
    {
        var arena = state.ReadArmyLifecycle() ?? throw new InvalidDataException("Army target receiver owner is absent.");
        var directory = new ArmyReceiverSourceDirectory(arena);
        if (!directory.TryResolveSourceOrdinal(receiverId, out _))
            throw new InvalidDataException("Army target write names an unowned receiver.");
        var decoded = ArmyTargetSourceAdapter.Decode(receiverId, fields, directory);
        return (state with { ResourceState = CampaignSourceKnowledge.From(state.ResourceState).WithArmyTarget(decoded.Residue) }).WithArmyLifecycle(arena with { Targets = (arena.Targets ?? new([])).With(receiverId, decoded.Target) });
    }
    internal static GameState PublishCreation(GameState state, int physical, byte? kind, ushort? identity)
    {
        if (state.ReadArmyLifecycle()!.Targets is null && kind is null) return state;
        return Write(state, state.ReadArmyLifecycle()!.Slots[physical].ReceiverId, new(kind, identity));
    }
    public static ArmyTargetSourceFields? Read(GameState state, ArmyReceiverId receiverId) =>
        state.ReadArmyLifecycle() is { Targets: { } targets } arena && targets.Find(receiverId) is { } target
            ? ArmyTargetSourceAdapter.Encode(target, (state.ResourceState as CampaignSourceKnowledge)?.FindArmyTarget(receiverId), new(arena))
            : state.ReadArmyLifecycle()?.Targets is not null ? new(null, null) : null;
    public static void BindCurrent(GameState state, SourceWorldContext current) => current.BindArmyTargets(state);
    public static GameState PublishCurrent(GameState state, SourceWorldContext current) => (state with { ResourceState = CampaignSourceKnowledge.From(state.ResourceState).ReplaceArmyTargets(current.ArmyTargetResidues) }).WithArmyLifecycle(state.ReadArmyLifecycle()! with { Targets = current.ArmyTargets });
}

public sealed partial class SourceWorldContext
{
    private ArmyTargetsState? _armyTargets;
    private ArmyReceiverSourceDirectory? _targetReceivers;
    private CampaignSourceKnowledge _targetResidues = new([]);
    internal ArmyTargetsState? ArmyTargets => _armyTargets;
    internal IReadOnlyCollection<ArmyTargetSourceResidue> ArmyTargetResidues => _targetResidues.ArmyTargets;
    internal void BindArmyTargets(GameState state)
    {
        _armyTargets = state.ReadArmyLifecycle()?.Targets;
        _targetReceivers = state.ReadArmyLifecycle() is { } arena ? new(arena) : null;
        _targetResidues = CampaignSourceKnowledge.From(state.ResourceState);
        BindArmyOverrides(state);
    }
    internal ArmyTargetState? ReadArmyTargetState(int sourceOrdinal) =>
        _targetReceivers is not null && _targetReceivers.TryResolveReceiverId(sourceOrdinal, out var receiver)
            ? _armyTargets?.Find(receiver) ?? (_armyTargets is null ? null : new(null, null)) : null;
    internal bool TargetsArmyReceiver(ArmyTargetDestination destination, int sourceOrdinal) =>
        destination.Kind == ArmyTargetDestinationKind.ArmyReceiver &&
        _targetReceivers is not null && _targetReceivers.TryResolveReceiverId(sourceOrdinal, out var receiver) &&
        destination.ReceiverId == receiver;
    internal void ClearArmyTargetToPlaceholder(int sourceOrdinal)
    {
        if (_targetReceivers is null || !_targetReceivers.TryResolveReceiverId(sourceOrdinal, out var owner) ||
            !_targetReceivers.TryResolveReceiverId(0, out var placeholder))
            throw new InvalidDataException("Army target clear has no receiver directory.");
        _armyTargets = (_armyTargets ?? new([])).With(owner,
            new(ArmyTargetKind.ArmyReceiver, new(ArmyTargetDestinationKind.ArmyReceiver, null, placeholder)));
        _targetResidues = _targetResidues.WithArmyTarget(new(owner, null, null));
    }
    public void SetArmyLifecycleTargetFields(int physical, ArmyTargetSourceFields? fields)
    {
        if (_targetReceivers is null || !_targetReceivers.TryResolveReceiverId(physical, out var receiver))
            throw new InvalidDataException("Army target write has no receiver directory.");
        var decoded = ArmyTargetSourceAdapter.Decode(receiver, fields ?? new(null, null), _targetReceivers);
        _armyTargets = (_armyTargets ?? new([])).With(receiver, decoded.Target);
        _targetResidues = _targetResidues.WithArmyTarget(decoded.Residue);
    }
    public ArmyTargetSourceFields? ReadArmyLifecycleTargetFields(int physical) =>
        _targetReceivers is not null && _targetReceivers.TryResolveReceiverId(physical, out var receiver) &&
        _armyTargets?.Find(receiver) is { } target
            ? ArmyTargetSourceAdapter.Encode(target, _targetResidues.FindArmyTarget(receiver), _targetReceivers)
            : _armyTargets is not null ? new(null, null) : null;
    private void CopyArmyTargetsTo(SourceWorldContext other)
    {
        other._armyTargets = _armyTargets; other._targetReceivers = _targetReceivers; other._targetResidues = _targetResidues;
        CopyArmyOverridesTo(other);
    }
}
