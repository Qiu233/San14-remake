using System.Collections.Immutable;

namespace San14.Core;

/// <summary>Pending temporary territory restoration, ordered by physical Hex.
/// An absent owner is unknown; only fresh construction creates the empty tree.</summary>
public sealed record CampaignTemporaryHexRestoreEntry(int HexPhysicalSlot, int ForcePhysicalSlot);

public sealed partial record GameState
{
    public ImmutableArray<CampaignTemporaryHexRestoreEntry>? CampaignTemporaryHexRestoreEntries { get; init; }
}
