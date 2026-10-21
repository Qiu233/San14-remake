using System.Globalization;
using San14.Core;

namespace San14.Data;

/// <summary>Only a City district byte without an independent DistrictId.
/// Slot0 is the source's default City receiver, not a gameplay city.</summary>
public sealed record CityDistrictSourceResidue
{
    public int CityId { get; }
    public byte DistrictCode { get; }
    public CityDistrictSourceResidue(int cityId, byte districtCode)
    {
        if ((uint)cityId >= 52 || cityId != 0 && districtCode is >= 1 and <= 51)
            throw new InvalidDataException("City district residue has a mapped or invalid owner.");
        CityId = cityId;
        DistrictCode = districtCode;
    }
}

/// <summary>The single current City affiliation projection. No capture or
/// restore reads initial resource rows to replace missing current knowledge.</summary>
internal static class CityDistrictSourceAdapter
{
    private const string LegacyPrefix = "ce-army-detached-current:City:";
    private const string LegacyGrowthPrefix = "ce-growth-current:city:";

    internal static byte? Read(GameState state, CityState city)
    {
        if (city.DistrictId is int district)
            return district is >= 1 and <= 51 && state.Districts.ContainsKey(district) ? (byte)district
                : throw new InvalidDataException("Current City district identity is outside its source table.");
        return CampaignSourceKnowledge.From(state.ResourceState).FindCityDistrict(city.Id)?.DistrictCode;
    }

    internal static byte? ReadDefault(GameState state) =>
        CampaignSourceKnowledge.From(state.ResourceState).FindCityDistrict(0)?.DistrictCode;

    internal static GameState InstallFresh(GameState state, LoadedScenario scenario)
    {
        var knowledge = CampaignSourceKnowledge.From(state.ResourceState);
        if (knowledge.CityDistricts.Count != 0 || scenario.Snapshot.Cities.Count != 52)
            throw new InvalidDataException("Fresh City district source requires its unused complete scenario owner.");
        var rows = new List<CityDistrictSourceResidue>();
        foreach (var source in scenario.Snapshot.Cities)
        {
            if (source.Slot == 0)
            {
                rows.Add(new(0, source.DistrictSlot));
                continue;
            }
            if (!state.Cities.TryGetValue(source.Slot, out var city) ||
                city.DistrictId != (source.DistrictSlot is >= 1 and <= 51 ? (int?)source.DistrictSlot : null))
                throw new InvalidDataException("Fresh City district projection differs from the selected source.");
            if (source.DistrictSlot is not (>= 1 and <= 51)) rows.Add(new(source.Slot, source.DistrictSlot));
        }
        return state with { ResourceState = knowledge.ReplaceCityDistricts(rows) };
    }

    internal static GameState PublishCurrent(GameState state, SourceWorldContext current)
    {
        var cities = new Dictionary<int, CityState>(state.Cities);
        var rows = new List<CityDistrictSourceResidue>();
        Add(0, current.ReadBaseDistrictSlot(new(BaseKind.City, 0)));
        foreach (var city in state.Cities.Values)
        {
            int? raw = current.ReadBaseDistrictSlot(new(BaseKind.City, city.Id));
            Add(city.Id, raw);
            int? districtId = raw is >= 1 and <= 51 ? raw : null;
            int? forceId = null;
            if (raw is not null)
            {
                int force = current.ReadBaseForceSlot(new(BaseKind.City, city.Id))
                    ?? throw new InvalidDataException("Current City force affiliation is unknown.");
                if (force != 0 && !state.Forces.ContainsKey(force))
                    throw new InvalidDataException("Current City force has no gameplay Force identity.");
                forceId = force == 0 ? null : force;
            }
            if (districtId is int mapped)
            {
                if (!state.Districts.TryGetValue(mapped, out var district))
                    throw new InvalidDataException("Current City district has no gameplay District identity.");
                if (district.ForceId != forceId)
                    throw new InvalidDataException("Current City district and force affiliation differ.");
            }
            cities[city.Id] = city with { DistrictId = districtId, ForceId = forceId };
        }
        return state with { Cities = cities,
            ResourceState = CampaignSourceKnowledge.From(state.ResourceState).ReplaceCityDistricts(rows) };

        void Add(int id, int? raw)
        {
            if (raw is null) return;
            if (raw is < 0 or > byte.MaxValue) throw new InvalidDataException("Current City district byte is outside its format.");
            if (id == 0 || raw is not (>= 1 and <= 51)) rows.Add(new(id, (byte)raw.Value));
        }
    }

    internal static GameState PruneReplacedChannels(GameState state)
    {
        var knowledge = CampaignSourceKnowledge.From(state.ResourceState);
        var retained = knowledge.CityDistricts.Where(row => row.CityId == 0 ||
            !state.Cities.TryGetValue(row.CityId, out var city) || city.DistrictId is null).ToArray();
        return retained.Length == knowledge.CityDistricts.Count ? state
            : state with { ResourceState = knowledge.ReplaceCityDistricts(retained) };
    }

    internal static void Validate(GameState state)
    {
        foreach (var city in state.Cities.Values)
            if (city.DistrictId is int district &&
                (district is < 1 or > 51 || !state.Districts.ContainsKey(district)))
                throw new InvalidDataException("Current City has an invalid gameplay District identity.");
        foreach (var row in CampaignSourceKnowledge.From(state.ResourceState).CityDistricts)
            if (row.CityId != 0 && (!state.Cities.TryGetValue(row.CityId, out var city) || city.DistrictId is not null))
                throw new InvalidDataException("City district residue conflicts with its gameplay owner.");
        if (state.NativeArmySimulation?.Parameters is { } parameters &&
            parameters.Int32Values.Keys.Concat(parameters.Float32Values.Keys).Any(IsLegacyKey))
            throw new InvalidDataException("Current City districts cannot retain legacy parameter keys.");
    }

