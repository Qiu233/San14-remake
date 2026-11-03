using Godot;
using San14.Data;

internal sealed partial class MapSystemMenuPreparedResources
{
    private static void PrepareCommanderTacticBalloonSources(GameResourceRepository repository, UiAssets assets,
        MapFontSource font, string locale, Dictionary<MapFont.PreparedTextKey, MapTextCache> caches,
        CancellationToken cancellationToken)
    {
        var layouts = CommanderTacticBalloonView.LayoutNames.Select(assets.SourceLayout).ToArray();
        foreach (var layout in layouts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            assets.PrepareLayoutArt(layout.Source, layout.Nodes.Where(node => node.Type is 1 or 3)
                .Select(node => node.Index), commonImports: true);
            _ = layout.Type6(1);
        }
        var descriptions = CommanderTacticBalloonDescriptions.Read(repository, locale);
        var presets = MapTextPresets.Create();
        // Physical effects compact into the two source rows. Every actual
        // static description is ready in both templates before entering a map.
        foreach (var description in descriptions.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PrepareEffect(layouts[0], 4, description);
            PrepareEffect(layouts[1], 4, description);
            PrepareEffect(layouts[1], 6, description);
        }
        var messages = new AdministrationMessages(repository, locale);
        foreach (int message in CommanderTacticBalloonView.ReasonMessages)
        {
            string text = messages.Format(message);
            var layout = layouts[text.Length * 2 > 16 ? 3 : 2];
            var rectangle = layout.Rectangle(4);
            var profile = CommanderTacticBalloonView.WarningProfile(layout);
            if (profile.WidthFitMode == 3)
                UiSourceParagraph.PrepareLiteralText(font, text, profile, rectangle.Size, caches);
            else MapFont.PrepareText(font, text, profile, rectangle.Size.X, caches);
        }

        void PrepareEffect(UiSourceLayout layout, int node, CommanderTacticBalloonDescription description)
        {
            var profile = MapFontProfile.FromNode(layout.Nodes[node], layout.Source.Source.ResourceUri)
                .WithPreset(presets[description.TextPreset]);
            MapFont.PrepareText(font, description.Text, profile, layout.Rectangle(node).Size.X, caches);
        }
    }
}
