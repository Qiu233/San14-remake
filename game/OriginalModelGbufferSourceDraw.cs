using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using San14.Data;
using San14.Data.Formats;

public sealed record OriginalModelGbufferPreparedDraw(OriginalModelGbufferInstancePacket Packet,
    uint NextCachedMaterialFlags, OriginalModelSpecularPreparation SpecularPreparation,
    IReadOnlyList<TerrainConstantWrite> PixelWrites);

/// <summary>One source-selected draw's immutable preparation plan. Load when its
/// model category or source revision changes, then Prepare for each accepted
/// frame. The source chooses shader, material fields, texture registers, proxy
/// image selectors, native sampler and source texture views; runtime variant, matrices,
/// previous material state, alpha enable and raster state remain explicit.</summary>
public sealed class OriginalModelGbufferSourceDraw
{
    public LoadedModelGeometry Model { get; }
    public LoadedShaderContainer Shader { get; }
    public OriginalModelRenderGraphShaderSource GraphSource { get; }
    public OriginalModelGbufferMaterialSource Material { get; }
    public OriginalModelStageTexturePlan TexturePlan { get; }
    public OriginalModelMatrixBindingSource? MatrixBindingSource { get; }
    public int ExplicitCombinationIndex { get; }
    public int DrawIndex => TexturePlan.DrawIndex;
    public DxbcResourceDefinitions VertexDefinitions { get; }
    public DxbcResourceDefinitions PixelDefinitions { get; }
    private readonly OriginalModelGbufferTextureUse[] _pixelTextures;

    private OriginalModelGbufferSourceDraw(LoadedModelGeometry model, LoadedShaderContainer shader,
        OriginalModelRenderGraphShaderSource graphSource, OriginalModelGbufferMaterialSource material,
        OriginalModelStageTexturePlan texturePlan, int combination,
        DxbcResourceDefinitions vertex, DxbcResourceDefinitions pixel,
        OriginalModelGbufferTextureUse[] pixelTextures, OriginalModelMatrixBindingSource? matrixBinding)
        => (Model, Shader, GraphSource, Material, TexturePlan, ExplicitCombinationIndex,
            VertexDefinitions, PixelDefinitions, _pixelTextures, MatrixBindingSource) =
            (model, shader, graphSource, material, texturePlan, combination, vertex, pixel, pixelTextures, matrixBinding);

    /// <summary>Every native view is supplied by register. TID bit31 is retained
    /// by the proxy plan and is never reinterpreted as a color-space flag.</summary>
    public static OriginalModelGbufferSourceDraw Load(OriginalGameRepository repository,
        LoadedModelTextureResources model, LoadedObjects shaderObjects, LoadedRenderGraph graph,
        int explicitGraphNode, int drawIndex, int explicitCombinationIndex,
        OriginalModelStageTextureRuntime explicitTextureRuntime,
        IReadOnlyDictionary<uint,uint> explicitNativeTextureViews)
        => LoadCore(repository, model, shaderObjects, graph, explicitGraphNode, drawIndex,
            explicitCombinationIndex, explicitTextureRuntime, explicitNativeTextureViews ??
                throw new ArgumentNullException(nameof(explicitNativeTextureViews)));

    /// <summary>Modern system0 single-layer source image header byte19 bit0
    /// chooses the native ordinary/alternate view. Unsupported headers fail.
    /// The explicit-view Load overload permits a separately proven runtime view.</summary>
    public static OriginalModelGbufferSourceDraw LoadSourceTextureViews(OriginalGameRepository repository,
        LoadedModelTextureResources model, LoadedObjects shaderObjects, LoadedRenderGraph graph,
        int explicitGraphNode, int drawIndex, int explicitCombinationIndex,
        OriginalModelStageTextureRuntime explicitTextureRuntime)
        => LoadCore(repository, model, shaderObjects, graph, explicitGraphNode, drawIndex,
            explicitCombinationIndex, explicitTextureRuntime, null);

