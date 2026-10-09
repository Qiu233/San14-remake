using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Canonical declaration-default View0 selection. This is not a current
/// Reference, prepared View, actor roster, dependency-ready or lighting receipt.</summary>
public sealed class LoadedDefaultView0Source
{
    private readonly LoadedRdbAsset viewSource, cameraSource, graphSource;
    internal LoadedDefaultView0Source(LoadedRdbAsset view, LoadedRdbAsset camera, LoadedRdbAsset graph,
        uint viewMask, uint[] cameraLists)
    {
        viewSource = view; cameraSource = camera; graphSource = graph;
        DeclaredViewMask = viewMask; DeclaredCameraListObjectIds = Array.AsReadOnly(cameraLists.ToArray());
        SourceIdentity = $"{graph.ResourceUri}@{graph.DecodedSha256}#node/9;" +
            $"{view.ResourceUri}@{view.DecodedSha256}#object/E1B6A687/slot0;" +
            $"{camera.ResourceUri}@{camera.DecodedSha256}#Reference35A031F4/DB33E498/Main38D04576;declaration-default-only";
    }
    public int DeclaredViewSlot => 0;
    public uint ViewObjectId => DefaultView0Source.ViewObjectId;
    public uint ReferenceCameraObjectId => DefaultView0Source.ReferenceCameraObjectId;
    public uint MainCameraObjectId => DefaultView0Source.MainCameraObjectId;
    public uint ViewParameterObjectId => DefaultView0Source.ViewParameterObjectId;
    public int TaskNodeIndex => 9;
    public uint TaskFactoryClassId => 0xAE91CA0F;
    public uint TaskLookupKey => 0x4963672A;
    public uint DeclaredViewMask { get; }
    public IReadOnlyList<uint> DeclaredCameraListObjectIds { get; }
    public uint DeclaredOptionalDependencyFileHash => 0x22704EBB;
    public string SourceIdentity { get; }

    /// <summary>Independent canonical Main declaration and audited construction
    /// words selected through View0's Reference. Each result owns its arrays.</summary>
    public LoadedMapCameraConstructionSource MainCamera =>
        MapCameraConstructionSource.ReadPerspective(CopyCameraDefinitions(), MainCameraObjectId);
    public DokObject CopyViewParameterDeclaration() => CopyCameraDefinitions().Document.Objects.Single(
        value => value.ObjectId == ViewParameterObjectId);
    public LoadedObjects CopyViewDefinitions() => CopyObjects(viewSource);
    public LoadedObjects CopyCameraDefinitions() => CopyObjects(cameraSource);
    public LoadedTaskGraph CopyTaskGraph()
    {
        LoadedRdbAsset copy = graphSource with { Bytes = graphSource.Bytes.ToArray() };
        return new(TaskGraphReader.Read(copy.Bytes), copy);
    }
    private static LoadedObjects CopyObjects(LoadedRdbAsset source)
    {
        LoadedRdbAsset copy = source with { Bytes = source.Bytes.ToArray() };
        return new(DokReader.Read(copy.Bytes), copy);
    }
}

/// <summary>Reads the exact original TGK node9 and the declaration-default E1
/// View0 -> native Reference35A -> Main38D chain. Reference is an inline native
/// class7BC602DD, not RDOK inheritance. Current retargeting needs another owner.</summary>
public static class DefaultView0Source
{
    public const uint ViewResourceId = 0x62550860;
    public const string ViewResourceSha256 = "59A0A5DB85C7B4A8B9FEB940F8EE40AC46E93A6BC28ED3FA223177C850279E4E";
    public const uint CameraResourceId = 0x908AB6E2;
    public const uint TaskGraphResourceId = 0xBD13A52E;
    public const string TaskGraphResourceSha256 = "470BA20ADB71D936AD3C41ABE2FF7C21BBD6CCF4D8CD707BFF53355840F6482B";
    public const uint ViewObjectId = 0xE1B6A687;
    public const uint ReferenceCameraObjectId = 0x35A031F4;
    public const uint MainCameraObjectId = 0x38D04576;
    public const uint ViewParameterObjectId = 0xBA42DABB;

