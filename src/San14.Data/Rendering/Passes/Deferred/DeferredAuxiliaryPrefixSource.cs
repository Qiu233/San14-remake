using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public enum DeferredAuxiliaryPrefixQueryState { RetainedFreshSource, EditedOrImported, Unresolved }
public enum DeferredAuxiliaryPrefixSceneState { ConstructorNullRetained, CachedOrImported, Unresolved }

/// <summary>The actual owned Main RLR query fields initialized from the pinned
/// source declaration and retained through Camera/View cloning. These fields
/// are outside the accepted Camera's3C0 prefix. Their owner must invalidate
/// this scope after a later RLR assignment or an imported Camera generation.</summary>
public sealed class DeferredAuxiliaryPrefixQueryOwner
{
    internal DeferredAuxiliaryPrefixQueryOwner(DeferredAuxiliaryPrefixSource source,
        MapCameraConstructionPacket camera, MapSourceCreatedViewFieldsPacket view,
        string generation, string producer)
    {
        ArgumentNullException.ThrowIfNull(camera); ArgumentNullException.ThrowIfNull(view);
        Source = source; ViewIdentity = view.Binding.ViewIdentity; CameraOwnerIdentity = view.Binding.CameraOwnerIdentity;
        ViewGenerationIdentity = generation; SourceProducerProvenance = producer;
        source.ValidateCamera(camera); view.Camera.Validate(camera);
        if (string.IsNullOrWhiteSpace(generation) || string.IsNullOrWhiteSpace(producer) ||
            string.IsNullOrWhiteSpace(ViewIdentity) || string.IsNullOrWhiteSpace(CameraOwnerIdentity))
            throw new InvalidDataException("Auxiliary RLR query owner requires its actual retained source producer and View generation.");
    }
    public DeferredAuxiliaryPrefixSource Source { get; }
    public string ViewIdentity { get; }
    public string CameraOwnerIdentity { get; }
    public string ViewGenerationIdentity { get; }
    public string SourceProducerProvenance { get; }
    public DeferredAuxiliaryPrefixQueryState State { get; private set; } = DeferredAuxiliaryPrefixQueryState.RetainedFreshSource;
    public long Revision { get; private set; }
    public int RlrMaxRaySamplesE8C => ReadRetained(Source.RlrMaxRaySamples);
    public int RlrResolutionEAC => ReadRetained(Source.RlrResolution);
    private int ReadRetained(int value)
    {
        if (State != DeferredAuxiliaryPrefixQueryState.RetainedFreshSource)
            throw new InvalidDataException("Edited/imported/unresolved RLR fields have no retained source query value.");
        return value;
    }
    public void MarkEditedOrImported(string actualAssignmentProducerProvenance) =>
        Invalidate(DeferredAuxiliaryPrefixQueryState.EditedOrImported, actualAssignmentProducerProvenance);
    public void MarkCurrentQueriesUnresolved(string unresolvedProducerProvenance) =>
        Invalidate(DeferredAuxiliaryPrefixQueryState.Unresolved, unresolvedProducerProvenance);
    private void Invalidate(DeferredAuxiliaryPrefixQueryState state, string producer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(producer); State = state; Revision = checked(Revision + 1);
    }
    internal void Validate(MapSourceCreatedF103CurrentFrame frame, string generation)
    {
        if (State != DeferredAuxiliaryPrefixQueryState.RetainedFreshSource || generation != ViewGenerationIdentity ||
            frame.ViewFields.Binding.ViewIdentity != ViewIdentity || frame.ViewFields.Binding.CameraOwnerIdentity != CameraOwnerIdentity ||
            frame.FrameNumber <= 0 || frame.ViewFields.Camera.FrameNumber != frame.FrameNumber)
            throw new InvalidDataException("Auxiliary no-op requires this retained source RLR query owner, View generation and accepted frame.");
        Source.ValidateCamera(frame.Camera); frame.ViewFields.Camera.Validate(frame.Camera);
    }
}

