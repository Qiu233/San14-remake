using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Canonical declaration-default View0 selection. This is not a current
/// Reference, prepared View, actor roster, dependency-ready or lighting receipt.</summary>
public sealed class LoadedDefaultView0Source
{
    internal LoadedDefaultView0Source(LoadedObjects view, LoadedObjects camera, LoadedTaskGraph graph,
        DokObject viewParameter, uint viewMask, uint[] cameraLists)
    {
        ViewDefinitions = view;
        CameraDefinitions = camera;
        TaskGraph = graph;
        ViewParameterDeclaration = viewParameter;
        CameraSourceIdentity = (camera.Source.ResourceUri, camera.Source.FileId, camera.Source.DecodedSha256);
        DeclaredViewMask = viewMask;
        DeclaredCameraListObjectIds = Array.AsReadOnly(cameraLists.ToArray());
        SourceIdentity = $"{graph.Source.ResourceUri}@{graph.Source.DecodedSha256}#node/9;" +
            $"{view.Source.ResourceUri}@{view.Source.DecodedSha256}#object/E1B6A687/slot0;" +
            $"{camera.Source.ResourceUri}@{camera.Source.DecodedSha256}#Reference35A031F4/DB33E498/Main38D04576;declaration-default-only";
    }
    public LoadedObjects ViewDefinitions { get; }
    public LoadedObjects CameraDefinitions { get; }
    public DokObject ViewParameterDeclaration { get; }
    public LoadedTaskGraph TaskGraph { get; }
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
    internal (string ResourceUri, uint FileId, string DecodedSha256) CameraSourceIdentity { get; }
}

/// <summary>Reads the exact original TGK node9 and the declaration-default E1
/// View0 -> native Reference35A -> Main38D chain. Reference is an inline native
/// class7BC602DD, not RDOK inheritance. Current retargeting needs another owner.</summary>
public static class DefaultView0Source
{
    public const uint ViewResourceId = 0x62550860;
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

    public static LoadedDefaultView0Source ReadDeclarationDefaults(LoadedRdbAsset viewDefinitions,
        LoadedRdbAsset cameraDefinitions, LoadedRdbAsset taskGraph)
    {
        ArgumentNullException.ThrowIfNull(viewDefinitions); ArgumentNullException.ThrowIfNull(cameraDefinitions);
        ArgumentNullException.ThrowIfNull(taskGraph);
        return ReadDeclarationDefaults(new LoadedObjects(DokReader.Read(viewDefinitions.Bytes), viewDefinitions),
            new LoadedObjects(DokReader.Read(cameraDefinitions.Bytes), cameraDefinitions),
            new LoadedTaskGraph(TaskGraphReader.Read(taskGraph.Bytes), taskGraph));
    }

