using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record OriginalTerrainTargetExtentSource(uint ObjectNameHash,
    OriginalRenderTextureKind Kind, int DeclaredWidth, int DeclaredHeight, int Width, int Height);
public sealed record OriginalTerrainTargetResizeSource(long FrameNumber, string CurrentContextOwnerIdentity,
    string CameraFrameProvenance, int ContextWidth3E8, int ContextHeight3EC, float RenderResolutionRatio,
    IReadOnlyList<OriginalTerrainTargetExtentSource> Targets, string SourceProvenance);
public sealed record OriginalTerrainInitialDepthStencilClearSource(long FrameNumber,
    string CurrentContextOwnerIdentity, uint CurrentContextFlags3E4, uint NativeTargetMask,
    uint DepthTargetObjectNameHash, uint DepthSurfaceId, float Depth, byte Stencil, string SourceProvenance)
{
    public bool ClearsColor => false;
    public bool ClearsDepth => true;
    public bool ClearsStencil => true;
}

/// <summary>Exact Main node53/node73 declarations, their no-clear attachment
/// switch, node32/33's separate depth/stencil clear, and the finite source
/// extent producer. This supplies no raster or successful allocation receipt.</summary>
public sealed class OriginalTerrainGbufferTargetSources
{
    public const string GraphSha256 = "B9AE1EC8209C067DBC73084CDC0EB149A65E282C25D4B8AD398F43071072971D";
    private readonly LoadedRenderGraph graph;
    private readonly LoadedObjects textures;
    private readonly byte[] graphBytes, textureBytes;
    private bool retained = true;
    private OriginalTerrainGbufferTargetSources(LoadedRenderGraph actualGraph, LoadedObjects actualTextures,
        string locale, string executableSha256, LoadedRenderGraphTargets geometry, LoadedRenderGraphTargets output)
    {
        graph = actualGraph; textures = actualTextures; Locale = locale; ExecutableSha256 = executableSha256;
        GeometryTargets = geometry; OutputTargets = output;
        graphBytes = actualGraph.Source.Bytes.ToArray(); textureBytes = actualTextures.Source.Bytes.ToArray();
    }
    public string Locale { get; }
    public string ExecutableSha256 { get; }
    public LoadedRenderGraphTargets GeometryTargets { get; }
    public LoadedRenderGraphTargets OutputTargets { get; }
    public uint AttachmentSwitchClearFlags => 0;
    public string SourceProvenance => $"ordinary-terrain-target-source/{Locale}/{ExecutableSha256};" +
        $"system/024496EE@{GraphSha256};system/93FB3280@{OriginalDeferredAuxiliaryTargetSources.ActualObjectsSha256};" +
        "E2C-CPU-v8-E931A0;node53-five-MRT/node73-output;E9334A-clearFlags0;" +
        "node32-depth-only/node33-Target70-without40;current-extent-Q3E8/3EC;Main996-retained";

