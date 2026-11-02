internal sealed partial class MapSystemMenuPreparedResources
{
    // Actual EventView message/cursor art and character-parent geometry. The
    // speaker/body are current text and the character images are shared bank9/12.
    private static void PrepareNarrationSources(UiAssets assets,CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _=assets.SourceLayout(0xD8B39FC6);
        _=assets.SourceLayout(0x86BA499F);
        foreach(uint file in new uint[]{0x98D39EC1,0x0D12D720})
        {
            var source=assets.SourceLayout(file);
            assets.PrepareLayoutArt(source.Source,[2],commonImports:true);
        }
    }
}