/// <summary>Finite no-push/no-draw authority for nodes134/135 at one actual
/// current boundary. It preserves the already bound outer attachment; source
/// Begin73 binds064B746B surface0. Earlier graph execution and current colour
/// contents remain the caller's production responsibility.</summary>
public sealed class DeferredAuxiliaryPrefixPacket
{
    private readonly MapCameraConstructionPacket camera;
    private readonly MapSourceCreatedViewFieldsPacket view;
    private readonly long queryRevision, sceneRevision;
    internal DeferredAuxiliaryPrefixPacket(DeferredAuxiliaryPrefixSource source,
        MapSourceCreatedF103CurrentFrame frame, MainMapContextCurrentPacket q,
        DeferredAuxiliaryPrefixQueryOwner queries, string generation)
    {
        Source = source; FrameNumber = frame.FrameNumber; CurrentQ = q; QueryOwner = queries;
        ViewIdentity = queries.ViewIdentity; CameraOwnerIdentity = queries.CameraOwnerIdentity;
        ViewGenerationIdentity = generation; camera = frame.Camera.CopyAcceptedSnapshot();
        view = frame.ViewFields; queryRevision = queries.Revision; sceneRevision = source.SceneRevision;
        CurrentProducerProvenance = queries.SourceProducerProvenance + ";" + frame.ViewFields.SourceProvenance + ";" + q.SourceProvenance;
    }
    public DeferredAuxiliaryPrefixSource Source { get; }
    public long FrameNumber { get; }
    public string ViewIdentity { get; }
    public string CameraOwnerIdentity { get; }
    public string ViewGenerationIdentity { get; }
    public MainMapContextCurrentPacket CurrentQ { get; }
    public DeferredAuxiliaryPrefixQueryOwner QueryOwner { get; }
    public int RlrMaxRaySamplesE8C => QueryOwner.RlrMaxRaySamplesE8C;
    public int RlrResolutionEAC => QueryOwner.RlrResolutionEAC;
    public int SourceResolutionModeC998D0 => RlrResolutionEAC > 0 ? 1 : 0;
    public bool Node134NoFramebufferPushOrDraw => true;
    public bool Node135Skipped => true;
    public bool PreservesExistingOuterAttachment => true;
    public int SourceOuterBeginSceneNode => 73;
    public uint SourceOuterColorObjectId => 0x064B746B;
    public int SourceOuterColorSurface => 0;
    public uint SourceOuterDepthObjectId => 0xE13DDB4F;
    public int SourceOuterDepthSurface => 0;
    public string CurrentProducerProvenance { get; }
    public void Validate(MapSourceCreatedF103CurrentFrame acceptedFrame,
        MainMapContextCurrentPacket actualCurrentQ, string actualViewGenerationIdentity)
    {
        ArgumentNullException.ThrowIfNull(acceptedFrame); ArgumentNullException.ThrowIfNull(actualCurrentQ);
        if (FrameNumber != acceptedFrame.FrameNumber || queryRevision != QueryOwner.Revision || sceneRevision != Source.SceneRevision ||
            !ReferenceEquals(view, acceptedFrame.ViewFields) ||
            !camera.Prefix.Span.SequenceEqual(acceptedFrame.Camera.Prefix.Span) ||
            !ReferenceEquals(CurrentQ.Source, actualCurrentQ.Source) || CurrentQ.FrameNumber != actualCurrentQ.FrameNumber ||
            CurrentQ.Flags3F0And3F4 != actualCurrentQ.Flags3F0And3F4 || CurrentQ.WriterProvenance != actualCurrentQ.WriterProvenance)
            throw new InvalidDataException("Auxiliary prefix packet precedes a later Camera/query/Scene/Q assignment or belongs to another frame.");
        Source.ValidateBoundary(acceptedFrame, actualCurrentQ, QueryOwner, actualViewGenerationIdentity);
    }
}

