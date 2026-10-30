using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //178990 retains the Person pointer read at entry. It is not recovered
    //from an old Unit after Full has reset the source Army receiver.
    private static AutomaticTacticActorSource CaptureAutomaticActorEntrySourceCurrent(
        GameState state, SourceWorldContext current, UnitState actor, int physical)
    {
        var reached = ReadAutomaticActorResumeCurrent(state, current, physical);
        if (reached.LatestImage != actor || !reached.NativeValid)
            throw BaseAutomaticMissing("entry_source_current_receiver");
        ushort commander = current.ReadArmyUnitCommanderId(reached.Army)
            ?? throw BaseAutomaticMissing("entry_source_Commander12");
        var person = current.ReadPersonLookupReference(commander <= 6000 ? commander : 0)
            ?? throw BaseAutomaticMissing("entry_source_Person_pointer");
        int personPhysical = person.PhysicalSlot
            ?? throw BaseAutomaticMissing("entry_source_Person_physical");
        return new(actor.ForceId, actor.OfficerId, personPhysical);
    }

    private GameState ApplyCapturedAutomaticActorMeritCurrent(GameState state,
        SourceWorldContext current, AutomaticTacticActorSource source)
    {
        int physical = source.CommanderPersonPhysical
            ?? throw BaseAutomaticMissing("captured_Person_pointer_unknown");
        bool valid = current.ReadPersonNativeValidity(physical)
            ?? throw BaseAutomaticMissing("captured_Person_validity");
        if (!valid) return state;
        int id = current.ReadPersonIdentity(physical)
            ?? throw BaseAutomaticMissing("captured_Person_identity");
        var parameters = state.NativeArmySimulation?.Parameters
            ?? throw BaseAutomaticMissing("captured_Person_parameters");
        string key = SimulationParameterKeys.Tactics.ActivationContribution;
        if (parameters.Float32Values.ContainsKey(key) || !parameters.Int32Values.TryGetValue(key, out int contribution))
            throw BaseAutomaticMissing("captured_Person_activation_contribution");
        var result = _pendingMerit.TryApplyWithinArmyOperation(state, current, id, unchecked(contribution * 10));
        return result.Outcome != PersonPendingMeritOutcome.CurrentInputsUnresolved ? result.State
            : throw BaseAutomaticMissing("captured_Person_merit1695C0");
    }
}
