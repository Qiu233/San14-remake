using System.Collections.Immutable;

namespace San14.Data.AI.Original;

/// <summary>The two ordered, strategy-entry classifications copied by the
/// original Force-information nodes. This is a detached value, not a native
/// pointer-list image or a claim that an old save has known empty lists.</summary>
public sealed record OriginalAiForceMemorySnapshot(int ForceId,
    ImmutableArray<OriginalAiEntity> FrontierFootholds,
    ImmutableArray<OriginalAiEntity> RearFootholds);

internal interface IOriginalAiForceMemoryInputs
{
    IReadOnlyList<int> RegisteredForces { get; }
    IReadOnlyList<OriginalAiEntity> OwnedFootholds(int force);
    IReadOnlyList<OriginalAiEntity> AdjacentFootholds(OriginalAiEntity foothold);
    int Owner(OriginalAiEntity foothold);
    bool IsCurrentPlayer(int force);
    bool HasCoalitionFlag(int force);
    byte AllianceByte(int force, int other);
    int FriendshipLevel(int force, int other);
    int RulerAllegianceDisposition(int force);
}

/// <summary>Normal strategy-entry frontier/rear classification. The caller
/// owns publication and stage timing. Information queries only copy the
/// previously published values and must not call this producer.</summary>
internal static class OriginalAiForceMemoryProducer
{
    internal static ImmutableArray<OriginalAiForceMemorySnapshot> Rebuild(IOriginalAiForceMemoryInputs inputs)
    {
        var result = ImmutableArray.CreateBuilder<OriginalAiForceMemorySnapshot>(inputs.RegisteredForces.Count);
        foreach (int force in inputs.RegisteredForces)
        {
            var frontier = ImmutableArray.CreateBuilder<OriginalAiEntity>();
            var rear = ImmutableArray.CreateBuilder<OriginalAiEntity>();
            var frontierSeen = new HashSet<OriginalAiEntity>();
            var rearSeen = new HashSet<OriginalAiEntity>();
            foreach (var foothold in inputs.OwnedFootholds(force))
            {
                bool atFrontier = false;
                foreach (var neighbor in inputs.AdjacentFootholds(foothold))
                {
                    if (!NeedsFrontier(force, inputs.Owner(neighbor), inputs)) continue;
                    atFrontier = true;
                    break;
                }
                if (atFrontier)
                {
                    if (frontierSeen.Add(foothold)) frontier.Add(foothold);
                }
                else if (rearSeen.Add(foothold)) rear.Add(foothold);
            }
            result.Add(new(force, frontier.ToImmutable(), rear.ToImmutable()));
        }
        return result.MoveToImmutable();
    }

    private static bool NeedsFrontier(int own, int other, IOriginalAiForceMemoryInputs inputs)
    {
        if (other == own) return false;
        if (other == 0) return true;
        if (inputs.IsCurrentPlayer(own))
        {
            if (inputs.HasCoalitionFlag(own) && inputs.HasCoalitionFlag(other)) return false;
            if ((inputs.AllianceByte(own, other) & 127) != 0) return false;
            return other is < 46 or > 50;
        }
        if ((inputs.AllianceByte(own, other) & 127) != 0) return false;
        if (other is >= 46 and <= 50) return false;
        if (inputs.HasCoalitionFlag(own) && inputs.HasCoalitionFlag(other)) return false;
        int level = inputs.FriendshipLevel(own, other);
        return inputs.RulerAllegianceDisposition(own) <= 10 ? level != 5 : level is < 4 or > 5;
    }
}
