using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private static NormalTurnStoryPlayback ReadStoryStateControlPlayback(GameState state,
        NormalTurnStoryPlayback expected, bool manual)
    {
        var actual = manual ? state.CampaignEventCommandPlayback?.Invocation : state.NativeNormalTurnContinuation?.StoryPlayback;
        if (actual is null || actual.Story.Source != expected.Story.Source || actual.Story.Sequence != expected.Story.Sequence)
            throw StoryStateControlLifecycleMissing("invocation_owner");
        return actual;
    }

    private static GameState StoreStoryStateControlPlayback(GameState state,
        NormalTurnStoryPlayback expected, NormalTurnStoryPlayback replacement, bool manual)
    {
        _ = ReadStoryStateControlPlayback(state, expected, manual);
        return manual ? state with { CampaignEventCommandPlayback = state.CampaignEventCommandPlayback! with { Invocation = replacement } }
            : state with { NativeNormalTurnContinuation = state.NativeNormalTurnContinuation! with { StoryPlayback = replacement } };
    }

    private GameState BypassStoryStateControlCurrent(GameState state, NormalTurnStoryPlayback expected, bool manual)
    {
        var playback = ReadStoryStateControlPlayback(state, expected, manual);
        var progress = playback.StateControl ?? throw StoryStateControlLifecycleMissing("bypass_owner");
        if (progress.IsSourceTimeUnavailable) return state;
        if (progress.PendingNotice is not null || progress.AwaitingPresentationBatch is not null ||
            progress.NextOperation is not (0 or 69)) throw StoryStateControlLifecycleMissing("bypass_after_started_tail");
        return StoreStoryStateControlPlayback(state, expected,
            playback with { StateControl = progress with { NextOperation = 69 } }, manual);
    }

    private (GameState State, bool ReadyToExit) AdvanceStoryStateControlCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, NormalTurnStoryPlayback expected, bool manual)
    {
        CampaignStoryStateControlProgress Read(GameState owner) => ReadStoryStateControlPlayback(owner, expected, manual).StateControl
            ?? throw StoryStateControlLifecycleMissing("owner");
        GameState Write(GameState owner, CampaignStoryStateControlProgress replacement)
        {
            var actual = ReadStoryStateControlPlayback(owner, expected, manual);
            return StoreStoryStateControlPlayback(owner, expected, actual with { StateControl = replacement }, manual);
        }
        return AdvanceStoryStateControlCurrent(state, current, pool, Read, Write);
    }

    private (GameState State, bool ReadyToExit) AdvanceStoryStateControlCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool,
        Func<GameState, CampaignStoryStateControlProgress> read,
        Func<GameState, CampaignStoryStateControlProgress, GameState> write)
    {
        var progress = read(state);
        //Without flags4,83C100 never queries the return-to-title gate.
        if (progress.HasChanges == false && progress.PendingNotice is null && progress.AwaitingPresentationBatch is null)
        {
            Save(progress with { NextOperation = 69 });
            return (state, true);
        }
        //The source tests return-to-title before entering its three loops. Do
        //not retest it between a shown notice and its real report/chronicle tail.
        if (progress.NextOperation == 0 && progress.PendingNotice is null && progress.AwaitingPresentationBatch is null &&
            PostEconomyNeed(ReadApplicationReturnToTitlePending(), "event_state_control_return_title"))
        {
            if (!progress.IsSourceTimeUnavailable) Save(progress with { NextOperation = 69 });
            return (state, true);
        }
        if (progress.IsSourceTimeUnavailable) throw StoryStateControlLifecycleMissing("source_time_unavailable");
        if (CampaignStoryStateControlProgress.IsAwaitingPresentation(state)) return (state, false);
        if (progress.AwaitingPresentationBatch is not null)
        {
            if (progress.PendingNotice is { } notice)
            {
                var effects = new List<CampaignEffectRequest>();
                state = CompleteStoryStateControlNoticeCurrent(state, current, pool, notice.Province,
                    notice.Force, notice.Gained, notice.TextAtSource, effects);
                progress = Latest() with { NextOperation = progress.NextOperation + 1, PendingNotice = null, AwaitingPresentationBatch = null };
                Save(progress);
                if (WaitFor(effects)) return (state, false);
            }
            else Save(progress with { AwaitingPresentationBatch = null });
        }
        if (progress.HasChanges != true)
        {
            Save(progress with { NextOperation = 69 });
            return (state, true);
        }
        while (progress.NextOperation < 69)
        {
            int operation = progress.NextOperation, province = operation % 23;
            var pair = progress.Pairs!.Value[province];
            if (operation < 46)
            {
                bool gained = operation < 23;
                int? force = gained ? pair.GainedForcePhysicalSlot : pair.LostForcePhysicalSlot;
                if (Valid(force))
                {
                    var effects = new List<CampaignEffectRequest>();
                    var prepared = PrepareStoryStateControlNoticeCurrent(state, current, province, force!.Value, gained, effects);
                    state = prepared.State;
                    progress = Latest() with { PendingNotice = new(province, force.Value, gained, prepared.Text) };
                    //The actual narration producer always emitted a request.
                    if (!WaitFor(effects)) throw StoryStateControlLifecycleMissing("notice_request");
                    return (state, false);
                }
                Save(progress with { NextOperation = operation + 1 });
                continue;
            }
            var specialEffects = new List<CampaignEffectRequest>();
            state = ApplyStoryStateControlSpecialCurrent(state, current, pool, province, pair, specialEffects);
            Save(Latest() with { NextOperation = operation + 1 });
            if (WaitFor(specialEffects)) return (state, false);
        }
        return (state, progress.IsCompleted);

        CampaignStoryStateControlProgress Latest() => read(state);
        void Save(CampaignStoryStateControlProgress replacement)
        {
            state = write(state, replacement);
            progress = replacement;
        }
        bool Valid(int? physical)
        {
            if (physical is null) return false;
            var force = new ForceReference(physical.Value);
            return PostEconomyNeed(current.ReadForceNativeValidity(force), "event_state_control_Force_validity");
        }
        bool WaitFor(List<CampaignEffectRequest> effects)
        {
            if (effects.Count == 0) return false;
            var outbox = state.CampaignEffectRequests ?? throw StoryStateControlLifecycleMissing("effect_owner");
            outbox = outbox.Append(CampaignEffectSource.MonthlyMaintenance,
                state.NativeMapSeason?.AdvancedCalendarSteps ?? throw StoryStateControlLifecycleMissing("effect_revision"), effects);
            state = state with { CampaignEffectRequests = outbox };
            Save(progress with { AwaitingPresentationBatch = outbox.LastBatchSequence });
            return true;
        }
    }

    private GameState ApplyStoryStateControlSpecialCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, int province, CampaignStoryStateControlPair pair, List<CampaignEffectRequest> effects)
    {
        var selected = current.ReadConditionStateTableSlot(province) ?? throw StoryStateControlLifecycleMissing("Province_pointer");
        if (!PostEconomyNeed(current.ReadConditionStateValidity(selected), "event_state_control_Province_validity")) return state;
        int physical = PostEconomyNeed(selected.PhysicalSlot, "event_state_control_Province_identity");
        int raw = PostEconomyNeed(_growthQueries.ReadStateAdvantage(state, physical), "event_state_control_Province30");
        int advantage = PostEconomyNeed(current.ReadStaticTableReference(0x85990, raw < 18 ? raw : 0)?.PhysicalSlot,
            "event_state_control_Advantage_pointer");
        if (advantage == 0) return state;
        ForceReference? special = null;
        //83C100 uses the first effect1 entry, including when its selected
        //Force is invalid or dependent; it never searches a second substitute.
        for (int index = 0; index < 2; index++)
        {
            if (PostEconomyNeed(_growthQueries.ReadAdvantageEffect(state, advantage, index), "event_state_control_effect") != 1) continue;
            int magnitude = PostEconomyNeed(_growthQueries.ReadCurrent(state,
                PersonGrowthCurrentQueries.MagnitudeKey(advantage, index)), "event_state_control_magnitude");
            if (magnitude is < 0 or > 255) throw StoryStateControlLifecycleMissing("magnitude_byte");
            special = current.ReadForceTableReference(magnitude < 52 ? magnitude : 0) ?? throw StoryStateControlLifecycleMissing("special_Force_pointer");
            break;
        }
        if (special is null || !PostEconomyNeed(current.ReadForceNativeValidity(special), "event_state_control_special_validity")) return state;
        int family = PostEconomyNeed(current.ReadForcePhysicalSlot(special), "event_state_control_special_identity");
        if (PostEconomyNeed(current.ReadConditionForceDependence(new(family)), "event_state_control_special194") != 0) return state;
        if (Force(pair.GainedForcePhysicalSlot) is { } gained)
        {
            int ordinary = PostEconomyNeed(current.ReadForcePhysicalSlot(gained), "event_state_control_gain_identity");
            int level = ReadDiplomacyRelationCurrent(state, current, ordinary, family).Level;
            return ApplyStoryStateAdvantageSpecialPersonnelCurrent(state, current, pool, gained, special, level, effects);
        }
        if (Force(pair.LostForcePhysicalSlot) is null) return state;
        var people = current.CreateTemporaryHexList() ?? throw StoryStateControlLifecycleMissing("recall_list");
        try
        {
            current.ClearTemporaryHexList(people);
            if (family is >= 46 and <= 50)
                for (int lookup = 4101; lookup <= 4200; lookup++)
                {
                    if ((lookup - 4101) % 20 < 10) continue;
                    int person = PostEconomyNeed(current.ReadPersonLookupReference(lookup)?.PhysicalSlot, "event_state_control_recall_pointer");
                    if (!PostEconomyNeed(current.ReadPersonNativeValidity(person), "event_state_control_recall_validity") ||
                        PostEconomyNeed(current.ReadPersonStatus(person), "event_state_control_recall_status") is not (>= 1 and <= 4)) continue;
                    int identity = PostEconomyNeed(current.ReadPersonIdentity(person), "event_state_control_recall_identity");
                    int actual = identity is >= 4001 and <= 4030 ? 46 + (identity - 4001) / 6 :
                        identity is >= 4101 and <= 4200 ? 46 + (identity - 4101) / 20 : 0;
                    if (actual == family) current.AppendPersonReturnTemporary(people, person);
                }
            return ApplySpecialPersonnelRecallCurrent(state, current, pool, people, conditional: false,
                requireValidOriginalForce: true, suppressPrimary: false,
                date: state.NativeMapSeason?.ThirdDate ?? throw StoryStateControlLifecycleMissing("recall_date"), effects: effects);
        }
        finally { current.ClearTemporaryHexList(people); current.ReleaseTemporaryHexList(people); }

        ForceReference? Force(int? slot)
        {
            if (slot is null) return null;
            var force = new ForceReference(slot.Value);
            return PostEconomyNeed(current.ReadForceNativeValidity(force), "event_state_control_pair_validity") ? force : null;
        }
    }

    private static InvalidDataException StoryStateControlLifecycleMissing(string field) => new("story.state-control." + field);
}
