using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private Action<GameState>? _periodPresentation;

    /// <summary>Borrow the private candidate at completed native clock boundaries.
    /// The callback must finish capturing its display data before returning; it
    /// must not retain the mutable candidate or issue simulation commands.</summary>
    public void ConfigurePeriodPresentation(Action<GameState>? presentation)
        => _periodPresentation = presentation;
}
