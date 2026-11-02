using System.Collections.Concurrent;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class GameResourceRepository
{
    private readonly ResidentSourceCache _residentSources;
    private long _observedResidentRevision;
    private readonly Dictionary<string, ResidentArchive> _archiveGenerations =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates an exclusive reader and provenance ledger borrowing this
    /// process resource owner's resident source snapshots. A scope is not a
    /// concurrently writable repository and owns no World or session.</summary>
    public GameResourceRepository CreateReadScope(Action<ResourceReadGeneration>? readGeneration = null)
    {
        string? portraitDirectory = _userPortraitDirectory;
        var entitlements = _portraitEntitlements;
        return new(SourceDirectory, OverrideDirectory, readGeneration, _residentSources)
        {
            _userPortraitDirectory = portraitDirectory,
            _portraitEntitlements = entitlements
        };
    }

    /// <summary>Changes only when a retained source is replaced or explicitly
    /// invalidated. Comparing it reads one scalar, without scanning resources.</summary>
    public long ResourceRevision => _residentSources.Revision;

    public bool SharesResidentSourcesWith(GameResourceRepository other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return ReferenceEquals(_residentSources, other._residentSources);
    }

    /// <summary>The actual resource replacement owner calls this even when it
    /// preserves size and timestamps. Previously returned snapshots remain
    /// valid borrows; subsequent reads select the replacement generation.</summary>
    public void Invalidate(GameResourceId resource)
    {
        if (resource.Kind is not ("file" or "rdb"))
            throw new ArgumentException("Only file and RDB sources have resident generations.", nameof(resource));
        _residentSources.Invalidate(resource);
        SynchronizeResidentInvalidation();
    }

    // This finite reset happens once after an actual source-generation change,
    // not on cache hits or frames. Scope-local parsed views must not hide a
    // same-size/time replacement behind their earlier source revision.
    private void SynchronizeResidentInvalidation()
    {
        long revision = _residentSources.Revision;
        if (_observedResidentRevision == revision) return;
        _observedResidentRevision = revision;
        _indices.Clear(); _assetRecords.Clear(); _archiveGenerations.Clear();
        _spriteDirectories.Clear(); _scenarioDocuments = null; _effectiveScenarios = null;
        _originalAiResources.Clear(); _originalAiScripts.Clear(); _originalAiPolicies.Clear();
        _userPortraits = null; _portraitContent = null; _specialPersonCapabilities = null;
        _portraitLinkBins.Clear(); _portraitLinkBinTextures.Clear(); _portraitResourceVersion++;
        // ReadFiles intentionally retains this scope's actual provenance; it
        // is neither the resident cache's inventory nor another reader's ledger.
    }

    private ResidentFile ReadResidentFile(string relative)
    {
        SynchronizeResidentInvalidation();
        GameResourceId id = GameResourceId.File(relative);
        if (HasResourceChangeNotifications && _residentSources.TryReadFile(id, out var retained))
        {
            ObserveResidentRead(retained!.Generation);
            _readFiles[id.ToString()] = retained.Source;
            return retained;
        }
        string fallback = SourcePath(relative);
        var generation = BeginResourceRead(id, fallback, _overrides.Find(id) ?? fallback);
        TrackResourceSource(generation);
        var snapshot = _residentSources.ReadFile(this, generation, relative);
        ObserveResidentRead(snapshot.Generation);
        _readFiles[id.ToString()] = snapshot.Source;
        return snapshot;
    }

    private void ObserveResidentRead(ResourceReadGeneration generation)
    {
        // A cache hit publishes the retained dependency's actual identity,
        // never a fabricated new read or a different scope's entire ledger.
        if (_readGeneration is null)
        {
            if (HasResourceChangeNotifications) VerifyResourceVersion(generation);
            else VerifyResourceRead(generation);
        }
        else CompleteResourceRead(generation);
    }

    private sealed record ResidentFile(byte[] Bytes, SourceProvenance Source,
        ResourceReadGeneration Generation);
    private sealed record ResidentArchive(ResidentFile Source, RdbIndex Index,
        IReadOnlyDictionary<uint, RdbRecord> Records);
    private sealed record ResidentPayload(ResidentArchive Archive,
        ResourceReadGeneration Generation, LoadedRdbAsset Source);

    private sealed partial class ResidentSourceCache
    {
        // Logical file/archive names resolve to Windows paths. Match the
        // repository's existing case-insensitive cache and invalidate contract
        // without rewriting the actual URI recorded in source provenance.
        private sealed class ResourceIdComparer : IEqualityComparer<GameResourceId>
        {
            internal static readonly ResourceIdComparer Instance = new();
            public bool Equals(GameResourceId left, GameResourceId right) =>
                StringComparer.OrdinalIgnoreCase.Equals(left.Kind, right.Kind) &&
                StringComparer.OrdinalIgnoreCase.Equals(left.Key, right.Key);
            public int GetHashCode(GameResourceId value) => HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.Kind),
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.Key));
        }
        private sealed class Slot
        {
            internal readonly object Gate = new();
            internal long Version;
            internal ResidentFile? File;
            internal ResidentArchive? Archive;
            internal ResidentPayload? Payload;
        }
        private static bool SameGeneration(ResourceReadGeneration left, ResourceReadGeneration right) =>
            ResourceIdComparer.Instance.Equals(left.Id, right.Id) &&
            StringComparer.OrdinalIgnoreCase.Equals(left.FallbackPath, right.FallbackPath) &&
            StringComparer.OrdinalIgnoreCase.Equals(left.SelectedPath, right.SelectedPath) &&
            left.Length == right.Length && left.LastWriteUtcTicks == right.LastWriteUtcTicks &&
            left.Version == right.Version;
        private readonly ConcurrentDictionary<GameResourceId, Slot> slots = new(ResourceIdComparer.Instance);
        // Explicit update entry points invalidate their actual typed source
        // closure, including files first read by another scope of this owner.
        internal readonly ConcurrentDictionary<GameResourceId, byte> OriginalAiBehaviorSources = new(ResourceIdComparer.Instance);
        internal readonly ConcurrentDictionary<GameResourceId, byte> OriginalAiScriptSources = new(ResourceIdComparer.Instance);
        internal readonly ConcurrentDictionary<GameResourceId, byte> PortraitMetadataSources = new(ResourceIdComparer.Instance);
        internal readonly ResidentUiSourceCache Ui = new();
        private long revision;
        internal long Revision { get { RequireChangeObserver(); return Interlocked.Read(ref revision); } }
        private Slot For(GameResourceId id) => slots.GetOrAdd(id, static _ => new());
        internal long Version(GameResourceId id)
        {
            RequireChangeObserver();
            var slot = For(id);
            lock (slot.Gate) return slot.Version;
        }
        internal void Invalidate(GameResourceId id)
        {
            var slot = For(id);
            lock (slot.Gate)
            {
                slot.Version++;
                slot.File = null; slot.Archive = null; slot.Payload = null;
                Interlocked.Increment(ref revision);
            }
        }

        internal bool TryReadFile(GameResourceId id, out ResidentFile? source)
        {
            var slot = For(id);
            lock (slot.Gate)
            {
                source = slot.File;
                return source is not null && source.Generation.Version == slot.Version;
            }
        }

        internal bool TryReadPayload(GameResourceId id, ResidentArchive archive, out ResidentPayload? payload)
        {
            var slot = For(id);
            lock (slot.Gate)
            {
                payload = slot.Payload;
                return payload is not null && payload.Generation.Version == slot.Version &&
                    ReferenceEquals(payload.Archive, archive);
            }
        }

        internal ResidentFile ReadFile(GameResourceRepository scope,
            ResourceReadGeneration generation, string relative)
        {
            var slot = For(generation.Id);
            lock (slot.Gate)
            {
                if (slot.File is { } cached && SameGeneration(cached.Generation, generation)) return cached;
                if (slot.File is not null) Interlocked.Increment(ref revision);
                slot.File = null; slot.Archive = null;
                scope.VerifyResourceRead(generation);
                byte[] bytes;
                try { bytes = File.ReadAllBytes(generation.SelectedPath); }
                catch { scope.VerifyResourceRead(generation); throw; }
                scope.VerifyResourceRead(generation);
                var source = new SourceProvenance(relative, bytes.LongLength,
                    Convert.ToHexString(SHA256.HashData(bytes)))
                {
                    ResourceUri = generation.Id.ToString(), PhysicalPath = generation.SelectedPath,
                    IsOverride = scope._overrides.Find(generation.Id) is not null
                };
                return slot.File = new(bytes, source, generation);
            }
        }

        internal ResidentArchive ReadArchive(GameResourceRepository scope, ResidentFile source)
        {
            var slot = For(source.Generation.Id);
            lock (slot.Gate)
            {
                if (!ReferenceEquals(slot.File, source))
                    throw new ResourceReadGenerationChangedException(source.Generation.Id);
                if (slot.Archive is { } cached) return cached;
                // Parse failures are never published as a successful cache or
                // replaced by an older/installed resource generation.
                var index = RdbIndexReader.Read(source.Bytes);
                scope.VerifyResourceRead(source.Generation);
                return slot.Archive = new(source, index,
                    index.Records.ToDictionary(record => record.FileId));
            }
        }

        internal LoadedRdbAsset ReadPayload(GameResourceRepository scope, ResidentArchive archive,
            RdbRecord record, ResourceReadGeneration generation, string indexPath, string containerRelative)
        {
            var slot = For(generation.Id);
            lock (slot.Gate)
            {
                if (slot.Payload is { } cached && ReferenceEquals(cached.Archive, archive) &&
                    SameGeneration(cached.Generation, generation)) return cached.Source;
                if (slot.Payload is { } previous && !SameGeneration(previous.Generation, generation))
                    Interlocked.Increment(ref revision);
                slot.Payload = null;
                scope.VerifyResourceRead(archive.Source.Generation);
                scope.VerifyResourceRead(generation);
                bool replaced = scope._overrides.Find(generation.Id) is not null;
                byte[] decoded;
                try
                {
                    decoded = replaced ? File.ReadAllBytes(generation.SelectedPath)
                        : RdbArchiveReader.ReadPayload(indexPath, archive.Index, record);
                }
                catch
                {
                    scope.VerifyResourceRead(archive.Source.Generation);
                    scope.VerifyResourceRead(generation); throw;
                }
                scope.VerifyResourceRead(archive.Source.Generation);
                scope.VerifyResourceRead(generation);
                string hash = Convert.ToHexString(SHA256.HashData(decoded));
                SourceProvenance? replacement = replaced ? new(generation.Id.OverrideRelativePath,
                    decoded.LongLength, hash)
                {
                    ResourceUri = generation.Id.ToString(), PhysicalPath = generation.SelectedPath, IsOverride = true
                } : null;
                var source = new LoadedRdbAsset(decoded, generation.Id.ToString(), containerRelative,
                    record.FileId, hash, archive.Source.Source, replacement) { LoaderMetadata = record.LoaderMetadata };
                slot.Payload = new(archive, generation, source);
                return source;
            }
        }
    }
}
