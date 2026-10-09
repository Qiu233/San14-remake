using System.Buffers.Binary;
using System.Numerics;
using San14.Data;
using San14.Data.Formats;

/// <summary>Actual producer-owned runtime image and sampler at a source proxy
/// identity. Their owner must outlive cached native draws. No static neighbor
/// image is selected when a runtime result is missing.</summary>
public sealed record RemakeArmyFlagRuntimeTexture(uint ProxyObjectId,
    ModelDeviceBinding Texture, ModelDeviceBinding Sampler,
    string ProducerSource)
{
    public Action? RequireActualCurrentOwner { get; init; }
}

public sealed record RemakeArmyFlagGbufferInputs(float CurrentTimeSeconds,
    UnitWorldTransformState World, DeferredCameraInputs Camera,
    ReadOnlyMemory<byte> CompleteVertexTemplate, ReadOnlyMemory<byte> PreviousPixelTemplate,
    uint PreviousCachedMaterialFlags, ModelRasterInputs Raster,
    IReadOnlyDictionary<uint, RemakeArmyFlagRuntimeTexture> RuntimeTextures)
{
    public NativeArmyFlagMaterialTextureGroupRecord? ActualCurrentTextureGroup {get;init;}
    public ReadOnlyMemory<byte>? ActualMaterialRecord64 {get;init;}
    public int? ActualMaterialId {get;init;}
    public bool? ActualBackendAlphaEnabled {get;init;}
}

public sealed record RemakeArmyFlagPreparedDraw(ModelGbufferInstancePacket Packet,
    ArmyAnimationResolvedFrame Animation, ArmyNativePaletteRows Palette,
    uint NextCachedMaterialFlags);

/// <summary>One exact source flag draw. Static pole draws can be submitted with
/// their complete original texture graph. Cloth draws additionally require the
/// result of their actual runtime primary-image producer. Four stored/implicit
/// influences select bSkin=[true,true,false], independently of soldiers.</summary>
public sealed class RemakeArmyFlagGbufferSource
{
    private readonly GameResourceRepository _repository;
    private readonly ArmyAnimationRuntime _runtime;
    private readonly byte[] _locals;
    private readonly Dictionary<int, (LoadedTextureContainer Container, int Image, uint View, byte[] Sampler)> _images = [];
    public RemakeArmyFlagSource Source { get; }
    public LoadedShaderContainer Shader { get; }
    public ModelRenderGraphShaderSource GraphSource { get; }
    public ModelGbufferMaterialSource Material { get; }
    public IReadOnlyList<RemakeArmyFlagDrawTexture> Textures { get; }
    public int DrawIndex { get; }
    public int PaletteIndex { get; }
    public IReadOnlyList<uint> RequiredRuntimeProxyIds => Textures.Where(row => row.Slot.RequiresRuntimeTexture)
        .Select(row => row.Slot.ProxyObjectId).Distinct().ToArray();

    public static RemakeArmyFlagGbufferSource Load(GameResourceRepository repository,
        RemakeArmyFlagSource source, LoadedObjects shaderObjects, LoadedRenderGraph graph, int drawIndex)
        => new(repository, source, shaderObjects, graph, drawIndex);

    private RemakeArmyFlagGbufferSource(GameResourceRepository repository,
        RemakeArmyFlagSource source, LoadedObjects objects, LoadedRenderGraph graph, int drawIndex)
    {
        _repository = repository; Source = source; DrawIndex = drawIndex; _runtime = new(repository);
        GraphSource = ModelShaderResources.ResolveRenderGraphNodeBaseSource(source.ShaderBindings, drawIndex, objects, graph, 49);
        var provider = GraphSource.Interface.Provider ?? throw Error("source flag G-buffer dependency is absent");
        Shader = ModelShaderResources.LoadDeclaredG1S(repository, provider);
        Textures = source.DrawTextures(drawIndex, provider, Shader);
        var stages = Shader.ReadG1Bindings().Combinations.Rows[1].Stages;
        if (stages.Single(row => row.StageCode == 1).Entry?.ProgramIndex != 1 ||
            stages.Single(row => row.StageCode == 0).Entry?.ProgramIndex != 2 ||
            ArmySkinVertexAdapter.Select(source.Model.Geometry, drawIndex) != ArmySkinVertexProfile.FourInfluenceUnsigned8)
            throw Error("flag source shader/geometry differs from the four-influence VS1/PS2 profile");
        ArmySkinConstants.Validate(Shader.ReadResourceDefinitions(1));
        Material = ModelGbufferMaterialSourceReader.Read(Shader,
            ModelShaderResources.ResolveDrawMaterials(source.ShaderBindings, drawIndex));
        var draw = G1ModelDrawReader.Read(source.Model.Geometry).Draws.Single(row => row.Index == drawIndex);
        PaletteIndex = checked((int)draw.UnknownValue8);
        _locals = source.Transforms.Transforms.Joints.SelectMany(row => row.RawBytes).ToArray();
    }

