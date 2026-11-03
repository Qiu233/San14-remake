using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The live map's common UI resource closure. Load has one worker owner;
/// after publication only the UI thread consumes or disposes this instance.</summary>
internal sealed partial class MapSystemMenuPreparedResources : IDisposable
{
    internal static readonly (int Node, int Message)[] CommandCaptions =
    [ (6, 0x0037), (10, 0x002E), (14, 0x0031), (34, 0x007A), (35, 0x0075),
      (18, 0x0064), (19, 0x0038), (23, 0x00D1), (27, 0x0063), (31, 0x0039), (32, 0x0033) ];
    internal UiAssets Assets { get; }
    internal LoadedMessages Messages { get; }
    internal CollectionCatalog TitleCatalog { get; }
    internal CollectionItemCatalog CollectionItems { get; }
    internal PoemEventCatalog PoemEvents { get; }
    internal MapFontSource FontSource { get; }
    internal UiSourceLayout CommonBlurSource => Assets.SourceLayout(0xAEF1609E);
    internal UiSourceLayout MenuSource => Assets.SourceLayout(0x33EF187B);
    internal UiSourceLayout TransitionSource => Assets.SourceLayout(0xF9C5DB14);
    private readonly Dictionary<MapFont.PreparedTextKey, MapTextCache> textCaches;
    private AdministrationPageChrome.Resources? chromeResources;
    private volatile bool disposed;
    private readonly ResourceOverrides generationOverrides;
    private readonly Dictionary<GameResourceId, ResourceReadGeneration> generation;

    private MapSystemMenuPreparedResources(UiAssets assets, LoadedMessages messages, MapFontSource font,
        Dictionary<MapFont.PreparedTextKey, MapTextCache> caches, ResourceOverrides overrides,
        Dictionary<GameResourceId, ResourceReadGeneration> resources,
        CollectionCatalog collectionCatalog, CollectionItemCatalog collectionItems, PoemEventCatalog poemEvents)
    { Assets = assets; Messages = messages; FontSource = font; textCaches = caches; generationOverrides = overrides; generation = resources;
        TitleCatalog = collectionCatalog; CollectionItems = collectionItems; PoemEvents = poemEvents; }

    // This method creates no GodotObject. Its repository and mutable caches
    // are never the live Main repository and are not used concurrently.
    internal static MapSystemMenuPreparedResources Load(string sourceDirectory, string? overrideDirectory,
        string locale, CancellationToken cancellationToken) =>
        Load(new GameResourceRepository(sourceDirectory, overrideDirectory), locale, cancellationToken);

