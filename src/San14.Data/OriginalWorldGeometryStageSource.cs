using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalWorldGeometrySourceArgument(int Index, uint NameHash, uint DescriptorWord, string RawHex);
public sealed record OriginalWorldGeometryAttachment(int Slot, uint ObjectNameHash, uint SurfaceId,
    uint MipLevel, uint ArraySlice, OriginalRenderTextureDefinition Texture, OriginalRenderTextureFormats Formats);
public sealed record OriginalWorldGeometryBindNode(int NodeIndex, uint LookupKey, uint FactoryClassId,
    IReadOnlyList<uint> ConnectedNodes, byte SourceFlag, uint CpuFlags20,
    IReadOnlyList<OriginalWorldGeometryAttachment> Colors, OriginalWorldGeometryAttachment Depth,
    IReadOnlyList<OriginalWorldGeometrySourceArgument> Arguments)
{
    public uint ClearFlags => 0;
    // The encoder writes eight rectangle words only with bit0 clear. With
    // bit0 set, opcode2's decoder uses the actual primary surface extent.
    public bool PushesTargetRow => true;
    public bool SerializesCurrentRectangles => (SourceFlag & 1) == 0;
    public bool UsesPrimarySurfaceFullExtent => (SourceFlag & 1) != 0;
    public bool AllowsOptionalHostTargetGetters => (CpuFlags20 & 0x02000000) != 0;
}
public sealed record OriginalWorldGeometryPopNode(int NodeIndex, uint LookupKey, uint FactoryClassId,
    IReadOnlyList<uint> ConnectedNodes, uint SourceFlags, IReadOnlyList<OriginalWorldGeometrySourceArgument> Arguments)
{
    public uint EffectiveFlags => SourceFlags & 0xFCFFFFFFu;
    public bool CopiesDepthBeforePop => (EffectiveFlags & 0x40000000) != 0;
    public bool AllowsOptionalE86E40 => (SourceFlags & 0x01000000) != 0;
    public uint Opcode => 3;
    public bool ChangesDepthCompareOrWriteOrCullOrStencilEnable => false;
}

/// <summary>Current fields retained by the real owned Host. Fresh values
/// originate in its constructor; getters do not reset them each request.</summary>
public readonly record struct OriginalWorldGeometryHostFields(ulong Q7F8, ulong? Target3B8, ulong? Target3C0)
{
    public uint Mode => (uint)((Q7F8 >> 4) & 3);
}
public readonly record struct OriginalWorldGeometryViewAdditionalOwners(ulong? PreRenderer1B80, ulong? Models1BB0);
public readonly record struct OriginalWorldGeometryOptionalTargetWork(uint Mode, bool CallsGetter3B8,
    bool CallsGetter3C0, bool HasGpuWork);
public readonly record struct OriginalWorldGeometryCurrentPop(bool ShouldPop, uint EffectiveFlags,
    bool CallsOptionalE86E40, bool CopiesDepthBeforePop);
public sealed record OriginalWorldGeometryEmptySceneCall(int NodeIndex, long FrameNumber, string ViewIdentity,
    string CameraOwnerIdentity, byte CurrentCallFlags, uint CurrentFlag2,
    int SourceOwnerOffset)
{
    public uint GeometryDrawCount => 0;
    public bool CopiesCurrentCameraIntoLocalObject => true;
    public bool PreservesEarlierRasterAndDepthState => true;
    public bool SubmitsGpuWork => false;
}
public sealed record OriginalWorldGeometryCurrentDepthSurface(ulong PrimaryResource10,
    ulong InternalCopyResource18, uint MipLevels, uint SurfaceMip44, uint SliceCount5C);
