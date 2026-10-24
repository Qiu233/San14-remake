using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IPersonnelDiplomacyExecution
{
    PersonnelDiplomacyAdvanceResult IPersonnelDiplomacyExecution.AdvanceDiplomacyCurrent(
        GameState state, SourceWorldContext current, PersonnelDiplomacyTask task, NativeMapSeasonDate date)
    {
        if (date.Hour != 8) return new(state, false);
        if (task.AwaitingNegotiation) return new(state, false);
        task = task with { Started = true };
        state = SetDiplomacyTaskCurrent(state, current, task);
        var effects = new List<CampaignEffectRequest>();
        var continuation = DiplomacyContinuationAllowedCurrent(state, current, task);
        if (!continuation.Allowed)
            return new(EndCancelled(), true);
        int remaining = Math.Max(0, unchecked(PersonReturnCommands.CalendarOrdinal(task.DueDate) -
            PersonReturnCommands.CalendarOrdinal(date)));
        if (remaining > 1) return new(state, false);
        continuation = DiplomacyContinuationAllowedCurrent(state, current, task);
        if (!continuation.Allowed) return new(EndCancelled(), true);
        state = WriteDiplomacyArrivalCurrent(state, current, task, effects);
        //The already-issued unilateral break has no acceptance gameplay:
        //3254D0 returns1 for kind6 and81E4C0 always dispatches821BE0.
        //CTreatDiplomacyState's kind6 branches are presentation, not an
        //offer editor or permission for the recipient to veto the command.
        if (task.NativeKind == 6)
        {
            return new(CompleteUnilateralDiplomacyCurrent(state, current, task, date, effects), true);
        }
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw DiplomacyMissing("arrival_player");
        int playerId = TalentKnown(player.PhysicalSlot, "diplomacy_player_id");
        if (TalentKnown(current.ReadForceNativeValidity(player), "diplomacy_player_validity") &&
            (task.ForceId == playerId || task.TargetForceId == playerId))
        {
            task = task with { AwaitingNegotiation = true, NegotiationDate = date,
                Negotiation = ReadInitialDiplomacyNegotiationCurrent(state, current, task) };
            return new(AppendDiplomacyEffects(SetDiplomacyTaskCurrent(state, current, task), effects), false);
        }
        var resolution = ResolveDiplomacyNpcArrivalCurrent(state, current, task);
        state = resolution.State;
        bool hadAlliance = ReadDiplomacyRelationCurrent(state, current, task.ForceId, task.TargetForceId).AllianceMonths != 0;
        //81E4C0's service-counter tail is reached only for a player
        //participant. Both forces here have already failed that exact gate;
        //the three actual treasury debits remain in resolution.State.
        state = ApplyDiplomacyNumericCurrent(state, current, task, task.InitialOffer, resolution.Trial.Accepted, effects, date);
        state = WriteDiplomacyHistoryCurrent(state, current, task, resolution.Trial.Accepted, date, effects, hadAlliance);
        int actor = current.ResolveOfficerSource(task.MemberIds[0]) ?? throw DiplomacyMissing("NPC_command_reward_actor");
        state = ApplyTalentCommonRewardsCurrent(state, current, actor, task.NativeKind, resolution.Trial.Accepted ? 1 : 2);
        state = FinishDiplomacyReturnCurrent(state, current, task, date);
        return new(AppendDiplomacyEffects(SynchronizeDiplomacyProjection(state), effects), true);

        GameState EndCancelled()
        {
            if (continuation.ReportCancellation)
                state = WriteDiplomacyHistoryCurrent(state, current, task, null, date, effects);
            state = RefundCancelledDiplomacyCurrent(state, current, task, effects, date);
            state = FinishDiplomacyReturnCurrent(state, current, task, date);
            return AppendDiplomacyEffects(state, effects);
        }
    }

    private CommandResult ConfirmDiplomacyCurrent(GameState state, ForceState force,
        ConfirmDiplomaticNegotiationOrder order, CommandContext context)
    {
        try
        {
            var task = state.PersonnelCommands?.Diplomacies?.GetValueOrDefault(order.TaskId);
            if (task is not { AwaitingNegotiation: true, IsRetiring: false, NativeKind: not 6 } ||
                task.ForceId != force.Id && task.TargetForceId != force.Id)
                return CommandResult.Reject("There is no current diplomatic negotiation for this force.", "diplomacy.negotiation");
            var date = task.NegotiationDate ?? throw DiplomacyMissing("negotiation_reached_date");
            var world = CaptureOwnedArmyReturnWorld(state);
            var current = world.Context;
            _personAssignmentPrecallInputs.BindCurrent(state, current);
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw DiplomacyMissing("negotiation_player");
            if (player.PhysicalSlot != force.Id)
                return CommandResult.Reject("The force does not own this negotiation decision.", "diplomacy.negotiation_permission");
            if (context.DelegatedDistrictId is int districtId && districtId != task.SourceDistrictId)
                return CommandResult.Reject("The diplomatic decision is outside this district.", "diplomacy.negotiation_scope");
            var negotiation = task.Negotiation ?? ReadInitialDiplomacyNegotiationCurrent(state, current, task);
            if (!negotiation.CanEditConditions && order.Offer != negotiation.Proposal)
                return CommandResult.Reject("The current diplomatic window does not admit changing the conditions.", "diplomacy.negotiation_stage");
            var effects = new List<CampaignEffectRequest>();
            if (negotiation.Stage == DiplomaticNegotiationStage.RecipientDecision && !order.Accepted)
            {
                if (order.MilitaryIntelligence.HasValue)
                    throw new ArgumentException("The readonly diplomatic decision cannot spend military intelligence.");
                (state, negotiation) = OpenDiplomacyRecipientCounterofferCurrent(state, current, task, negotiation, effects);
                return PublishWindow();
            }
            RequireDiplomacyAreaOfferCurrent(state, current, task, order.Offer);
            RequireDiplomacyItemOfferCurrent(state, current, task, order.Offer);
            bool hadAlliance = ReadDiplomacyRelationCurrent(state, current, task.ForceId, task.TargetForceId).AllianceMonths != 0;
            bool accepted = order.Accepted;
            if (accepted && negotiation.Stage != DiplomaticNegotiationStage.RecipientDecision)
            {
                bool changed = order.Offer != negotiation.Proposal;
                if (changed)
                    negotiation = negotiation with { Probability = ReadDiplomacyNegotiationProposalProbability(new DiplomacyQuery(this, state, current, task),
                        task with { Negotiation = negotiation }, order.Offer, force.Id) };
                (state, negotiation) = SpendDiplomacyPlayerIntelligenceCurrent(state, current, task, negotiation, order.Offer, order.MilitaryIntelligence);
                negotiation = negotiation with { Proposal = order.Offer };
                bool spent = order.MilitaryIntelligence is > 0;
                if (changed || spent)
                    state = WriteDiplomacyNegotiationResponseCurrent(state, current,
                        changed ? (spent ? 0x3509 : 0x3508) : 0x3507, force.Id, effects);
                bool response = negotiation.Stage == DiplomaticNegotiationStage.SenderPersuasion && !negotiation.PersuasionLocked ||
                    negotiation.Stage == DiplomaticNegotiationStage.RecipientCounterOffer && (changed || order.MilitaryIntelligence is > 0);
                if (response)
                {
                    var result = ResolveDiplomacyNegotiationResponseCurrent(state, current, task, order.Offer,
                        negotiation.Probability, negotiation.Stage == DiplomaticNegotiationStage.RecipientCounterOffer,
                        negotiation.SenderFirstExpense, negotiation.RecipientExpense, negotiation.PersuasionLocked);
                    state = result.State;
                    if (result.ResponseMessageRoot is int responseRoot)
                        state = WriteDiplomacyNegotiationResponseCurrent(state, current,
                            responseRoot, result.ResponseForceId, effects);
                    negotiation = negotiation with { Proposal = result.Proposal, Probability = result.Probability,
                        SenderFirstExpense = result.SenderFirstExpense, RecipientExpense = result.RecipientExpense,
                        SenderFinalExpense = result.SenderFinalExpense };
                    if (result.NextPlayerStage is int next)
                    {
                        negotiation = negotiation with { Stage = (DiplomaticNegotiationStage)next,
                            CanEditConditions = result.ProposalChanged, PersuasionLocked = false };
                        return PublishWindow();
                    }
                    accepted = (result.Trial ?? throw DiplomacyMissing("negotiation_response_trial")).Accepted;
                }
                else accepted = DrawDiplomacyFinalProbabilityCurrent(state, current, task, negotiation.Probability,
                    () => ReadArmyDestructionSampler(state, current) ?? throw DiplomacyMissing("trial_sampler")).Accepted;
            }
            else if (order.MilitaryIntelligence.HasValue)
                throw new ArgumentException("This diplomatic decision cannot spend military intelligence.");
            task = task with { Negotiation = negotiation };
            state = ApplyDiplomacyNumericCurrent(state, current, task, negotiation.Proposal, accepted, effects, date);
            state = WriteDiplomacyHistoryCurrent(state, current, task, accepted, date, effects, hadAlliance);
            int actor = current.ResolveOfficerSource(task.MemberIds[0]) ?? throw DiplomacyMissing("command_reward_actor");
            state = ApplyTalentCommonRewardsCurrent(state, current, actor, task.NativeKind, accepted ? 1 : 2);
            state = SetDiplomacyTaskCurrent(state, current, task with { AwaitingNegotiation = false });
            state = FinishDiplomacyReturnCurrent(state, current, task, date);
            return CommandResult.Accept(SynchronizeDiplomacyProjection(
                PersonReturnCommands.PublishCurrent(AppendDiplomacyEffects(state, effects), current)));

            CommandResult PublishWindow()
            {
                state = SetDiplomacyTaskCurrent(state, current, task with { Negotiation = negotiation });
                return CommandResult.Accept(PersonReturnCommands.PublishCurrent(AppendDiplomacyEffects(state, effects), current));
            }
        }
        catch (ArgumentException error) { return CommandResult.Reject(error.Message, "diplomacy.offer"); }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, "Diplomatic settlement is unresolved."); }
    }

    //Schema149 could retain kind6 after its arrival presentation. Only the
    //settlement tail is resumed: no continuation trial, arrival or App draw.
    private GameState ContinueUnilateralDiplomacyCurrent(GameState state, SourceWorldContext current,
        int taskId, NativeMapSeasonDate date)
    {
        var task = current.ReadOwnedPersonCommandManager()?.Diplomacies?.GetValueOrDefault(taskId);
        if (task is not { AwaitingNegotiation: true, NativeKind: 6 }) return state;
        if (task.IsRetiring || task.NegotiationDate != date)
            throw DiplomacyMissing("legacy_unilateral_arrival_boundary");
        //These immutable rule constants were first packaged after schema149.
        //Preserve saved values and every unknown current/global parameter.
        state = InstallFreshDiplomacyRequestTerminationParameters(state, preserveExisting: true);
        state = InstallFreshDiplomacyCommandRewardParameters(state, preserveExisting: true);
        return CompleteUnilateralDiplomacyCurrent(state, current, task, date, []);
    }

    private GameState CompleteUnilateralDiplomacyCurrent(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task, NativeMapSeasonDate date, List<CampaignEffectRequest> effects)
    {
        state = ApplyDiplomacyNumericCurrent(state, current, task, task.InitialOffer, true, effects, date);
        state = WriteDiplomacyHistoryCurrent(state, current, task, true, date, effects);
        int actor = current.ResolveOfficerSource(task.MemberIds[0]) ?? throw DiplomacyMissing("cancel_command_reward_actor");
        state = ApplyTalentCommonRewardsCurrent(state, current, actor, task.NativeKind, 1);
        task = task with { AwaitingNegotiation = false };
        state = SetDiplomacyTaskCurrent(state, current, task);
        state = FinishDiplomacyReturnCurrent(state, current, task, date);
        return AppendDiplomacyEffects(SynchronizeDiplomacyProjection(state), effects);
    }

    private static GameState SetDiplomacyTaskCurrent(GameState state, SourceWorldContext current, PersonnelDiplomacyTask task)
    {
        var owner = current.ReadOwnedPersonCommandManager() ?? throw DiplomacyMissing("task_manager");
        owner = owner with { Diplomacies = new(owner.Diplomacies ?? throw DiplomacyMissing("saved_task_owner")) { [task.Id] = task } };
        current.SetOwnedPersonCommandManager(owner);
        return state with { PersonnelCommands = owner };
    }

    private GameState FinishDiplomacyReturnCurrent(GameState state, SourceWorldContext current,
        PersonnelDiplomacyTask task, NativeMapSeasonDate date)
    {
        var district = current.ReadDistrictTableReference(task.SourceDistrictId <= 51 ? task.SourceDistrictId : 0)
            ?? throw DiplomacyMissing("return_District");
        state = PersonReturnCommands.ScheduleTalentCompletionReturnCurrent(state, current, task.Id, task.MemberIds,
            date, IsTalentPlayerPrimaryDistrict(state, current, district));
        var owner = current.ReadOwnedPersonCommandManager() ?? throw DiplomacyMissing("return_task_owner");
        owner = PersonReturnCommands.QueueRetirementCurrent(owner, current, task.Id);
        return state with { PersonnelCommands = owner };
    }

    private static GameState AppendDiplomacyEffects(GameState state, List<CampaignEffectRequest> effects)
        => effects.Count == 0 ? state : state with
        {
            CampaignEffectRequests = (state.CampaignEffectRequests ?? throw DiplomacyMissing("effect_owner"))
                .Append(CampaignEffectSource.DiplomacyCommands,
                    state.NativeMapSeason?.AdvancedCalendarSteps ?? throw DiplomacyMissing("effect_calendar"), effects.ToArray())
        };
}
