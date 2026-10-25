using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private Func<GameState, GameState>? _periodPresentation;

    /// <summary>Borrow the private candidate at completed native clock boundaries.
    /// The callback owns its stopped worker's candidate until it returns. Only
    /// actual ordinary-presentation manager/App changes return to the worker;
    /// it must not retain the candidate or issue simulation commands.</summary>
    public void ConfigurePeriodPresentation(Func<GameState, GameState>? presentation)
        => _periodPresentation = presentation;
}