public readonly record struct OriginalWorldGeometryDepthCopyCommand(ulong DestinationResource, uint DestinationSubresource,
    uint DestinationX, uint DestinationY, uint DestinationZ, ulong SourceResource, uint SourceSubresource,
    bool SourceBoxIsNull)
{
    public bool ProvesGpuPixelsCopied => false;
}
public sealed record OriginalWorldGeometryIndependentDepthSource(OriginalRenderTextureDefinition Declaration,
    OriginalRenderTextureFormats Formats)
{
    public bool RequiresIndependentInternalCopy => true;
    public uint InternalCopyBindFlags => 8;
    public uint InitialSurfaceMip44 => 0;
    public uint InitialSurfaceSliceCount5C => 1;
    public bool ShaderViewUsesInternalCopy => true;
    public bool DepthViewUsesPrimaryResource => true;
}

/// <summary>One loaded source contract for graph48/52/53/59 and current
/// owned Host/View empty branches. It retains typed declarations and native
/// facts; normal methods do not read/hash/reparse original assets. This is
/// not a declaration that intermediate57/58 or GPU stage work completed.</summary>
public sealed class LoadedOriginalWorldGeometryStageSource
{
    internal LoadedOriginalWorldGeometryStageSource(OriginalWorldGeometryStageNativeSource native,
        OriginalWorldGeometryBindNode army, OriginalWorldGeometryBindNode geometry,
        OriginalWorldGeometryPopNode afterArmy, OriginalWorldGeometryPopNode finalGeometry,
        OriginalWorldGeometryIndependentDepthSource depth)
    { Native = native; ArmyTargets48 = army; GeometryTargets53 = geometry;
        AfterArmyPop52 = afterArmy; FinalGeometryPop59 = finalGeometry; Depth = depth; }
    public OriginalWorldGeometryStageNativeSource Native { get; }
    public OriginalWorldGeometryBindNode ArmyTargets48 { get; }
    public OriginalWorldGeometryBindNode GeometryTargets53 { get; }
    public OriginalWorldGeometryPopNode AfterArmyPop52 { get; }
    public OriginalWorldGeometryPopNode FinalGeometryPop59 { get; }
    public OriginalWorldGeometryIndependentDepthSource Depth { get; }
    public string SourceProvenance => Native.SourceProvenance + ";canonical-system024496EE/93FB3280;57/58-not-closed-here";

    /// <summary>The source constructor writes these three fields zero. Store
    /// them on Host creation, then retain actual writers' current fields.</summary>
    public OriginalWorldGeometryHostFields ConstructFreshHostFields() => new(0, null, null);
    /// <summary>Only these two owned slots are represented here. The source
    /// View constructor/reset also affects other slots owned elsewhere.</summary>
    public OriginalWorldGeometryViewAdditionalOwners ConstructFreshViewAdditionalOwners() => new(null, null);
    public OriginalWorldGeometryViewAdditionalOwners ResetViewAdditionalOwnerSlots() => new(null, null);

    public OriginalWorldGeometryBindNode BindNode(int index) => index switch
    { 48 => ArmyTargets48, 53 => GeometryTargets53,
        _ => throw new NotSupportedException("This loaded geometry target contract closes bind nodes48/53 only.") };
    public OriginalWorldGeometryPopNode PopNode(int index) => index switch
    { 52 => AfterArmyPop52, 59 => FinalGeometryPop59,
        _ => throw new NotSupportedException("This loaded geometry target contract closes pop nodes52/59 only.") };

    /// <summary>E931A0 may call the two host target getters. With explicit
    /// null current slots they perform no GPU action in every mode; do not
    /// replace a nonnull current publication with constructor absence.</summary>
    public OriginalWorldGeometryOptionalTargetWork PrepareNullOptionalHostTargets(int bindNodeIndex,
        OriginalWorldGeometryHostFields currentHost)
        => PrepareNullOptionalHostTargets(BindNode(bindNodeIndex), currentHost);

    /// <summary>Same loaded E931A0 source for an additional actual graph bind.
    /// Its declaration is imported once by the target-generation owner.</summary>
    public OriginalWorldGeometryOptionalTargetWork PrepareNullOptionalHostTargets(OriginalWorldGeometryBindNode node,
        OriginalWorldGeometryHostFields currentHost)
    {
        if (currentHost.Target3B8 is not (null or 0) || currentHost.Target3C0 is not (null or 0))
            throw new NotSupportedException("Nonnull host3B8/3C0 requires its actual current size/release producer.");
        bool called = node.AllowsOptionalHostTargetGetters && currentHost.Mode != 0;
        return new(currentHost.Mode, called, called, false);
    }

