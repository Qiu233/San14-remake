using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //26DA10 reaches this through the old District's actual2E9140 check.
    //City ownership already follows its changed District; Object13 and
    //resources can still be old at the first call. The caller owns
    //its Person/Area/reward temporaries and publishes one capture transaction.
    internal GameState ApplyOrdinaryLastCityCaptureCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, int oldForce, int winnerForce, NativeMapSeasonDate date,
        List<CampaignEffectRequest> effects, Action<int>? resetArmyReceiver = null)
    {
        if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile())
            throw LastCityMissing("shared_current_pool");
        if (oldForce is < 1 or > 45 || winnerForce is < 1 or > 45 || oldForce == winnerForce)
            throw LastCityMissing("ordinary_Forces");
        var victim = current.ReadForceTableReference(oldForce) ?? throw LastCityMissing("victim_pointer");
        if (!LastCityNeed(current.ReadForceNativeValidity(victim), "victim_validity")) return state;
        if (!LastCityNeed(current.ReadForceNativeValidity(new(winnerForce)), "winner_validity"))
            throw LastCityMissing("winner_invalid");

        //293BD0's notice and27A6B0 are shared with ordinary unregister
        //and ruler death. A terminal decision returns before any retirement.
        bool selected = ReadApplicationInformationRestriction(state)
            ?? throw LastCityMissing("Application_user_selection");
        if (selected && TalentForceIsPlayerCurrent(state, current, victim))
            throw LastCityMissing("selected_user_retiring_Force44C_color448_owner");
        var retirement = new ForceUnregisterSource(this);
        state = retirement.QueueEliminationPresentationCurrent(state, current, oldForce,
            suppressEliminationPresentation: false, absorption: false, effects);
        var terminal = retirement.ApplyTerminalCurrent(state, current, oldForce, effects);
        state = terminal.State;
        if (terminal.NativeReturn != 0) return QueueForceTerminalExitCurrent(state, effects);
        var result = new ForceUnregisterSource(this).TryApplyCaptureBorrowedCurrent(state,
            oldForce, winnerForce, current, pool, date, effects, resetArmyReceiver);
        return result.State ?? throw LastCityMissing(result.UnresolvedOwner ?? "293BD0");
    }

    //293BD0 writes history after its Object/Hex pass, before its Army loop.
    internal GameState WriteOrdinaryForceUnregisterHistoryCurrent(GameState state,
        SourceWorldContext current, int victim, List<CampaignEffectRequest> effects)
    {
        var main = FormatNormalTurnReport(state, current, 0x3200, new(Force0: victim));
        state = main.State;
        string? supplemental = CampaignHistoryWriter.Supplemental(state);
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw LastCityMissing("history_player_pointer");
        if (LastCityNeed(current.ReadForceNativeValidity(player), "history_player_validity"))
        {
            int ruler = LastCityNeed(current.ReadMoraleWaveRuler22EAC0(player), "history_player_ruler");
            var detail = FormatNormalTurnReport(state, current, 0x533D,
                new(Person0: 5012, Person1: ruler, Force0: victim));
            state = detail.State; supplemental = detail.Text;
        }
        int day = (ReadArmyDestructionSampler(state, current) ?? throw LastCityMissing("history_Day450")).Day450;
        return WriteHistory(state, current, new(5, 0, 0, day, main.Text, supplemental,
            5012, MarkBit0: 1, MarkBit4: 0), effects);
    }

    private GameState LastCityAnnouncement(GameState state, SourceWorldContext current,
        int root, HistoryMessageOperands operands, List<CampaignEffectRequest> effects)
    {
        var text = FormatNormalTurnReport(state, current, root, operands);
        if (text.Voices.Count != 0) throw LastCityMissing("announcement_resource_voice");
        effects.Add(new(null, null) { TalentCommandPresentation = new(
            TalentCommandPresentationOperation.Announcement, null, null, null, null, text.Text) });
        return text.State;
    }

    private void LastCitySound(List<CampaignEffectRequest> effects, int sound)
    {
        bool returning = ReadApplicationReturnToTitlePending() ?? throw LastCityMissing("Application_return_to_title");
        if (!returning) effects.Add(new(null, null) { TalentCommandPresentation = new(
            TalentCommandPresentationOperation.SoundCue, sound, null, null, null, null) });
    }

    //The Hex caller's185D00 uses City0. Therefore ordinary center/Home and
    //scenery reports do not run; special ownership and the terrain gate still
    //use the existing real occupation kernel. Its final2413D0 remains the
    //unconditional store in ForceUnregisterSource.SettleHexes.
    internal GameState ApplyOrdinaryLastCityHexTransferCurrent(GameState state, SourceWorldContext current,
        HexReference hex, int force, List<CampaignEffectRequest> effects)
    {
        state = TransferAbsorptionHexCurrent(state, current, hex, force, 0);
        int actual = LastCityNeed(current.ReadHexForceSlot(hex), "transferred_Hex14");
        if (LastCityNeed(current.ReadForceNativeValidity(new(actual)), "transferred_Force_validity"))
            effects.Add(new(null, null) { MonthlyMaintenance = new(MonthlyMaintenanceOperation.TerritoryOccupation,
                5, null, null) { ForceId = force, HexId = LastCityNeed(hex.PhysicalSlot, "transferred_Hex") } });
        return state;
    }

    private static T LastCityNeed<T>(T? value, string owner) where T : struct => value ?? throw LastCityMissing(owner);
    private static InvalidDataException LastCityMissing(string owner) => new("capture.last-City." + owner);
}
