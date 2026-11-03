using San14.Data;

internal sealed partial class MapSystemMenuPreparedResources
{
    private static void PrepareCommandRailHintTexts(GameResourceRepository repository,UiAssets assets,
        MapFontSource font,string locale,Dictionary<MapFont.PreparedTextKey,MapTextCache> caches,
        CancellationToken cancellationToken)
    {
        var source=assets.SourceLayout(0xBE0DC488);
        var messages=new AdministrationMessages(repository,locale);
        foreach (int id in MapCommandRail.HintMessageIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string text = id == 0x5991
                ? MainCommandHelpText.ReadRankRequirement(repository.LoadMessages(locale,San14.Data.Formats.MessageKind.Game))
                : messages.Format(id);
            if (text.Contains('^')) UiSourceSimpleTooltip.PrepareKnownColourText(source,font,text,caches);
            else UiSourceSimpleTooltip.PreparePlainText(source,font,text,caches);
        }
    }
}
