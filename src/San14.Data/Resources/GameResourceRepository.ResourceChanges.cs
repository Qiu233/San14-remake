namespace San14.Data;

public sealed partial class GameResourceRepository
{
    /// <summary>The application root starts this before its first source read.
    /// Read scopes borrow the same notification owner and never stop it.</summary>
    public void StartResourceChangeNotifications() => _residentSources.StartChanges(SourceDirectory, OverrideDirectory);
    public void StopResourceChangeNotifications() => _residentSources.StopChanges();
    public bool HasResourceChangeNotifications => _residentSources.HasChangeNotifications;

    private void TrackResourceSource(ResourceReadGeneration source) => _residentSources.TrackSource(
        source.Id, source.FallbackPath, source.SelectedPath, _overrides.CandidatePath(source.Id),
        _residentSources.PortraitMetadataSources.ContainsKey(source.Id));

    private void TrackPortraitSource(GameResourceId id, string fallback, string? selected) =>
        _residentSources.TrackSource(id, fallback, selected ?? fallback, _overrides.CandidatePath(id), portrait: true, UserPortraitDirectory);

    private sealed partial class ResidentSourceCache
    {
        private readonly object changeLifetime = new();
        private volatile SourceChangeObserver? externalChanges;

        internal bool HasChangeNotifications
        {
            get { var current = externalChanges; current?.ThrowIfFailed(); return current is not null; }
        }
        private void RequireChangeObserver() => externalChanges?.ThrowIfFailed();

        internal void StartChanges(string originalRoot, string? overrideRoot)
        {
            lock (changeLifetime)
            {
                if (externalChanges is not null) { externalChanges.ThrowIfFailed(); return; }
                if (!slots.IsEmpty)
                    throw new InvalidOperationException("Application source notifications must start before resource reads.");
                externalChanges = new(originalRoot, overrideRoot, SourceChanged, InvalidateAllSources);
            }
        }
        internal void StopChanges()
        {
            SourceChangeObserver? previous;
            lock (changeLifetime) { previous = externalChanges; externalChanges = null; }
            previous?.Dispose();
        }
        internal void TrackSource(GameResourceId id, string fallback, string selected,
            string? replacement, bool portrait, string? portraitRoot = null)
        {
            var current = externalChanges;
            if (current is null) return;
            current.Track(id, fallback, portrait, portraitRoot);
            if (!StringComparer.OrdinalIgnoreCase.Equals(fallback, selected)) current.Track(id, selected, portrait, portraitRoot);
            if (replacement is not null) current.Track(id, replacement, portrait, portraitRoot);
        }
        private void SourceChanged(IReadOnlyCollection<GameResourceId> resources, bool portrait)
        {
            // Notification threads only change shared slots/epochs. Scope-local
            // parsed/UI dictionaries synchronize on their normal reader thread.
            var changed = new HashSet<GameResourceId>(resources, ResourceIdComparer.Instance);
            if (portrait)
            {
                changed.UnionWith(PortraitMetadataSources.Keys);
                changed.Add(GameResourceId.File("ADD_FACE/ManageData.s14"));
            }
            foreach (var resource in changed) Invalidate(resource);
        }
        internal void InvalidatePortraitSources() => SourceChanged([], portrait: true);

        private void InvalidateAllSources()
        {
            // Only an actual watcher error reaches this conservative reset.
            // It scans in-memory cache slots, never files, worlds or frames.
            foreach (var slot in slots.Values)
                lock (slot.Gate)
                { slot.Version++; slot.File = null; slot.Archive = null; slot.Payload = null; }
            Interlocked.Increment(ref revision);
        }