    public static OriginalTerrainGbufferTargetSources ReadSource(OriginalGameRepository repository,
        string locale, LoadedRenderGraph graph, LoadedObjects textures)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(textures);
        if (locale is not ("SC" or "TC")) throw new InvalidDataException("Terrain targets require exact SC or TC.");
        RequireSource(graph.Source, GameResourceId.Rdb("system", 0x024496EE).ToString(), GraphSha256);
        RequireSource(textures.Source, OriginalDeferredAuxiliaryTargetSources.ActualObjectsResourceUri,
            OriginalDeferredAuxiliaryTargetSources.ActualObjectsSha256);
        var canonicalGraph = graph with { Document = RenderGraphReader.Read(graph.Source.Bytes) };
        var canonicalTextures = textures with { Document = DokReader.Read(textures.Source.Bytes) };
        var geometry = OriginalRenderGraphTargetResources.Read(canonicalGraph, 53, canonicalTextures);
        var output = OriginalRenderGraphTargetResources.Read(canonicalGraph, 73, canonicalTextures);
        if (geometry.Flag != 0x46 || geometry.ColorRenderTargetNum != 5 ||
            !geometry.ColorTargets.Select(t => (t.ObjectNameHash, t.SurfaceId)).SequenceEqual(new (uint, uint)[]
            { (0x8B18CE44, 0), (0x7E94C315, 0), (0x521C7DED, 0), (0x85C125A6, 0), (0x521C7DED, 2) }) ||
            geometry.DepthObjectNameHash != 0xE13DDB4F || geometry.DepthSurfaceId != 0 ||
            output.Flag != 0x12 || output.ColorRenderTargetNum != 1 ||
            output.ColorTargets[0].ObjectNameHash != 0x064B746B || output.ColorTargets[0].SurfaceId != 0 ||
            output.DepthObjectNameHash != 0xE13DDB4F || output.DepthSurfaceId != 0)
            throw new InvalidDataException("Actual node53/73 attachment order or source identity differs.");
        foreach (var texture in geometry.ColorTargets.Concat(output.ColorTargets).Select(t => t.Texture)
            .Concat(new[] { geometry.DepthTarget! }).DistinctBy(t => t.Object.ObjectId))
        {
            if (texture.Width != 0 || texture.Height != 0 || texture.LevelNum != 1 || texture.MinMipLevel != 0 ||
                texture.MultiSample != 0 || texture.NeverInternalCopy != 0)
                throw new InvalidDataException("Terrain frame target is outside the exact source single-level extent contract.");
            _ = OriginalRenderTextureFormatMath.Resolve(texture);
        }
        // node32's serialized count is1 but Flag44 lacks0A: the loader forces
        // effective color count0 before resolving the all-null color names.
        var initial = canonicalGraph.Document.Nodes[32]; var clear = canonicalGraph.Document.Nodes[33];
        if (initial.Call.FactoryClassId != OriginalRenderGraphTargetResources.FactoryClassId ||
            Arg(initial, 0x05D92ACC, 0x01000001, 1)[0] != 0x44 ||
            U(initial, 0xC13FB0BE, 0x05000001) != 0xE13DDB4F ||
            U(initial, 0xAF2E2D9E, 0x05000001) != 0 || clear.Call.FactoryClassId != 0xCD713E46 ||
            U(clear, 0xAA8D0DB1, 0x04000001) != 0x70 ||
            !Arg(clear, 0xC8D7689D, 0x01000004, 4).SequenceEqual(new byte[4]) ||
            U(clear, 0x00000AE6, 0x08000001) != 0x3F800000 ||
            U(clear, 0x349AC1A4, 0x04000001) != 0 || U(clear, 0x51332A47, 0x05000001) != 0 ||
            U(clear, 0x05D92ACC, 0x05000001) != 0 || U(clear, 0x39E192FF, 0x04000001) != uint.MaxValue)
            throw new InvalidDataException("Terrain initial depth/stencil clear differs from actual node32/33 source.");
        string sha = OriginalCeHexWorldCoordinates.VerifyInstalledExecutable(repository.SourceDirectory, locale);
        return new(canonicalGraph, canonicalTextures, locale, sha, geometry, output);
    }

    /// <summary>Current dimensions are explicit values published by the owned
    /// Host resize. The actual source Main ratio0 is retained and E8F210 returns
    /// 1. Imported/current ratio edits must invalidate this source owner.</summary>
    public OriginalTerrainTargetResizeSource PrepareResize(OriginalMapCameraConstructionPacket currentCamera,
        string currentContextOwnerIdentity, int currentWidth3E8, int currentHeight3EC)
    {
        ArgumentNullException.ThrowIfNull(currentCamera); ArgumentException.ThrowIfNullOrWhiteSpace(currentContextOwnerIdentity);
        RequireRetained();
        if (currentCamera.FrameNumber < 1 || currentCamera.ExecutableLocale != Locale ||
            currentWidth3E8 is < 1 or > short.MaxValue || currentHeight3EC is < 1 or > short.MaxValue)
            throw new InvalidDataException("Terrain resize needs the accepted source Camera and explicit positive native signed16 Host dimensions.");
        var main = OriginalMapCameraConstructionSource.ReadPerspective(currentCamera.Source.Resource, 0x38D04576);
        if (currentCamera.Source.Camera.ObjectId != main.Camera.ObjectId)
            throw new InvalidDataException("Terrain target ratio must come from the accepted Main38D Camera.");
        var ratio = main.Camera.Properties[996];
        if (ratio.Index != 996 || ratio.TypeCode != 8 || ratio.ElementCount != 1 || ratio.RawValue.Length != 4 ||
            BinaryPrimitives.ReadUInt32LittleEndian(ratio.RawValue) != 0)
            throw new InvalidDataException("Terrain target owner does not retain Main's actual RenderResolutionRatio0 source.");
        var rows = GeometryTargets.ColorTargets.Concat(OutputTargets.ColorTargets).Select(t => t.Texture)
            .Concat(new[] { GeometryTargets.DepthTarget! }).DistinctBy(t => t.Object.ObjectId).Select(texture =>
                new OriginalTerrainTargetExtentSource(texture.Object.ObjectId, texture.Kind, texture.Width, texture.Height,
                    ResolveCurrentDimension(texture.Width, currentWidth3E8, 1),
                    ResolveCurrentDimension(texture.Height, currentHeight3EC, 1))).ToArray();
        return new(currentCamera.FrameNumber, currentContextOwnerIdentity, currentCamera.FrameProvenance,
            currentWidth3E8, currentHeight3EC, 1, Array.AsReadOnly(rows), SourceProvenance);
    }

    /// <summary>Explicit current context flags select native Z or1-Z. This is
    /// the independent node33 depth/stencil clear; node53/73 do not clear color.</summary>
    public OriginalTerrainInitialDepthStencilClearSource PrepareInitialDepthStencilClear(long frameNumber,
        string currentContextOwnerIdentity, uint currentContextFlags3E4)
    {
        RequireRetained(); ArgumentException.ThrowIfNullOrWhiteSpace(currentContextOwnerIdentity);
        if (frameNumber < 1) throw new InvalidDataException("Terrain clear needs an explicit owned render attempt.");
        return new(frameNumber, currentContextOwnerIdentity, currentContextFlags3E4, 0x30, 0xE13DDB4F, 0,
            (currentContextFlags3E4 & 0x20000000) != 0 ? 0 : 1, 0, SourceProvenance);
    }

    /// <summary>Finite CPU C0BB60 current-target arithmetic: signed16
    /// declaration<=0 takes SAR hostDimension, float32 conversion, MULSS and
    /// CVTTSS2SI. Positive fixed dimensions bypass ratio multiplication.</summary>
    public static int ResolveCurrentDimension(int declared, int currentHostDimension, float currentRatio)
    {
        if (declared is < short.MinValue or > short.MaxValue || currentHostDimension is < 1 or > short.MaxValue ||
            !float.IsFinite(currentRatio) || currentRatio <= 0 || currentRatio > 1)
            throw new InvalidDataException("Finite target extent requires signed16 source/positive Host dimensions and effective native ratio(0,1].");
        if (declared > 0) return declared;
        int shifted = currentHostDimension >> (unchecked((byte)-declared) & 31);
        float scaled = (float)shifted * currentRatio;
        int result = (int)scaled;
        if (result <= 0) throw new InvalidDataException("Original target arithmetic produced no positive allocation extent.");
        return result;
    }

    public void MarkImportedOrEdited(string actualWriter)
    { ArgumentException.ThrowIfNullOrWhiteSpace(actualWriter); retained = false; }
    private void RequireRetained()
    {
        if (!retained || !graph.Source.Bytes.AsSpan().SequenceEqual(graphBytes) ||
            !textures.Source.Bytes.AsSpan().SequenceEqual(textureBytes))
            throw new InvalidDataException("Terrain target source was replaced/imported/edited; current source preparation is required.");
    }
    private static byte[] Arg(CompiledGraphNode node, uint name, uint word, int count)
    {
        var row = node.Call.Arguments.SingleOrDefault(a => a.NameHash == name);
        if (row is null || row.DescriptorWord != word || row.RawValue.Length != count)
            throw new InvalidDataException("Terrain target or clear requires the exact typed source literal.");
        return row.RawValue;
    }
    private static uint U(CompiledGraphNode node, uint name, uint word) => BinaryPrimitives.ReadUInt32LittleEndian(Arg(node, name, word, 4));
    private static void RequireSource(LoadedRdbAsset source, string uri, string sha)
    {
        if (source.OverrideSource is not null || source.ResourceUri != uri || source.DecodedSha256 != sha ||
            Convert.ToHexString(SHA256.HashData(source.Bytes)) != sha)
            throw new InvalidDataException("Terrain frame rules require exact canonical unchanged source bytes.");
    }
}
