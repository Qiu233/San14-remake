using San14.Data;

namespace San14.Client.Gameplay;

public sealed partial class CampaignComposition
{
    private ApplicationPresentationOptions? _applicationPresentationOptions;

    internal void ConfigureApplicationPresentationOptions(ApplicationPresentationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Volatile.Write(ref _applicationPresentationOptions, options);
        _army?.ConfigureApplicationPresentationOptions(options);
    }
}