    /// <summary>Original non-named group branch gives wrapper count zero. Its
    /// type17 input selects the original matrix row, inserted into dimension2
    /// by source combination arithmetic. The other six dimensions and pass are
    /// supplied explicitly; named wrappers and absent type17 are rejected.</summary>
    public static OriginalModelGbufferSourceDraw LoadStaticSourceMatrixBinding(OriginalGameRepository repository,
        LoadedModelTextureResources model, LoadedObjects shaderObjects, LoadedRenderGraph graph,
        int explicitGraphNode, int drawIndex, ReadOnlySpan<uint> explicitOtherSixDimensions,
        uint explicitPassIndex, OriginalModelStageTextureRuntime explicitTextureRuntime)
    {
        if (explicitOtherSixDimensions.Length != 6)
            throw new InvalidDataException("Original model source draw requires six other explicit combination dimensions.");
        return LoadCore(repository, model, shaderObjects, graph, explicitGraphNode, drawIndex,
            null, explicitTextureRuntime, null, explicitOtherSixDimensions.ToArray(), explicitPassIndex);
    }

    private static OriginalModelGbufferSourceDraw LoadCore(OriginalGameRepository repository,
        LoadedModelTextureResources model, LoadedObjects shaderObjects, LoadedRenderGraph graph,
        int explicitGraphNode, int drawIndex, int? explicitCombinationIndex,
        OriginalModelStageTextureRuntime explicitTextureRuntime,
        IReadOnlyDictionary<uint,uint>? explicitNativeTextureViews,
        uint[]? explicitOtherSixDimensions = null, uint? explicitPassIndex = null)
    {
        ArgumentNullException.ThrowIfNull(repository); ArgumentNullException.ThrowIfNull(model);
        if (!Convert.ToHexString(SHA256.HashData(model.Model.Source.Bytes)).Equals(model.Model.Source.DecodedSha256,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Original model source draw model hash changed.");
        var canonicalGeometry = G1ModelGeometryReader.Read(model.Model.Source.Bytes,model.Model.Source.Bytes.LongLength);
        model = model with {Model = new(canonicalGeometry,G1ModelMaterialReader.Read(canonicalGeometry),model.Model.Source)};
        var bindings = OriginalModelShaderResources.LoadBindings(repository, model);
        var selected = OriginalModelShaderResources.ResolveRenderGraphNodeBaseSource(
            bindings, drawIndex, shaderObjects, graph, explicitGraphNode);
        var provider = selected.Interface.Provider ??
            throw new InvalidDataException("Selected original model graph state has a null shader provider.");
        var shader = OriginalModelShaderResources.LoadDeclaredG1S(repository, provider);
        OriginalModelMatrixBindingSource? matrix = null;
        if (explicitCombinationIndex is null)
        {
            if (explicitOtherSixDimensions is null || explicitPassIndex is null)
                throw new InvalidDataException("Original model source draw has no explicit combination or source matrix selection inputs.");
            var input = OriginalModelMatrixBindingSourceReader.PrepareUnboundGroup(bindings,drawIndex) ??
                throw new InvalidDataException("Original draw has no type17 matrix input; previous runtime selection must be explicit.");
            matrix = OriginalModelMatrixBindingSourceReader.Select(shader,input);
            explicitCombinationIndex = matrix.ResolveCombination(explicitOtherSixDimensions,explicitPassIndex.Value).Index;
        }
        int combinationIndex = explicitCombinationIndex.Value;
        var plan = OriginalModelStageTextures.Resolve(model, drawIndex, provider, shader,
            combinationIndex, explicitTextureRuntime);
        if (explicitNativeTextureViews is not null &&
            !explicitNativeTextureViews.Keys.Order().SequenceEqual(plan.Bindings.Select(b=>b.TextureRegister).Order()))
            throw new InvalidDataException("Original model source draw requires explicit native views for exactly its selected source PS texture registers.");
        var combination = shader.ReadG1Bindings().Combinations.Rows[combinationIndex];
        if (combination.Stages.Any(s => s.StageCode is not (0 or 1) && s.Entry is not null))
            throw new InvalidDataException("Original model source draw only supports the selected original VS/PS pair.");
        var vertex = shader.ReadResourceDefinitions(combination.Stages.Single(s => s.StageCode == 1).Entry!.ProgramIndex);
        var pixel = shader.ReadResourceDefinitions(combination.Stages.Single(s => s.StageCode == 0).Entry!.ProgramIndex);
        if (vertex.Bindings.Any(b => b.InputType != 0))
            throw new InvalidDataException("Original model source draw VS resources require an explicit extended packet adapter.");
        var material = OriginalModelGbufferMaterialSourceReader.Read(shader,
            OriginalModelShaderResources.ResolveDrawMaterials(bindings, drawIndex));
        var textures = new List<OriginalModelGbufferTextureUse>();
        foreach (var binding in plan.Bindings)
        {
            var image = OriginalModelStageTextures.LoadImage(repository, model, plan, binding.TextureRegister);
            uint view = explicitNativeTextureViews is null ? OriginalModelStageTextures.ReadNativeViewFormat(image.Image) :
                explicitNativeTextureViews[binding.TextureRegister];
            ValidateView(image.Image.PixelFormat, view);
            byte[] sampler = image.Sampler.EncodeD3D11Descriptor();
            textures.Add(new("Texture", binding.TextureRegister, image.Container, binding.ImageIndex, view, sampler));
            textures.Add(new("Sampler", binding.SamplerRegister, image.Container, binding.ImageIndex, view, sampler));
        }
        return new(new(model.Model.Geometry, model.Model.Source), shader, selected, material,
            plan, combinationIndex, vertex, pixel, textures.ToArray(), matrix);
    }

    /// <summary>Complete vertex CBs permit caller-prepared rigid or skinned source
    /// profiles. Absent nMtrID and native specular retention read the caller's
    /// previous complete PS template; they never become fabricated defaults.</summary>
    public OriginalModelGbufferPreparedDraw Prepare(string instanceKey,
        IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> explicitCompleteVertexConstants,
        ReadOnlySpan<byte> explicitPreviousPixelTemplate, float[] explicitWorldToViewRows,
        uint explicitPreviousCachedMaterialFlags, bool explicitBackendAlphaEnabled,
        byte? explicitForcedAlphaReference, OriginalModelRasterInputs explicitRaster)
    {
        if (string.IsNullOrWhiteSpace(instanceKey)) throw new ArgumentException("Original model instance key is required.", nameof(instanceKey));
        ArgumentNullException.ThrowIfNull(explicitCompleteVertexConstants); ArgumentNullException.ThrowIfNull(explicitRaster);
        var constants = SnapshotConstants(VertexDefinitions, explicitCompleteVertexConstants);
        if (explicitPreviousPixelTemplate.Length != 80)
            throw new InvalidDataException("Original model source draw requires its complete previous 80-byte PS template.");
        var specular = OriginalModelGbufferMaterialSourceReader.PrepareSpecular(Material, explicitPreviousCachedMaterialFlags);
        var buffer = PixelDefinitions.Buffers.Single();
        int specOffset = checked((int)buffer.Variables.Single(v => v.Name.Text == "cSpec").StartOffset);
        int idOffset = checked((int)buffer.Variables.Single(v => v.Name.Text == "nMtrID").StartOffset);
        Vector4 previousSpecular = new(F32(explicitPreviousPixelTemplate, specOffset), F32(explicitPreviousPixelTemplate, specOffset+4),
            F32(explicitPreviousPixelTemplate, specOffset+8), F32(explicitPreviousPixelTemplate, specOffset+12));
        int materialId = Material.MaterialId ?? BinaryPrimitives.ReadInt32LittleEndian(explicitPreviousPixelTemplate.Slice(idOffset,4));
        bool alpha = buffer.Variables.Any(v => v.Name.Text == "vATest");
        float? threshold = alpha ? OriginalModelGbufferMaterialSourceReader.PrepareAlphaTest(
            Material.SourceAlphaReference, explicitBackendAlphaEnabled, explicitForcedAlphaReference) : null;
        var pixel = OriginalModelGbufferConstants.Pixel(PixelDefinitions, explicitPreviousPixelTemplate,
            new(specular.Value ?? previousSpecular, explicitWorldToViewRows, materialId, threshold));
        var packet = new OriginalModelGbufferInstancePacket(instanceKey, Model, Shader, ExplicitCombinationIndex, DrawIndex,
            new(constants, [], []), new(new Dictionary<uint,ReadOnlyMemory<byte>> {[0] = pixel.Bytes},
                _pixelTextures.Select(t => t with {SamplerDescriptor52 = t.SamplerDescriptor52.ToArray()}).ToArray(), []), explicitRaster);
        return new(packet, specular.NextCachedFlags, specular, pixel.Writes);
    }

    /// <summary>Named rigid matrix writes reject the 6256-byte skinned profile.
    /// Bone matrices and bSkin for that profile use the complete-CB Prepare path.</summary>
    public OriginalModelGbufferPreparedDraw PrepareRigid(string instanceKey,
        ReadOnlySpan<byte> explicitVertexTemplate, OriginalModelGbufferVertexInputs explicitVertexInputs,
        ReadOnlySpan<byte> explicitPreviousPixelTemplate, float[] explicitWorldToViewRows,
        uint explicitPreviousCachedMaterialFlags, bool explicitBackendAlphaEnabled,
        byte? explicitForcedAlphaReference, OriginalModelRasterInputs explicitRaster)
    {
        var vertex = OriginalModelGbufferConstants.Vertex(VertexDefinitions, explicitVertexTemplate, explicitVertexInputs);
        return Prepare(instanceKey, new Dictionary<uint,ReadOnlyMemory<byte>> {[0] = vertex.Bytes},
            explicitPreviousPixelTemplate, explicitWorldToViewRows, explicitPreviousCachedMaterialFlags,
            explicitBackendAlphaEnabled, explicitForcedAlphaReference, explicitRaster);
    }

    private static Dictionary<uint,ReadOnlyMemory<byte>> SnapshotConstants(DxbcResourceDefinitions definitions,
        IReadOnlyDictionary<uint,ReadOnlyMemory<byte>> constants)
    {
        var bindings = definitions.Bindings.Where(b => b.InputType == 0).ToArray();
        if (constants.Count != bindings.Length) throw new InvalidDataException("Original model VS constants differ from the selected complete source CB inventory.");
        var result = new Dictionary<uint,ReadOnlyMemory<byte>>();
        foreach (var binding in bindings)
        {
            var buffer = definitions.Buffers.Single(b => b.Name.Text == binding.Name.Text);
            if (binding.BindCount != 1 || !constants.TryGetValue(binding.BindPoint, out var bytes) || bytes.Length != buffer.ByteSize)
                throw new InvalidDataException("Original model VS constants require the complete selected source CB.");
            result.Add(binding.BindPoint, bytes.ToArray());
        }
        return result;
    }

    private static float F32(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(offset,4));
    private static void ValidateView(byte pixelFormat, uint view)
    {
        if (OriginalModelTextureFormatMath.Resolve(pixelFormat,0).ShaderViewFormat == view) return;
        try { if (OriginalModelTextureFormatMath.Resolve(pixelFormat,1).ShaderViewFormat == view) return; }
        catch (InvalidDataException) { }
        throw new InvalidDataException("Explicit original model texture view has no verified source format row.");
    }
}
