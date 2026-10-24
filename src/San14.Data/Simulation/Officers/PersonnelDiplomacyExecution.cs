using San14.Core;

namespace San14.Data;

internal sealed record PersonnelDiplomacyAdvanceResult(GameState State, bool Retire);
internal interface IPersonnelDiplomacyExecution
{
    PersonnelDiplomacyAdvanceResult AdvanceDiplomacyCurrent(GameState state,
        SourceWorldContext current, PersonnelDiplomacyTask task, NativeMapSeasonDate date);
}
