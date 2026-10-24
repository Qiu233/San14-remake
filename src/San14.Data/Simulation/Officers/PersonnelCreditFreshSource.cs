using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

internal static class PersonnelCreditFreshSource
{
    internal static GameState InstallFresh(GameState state)
    {
        if (state.Phase != TurnPhase.Initializing || state.ForceCreditReserves is not null)
            throw new InvalidDataException("Personnel credit reserves require a fresh world.");
        //227070 constructs all52 Force receivers and clears the six reserve bytes.
        return state with { ForceCreditReserves = new(Enumerable.Range(0, 52).ToImmutableDictionary(
            id => id, _ => ImmutableArray.Create<byte>(0, 0, 0, 0, 0, 0))) };
    }
}