    /// <summary>E96F30's real current Q3F0/table gates and source flag mask.
    /// Nonzero-mode59 requires the separately owned E86E40 helper; this
    /// finite normal contract cannot silently skip it.</summary>
    public OriginalWorldGeometryCurrentPop PrepareCurrentPop(int nodeIndex, int currentQ3F0,
        bool currentTablePresent, OriginalWorldGeometryHostFields currentHost)
    {
        var node = PopNode(nodeIndex);
        if (currentQ3F0 < 0 || !currentTablePresent) return new(false, node.EffectiveFlags, false, false);
        bool extra = node.AllowsOptionalE86E40 && currentHost.Mode != 0;
        if (extra) throw new NotSupportedException("Current nonzero-mode pop59 requires its actual E86E40 branch.");
        return new(true, node.EffectiveFlags, false, node.CopiesDepthBeforePop);
    }

    /// <summary>Explicit current nullable View owner gates. The primitive
    /// arenas and record counts do not choose these independent slots.</summary>
    public IReadOnlyList<OriginalWorldGeometryEmptySceneCall> PrepareCurrentEmptySceneCalls(
        OriginalMainSceneNode51Execution currentExecution, OriginalWorldGeometryViewAdditionalOwners currentOwners)
    {
        ArgumentNullException.ThrowIfNull(currentExecution);
        if (currentExecution.Source.Locale != Native.Locale)
            throw new InvalidDataException("Empty geometry calls require the same supported GeneralScene layout.");
        if (currentOwners.PreRenderer1B80 is not (null or 0) || currentOwners.Models1BB0 is not (null or 0))
            throw new NotSupportedException("Nonnull current View1B80/1BB0 requires its actual additional geometry packet.");
        int node = currentExecution.Source.NodeIndex; long frame = currentExecution.FrameNumber;
        var selected = currentExecution.Invocation.SelectedView;
        string view = selected.ViewIdentity;
        return Array.AsReadOnly(new[] {
            new OriginalWorldGeometryEmptySceneCall(node, frame, view, selected.CameraOwnerIdentity,
                currentExecution.CurrentCallFlags, currentExecution.CurrentFlag2, 0x1B80),
            new OriginalWorldGeometryEmptySceneCall(node, frame, view, selected.CameraOwnerIdentity,
                currentExecution.CurrentCallFlags, currentExecution.CurrentFlag2, 0x1BB0) });
    }

    /// <summary>Exact source D1 surface0/single-slice CopySubresourceRegion
    /// invocation plan. Actual resource identities are supplied by the owner.
    /// The return is a command, never proof of executed copy or sampled data.</summary>
    public IReadOnlyList<OriginalWorldGeometryDepthCopyCommand> PrepareDepthCopyCommands(int popNodeIndex,
        OriginalWorldGeometryCurrentDepthSurface currentSurface)
    {
        ArgumentNullException.ThrowIfNull(currentSurface);
        var node = PopNode(popNodeIndex);
        if (!node.CopiesDepthBeforePop || currentSurface.InternalCopyResource18 == 0)
            return Array.Empty<OriginalWorldGeometryDepthCopyCommand>();
        if (currentSurface.PrimaryResource10 == 0 || currentSurface.PrimaryResource10 == currentSurface.InternalCopyResource18 ||
            currentSurface.MipLevels != Depth.Declaration.LevelNum || currentSurface.SurfaceMip44 != Depth.InitialSurfaceMip44 ||
            currentSurface.SliceCount5C != Depth.InitialSurfaceSliceCount5C)
            throw new InvalidDataException("Geometry depth copy requires distinct actual D1 primary/copy resources and its source mip0/single-slice surface.");
        uint subresource = currentSurface.SurfaceMip44; // mip0 + Level1 * slice0.
        return Array.AsReadOnly(new[] { new OriginalWorldGeometryDepthCopyCommand(
            currentSurface.InternalCopyResource18, subresource, 0, 0, 0, currentSurface.PrimaryResource10, subresource, true) });
    }
    public static float EffectiveCurrentRatio1524(float currentRatio) => currentRatio > 0 && currentRatio <= 1 ? currentRatio : 1;
}