    /// <summary>Imports one declaration generation. All consumers of this owner
    /// share its parsed documents; loading again imports a new resource generation.</summary>
    public static LoadedDefaultView0Source ReadDeclarationDefaults(LoadedObjects viewDefinitions,
        LoadedObjects cameraDefinitions, LoadedTaskGraph taskGraph)
    {
        ArgumentNullException.ThrowIfNull(viewDefinitions);
        ArgumentNullException.ThrowIfNull(cameraDefinitions);
        ArgumentNullException.ThrowIfNull(taskGraph);
        RequireLibrary(viewDefinitions.Source, ViewResourceId);
        RequireLibrary(cameraDefinitions.Source, CameraResourceId);
        RequireLibrary(taskGraph.Source, TaskGraphResourceId);
        DokDocument view = viewDefinitions.Document, cameras = cameraDefinitions.Document;
        CompiledGraphDocument graph = taskGraph.Document;

        DokObject declaredView = Object(view, ViewObjectId, 0x97AF26FD);
        DokProperty viewParameters = Property(declaredView, 0x03464480, 5, 9, 4);
        DokProperty declaredCameras = Property(declaredView, 0xB43036BD, 5, 9, 4);
        if (Word(viewParameters.RawValue, 0) != ViewParameterObjectId ||
            Word(declaredCameras.RawValue, 0) != ReferenceCameraObjectId)
            throw new InvalidDataException("DefaultView0 declaration slot0 differs from its native ViewParam/Reference pair.");
        DokObject reference = Object(cameras, ReferenceCameraObjectId, 0x7BC602DD);
        if (Word(Property(reference, 0xDB33E498, 5, 1, 4).RawValue, 0) != MainCameraObjectId)
            throw new InvalidDataException("DefaultView0 native Reference does not declare Main38D04576.");
        DokObject viewParameter = Object(cameras, ViewParameterObjectId, 0xB5EA88F0);
        (uint Hash, uint Type, int Count, int Width)[] parameterShapes = [
            (0x4B705FD4, 8, 1, 4), (0x243BC6CC, 5, 1, 4), (0x5D9AD671, 5, 1, 4),
            (0x1BA8C540, 5, 1, 4), (0xDD5AF9BB, 1, 15, 1),
            (0xB5467DC8, 1, 15, 1), (0x8A96C17A, 1, 15, 1) ];
        for (int i = 0; i < parameterShapes.Length; i++)
        {
            var expected = parameterShapes[i];
            Property(viewParameter, expected.Hash, expected.Type, expected.Count, expected.Width);
        }
        if (graph.GraphKind != CompiledGraphKind.TaskGraph || graph.Nodes.Count <= 15 ||
            graph.DependencyFileHash != 0x22704EBB)
            throw new InvalidDataException("DefaultView0 requires its exact declared TGK and optional dependency identity.");
        CompiledGraphNode node = graph.Nodes[9]; TaskGraphCallMetadata call = node.Call;
        if (node.Index != 9 || call.FactoryClassId != 0xAE91CA0F || call.LookupKey != 0x4963672A || call.RawWord14 != 0 ||
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
        // Construction and View selection use the same decoded declaration.
        MapCameraConstructionSource.ReadPerspective(cameraDefinitions, MainCameraObjectId);
        return new(viewDefinitions, cameraDefinitions, taskGraph, viewParameter, Word(mask.RawValue, 0), listValues);
    }

    private static void RequireLibrary(LoadedRdbAsset source, uint fileId)
    {
        if (source.FileId != fileId || source.ResourceUri != GameResourceId.Rdb("system", fileId).ToString())
            throw new InvalidDataException("DefaultView0 requires its supported system declaration library.");
    }
    private static DokObject Object(DokDocument document, uint id, uint classId)
    {
        DokObject[] matches = document.Objects.Where(value => value.ObjectId == id).ToArray();
        if (matches.Length != 1 || matches[0].Kind != DokObjectKind.Inline || matches[0].Reference is not null ||
            matches[0].ClassId != classId)
            throw new InvalidDataException($"DefaultView0 requires unique inline object{id:X8}/class{classId:X8}.");
        return matches[0];
    }
    private static DokProperty Property(DokObject owner, uint hash, uint type, int count, int width)
    {
        var fields = owner.Properties.Where(field => field.NameHash == hash).ToArray();
        if (fields.Length != 1)
            throw new InvalidDataException("DefaultView0 requires a unique declared property.");
        DokProperty property = fields[0];
        if (property.TypeCode != type ||
            property.ElementCount != count || property.ElementSize != width || property.RawValue.Length != count * width)
            throw new InvalidDataException("DefaultView0 property differs from its exact native declaration binding.");
        return property;
    }
    private static TaskGraphArgument Argument(TaskGraphCallMetadata call, int index, uint hash, uint count)
    {
        if ((uint)index >= call.Arguments.Count)
            throw new InvalidDataException("DefaultView0 is missing a required typed task argument.");
        TaskGraphArgument argument = call.Arguments[index];
        if (argument.Index != index || argument.NameHash != hash || argument.RawType != 5 ||
            argument.RawElementCount != count || argument.DescriptorWord != (0x05000000u | count) ||
            argument.RawValue.Length != count * 4 || call.Arguments.Count(value => value.NameHash == hash) != 1)
            throw new InvalidDataException("DefaultView0 TGK argument differs from the actual typed node9 literal.");
        return argument;
    }
    private static uint Word(byte[] bytes, int index) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4, 4));
}
