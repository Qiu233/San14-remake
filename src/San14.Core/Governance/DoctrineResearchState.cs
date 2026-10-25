using System.Collections.Immutable;

namespace San14.Core;

/// <summary>The current research frontier. Learned and suppressed panels
/// remain owned by ForcePolicyEffects; a command draft owns its operation trace.</summary>
public sealed record DoctrineResearchState(ImmutableHashSet<int> AvailablePanelIds);

public sealed partial record ForceState
{
    public DoctrineResearchState? DoctrineResearch { get; init; }
}
