using Godot;
using San14.Data;
using San14.Data.Formats;

/// <summary>The Config page's CPU resource closure. Load has one worker owner;
/// after publication only the UI thread consumes or disposes this instance.</summary>
internal sealed class MapSystemMenuPreparedResources : IDisposable
{
    internal static readonly (int Node, int Message)[] CommandCaptions =
    [ (6, 0x0037), (10, 0x002E), (14, 0x0031), (34, 0x007A), (35, 0x0075),
      (18, 0x0064), (19, 0x0038), (23, 0x00D1), (27, 0x0063), (31, 0x0039), (32, 0x0033) ];
    internal UiAssets Assets { get; }
    internal LoadedMessages Messages { get; }
    internal MapFontSource FontSource { get; }
    internal UiSourceLayout CommonBlurSource => Assets.SourceLayout(0xAEF1609E);
    internal UiSourceLayout MenuSource => Assets.SourceLayout(0x33EF187B);
    internal UiSourceLayout TransitionSource => Assets.SourceLayout(0xF9C5DB14);
    private readonly Dictionary<MapFont.PreparedTextKey, MapTextCache> textCaches;
    private AdministrationPageChrome.Resources? chromeResources;
    private volatile bool disposed;
    private readonly ResourceOverrides generationOverrides;
    private readonly ResourceReadGeneration[] generation;

    private MapSystemMenuPreparedResources(UiAssets assets, LoadedMessages messages, MapFontSource font,
        Dictionary<MapFont.PreparedTextKey, MapTextCache> caches, ResourceOverrides overrides,
        ResourceReadGeneration[] resources)
    { Assets = assets; Messages = messages; FontSource = font; textCaches = caches; generationOverrides = overrides; generation = resources; }

    // This method creates no GodotObject. Its repository and mutable caches
    // are never the live Main repository and are not used concurrently.
    internal static MapSystemMenuPreparedResources Load(string sourceDirectory, string? overrideDirectory,
        string locale, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var reads = new Dictionary<GameResourceId, ResourceReadGeneration>();
        var repository = new GameResourceRepository(sourceDirectory, overrideDirectory, observed =>
        {
            if (reads.TryGetValue(observed.Id, out var previous))
            {
                if (previous != observed) throw new ResourceReadGenerationChangedException(observed.Id);
            }
            else reads.Add(observed.Id, observed);
        });
        var assets = new UiAssets(repository);
        try
        {
            foreach (uint file in new uint[] { 0xAEF1609E, 0xDC8AC8AA, 0xD8913AB2, 0xAB6ADAA7,
                0xAD4A3EFA, 0x33EF187B, 0x16D9E461, 0xF9C5DB14 })
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = assets.SourceLayout(file);
            }
            var messages = repository.LoadMessages(locale, MessageKind.Data);
            var records = messages.Messages.Sections[1].Records;
            var font = MapFontSource.Load(repository, locale);
            var caches = new Dictionary<MapFont.PreparedTextKey, MapTextCache>();
            var presets = MapTextPresets.Create();
            var header = assets.SourceLayout(0xDC8AC8AA);
            PrepareArt(header, [1]);
            MapFont.PrepareText(font, Caption(0x0473),
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
                PrepareArt(command, [3, 5, 8, 11, 13]);
                var profile = MapFontProfile.FromNode(command.Nodes[16], command.Source.Source.ResourceUri);
                for (int state = 0; state < 5; state++)
                    MapFont.PrepareText(font, Caption(binding.Message), profile.WithPreset(presets[28 + state]),
                        command.Rectangle(16).Size.X, caches);
            }
            // Parse both true capture parameters now; actual Type6 controls
            // and their compositor registrations still belong to the UI tree.
            _ = assets.SourceLayout(0xAEF1609E).Type6(1);
            _ = assets.SourceLayout(0xAEF1609E).Type6(2);
            _ = menu.Type6(3); _ = menu.Type6(4);
            cancellationToken.ThrowIfCancellationRequested();
            var overrides = new ResourceOverrides(repository.OverrideDirectory);
            var prepared = new MapSystemMenuPreparedResources(assets, messages, font, caches, overrides, reads.Values.ToArray());
            if (prepared.ChangedResource() is { } changed)
                throw new ResourceReadGenerationChangedException(changed);
            cancellationToken.ThrowIfCancellationRequested();
            return prepared;

            string Caption(int id) => records[id].Text
                ?? throw new InvalidDataException("Missing map system command caption.");
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
        foreach (var resource in generation)
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
        var font = new MapFont(FontSource, textCaches);
        try
        {
            var shader = UiSourceSprite.CreateShader();
            return chromeResources = new(Assets, font, shader, Messages);
        }
        catch { font.Dispose(); throw; }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (chromeResources is { } resources)
        { resources.Font.Dispose(); resources.SpriteShader.Dispose(); chromeResources = null; }
        Assets.Dispose(); textCaches.Clear();
    }
}
