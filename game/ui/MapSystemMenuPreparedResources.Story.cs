using System.Globalization;
using Godot;
using San14.Data;

internal sealed partial class MapSystemMenuPreparedResources
{
    // Static event artwork is qualified by the same original descriptors as
    // the editor. Actors, messages, waits, logs and report advice stay local.
    private static void PrepareStorySources(GameResourceRepository repository, UiAssets assets,
        MapFontSource font, LoadedMessages messages, string locale,
        Dictionary<MapFont.PreparedTextKey, MapTextCache> caches, CancellationToken cancellationToken)
    {
        _ = assets.SourceLayout(0xE6F9D1D6);
        _ = assets.SourceLayout(0xF6352F00);
        _ = assets.SourceLayout(0xC6CCCD9C);
        foreach (string name in new[] { "000_cmn_icon_advance_spd2", "000_cmn_icon_advance_skip", "000_cmn_icon_log" })
            assets.PrepareCommonSprite(name);
        Art(assets.SourceLayout(0x909CAB01), [1]);
        Art(assets.SourceLayout(0x0834A553), [1]);

        foreach (var family in EditedEventContentCatalog.StillFamilies)
            for (int id = family.First; id <= family.Last; id++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string name = family.SpriteStem + id.ToString("D3", CultureInfo.InvariantCulture);
                string pack = "texture_" + name;
                if (!assets.DeclaredSprites.HasTexturePackDeclaration(pack))
                    throw new InvalidDataException("Story image descriptor has no installed texture declaration: " + pack);
                assets.PrepareCommonSprite(name, pack);
            }
        // The story item mutator hides only on null. Image0 is an actual
        // requested picture, separate from the collection's empty item slot.
        var itemZero = CollectionImageRoute.ForItem(0);
        assets.PrepareCommonSprite(itemZero.SpriteName, itemZero.TexturePack);

        var editor = assets.SourceLayout(0x24C5A7C7);
        Art(editor, [3, 6, 23, 25, 28, 30]);
        _ = editor.Type6(4); _ = editor.Type6(5);
        var presets = MapTextPresets.Create();
        var header = assets.SourceLayout(0xDC8AC8AA);
        Text(header, 2, Caption(0x91), 56);
        int[] captions = [0x300, 0x2FD, 0x2EA, 0x303, 0x304, 0x2FE];
        for (int field = 0; field < captions.Length; field++)
        {
            var button = assets.CommandButtonLayout(editor.Rectangle(7 + field * 2).Size);
            Art(button, [3, 5, 6, 8, 9, 11, 13, 14]); _ = assets.ButtonStateProfile(button.Source);
            for (int state = 0; state < 5; state++) Text(button, 16, Caption(captions[field]), 28 + state);
        }
        Text(editor, 24, Caption(0x303)); Text(editor, 29, Caption(0x304));
        var space = assets.SourceLayout(0x8D5C4E58);
        Art(space, [1]); Text(space, 2, Caption(0x2FB), 13);
        Art(assets.SourceLayout(0xAFC125D8), [1, 2]);

        string Caption(int id) => messages.Messages.Sections[1].Records[id].Text
            ?? throw new InvalidDataException("Missing story/editor source caption.");
        void Art(UiSourceLayout source, IEnumerable<int> nodes)
        { cancellationToken.ThrowIfCancellationRequested(); assets.PrepareLayoutArt(source.Source, nodes, commonImports: true); }
        void Text(UiSourceLayout source, int node, string value, int? preset = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var profile = MapFontProfile.FromNode(source.Nodes[node], source.Source.Source.ResourceUri);
            MapFont.PrepareText(font, value, preset is int index ? profile.WithPreset(presets[index]) : profile,
                source.Rectangle(node).Size.X, caches);
        }
    }
}
