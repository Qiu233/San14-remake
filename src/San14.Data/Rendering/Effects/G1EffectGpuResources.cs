using San14.Data.Formats;

namespace San14.Data;

/// <summary>Normal encoded GPU resources for one map resource generation.
/// Selection is driven by the actual view request and current draw row. This
/// owner retains uploads and selected programs; it does not create a device.</summary>
public sealed partial class G1EffectGpuResources
{
    private readonly GameResourceRepository repository;
    private readonly Dictionary<G1EffectGpuPipelineKey, G1EffectGpuPipeline> pipelines = [];
    private readonly Dictionary<(int Bank, uint Stage, int Program), G1EffectGpuStage> stages = [];
    private readonly Dictionary<int, G1EffectGpuTexture> textures = [];

    public G1EffectGpuResources(GameResourceRepository repository, LoadedMapEffectResources resources)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(resources);
        this.repository = repository; Resources = resources;
        Meshes = resources.Model.Model.Meshes.Select(mesh => new G1EffectGpuMesh(mesh.Index,
            mesh.VertexStride, mesh.IndexDxgiFormat, mesh.VertexBytes, mesh.IndexBytes,
            mesh.StaticLayout, mesh.ShortLayout, mesh.FullLayout)).ToArray();
    }

    public LoadedMapEffectResources Resources { get; }
    public IReadOnlyList<G1EffectGpuMesh> Meshes { get; }
    public IReadOnlyList<G1EffectModelSubdraw> Subdraws => Resources.Model.Model.Subdraws;
    public IReadOnlyCollection<G1EffectGpuPipeline> Pipelines => pipelines.Values;
    public IReadOnlyCollection<G1EffectGpuTexture> Textures => textures.Values;

    /// <summary>Only the generation's source images are imported. Mips retain
    /// their original compressed/float encoding, with actual device pitches.</summary>
    public G1EffectGpuTexture PrepareTexture(int slot)
    {
        if (textures.TryGetValue(slot, out var existing)) return existing;
        var binding = Resources.LoadTextureSlot(repository, slot);
        var image = binding.Image;
        if (image.TextureSystem != 0 || image.LayerCount != 1 || image.Depth != 1 ||
            image.SubresourceLayout != G1TextureSubresourceLayout.SingleLayerMipChain)
            throw new NotSupportedException("Current effect native upload requires an original planar 2D texture.");
        uint view = ModelStageTextures.ReadNativeViewFormat(image);
        var ordinary = ModelTextureFormatMath.Resolve(image.PixelFormat, 0);
        bool compressed = image.PixelFormat is 0x06 or 0x59 or 0x5B or 0x5C or 0x5E or 0x5F;
        int unitBytes = image.PixelFormat switch
        {
            0x00 or 0x01 or 0x02 => 4, 0x03 => 8, 0x04 => 16,
            0x06 or 0x59 or 0x5C => 8, 0x5B or 0x5E or 0x5F => 16,
            _ => throw new NotSupportedException($"Effect texture format {image.PixelFormat:X2} has no device pitch declaration.")
        };
        var mips = new List<G1EffectGpuTextureMip>(image.MipCount);
        foreach (var mip in image.Mips)
        {
            int widthUnits = compressed ? Math.Max(1, (mip.Width + 3) / 4) : mip.Width;
            int heightUnits = compressed ? Math.Max(1, (mip.Height + 3) / 4) : mip.Height;
            int rowPitch = checked(widthUnits * unitBytes), slicePitch = checked(rowPitch * heightUnits);
            var bytes = image.GetEncodedMip(binding.Resource.Source.Bytes, mip.Level).ToArray();
            if (bytes.Length != slicePitch)
                throw new InvalidDataException("Effect texture mip does not match its encoded device pitch.");
            mips.Add(new(mip.Level, mip.Width, mip.Height, rowPitch, slicePitch, bytes));
        }
        var result = new G1EffectGpuTexture(slot, slot, binding, image.Width, image.Height,
            ordinary.ResourceFormat, view, mips.AsReadOnly(),
            G1TextureSamplerReader.ReadRetainedSource(image).EncodeD3D11Descriptor());
        textures.Add(slot, result);
        return result;
    }

    /// <summary>A52E50's four-word variant and the seven original G1S dimensions
    /// select this row's actual pass. No feature, bank or variant fallback is supplied.</summary>
    public G1EffectGpuPipeline PreparePipeline(int runtimeBankIndex,
        ReadOnlyMemory<byte> featureRequest48, G1EffectDrawPacket packet, int? retainedFeatureRow = null)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (!ReferenceEquals(packet.Resources, Resources))
            throw new ArgumentException("Effect draw belongs to a different resource generation.", nameof(packet));
        int? meshIndex = null;
        if (packet.MeshSubdrawIndex is { } subdraw)
        {
            if ((uint)subdraw >= Subdraws.Count) throw new InvalidDataException("Effect subdraw is outside its model.");
            meshIndex = Subdraws[subdraw].MeshIndex;
            if ((uint)meshIndex.Value >= Meshes.Count) throw new InvalidDataException("Effect subdraw references an absent mesh.");
        }
        if (packet.RenderingType < 0) throw new InvalidDataException("Effect pass is negative.");
        var key = new G1EffectGpuPipelineKey(runtimeBankIndex,
            G1EffectGpuFeatureRequest.Read(featureRequest48.Span), G1EffectGpuVariant.FromFlags(packet.Flags),
            (uint)packet.RenderingType, meshIndex, packet.UsesFullMeshLayout, packet.DynamicStride) { RetainedFeatureRow = retainedFeatureRow };
        if (pipelines.TryGetValue(key, out var found)) return found;
        var shader = Resources.LoadShaderBank(repository, runtimeBankIndex) ??
            throw new NotSupportedException($"Effect shader bank {runtimeBankIndex} is an original null dependency.");
        var tables = shader.ReadG1Bindings();
        // D33 bank3 does not call the feature setter. Its renderer retains
        // the row installed by construction or an actual previous setter.
        if (retainedFeatureRow is { } retained && (uint)retained >= tables.Features.Count)
            throw new InvalidDataException("Retained effect feature row is outside its owned shader table.");
        var feature = retainedFeatureRow is { } row
            ? new G1ShaderFeatureSelection(row, false, null, 0)
            : G1ShaderFeatureMatcher.Select(key.FeatureRequest.ToWords(),
                tables.Features.Select(item => item.Feature).ToArray());
        if (feature.RowIndex < 0)
            throw new NotSupportedException("Effect shader feature request has no selectable original row.");
        var variant = tables.Variants.Match(key.Variant.ToBytes()) ??
            throw new NotSupportedException("Effect shader variant has no exact original row.");
        uint[] dimensions = [(uint)feature.RowIndex, 0, 0, 0, 0, 0, (uint)variant.Index];
        var combination = tables.ResolveCombination(dimensions, key.Pass);
        var selectedStages = new List<G1EffectGpuStage>();
        var missing = new List<G1EffectGpuMissing>();
        foreach (var stage in combination.Stages)
        {
            if (stage.Entry is not { } entry) continue;
            var stageKey = (runtimeBankIndex, stage.StageCode, entry.ProgramIndex);
            if (!stages.TryGetValue(stageKey, out var prepared))
            {
                prepared = new(stage.StageCode, entry.ProgramIndex,
                    shader.Container.GetProgramBytes(shader.Source.Bytes, entry.ProgramIndex).ToArray(),
                    shader.ReadSignatures(entry.ProgramIndex), shader.ReadResourceDefinitions(entry.ProgramIndex));
                stages.Add(stageKey, prepared);
            }
            selectedStages.Add(prepared);
            if (stage.StageCode is not (0 or 1))
                missing.Add(new("NativeStage", $"Selected original stage {stage.StageCode} requires its native stage consumer."));
        }
        if (selectedStages.Count(stage => stage.StageCode == 1) != 1)
            missing.Add(new("InputLayout", "Selected effect pipeline requires one original vertex program."));
        var layout = CreateLayout(key, selectedStages, missing);
        var result = new G1EffectGpuPipeline(pipelines.Count, key, shader, feature, variant.Index,
            Array.AsReadOnly(dimensions), combination, shader.ReadG1ResourceBindings(combination.Index),
            selectedStages.AsReadOnly(), layout, missing.AsReadOnly());
        pipelines.Add(key, result);
        return result;
    }

    private IReadOnlyList<G1EffectGpuInputElement> CreateLayout(G1EffectGpuPipelineKey key,
        IReadOnlyList<G1EffectGpuStage> selectedStages, List<G1EffectGpuMissing> missing)
    {
        G1EffectGpuInputElement[] elements;
        if (key.MeshIndex is { } index)
        {
            var mesh = Meshes[index];
            var attributes = key.FullMeshLayout ? mesh.FullLayout : mesh.ShortLayout;
            elements = attributes.Select(attribute => new G1EffectGpuInputElement(attribute.SemanticName,
                attribute.SemanticIndex, (uint)attribute.DxgiFormat, (uint)attribute.InputSlot,
                (uint)attribute.ByteOffset, attribute.PerInstance ? 1u : 0u,
                (uint)attribute.InstanceStepRate)).ToArray();
            foreach (var attribute in attributes.Where(attribute => attribute.PerInstance))
                if (attribute.ByteOffset + attribute.ByteWidth > key.DynamicStride)
                    missing.Add(new("InputLayout", $"Mesh instance {attribute.SemanticName}{attribute.SemanticIndex} exceeds the actual {key.DynamicStride}-byte stream."));
        }
        else
        {
            if (key.DynamicStride is not (32 or 64))
                missing.Add(new("InputLayout", $"Sprite stride {key.DynamicStride} needs its corresponding original input declaration."));
            // AA0890's normal and soft declarations have these same four
            // physical attributes; COLOR uses the original BGRA format87.
            elements = [new("POSITION", 0, 2, 0, 0, 0, 0), new("COLOR", 0, 87, 0, 16, 0, 0),
                new("COLOR", 1, 87, 0, 20, 0, 0), new("TEXCOORD", 0, 16, 0, 24, 0, 0)];
            if (key.DynamicStride == 64)
                elements = [.. elements, new("TEXCOORD", 1, 16, 0, 32, 0, 0),
                    new("TEXCOORD", 2, 16, 0, 40, 0, 0), new("TEXCOORD", 3, 41, 0, 48, 0, 0)];
        }
        foreach (var vertex in selectedStages.Where(stage => stage.StageCode == 1))
            foreach (var input in vertex.Signatures.Parts.Where(part => part.Kind == DxbcSignatureKind.Input)
                .SelectMany(part => part.Parameters).Where(input => input.RawSystemValue == 0))
                if (!elements.Any(element => element.SemanticIndex == input.SemanticIndex &&
                    string.Equals(element.SemanticName, input.SemanticName.Text, StringComparison.OrdinalIgnoreCase)))
                    missing.Add(new("InputLayout", $"Original vertex input {input.SemanticName.Text}{input.SemanticIndex} has no source declaration."));
        return Array.AsReadOnly(elements);
    }
}
