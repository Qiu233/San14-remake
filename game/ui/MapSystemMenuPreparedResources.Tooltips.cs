using San14.Data;

internal sealed partial class MapSystemMenuPreparedResources
{
    private static void PrepareTopButtonHintTexts(GameResourceRepository repository, UiAssets assets,
        MapFontSource font, string locale, Dictionary<MapFont.PreparedTextKey, MapTextCache> caches,
        CancellationToken cancellationToken)
    {
        var source = assets.SourceLayout(0xBE0DC488);
        var messages = new AdministrationMessages(repository, locale);
        foreach (int message in MapTopButtonHints.MessageIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            UiSourceSimpleTooltip.PreparePlainText(source, font, messages.Format(message), caches);
        }
    }
}
