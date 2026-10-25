using San14.Core;

namespace San14.Data;

public sealed record ForceProcessingOrderResult(GameState? State, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Campaign4290D0 prepares28B290's force order at its source
    /// boundary. The shared writer publishes World1630 and the processing view.</summary>
    public ForceProcessingOrderResult TryPrepareForceProcessingOrder(GameState state)
    {
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            return new(PrepareCoalitionPreferenceCurrent(state, current), null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }
}
