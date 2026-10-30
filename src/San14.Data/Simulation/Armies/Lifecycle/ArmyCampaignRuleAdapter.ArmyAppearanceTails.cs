using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private Func<bool?>? armyDepartureController;

    /// <summary>The real map layout manager's ArmyInfo controller, independent
    /// from whether an individual army already has a model or a label.</summary>
    public void ConfigureArmyDepartureController(Func<bool?>? presence) => armyDepartureController = presence;

    internal GameState CompleteArmyDeparturePresentationCurrent(GameState state, SourceWorldContext current,
        UnitState army, NativeMapSeasonDate? sourceDate)
    {
        int physical = army.NativeArmyOrigin?.NativeSlotIndex ?? throw DepartureMissing("Army_physical");
        bool controller = armyDepartureController?.Invoke() ?? throw DepartureMissing("Map_ArmyInfo_controller_presence");
        var grouping = new ArmyMapGroupingCurrentQueries(state, current, _scenario, _governorInputs,
            Seed.Language, SimulationRuleIdentity.Version);
        bool reconcile = !(grouping.IsAtHome2C96E0(physical) ?? throw DepartureMissing("model_at_home"));
        int person = current.ResolveOfficerSource(army.OfficerId) ?? throw DepartureMissing("commander_pointer");
        if (reconcile || controller)
        {
            int appearance = current.ReadPersonPortraitAppearance(person) ?? throw DepartureMissing("commander_appearance");
            var pending = state.CampaignEffectRequests ?? throw DepartureMissing("pending_owner");
            long revision = state.NativeClock?.UpdateOrdinal ?? throw DepartureMissing("source_revision");
            state = state with { CampaignEffectRequests = pending.Append(CampaignEffectSource.ArmyMovement, revision,
                [new(null, null) { ArmyDeparture = CaptureArmyDeparturePresentationCurrent(state, current, army,
                    appearance, reconcile, controller) }]) };
        }
        if (!controller) return state;

        //2307D0 builds its real temporary in slot order. Scan only successfully
        //appended nodes; break on the first qualifying trait even if neither
        //speech family accepts it. A missing individual label never suppresses
        //the independent1A3BF0 speech admission.
        var temporary = current.CreatePersonInjuryTemporary();
        try
        {
            current.ClearPersonInjuryTemporary(temporary, false);
            for (int index = 0; index < 9; index++)
            {
                var trait = TacticRemakeCurrentTraits.ResolveCommanderPersonality(current, person, index)
                    ?? throw DepartureMissing("commander_personality");
                if (current.ReadSkillPersonalityValidity(trait) ?? throw DepartureMissing("personality_validity"))
                    current.AppendPersonInjuryTemporary(temporary, trait);
            }
            var node = current.ReadSkillTemporaryHead(temporary) ?? throw DepartureMissing("personality_head");
            while (node.Identity.HasValue)
            {
                var trait = current.ReadSkillTemporaryPayload(node) ?? throw DepartureMissing("personality_payload");
                int key = trait.PhysicalSlot ?? throw DepartureMissing("personality_identity");
                if (key is not (>= 26 and <= 29) &&
                    (current.ReadPersonalityWord(trait, 0xB4) ?? throw DepartureMissing("personality_B4")) == ushort.MaxValue)
                {
                    var requests = new List<CampaignEffectRequest>();
                    var date = sourceDate ?? state.NativeMapSeason?.ThirdDate ?? throw DepartureMissing("source_date");
                    var feedback = new ArmyTraitFeedback(state, current, date, requests, () => state,
                        speechConsumer: request =>
                        {
                            int action = ArmyTraitFeedbackRequest.HasContext(request.TraitId) ? 26 : request.TraitId == 20 ? 27 : 0;
                            if (action != 0)
                                state = QueueArmyMapSpeechCurrent(state, current, request.ArmyId, request.OfficerId,
                                    action, request.SpeakerHexId, contextTraitId: request.TraitId,
                                    contextAreaId: request.ContextHexId);
                            return true; //The actual support/admission branch has run, not played.
                        });
                    feedback.RefreshAndFeedback(new(physical), trait);
                    state = feedback.Publish(state);
                    if (requests.Count != 0)
                        state = state with { CampaignEffectRequests = (state.CampaignEffectRequests ?? throw DepartureMissing("trait_pending_owner"))
                            .Append(CampaignEffectSource.ArmyMovement, state.NativeClock?.UpdateOrdinal
                                ?? throw DepartureMissing("trait_source_revision"), requests) };
                    break;
                }
                node = current.ReadHexListNodeNext(node) ?? throw DepartureMissing("personality_next");
            }
        }
        finally { current.ClearPersonInjuryTemporary(temporary, true); }
        return state.WithArmyLifecycle(state.ReadArmyLifecycle()! with
            { Indexes = (current.ReadOwnedCurrentListPool() ?? throw DepartureMissing("personality_pool")).ExportOwnedState() });
    }

    internal GameState ApplyArmyDepartureTerritoryCurrent(GameState state, SourceWorldContext current, UnitState army, int hex)
    {
        var pool = current.ReadOwnedCurrentListPool() ?? throw DepartureMissing("territory_pool");
        current.ArmyReturnTransferMode = ReadApplicationTransferModeCurrent();
        var transferred = new MonthlyCampaignMaintenanceSource(this, state, current, pool)
            .ApplyArmyDepartureOccupation(hex, army.ForceId);
        state = transferred.State;
        if (!transferred.Transferred) return state;
        int person = current.ResolveOfficerSource(army.OfficerId) ?? throw DepartureMissing("reward_Person_pointer");
        if (!(current.ReadPersonNativeValidity(person) ?? throw DepartureMissing("reward_Person_validity"))) return state;
        int home = current.ReadPersonFirstLocation(person) ?? throw DepartureMissing("reward_Person_Home");
        int personForce = (current.ReadPersonForce(person) ?? throw DepartureMissing("reward_Person_Force")).PhysicalSlot
            ?? throw DepartureMissing("reward_Force_physical");
        int id = current.ReadPersonIdentity(person) ?? throw DepartureMissing("reward_Person_identity");
        if (home is >= 1 and <= 61 && (personForce is >= 46 and <= 50 || BattleRemakeCurrentRegionFactor.IsNativeBandPerson(id)))
            return state;
        var sourceArmy = PersonArmyLocationSource.ReadCommanderArmy(current, person) ?? throw DepartureMissing("reward_Person_Army");
        if (!(current.ReadArmyUnitNativeValidity(sourceArmy) ?? throw DepartureMissing("reward_Army_validity")))
        {
            var assignments = current.ReadAssignmentPersonAreaVector(person) ?? throw DepartureMissing("reward_Person_areas");
            bool assigned = false;
            for (int index = 0; index < assignments.ActiveCount1E8; index++)
            {
                int areaId = assignments.Values[index];
                var area = current.ReadAreaTableReference(areaId <= 500 ? areaId : 0) ?? throw DepartureMissing("reward_Area_pointer");
                if (current.ReadAreaNativeValidity(area) ?? throw DepartureMissing("reward_Area_validity")) { assigned = true; break; }
            }
            if (!assigned) return state;
        }
        var growth = _growthSource.TryApplyWithinArmyOperation(state, current, _growthQueries, army.OfficerId, 4,
            OccupationParameter(state, SimulationParameterKeys.ArmyMarch.OccupationGrowthScale), 0);
        if (growth.Outcome == PersonGrowthOutcome.CurrentInputsUnresolved) throw DepartureMissing("occupation_growth");
        state = growth.State;
        var merit = _pendingMerit.TryApplyWithinArmyOperation(state, current, army.OfficerId,
            unchecked(OccupationParameter(state, SimulationParameterKeys.ArmyMarch.OccupationMeritScale) * 10));
        if (merit.Outcome == PersonPendingMeritOutcome.CurrentInputsUnresolved) throw DepartureMissing("occupation_merit");
        return merit.State;
    }

    internal sealed partial class MonthlyCampaignMaintenanceSource
    {
        internal (GameState State, bool Transferred) ApplyArmyDepartureOccupation(int identity, int force)
        {
            var hex = current.ReadHexTableReference(identity) ?? throw DepartureMissing("territory_Hex_pointer");
            bool transferred = RestoreMonthlyOccupation(hex, force, 0);
            return (state.WithArmyLifecycle(state.ReadArmyLifecycle()! with { Indexes = pool.ExportOwnedState() }), transferred);
        }
    }

    private static InvalidDataException DepartureMissing(string name) => new("army.departure." + name);
}
