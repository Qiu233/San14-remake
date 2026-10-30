using System.Collections.Immutable;

namespace San14.Data.AI.Original;

internal sealed record OriginalAiDeepInteriorSnapshot(int ForceId, ImmutableArray<OriginalAiEntity> Footholds);

/// <summary>The third classification written during strategy preparation.
/// Its first- and second-neighbor ownership tests are separate source passes.</summary>
internal static class OriginalAiDeepInteriorFootholds
{
    internal static ImmutableArray<OriginalAiDeepInteriorSnapshot> Rebuild(
        IOriginalAiForceMemoryInputs inputs, Func<int, IReadOnlySet<OriginalAiEntity>> readFrontier)
    {
        var snapshots = ImmutableArray.CreateBuilder<OriginalAiDeepInteriorSnapshot>(inputs.RegisteredForces.Count);
        foreach (int force in inputs.RegisteredForces)
        {
            var owned = inputs.OwnedFootholds(force);
            var result = ImmutableArray.CreateBuilder<OriginalAiEntity>();
            // The source first clears the third list, then counts the actual
            // filtered iterator. At two or fewer it never queries the frontier.
            if (owned.Count > 2)
            {
                var frontier = readFrontier(force);
                var seen = new HashSet<OriginalAiEntity>();
                foreach (var foothold in owned)
                {
                    if (frontier.Contains(foothold)) continue;
                    var neighbors = inputs.AdjacentFootholds(foothold);
                    bool accepted = true;
                    // Do not query a neighbor's adjacency before every first
                    // neighbor has passed; an earlier foreign owner short-circuits.
                    foreach (var neighbor in neighbors)
                        if (inputs.Owner(neighbor) != force) { accepted = false; break; }
                    if (!accepted) continue;
                    foreach (var neighbor in neighbors)
                    {
                        foreach (var second in inputs.AdjacentFootholds(neighbor))
                            if (inputs.Owner(second) != force) { accepted = false; break; }
                        if (!accepted) break;
                    }
                    // Vacuously closed adjacency is a genuine source result;
                    // no invented minimum neighbor count or map-radius test.
                    if (accepted && seen.Add(foothold)) result.Add(foothold);
                }
            }
            snapshots.Add(new(force, result.ToImmutable()));
        }
        return snapshots.MoveToImmutable();
    }
}
