using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

internal sealed record StoryForceRankMutationResult(GameState State, int? NativeReturn,
    CampaignStoryForceRankContinuation? Continuation, IReadOnlyList<CampaignEffectRequest> Effects,
    int? CompletedPlayerRank);

public sealed partial class ArmyCampaignRuleAdapter
{
    //One real120 invocation. Core owns source/leaf identity and actual input
    //receipts; the world writer runs once before the genuine message barrier.
    internal StoryForceRankMutationResult ApplyStoryForceRankCurrent(GameState state,
        SourceWorldContext current, IReadOnlyList<int> arguments, ForceReference selectedForce,
        int eventId, int eventRecordKind, bool modalAllowed,
        CampaignStoryForceRankContinuation? previous)
    {
        if (arguments.Count != 8) throw StoryRankMissing("arguments");
        var pool = current.ReadOwnedCurrentListPool() ?? throw StoryRankMissing("current_pool");
        var effects = new List<CampaignEffectRequest>();
        CampaignStoryForceRankContinuation progress;
        ForceRankWorldContext context;
        if (previous is null)
        {
            bool manager = RankNeed(current.ReadEventManagerPresent(), "event_manager_presence");
            state = SetStoryRankRecordingGates(state, current, eventId is >= 201 and <= 217 ? 0 : 1,
                manager && eventRecordKind == 1);
            var prepared = ReadForceRankWorldContext(state, current, pool, selectedForce, arguments[1]);
            if (prepared is null) return Complete(selectedForce, arguments[1]);
            context = prepared;
            progress = new(context.ForcePhysicalSlot, context.Rank, arguments[2], context.RulerPhysicalSlot,
                context.EmperorPhysicalSlot, context.SelfProclaimed, context.ImperialOwnerForceSlot,
                CampaignStoryForceRankStage.KingdomName);
            if (context.Rank == 1)
            {
                if (arguments[2] is >= 1 and <= 100)
                    state = StoreEmpireNameCurrent(state, current, context.ForcePhysicalSlot, arguments[2]);
                else if (TalentForceIsPlayerCurrent(state, current, selectedForce))
                {
                    var names = ReadKingdomNameChoicesCurrent(state, current, selectedForce);
                    state = PublishPool(state);
                    if (names.AutomaticSelectionId is int automatic)
                        state = StoreEmpireNameCurrent(state, current, context.ForcePhysicalSlot, automatic);
                    else if (names.Choices.Count != 0)
                    {
                        progress = progress with { KingdomChoices = names.Choices.ToImmutableArray() };
                        return new(state, null, progress, effects, null);
                    }
                }
                else
                {
                    var names = PrepareNormalTurnEmpireNameCurrent(state, current, pool, selectedForce, arguments[2]);
                    state = names.State;
                }
            }
        }
        else
        {
            progress = previous;
            context = new(progress.ForcePhysicalSlot, progress.Rank, progress.RulerPhysicalSlot,
                progress.EmperorPhysicalSlot, progress.SelfProclaimed, progress.ImperialOwnerForceSlot);
            if (progress.Stage == CampaignStoryForceRankStage.Messages)
            {
                if (!progress.MessagesCompleted) return new(state, null, progress, effects, null);
                state = ApplyNormalTurnRankPromotionReportsCurrent(state, current, pool, new(context.ForcePhysicalSlot),
                    context.Rank, context.EmperorPhysicalSlot, context.SelfProclaimed,
                    playerBranch: TalentForceIsPlayerCurrent(state, current, new(context.ForcePhysicalSlot)));
                return Complete(new(context.ForcePhysicalSlot), context.Rank);
            }
            if (progress.Stage != CampaignStoryForceRankStage.KingdomName) throw StoryRankMissing("continuation_stage");
            if (!progress.KingdomSelectionResolved) return new(state, null, progress, effects, null);
            if (progress.SelectedKingdomNameId is int selected)
            {
                if (!progress.KingdomChoices.Any(choice => choice.Id == selected)) throw StoryRankMissing("selected_kingdom_not_eligible");
                state = StoreEmpireNameCurrent(state, current, context.ForcePhysicalSlot, selected);
            }
            //A genuine cancel leaves48 untouched. The current name's native
            //validity below, rather than the cancel itself, controls the writer.
        }

        var force = new ForceReference(context.ForcePhysicalSlot);
        if (context.Rank == 1)
        {
            int actual = RankNeed(current.ReadForceKingdomNameIndex(context.ForcePhysicalSlot), "current_kingdom_name");
            var definitions = ReadEmpireNameResourcesCurrent().CustomNames.Definitions;
            if (!definitions[actual <= 100 ? actual : 0].NativeValidity) return Complete(force, context.Rank);
        }
        var messages = new List<CampaignStoryForceRankMessage>(3);
        bool player = TalentForceIsPlayerCurrent(state, current, force);
        if (player && context.Rank == 1)
        {
            int ruler = RankNeed(current.ReadPersonIdentity(context.RulerPhysicalSlot), "message_ruler_identity");
            var person = state.Officers.GetValueOrDefault(ruler) ?? throw StoryRankMissing("message_ruler_owner");
            var speaker = new TalentOfficerPresentationContext(ruler,
                (person.CurrentSurname ?? throw StoryRankMissing("message_ruler_surname")) +
                (person.CurrentGivenName ?? throw StoryRankMissing("message_ruler_given_name")),
                current.ReadPersonPortraitAppearance(context.RulerPhysicalSlot));
            Format(0x2C6B, new(Person0: ruler, Force0: context.ForcePhysicalSlot, Force1: context.ForcePhysicalSlot), speaker);
        }
        state = ApplyForceRankWorldCurrent(state, current, context, effects);
        if (player)
        {
            var date = state.NativeMapSeason?.ThirdDate ?? throw StoryRankMissing("message_date");
            int emperor = RankNeed(current.ReadPersonIdentity(context.EmperorPhysicalSlot), "message_emperor_identity");
            int root = context.Rank == 1 ? context.SelfProclaimed ? 0x2C7E : 0x2C7C
                : context.SelfProclaimed ? 0x2C67 : 0x2C66;
            Format(root, new(Person0: context.SelfProclaimed ? 0 : emperor,
                Force0: context.ForcePhysicalSlot, Force1: context.ForcePhysicalSlot,
                Integers: [date.Year, date.Month]), null);
            Format(0x2C6A, new(Force0: context.ForcePhysicalSlot), null);
        }
        state = PublishPool(state);
        //2D2580 obeys the real manager1B0/1AC and parent admission. A source
        //skip proceeds to history without inventing any presentation receipt.
        if (messages.Count != 0 && modalAllowed && eventRecordKind != 1)
        {
            progress = progress with { Stage = CampaignStoryForceRankStage.Messages,
                Messages = messages.ToImmutableArray(), KingdomSelectionResolved = true };
            return new(state, null, progress, effects, null);
        }
        state = ApplyNormalTurnRankPromotionReportsCurrent(state, current, pool, force, context.Rank,
            context.EmperorPhysicalSlot, context.SelfProclaimed, playerBranch: player);
        return Complete(force, context.Rank);

        void Format(int root, HistoryMessageOperands operands, TalentOfficerPresentationContext? speaker)
        {
            var formatted = FormatNormalTurnReport(state, current, root, operands);
            state = formatted.State;
            if (formatted.Voices.Count != 0 || formatted.SpatialVoices.Count != 0)
                throw StoryRankMissing("message_unexpected_voice");
            messages.Add(new(formatted.Text, speaker));
        }
        GameState PublishPool(GameState value) => value.WithArmyLifecycle(
            (value.ReadArmyLifecycle() ?? throw StoryRankMissing("lifecycle_owner")) with { Indexes = pool.ExportOwnedState() });
        StoryForceRankMutationResult Complete(ForceReference actualForce, int rank)
        {
            state = PublishPool(state);
            state = SetStoryRankRecordingGates(state, current, 0, false);
            //The outer caller reaches its two independent player tails even
            //when the inner writer legitimately returned0.
            int? playerRank = TalentForceIsPlayerCurrent(state, current, actualForce) ? rank : null;
            int achievement = playerRank switch { 1 => 24, 3 => 25, 5 => 26, 6 => 27, 7 => 28, _ => -1 };
            if (achievement >= 0)
                state = PrepareApplicationServiceCurrent(state, current, profile =>
                {
                    if (RankNeed(current.ReadTransferServiceEligibility(), "application_achievement_eligibility"))
                        SetApplicationAchievementAndCompletionCurrent(current, profile, achievement);
                    return profile;
                });
            return new(state, 1, null, effects, playerRank);
        }
    }

    private static GameState SetStoryRankRecordingGates(GameState state, SourceWorldContext current,
        int chronologySuppression, bool reportSuppressed)
    {
        var chronicle = state.CampaignChronicle ?? throw StoryRankMissing("chronicle_owner");
        var eligibility = state.CampaignEventEligibility ?? throw StoryRankMissing("event_manager_owner");
        current.ReportBlockerState = new(true, reportSuppressed ? 1 : 0);
        return state with { CampaignChronicle = chronicle with { RecordingSuppression = chronologySuppression },
            CampaignEventEligibility = eligibility with { HistoryRecordingSuppressed = reportSuppressed } };
    }

    private static T RankNeed<T>(T? value, string owner) where T : struct => value ?? throw StoryRankMissing(owner);
    private static InvalidDataException StoryRankMissing(string owner) => new("story.force_rank.current." + owner);
}