        private sealed class SourceChangeObserver(string originalRoot, string? overrideRoot,
            Action<IReadOnlyCollection<GameResourceId>, bool> changed, Action uncertain) : IDisposable
        {
            private sealed class Target
            {
                internal readonly HashSet<GameResourceId> Resources = new(ResourceIdComparer.Instance);
                internal bool Portrait;
            }
            private sealed class DirectoryWatch(string directory, bool recursive, FileSystemWatcher watcher)
            {
                internal readonly string Directory = directory;
                internal readonly bool Recursive = recursive;
                internal readonly FileSystemWatcher Watcher = watcher;
                internal readonly HashSet<string> Boundaries = new(StringComparer.OrdinalIgnoreCase);
                internal bool Activating;
                internal Exception? ActivationError;
            }
            private readonly object gate = new();
            private readonly Dictionary<string, Target> targets = new(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> roots = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, DirectoryWatch> watches = new(StringComparer.OrdinalIgnoreCase);
            private volatile Exception? failure;
            private bool disposed;

            internal void ThrowIfFailed()
            {
                if (failure is { } error) throw new IOException("Resource change notifications failed.", error);
            }
            internal void Track(GameResourceId resource, string physicalPath, bool portrait, string? portraitRoot)
            {
                lock (gate)
                {
                    if (disposed) throw new ObjectDisposedException(nameof(SourceChangeObserver));
                    ThrowIfFailed();
                    string path = Path.GetFullPath(physicalPath);
                    if (targets.TryGetValue(path, out var preceding))
                    { preceding.Resources.Add(resource); preceding.Portrait |= portrait; return; }
                    // SourcePath/override validation already confines ordinary
                    // sources. The external face root is the existing explicit
                    // ADD_FACE owner, never its Documents or workspace parent.
                    string root = AtOrBelow(path, originalRoot) ? originalRoot :
                        overrideRoot is not null && AtOrBelow(path, overrideRoot) ? overrideRoot :
                        portrait && portraitRoot is not null && AtOrBelow(path, portraitRoot) ? portraitRoot :
                        throw new InvalidDataException("Resource source has no configured notification root: " + path);
                    root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
                    var target = new Target { Portrait = portrait }; target.Resources.Add(resource); targets.Add(path, target);
                    try { if (roots.Add(root)) BindRoots(); }
                    catch (Exception error) { failure = error; throw new IOException("Cannot observe resource source: " + path, error); }
                }
            }
            private DirectoryWatch Watch(string directory, bool recursive)
            {
                if (watches.TryGetValue(directory, out var retained))
                {
                    if (retained.Recursive != recursive) throw new InvalidOperationException("Conflicting resource notification root.");
                    return retained;
                }
                var watcher = new FileSystemWatcher(directory)
                {
                    IncludeSubdirectories = recursive,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size
                };
                var watch = new DirectoryWatch(directory, recursive, watcher); watches.Add(directory, watch);
                watcher.Changed += (_, item) => Changed(watch, item.FullPath, null, directoryBoundary: false);
                watcher.Created += (_, item) => Changed(watch, item.FullPath, null, directoryBoundary: true);
                watcher.Deleted += (_, item) => Changed(watch, item.FullPath, null, directoryBoundary: true);
                watcher.Renamed += (_, item) => Changed(watch, item.FullPath, item.OldFullPath, directoryBoundary: true);
                watcher.Error += (_, item) => Failed(watch, item.GetException());
                try
                {
                    watch.Activating = true;
                    watcher.EnableRaisingEvents = true;
                    if (watch.ActivationError is { } error)
                        throw new IOException("Cannot activate resource source notifications: " + directory, error);
                }
                catch { watches.Remove(directory); watcher.Dispose(); throw; }
                finally { watch.Activating = false; }
                return watch;
            }
            private void BindRoots()
            {
                var requested = roots.ToArray();
                // Nested configured roots are already covered by their outer
                // actual resource root; a second child handle would once again
                // prevent a legal move of a directory inside that root.
                var outerRoots = requested.Where(root => !requested.Any(other =>
                    !StringComparer.OrdinalIgnoreCase.Equals(root, other) && AtOrBelow(root, other))).ToArray();
                var needed = new Dictionary<string, (bool Recursive, HashSet<string> Boundaries)>(StringComparer.OrdinalIgnoreCase);
                void Need(string directory, bool recursive, string? boundary = null)
                {
                    if (!needed.TryGetValue(directory, out var request)) request = (recursive, new(StringComparer.OrdinalIgnoreCase));
                    if (recursive) request.Recursive = true;
                    if (boundary is not null) request.Boundaries.Add(boundary);
                    needed[directory] = request;
                }
                foreach (string root in outerRoots)
                {
                    if (System.IO.Directory.Exists(root))
                    {
                        Need(root, recursive: true);
                        string parent = Path.GetDirectoryName(root) ?? throw new IOException("Resource root has no parent boundary: " + root);
                        Need(parent, recursive: false, root);
                    }
                    else
                    {
                        // Missing override/ADD_FACE roots are observed through
                        // only the next exact child of the nearest existing
                        // parent. Creation moves this boundary inward; no broad
                        // recursive ancestor watch or per-page polling occurs.
                        string child = root;
                        string parent = Path.GetDirectoryName(child) ?? throw new DirectoryNotFoundException(root);
                        while (!System.IO.Directory.Exists(parent))
                        { child = parent; parent = Path.GetDirectoryName(child) ?? throw new DirectoryNotFoundException(root); }
                        Need(parent, recursive: false, child);
                    }
                }
                foreach (var watch in watches.Values.ToArray())
                    if (!needed.TryGetValue(watch.Directory, out var request) || request.Recursive != watch.Recursive)
                    { watches.Remove(watch.Directory); watch.Watcher.Dispose(); }
                foreach (var (directory, request) in needed)
                {
                    var watch = Watch(directory, request.Recursive); watch.Boundaries.Clear();
                    watch.Boundaries.UnionWith(request.Boundaries);
                }
            }
            private bool IsCurrent(DirectoryWatch watch) => !disposed &&
                watches.TryGetValue(watch.Directory, out var current) && ReferenceEquals(current, watch);
            private static bool AtOrBelow(string file, string directory) =>
                StringComparer.OrdinalIgnoreCase.Equals(file, directory) ||
                file.StartsWith(Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase);

            private void Changed(DirectoryWatch watch, string physicalPath, string? oldPath, bool directoryBoundary)
            {
                var resources = new HashSet<GameResourceId>(ResourceIdComparer.Instance); bool portrait = false, reset = false;
                lock (gate)
                {
                    if (!IsCurrent(watch)) return;
                    void Add(Target target) { resources.UnionWith(target.Resources); portrait |= target.Portrait; }
                    string path = Path.GetFullPath(physicalPath);
                    string? old = oldPath is null ? null : Path.GetFullPath(oldPath);
                    if (targets.TryGetValue(path, out var target)) Add(target);
                    if (old is not null && targets.TryGetValue(old, out target)) Add(target);
                    bool rootBoundary = directoryBoundary &&
                        (watch.Boundaries.Contains(path) || old is not null && watch.Boundaries.Contains(old));
                    if (directoryBoundary && (watch.Recursive || rootBoundary))
                        foreach (var (file, dependency) in targets)
                            if (AtOrBelow(file, path) || old is not null && AtOrBelow(file, old)) Add(dependency);
                    if (rootBoundary)
                    {
                        try
                        {
                            // Root handles can follow a renamed old tree. Only
                            // the affected roots are reopened; healthy unrelated
                            // roots keep observing their own file changes.
                            foreach (var current in watches.Values.ToArray())
                                if (current.Recursive && (AtOrBelow(current.Directory, path) || old is not null && AtOrBelow(current.Directory, old)))
                                { watches.Remove(current.Directory); current.Watcher.Dispose(); }
                            BindRoots();
                        }
                        catch (Exception error) { failure = error; reset = true; }
                    }
                }
                // Never enter a resident slot while holding the observer gate.
                if (reset) uncertain();
                else if (resources.Count != 0) changed(resources, portrait);
            }
            private void Failed(DirectoryWatch watch, Exception error)
            {
                lock (gate)
                {
                    if (!IsCurrent(watch)) return;
                    if (watch.Activating) { watch.ActivationError = error; return; }
                    try
                    {
                        var previous = watches.Values.ToArray(); watches.Clear();
                        foreach (var current in previous) current.Watcher.Dispose();
                        BindRoots();
                    }
                    catch (Exception restart) { failure = new AggregateException(error, restart); }
                }
                uncertain();
            }
            public void Dispose()
            {
                lock (gate)
                {
                    if (disposed) return; disposed = true;
                    foreach (var watch in watches.Values) watch.Watcher.Dispose();
                    watches.Clear(); targets.Clear(); roots.Clear();
                }
            }
        }

    }
}
