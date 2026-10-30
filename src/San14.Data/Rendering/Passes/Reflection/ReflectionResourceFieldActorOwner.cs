using System.Buffers.Binary;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

public sealed record ReflectionResourceFieldActor(ReflectionSceneRootReference SourceReference,
    OwnedWorldData Data, ReflectionCaptureFieldInstance Field);
public readonly record struct ReflectionResourceFieldAppendResult(int Attempts, int Appended);

/// <summary>Field CPUs belonging to one actual loaded Scene root generation.
/// The root's existing Data owner supplies identities once; current spatial
/// traversal supplies publication order. Other dynamic/action-created Fields
/// remain the responsibility of their actual creators and are not declared absent.</summary>
public sealed class ReflectionResourceFieldActorOwner : IDisposable
{
    private readonly OwnedWorldLists lists;
    private readonly Dictionary<ReflectionSceneRootReference, ReflectionResourceFieldActor> byReference =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<OwnedWorldData, ReflectionCaptureFieldInstance> byData =
        new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<ReflectionSceneRootReference> sourceReferences;
    private bool disposed;

    public ReflectionResourceFieldActorOwner(LoadedReflectionSceneRootSource actualRoot,
        OwnedWorldLists actualLists,
        IReadOnlyDictionary<ReflectionSceneRootReference, OwnedWorldData> actualCurrentData)
    {
        ArgumentNullException.ThrowIfNull(actualRoot);
        ArgumentNullException.ThrowIfNull(actualLists);
        ArgumentNullException.ThrowIfNull(actualCurrentData);
        Source = actualRoot; lists = actualLists;
        sourceReferences = new(actualRoot.SourceReferences, ReferenceEqualityComparer.Instance);
        try
        {
            foreach (var reference in actualRoot.SourceReferences)
            {
                if (reference.ResolvedDefinition.ClassId != ReflectionCaptureFieldSource.ClassId) continue;
                if (!actualCurrentData.TryGetValue(reference, out var data) || data is null ||
                    !ReferenceEquals(data.Owner, lists))
                    throw new InvalidDataException("Selected root Field requires its actual existing owned Data identity.");
                if (data.CpuState == OwnedWorldCpuState.Null)
                    throw new InvalidOperationException("A retired root Data cannot be reused for a new resource Field generation.");
                if (!byData.TryGetValue(data, out var field))
                {
                    field = new(ReadResolvedField(reference));
                    byData.Add(data, field);
                    lists.SetCpuState(data, OwnedWorldCpuState.Present);
                }
                byReference.Add(reference, new(reference, data, field));
            }
            Actors = Array.AsReadOnly(actualRoot.SourceReferences.Where(byReference.ContainsKey)
                .Select(reference => byReference[reference]).ToArray());
        }
        catch { Dispose(); throw; }
    }

    public LoadedReflectionSceneRootSource Source { get; }
    /// <summary>Selected resource children only, in their declared reference
    /// order. This is not a complete current Scene or dynamic Field roster.</summary>
    public IReadOnlyList<ReflectionResourceFieldActor> Actors { get; }

    /// <summary>Invoke the actual reached Field World.v30 callback. Appending
    /// a Data or discovering its declaration does not implicitly run v30.</summary>
    public void InvokeCurrentWorldCallback(LoadedReflectionSceneRootSource actualRoot,
        ReflectionSceneRootReference actualReference)
    {
        RequireCurrent(actualRoot, actualReference);
        if (byReference.TryGetValue(actualReference, out var actor)) actor.Field.RegisterForCurrentViews();
    }

    /// <summary>Publish the actual current Octree traversal's selected Fields
    /// into its explicitly resolved List destination. Each v8 attempt is kept
    /// separate: aliases/repeats retain identity and later attempts continue
    /// after allocation failure. This does not clear or advance another owner's List.</summary>
    public ReflectionResourceFieldAppendResult AppendCurrent(LoadedReflectionSceneRootSource actualRoot,
        long expectedFrame, uint actualDestinationListName,
        IReadOnlyList<ReflectionSceneRootReference> actualCurrentTraversal)
    {
        ArgumentNullException.ThrowIfNull(actualCurrentTraversal);
        RequireCurrent(actualRoot);
        int attempts = 0, appended = 0;
        if (byReference.Count == 0) return new(0, 0);
        foreach (var reference in actualCurrentTraversal)
        {
            RequireCurrent(actualRoot, reference);
            if (!byReference.TryGetValue(reference, out var actor)) continue;
            actor.Field.RequireCurrent();
            if (actor.Data.CpuState != OwnedWorldCpuState.Present)
                throw new InvalidOperationException("Current root Field Data no longer owns its constructed CPU.");
            attempts++;
            if (lists.Append(expectedFrame, actualDestinationListName, new OwnedWorldData?[] { actor.Data })) appended++;
        }
        return new(attempts, appended);
    }

