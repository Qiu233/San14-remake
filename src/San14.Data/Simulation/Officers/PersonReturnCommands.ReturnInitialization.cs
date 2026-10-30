using San14.Core;

namespace San14.Data;

public static partial class PersonReturnCommands
{
    /// <summary>Direct1F68B0 initialization after the caller has selected its
    /// actual members. Unlike cancellation, it does not require Person status
    ///1..4. The same receiver allocation, association, current position, route,
    /// flags and deadline writer remains the only Return-task implementation.</summary>
    internal static GameState ScheduleReturnCurrent(GameState state, SourceWorldContext current,
        int[] members, NativeMapSeasonDate date, bool isPlayer)
    {
        var result = RescheduleEligibleMembers(state, current, members, isPlayer, date, servingOnly: false);
        return state with { PersonnelCommands = result.Owner };
    }
}
