using San14.Core;

namespace San14.Data;

public sealed partial class ForceUnregisterSource
{
    //27A6B0, before293BD0 changes any Force/Person/District ownership.
    //The return value is the source branch result, not a presentation receipt.
    internal (GameState State, int NativeReturn) ApplyDeathTerminalCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, int victim,
        NativeMapSeasonDate date, List<CampaignEffectRequest> effects)
    {
        if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw Missing("death_terminal_current_pool");
        var result = ApplyTerminalCurrent(state, current, victim, effects);
        return (result.State, result.NativeReturn);
    }

    //Shared27A6B0: the fixed Force table is this business rule's iterator.
    //It creates no registry temporary and needs no separate mutable owner.
    internal ForceUnregisterTerminalResult ApplyTerminalCurrent(GameState state,
        SourceWorldContext current, int victim, List<CampaignEffectRequest> effects)
    {
        var victimForce = new ForceReference(victim);
        int victimOrdinal = Need(current.ReadForcePhysicalSlot(victimForce), "death_terminal_victim60");
        int remaining = 0, remainingPlayers = 0;
        //The actual fixed pointer table, including slot0. Neither managerC0,
        //a Core active-force count nor a blanket1..45 range is this iterator.
        for (int slot = 0; slot < 52; slot++)
        {
            var force = current.ReadForceTableReference(slot) ?? throw Missing("death_terminal_Force_table_pointer");
            if (!Known(current.ReadForceNativeValidity(force), "death_terminal_Force_validity")) continue;
            int ordinal = Need(current.ReadForcePhysicalSlot(force), "death_terminal_Force60");
            if (ordinal == 51 || ordinal is >= 46 and <= 50 || ordinal == victimOrdinal) continue;
            remaining = unchecked(remaining + 1);
            if (package.TalentForceIsPlayerCurrent(state, current, force)) remainingPlayers = unchecked(remainingPlayers + 1);
        }
        //1D3840 first, then signedWorldBC only when that service exists.
        //The selected-user branch's092670 is the cached three-byte empty
        //_guard_check_icall body. It returns0 without a task-result writer.
        bool selectedUserActive = package.ReadApplicationInformationRestriction(state)
            ?? throw Missing("death_terminal_selected_user_service");
        if (selectedUserActive && remainingPlayers == 0) return new(state, 0, remaining, remainingPlayers);

        var tutorial = current.ReadTransferModeState() ?? throw Missing("death_terminal_tutorial_service");
        if (tutorial.Present && Need(tutorial.Value, "death_terminal_tutorial_active") != 0 && remainingPlayers == 0)
            return new(package.ApplyTutorialForceDefeatCurrent(state, current, effects), 1, remaining, remainingPlayers);
        if (remaining == 0)
        {
            Music();
            Announcement(0x2B39);
            //No surviving ordinary Force is a stop, not an award of the
            //separate288800 unification result to a guessed player.
            return new(state with { CampaignTurnStop = new(CampaignTurnStopReason.OrdinaryForcesResolved, null, null) }, 1, remaining, remainingPlayers);
        }
        if (package.TalentForceIsPlayerCurrent(state, current, victimForce) && remainingPlayers < 1)
        {
            bool returning = package.ReadApplicationReturnToTitlePending()
                ?? throw Missing("death_terminal_return_to_title");
            if (!returning) effects.Add(new(null, null) { TalentCommandPresentation =
                new(TalentCommandPresentationOperation.SoundCue, 7, null, null, null, null) });
            Music();
            effects.Add(new(null, null) { MonthlyMaintenance =
                new(MonthlyMaintenanceOperation.EmperorSceneEnter, 55, null, null) });
            Announcement(0x2B3A);
            effects.Add(new(null, null) { MonthlyMaintenance =
                new(MonthlyMaintenanceOperation.EmperorSceneExit, null, null, null) });
            return new(state with { NativePlayableTerminal = new(NativePlayableTerminalKind.PlayerDefeated,
                victimOrdinal, victimOrdinal, PlayableTerminalSource.PlayerDefeatRuleId) }, 1, remaining, remainingPlayers);
        }
        return new(state, 0, remaining, remainingPlayers);

        void Announcement(int root)
        {
            var formatted = package.FormatNormalTurnReport(state, current, root, new());
            if (formatted.Voices.Count != 0) throw Missing("death_terminal_announcement_voice");
            state = formatted.State;
            effects.Add(new(null, null) { TalentCommandPresentation =
                new(TalentCommandPresentationOperation.Announcement, null, null, null, null, formatted.Text) });
        }
        void Music()
        {
            long cycle = state.NativeMapSeason?.AdvancedCalendarSteps ?? throw Missing("death_terminal_music_cycle");
            //404890(25,1,1000f,1000f), queued before the actual following
            //illustration/message. This is an intent, never playback ACK.
            effects.Add(new(null, null) { MapMusicIntent = new(cycle, 25, true, 1000, 1000) });
        }
    }
}
