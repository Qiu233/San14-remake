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
        ConsumeWords(words);
        int last = 4;
        while (last > 0 && words[last] == ArmyMarchSourceAdapter.SourceHexCount) last--;
        return march with
        {
            Target = ArmyMarchSourceAdapter.DecodeDestination(words[0]),
            FollowingWaypoints = new(words.Skip(1).Take(last)
                .Select(word => (ArmyMarchDestination?)ArmyMarchSourceAdapter.DecodeDestination(word)).ToArray())
        };
    }

    /// <summary>The five native words have one consumer, while ordinary and
    /// transport orders retain their separate saved owners.</summary>
    internal static GameState? ConsumeCurrent(GameState state, UnitState army)
    {
        if (army.NativeTacticRuntime?.Status10 != 2)
        {
            var march = Consume(state, army);
            return march is null ? null : state with { Units = new(state.Units) { [army.Id] = army with { March = march } } };
        }
        if (TransportRouteSourceAdapter.ReadWaypoints(state, army) is not { } words ||
            ArmySavedCommandKnowledge.ReadControl(state, army) is not byte control) return null;
        if (control != 0) Array.Fill(words, (ushort)ArmyMarchSourceAdapter.SourceHexCount);
        else ConsumeWords(words);
        var resources = state.ResourceState;
        if ((resources as CampaignSourceKnowledge)?.FindTransportRoute(army.Id) is { } residue)
            resources = TransportRouteSourceAdapter.Write(resources, army.Id, words, residue.Choice41);
        return state with { Units = new(state.Units) { [army.Id] = army with
            { TransportRoute = TransportRouteSourceAdapter.Decode(words) } }, ResourceState = resources };
    }

    private static void ConsumeWords(ushort[] words)
    {
        ushort previous;
        do
        {
            previous = words[0];
            for (int i = 0; i < 4; i++)
                words[i] = words[i + 1] == ArmyMarchSourceAdapter.SourceHexCount
                    ? words[i + 1] : Math.Min(words[i + 1], (ushort)(ArmyMarchSourceAdapter.SourceHexCount - 1));
            words[4] = ArmyMarchSourceAdapter.SourceHexCount;
        } while (previous != ArmyMarchSourceAdapter.SourceHexCount && previous == words[0]);
    }
}
