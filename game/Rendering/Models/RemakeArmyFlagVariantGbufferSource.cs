using System.Buffers.Binary;
using System.Numerics;
using San14.Data;
using San14.Data.Formats;

public sealed record RemakeArmyFlagVariantGbufferInputs(float CurrentTimeSeconds,
    UnitWorldTransformState World, DeferredCameraInputs Camera,
    ReadOnlyMemory<byte> CompleteVertexTemplate, ReadOnlyMemory<byte> PreviousPixelTemplate,
    uint PreviousCachedMaterialFlags, ModelRasterInputs Raster,
    IReadOnlyDictionary<uint,RemakeArmyFlagRuntimeTexture> ProducerImages,
    float ExplicitSpecularW, string ExplicitSpecularSource, bool ExplicitBackendAlphaEnabled,
    byte? ExplicitForcedAlphaReference=null)
{
    public NativeArmyFlagMaterialTextureGroupRecord? ActualCurrentTextureGroup { get; init; }
    public ReadOnlyMemory<byte>? ActualMaterialRecord64 { get; init; }
    public int? ActualMaterialId { get; init; }
    public string? ActualMaterialParameterSource { get; init; }
}

public sealed record RemakeArmyFlagVariantPreparedDraw(
    ModelGbufferInstancePacket Packet, ArmyAnimationResolvedFrame Animation,
    ArmyNativePaletteRows Palette, ArmyFlagNativeMaterial NativeMaterial,
    uint NextCachedMaterialFlags);

/// <summary>The actual ColorVariation replacement material, shader and GSTK
/// registrations for one cloth draw. Its primary is the producer-owned source
/// render image. Both the explicit force variant and material constructor caller
/// argument remain distinct from unknown current original state.</summary>
public sealed class RemakeArmyFlagVariantGbufferSource
{
    private readonly GameResourceRepository _repository;
    private readonly ArmyAnimationRuntime _runtime;
    private readonly byte[] _locals;
    private readonly ArmyFlagNativeMaterialSource _materialSource;
    private readonly Dictionary<int,LoadedArmyFlagMaterialImage> _images=[];
    public RemakeArmyFlagSource Source { get; }
    public LoadedArmyFlagMaterialVariation Variation { get; }
    public ModelShaderInterfaceSource SelectedShaderInterface { get; }
    public LoadedShaderContainer Shader { get; }
    public IReadOnlyList<ArmyFlagMaterialTextureUse> TextureUses { get; }
    public int DrawIndex { get; }
    public int PaletteIndex { get; }
    public IReadOnlyList<uint> RequiredProducerProxyIds=>TextureUses.Where(t=>t.Slot.RequiresProducerImage)
        .Select(t=>t.Slot.ProxyObjectId).Distinct().ToArray();

    public static RemakeArmyFlagVariantGbufferSource Load(GameResourceRepository repository,
        RemakeArmyFlagSource source,int drawIndex,int explicitOwnedForceVariant)
        =>new(repository,source,drawIndex,explicitOwnedForceVariant,null);

    public static RemakeArmyFlagVariantGbufferSource LoadForActualFinalTable(GameResourceRepository repository,
        RemakeArmyFlagSource source,int drawIndex,int explicitOwnedForceVariant,
        NativeWorldShaderTableState actualFinalTable)
    {
        ArgumentNullException.ThrowIfNull(actualFinalTable);
        actualFinalTable.Registry.RequireCurrent(actualFinalTable);
        return new(repository,source,drawIndex,explicitOwnedForceVariant,
            actualFinalTable.Registry.ReadSelectedInterface(actualFinalTable,18));
    }

    public static RemakeArmyFlagVariantGbufferSource LoadForActualFinalTable(GameResourceRepository repository,
        RemakeArmyFlagSource source,int drawIndex,LoadedArmyFlagMaterialVariation actualVariation,
        NativeWorldShaderTableState actualFinalTable)
    {
        ArgumentNullException.ThrowIfNull(actualFinalTable); ArgumentNullException.ThrowIfNull(actualVariation);
        actualFinalTable.Registry.RequireCurrent(actualFinalTable);
        return new(repository,source,drawIndex,actualVariation.OwnedForceVariant,
            actualFinalTable.Registry.ReadSelectedInterface(actualFinalTable,18),actualVariation);
    }

