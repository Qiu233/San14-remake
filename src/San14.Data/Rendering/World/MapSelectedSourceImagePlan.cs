using System.Buffers.Binary;
using System.Security.Cryptography;
using San14.Data.Formats;

namespace San14.Data;

public sealed record MapSelectedImageConsumer(string Role, short? EncodedParameter,
    DxbcRdefBinding Declaration, G1ShaderResourceBinding? ResourceMapping);
public sealed record MapSelectedSamplerConsumer(DxbcRdefBinding Declaration,
    MapSelectedImageConsumer KeySource, IReadOnlyList<string> SharedImageRoles);

/// <summary>One accepted F103 selection's canonical resource roles. Register
/// numbers are read from that selected original program; no PS1 slot is copied.
/// Graph targets name their owners, without claiming allocation or contents.</summary>
public sealed class MapSelectedSourceImagePlan
{
    public DeferredF103SelectedSource Selection { get; }
    public LoadedDeferredTexturePlan TexturePlan { get; }
    public IReadOnlyList<MapSelectedImageConsumer> Images { get; }
    public IReadOnlyList<MapSelectedSamplerConsumer> Samplers { get; }
    public IReadOnlyList<DeferredTargetImageSource> GraphTargets { get; }
    public long FrameNumber => Selection.Current.FrameNumber;
    public string CurrentProducerProvenance => Selection.Current.CurrentProducerProvenance;

    private MapSelectedSourceImagePlan(DeferredF103SelectedSource selection,
        LoadedDeferredTexturePlan textures, List<MapSelectedImageConsumer> images,
        List<MapSelectedSamplerConsumer> samplers, List<DeferredTargetImageSource> targets)
        => (Selection, TexturePlan, Images, Samplers, GraphTargets) =
            (selection, textures, images.AsReadOnly(), samplers.AsReadOnly(), targets.AsReadOnly());

    private static readonly (short Encoded, string Name, string Role, uint Dimension)[] Ordinary =
    [ (32,"sIES","IES",5), (52,"sVolumetricFogMap","VolumetricFog",8),
      (35,"sDepthMap","RefractionDepth",4), (46,"sSplShadow","PointShadow",10),
      (51,"sSslShadow","SpotShadow",5), (54,"sGxAmbLut","GGX",4),
      (36,"sHatchingShadow","Hatching",4), (27,"sAmbSpc","AmbientSpecular",9),
      (34,"sTrnsSpc","TranslucentSpecular",4), (4,"sTrnsDpt","BlurredDepth",4),
      (16,"sRamp","Ramp",4), (8,"sDepth","GBufferDepth",4),
      (128,"sGBuf0","GBufferAB",4), (129,"sGBuf1","GBufferRS",5),
      (130,"sGBuf2","GBufferNS",4), (131,"sGBuf3","GBufferRR",5) ];
    private static readonly (string Name, string Role, string Configuration, uint Hash, uint Return)[] ExtraBuffers =
    [ ("tCllLightIndices","ClusterIndices","ClusterLightList",0xAB69006D,4),
      ("tCllLightPositions","ClusterPositions","ClusterLightList",0x6AF9A27C,5),
      ("tCllLightAttributes","ClusterAttributes","ClusterLightList",0x5764F4BD,5),
      ("tCllLightExAttributes","ClusterExAttributes","ClusterLightList",0x56C4C954,4),
      ("tCllSptLightAttributes","ClusterSpotAttributes","ClusterLightList",0x689F3D34,5),
      ("tCllSptLightExAttributes","ClusterSpotExAttributes","ClusterLightList",0x65BD2433,4),
      ("tLasData","LocalAmbientData","LocalAmbSpecMaps",0xC356A050,5),
      ("tLasIndices","LocalAmbientIndices","LocalAmbSpecMaps",0xA119EC14,4) ];