    public bool TryGetField(OwnedWorldData actualData, out ReflectionCaptureFieldInstance? field)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(actualData);
        return byData.TryGetValue(actualData, out field);
    }

    /// <summary>The outer root owner calls this after current borrows finish.
    /// List references retain stale Data until their real clear/remove writer;
    /// replacing a root does not delete unrelated or aliased List entries.</summary>
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var pair in byData)
        {
            pair.Value.Dispose();
            lists.SetCpuState(pair.Key, OwnedWorldCpuState.Null);
        }
    }

    private void RequireCurrent(LoadedReflectionSceneRootSource actualRoot,
        ReflectionSceneRootReference? reference = null)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!ReferenceEquals(Source, actualRoot))
            throw new InvalidOperationException("Field actors belong to a different loaded Scene root generation.");
        if (reference is not null && !sourceReferences.Contains(reference))
            throw new InvalidOperationException("Current traversal contains a foreign Scene root reference.");
    }

    /// <summary>Read the actual selected root child and its typed RDOK overrides.</summary>
    public static ReflectionCaptureFieldDefinition ReadResolvedField(ReflectionSceneRootReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var definition = ReflectionCaptureFieldSource.Read(reference.DefinitionDocument,
            reference.ResolvedDefinition.ObjectId);
        if (reference.Instance.Kind == DokObjectKind.Inline) return definition;
        if (reference.Instance.Kind != DokObjectKind.Reference || reference.Instance.Reference is not { } link ||
            link.ResourceId != reference.DefinitionDocument.Source.FileId ||
            link.ObjectId != reference.ResolvedDefinition.ObjectId)
            throw new InvalidDataException("Root Field requires its actual one-level RDOK definition join.");
        DokProperty? Override(uint hash, uint type, int count)
        {
            var fields = reference.Instance.Properties.Where(field => field.NameHash == hash).ToArray();
            if (fields.Length == 0) return null;
            if (fields.Length != 1 || fields[0].TypeCode != type || fields[0].ElementCount != count ||
                fields[0].ElementSize != 4 || fields[0].RawValue.Length != checked(count * 4))
                throw new InvalidDataException($"Root Field override {hash:X8} has invalid source type/count.");
            return fields[0];
        }
        float Float(DokProperty field, int index) => BinaryPrimitives.ReadSingleLittleEndian(field.RawValue.AsSpan(index * 4));
        uint Word(DokProperty field, int index) => BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue.AsSpan(index * 4));
        float Scalar(uint hash, float inherited) => Override(hash, 8, 1) is { } field ? Float(field, 0) : inherited;
        uint Unsigned(uint hash, uint inherited) => Override(hash, 5, 1) is { } field ? Word(field, 0) : inherited;
        var position = Override(0x27D889E5, 8, 3);
        var quaternion = Override(0xF52EA910, 8, 4);
        var dimensions = Override(0xA9A7E56C, 5, 3);
        return definition with
        {
            ObjectId = reference.Instance.ObjectId,
            SourceFileId = reference.InstanceDocument.Source.FileId,
            SourceResourceUri = reference.InstanceDocument.Source.ResourceUri,
            SourceDecodedSha256 = reference.InstanceDocument.Source.DecodedSha256,
            WorldPosition = position is null ? definition.WorldPosition : new(Float(position, 0), Float(position, 1), Float(position, 2)),
            WorldQuaternion = quaternion is null ? definition.WorldQuaternion : new(Float(quaternion, 0), Float(quaternion, 1), Float(quaternion, 2), Float(quaternion, 3)),
            FieldDimension = dimensions is null ? definition.FieldDimension : new(Word(dimensions, 0), Word(dimensions, 1), Word(dimensions, 2)),
            CellSize = Scalar(0x46843CA3, definition.CellSize),
            TileSize = Unsigned(0x4621A92F, definition.TileSize),
            GBufSize = Unsigned(0x4633440D, definition.GBufSize),
            ViewRange = Scalar(0x4929D408, definition.ViewRange),
            GroupMask = Unsigned(0xC0C5A5B5, definition.GroupMask),
            MaxPrimitiveNum = Unsigned(0xDB26951D, definition.MaxPrimitiveNum),
            ProbeFieldShadowMapSize = Unsigned(0x4A4A850D, definition.ProbeFieldShadowMapSize),
        };
    }
}
