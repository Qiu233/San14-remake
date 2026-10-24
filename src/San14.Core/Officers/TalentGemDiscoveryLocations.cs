namespace San14.Core;

/// <summary>An ordered discovery location and its current remaining charges.</summary>
public sealed record GemDiscoverySite(ushort Area, byte Remaining);

/// <summary>Mutable discovery sites are independent of fixed Gem definitions
/// and a force's acquired tactic books. Null GameState knowledge stays unknown.</summary>
public sealed record TalentGemDiscoveryState(Dictionary<int, GemDiscoverySite[]> Gems);