/// <summary>Fresh original node134 Scene-cache construction plus exact source
/// Main query declarations. A known null cache remains null through these
/// no-op preparations. Cached/imported/unknown Scene history is unsupported.</summary>
public sealed class DeferredAuxiliaryPrefixSource
{
    public const string GraphSha256 = "B9AE1EC8209C067DBC73084CDC0EB149A65E282C25D4B8AD398F43071072971D";
    public const string CameraSha256 = "3CB072E68CA9A2086D0230E3F9F86AD1B9B10E08CEA521A474CE1057A0E23B06";
    public const string NativeNoOp134 = "TC:E6BC31/E6BC41;E9FE85/E9FE94;E9FF27/E9FF54/E9FF56/E9FFAF/E9FFFB;EA06EE/EA06F3->EA0A56";
    public const string NativeNoOp135 = "TC:EB2FBD..EB2FE0;current-Q3F0-signed-nonnegative/bit20-clear";
    private readonly byte[] cameraBytes;
    private DeferredAuxiliaryPrefixSource(byte[] cameraSource, int maxSamples, int resolution, string generation)
    { cameraBytes = cameraSource.ToArray(); RlrMaxRaySamples = maxSamples; RlrResolution = resolution; GraphGenerationIdentity = generation; }
    public int RlrMaxRaySamples { get; }
    public int RlrResolution { get; }
    public string GraphGenerationIdentity { get; }
    public DeferredAuxiliaryPrefixSceneState SceneState { get; private set; } = DeferredAuxiliaryPrefixSceneState.ConstructorNullRetained;
    public long SceneRevision { get; private set; }

    public static DeferredAuxiliaryPrefixSource Read(LoadedRenderGraph graph, LoadedObjects cameraObjects,
        string actualFreshGraphGenerationIdentity)
    {
        ArgumentNullException.ThrowIfNull(graph); ArgumentNullException.ThrowIfNull(cameraObjects);
        ArgumentException.ThrowIfNullOrWhiteSpace(actualFreshGraphGenerationIdentity);
        Exact(graph.Source, "san14://rdb/system/024496EE", 0x024496EE, GraphSha256);
        Exact(cameraObjects.Source, "san14://rdb/system/908AB6E2", 0x908AB6E2, CameraSha256);
        var canonical = RenderGraphReader.Read(graph.Source.Bytes);
        foreach (var item in new (int Index, uint Class, string Sha)[]
        {
            (73, 0xE2C0442A, "35CE6C656D0D1F9B06A03C2E36CA0CCFAEF325FAFCFAB7CCE48789175B2E000E"),
            (134, 0xAC078655, "F414E23719341846350A819FF18A39100766C3D47786364A06C60F7843355920"),
            (135, 0xE1FE052E, "E83D0F29F054784BD2BA08BFE460986650C6982F4C442D77DDFCA65C07B6DAB1")
        })
        {
            var node = canonical.Nodes[item.Index];
            if (node.Call.FactoryClassId != item.Class || Hash(graph.Source.Bytes.AsSpan(node.SourceOffset, node.DeclaredBytes)) != item.Sha)
                throw new InvalidDataException("Auxiliary prefix requires exact original Begin73/RLR134/Merge135 records.");
        }
        var source = MapCameraConstructionSource.ReadPerspective(cameraObjects, 0x38D04576);
        int Property(int ordinal, uint hash, int descriptor, int value)
        {
            var field = source.Camera.Properties[ordinal];
            if (field.Index != ordinal || field.NameHash != hash || field.TypeCode != 4 || field.ElementCount != 1 ||
                field.ElementSize != 4 || field.RawValue.Length != 4 || field.DescriptorOffset != descriptor || field.ValueOffset != value ||
                BinaryPrimitives.ReadInt32LittleEndian(field.RawValue) != 0)
                throw new InvalidDataException("Auxiliary RLR query declaration differs from the exact Main source ordinal/type/value.");
            return BinaryPrimitives.ReadInt32LittleEndian(field.RawValue);
        }
        return new(cameraObjects.Source.Bytes, Property(168, 0x134DEF8B, 40212, 52404),
            Property(320, 0xC91C4DDC, 42036, 53064), actualFreshGraphGenerationIdentity);
    }

