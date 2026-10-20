using San14.Core;

namespace San14.Data;

/// <summary>TC2EF9F0: consume the reached first intermediate destination and
/// its adjacent duplicates. The final order, route step and credit are separate.</summary>
internal static class ArmyWaypointArrivalSource
{
    internal static ArmyMarchState? Consume(GameState state, UnitState army)
    {
        if (army.March is not { } march || ArmySavedCommandKnowledge.ReadControl(state, army) is not byte control) return null;
        if (control != 0)
            return march with { Target = ArmyMarchDestination.None, FollowingWaypoints = new([]) };
        if (ArmyMarchSourceAdapter.ReadOrdinaryWaypoints(march) is not { } words) return null;
        ushort previous;
        do
        {
            previous = words[0];
            for (int i = 0; i < 4; i++)
                words[i] = words[i + 1] == ArmyMarchSourceAdapter.SourceHexCount
                    ? words[i + 1] : Math.Min(words[i + 1], (ushort)(ArmyMarchSourceAdapter.SourceHexCount - 1));
            words[4] = ArmyMarchSourceAdapter.SourceHexCount;
        } while (previous != ArmyMarchSourceAdapter.SourceHexCount && previous == words[0]);
        int last = 4;
        while (last > 0 && words[last] == ArmyMarchSourceAdapter.SourceHexCount) last--;
        return march with
        {
            Target = ArmyMarchSourceAdapter.DecodeDestination(words[0]),
            FollowingWaypoints = new(words.Skip(1).Take(last)
                .Select(word => (ArmyMarchDestination?)ArmyMarchSourceAdapter.DecodeDestination(word)).ToArray())
        };
    }
}
