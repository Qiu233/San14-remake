using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //374D20: the numerical calculation happens once during registration,
    //before any camera or duel-window phase. Registration remains inside the
    //uncommitted campaign transaction until the actual aftermath completes.
    private GameState RegisterSelectedDuel(GameState state, SourceWorldContext current,
        ArmyDuelSelectedPair selected, NativeArmyProgressStageMoment moment)
    {
        // This route capture owns the shared association index, but its
        // task payload fields must come from this current campaign owner.
        // Bind only for a genuinely selected duel before position queries.
        PersonReturnCommands.BindCurrent(state, current);
        PersonArmyCaptureInputs.BindCurrent(state, current);
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language,
            SimulationRuleIdentity.ForLocale(Seed.Language));
        var result = ArmyDuelNumericSource.Evaluate(state, current, selected, moment.PreWriterThirdDate,
            FSampler(state, moment), (id, dimension) => abilities.ResolveDimension(state, id, dimension,
                (force, effect) => _baseCapacityInputs.ReadPanelModifier(state, force, effect)),
            //The normal owned campaign records the genuine absent task scene;
            //an original task-scene transaction has a different composition.
            new CaptureApplicationOwners(false));
        var pair = new CampaignDuelPair(selected.ActorUnitId, selected.OpponentUnitId, selected.AssistUnitId,
            selected.ActorPersonId, selected.OpponentPersonId, selected.AssistPersonId,
            selected.PlayerSide, selected.CurrentSetting50);
        var prepared = new CampaignDuelResult((CampaignDuelOutcome)result.PlayerOutcome, result.InstantDefeat,
            (CampaignDuelAftermath)result.Aftermath, result.FirstStrikerPersonId, result.WinnerPersonId,
            result.LoserPersonId, Side(result.Actor), Side(result.Opponent));
        var registered=CampaignDuelRegistration.Register(state,pair,prepared,moment.PreWriterThirdDate,
            moment.CalendarCycleOrdinal);
        var service=registered.CampaignDuelService!;
        return registered with{CampaignDuelService=service with
        {Registered=service.Registered! with{PresentationSnapshot=CaptureDuelPresentationSnapshotCurrent(registered,current,pair)}}};

        static CampaignDuelParticipant Side(ArmyDuelSideNumericResult side) => new(side.PersonId,
            (CampaignDuelOutcome)side.Outcome, side.ReadinessBefore, side.ReadinessAfter, side.WriteReadiness,
            side.PreparedInitialReadiness, side.PreparedFinalReadiness, side.ReadinessTrajectory.ToArray(),
            side.Rounds.Select(round => new CampaignDuelRound(round.Number, round.Damage,
                (CampaignDuelRoundAction)round.Action)).ToArray());
    }
}