public static class OriginalWorldGeometryStageSource
{
    public static LoadedOriginalWorldGeometryStageSource Load(OriginalGameRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        var native = OriginalDeferredSamplerSources.LoadWorldGeometryStageNativeSource(repository.SourceDirectory, locale);
        var graph = repository.LoadRenderGraph("system", 0x024496EE);
        var objects = repository.LoadObjects("system", 0x93FB3280);
        if (graph.Source.ResourceUri != GameResourceId.Rdb("system", 0x024496EE).ToString())
            throw new InvalidDataException("World geometry graph resource reference differs.");
        Require(objects.Source, OriginalDeferredAuxiliaryTargetSources.ActualObjectsResourceUri, OriginalDeferredAuxiliaryTargetSources.ActualObjectsSha256);
        graph = graph with { Document = RenderGraphReader.Read(graph.Source.Bytes) };
        objects = objects with { Document = DokReader.Read(objects.Source.Bytes) };
        var army = ReadBind(graph, objects, 48, 0xB78AC02B, 0x06, new[] {
            (0x8B18CE44u,0u),(0x7E94C315u,0u),(0x521C7DEDu,0u),(0x85C125A6u,0u),(0x521C7DEDu,2u),(0x85C125A6u,2u) });
        var geometry = ReadBind(graph, objects, 53, 0xBE22DAEB, 0x46, new[] {
            (0x8B18CE44u,0u),(0x7E94C315u,0u),(0x521C7DEDu,0u),(0x85C125A6u,0u),(0x521C7DEDu,2u) });
        var depth = army.Depth.Texture;
        if (depth.Object.ObjectId != 0xE13DDB4F || depth.Kind != OriginalRenderTextureKind.Texture2D || depth.PixelFormat != 0x6C ||
            depth.ColorSpace != 0 || depth.OutputColorSpace != 0 || depth.LevelNum != 1 || depth.MinMipLevel != 0 ||
            depth.MultiSample != 0 || depth.NeverInternalCopy != 0)
            throw new InvalidDataException("Geometry D1 does not match the source independent primary/copy creation contract.");
        return new(native, army, geometry, ReadPop(graph, 52, 0xF54061F9, 0x4000003F),
            ReadPop(graph, 59, 0x54A02CB9, 0x4300001F), new(depth, OriginalRenderTextureFormatMath.Resolve(depth)));
    }

    private static OriginalWorldGeometryBindNode ReadBind(LoadedRenderGraph graph, LoadedObjects objects,
        int index, uint key, byte flag, (uint Name, uint Surface)[] expected)
    {
        var loaded = OriginalRenderGraphTargetResources.Read(graph, index, objects);
        if (loaded.Node.Call.LookupKey != key || loaded.Flag != flag || loaded.ColorRenderTargetNum != expected.Length ||
            !loaded.Node.Call.ConnectedNodeIndices.SequenceEqual(new[] { (uint)index + 1 }) ||
            !loaded.ColorTargets.Select(t => (t.ObjectNameHash, t.SurfaceId)).SequenceEqual(expected) ||
            loaded.DepthObjectNameHash != 0xE13DDB4F || loaded.DepthSurfaceId != 0 || loaded.DepthTarget is null)
            throw new InvalidDataException($"Actual geometry bind node{index} differs from its own source arguments/edges.");
        return ReadLoadedBindDeclaration(loaded);
    }