    public DeferredAuxiliaryPrefixQueryOwner CreateFreshQueryOwner(MapCameraConstructionPacket acceptedCamera,
        MapSourceCreatedViewFieldsPacket acceptedView, string actualViewGenerationIdentity,
        string actualRetainedSourceQueryProducerProvenance) =>
        new(this, acceptedCamera, acceptedView, actualViewGenerationIdentity, actualRetainedSourceQueryProducerProvenance);

    public void MarkCachedOrImportedSceneHistory(string actualSceneProducerProvenance) =>
        InvalidateScene(DeferredAuxiliaryPrefixSceneState.CachedOrImported, actualSceneProducerProvenance);
    public void MarkCurrentSceneHistoryUnresolved(string unresolvedSceneProducerProvenance) =>
        InvalidateScene(DeferredAuxiliaryPrefixSceneState.Unresolved, unresolvedSceneProducerProvenance);
    private void InvalidateScene(DeferredAuxiliaryPrefixSceneState state, string producer)
    { ArgumentException.ThrowIfNullOrWhiteSpace(producer); SceneState = state; SceneRevision = checked(SceneRevision + 1); }

    public DeferredAuxiliaryPrefixPacket Prepare(MapSourceCreatedF103CurrentFrame acceptedFrame,
        MainMapContextCurrentPacket actualCurrentQ, DeferredAuxiliaryPrefixQueryOwner actualQueryOwner)
    {
        ArgumentNullException.ThrowIfNull(actualQueryOwner);
        return Prepare(acceptedFrame, actualCurrentQ, actualQueryOwner, actualQueryOwner.ViewGenerationIdentity);
    }
    public DeferredAuxiliaryPrefixPacket Prepare(MapSourceCreatedF103CurrentFrame acceptedFrame,
        MainMapContextCurrentPacket actualCurrentQ, DeferredAuxiliaryPrefixQueryOwner actualQueryOwner,
        string actualViewGenerationIdentity)
    {
        ValidateBoundary(acceptedFrame, actualCurrentQ, actualQueryOwner, actualViewGenerationIdentity);
        return new(this, acceptedFrame, actualCurrentQ, actualQueryOwner, actualViewGenerationIdentity);
    }
    internal void ValidateBoundary(MapSourceCreatedF103CurrentFrame frame, MainMapContextCurrentPacket q,
        DeferredAuxiliaryPrefixQueryOwner owner, string generation)
    {
        ArgumentNullException.ThrowIfNull(frame); ArgumentNullException.ThrowIfNull(q); ArgumentNullException.ThrowIfNull(owner);
        owner.Validate(frame, generation); q.Source.Validate(q);
        if (!ReferenceEquals(owner.Source, this) || SceneState != DeferredAuxiliaryPrefixSceneState.ConstructorNullRetained ||
            RlrMaxRaySamples != 0 || RlrResolution != 0 || q.FrameNumber != frame.FrameNumber ||
            !ReferenceEquals(q.Source, frame.Context.Source) || unchecked((int)q.ContextFlags3F0) < 0 ||
            (q.ContextFlags3F0 & 0x100000) != 0)
            throw new InvalidDataException("Auxiliary prefix no-op needs exact retained Main queries, null node134 Scene and this actual nonnegative/bit20-clear current Q.");
    }
    internal void ValidateCamera(MapCameraConstructionPacket camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        var source = camera.Source.Resource.Source;
        Exact(source, "san14://rdb/system/908AB6E2", 0x908AB6E2, CameraSha256);
        if (camera.Source.Camera.ObjectId != 0x38D04576 || camera.Source.Camera.ClassId != 0x23E7C64E ||
            !source.Bytes.AsSpan().SequenceEqual(cameraBytes))
            throw new InvalidDataException("Auxiliary RLR query owner requires the same exact source Main Camera generation.");
    }
    private static void Exact(LoadedRdbAsset source, string uri, uint id, string sha)
    {
        if (source.OverrideSource is not null || source.ResourceUri != uri || source.FileId != id ||
            source.DecodedSha256 != sha || Hash(source.Bytes) != sha)
            throw new InvalidDataException("Auxiliary prefix requires unedited pinned original source bytes: " + uri);
    }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
