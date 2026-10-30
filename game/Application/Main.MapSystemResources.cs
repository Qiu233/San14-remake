using Godot;
using San14.Data;

public partial class Main
{
    // One finite resource bundle belongs to the actual live map and locale.
    // It owns no Session, World, command callback or prepared map publication.
    private sealed class MapSystemResourceWorkspace(GameResourceRepository repository, MapSceneView? map,
        string locale, CancellationTokenSource lifetime, Task<MapSystemMenuPreparedResources> preparation)
    {
        internal readonly GameResourceRepository Repository = repository;
        internal readonly MapSceneView? Map = map;
        internal readonly string Locale = locale;
        internal readonly CancellationTokenSource Lifetime = lifetime;
        internal readonly Task<MapSystemMenuPreparedResources> Preparation = preparation;
        internal MapSystemMenuPreparedResources? Resources;
        internal int Borrowers;
        internal Action? MapExited;
        internal bool Retired;
    }
    private MapSystemResourceWorkspace? _mapSystemResources;

    private void EnsureMapSystemMenuResourcesForLiveMap()
    {
        if (_failed || _shuttingDown || IsReturningToTitle || _repository is null || _ui is null ||
            _residentMap is not { } map || _session is not { PlayerForceId: not null } session ||
            session.World.Phase == San14.Core.TurnPhase.Initializing) return;
        // Normal world commits only compare these retained owners. No source
        // stat, resource hash or business graph is read on this path.
        _ = EnsureMapSystemResourcePreparation(map, checkGeneration: false);
    }

    private MapSystemResourceWorkspace EnsureMapSystemResourcePreparation(MapSceneView? map, bool checkGeneration)
    {
        var repository = _repository ?? throw new InvalidOperationException("Settings resources require their repository owner.");
        string locale = _ui.SourceLocale;
        if (_mapSystemResources is { Retired: false } current && current.Repository == repository &&
            current.Map == map && current.Locale == locale)
        {
            // Only an actual open checks the finite source-cache generation,
            // including newly added or removed normal resource overrides.
            if (!checkGeneration || !current.Preparation.IsCompleted || current.Preparation.IsCompletedSuccessfully) return current;
        }
        RetireMapSystemResourcePreparation();
        var lifetime = new CancellationTokenSource(); var token = lifetime.Token;
        string sourceDirectory = repository.SourceDirectory; string? overrides = repository.OverrideDirectory;
        var task = Task.Run(() => MapSystemMenuPreparedResources.Load(sourceDirectory, overrides, locale, token), token);
        var workspace = new MapSystemResourceWorkspace(repository, map, locale, lifetime, task);
        _mapSystemResources = workspace;
        if (map is not null)
        {
            workspace.MapExited = () => RetireMapSystemResourcePreparation(workspace);
            map.TreeExiting += workspace.MapExited;
        }
        ObserveMapSystemResourcePreparation(workspace);
        return workspace;
    }

    private async void ObserveMapSystemResourcePreparation(MapSystemResourceWorkspace workspace)
    {
        try
        {
            workspace.Resources = await workspace.Preparation;
            if (workspace.Retired && workspace.Borrowers == 0) workspace.Resources.Dispose();
        }
        catch (OperationCanceledException) when (workspace.Lifetime.IsCancellationRequested) { }
        catch (Exception)
        {
            // Observe the worker even when no page was requested. A real open
            // awaits this same task and reports its ordinary preparation error.
        }
    }

    private Action BorrowMapSystemResources(MapSystemResourceWorkspace workspace,
        MapSystemMenuView view, MapSystemMenuPreparedResources resources)
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
            if (workspace.Retired && workspace.Borrowers == 0) workspace.Resources?.Dispose();
        };
    }

    private void RetireMapSystemResourcePreparation(MapSystemResourceWorkspace? workspace = null)
    {
        workspace ??= _mapSystemResources;
        if (workspace is null || workspace.Retired) return;
        workspace.Retired = true;
        if (_mapSystemResources == workspace) _mapSystemResources = null;
        if (workspace.Map is { } map && workspace.MapExited is { } exited && GodotObject.IsInstanceValid(map))
            map.TreeExiting -= exited;
        workspace.MapExited = null;
        workspace.Lifetime.Cancel(); workspace.Lifetime.Dispose();
        if (workspace.Borrowers == 0 && workspace.Preparation.IsCompletedSuccessfully)
            workspace.Preparation.Result.Dispose();
    }
}
