using Godot;
using San14.Data;

public partial class Main
{
    // Shared UI definitions belong to this application and locale. Pages and
    // maps borrow them; only resource replacement or process shutdown retires
    // a generation, after its final actual UI borrower has exited.
    private sealed class MapSystemResourceWorkspace(GameResourceRepository repository, long resourceRevision,
        string locale, CancellationTokenSource lifetime, Task<MapSystemMenuPreparedResources> preparation)
    {
        internal readonly GameResourceRepository Repository = repository;
        internal readonly long ResourceRevision = resourceRevision;
        internal bool HasCurrentResourceRevision => Repository.ResourceRevision == ResourceRevision;
        internal readonly string Locale = locale;
        internal readonly CancellationTokenSource Lifetime = lifetime;
        internal readonly Task<MapSystemMenuPreparedResources> Preparation = preparation;
        internal MapSystemMenuPreparedResources? Resources;
        internal int Borrowers;
        internal bool Retired;
        internal readonly TaskCompletionSource Retirement=new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private readonly Dictionary<string, MapSystemResourceWorkspace> _applicationUiResources = [];
    // Keep the startup resource root. Campaign/query scopes may be replaced
    // without changing the process-owned static content or its explicit epoch.
    private GameResourceRepository? _applicationUiResourceRepository;
    private UiAssets.PortraitImages? _applicationPortraitImages;
    private long _applicationPortraitRevision=-1;
    private readonly List<UiAssets.PortraitImages> _applicationPortraitGenerations=[];
    // Pending/faulted completions do not retain Task<T>.Result and its disposed
    // page resources. Successful entries leave at existing resource boundaries.
    private readonly List<Task> _applicationUiResourceRetirements=[];

    private async Task PrepareApplicationUiResourcesAsync()
    {
        // CPU closures complete during startup, then Main materializes their
        // static Godot textures before title input is admitted. No World or
        // native map device/session is constructed by this resource stage.
        foreach (string locale in new[] { "TC", "SC" })
        {
            while (true)
            {
                var owner = EnsureMapSystemResourcePreparation(null, checkGeneration: false, requestedLocale: locale);
                var resources = await owner.Preparation;
                if (_shuttingDown || _failed || !IsInsideTree()) return;
                if (!owner.HasCurrentResourceRevision)
                {
                    RetireMapSystemResourcePreparation(owner);
                    continue;
                }
                try { await resources.Assets.Portraits.MaterializeAsync(); }
                catch(OperationCanceledException) when(owner.Retired||_shuttingDown||_failed||!IsInsideTree())
                {
                    if(_shuttingDown||_failed||!IsInsideTree())return;
                    continue;
                }
                if(_shuttingDown||_failed||!IsInsideTree())return;
                if(owner.Retired||!owner.HasCurrentResourceRevision)
                {
                    RetireMapSystemResourcePreparation(owner);
                    continue;
                }
                MaterializeApplicationUiResources(owner, resources);
                break;
            }
        }
    }

    private void EnsureMapSystemMenuResourcesForLiveMap()
    {
        if (_failed || _shuttingDown || IsReturningToTitle || _repository is null || _ui is null ||
            _residentMap is not { } map || _session is not { PlayerForceId: not null } session ||
            session.World.Phase == San14.Core.TurnPhase.Initializing) return;
        // Normal world commits only compare these retained owners. No source
        // stat, resource hash or business graph is read on this path.
        _ = EnsureMapSystemResourcePreparation(map, checkGeneration: false);
    }

    private MapSystemResourceWorkspace EnsureMapSystemResourcePreparation(MapSceneView? map, bool checkGeneration,
        string? requestedLocale = null)
    {
        PruneCompletedApplicationUiRetirements();
        var repository = _applicationUiResourceRepository ??=
            _repository ?? throw new InvalidOperationException("Settings resources require their repository owner.");
        string locale = requestedLocale ?? _ui.SourceLocale;
        _applicationUiResources.TryGetValue(locale, out var current);
        if (current is { Retired: false } && current.Repository.SharesResidentSourcesWith(repository) &&
            current.HasCurrentResourceRevision)
        {
            // The application's source notifications advance this one epoch.
            // Standalone readers without that owner retain their cold checks.
            if (!checkGeneration || !current.Preparation.IsCompleted || current.Preparation.IsCompletedSuccessfully) return current;
        }
        if (current is not null) RetireMapSystemResourcePreparation(current);
        var lifetime = new CancellationTokenSource(); var token = lifetime.Token;
        long revision = repository.ResourceRevision;
        if(_applicationPortraitImages is null||_applicationPortraitImages.IsDisposed||_applicationPortraitRevision!=revision)
        {
            // Retire unused locale facades of the old application generation
            // together. Actual live pages keep their existing borrower lease;
            // an unopened locale must not pin an obsolete image bank forever.
            foreach(var stale in _applicationUiResources.Values.Where(item=>item.ResourceRevision!=revision).ToArray())
                RetireMapSystemResourcePreparation(stale);
            _applicationPortraitImages=new UiAssets.PortraitImages();_applicationPortraitRevision=revision;
            _applicationPortraitGenerations.Add(_applicationPortraitImages);
        }
        var portraitImages=_applicationPortraitImages;
        var task = Task.Run(() => MapSystemMenuPreparedResources.Load(repository, locale, token, portraitImages), token);
        var workspace = new MapSystemResourceWorkspace(repository, revision, locale, lifetime, task);
        _applicationUiResources[locale] = workspace;
        ObserveMapSystemResourcePreparation(workspace);
        return workspace;
    }

    private async void ObserveMapSystemResourcePreparation(MapSystemResourceWorkspace workspace)
    {
        try
        {
            workspace.Resources = await workspace.Preparation;
            if (workspace.Retired && workspace.Borrowers == 0) CompleteMapSystemResourceRetirement(workspace);
        }
        catch (OperationCanceledException) when (workspace.Lifetime.IsCancellationRequested)
        { if(workspace.Retired)workspace.Retirement.TrySetResult(); }
        catch (Exception)
        {
            if(workspace.Retired && !workspace.Preparation.IsCompletedSuccessfully)workspace.Retirement.TrySetResult();
            // Observe the worker even when no page was requested. A real open
            // awaits this same task and reports its ordinary preparation error.
        }
    }

    private Action BorrowMapSystemResources(MapSystemResourceWorkspace workspace,
        Control view, MapSystemMenuPreparedResources resources)
    {
        if (workspace.Retired ||
            !workspace.Preparation.IsCompletedSuccessfully || !ReferenceEquals(workspace.Preparation.Result, resources))
            throw new InvalidOperationException("The Settings resource bundle has another presentation owner.");
        workspace.Resources = resources; workspace.Borrowers++;
        bool released = false;
        return () =>
        {
            // Called by the actual page's child-first _ExitTree, after every
            // font/RID borrower has retired; never on merely QueueFree.
            if (released) return;
            released = true; workspace.Borrowers--;
            if (workspace.Retired && workspace.Borrowers == 0) CompleteMapSystemResourceRetirement(workspace);
        };
    }

    private AdministrationPageChrome.Resources TryBorrowMapUiResources(Control view) =>
        TryBorrowApplicationUiResources(view);

    // Every ordinary call uses the completed startup owner. A genuine source
    // update may replace this finite closure; its CPU worker has no UI-thread
    // dependency, so the rare replacement can be joined here. A failed parse
    // is visible to the original page error path, never a local-cache fallback.
    private AdministrationPageChrome.Resources TryBorrowApplicationUiResources(Control view) =>
        TryBorrowApplicationPreparedResources(view).CreateChromeResources();

    private MapHudResources TryBorrowApplicationHudResources(Control view) =>
        TryBorrowApplicationPreparedResources(view).CreateHudResources();

    private static void MaterializeApplicationUiResources(MapSystemResourceWorkspace workspace,
        MapSystemMenuPreparedResources resources)
    {
        // Bundled text uses Godot FileAccess, so resolve it on Main after the
        // CPU source transfer and before the shared font is materialized.
        if (!resources.HudCaptionsPrepared)
            resources.PrepareHudCaptions(new PreviewText(workspace.Locale == "TC",
                new ResourceOverrides(workspace.Repository.OverrideDirectory)));
        _ = resources.CreateChromeResources();
        _ = resources.CreateHudResources();
    }

    private MapSystemMenuPreparedResources TryBorrowApplicationPreparedResources(Control view)
    {
        var resources = AcquireApplicationPreparedResources(view, out var release);
        // Ordinary pages release after their actual child-first tree exit.
        view.TreeExited += release;
        return resources;
    }

    private (AdministrationPageChrome.Resources Resources, Action Release) BorrowApplicationSummaryResources(Control view)
    {
        var resources = AcquireApplicationPreparedResources(view, out var release);
        // The map explicitly tears down ordinary and externally mounted
        // summaries, then releases this lease at that same ownership boundary.
        return (resources.CreateChromeResources(), release);
    }

    private MapSystemMenuPreparedResources AcquireApplicationPreparedResources(Control view, out Action release)
    {
        if (_repository is null)
            throw new InvalidOperationException("Application UI resources require their repository owner.");
        while (true)
        {
            var workspace = EnsureMapSystemResourcePreparation(null, checkGeneration: true);
            MapSystemMenuPreparedResources resources;
            try
            {
                resources = workspace.Preparation.GetAwaiter().GetResult();
                if (!workspace.HasCurrentResourceRevision ||
                    (!workspace.Repository.HasResourceChangeNotifications && !resources.IsCurrentGeneration()))
                {
                    RetireMapSystemResourcePreparation(workspace);
                    continue;
                }
            }
            catch (ResourceReadGenerationChangedException)
            {
                RetireMapSystemResourcePreparation(workspace);
                continue;
            }
            MaterializeApplicationUiResources(workspace, resources);
            release = BorrowMapSystemResources(workspace, view, resources);
            return resources;
        }
    }

    private void RetireMapSystemResourcePreparation(MapSystemResourceWorkspace? workspace = null)
    {
        if (workspace is null)
        {
            foreach (var current in _applicationUiResources.Values.ToArray())
                RetireMapSystemResourcePreparation(current);
            return;
        }
        if (workspace.Retired) return;
        workspace.Retired = true;
        _applicationUiResourceRetirements.Add(workspace.Retirement.Task);
        if (_applicationUiResources.GetValueOrDefault(workspace.Locale) == workspace)
            _applicationUiResources.Remove(workspace.Locale);
        workspace.Lifetime.Cancel(); workspace.Lifetime.Dispose();
        if (workspace.Borrowers == 0 && workspace.Preparation.IsCompletedSuccessfully)
            CompleteMapSystemResourceRetirement(workspace);
        else if(workspace.Preparation.IsCompleted&&!workspace.Preparation.IsCompletedSuccessfully)
            workspace.Retirement.TrySetResult();
        PruneCompletedApplicationUiRetirements();
    }

    private static void CompleteMapSystemResourceRetirement(MapSystemResourceWorkspace workspace)
    {
        try
        {
            (workspace.Resources??workspace.Preparation.Result).Dispose();
            workspace.Retirement.TrySetResult();
        }
        catch(Exception error){workspace.Retirement.TrySetException(error);throw;}
    }

    private async Task RetireApplicationUiResourcesAsync()
    {
        // The real close path has closed and queued all UI borrowers before
        // waiting here. Their actual TreeExited callbacks release the leases;
        // keep Godot alive through the final image detach/fence/free task.
        RetireMapSystemResourcePreparation();
        await Task.WhenAll(_applicationUiResourceRetirements.ToArray());
        await Task.WhenAll(_applicationPortraitGenerations.Select(owner=>owner.FinishUnborrowedRetirementAsync()));
        PruneCompletedApplicationUiRetirements();
    }

    private void PruneCompletedApplicationUiRetirements()
    {
        // Called on Main at preparation/replacement/shutdown boundaries only.
        // Faulted completions remain observable to the real shutdown await;
        // IsDisposed alone is not evidence that image retirement finished.
        _applicationUiResourceRetirements.RemoveAll(completion=>completion.IsCompletedSuccessfully);
        _applicationPortraitGenerations.RemoveAll(owner=>owner.Retirement.IsCompletedSuccessfully);
    }
}
