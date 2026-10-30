using San14.Core;

namespace San14.Data;

//31FD60/31FDD0 operate on the World's selected-user statistics, not District14.
internal static class ApplicationCommandAccountingSource
{
    //31B080 constructs these counters as zero;326230 resets the complete
    //0x44-byte (68-byte) World78 statistics block. Only the normal new-World
    //construction invokes this.
    internal static GameState InstallFresh(GameState state) => state with
        { ApplicationCommandAccounting = new(0, 0)
            { SingleLinkedTacticCount = 0, DoubleLinkedTacticCount = 0,
                TripleLinkedTacticCount = 0, LargeLinkedTacticCount = 0, ManualTacticUseCount = 0 } };

    internal static bool HasSelectedUser(GameState state, bool? servicePresent) => servicePresent switch
    {
        false => false,
        true => (state.CampaignEventWorld?.CurrentUserIndex ?? throw Missing("selected_user")) != -1,
        _ => throw Missing("application_service")
    };

    internal static GameState Use(GameState state, SourceWorldContext current, int amount, bool? servicePresent)
        => Add(state, current, amount, servicePresent, false);

    internal static GameState Grant(GameState state, SourceWorldContext current, int amount, bool? servicePresent)
        => Add(state, current, amount, servicePresent, true);

    private static GameState Add(GameState state, SourceWorldContext current, int amount, bool? servicePresent, bool grant)
    {
        uint flags = current.ReadWorldTransferServiceFlags() ?? throw Missing("World_flags");
        if ((flags & 0x100) != 0 || !HasSelectedUser(state, servicePresent)) return state;
        var statistics = state.ApplicationCommandAccounting ?? throw Missing("statistics_owner");
        uint before = (grant ? statistics.CommandBooksGranted : statistics.CommandBooksUsed)
            ?? throw Missing(grant ? "books_granted" : "books_used");
        //The native add and comparison are unsigned, including negative int
        //operands and overflow. The caller's requested debit/grant is retained.
        uint after = Math.Min(unchecked(before + (uint)amount), grant ? 10000u : 1000u);
        return state with { ApplicationCommandAccounting = grant
            ? statistics with { CommandBooksGranted = after }
            : statistics with { CommandBooksUsed = after } };
    }

    private static InvalidDataException Missing(string field) => new("application.command-accounting." + field);
}

public sealed partial class ArmyCampaignRuleAdapter
{
    private bool HasCommandAccountingUser(GameState state) => ApplicationCommandAccountingSource.HasSelectedUser(
        state, ReadApplicationGameplay()?.UserSelectionServicePresent);

    private GameState ApplyCommandBookUsageCurrent(GameState state, SourceWorldContext current, int amount)
        => ApplicationCommandAccountingSource.Use(state, current, amount,
            ReadApplicationGameplay()?.UserSelectionServicePresent);

    /// <summary>173E13/1743FD selected-user linked-use statistics and
    ///174572's independent manual-use statistic. This is World78 accounting,
    ///not a platform achievement or a playback acknowledgement.
    ///Call with the real linkedCount before waiting. For the manual-only
    ///post-waiting tail, call again with linkedCount0 and manual=true.</summary>
    private GameState ApplyLinkedTacticAccountingCurrent(GameState state, SourceWorldContext current,
        int physical, int linkedCount, bool manual = false)
    {
        if (linkedCount is < 0 or > 10) throw TacticAccountingMissing("linked_count");
        if (linkedCount == 0 && !manual) return state;
        uint flags = current.ReadWorldTransferServiceFlags() ?? throw TacticAccountingMissing("World_flags");
        if ((flags & 0x100) != 0 || !HasCommandAccountingUser(state)) return state;
        //The source passes the captured receiver pointer.22BC70 reads its
        //current Commander even after Full has released the Unit image.
        var army = current.ReadArmyUnitTableReference(physical) ?? throw TacticAccountingMissing("main_Army_pointer");
        byte rawDistrict = ReadFacilityHistoryDistrict(current, army);
        var district = current.ReadDistrictTableReference(rawDistrict <= 51 ? rawDistrict : 0)
            ?? throw TacticAccountingMissing("main_District_pointer");
        if (!BaseCaptureRewardSource.ReadPlayerRulerDistrict(current, district, TacticAccountingMissing)) return state;
        var statistics = state.ApplicationCommandAccounting ?? throw TacticAccountingMissing("statistics_owner");
        if (linkedCount != 0)
        {
            statistics = linkedCount switch
            {
                1 => statistics with { SingleLinkedTacticCount = Increment(statistics.SingleLinkedTacticCount, "single_linked") },
                2 => statistics with { DoubleLinkedTacticCount = Increment(statistics.DoubleLinkedTacticCount, "double_linked") },
                3 => statistics with { TripleLinkedTacticCount = Increment(statistics.TripleLinkedTacticCount, "triple_linked") },
                _ => statistics with { LargeLinkedTacticCount = Increment(statistics.LargeLinkedTacticCount, "large_linked") }
            };
        }
        if (manual) statistics = statistics with { ManualTacticUseCount = Increment(statistics.ManualTacticUseCount, "manual_use") };
        return state with { ApplicationCommandAccounting = statistics };

        static uint Increment(uint? before, string owner) => Math.Min(unchecked(
            (before ?? throw TacticAccountingMissing(owner)) + 1u), 1000u);
    }
    private static InvalidDataException TacticAccountingMissing(string owner) => new("application.tactic-accounting." + owner);
}
