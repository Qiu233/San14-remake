using San14.Core;

namespace San14.Data;

/// <summary>Ordinary29F220 into a friendly Gate followed by actual28E6D0
/// deferred cleanup. Gate troops/wounds/morale stay on the Gate; currency
/// virtual getters/setters use its current mapped City.</summary>
public sealed class OriginalOwnedArmyOrdinaryGateReturnStage(OriginalOwnedArmyCampaignRules package,
    OriginalOwnedArmyGovernorComparatorInputs governorInputs) : IPlayableGateEntryStateRules
{
    public const string Revision = "san14:ce-owned-army-ordinary-gate-return-20261007";
    public bool RequiresCurrentState(GameState state, UnitState unit, GateState gate) =>
        unit.NativeArmyOrigin is not null && unit.Mission?.Kind != UnitMissionKind.Transport;

    public PlayableCityEntryStateResolution? Return(GameState state, UnitState unit, GateState gate)
    {
        var result = TryApply(state, unit.Id, gate.Id);
        return result.State is { } next ? new(next, Revision) : null;
    }

    public OriginalOwnedArmyOrdinaryCityReturnResult TryApply(GameState state, int unitId, int gateId)
    {
        if (!state.Units.TryGetValue(unitId, out var unit) || !state.Gates.TryGetValue(gateId, out var gate) ||
            unit.NativeArmyOrigin?.NativeSlotIndex is not int physical ||
            state.NativeArmyLifecycle?.Auxiliaries is null || unit.Mission?.Kind == UnitMissionKind.Transport ||
            gate.HexId != unit.HexId || gate.ForceId != unit.ForceId)
            return new(null, [], "current.owned_ordinary_gate_entry");
        try
        {
            var world = package.CaptureOwnedArmyReturnWorld(state);
            var result = OriginalArmyOrdinaryReturnRuntime.PrepareCurrentResourceOutputs(new(physical),
                new(OriginalBaseKind.Gate, gateId - 51), 0, world.Context, world.Pool,
                new Queries(state, governorInputs), new(false, false), new(package, state, new(false)));
            if (result.Packet is not { } packet) return new(null, [], result.UnresolvedOwner);
            var returned = world.CommitPrepared(state, packet);
            if (returned is null) return new(null, packet.SourceEffectOrder, "current.ordinary_gate_return_publication");
            returned = OriginalOwnedArmyLifecycleDetachedInputs.PublishCurrent(returned, world.Context);
            var cleaned = package.ConsumeArmyLifecyclePending(returned);
            return new(cleaned, packet.SourceEffectOrder, cleaned is null ? "current.deferred_cleanup_owner" : null)
            { DistrictTransferAssociations = cleaned is null ? [] : packet.RulerTailAssociationUpdates };
        }
        catch (InvalidDataException error) { return new(null, [], error.Message); }
    }

    private sealed class Queries(GameState state, OriginalOwnedArmyGovernorComparatorInputs inputs)
        : IOriginalArmyReturnGovernorCurrentQueries
    {
        public OriginalArmyReturnGovernorComparatorFields? ReadCurrentComparatorFields(int personPhysical) =>
            inputs.ReadCurrentComparatorFields(state, personPhysical);
    }
}

public sealed partial class OriginalOwnedArmyCampaignRules
{
    public IPlayableGateEntryStateRules CreateGateEntryStateRules() =>
        new OriginalOwnedArmyOrdinaryGateReturnStage(this, _governorInputs);
}
