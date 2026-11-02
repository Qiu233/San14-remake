using Godot;
using San14.Data;
using San14.Data.Formats;

internal sealed partial class MapSystemMenuPreparedResources
{
    private static void PreparePopularitySources(GameResourceRepository repository, UiAssets assets, MapFontSource font,
        LoadedMessages messages, string locale, Dictionary<MapFont.PreparedTextKey, MapTextCache> caches,
        CancellationToken cancellation)
    {
        var page = assets.SourceLayout(0xC06CEC9C);
        assets.PrepareLayoutArt(page.Source,
            [2, 5, 6, 10, 15, 26, 27, 31, 32, 36, 52, 53, 57, 58, 62, 78, 79, 83, 84, 100, 102],
            commonImports: true);
        _ = page.Type6(88);
        var row = assets.SourceLayout("09_01_01_event_list_offset_02");
        var presets = MapTextPresets.Create();
        var records = messages.Messages.Sections[1].Records;
        string Caption(int id) => records[id].Text
            ?? throw new InvalidDataException("Missing popularity caption: " + id);
        void Text(UiSourceLayout layout, int node, int message, int preset, float? width = null)
        {
            cancellation.ThrowIfCancellationRequested();
            MapFont.PrepareText(font, Caption(message),
                MapFontProfile.FromNode(layout.Nodes[node], layout.Source.Source.ResourceUri).WithPreset(presets[preset]),
                width ?? layout.Rectangle(node).Size.X, caches);
        }
        Text(assets.SourceLayout(0xDC8AC8AA), 2, 0x83F, 56);
        Text(page, 7, 0x8AC, 13);
        for (int group = 0; group < 3; group++)
        {
            Text(page, 28 + 26 * group, 0x8AE, 13);
            Text(page, 33 + 26 * group, 0x8AF, 13);
        }
        uint[] tabFiles = [0x172EDC24, 0x9AFB27B7, 0xD45873AA];
        for (int index = 0; index < tabFiles.Length; index++)
        {
            var tab = assets.SourceLayout(tabFiles[index]);
            assets.PrepareLayoutArt(tab.Source, [3, 5, 8, 11, 13], commonImports: true);
            float width = tab.Rectangle(16).Size.X + page.Rectangle(89 + index * 2).Size.X - tab.Nodes[0].Value44;
            for (int state = 0; state < 4; state++) Text(tab, 16, 0xCD + index, 8 + state, width);
        }
        // These explanations have no campaign arguments. Prepare plain fixed
        // lines in the same startup font cache; colour runs keep their normal
        // live-slot rendering path rather than discarding source controls.
        var explanations = new InformationDestinyExplanations(repository.LoadMessages(locale, MessageKind.Game));
        var profile = MapFontProfile.FromNode(row.Nodes[22], row.Source.Source.ResourceUri).WithPreset(presets[0]);
        for (int message = 0x34CD; message <= 0x34EB; message++)
        {
            cancellation.ThrowIfCancellationRequested();
            string expanded = explanations.Read(message)
                .Replace("\r\n", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal);
            DeploymentNarrationPlainParagraph.RequireKnownColours(expanded);
            var line = DeploymentNarrationParagraphPlan.Runs(expanded).Single();
            if (line.Runs.All(run => run.Colour == NarrationBodyColour.OriginalNodeBody))
                MapFont.PrepareText(font, line.VisibleText, profile, row.Rectangle(22).Size.X, caches);
        }
    }
}