    /// <summary>Import an already parsed actual bind declaration. Native
    /// E931A0 and E752E0 semantics come from the loaded stage source; this
    /// creates no runtime binding, framebuffer or pixel completion.</summary>
    public static OriginalWorldGeometryBindNode ReadLoadedBindDeclaration(LoadedRenderGraphTargets loaded)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        if (loaded.Node.Call.FactoryClassId != 0xE2C0442A || loaded.DepthTarget is null ||
            loaded.ColorRenderTargetNum != loaded.ColorTargets.Count)
            throw new InvalidDataException("An additional geometry bind requires its actual typed attachment declaration.");
        byte flag = loaded.Flag;
        uint cpu20 = loaded.ColorRenderTargetNum;
        if ((flag & 0x14) != 0) cpu20 |= 0x40000000;
        if ((flag & 0x20) != 0) cpu20 |= 0x10000000;
        if ((flag & 0x40) != 0) cpu20 |= 0x02000000;
        if ((flag & 1) != 0) cpu20 |= 0x80000000;
        var colors = loaded.ColorTargets.Select(t => Attachment(t.Slot, t.ObjectNameHash, t.SurfaceId, t.Texture)).ToArray();
        return new(loaded.Node.Index, loaded.Node.Call.LookupKey, loaded.Node.Call.FactoryClassId,
            Array.AsReadOnly(loaded.Node.Call.ConnectedNodeIndices.ToArray()), flag, cpu20,
            Array.AsReadOnly(colors), Attachment(-1, loaded.DepthObjectNameHash, loaded.DepthSurfaceId, loaded.DepthTarget), Arguments(loaded.Node));
    }
    private static OriginalWorldGeometryAttachment Attachment(int slot, uint name, uint surface, OriginalRenderTextureDefinition texture)
    {
        if (texture.LevelNum != 1 || texture.MinMipLevel != 0 || texture.MultiSample != 0)
            throw new NotSupportedException("Geometry attachments close the actual single-level source views.");
        uint layer = texture.Kind == OriginalRenderTextureKind.Texture2DArray ? surface : 0;
        if ((texture.Kind == OriginalRenderTextureKind.Texture2D && surface != 0) ||
            (texture.Kind == OriginalRenderTextureKind.Texture2DArray &&
             (texture.NeedSliceTargets != 1 || texture.NeedAllTargets != 0 || texture.ArrayDepth is not int slices || layer >= slices)))
            throw new InvalidDataException("Geometry surface differs from the source per-slice mip0 table.");
        return new(slot, name, surface, 0, layer, texture, OriginalRenderTextureFormatMath.Resolve(texture));
    }
    private static OriginalWorldGeometryPopNode ReadPop(LoadedRenderGraph graph, int index, uint key, uint flags)
    {
        var node = graph.Document.Nodes[index];
        if (node.Call.LookupKey != key || node.Call.FactoryClassId != 0x1F7C5CF8 || node.Call.Arguments.Count != 1 ||
            !node.Call.ConnectedNodeIndices.SequenceEqual(new[] { (uint)index + 1 }))
            throw new InvalidDataException("Geometry pop node differs from its actual class/lookup/edges.");
        var argument = node.Call.Arguments[0];
        if (argument.NameHash != 0x636CA9AE || argument.DescriptorWord != 0x05000001 || argument.RawValue.Length != 4 ||
            BinaryPrimitives.ReadUInt32LittleEndian(argument.RawValue) != flags)
            throw new InvalidDataException("Geometry pop requires its actual DWORD source flag.");
        return new(index, key, node.Call.FactoryClassId, Array.AsReadOnly(node.Call.ConnectedNodeIndices.ToArray()), flags, Arguments(node));
    }
    private static IReadOnlyList<OriginalWorldGeometrySourceArgument> Arguments(CompiledGraphNode node) =>
        Array.AsReadOnly(node.Call.Arguments.Select(a => new OriginalWorldGeometrySourceArgument(a.Index, a.NameHash,
            a.DescriptorWord, Convert.ToHexString(a.RawValue))).ToArray());
    private static void Require(LoadedRdbAsset source, string uri, string sha)
    {
        if (source.ResourceUri != uri || source.DecodedSha256 != sha || source.OverrideSource is not null ||
            Convert.ToHexString(SHA256.HashData(source.Bytes)) != sha)
            throw new InvalidDataException("Geometry stage loading requires the accepted original graph/texture source.");
    }
}