    public static MapSelectedSourceImagePlan Read(DeferredF103SelectedSource selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var textures = selection.ReadSelectedTexturePlan();
        ValidateAsset(textures.TextureObjects.Source);
        var shader = textures.Shader;
        var definitions = shader.ReadResourceDefinitions(selection.PixelProgramIndex);
        var mappings = shader.ReadG1ResourceBindings(selection.Combination.Index);
        var configurations = shader.ReadConfigurations();
        var images = new List<MapSelectedImageConsumer>();
        foreach (var declaration in definitions.Bindings.Where(b => b.InputType == 2))
        {
            if (declaration.BindCount != 1) throw Error("array declarations require their current producer extent");
            var ordinary = Ordinary.SingleOrDefault(row => row.Name == declaration.Name.Text);
            if (ordinary.Name is not null)
            {
                var matches = mappings.Entries.Where(e => e.PrimaryEncoded == ordinary.Encoded).ToArray();
                if (matches.Length != 1 || matches[0].Name != ordinary.Name || matches[0].SlotIndex < 0 ||
                    declaration.BindPoint != (uint)matches[0].SlotIndex || declaration.Dimension != ordinary.Dimension ||
                    declaration.ReturnType != 5)
                    throw Error("encoded image role differs from its selected source declaration");
                images.Add(new(ordinary.Role, ordinary.Encoded, declaration, matches[0]));
                continue;
            }
            var extra = ExtraBuffers.SingleOrDefault(row => row.Name == declaration.Name.Text);
            if (extra.Name is not null)
            {
                var config = configurations.Records.Single(r => r.Name == extra.Configuration);
                var slot = config.Find(extra.Hash);
                if (slot is null || declaration.BindPoint != slot.RawValue || declaration.Dimension != 1 ||
                    declaration.ReturnType != extra.Return)
                    throw Error("extra typed-buffer role differs from its actual plugin configuration");
                images.Add(new(extra.Role, null, declaration, null));
                continue;
            }
            if (declaration.Name.Text == "sLclAmbSpc")
            {
                var config = configurations.Records.Single(r => r.Name == "LocalAmbSpecMaps");
                var slot = config.Find(0xBAE590DA);
                var named = G2ShaderMetadataReader.Read(shader.Source.Bytes).Records.Single(r => r.Name?.Text == "sLclAmbSpc");
                if (slot is null || declaration.BindPoint != checked(slot.RawValue + 16) || declaration.Dimension != 10 ||
                    declaration.ReturnType != 5 || named.RawWords[1] != 42 || named.RawWords[2] != 42)
                    throw Error("LAS cube role differs from its ExtraSampler+16 and named source metadata");
                images.Add(new("LocalAmbientCube", null, declaration, null));
                continue;
            }
            if (declaration.Name.Text == "tMaterialTable" && declaration.Dimension == 1 && declaration.ReturnType == 4)
            { images.Add(new("MaterialTable", null, declaration, null)); continue; }
            throw Error("selected source has an image role outside the recovered owner contracts");
        }
        if (images.Select(i => i.Role).Distinct().Count() != images.Count ||
            images.Select(i => i.Declaration.BindPoint).Distinct().Count() != images.Count)
            throw Error("selected image roles or registers are duplicated");
        var samplers = new List<MapSelectedSamplerConsumer>();
        foreach (var declaration in definitions.Bindings.Where(b => b.InputType == 3))
        {
            if (declaration.BindCount != 1 || declaration.Dimension != 0)
                throw Error("selected sampler declaration has a different type");
            // This sampler is retained by the source base RR preparation. Its
            // own RDEF s15 may differ from the selected RR image's t14.
            string imageName = declaration.Name.Text == "__smpsShadowPointSampler" ? "sGBuf3" :
                declaration.Name.Text.StartsWith("__smp", StringComparison.Ordinal) ? declaration.Name.Text[5..] : "";
            var keySources = images.Where(i => i.Declaration.Name.Text == imageName).ToArray();
            if (keySources.Length != 1) throw Error("sampler has no exact selected source key owner");
            var keySource = keySources[0];
            if (declaration.Name.Text != "__smpsShadowPointSampler" && declaration.BindPoint != keySource.Declaration.BindPoint)
                throw Error("selected ordinary sampler no longer shares its mapped source slot");
            string[] shared = keySource.Role == "AmbientSpecular" && images.Any(i => i.Role == "LocalAmbientCube")
                ? [keySource.Role, "LocalAmbientCube"] : [keySource.Role];
            samplers.Add(new(declaration, keySource, Array.AsReadOnly(shared)));
        }

        var graph = RenderGraphReader.Read(textures.Graph.Source.Bytes);
        var node = graph.Nodes[selection.SourcePlan.Node.Index];
        var objects = new LoadedObjects(DokReader.Read(textures.TextureObjects.Source.Bytes), textures.TextureObjects.Source);
        var targets = new List<DeferredTargetImageSource>();
        foreach (var target in new (string Role, string Consumer, uint Hash)[] {
            ("RefractionDepth","RefractionDepth",0xF744A94F), ("TranslucentSpecular","TranslucentSpecular",0x7AA5F8D7),
            ("FullSizeTranslucentSpecular","TranslucentSpecular",0x1D2BAAE7), ("BlurredDepth","BlurredDepth",0x1424ADE6) })
        {
            var consumer = images.SingleOrDefault(i => i.Role == target.Consumer);
            if (consumer is null) continue;
            var fields = node.Call.Arguments.Where(a => a.NameHash == target.Hash).ToArray();
            if (fields.Length != 1 || fields[0].DescriptorWord != 0x05000001 || fields[0].RawValue.Length != 4 ||
                consumer.ResourceMapping is null || consumer.EncodedParameter is null)
                throw Error("graph target lacks its actual typed source field");
            var texture = RenderTextureResources.Read(objects, BinaryPrimitives.ReadUInt32LittleEndian(fields[0].RawValue));
            if (texture.Kind != RenderTextureKind.Texture2D) throw Error("graph target is not a float2D source view");
            targets.Add(new(target.Role, consumer.EncodedParameter.Value, fields[0], consumer.ResourceMapping,
                consumer.Declaration, texture));
        }
        return new(selection, textures, images, samplers, targets);
    }

    public MapSelectedImageConsumer RequireRole(string role) => Images.SingleOrDefault(i => i.Role == role)
        ?? throw Error("requested role is absent from this selected original program");
    public DeferredTargetImageSource SelectSpecular(DeferredTransparencyImageSelection current)
        => GraphTargets.Single(t => t.Role == (current.SourceIndex == 0 ? "TranslucentSpecular" : "FullSizeTranslucentSpecular"));
    public void Validate()
    {
        // The private Read constructor already imported the source roles and
        // declarations. Reuse them while checking this actual current source
        // selection; normal consumers do not reparse sealed internal fields.

    }
    private static void ValidateAsset(LoadedRdbAsset source)
    {
        if (source.OverrideSource is not null || string.IsNullOrWhiteSpace(source.ResourceUri) ||
            Convert.ToHexString(SHA256.HashData(source.Bytes)) != source.DecodedSha256)
            throw Error("original image owner source bytes differ from their exact declared identity");
    }
    private static InvalidDataException Error(string reason) => new("Selected F103 image plan: " + reason + ".");
}
