using San14.Core;

namespace San14.Data;

public sealed partial class ForceUnregisterSource
{
    internal static (GameState State, int[] Forces) RankCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext primary)
    {
        var module = state.NativeForceUnregisterCurrent!;
        var rankings = new Dictionary<int, NativeForceRankingCurrent>(module.ForceRankings);
        var active = new List<int>();
        for (int force = 1; force <= 45; force++)
        {
            if (!Known(current.ReadForceNativeValidity(new(force)), "ranking force validity")) continue;
            active.Add(force);
            rankings[force] = rankings[force] with { Byte1C4 = 0 };
        }
        var distinctScores = active.Select(force => Need(rankings[force].Word1BC, "force ranking score"))
            .Distinct().OrderDescending().ToArray();
        var lowest = new List<int>();
        foreach (int force in active)
        {
            int value = Need(rankings[force].Word1BC, "force ranking score");
            int position = Array.IndexOf(distinctScores, (ushort)value) + 1;
            if (position == 1) rankings[force] = rankings[force] with { Byte1C4 = value == 0 ? (byte)3 : (byte)1 };
            else if (position == 2) rankings[force] = rankings[force] with { Byte1C4 = 2 };
            else if (distinctScores.Length >= 4 && position == distinctScores.Length) lowest.Add(force);
            else rankings[force] = rankings[force] with { Byte1C4 = 3 };
        }
        foreach (int force in lowest)
            rankings[force] = rankings[force] with { Byte1C4 = lowest.Count == 1 ? (byte)4 : (byte)3 };
        return (state with { NativeForceUnregisterCurrent = module with { ForceRankings = rankings } }, active.ToArray());
    }
}
