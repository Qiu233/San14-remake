using San14.Core;

namespace San14.Data;

/// <summary>Independent BaseA8 enemy and BaseB0 route-area writes. A missing
/// entry means the source did not visit that receiver; null means an actual clear.</summary>
public sealed record OriginalAiCrisisEntry(int LocationId, int? EnemyLocationId, int? TargetAreaId);

internal static class OriginalAiCrisisSource
{
    internal const string StrengthDivisorKey = "original-ai.weighted-strength-divisor";

    //021E20(mode0)/096A50: stable descending full leadership+strength.
    //Read scores only at actual comparator calls, including the one-row case.
    internal static void SortPeople(List<int> people, Func<int, int> score)
    {
        for (int i = 1; i < people.Count; i++)
        {
            int person = people[i], position = i;
            while (position > 0 && score(person) > score(people[position - 1])) position--;
            if (position == i) continue;
            people.RemoveAt(i); people.Insert(position, person);
        }
    }

    internal static int SelectEnemy(int ownStrength, IReadOnlyList<int> neighbors,
        Func<int, bool> isEnemy, Func<int, int> strength)
    {
        int threshold = ownStrength / 2, selected = -1;
        foreach (int at in neighbors)
        {
            if (!isEnemy(at)) continue;
            int value = strength(at);
            if (threshold >= value) continue;
            threshold = value; selected = at;
        }
        return selected;
    }
}

internal static partial class ArmyRouteSearch
{
    //0CE460: zeroed descriptor (null Army, Force0), flags1, pool2,
    //one leg from the enemy Base hex toward the original Base hex.
    internal static int[] OriginalAiCrisisPath(GameState state, SourceWorldContext current,
        int enemyHex, int ownHex)
    {
        if ((uint)enemyHex >= Count || (uint)ownHex >= Count) return [];
        var fields = state.NativeBaseCaptureCurrent
            ?? throw new InvalidDataException("original_ai.crisis.route_Hex16_owner");
        ushort? ReadHex16(int physical) => (uint)physical < (uint)fields.HexWords16.Length
            ? fields.HexWords16[physical]
            : throw new InvalidDataException("original_ai.crisis.route_Hex16_index");
        var query = new Queries(state, current, new CampaignMovement(0), enemyHex, ownHex,
            0, 0, null, 1, hasArmyDescriptor: false, readHexWord16: ReadHex16);
        try { return Search(query, out _, out _, hasArmyDescriptor: false); }
        catch (MissingOwner error) { throw new InvalidDataException(error.Message, error); }
    }
}
