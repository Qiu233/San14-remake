using San14.Core;

namespace San14.Client.Gameplay;

public sealed partial class CampaignComposition
{
    private int _specialPersonResourceVersion=-1;

    private void RefreshApplicationSpecialPersonCapabilities()
    {
        if(_applicationGameplay.Context is not {} context)return;
        int version=_repository.PortraitResourceVersion;
        if(_specialPersonResourceVersion==version&&context.SpecialPersons is not null)return;
        var capabilities=_repository.LoadApplicationSpecialPersonCapabilities();
        _applicationGameplay.UpdateContext(context with{SpecialPersons=capabilities});
        _specialPersonResourceVersion=version;
    }
}
