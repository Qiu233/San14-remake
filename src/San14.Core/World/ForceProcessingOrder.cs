namespace San14.Core;

/// <summary>The order prepared at the campaign's force-order boundary.
/// Later consumers use this saved order even if holdings change meanwhile.</summary>
public sealed record ForceProcessingOrder
{
    public IReadOnlyList<int> ForceIds { get; }
    public ForceProcessingOrder(IReadOnlyList<int> forceIds)
    {
        ArgumentNullException.ThrowIfNull(forceIds);
        ForceIds = Array.AsReadOnly(forceIds.ToArray());
    }
}

public sealed partial record GameState
{
    public ForceProcessingOrder? ForceProcessingOrder { get; init; }
}