    internal static GameState MigrateLegacy(GameState state, int? decodedResourceVersion)
    {
        if (state.NativeArmySimulation is not { } simulation) return state;
        var parameters = simulation.Parameters;
        // Both historical fields were integer bytes. A float with either key
        // is a conflicting format owner, never a source for current knowledge.
        if (parameters.Float32Values.Keys.Any(IsLegacyKey))
            throw new InvalidDataException("Legacy City district parameter has a conflicting float owner.");
        var entries = parameters.Int32Values.Where(pair => IsLegacyCityKey(pair.Key)).ToArray();
        var growthEntries = parameters.Int32Values.Where(pair => IsLegacyGrowthCityKey(pair.Key)).ToArray();
        if (entries.Length == 0 && growthEntries.Length == 0) return state;
        if (decodedResourceVersion is >= 11)
            throw new InvalidDataException("Resource codec11 cannot contain legacy City district parameters.");
        if (entries.Length != 0 && (parameters.Int32Values.GetValueOrDefault(OwnedArmyLifecycleDetachedInputs.VersionKey) != 1 ||
                parameters.Float32Values.ContainsKey(OwnedArmyLifecycleDetachedInputs.VersionKey)) ||
            growthEntries.Length != 0 && (parameters.Int32Values.GetValueOrDefault(PersonGrowthCurrentQueries.VersionKey) != 1 ||
                parameters.Float32Values.ContainsKey(PersonGrowthCurrentQueries.VersionKey)))
            throw new InvalidDataException("Legacy City district parameters have no supported saved owner.");
        var detached = ReadEntries(entries, LegacyPrefix, "30");
        var growth = ReadEntries(growthEntries, LegacyGrowthPrefix, "26");
        var cities = new Dictionary<int, CityState>(state.Cities);
        var knowledge = CampaignSourceKnowledge.From(state.ResourceState);
        var rows = new List<CityDistrictSourceResidue>(knowledge.CityDistricts);
        foreach (int id in detached.Keys.Concat(growth.Keys).Distinct().Order())
        {
            if (rows.Any(row => row.CityId == id))
                throw new InvalidDataException("Legacy City district conflicts with source knowledge.");
            if (id == 0)
            {
                // The old growth fallback required an ordinary CityState.
                // City0 had no such owner; only its actual detached byte applies.
                if (detached.TryGetValue(0, out byte code)) rows.Add(new(0, code));
                continue;
            }
            if (!cities.TryGetValue(id, out var city) || city.DistrictId is < 1 or > 51)
                throw new InvalidDataException("Legacy City district has an invalid gameplay owner.");
            // Every old reader gave mapped gameplay identities priority.
            if (city.DistrictId is int mapped)
            {
                if (!state.Districts.ContainsKey(mapped))
                    throw new InvalidDataException("Legacy City district has no gameplay District identity.");
                continue;
            }
            // Detached City30 was the last published current byte. The initial
            // Growth26 fallback was reached only when current remained unknown.
            bool hasCurrent = detached.TryGetValue(id, out byte district);
            if (!hasCurrent)
            {
                district = growth[id];
                // Old fallback did not recover a departed mapped district.
                if (district is >= 1 and <= 51) continue;
            }
            if (district is >= 1 and <= 51)
            {
                if (!state.Districts.ContainsKey(district))
                    throw new InvalidDataException("Legacy City district has no gameplay District identity.");
                // A mapped District is owned by its saved Force. Publish both
                // projections together, as required by Core KnownDistrict.
                cities[id] = city with { DistrictId = district, ForceId = state.Districts[district].ForceId };
            }
            else rows.Add(new(id, district));
        }
        var values = new Dictionary<string, int>(parameters.Int32Values);
        foreach (var pair in entries.Concat(growthEntries)) values.Remove(pair.Key);
        return state with { Cities = cities, ResourceState = knowledge.ReplaceCityDistricts(rows),
            NativeArmySimulation = simulation with { Parameters = parameters with { Int32Values = values } } };

        static Dictionary<int, byte> ReadEntries(KeyValuePair<string, int>[] source, string prefix, string field)
        {
            var result = new Dictionary<int, byte>();
            foreach (var pair in source)
            {
                string suffix = pair.Key[prefix.Length..];
                int separator = suffix.IndexOf(':');
                if (separator <= 0 || suffix[(separator + 1)..] != field ||
                    !int.TryParse(suffix[..separator], NumberStyles.None, CultureInfo.InvariantCulture, out int id) ||
                    (uint)id >= 52 || pair.Key != prefix + id.ToString(CultureInfo.InvariantCulture) + ":" + field ||
                    pair.Value is < 0 or > byte.MaxValue || !result.TryAdd(id, (byte)pair.Value))
                    throw new InvalidDataException("Legacy City district parameter is malformed or duplicated.");
            }
            return result;
        }
    }

    private static bool IsLegacyCityKey(string key) => key.StartsWith(LegacyPrefix, StringComparison.Ordinal);
    private static bool IsLegacyGrowthCityKey(string key) =>
        key.StartsWith(LegacyGrowthPrefix, StringComparison.Ordinal) && key.EndsWith(":26", StringComparison.Ordinal);
    private static bool IsLegacyKey(string key) => IsLegacyCityKey(key) || IsLegacyGrowthCityKey(key);
}
