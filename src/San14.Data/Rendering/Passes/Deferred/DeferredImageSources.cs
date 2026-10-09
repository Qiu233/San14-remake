using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record DeferredTargetImageSource(string Role, short EncodedParameter,
    TaskGraphArgument SourceArgument, G1ShaderResourceBinding ResourceMapping,
    DxbcRdefBinding PixelBinding, RenderTextureDefinition Texture);

/// <summary>C9AB00 consumes Camera+ED8 and, for a negative override,
/// CommonView+8C. The legacy CameraField8C name is retained for existing callers;
/// production readers use the typed Camera filter source receipt.</summary>
public readonly record struct DeferredTransparencyImageSelection(int SourceOverrideEd8,
    int? CameraField8C)
{
    public int? CommonViewField8C => CameraField8C;
    public int SourceIndex => SourceOverrideEd8 < 0
        ? CameraField8C is null || CameraField8C == 0 ? 1 : 0
        : SourceOverrideEd8 == 0 ? 1 : 0;
}

public sealed record LoadedDeferredImageSources(LoadedDeferredTexturePlan Deferred,
    DeferredTargetImageSource RefractionDepth,
    DeferredTargetImageSource TranslucentSpecular,
    DeferredTargetImageSource FullSizeTranslucentSpecular,
    DeferredTargetImageSource BlurredDepth)
{
    public DeferredTargetImageSource SelectSpecular(DeferredTransparencyImageSelection selection)
        => selection.SourceIndex == 0 ? TranslucentSpecular : FullSizeTranslucentSpecular;
}

/// <summary>Actual F103 auxiliary image fields and consumers. TC E75E34..E76603
/// passes source15/16/17 to E69720 keys588/58C/590; EAD085 C9AB00 selects
/// CPU cache560/568. EADFDA binds34/t8 and EADFF9 binds4/t9. These are target
/// owners, with contents produced by the original graph; no file image exists.</summary>
public static class DeferredImageSources
{
    public const string PixelSourceSha256 = "B01248B54B5D09A98D850D4627520F78808EA14C1CF090F3C804753E78F1884E";

    /// <summary>Raw-byte fixture projection only. The legacy argument names do
    /// not identify the native owner: ED8 must belong to the Camera snapshot,
    /// and the second value to CommonView. Production uses the typed source.</summary>
    public static DeferredTransparencyImageSelection ReadSelection(ReadOnlySpan<byte> explicitCompleteLightData,
        int? explicitCameraField8C)
    {
        if (explicitCompleteLightData.Length != MainLightConstruction.LightDataBytes)
            throw new InvalidDataException("Transparency selection requires the complete current10C0 LightData.");
        return new(BinaryPrimitives.ReadInt32LittleEndian(explicitCompleteLightData.Slice(0xED8, 4)), explicitCameraField8C);
    }

    public static LoadedDeferredImageSources Read(LoadedDeferredTexturePlan deferred)
    {
        ArgumentNullException.ThrowIfNull(deferred);
        ValidateResource(deferred.Shader.Source);
        ValidateResource(deferred.TextureObjects.Source);
        ValidateResource(deferred.Graph.Source);
        var shader = new LoadedShaderContainer(G2ShaderContainerReader.Read(deferred.Shader.Source.Bytes), deferred.Shader.Source);
        var resources = shader.ReadG1ResourceBindings(deferred.ResourceBindings.Combination.Index);
        var pixels = resources.Combination.Stages.Where(s => s.StageCode == 0 && s.Entry is not null).ToArray();
        if (pixels.Length != 1 || Convert.ToHexString(SHA256.HashData(shader.Container.GetProgramBytes(
                shader.Source.Bytes, pixels[0].Entry!.ProgramIndex))) != PixelSourceSha256 ||
            deferred.Node.Call.FactoryClassId != RenderGraphShaderReferences.ClusteredDeferredFactoryClassId)
            throw new InvalidDataException("Deferred image sources require the recovered F103 PS1 identity.");
        var graph = RenderGraphReader.Read(deferred.Graph.Source.Bytes);
        var textureObjects = new LoadedObjects(DokReader.Read(deferred.TextureObjects.Source.Bytes), deferred.TextureObjects.Source);
        var pixel = shader.ReadResourceDefinitions(pixels[0].Entry!.ProgramIndex);
        var actualNode = graph.Nodes.Single(n => n.Index == deferred.Node.Index);
        if (graph.GraphKind != CompiledGraphKind.RenderGraph ||
            actualNode.Call.FactoryClassId != RenderGraphShaderReferences.ClusteredDeferredFactoryClassId ||
            !SameArguments(actualNode.Call.Arguments, deferred.Node.Call.Arguments))
            throw new InvalidDataException("Deferred source node arguments differ from their graph.");
        DeferredTargetImageSource Field(string role, uint hash, short encoded, uint register, string name)
        {
            var rows = actualNode.Call.Arguments.Where(a => a.NameHash == hash).ToArray();
            if (rows.Length != 1 || rows[0].DescriptorWord != 0x05000001 || rows[0].RawValue.Length != 4)
                throw new InvalidDataException($"Deferred {role} requires its exact typed source field{hash:X8}.");
            var texture = RenderTextureResources.Read(textureObjects,
                BinaryPrimitives.ReadUInt32LittleEndian(rows[0].RawValue));
            var mappings = resources.Entries.Where(e => e.PrimaryEncoded == encoded).ToArray();
            var bindings = pixel.Bindings.Where(b => b.Name.Text == name && b.InputType == 2).ToArray();
            if (texture.Kind != RenderTextureKind.Texture2D || mappings.Length != 1 ||
                mappings[0].Name != name || mappings[0].SlotIndex != (int)register || bindings.Length != 1 ||
                bindings[0].BindPoint != register || bindings[0].Dimension != 4 || bindings[0].ReturnType != 5 ||
                bindings[0].BindCount != 1)
                throw new InvalidDataException($"Deferred {role} differs from its recovered float Texture2D consumer.");
            return new(role, encoded, rows[0], mappings[0], bindings[0], texture);
        }
        return new(deferred,
            Field("RefractionDepth", 0xF744A94F, 35, 2, "sDepthMap"),
            Field("TranslucentSpecular", 0x7AA5F8D7, 34, 8, "sTrnsSpc"),
            Field("FullSizeTranslucentSpecular", 0x1D2BAAE7, 34, 8, "sTrnsSpc"),
            Field("BlurredDepth", 0x1424ADE6, 4, 9, "sTrnsDpt"));
    }

    private static bool SameArguments(IReadOnlyList<TaskGraphArgument> left, IReadOnlyList<TaskGraphArgument> right)
        => left.Count == right.Count && left.Zip(right).All(p => p.First.NameHash == p.Second.NameHash &&
            p.First.DescriptorWord == p.Second.DescriptorWord && p.First.RawValue.AsSpan().SequenceEqual(p.Second.RawValue));

    private static void ValidateResource(LoadedRdbAsset source)
    {
        if (source.OverrideSource is not null || !Convert.ToHexString(SHA256.HashData(source.Bytes))
                .Equals(source.DecodedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Deferred image source bytes differ from their original resource identity.");
    }
}
