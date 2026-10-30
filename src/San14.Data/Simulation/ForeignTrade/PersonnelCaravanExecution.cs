using San14.Core;

namespace San14.Data;

internal sealed record PersonnelCaravanAdvanceResult(GameState State, bool Retire);
internal interface IPersonnelCaravanExecution
{
    PersonnelCaravanAdvanceResult AdvanceCaravanCurrent(GameState state, SourceWorldContext current,
        WorldIndexContext pool, PersonnelCaravanTask task, NativeMapSeasonDate date,
        IArmyReturnGovernorCurrentQueries queries, bool borrowedCurrent);
}
