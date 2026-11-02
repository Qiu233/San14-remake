using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IArmyReturnCaptiveRules
{
    public CommandResult ConfirmArmyReturnCaptiveBatch(GameState state,
        ConfirmArmyReturnCaptiveBatchOrder order, CommandContext context)
    {
        try
        {
            var saved = RequirePlayerReturnCaptiveReply(state, order.BatchId, order.Round, order.ForceId, context);
            if (saved.Phase != ArmyReturnCaptivePhase.AwaitingSelection || !Enum.IsDefined(order.Disposition) ||
                order.OfficerIds.IsDefaultOrEmpty || order.OfficerIds.Distinct().Count() != order.OfficerIds.Length ||
                order.OfficerIds.Any(id => !saved.RemainingOfficerIds.Contains(id)))
                throw PlayerReturnCaptiveMissing("batch_selection");
            var source = new PlayerReturnCaptiveCommandSource(this, state);
            source.BeginBatch(order.Disposition, order.OfficerIds);
            return CommandResult.Accept(source.Publish());
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, error.Message); }
    }

    public CommandResult ConfirmArmyReturnCaptiveExecution(GameState state,
        ConfirmArmyReturnCaptiveExecutionOrder order, CommandContext context)
    {
        try
        {
            var saved = RequirePlayerReturnCaptiveReply(state, order.BatchId, order.Round, order.ForceId, context);
            if (saved.Phase != ArmyReturnCaptivePhase.AwaitingExecutionConfirmation || saved.ExecutionConfirmation is null)
                throw PlayerReturnCaptiveMissing("execution_confirmation");
            if (!order.Confirmed)
                return CommandResult.Accept(state with { ArmyReturnCaptiveContinuation = ClearPlayerCaptiveBatch(saved,
                    ArmyReturnCaptivePhase.AwaitingSelection, checked(saved.Round + 1)) });
            var source = new PlayerReturnCaptiveCommandSource(this, state);
            source.ApplySelectedBatch();
            return CommandResult.Accept(source.Publish());
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, error.Message); }
    }

    public CommandResult CompleteArmyReturnCaptivePresentation(GameState state,
        CompleteArmyReturnCaptivePresentationOrder order, CommandContext context)
    {
        try
        {
            var saved = RequirePlayerReturnCaptiveReply(state, order.BatchId, order.Round, order.ForceId, context);
            if (saved.PendingPresentation is not { } notice || saved.Phase is not
                    (ArmyReturnCaptivePhase.AwaitingIntroduction or ArmyReturnCaptivePhase.AwaitingBatchPresentation) ||
                state.CampaignEffectRequests is not { } outbox ||
                outbox.PendingBatches.Any(batch => batch.Sequence == notice.EffectBatchSequence))
                throw PlayerReturnCaptiveMissing("actual_presentation_not_completed");
            bool finished = saved.RemainingOfficerIds.IsEmpty;
            var phase = finished ? ArmyReturnCaptivePhase.ReadyToFinishReturn : ArmyReturnCaptivePhase.AwaitingSelection;
            var next = state with { ArmyReturnCaptiveContinuation = ClearPlayerCaptiveBatch(saved, phase,
                saved.Phase == ArmyReturnCaptivePhase.AwaitingIntroduction ? saved.Round : checked(saved.Round + 1)) };
            if (!finished) next = PreparePlayerReturnCaptiveAdviceCurrent(next, CaptureOwnedArmyReturnWorld(next).Context);
            if (finished && saved.Caller is ArmyReturnCaptiveCaller.OrdinaryEntry or ArmyReturnCaptiveCaller.CampaignWithdrawal)
                next = ResumeDirectArmyReturnCaptiveSuffix(next);
            return CommandResult.Accept(next);
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, error.Message); }
    }

    private static ArmyReturnCaptiveContinuation RequirePlayerReturnCaptiveReply(GameState state,
        ArmyReceiverId id, int round, int force, CommandContext context)
    {
        var saved = state.ArmyReturnCaptiveContinuation ?? throw PlayerReturnCaptiveMissing("reply_owner");
        if (context.DelegatedDistrictId is not null || saved.BatchId != id || saved.Round != round ||
            saved.CapturingForceId != force || saved.Caller == ArmyReturnCaptiveCaller.Unspecified)
            throw PlayerReturnCaptiveMissing("reply_identity");
        return saved;
    }

    private static ArmyReturnCaptiveContinuation ClearPlayerCaptiveBatch(ArmyReturnCaptiveContinuation value,
        ArmyReturnCaptivePhase phase, int round) => value with
    {
        Phase = phase, Round = round, PendingPresentation = null, ExecutionConfirmation = null,
        ActiveDisposition = null, RequestedOfficerIds = [], ActiveBatchOfficerIds = [], ActiveBatchIndex = 0,
        FirstResultPresentation = null, FirstResultQueued = false, AwaitingPersonDeathOfficerId = null
    };

    private sealed partial class PlayerReturnCaptiveCommandSource
    {
        private readonly ArmyCampaignRuleAdapter package;
        private readonly SourceWorldContext current;
        private readonly WorldIndexContext pool;
        private readonly ArmyReturnCaptivesSource owner;
        private readonly ArmyReturnDistrictOperationQueries district;
        private long? latestPresentationBatch;
        private GameState State { get => owner.CurrentState; set => owner.AdoptCurrentState(value); }
        private ArmyReturnCaptiveContinuation Pending => State.ArmyReturnCaptiveContinuation
            ?? throw PlayerReturnCaptiveMissing("command_pending_owner");
        private TacticSourceSamplerWorld Sampler() => new(Pending.SampleDay, Pending.SampleSalt);
        private int Day(GameState _) => Pending.SampleDay;
        private BaseReference Home => Pending.Destination.Kind == SettlementBaseKind.City
            ? new(BaseKind.City, Pending.Destination.Id) : new(BaseKind.Gate, Pending.Destination.Id - 51);

        internal PlayerReturnCaptiveCommandSource(ArmyCampaignRuleAdapter package, GameState input)
        {
            this.package = package;
            var world = package.CaptureOwnedArmyReturnWorld(input);
            current = world.Context; pool = world.Pool;
            owner = new(package, input, new(true));
            district = owner.BorrowDistrictOperationCurrent(current, package.CreateCaptureGovernorQueries(input),
                Pending.SourceDate, Pending.SourceRevision, Day, _ => Sampler());
        }

        internal void BeginBatch(ArmyReturnCaptiveDisposition choice, ImmutableArray<int> requested)
        {
            var pending = Pending;
            foreach (int id in requested)
            {
                int person = Person(id);
                if (current.ReadPersonNativeValidity(person) != true || current.ReadPersonStatus(person) != 6 ||
                    current.ReadPersonSecondLocation(person) != pending.ArmyPhysicalSlot + 61)
                    throw PlayerReturnCaptiveMissing("current_selected_captive");
            }
            State = State with { ArmyReturnCaptiveContinuation = pending with
            {
                ActiveDisposition = choice, RequestedOfficerIds = requested, ActiveBatchOfficerIds = requested,
                ActiveBatchIndex = 0, FirstResultPresentation = null, FirstResultQueued = false,
                PendingPresentation = null, ExecutionConfirmation = null
            } };
            if (choice == ArmyReturnCaptiveDisposition.Execute && requested.Length > 1)
            {
                string text = Text(0x2C46, new());
                State = State with { ArmyReturnCaptiveContinuation = Pending with
                {
                    Phase = ArmyReturnCaptivePhase.AwaitingExecutionConfirmation,
                    ExecutionConfirmation = new(pending.BatchId, pending.Round, pending.CapturingForceId, requested, text)
                } };
                return;
            }
            ApplySelectedBatch();
        }

        internal void ApplySelectedBatch()
        {
            var saved = Pending;
            var choice = saved.ActiveDisposition ?? throw PlayerReturnCaptiveMissing("active_batch_disposition");
            var selected = saved.RequestedOfficerIds;
            var rejected = Sort(selected.Where(id => choice == ArmyReturnCaptiveDisposition.Recruit
                ? !Trial(id).RecruitmentAccepted : choice == ArmyReturnCaptiveDisposition.Execute && Trial(id).ExecutionProtected).ToImmutableArray());
            PrepareRejectedNotices(choice, rejected, saved.RequestedOfficerIds.Length);
            selected = Sort(selected.Where(id => !rejected.Contains(id)).ToImmutableArray());
            State = State with { ArmyReturnCaptiveContinuation = Pending with
                { Phase = ArmyReturnCaptivePhase.ApplyingBatch, ExecutionConfirmation = null, ActiveBatchOfficerIds = selected, ActiveBatchIndex = 0 } };
            ApplyActiveBatch();
        }

        internal void ApplyActiveBatch()
        {
            var pending = Pending;
            var disposition = pending.ActiveDisposition ?? throw PlayerReturnCaptiveMissing("active_disposition");
            for (int index = pending.ActiveBatchIndex; index < pending.ActiveBatchOfficerIds.Length; index++)
            {
                int id = pending.ActiveBatchOfficerIds[index], person = Person(id);
                ushort flags = current.ReadArmyReturnPersonFlags196(person) ?? throw PlayerReturnCaptiveMissing("selected_flags");
                current.WriteArmyReturnPersonFlags196(person, (ushort)(flags & 0xEFAF));
                var feedback = Pending.FirstResultPresentation is null ? CaptiveTreatmentFeedback.CopyFirst : CaptiveTreatmentFeedback.Silent;
                var effects = new List<CampaignEffectRequest>();
                State = package.ApplyOrdinaryCaptiveTreatmentCurrent(State, current, person, Home,
                    disposition switch
                    {
                        ArmyReturnCaptiveDisposition.Recruit => ArmyReturnCaptiveTreatment.Recruit,
                        ArmyReturnCaptiveDisposition.Execute => ArmyReturnCaptiveTreatment.Execute,
                        ArmyReturnCaptiveDisposition.Release => ArmyReturnCaptiveTreatment.Release,
                        ArmyReturnCaptiveDisposition.Keep => ArmyReturnCaptiveTreatment.Keep,
                        _ => throw PlayerReturnCaptiveMissing("disposition")
                    }, Pending.SourceDate, Pending.SourceRevision, Day, Sampler, effects, feedback,
                    message => { if (!string.IsNullOrEmpty(message.TextAtSource) && Pending.FirstResultPresentation is null)
                        State = State with { ArmyReturnCaptiveContinuation = Pending with { FirstResultPresentation = message } }; },
                    () => State, value => State = value);
                Append(effects);
                if (State.PersonDeathContinuation is not null)
                {
                    State = State with { ArmyReturnCaptiveContinuation = Pending with
                        { Phase = ArmyReturnCaptivePhase.AwaitingPersonDeath, ActiveBatchIndex = index, AwaitingPersonDeathOfficerId = id } };
                    return;
                }
                CompleteActivePerson(id, index + 1);
            }
            FinishBatch();
        }

        internal void ResumeAfterPersonDeath()
        {
            if (Pending.Phase != ArmyReturnCaptivePhase.AwaitingPersonDeath ||
                State.PersonDeathContinuation is not null || Pending.AwaitingPersonDeathOfficerId is not int id ||
                Pending.ActiveBatchIndex >= Pending.ActiveBatchOfficerIds.Length ||
                Pending.ActiveBatchOfficerIds[Pending.ActiveBatchIndex] != id)
                throw PlayerReturnCaptiveMissing("death_batch_continuation");
            CompleteActivePerson(id, checked(Pending.ActiveBatchIndex + 1));
            State = State with { ArmyReturnCaptiveContinuation = Pending with { Phase = ArmyReturnCaptivePhase.ApplyingBatch } };
            ApplyActiveBatch();
        }

        private void CompleteActivePerson(int id, int next)
        {
            State = State with { ArmyReturnCaptiveContinuation = Pending with
                { RemainingOfficerIds = Pending.RemainingOfficerIds.Remove(id), ActiveBatchIndex = next, AwaitingPersonDeathOfficerId = null } };
            if (!Pending.FirstResultQueued && Pending.FirstResultPresentation is { } result)
            {
                Append([new(null, null) { TalentCommandPresentation = result }]);
                State = State with { ArmyReturnCaptiveContinuation = Pending with { FirstResultQueued = true } };
            }
        }

        private void FinishBatch()
        {
            var pending = Pending;
            if (pending.RequestedOfficerIds.Length > 1 && !pending.ActiveBatchOfficerIds.IsEmpty &&
                pending.FirstResultPresentation is { OfficerId: int speaker } &&
                pending.ActiveDisposition is not ArmyReturnCaptiveDisposition.Keep)
            {
                int root = pending.ActiveDisposition switch
                { ArmyReturnCaptiveDisposition.Recruit => 0x2B91, ArmyReturnCaptiveDisposition.Execute => 0x2B97,
                    ArmyReturnCaptiveDisposition.Release => 0x2B98, _ => throw PlayerReturnCaptiveMissing("summary_disposition") };
                var summary = Message(root, new(Person0: speaker, Integers: [pending.ActiveBatchOfficerIds.Length]), null);
                Append([new(null, null) { TalentCommandPresentation = summary }]);
            }
            var outbox = State.CampaignEffectRequests ?? throw PlayerReturnCaptiveMissing("batch_outbox");
            if (latestPresentationBatch is long presentationSequence)
                State = State with { ArmyReturnCaptiveContinuation = Pending with
                {
                    Phase = ArmyReturnCaptivePhase.AwaitingBatchPresentation,
                    PendingPresentation = new(pending.BatchId, pending.Round, pending.CapturingForceId, presentationSequence)
                } };
            else
            {
                bool complete = Pending.RemainingOfficerIds.IsEmpty;
                State = State with { ArmyReturnCaptiveContinuation = ClearPlayerCaptiveBatch(Pending,
                    complete ? ArmyReturnCaptivePhase.ReadyToFinishReturn : ArmyReturnCaptivePhase.AwaitingSelection,
                    checked(Pending.Round + 1)) };
            }
        }

        internal GameState Publish()
        {
            var state = OwnedArmyReturnCurrentFields.PublishCurrent(State, current);
            state = PersonAssignmentPrecallInputs.PublishCurrent(state, current);
            state = PersonReturnDistrictTransferSource.PublishAreaAssignments(state, current, district.ChangedAreas, district.ChangedGates);
            state = PersonReturnDistrictReorganizationSource.PublishCurrent(state, current, district);
            state = OwnedArmyLifecycleDetachedInputs.PublishCurrent(state, current);
            state = state with { PersonnelCommands = current.ReadOwnedPersonCommandManager() };
            state = state.WithArmyLifecycle(state.ReadArmyLifecycle()! with { Indexes = pool.ExportOwnedState() });
            State = state;
            if (state.ArmyReturnCaptiveContinuation is { Phase: ArmyReturnCaptivePhase.ReadyToFinishReturn,
                Caller: ArmyReturnCaptiveCaller.OrdinaryEntry or ArmyReturnCaptiveCaller.CampaignWithdrawal })
                return package.ResumeDirectArmyReturnCaptiveSuffix(state);
            return state;
        }

        private ImmutableArray<int> Sort(ImmutableArray<int> ids)
        {
            if (ids.Length < 2) return ids;
            var abilities = new TacticRemakeCurrentAbilities(current, package._scenario, package.Seed.Language,
                SimulationRuleIdentity.ForLocale(package.Seed.Language));
            var sums = new Dictionary<int, int>();
            int Sum(int id)
            {
                if (sums.TryGetValue(id, out int saved)) return saved;
                int sum = 0;
                for (int d = 0; d < 5; d++) sum = unchecked(sum + (abilities.ResolveDimension(State, id, d,
                    (force, effect) => package._baseCapacityInputs.ReadPanelModifier(State, force, effect))
                    ?? throw PlayerReturnCaptiveMissing("batch_current_ability")));
                sums.Add(id, sum);
                return sum;
            }
            int Birth(int id) => State.Officers[id].BirthYear ?? throw PlayerReturnCaptiveMissing("batch_birth_year");
            bool Before(int first, int second)
            {
                int a = Sum(first), b = Sum(second);
                if (a != b) return a > b;
                int bornA = Birth(first), bornB = Birth(second);
                return bornA != bornB ? bornA < bornB : first < second;
            }
            var sorted = ids.ToArray();
            for (int i = 1; i < sorted.Length; i++)
            {
                int moving = sorted[i], at = i;
                while (at > 0 && Before(moving, sorted[at - 1]))
                { sorted[at] = sorted[at - 1]; at--; }
                sorted[at] = moving;
            }
            return sorted.ToImmutableArray();
        }
        private ArmyReturnCaptiveTrial Trial(int id) => Pending.RecruitmentTrials.First(row => row.OfficerId == id);
        private int Person(int id) => current.ReadPersonLookupReference(id)?.PhysicalSlot ?? throw PlayerReturnCaptiveMissing("Person_lookup");
        private void Append(IReadOnlyList<CampaignEffectRequest> requests)
        {
            if (requests.Count == 0) return;
            State = State with { CampaignEffectRequests = (State.CampaignEffectRequests ?? throw PlayerReturnCaptiveMissing("effect_owner"))
                .Append(CampaignEffectSource.ArmyBattleDefeat, Pending.SourceRevision, requests) };
            if (requests.Any(request => request.TalentCommandPresentation is not null))
                latestPresentationBatch = State.CampaignEffectRequests.LastBatchSequence;
        }
        private string Text(int root, HistoryMessageOperands args)
        { var formatted = package.FormatNormalTurnReport(State, current, root, args); State = formatted.State; return formatted.Text; }
        private TalentCommandPresentationRequest Message(int root, HistoryMessageOperands args, int? speaker)
        {
            var formatted = package.FormatNormalTurnReport(State, current, root, args); State = formatted.State;
            if (formatted.Voices.Count > 1) throw PlayerReturnCaptiveMissing("message_multiple_voices");
            return new(speaker is null ? TalentCommandPresentationOperation.Announcement : TalentCommandPresentationOperation.OfficerText,
                null, speaker, null, null, formatted.Text)
            {
                Voice = formatted.Voices.FirstOrDefault() is { } voice ? SaveVoice(voice) : null,
                Speaker = speaker is int id ? new(id,
                    (State.Officers[id].CurrentSurname ?? throw PlayerReturnCaptiveMissing("message_surname")) +
                    (State.Officers[id].CurrentGivenName ?? throw PlayerReturnCaptiveMissing("message_given_name")),
                    current.ReadPersonPortraitAppearance(Person(id))) : null
            };
        }
    }
}
