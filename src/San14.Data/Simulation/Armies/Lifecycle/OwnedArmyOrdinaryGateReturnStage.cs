using San14.Core;

namespace San14.Data;

/// <summary>Ordinary29F220 into a friendly Gate followed by actual28E6D0
/// deferred cleanup. Gate troops/wounds/morale stay on the Gate; currency
/// virtual getters/setters use its current mapped City.</summary>
public sealed class OwnedArmyOrdinaryGateReturnStage(ArmyCampaignRuleAdapter package,
    OwnedArmyGovernorComparatorInputs governorInputs) : IPlayableGateEntryStateRules
{
    public const string Revision = "san14:ce-owned-army-ordinary-gate-return-20261007";
    public bool RequiresCurrentState(GameState state, UnitState unit, GateState gate) =>
        unit.NativeArmyOrigin is not null && unit.Mission?.Kind != UnitMissionKind.Transport;

    public PlayableCityEntryStateResolution? Return(GameState state, UnitState unit, GateState gate)
    {
        var result = TryApply(state, unit.Id, gate.Id);
        return result.State is { } next ? new(next, Revision) : null;
    }

    public OwnedArmyOrdinaryCityReturnResult TryApply(GameState state, int unitId, int gateId)
    {
        if (!state.Units.TryGetValue(unitId, out var unit) || !state.Gates.TryGetValue(gateId, out var gate) ||
            unit.NativeArmyOrigin?.NativeSlotIndex is not int physical ||
            state.ReadArmyLifecycle() is not { ArmyGroups: not null, ArmyCautions: not null } || unit.Mission?.Kind == UnitMissionKind.Transport ||
            gate.HexId != unit.HexId || gate.ForceId != unit.ForceId)
            return new(null, [], "current.owned_ordinary_gate_entry");
        try
        {
            var world = package.CaptureOwnedArmyReturnWorld(state);
            var result = package.PrepareOwnedArmyReturnCurrent(state, new(physical),
                new(BaseKind.Gate, gateId - 51), 0, world.Context, world.Pool,
                new Queries(state, governorInputs), new(false, false));
            if (result.Suspension is { } suspension)
            {
                var suspended = world.CommitSuspension(state, suspension);
                var pending = suspended.ArmyReturnCaptiveContinuation
                    ?? throw new InvalidDataException("Ordinary return suspension lost its captive owner.");
                // The same Army remains active until the actual return suffix
                // admits pending70; do not consume retirement during selection.
                return new(suspended with { ArmyReturnCaptiveContinuation = pending with
                    { Caller = ArmyReturnCaptiveCaller.OrdinaryEntry } }, [], null);
            }
            if (result.Packet is not { } packet) return new(null, [], result.UnresolvedOwner);
            var returned = world.CommitPrepared(state, packet);
            if (returned is null) return new(null, packet.SourceEffectOrder, "current.ordinary_gate_return_publication");
            returned = OwnedArmyLifecycleDetachedInputs.PublishCurrent(returned, world.Context);
            var cleaned = package.ConsumeArmyLifecyclePending(returned);
            return new(cleaned, packet.SourceEffectOrder, cleaned is null ? "current.deferred_cleanup_owner" : null)
            { DistrictTransferAssociations = cleaned is null ? [] : packet.RulerTailAssociationUpdates };
        }
        catch (InvalidDataException error) { return new(null, [], error.Message); }
    }

    private sealed class Queries(GameState state, OwnedArmyGovernorComparatorInputs inputs)
        : IArmyReturnGovernorCurrentQueries
    {
        public ArmyReturnGovernorComparatorFields? ReadCurrentComparatorFields(int personPhysical) =>
            inputs.ReadCurrentComparatorFields(state, personPhysical);
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    public IPlayableGateEntryStateRules CreateGateEntryStateRules() =>
        new OwnedArmyOrdinaryGateReturnStage(this, _governorInputs);
}
