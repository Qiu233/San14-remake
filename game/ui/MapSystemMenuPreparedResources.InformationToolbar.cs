using San14.Data;

internal sealed partial class MapSystemMenuPreparedResources
{
    private static void PrepareInformationToolbarSources(UiAssets assets,CancellationToken cancellationToken)
    {
        var popup=assets.SourceLayout("00_02_00_mein_offset_61");
        assets.PrepareLayoutArt(popup.Source,[1]);
        var button=assets.SourceLayout("cmn_btn_fnc_check_sub");
        assets.PrepareLayoutArt(button.Source,UiSourceInformationButton.Artwork);
        foreach(var icons in UiSourceInformationButton.Icons)
            foreach(string icon in icons)
            {cancellationToken.ThrowIfCancellationRequested();assets.PrepareCommonSprite(icon);}
    }
}
