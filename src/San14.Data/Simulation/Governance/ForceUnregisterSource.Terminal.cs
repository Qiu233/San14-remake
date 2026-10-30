using San14.Core;

namespace San14.Data;

internal sealed record ForceUnregisterTerminalResult(GameState State, int NativeReturn,
    int RemainingOrdinaryForces, int RemainingPlayerForces);

public sealed partial class ForceUnregisterSource
{
    //293BD0's optional presentation precedes27A6B0. Absorption supplies
    //param3=1, so a nonsuppressed call opens scene24 without the3201 line.
    internal GameState QueueEliminationPresentationCurrent(GameState state, SourceWorldContext current,
        int victim, bool suppressEliminationPresentation, bool absorption, List<CampaignEffectRequest> effects)
    {
        if (suppressEliminationPresentation ||
            (package.ReadApplicationInformationRestriction(state) ?? throw Missing("elimination_user_selection_service"))) return state;
        effects.Add(new(null, null) { ForceCoalitionVisual =
            new(ForceCoalitionVisualOperation.NarrationEnter, null) { SceneKind = 24 } });
        if (absorption) return state;
        if (!(package.ReadApplicationReturnToTitlePending() ?? throw Missing("elimination_return_to_title")))
            effects.Add(new(null, null) { TalentCommandPresentation =
                new(TalentCommandPresentationOperation.SoundCue, 8, null, null, null, null) });
        var text = package.FormatNormalTurnReport(state, current, 0x3201, new(Force0: victim));
        if (text.Voices.Count != 0) throw Missing("elimination_announcement_voice");
        effects.Add(new(null, null) { TalentCommandPresentation =
            new(TalentCommandPresentationOperation.Announcement, null, null, null, null, text.Text) });
        return text.State;
    }

    //279EA0 records campaign accomplishments in World169C, not Steam state.
    //These four source pairs are ordinary Person identities, not Force slots.
    private GameState ApplyHistoricalEliminationFlagsCurrent(GameState state, SourceWorldContext current,
        int victim, int captor)
    {
        var retiring = current.ReadForceTableReference(victim) ?? throw Missing("elimination_flag_victim_pointer");
        var receiving = current.ReadForceTableReference(captor) ?? throw Missing("elimination_flag_captor_pointer");
        if (!Known(current.ReadForceNativeValidity(retiring), "elimination_flag_victim_validity") ||
            !Known(current.ReadForceNativeValidity(receiving), "elimination_flag_captor_validity")) return state;
        int retiringRuler = Need(current.ReadMoraleWaveRuler22EAC0(retiring), "elimination_flag_victim_ruler");
        if (retiringRuler is not (521 or 35)) return state;
        int receivingRuler = Need(current.ReadMoraleWaveRuler22EAC0(receiving), "elimination_flag_captor_ruler");
        if (!package.TalentForceIsPlayerCurrent(state, current, receiving)) return state;
        int bit = (retiringRuler, receivingRuler) switch
        { (521, 952) => 0, (521, 35) => 1, (521, 769) => 2, (35, 266) => 3, _ => -1 };
        if (bit < 0) return state;
        var owner = state.CampaignEventWorld ?? throw Missing("elimination_campaign_accomplishment_owner");
        uint flags = Need(owner.EventAchievementFlags, "elimination_campaign_accomplishments");
        uint after = flags | (1u << bit);
        if (after == flags) return state;
        current.ConditionEventAchievementFlags = after;
        return state with { CampaignEventWorld = owner with { EventAchievementFlags = after } };
    }

    private static GameState PublishReachedEffects(GameState state, List<CampaignEffectRequest> effects,
        CampaignEffectSource source)
    {
        if (effects.Count == 0) return state;
        return state with { CampaignEffectRequests = (state.CampaignEffectRequests ?? throw Missing("terminal_effect_queue"))
            .Append(source, state.NativeClock?.UpdateOrdinal ?? throw Missing("terminal_effect_revision"), effects) };
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    //8232C0 asks27A6B0 before history or2A2760. A known ordinary continuation
    //returns null; a stop preserves the actual terminal state and ordered host
    //requests. It never pretends that absorption or an exit has completed.
    internal GameState? TryApplyDemandSurrenderTerminalCurrent(GameState state, SourceWorldContext current,
        int targetForce, int ownForce, List<CampaignEffectRequest>? effects = null)
    {
        var pending = effects ?? [];
        var result = new ForceUnregisterSource(this).ApplyTerminalCurrent(state, current, targetForce, pending);
        if (result.NativeReturn == 0) return null;
        state = QueueForceTerminalExitCurrent(result.State, pending);
        if (effects is not null || pending.Count == 0) return state;
        return state with { CampaignEffectRequests = (state.CampaignEffectRequests ?? throw TerminalMissing("effect_queue"))
            .Append(CampaignEffectSource.DiplomacyCommands,
                state.NativeClock?.UpdateOrdinal ?? throw TerminalMissing("effect_revision"), pending) };
    }

    //32BD90 ->32BDD0. Source user-selection service can suppress the whole
    //operation. The normal attached application consumes each request later.
    internal GameState QueueForceTerminalExitCurrent(GameState state, List<CampaignEffectRequest> effects)
    {
        if (ReadApplicationInformationRestriction(state) ?? throw TerminalMissing("application_user_selection")) return state;
        _ = ReadApplicationGameplay() ?? throw TerminalMissing("attached_application_owner");
        long cycle = state.NativeMapSeason?.AdvancedCalendarSteps ?? throw TerminalMissing("exit_music_cycle");
        effects.Add(new(null, null) { ApplicationExit = new(ApplicationExitOperation.ReturnToTitle) });
        effects.Add(new(null, null) { MapMusicIntent = new(cycle, 1, false, 1000, 1000) });
        effects.Add(new(null, null) { ApplicationExit = new(ApplicationExitOperation.ResetAudioSpecialMode) });
        return state;
    }

    private static InvalidDataException TerminalMissing(string owner) => new("force.terminal." + owner);
}