    public static LoadedDefaultView0Source LoadDeclarationDefaults(GameResourceRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return ReadDeclarationDefaults(repository.LoadObjects("system", ViewResourceId),
            repository.LoadObjects("system", CameraResourceId), repository.LoadTaskGraph("system", TaskGraphResourceId));
    }

    public static LoadedDefaultView0Source ReadDeclarationDefaults(LoadedObjects viewDefinitions,
        LoadedObjects cameraDefinitions, LoadedTaskGraph taskGraph)
    {
        ArgumentNullException.ThrowIfNull(viewDefinitions); ArgumentNullException.ThrowIfNull(cameraDefinitions);
        ArgumentNullException.ThrowIfNull(taskGraph);
        LoadedRdbAsset viewSource = Snapshot(viewDefinitions.Source, ViewResourceId, ViewResourceSha256);
        LoadedRdbAsset cameraSource = Snapshot(cameraDefinitions.Source, CameraResourceId,
            MapCameraConstructionSource.SourceSha256);
        LoadedRdbAsset graphSource = Snapshot(taskGraph.Source, TaskGraphResourceId, TaskGraphResourceSha256);
        DokDocument view = DokReader.Read(viewSource.Bytes), cameras = DokReader.Read(cameraSource.Bytes);
        CompiledGraphDocument graph = TaskGraphReader.Read(graphSource.Bytes);
        RequireMetadata(viewDefinitions.Document, view, "View DOK");
        RequireMetadata(cameraDefinitions.Document, cameras, "Camera DOK");
        RequireMetadata(taskGraph.Document, graph, "TGK");

        DokObject declaredView = Object(view, ViewObjectId, 0x97AF26FD, 52);
        DokProperty viewParameters = Property(declaredView, 0, 0x03464480, 5, 9, 4);
        DokProperty declaredCameras = Property(declaredView, 1, 0xB43036BD, 5, 9, 4);
        if (Word(viewParameters.RawValue, 0) != ViewParameterObjectId ||
            Word(declaredCameras.RawValue, 0) != ReferenceCameraObjectId)
            throw new InvalidDataException("DefaultView0 declaration slot0 differs from its native ViewParam/Reference pair.");
        DokObject reference = Object(cameras, ReferenceCameraObjectId, 0x7BC602DD, 1);
        if (Word(Property(reference, 0, 0xDB33E498, 5, 1, 4).RawValue, 0) != MainCameraObjectId)
            throw new InvalidDataException("DefaultView0 native Reference does not declare Main38D04576.");
        Object(cameras, MainCameraObjectId, MapCameraConstructionSource.PerspectiveCameraClassId, 1111);
        DokObject viewParameter = Object(cameras, ViewParameterObjectId, 0xB5EA88F0, 7);
        (uint Hash, uint Type, int Count, int Width)[] parameterShapes = [
            (0x4B705FD4, 8, 1, 4), (0x243BC6CC, 5, 1, 4), (0x5D9AD671, 5, 1, 4),
            (0x1BA8C540, 5, 1, 4), (0xDD5AF9BB, 1, 15, 1),
            (0xB5467DC8, 1, 15, 1), (0x8A96C17A, 1, 15, 1) ];
        for (int i = 0; i < parameterShapes.Length; i++)
        {
            var expected = parameterShapes[i];
            Property(viewParameter, i, expected.Hash, expected.Type, expected.Count, expected.Width);
        }
        if (graph.GraphKind != CompiledGraphKind.TaskGraph || graph.Nodes.Count != 27 ||
            graph.DependencyFileHash != 0x22704EBB || graph.DeclaredTotalBytes != 3048)
            throw new InvalidDataException("DefaultView0 requires its exact declared TGK and optional dependency identity.");
        CompiledGraphNode node = graph.Nodes[9]; TaskGraphCallMetadata call = node.Call;
        if (node.Index != 9 || node.SourceOffset != 1176 || node.DeclaredBytes != 164 ||
            call.FactoryClassId != 0xAE91CA0F || call.LookupKey != 0x4963672A || call.RawWord14 != 0 ||
            !call.ConnectedNodeIndices.SequenceEqual(new uint[] { 10, 11, 12, 13, 14, 15 }) ||
            call.Arguments.Count != 3 || call.UninterpretedTrailingBytes != 0)
            throw new InvalidDataException("DefaultView0 requires the actual node9/classAE91CA0F callback declaration.");
        TaskGraphArgument key = Argument(call, 0, 0x72DB7EBD, 1);
        TaskGraphArgument mask = Argument(call, 1, 0xACF79102, 1);
        TaskGraphArgument lists = Argument(call, 2, 0x101C73E5, 16);
        uint[] listValues = Enumerable.Range(0, 16).Select(i => Word(lists.RawValue, i)).ToArray();
        uint[] expectedLists = [0, 0, 0, 0x27FABDE8, 0x7F18AEC7, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
        if (Word(key.RawValue, 0) != ViewObjectId || Word(mask.RawValue, 0) != 0xFFFF003F ||
            !listValues.SequenceEqual(expectedLists))
            throw new InvalidDataException("DefaultView0 node9 source View/mask/camera-list inputs changed.");
        // Validate the existing construction adapter now, so this packet cannot
        // defer an incompatible Camera declaration failure to the caller.
        MapCameraConstructionSource.ReadPerspective(new(cameras, cameraSource), MainCameraObjectId);
        return new(viewSource, cameraSource, graphSource, Word(mask.RawValue, 0), listValues);
    }

    private static LoadedRdbAsset Snapshot(LoadedRdbAsset source, uint fileId, string sha)
    {
        string uri = GameResourceId.Rdb("system", fileId).ToString();
        if (source is null || source.Bytes is null || source.FileId != fileId || source.ResourceUri != uri ||
            !string.Equals(source.DecodedSha256, sha, StringComparison.OrdinalIgnoreCase) ||
            source.OverrideSource is not null || source.IndexSource is null || source.IndexSource.IsOverride)
            throw new InvalidDataException("DefaultView0 requires exact original system asset/index identities without overrides.");
        byte[] bytes = source.Bytes.ToArray();
        if (Convert.ToHexString(SHA256.HashData(bytes)) != sha)
            throw new InvalidDataException("DefaultView0 source bytes differ from the pinned SHA256.");
        return source with { Bytes = bytes };
    }
    private static void RequireMetadata<T>(T supplied, T canonical, string kind)
    {
        if (supplied is null || !JsonSerializer.SerializeToUtf8Bytes(supplied).AsSpan()
            .SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(canonical)))
            throw new InvalidDataException($"Supplied DefaultView0 {kind} metadata differs from canonical source bytes.");
    }
    private static DokObject Object(DokDocument document, uint id, uint classId, int properties)
    {
        DokObject[] matches = document.Objects.Where(value => value.ObjectId == id).ToArray();
        if (matches.Length != 1 || matches[0].Kind != DokObjectKind.Inline || matches[0].Reference is not null ||
            matches[0].ClassId != classId || matches[0].Properties.Count != properties)
            throw new InvalidDataException($"DefaultView0 requires unique inline object{id:X8}/class{classId:X8}.");
        return matches[0];
    }
    private static DokProperty Property(DokObject owner, int ordinal, uint hash, uint type, int count, int width)
    {
        DokProperty property = owner.Properties[ordinal];
        if (property.Index != ordinal || property.NameHash != hash || property.TypeCode != type ||
            property.ElementCount != count || property.ElementSize != width || property.RawValue.Length != count * width ||
            owner.Properties.Count(value => value.NameHash == hash) != 1)
            throw new InvalidDataException("DefaultView0 property differs from its exact native declaration binding.");
        return property;
    }
    private static TaskGraphArgument Argument(TaskGraphCallMetadata call, int index, uint hash, uint count)
    {
        TaskGraphArgument argument = call.Arguments[index];
        if (argument.Index != index || argument.NameHash != hash || argument.RawType != 5 ||
            argument.RawElementCount != count || argument.DescriptorWord != (0x05000000u | count) ||
            argument.RawValue.Length != count * 4 || call.Arguments.Count(value => value.NameHash == hash) != 1)
            throw new InvalidDataException("DefaultView0 TGK argument differs from the actual typed node9 literal.");
        return argument;
    }
    private static uint Word(byte[] bytes, int index) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4, 4));
}
