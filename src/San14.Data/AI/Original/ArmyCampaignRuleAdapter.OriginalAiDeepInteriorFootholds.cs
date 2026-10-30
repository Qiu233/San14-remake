using System.Collections.Immutable;
using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The third part of0CEAD0, reached after the frontier/rear pass at
    /// actual strategy entry. Commands and information queries do not refresh it.</summary>
    public GameState RefreshOriginalAiDeepInteriorFootholdsCurrent(GameState state)
    {
        if (Seed.Language != "TC") throw DeepInteriorMissing("SC_strategy_preparation_contract");
        var memory = state.OriginalAiStrategyMemory ?? throw DeepInteriorMissing("owner");
        var current = CaptureCurrentCampaignEventQueries(state);
        // Reuse the existing ordered registration, Base58 links and current
        // ownership source, including its real validity and fallback semantics.
        var inputs = new ForceMemoryInputs(this, state, current);
        var rows = OriginalAiDeepInteriorFootholds.Rebuild(inputs, ReadFrontier);
        var forces = memory.Forces.ToBuilder();
        foreach (var row in rows)
        {
            if (row.ForceId == 0) continue; // The domain excludes the native sentinel.
            if (!forces.TryGetValue(row.ForceId, out var before)) throw DeepInteriorMissing("Force_owner." + row.ForceId);
            forces[row.ForceId] = before with { DeepInteriorLocationIds = row.Footholds.Select(value => value.Id).ToImmutableArray() };
        }
        return state with { OriginalAiStrategyMemory = memory with { Forces = forces.ToImmutable() } };

        IReadOnlySet<OriginalAiEntity> ReadFrontier(int force)
        {
            var saved = memory.Forces.GetValueOrDefault(force) ?? throw DeepInteriorMissing("frontier_owner." + force);
            if (saved.FrontierLocationIds.IsDefault) throw DeepInteriorMissing("frontier_list." + force);
            return saved.FrontierLocationIds.Select(DeepInteriorEntity).ToHashSet();
        }
    }

    /// <summary>ForceInfo33 copies the phase-owned third list. Its members keep
    /// their identities even if a later command changes current ownership.</summary>
    internal IReadOnlyList<OriginalAiEntity> ReadOriginalAiDeepInteriorFootholdsCurrent(GameState state, int force)
    {
        var saved = state.OriginalAiStrategyMemory?.Forces.GetValueOrDefault(force)
            ?? throw DeepInteriorMissing("saved_Force_owner." + force);
        if (saved.DeepInteriorLocationIds is not { } locations || locations.IsDefault)
            throw DeepInteriorMissing("saved_list." + force);
        return locations.Select(DeepInteriorEntity).ToArray();
    }

    private static OriginalAiEntity DeepInteriorEntity(int location) => location switch
    {
        >= 1 and <= 51 => new(WorldEntityKind.City, location),
        >= 52 and <= 61 => new(WorldEntityKind.Gate, location),
        _ => throw DeepInteriorMissing("location_identity." + location)
    };
    private static InvalidDataException DeepInteriorMissing(string field) => new("original_ai.deep_interior." + field);
}
