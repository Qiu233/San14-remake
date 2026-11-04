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

        // Ordinary announcements and person messages share this same font
        // generation. Only fixed art is prepared; current text stays dynamic.
        var announcement=assets.SourceLayout(0xF6332D26);
        assets.PrepareLayoutArt(announcement.Source,[1,3],commonImports:true);
        _=announcement.Type6(2);
        var person=assets.SourceLayout(0x3B311A21);
        assets.PrepareLayoutArt(person.Source,[1,3,4,5,11],commonImports:true);
        _=person.Type6(2);
        var commonCursor=assets.SourceLayout(0xC09B0A33);
        assets.PrepareLayoutArt(commonCursor.Source,[2],commonImports:true);

        // Collection already prepares the real scenery pictures and summary
        // sources prepare the three bonus icons. Do not load the panel's stub.
        var discovery=assets.SourceLayout(0x04C84E57);
        assets.PrepareLayoutArt(discovery.Source,[1,4,5,6],commonImports:true);
        // Actual caravan item/book and foreign-weapon bodies share the
        // report's startup chrome. Node2 is a current image override.
        foreach(uint file in new uint[]{0xCC79B335,0x5F9F176F})
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reward=assets.SourceLayout(file);
            assets.PrepareLayoutArt(reward.Source,reward.Nodes.Where(node=>
                (node.Type is 1 or 3)&&node.Index!=2).Select(node=>node.Index),commonImports:true);
        }
        cancellationToken.ThrowIfCancellationRequested();
    }
}
