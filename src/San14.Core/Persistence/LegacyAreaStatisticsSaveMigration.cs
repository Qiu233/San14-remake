using System.Text.Json;
using System.Text.Json.Nodes;

namespace San14.Core;

/// <summary>Converts the old field-key representation at the save boundary, never during a query.</summary>
internal static class LegacyAreaStatisticsSaveMigration
{
    private const string Prefix = "ce-area-statistics-current:";
    private const int LegacyAreaCount = 501;
    private const int LegacyCityCount = 52;

    public static void Apply(JsonObject state)
    {
        if (state["NativeArmySimulation"]?["Parameters"]?["Int32Values"] is not JsonObject values ||
            !values.ContainsKey(Prefix + "profile-version")) return;
        int version = Read(Prefix + "profile-version");
        if (version is not (1 or 2)) throw new InvalidDataException("Unsupported legacy area statistics version.");
        var entries = new Dictionary<int, AreaMapStatisticsEntry>();
        for (int areaId = 0; areaId < LegacyAreaCount; areaId++)
        {
            int city = ReadField(areaId, "34"), center = ReadField(areaId, "3A");
            int mask = ReadField(areaId, "66"), owned = ReadField(areaId, "68");
            int terrain = ReadField(areaId, "76"), force = ReadField(areaId, "78");
            if (city is < 0 or >= LegacyCityCount || center < 0 || mask is < 0 or > byte.MaxValue ||
                owned is < 0 or > ushort.MaxValue || terrain is < 0 or > ushort.MaxValue ||
                force is < 0 or >= LegacyCityCount)
                throw new InvalidDataException($"Invalid legacy area statistics for area {areaId}.");
            int counterVersion = values[$"{Prefix}area:{areaId}:owned-count-version"]?.GetValue<int>() ?? 0;
            entries.Add(areaId, new(areaId, city == 0 ? null : city, center, (byte)mask, owned,
                terrain, force == 0 ? null : force, force != 0 || counterVersion is 1 or 2));
        }
        int count = Read(Prefix + "registered-area-count");
        if (count is < 0 or > LegacyAreaCount) throw new InvalidDataException("Invalid legacy area update order.");
        var updateOrder = new int[count];
        for (int ordinal = 0; ordinal < count; ordinal++)
        {
            int areaId = Read($"{Prefix}registered-area:{ordinal}");
            if (!entries.ContainsKey(areaId)) throw new InvalidDataException("Legacy area update order references a missing area.");
            updateOrder[ordinal] = areaId;
        }
        state["RegionStatistics"] = JsonSerializer.SerializeToNode(new AreaMapStatisticsState(entries, updateOrder));
        foreach (string key in values.Select(entry => entry.Key).Where(key => key.StartsWith(Prefix, StringComparison.Ordinal)).ToArray())
            values.Remove(key);

        int Read(string key) => values[key]?.GetValue<int>()
            ?? throw new InvalidDataException($"Missing legacy area statistics field: {key}.");
        int ReadField(int areaId, string field) => Read($"{Prefix}area:{areaId}:field:{field}");
    }
}