    internal static MapSystemMenuPreparedResources Load(GameResourceRepository resourceRoot,
        string locale, CancellationToken cancellationToken, UiAssets.PortraitImages? portraits = null)
    {
        ArgumentNullException.ThrowIfNull(resourceRoot);
        cancellationToken.ThrowIfCancellationRequested();
        var reads = new Dictionary<GameResourceId, ResourceReadGeneration>();
        var repository = resourceRoot.CreateReadScope(observed =>
        {
            lock (reads)
            {
                if (reads.TryGetValue(observed.Id, out var previous))
                {
                    if (previous != observed) throw new ResourceReadGenerationChangedException(observed.Id);
                }
                else reads.Add(observed.Id, observed);
            }
        });
        var assets = new UiAssets(repository,portraits);
        try
        {
            foreach (uint file in new uint[] { 0xAEF1609E, 0xDC8AC8AA, 0xD8913AB2, 0xAB6ADAA7,
                0xAD4A3EFA, 0x33EF187B, 0x16D9E461, 0xF9C5DB14 })
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = assets.SourceLayout(file);
            }
            var messages = repository.LoadMessages(locale, MessageKind.Data);
            _ = repository.LoadMessages(locale, MessageKind.Game);
            var records = messages.Messages.Sections[1].Records;
            var font = MapFontSource.Load(repository, locale);
            var caches = new Dictionary<MapFont.PreparedTextKey, MapTextCache>();
            var presets = MapTextPresets.Create();
            var header = assets.SourceLayout(0xDC8AC8AA);
            PrepareArt(header, [1]);
            foreach (int caption in new[] { 0x0473, 0x0026 })
                MapFont.PrepareText(font, Caption(caption),
                    MapFontProfile.FromNode(header.Nodes[2], header.Source.Source.ResourceUri).WithPreset(presets[56]),
                    header.Rectangle(2).Size.X, caches);
            var hint = assets.SourceLayout(0xAB6ADAA7);
            PrepareArt(hint, hint.Nodes.Where(node => node.Type is 1 or 3).Select(node => node.Index));
            var round = assets.SourceLayout(0xAD4A3EFA);
            PrepareArt(round, [3, 5, 8, 12, 14]);
            _ = assets.PrepareCommonCaption(locale, "000_cmn_txt_decision_modoru");
            var menu = assets.SourceLayout(0x33EF187B);
            PrepareArt(menu, [2, 5]);
            foreach (var binding in CommandCaptions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var command = assets.CommandButtonLayout(menu.Rectangle(binding.Node).Size);
                PrepareArt(command, [3, 5, 6, 8, 9, 11, 13, 14]);
                // Parse the retained state profile before this CPU owner transfers to the UI thread.
                _ = assets.ButtonStateProfile(command.Source);
                var profile = MapFontProfile.FromNode(command.Nodes[16], command.Source.Source.ResourceUri);
                for (int state = 0; state < 5; state++)
                    MapFont.PrepareText(font, Caption(binding.Message), profile.WithPreset(presets[28 + state]),
                        command.Rectangle(16).Size.X, caches);
            }
            // These normal Talent pages borrow this map's resources.
            // Decode their authored background art on the existing worker,
            // before ownership transfers to UI consumers. No GodotObject is
            // created here, and this does not retain a campaign or draft.
            foreach (var page in new[] { (File: 0x2854C93Bu, Search: false), (File: 0x639D10C6u, Search: true) })
            {
                cancellationToken.ThrowIfCancellationRequested();
                var layout = assets.SourceLayout(page.File);
                assets.PrepareLayoutArt(layout.Source, TalentOrdersView.PageNodeIndices(layout, page.Search)
                    .Where(index => layout.Nodes[index].Type is 1 or 3));
            }
            // Explicit authored dependencies of the entered title, Collection,
            // event/movie directory and music editor. These are static sources;
            // page rows, playback, captures and editable drafts remain local.
            var hudSources = PrepareHudSources(repository, assets, font, locale, caches, cancellationToken);
            PrepareRightHudSources(assets, cancellationToken);
            PrepareSummarySources(repository, assets, cancellationToken);
            PrepareStandingPortraitLayouts(assets);
            PrepareNarrationSources(assets,cancellationToken);
            PrepareStorySources(repository, assets, font, messages, locale, caches, cancellationToken);
            PrepareBottomHudSources(repository, assets, cancellationToken);
            PrepareReportSources(assets, font, messages, locale, caches, cancellationToken);
            PreparePopularitySources(repository, assets, font, messages, locale, caches, cancellationToken);
            PrepareTitleCollectionSources();
            PrepareScenarioSelectionSources(repository, assets, font, messages, locale, caches, cancellationToken);
            PrepareArmyReturnCaptiveSources(repository, assets, font, messages, locale, caches, cancellationToken);
            PrepareArmyOrderSources(repository, assets, font, messages, locale, caches, cancellationToken);
            PrepareDeploymentSources(repository, assets, font, messages, locale, caches, cancellationToken);
            PrepareMapCommandPopupSources(repository, assets, font, messages, locale, caches, cancellationToken);
            PrepareAreaMapSources(repository, assets, font, locale, caches, cancellationToken);
            PrepareOrdinaryInformationDetails(repository, assets, font, messages, locale, caches, cancellationToken);
            PrepareSceneryInformationSources(repository, assets, font, messages, locale, caches, cancellationToken);
            PrepareSpecialInformationSources(assets, font, messages, caches, cancellationToken);
            var titleCatalogs = PrepareTitleCatalogSources(repository, assets, font, messages, locale, caches, cancellationToken);
            PrepareSceneryLabelSources(repository, assets, font, titleCatalogs.Catalog, caches, cancellationToken);
            PrepareTitleOfficerSources(repository, assets, font, messages, locale, caches, cancellationToken);
            // Parse both true capture parameters now; actual Type6 controls
            // and their compositor registrations still belong to the UI tree.
            _ = assets.SourceLayout(0xAEF1609E).Type6(1);
            _ = assets.SourceLayout(0xAEF1609E).Type6(2);
            _ = menu.Type6(3); _ = menu.Type6(4);
            cancellationToken.ThrowIfCancellationRequested();
            assets.Portraits.Prepare(repository,cancellationToken);
            var overrides = new ResourceOverrides(repository.OverrideDirectory);
            var prepared = new MapSystemMenuPreparedResources(assets, messages, font, caches, overrides, reads,
                titleCatalogs.Catalog, titleCatalogs.Items, titleCatalogs.PoemEvents)
            { hudSources = hudSources };
            if (prepared.ChangedResource() is { } changed)
                throw new ResourceReadGenerationChangedException(changed);
            cancellationToken.ThrowIfCancellationRequested();
            return prepared;

            string Caption(int id) => records[id].Text
                ?? throw new InvalidDataException("Missing map system command caption.");
            void PrepareTitleCollectionSources()
            {
                // Title roots and all five common title button states.
                _ = assets.SourceLayout(0xE7A28866);
                StaticArt(assets.SourceLayout(0x583BEE54), [1]);
                StaticArt(assets.SourceLayout(0x5B2BB9DD), [1]);
                StaticArt(assets.SourceLayout(0xE37E0C60), [1, 4, 5]);
                StaticArt(assets.SourceLayout(0x918FA91E),
                    [3, 4, 5, 7, 8, 9, 10, 12, 13, 14, 15, 17, 18, 19, 21, 22, 23, 24]);
                foreach (uint file in new uint[] { 0x172EDC24, 0x9AFB27B7, 0xD45873AA, 0xCEE6F358 })
                    StaticArt(assets.SourceLayout(file), [3, 5, 8, 11, 13]);
                foreach (uint file in new uint[] { 0xF2F266CA, 0x0C1434D0 })
                    StaticArt(assets.SourceLayout(file), [3, 5, 8, 11, 13, 17]);
                StaticArt(assets.SourceLayout(0x16D9E461), [3, 5, 6, 8, 9, 11, 13, 14]);
                StaticArt(assets.SourceLayout(0x9F416DB7),
                    [2, 5, 7, 8, 10, 11, 13, 19, 21, 22, 24, 25, 27, 33, 35, 36, 38, 39, 41]);
                var collection = assets.SourceLayout("02_04_00_edit_top_new");
                StaticArt(collection, collection.Nodes.Where(node => node.Index != 4 && node.ParentIndex != 4 &&
                    node.Type is 1 or 3).Select(node => node.Index));
                foreach (var node in collection.Nodes.Where(node => node.Index != 4 && node.ParentIndex != 4 && node.Type == 6))
                    _ = collection.Type6(node.Index);
                NamedArt("02_04_00_btn_set_offset_00_new", [1, 2]);
                NamedArt("02_04_00_btn_offset_00_new", [3, 5, 8, 11, 13]);
                NamedArt("02_12_00_BGM_edit", [3]);
                _ = assets.SourceLayout("02_12_00_BGM_edit").Type6(2);
                NamedArt("02_07_04_BGM", [3]);
                _ = assets.SourceLayout("02_07_04_BGM").Type6(2);
                NamedArt("02_12_00_offset_00", [2, 6, 7, 8, 9, 12, 13]);
                NamedArt("02_07_04_offset_00", [2, 6, 7, 9, 10, 11]);
                NamedArt("02_12_00_offset_01", [3, 5, 8, 11, 13]);
                NamedArt("02_07_03_movie", [3]);
                _ = assets.SourceLayout("02_07_03_movie").Type6(2);
                _ = assets.SourceLayout("cmn_header_re_new");
                _ = assets.SourceLayout("cmn_movie");
                NamedArt("cmn_header_offset_00", [1]);
                NamedArt("02_07_03_offset_movie", [3, 5, 6, 8, 9, 11, 13, 14, 16]);
                var events = assets.SourceLayout("09_01_01_event_list_pk_cn");
                StaticArt(events, new[] { 3, 61, 63, 64, 66, 68, 78, 105, 57 }
                    .Where(index => events.Nodes[index].Type is 1 or 3));
                foreach (int index in new[] { 3, 61, 63, 64, 66, 68, 78, 105, 57 }.Where(index => events.Nodes[index].Type == 6))
                    _ = events.Type6(index);
                NamedArt("09_01_01_event_list_offset_00_cn", [3, 7, 8, 10, 12, 14, 15, 18]);
                // This row is authored text; parse its profile before browsing.
                _ = assets.SourceLayout("09_01_01_event_list_offset_02");
                _ = assets.PrepareCommonCaption(locale, "000_cmn_txt_decision_kettei");
                assets.PrepareCommonSprite("000_cmn_icon_advance_play");
                assets.PrepareCommonSprite("000_cmn_icon_advance_stop");
                string language = locale == "TC" ? "tw" : "ch";
                assets.PrepareCommonSprite(language + "_020_sho_mrk_pk", "texture_lang_" + language);
                assets.PrepareCommonSprite(language + "_020_sho_mrk_ce", "texture_ce_lang_" + language);
                // Fixed captions use the same resized profiles and state presets
                // as the actual controls. Dynamic rows still use live text slots.
                var titleSize = assets.SourceLayout(0xE37E0C60).Rectangle(2).Size;
                var titleButton = assets.SourceLayout(0x918FA91E).ResizedSubtree(0,
                    new Vector2((int)titleSize.X, (int)titleSize.Y), true);
                foreach (int id in new[] { 0x2E, 0x2F, 0x2D, 0x7B, 0x66, 0x31, 0x32, 0x33 })
                    FixedCaption(titleButton, 26, id, 3);
                var collectionButtons = assets.SourceLayout("02_04_00_btn_set_offset_00_new");
                int[] collectionCaptions = [0x6E, 0x6C, 0x6D, 0x7DA, 0x7DD, 0xB8, 0xB7];
                int[] editCaptions = [0x30, 0x67, 0x2DF, 0x2E7, 0x72C, 0x7CD, 0x7F5];
                for (int index = 0; index < collectionCaptions.Length; index++)
                {
                    var size = collectionButtons.Rectangle(3 + index * 3).Size;
                    FixedCaption(assets.SourceLayout("02_04_00_btn_offset_00_new").ResizedSubtree(0, size, true),
                        15, collectionCaptions[index], 3);
                    FixedCaption(assets.CommandButtonLayout(size), 16, editCaptions[index], 28);
                }
                foreach (int id in new[] { 0x32, 0x66, 0x2DF, 0x6E, 0x7DA, 0x7DD })
                    MapFont.PrepareText(font, Caption(id),
                        MapFontProfile.FromNode(header.Nodes[2], header.Source.Source.ResourceUri).WithPreset(presets[56]),
                        header.Rectangle(2).Size.X, caches);

                void FixedCaption(UiSourceLayout layout, int node, int message, int preset)
                {
                    var profile = MapFontProfile.FromNode(layout.Nodes[node], layout.Source.Source.ResourceUri);
                    for (int state = 0; state < 5; state++)
                        MapFont.PrepareText(font, Caption(message), profile.WithPreset(presets[preset + state]),
                            layout.Rectangle(node).Size.X, caches);
                }

                void NamedArt(string name, IEnumerable<int> indices) => StaticArt(assets.SourceLayout(name), indices);
                void StaticArt(UiSourceLayout layout, IEnumerable<int> indices)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    assets.PrepareLayoutArt(layout.Source, indices, commonImports: true);
                }
            }
            void PrepareArt(UiSourceLayout layout, IEnumerable<int> indices)
            {
                cancellationToken.ThrowIfCancellationRequested();
                // These Config/chrome layouts import the native Common bank zero.
                // Resolve that declared owner, not a global atlas-name directory.
                assets.PrepareLayoutArt(layout.Source, indices, commonImports: true);
            }
        }
        catch { assets.Dispose(); throw; }
    }

    // Only an open-page event asks this finite question. No payload reads,
    // hashes, world scans or per-frame checks are performed here.
    internal bool IsCurrentGeneration() => !disposed && ChangedResource() is null;

    private GameResourceId? ChangedResource()
    {
        var files = new Dictionary<string, (bool Exists, long Length, long Ticks)>(StringComparer.OrdinalIgnoreCase);
        var directories = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        // Shared pages may lazily introduce another sprite after the worker
        // has published. Snapshot only these actual reads, once on page open;
        // a concurrent Config generation check must not enumerate a writer.
        ResourceReadGeneration[] snapshot;
        lock (generation) snapshot = generation.Values.ToArray();
        foreach (var resource in snapshot)
        {
            string? replacement = null;
            if (generationOverrides.DirectoryPath is { } root)
            {
                string parent = Path.GetDirectoryName(Path.Combine(root, resource.Id.OverrideRelativePath))!;
                if (!directories.TryGetValue(parent, out bool exists))
                    directories.Add(parent, exists = Directory.Exists(parent));
                if (exists) replacement = generationOverrides.Find(resource.Id);
            }
            string selected = Path.GetFullPath(replacement ?? resource.FallbackPath);
            if (!StringComparer.OrdinalIgnoreCase.Equals(selected, resource.SelectedPath)) return resource.Id;
            if (!files.TryGetValue(selected, out var stamp))
            {
                try
                {
                    var file = new FileInfo(selected);
                    stamp = (file.Exists, file.Exists ? file.Length : 0, file.Exists ? file.LastWriteTimeUtc.Ticks : 0);
                }
                catch (FileNotFoundException) { stamp = default; }
                catch (DirectoryNotFoundException) { stamp = default; }
                files.Add(selected, stamp);
            }
            if (!stamp.Exists || stamp.Length != resource.Length || stamp.Ticks != resource.LastWriteUtcTicks)
                return resource.Id;
        }
        return null;
    }

    // UI thread only. The containing map owns this shared resource set; all
    // borrowed pages must exit before that owner calls Dispose.
    internal AdministrationPageChrome.Resources CreateChromeResources()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (chromeResources is not null) return chromeResources;
        // This first call occurs on Main during startup. All finite static
        // art and prepared captions become Godot resources here; later page
        // borrowers only receive these same textures, font and shader.
        Assets.MaterializePreparedTextures();
        var font = new MapFont(FontSource, textCaches);
        try
        {
            font.MaterializePreparedText();
            var shader = UiSourceSprite.CreateShader();
            return chromeResources = new(Assets, font, shader, Messages);
        }
        catch { font.Dispose(); throw; }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        hudResources?.Dispose(); hudResources = null;
        if (chromeResources is { } resources)
        { resources.Font.Dispose(); resources.SpriteShader.Dispose(); chromeResources = null; }
        Assets.Dispose(); textCaches.Clear();
    }
}
