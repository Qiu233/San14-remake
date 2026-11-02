using System.Globalization;
using San14.Client.Gameplay;
using San14.Data;
using San14.Data.Formats;

internal sealed partial class MapSystemMenuPreparedResources
{
    // The ordinary directory has a finite installed static closure. Profile
    // unlocks, completion history and current selection remain page inputs;
    // they do not own these textures or justify a later cold resource load.
    private static void PrepareScenarioSelectionSources(GameResourceRepository repository,
        UiAssets assets, MapFontSource font, LoadedMessages messages, string locale,
        Dictionary<MapFont.PreparedTextKey, MapTextCache> caches, CancellationToken cancellationToken)
    {
        PrepareNewGameForceSources(repository, assets, font, messages, locale, caches, cancellationToken);
        _ = assets.SourceLayout(0xEE041FF9);
        Art(0x53910979, [2, 3]);
        var description = Art(0x09C88359, [2]);
        _ = description.Type6(1);
        Art(0xCC2D6FFD, [1, 2]);
        var row = Art(0xF2C9CA9A, [3, 5, 8, 11, 13]);
        Art(0x9668AE5A, [1]);
        Art(0xE6446CC9, [3, 5, 8, 11, 13]);
        Art(0x3E2AD14F, [3, 5, 8, 11, 13]);
        Art(0x2F276C90, [3, 5, 8, 12, 14]);
        _ = assets.SourceLayout(0x95761708);
        assets.PrepareCommonSprite("base_pad", "texture_pad");
        assets.PrepareCommonSprite("pad_cross", "texture_pad");
        string language = locale == "TC" ? "tw" : "ch";
        foreach (string name in new[] { "020_sho_mrk_scenario_1", "020_sho_mrk_scenario_2", "020_sho_seiha" })
            assets.PrepareCommonSprite(language + "_" + name, "texture_lang_" + language);

        var records = messages.Messages.Sections[1].Records;
        var header = assets.SourceLayout(0xDC8AC8AA);
        var presets = MapTextPresets.Create();
        MapFont.PrepareText(font, records[0x578].Text
            ?? throw new InvalidDataException("Missing scenario selection title."),
            MapFontProfile.FromNode(header.Nodes[2], header.Source.Source.ResourceUri).WithPreset(presets[56]),
            header.Rectangle(2).Size.X, caches);
        string yearSuffix = records[0x2AB].Text ?? throw new InvalidDataException("Scenario year suffix is missing.");
        string monthSuffix = records[0x2AC].Text ?? throw new InvalidDataException("Scenario month suffix is missing.");
        var selections = repository.EffectiveScenarios.Where(selection => selection.Locale == locale)
            .ToDictionary(selection => selection.SourcePath, StringComparer.OrdinalIgnoreCase);
        var names = ArmyMapUiSources.Create();
        var portrait = assets.SourceLayout(0xB1FE7E6D);
        var parts = LcskSpriteNodeReader.Read(portrait.Source.Layout, portrait.Nodes[1]).Groups.Single();
        if (parts.ComponentCount != 2 || parts.Parts.Count != 2)
            throw new InvalidDataException("Scenario portrait requires its original two components.");
        PrepareComponent(assets.SourceLayoutSprite(portrait.Source, parts.Parts[1].SpriteName));
        foreach (var document in repository.ScenarioDocuments.Where(document => selections.ContainsKey(document.SourcePath)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var metadata = document.SelectionMetadata
                ?? throw new InvalidDataException("Scenario selection metadata is missing.");
            if (metadata.NativeId >= 199 || metadata.FilterKind is not (0 or 1)) continue;
            int edition = metadata.EditionCode ?? selections[document.SourcePath].Edition switch
            { "CE" => 2, "PK" => 1, _ => 0 };
            if (ScenarioSelectionCatalog.LoadEditionBadge(assets.DeclaredSprites, locale, edition, metadata.NativeId) is { } badge)
                assets.PrepareSprite(badge, srgb: true);
            string year = string.Format(CultureInfo.InvariantCulture, "{0,3}{1}", document.StartYear
                ?? throw new InvalidDataException("Scenario selection year is missing."), yearSuffix);
            string month = string.Format(CultureInfo.InvariantCulture, "{0,2}{1}", document.StartMonth
                ?? throw new InvalidDataException("Scenario selection month is missing."), monthSuffix);
            PrepareRow(16, year);
            PrepareRow(17, month);
            PrepareRow(18, document.Title ?? throw new InvalidDataException("Scenario selection title is missing."));
            PrepareNewGameForceScenarioHeader(assets, font, caches, year, month,
                document.Title ?? throw new InvalidDataException("Scenario selection title is missing."));
            foreach (var person in metadata.PreviewPeople)
            {
                if (person.PersonId is < 1 or > 5999) continue;
                var route = names.PortraitNames(checked((ushort)person.PersonId));
                PrepareComponent(assets.DeclaredSprites.LoadTexturePackSprite(route.ResourceName, route.SpriteName));
            }
        }

        void PrepareRow(int node, string caption)
        {
            var profile = MapFontProfile.FromNode(row.Nodes[node], row.Source.Source.ResourceUri);
            for (int state = 0; state < 5; state++)
                MapFont.PrepareText(font, caption, profile.WithPreset(presets[3 + state]), row.Rectangle(node).Size.X, caches);
        }

        void PrepareComponent(LoadedSprite component) => assets.PrepareSprite(component,
            MapPortraitQuad.UsesSrgbView(repository, assets, component.Definition.Name, component));

        UiSourceLayout Art(uint file, IEnumerable<int> indices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = assets.SourceLayout(file);
            assets.PrepareLayoutArt(source.Source, indices, commonImports: true);
            return source;
        }
    }
}
