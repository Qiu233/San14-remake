using San14.Core;

namespace San14.Data;

//31FD60/31FDD0 operate on the World's selected-user statistics, not District14.
internal static class ApplicationCommandAccountingSource
{
    //31B080 constructs both words as zero;326230 resets the complete44-byte
    //statistics block. Only the normal new-World construction invokes this.
    internal static GameState InstallFresh(GameState state) => state with
        { ApplicationCommandAccounting = new(0, 0) };

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
}