    public bool HasCompleteTextures(IReadOnlyDictionary<uint, RemakeArmyFlagRuntimeTexture> runtimeTextures)
        => RequiredRuntimeProxyIds.All(runtimeTextures.ContainsKey);

    public RemakeArmyFlagPreparedDraw Prepare(string key, RemakeArmyFlagGbufferInputs input)
    {
        if (string.IsNullOrWhiteSpace(key) || input.CompleteVertexTemplate.Length != 6256 ||
            input.PreviousPixelTemplate.Length != 80 || !float.IsFinite(input.CurrentTimeSeconds) || input.CurrentTimeSeconds < 0)
            throw Error("requires an explicit key, complete CB templates and nonnegative caller clock");
        ArgumentNullException.ThrowIfNull(input.RuntimeTextures);
        if (input.ActualCurrentTextureGroup is null&&!HasCompleteTextures(input.RuntimeTextures))
            throw Error("runtime flag primary image producer has not supplied this source proxy");
        var animation = Source.FreshAction();
        var clip = Source.Action0.AnimationData.Single().Clip;
        float duration = clip.FrameCount / clip.FrameRate;
        if (!(duration > 0) || !float.IsFinite(duration)) throw Error("source flag action has no finite declared playback duration");
        animation = animation with { CurrentTimeSeconds = input.CurrentTimeSeconds % duration };
        _ = input.World.ShaderTransform();
        var root = new ArmyAnimationRootTransform(new(input.World.Scale34, 1),
            input.World.Quaternion24, new(input.World.Position18, 1));
        var resolved = _runtime.SampleResolvedPose(animation, Source.Transforms, _locals, root);
        var palette = ArmyNativePaletteWriter.Build(Source.Transforms.Transforms, PaletteIndex,
            resolved.Current.BuildMatrices(), Source.Transforms.Transforms.Joints.Count,
            ArmySkinConstants.ReadPalette(input.CompleteVertexTemplate.Span), ArmyNativePoseBufferKind.Current);
        var m = input.Camera.SourceWorldToProjection;
        byte[] vertex = ArmySkinConstants.Write(Shader.ReadResourceDefinitions(1), input.CompleteVertexTemplate.Span,
            [m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44],
            palette.Rows, [true,true,false]);
        ArmySkinVertexAdapter.ValidateGates(vertex, ArmySkinVertexProfile.FourInfluenceUnsigned8);
        var currentMaterial=Material;
        if(input.ActualMaterialRecord64 is {} actualRecord)
        {
            if(actualRecord.Length!=64||input.ActualMaterialId is not int actualId)
                throw Error("current container material needs complete64 byte parameters and the actual mapped ID");
            currentMaterial=new(Shader,Material.Draw,BinaryPrimitives.ReadUInt32LittleEndian(Shader.Container.RawHeader.AsSpan(0x54,4)),
                BinaryPrimitives.ReadUInt32LittleEndian(actualRecord.Span.Slice(28,4)),
                new(F(actualRecord.Span,32),F(actualRecord.Span,36),F(actualRecord.Span,40),F(actualRecord.Span,44)),
                actualRecord.Span[62],null,actualId);
        }
        var spec = ModelGbufferMaterialSourceReader.PrepareSpecular(currentMaterial, input.PreviousCachedMaterialFlags);
        var pixelDefinition = Shader.ReadResourceDefinitions(2);
        Vector4 oldSpec = new(F(input.PreviousPixelTemplate.Span,0),F(input.PreviousPixelTemplate.Span,4),
            F(input.PreviousPixelTemplate.Span,8),F(input.PreviousPixelTemplate.Span,12));
        int materialId = currentMaterial.MaterialId ?? BinaryPrimitives.ReadInt32LittleEndian(input.PreviousPixelTemplate.Span.Slice(64,4));
        bool hasAlphaParameter=pixelDefinition.Buffers.Any(b=>b.Variables.Any(v=>v.Name.Text=="vATest"));
        float? actualAlpha=hasAlphaParameter?
            ModelGbufferMaterialSourceReader.PrepareAlphaTest(currentMaterial.SourceAlphaReference,
                input.ActualBackendAlphaEnabled??throw Error("selected shader alpha parameter needs its actual backend state"),null):null;
        var pixel = ModelGbufferConstants.Pixel(pixelDefinition, input.PreviousPixelTemplate.Span,
            new(spec.Value ?? oldSpec, MapStaticModelTransforms.ToShaderAffineRows(input.Camera.SourceWorldToView), materialId, actualAlpha));
        var owned = new List<ModelGbufferTextureUse>();
        var borrowed = new List<ModelDeviceBinding>();
        if(input.ActualCurrentTextureGroup is {} actualTextureGroup)
        {
            var bindings=actualTextureGroup.PrepareActualPixelBindings(Shader);
            owned.AddRange(bindings.Owned);borrowed.AddRange(bindings.Borrowed);
        }
        else foreach (var use in Textures)
        {
            if (use.Slot.RequiresRuntimeTexture)
            {
                var resource = input.RuntimeTextures[use.Slot.ProxyObjectId];
                if (resource.ProxyObjectId != use.Slot.ProxyObjectId || string.IsNullOrWhiteSpace(resource.ProducerSource) ||
                    resource.Texture.DescriptorKind != "Texture" || resource.Sampler.DescriptorKind != "Sampler" ||
                    !resource.Texture.Resource.IsValid || resource.Texture.OwnerTexture is not { IsValid: true } ||
                    !resource.Sampler.Resource.IsValid || string.IsNullOrWhiteSpace(resource.Texture.SourceResourceUri))
                    throw Error("runtime primary image/sampler identity or owner is unavailable");
                borrowed.Add(resource.Texture with { SourceBindPoint = use.Register });
                borrowed.Add(resource.Sampler with { SourceBindPoint = use.SamplerRegister });
            }
            else
            {
                var image = StaticImage(use.Slot);
                owned.Add(new("Texture", use.Register, image.Container, image.Image, image.View, image.Sampler));
                owned.Add(new("Sampler", use.SamplerRegister, image.Container, image.Image, image.View, image.Sampler));
            }
        }
        var packet = new ModelGbufferInstancePacket(key, new(Source.Model.Geometry, Source.Model.Source), Shader, 1, DrawIndex,
            new(new Dictionary<uint, ReadOnlyMemory<byte>> { [0] = vertex }, [], []),
            new(new Dictionary<uint, ReadOnlyMemory<byte>> { [0] = pixel.Bytes }, owned, borrowed), input.Raster)
            { ArmySkinProfile = ArmySkinVertexProfile.FourInfluenceUnsigned8 };
        return new(packet, resolved, palette, spec.NextCachedFlags);
    }

    private (LoadedTextureContainer Container, int Image, uint View, byte[] Sampler) StaticImage(RemakeArmyFlagTextureSlot slot)
    {
        if (_images.TryGetValue(slot.Index, out var cached)) return cached;
        if (slot.RequiresRuntimeTexture || slot.StaticTextureFileId is not uint file || slot.RawTextureId is not uint tid)
            throw Error("a runtime primary texture cannot use a static image path");
        var container = _repository.LoadTextureContainer("MaterialEditor", file);
        int index = checked((int)(tid & 0x7FFFFFFF));
        if ((uint)index >= container.Container.Textures.Count) throw Error("source static proxy TID exceeds its G1T container");
        var image = container.Container.Textures[index];
        var value = (container, index, ModelStageTextures.ReadNativeViewFormat(image),
            G1TextureSamplerReader.ReadRetainedSource(image).EncodeD3D11Descriptor());
        _images.Add(slot.Index, value); return value;
    }
    private static float F(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(at,4));
    private static InvalidDataException Error(string message) => new($"Source-created Army flag draw {message}.");
}
