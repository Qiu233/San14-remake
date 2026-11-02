using Godot;
using San14.Data;

public partial class Main
{
    private MapStaticResources? _applicationMapResources;
    private CancellationTokenSource? _applicationMapResourceLifetime;
    private string? _applicationTerrainProgramInputRoot;
    private string? _applicationMapStaticMediaRoot;
    private string? _applicationMapStaticMediaNamespace;
    private Task? _applicationMapStaticMediaRetirement;
    private Task<MapStaticResources>? _applicationMapResourcePreparation;

    private async Task PrepareApplicationMapResourcesAsync()
    {
        if (_shuttingDown || _failed || !IsInsideTree()) return;
        var root = _applicationUiResourceRepository ??= _repository ??
            throw new InvalidOperationException("Static map loading requires the application resource root.");
        using Godot.FileAccess sceneFile = Godot.FileAccess.Open("res://ui/scene-preview.json", Godot.FileAccess.ModeFlags.Read)
            ?? throw new InvalidDataException("Scene preview configuration is missing.");
        using Godot.FileAccess projectionFile = Godot.FileAccess.Open("res://ui/map-projection.json", Godot.FileAccess.ModeFlags.Read)
            ?? throw new InvalidDataException("Map projection configuration is missing.");
        var configuration = MapSceneConfiguration.Parse(sceneFile.GetAsText(), projectionFile.GetAsText(), embedded: true);
        string nativeProgramRoot = _applicationTerrainProgramInputRoot ??=
            ProjectSettings.GlobalizePath("user://render-cache/static-terrain-programs");
        var lifetime = _applicationMapResourceLifetime ??= new CancellationTokenSource();
        var token = lifetime.Token;
        string staticMediaRoot = RequireApplicationMapStaticMediaRoot(nativeProgramRoot);
        while (true)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var loaded = await (_applicationMapResourcePreparation = Task.Run(() => MapStaticResources.Load(root, configuration, nativeProgramRoot, staticMediaRoot, token), token));
                if (_shuttingDown || _failed || !IsInsideTree()) return;
                if (!loaded.IsCurrentGeneration()) continue;
                _applicationMapResources = loaded;
                return;
            }
            catch (ResourceReadGenerationChangedException) when (!token.IsCancellationRequested) { }
        }
    }

    private MapStaticResources BorrowApplicationMapResources()
    {
        if (_shuttingDown || _failed || !IsInsideTree())
            throw new InvalidOperationException("The application is not admitting another map.");
        var current = _applicationMapResources ?? throw new InvalidOperationException(
            "The actual startup static map load has not completed.");
        if (current.IsCurrentGeneration()) return current;
        var root = _applicationUiResourceRepository ?? throw new InvalidOperationException("Static map resource root is absent.");
        var token = _applicationMapResourceLifetime?.Token ?? throw new InvalidOperationException("Static map resource lifetime is absent.");
        string nativeProgramRoot = _applicationTerrainProgramInputRoot
            ?? throw new InvalidOperationException("Static Landscape program input namespace is absent.");
        string staticMediaRoot = RequireApplicationMapStaticMediaRoot(nativeProgramRoot);
        // Only a real source replacement enters this path. Join the complete
        // new CPU closure; never fall back to per-scene cold loading or mutate
        // the old snapshots still borrowed by an existing map.
        while (true)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var loaded = (_applicationMapResourcePreparation = Task.Run(() => MapStaticResources.Load(root, current.Configuration, nativeProgramRoot,
                    staticMediaRoot, token, current), token)).GetAwaiter().GetResult();
                if (!loaded.IsCurrentGeneration()) continue;
                if (applicationNativeMapDevice is { } deviceOwner)
                    Task.Run(() =>
                    {
                        deviceOwner.LoadTerrainPrograms(loaded.TerrainPrograms);
                        deviceOwner.LoadPrimaryModels(loaded.PrimaryGpuMedia);
                        deviceOwner.LoadRouteMedia(loaded.RouteGpuMedia);
                    }).GetAwaiter().GetResult();
                if (!loaded.IsCurrentGeneration()) continue;
                return _applicationMapResources = loaded;
            }
            catch (ResourceReadGenerationChangedException) when (!token.IsCancellationRequested) { }
        }
    }


    private string RequireApplicationMapStaticMediaRoot(string nativeProgramRoot)
    {
        if (_applicationMapStaticMediaRoot is { } existing) return existing;
        // This process alone creates and owns this exact directory. All live
        // resource generations retain it; page retirement never removes it.
        string mediaNamespace = Path.GetFullPath(Path.Combine(nativeProgramRoot, "application-media"));
        string created = Path.Combine(mediaNamespace, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(created);
        _applicationMapStaticMediaNamespace = mediaNamespace;
        return _applicationMapStaticMediaRoot = created;
    }

    private Task RetireApplicationMapStaticMediaAsync()
    {
        if (_applicationMapStaticMediaRetirement is null || _applicationMapStaticMediaRetirement.IsFaulted ||
            _applicationMapStaticMediaRetirement.IsCanceled)
            _applicationMapStaticMediaRetirement = RetireApplicationMapStaticMediaCoreAsync();
        return _applicationMapStaticMediaRetirement;
    }

    private async Task RetireApplicationMapStaticMediaCoreAsync()
    {
        _applicationMapResourceLifetime?.Cancel();
        if (_applicationMapResourcePreparation is { } preparing)
        {
            // Startup already observes its resource error. Join its worker so
            // no file writer can outlive this final, post-native retirement.
            try { await preparing; } catch { }
        }
        if (_applicationMapStaticMediaRoot is not { } owned) return;
        string resolved = Path.GetFullPath(owned);
        if (_applicationMapStaticMediaNamespace is not { } mediaNamespace ||
            !StringComparer.OrdinalIgnoreCase.Equals(resolved, owned) ||
            !StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(resolved), mediaNamespace) ||
            !Guid.TryParseExact(Path.GetFileName(resolved), "N", out _))
            throw new InvalidOperationException("Static media retirement does not name this application's created directory.");
        if (Directory.Exists(resolved))
        {
            if ((File.GetAttributes(resolved) & FileAttributes.ReparsePoint) != 0 ||
                (File.GetAttributes(mediaNamespace) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Static media retirement cannot follow a replaced directory junction.");
            // Directory.Delete removes child reparse points themselves; it does
            // not traverse their targets. No parent or historical cache is selected.
            Directory.Delete(resolved, recursive: true);
        }
        _applicationMapStaticMediaRoot = null;
        _applicationMapResources = null;
    }

    private void StopApplicationMapResourcePreparation()
    {
        var lifetime = _applicationMapResourceLifetime; _applicationMapResourceLifetime = null;
        lifetime?.Cancel(); lifetime?.Dispose();
        // CPU-only static generations remain strongly held by existing views
        // until they exit. Their root and content then leave with the process.
    }
}
