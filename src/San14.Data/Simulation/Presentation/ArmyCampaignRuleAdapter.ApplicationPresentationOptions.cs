using System.Text.Json.Serialization;

namespace San14.Data;

/// <summary>Application-owned presentation choices. They do not change duel
/// selection, numerical effects or the campaign's scenario settings.</summary>
public sealed record ApplicationPresentationOptions
{
    [JsonRequired] public bool TacticDirectionsEnabled { get; init; } = true;
    [JsonRequired] public bool MoviesEnabled { get; init; } = true;
    // Keep the saved preference compatible while permanently skipping duel scenes.
    [JsonRequired] public bool DuelsEnabled { get => false; init { } }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    private ApplicationPresentationOptions? _applicationPresentationOptions;

    public void ConfigureApplicationPresentationOptions(ApplicationPresentationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Volatile.Write(ref _applicationPresentationOptions, options);
    }

    private int? ReadDuelPresentationSettingCurrent() => 0;
}