    private RemakeArmyFlagVariantGbufferSource(GameResourceRepository repository,
        RemakeArmyFlagSource source,int drawIndex,int forceVariant,
        ModelShaderInterfaceSource? actualSelectedShader,
        LoadedArmyFlagMaterialVariation? actualVariation=null)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(source);
        var group=source.ReadDeclaredMaterialGroups(repository).ReadDeclaredDraw(drawIndex);
        uint groupOrdinal=group.DeclaredContainerGroupOrdinal??throw Error("actual source draw has no declared World material group");
        int manager=ArmyFlagMaterialVariations.ReadManagerForDeclaredGroup(source.Category.Category,groupOrdinal);
        _repository=repository;Source=source;DrawIndex=drawIndex;_runtime=new(repository);
        Variation=actualVariation??ArmyFlagMaterialVariations.Load(repository,source.Catalog,manager,forceVariant);
        if(Variation.ManagerRow.ManagerKind!=manager||Variation.OwnedForceVariant!=forceVariant)
            throw Error("material variation belongs to another actual declared World group/force");
        SelectedShaderInterface=actualSelectedShader??ModelShaderResources.ResolveType16Interface(Variation.ShaderOwner,18);
        var provider=SelectedShaderInterface.Provider??throw Error("actual final table has no state18 shader consumer");
        if(SelectedShaderInterface.StateIndex!=18)throw Error("actual final table needs its state18 interface");
        Shader=ModelShaderResources.LoadDeclaredG1S(repository,provider);
        TextureUses=ArmyFlagMaterialVariations.ResolveTextureUsesForActualShader(Variation,Shader);
        _materialSource=ArmyFlagNativeMaterials.ReadSource(Variation);
        if(!Variation.ControllerGateNonzero||ArmySkinVertexAdapter.Select(source.Model.Geometry,drawIndex)!=ArmySkinVertexProfile.FourInfluenceUnsigned8)
            throw Error("source controller gate or four-influence geometry is unavailable");
        var stages=Shader.ReadG1Bindings().Combinations.Rows[1].Stages;
        if(stages.Single(row=>row.StageCode==1).Entry?.ProgramIndex!=1||
            stages.Single(row=>row.StageCode==0).Entry?.ProgramIndex!=2)
            throw Error("actual final shader does not have the supported VS1/PS2 skin profile");
        ArmySkinConstants.Validate(Shader.ReadResourceDefinitions(1));
        var draw=G1ModelDrawReader.Read(source.Model.Geometry).Draws.Single(d=>d.Index==drawIndex);
        PaletteIndex=checked((int)draw.UnknownValue8);
        _locals=source.Transforms.Transforms.Joints.SelectMany(j=>j.RawBytes).ToArray();
    }

    public RemakeArmyFlagVariantPreparedDraw Prepare(string key,RemakeArmyFlagVariantGbufferInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);ArgumentNullException.ThrowIfNull(input.ProducerImages);
        if(string.IsNullOrWhiteSpace(key)||!float.IsFinite(input.CurrentTimeSeconds)||input.CurrentTimeSeconds<0||
            input.CompleteVertexTemplate.Length!=6256||input.PreviousPixelTemplate.Length!=80)
            throw Error("requires a key, nonnegative explicit clock and complete source CB templates");
        ArmyFlagNativeMaterial material;
        if(input.ActualMaterialRecord64 is { } currentRecord)
        {
            if(currentRecord.Length!=64||input.ActualMaterialId is not int currentId||
                string.IsNullOrWhiteSpace(input.ActualMaterialParameterSource))
                throw Error("actual material record needs all64 bytes, its mapped ID and parameter source");
            material=new(currentRecord.ToArray(),BinaryPrimitives.ReadUInt32LittleEndian(currentRecord.Span.Slice(28,4)),
                new(F(currentRecord.Span,32),F(currentRecord.Span,36),F(currentRecord.Span,40),F(currentRecord.Span,44)),
                currentRecord.Span[62],currentId,Variation,input.ActualMaterialParameterSource);
        }
        else material=ArmyFlagNativeMaterials.Construct(_materialSource,input.ExplicitSpecularW,input.ExplicitSpecularSource);
        var clip=Source.Action0.AnimationData.Single().Clip;
        float duration=clip.FrameCount/clip.FrameRate;
        if(!(duration>0)||!float.IsFinite(duration))throw Error("source action0 has no finite playback duration");
        var action=Source.FreshAction() with{CurrentTimeSeconds=input.CurrentTimeSeconds%duration};
        _=input.World.ShaderTransform();
        var root=new ArmyAnimationRootTransform(new(input.World.Scale34,1),input.World.Quaternion24,new(input.World.Position18,1));
        var animation=_runtime.SampleResolvedPose(action,Source.Transforms,_locals,root);
        var palette=ArmyNativePaletteWriter.Build(Source.Transforms.Transforms,PaletteIndex,
            animation.Current.BuildMatrices(),Source.Transforms.Transforms.Joints.Count,
            ArmySkinConstants.ReadPalette(input.CompleteVertexTemplate.Span),ArmyNativePoseBufferKind.Current);
        var m=input.Camera.SourceWorldToProjection;
        byte[] vertex=ArmySkinConstants.Write(Shader.ReadResourceDefinitions(1),input.CompleteVertexTemplate.Span,
            [m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44],palette.Rows,[true,true,false]);
        ArmySkinVertexAdapter.ValidateGates(vertex,ArmySkinVertexProfile.FourInfluenceUnsigned8);
        var draw=ModelShaderResources.ResolveDrawMaterials(Source.ShaderBindings,DrawIndex);
        uint modes=BinaryPrimitives.ReadUInt32LittleEndian(Shader.Container.RawHeader.AsSpan(0x54,4));
        uint actualFlags=material.RawFlags; Vector4 actualSpecular=material.Specular;
        byte actualAlphaReference=material.AlphaReference; int actualMaterialId=input.ActualMaterialId??material.MaterialId;
        if(input.ActualMaterialRecord64 is { } actualRecord)
        {
            if(actualRecord.Length!=64)throw Error("actual type12 material record must contain all64 bytes");
            actualFlags=BinaryPrimitives.ReadUInt32LittleEndian(actualRecord.Span.Slice(28,4));
            actualSpecular=new(F(actualRecord.Span,32),F(actualRecord.Span,36),F(actualRecord.Span,40),F(actualRecord.Span,44));
            actualAlphaReference=actualRecord.Span[62];
        }
        var nativeSource=new ModelGbufferMaterialSource(Shader,draw,modes,actualFlags,
            actualSpecular,actualAlphaReference,null,actualMaterialId);
        var spec=ModelGbufferMaterialSourceReader.PrepareSpecular(nativeSource,input.PreviousCachedMaterialFlags);
        Vector4 oldSpec=new(F(input.PreviousPixelTemplate.Span,0),F(input.PreviousPixelTemplate.Span,4),
            F(input.PreviousPixelTemplate.Span,8),F(input.PreviousPixelTemplate.Span,12));
        var pixelDefinition=Shader.ReadResourceDefinitions(2);
        bool hasAlphaParameter=pixelDefinition.Buffers.Any(b=>b.Variables.Any(v=>v.Name.Text=="vATest"));
        float? alpha=hasAlphaParameter?ModelGbufferMaterialSourceReader.PrepareAlphaTest(actualAlphaReference,
            input.ExplicitBackendAlphaEnabled,input.ExplicitForcedAlphaReference):null;
        var pixel=ModelGbufferConstants.Pixel(pixelDefinition,input.PreviousPixelTemplate.Span,
            new(spec.Value??oldSpec,MapStaticModelTransforms.ToShaderAffineRows(input.Camera.SourceWorldToView),actualMaterialId,alpha));
        var owned=new List<ModelGbufferTextureUse>();var borrowed=new List<ModelDeviceBinding>();
        if(input.ActualCurrentTextureGroup is { } actualTextureGroup)
        {
            var actual=actualTextureGroup.PrepareActualPixelBindings(Shader);
            owned.AddRange(actual.Owned); borrowed.AddRange(actual.Borrowed);
        }
        else foreach(var use in TextureUses)
        {
            if(use.Slot.RequiresProducerImage)
            {
                if(!input.ProducerImages.TryGetValue(use.Slot.ProxyObjectId,out var image)||image.ProxyObjectId!=use.Slot.ProxyObjectId||
                    string.IsNullOrWhiteSpace(image.ProducerSource)||image.Texture.DescriptorKind!="Texture"||image.Sampler.DescriptorKind!="Sampler"||
                    !image.Texture.Resource.IsValid||image.Texture.OwnerTexture is not{IsValid:true}||!image.Sampler.Resource.IsValid||
                    string.IsNullOrWhiteSpace(image.Texture.SourceResourceUri))
                    throw Error("requires the actual producer-owned primary image and source RT sampler");
                borrowed.Add(image.Texture with{SourceBindPoint=use.TextureRegister});
                borrowed.Add(image.Sampler with{SourceBindPoint=use.SamplerRegister});
            }
            else
            {
                if(!_images.TryGetValue(use.Slot.Index,out var image))
                    _images.Add(use.Slot.Index,image=ArmyFlagMaterialVariations.LoadStaticImage(_repository,use));
                byte[] sampler=image.Sampler.EncodeD3D11Descriptor();
                owned.Add(new("Texture",use.TextureRegister,image.Container,image.Image.Index,image.NativeViewFormat,sampler));
                owned.Add(new("Sampler",use.SamplerRegister,image.Container,image.Image.Index,image.NativeViewFormat,sampler));
            }
        }
        var packet=new ModelGbufferInstancePacket(key,new(Source.Model.Geometry,Source.Model.Source),Shader,1,DrawIndex,
            new(new Dictionary<uint,ReadOnlyMemory<byte>>{[0]=vertex},[],[]),
            new(new Dictionary<uint,ReadOnlyMemory<byte>>{[0]=pixel.Bytes},owned,borrowed),input.Raster)
            {ArmySkinProfile=ArmySkinVertexProfile.FourInfluenceUnsigned8};
        return new(packet,animation,palette,material,spec.NextCachedFlags);
    }
    private static float F(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(at,4));
    private static InvalidDataException Error(string message)=>new("Source-created Army flag variant draw "+message+".");
}
