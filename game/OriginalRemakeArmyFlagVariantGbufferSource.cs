using System.Buffers.Binary;
using System.Numerics;
using San14.Data;
using San14.Data.Formats;

public sealed record OriginalRemakeArmyFlagVariantGbufferInputs(float CurrentTimeSeconds,
    OriginalUnitWorldTransformState World, OriginalDeferredCameraInputs Camera,
    ReadOnlyMemory<byte> CompleteVertexTemplate, ReadOnlyMemory<byte> PreviousPixelTemplate,
    uint PreviousCachedMaterialFlags, OriginalModelRasterInputs Raster,
    IReadOnlyDictionary<uint,OriginalRemakeArmyFlagRuntimeTexture> ProducerImages,
    float ExplicitSpecularW, string ExplicitSpecularSource, bool ExplicitBackendAlphaEnabled,
    byte? ExplicitForcedAlphaReference=null)
{
    public OriginalNativeArmyFlagMaterialTextureGroupRecord? ActualCurrentTextureGroup { get; init; }
    public ReadOnlyMemory<byte>? ActualMaterialRecord64 { get; init; }
    public int? ActualMaterialId { get; init; }
    public string? ActualMaterialParameterSource { get; init; }
}

public sealed record OriginalRemakeArmyFlagVariantPreparedDraw(
    OriginalModelGbufferInstancePacket Packet, OriginalArmyAnimationResolvedFrame Animation,
    OriginalArmyNativePaletteRows Palette, OriginalArmyFlagNativeMaterial NativeMaterial,
    uint NextCachedMaterialFlags);

/// <summary>The actual ColorVariation replacement material, shader and GSTK
/// registrations for one cloth draw. Its primary is the producer-owned source
/// render image. Both the explicit force variant and material constructor caller
/// argument remain distinct from unknown current original state.</summary>
public sealed class OriginalRemakeArmyFlagVariantGbufferSource
{
    private readonly OriginalGameRepository _repository;
    private readonly OriginalArmyAnimationRuntime _runtime;
    private readonly byte[] _locals;
    private readonly OriginalArmyFlagNativeMaterialSource _materialSource;
    private readonly Dictionary<int,LoadedOriginalArmyFlagMaterialImage> _images=[];
    public OriginalRemakeArmyFlagSource Source { get; }
    public LoadedOriginalArmyFlagMaterialVariation Variation { get; }
    public OriginalModelShaderInterfaceSource SelectedShaderInterface { get; }
    public LoadedShaderContainer Shader { get; }
    public IReadOnlyList<OriginalArmyFlagMaterialTextureUse> TextureUses { get; }
    public int DrawIndex { get; }
    public int PaletteIndex { get; }
    public IReadOnlyList<uint> RequiredProducerProxyIds=>TextureUses.Where(t=>t.Slot.RequiresProducerImage)
        .Select(t=>t.Slot.ProxyObjectId).Distinct().ToArray();

    public static OriginalRemakeArmyFlagVariantGbufferSource Load(OriginalGameRepository repository,
        OriginalRemakeArmyFlagSource source,int drawIndex,int explicitOwnedForceVariant)
        =>new(repository,source,drawIndex,explicitOwnedForceVariant,null);

    public static OriginalRemakeArmyFlagVariantGbufferSource LoadForActualFinalTable(OriginalGameRepository repository,
        OriginalRemakeArmyFlagSource source,int drawIndex,int explicitOwnedForceVariant,
        OriginalNativeWorldShaderTableState actualFinalTable)
    {
        ArgumentNullException.ThrowIfNull(actualFinalTable);
        actualFinalTable.Registry.RequireCurrent(actualFinalTable);
        return new(repository,source,drawIndex,explicitOwnedForceVariant,
            actualFinalTable.Registry.ReadSelectedInterface(actualFinalTable,18));
    }

    public static OriginalRemakeArmyFlagVariantGbufferSource LoadForActualFinalTable(OriginalGameRepository repository,
        OriginalRemakeArmyFlagSource source,int drawIndex,LoadedOriginalArmyFlagMaterialVariation actualVariation,
        OriginalNativeWorldShaderTableState actualFinalTable)
    {
        ArgumentNullException.ThrowIfNull(actualFinalTable); ArgumentNullException.ThrowIfNull(actualVariation);
        actualFinalTable.Registry.RequireCurrent(actualFinalTable);
        return new(repository,source,drawIndex,actualVariation.OwnedForceVariant,
            actualFinalTable.Registry.ReadSelectedInterface(actualFinalTable,18),actualVariation);
    }

    private OriginalRemakeArmyFlagVariantGbufferSource(OriginalGameRepository repository,
        OriginalRemakeArmyFlagSource source,int drawIndex,int forceVariant,
        OriginalModelShaderInterfaceSource? actualSelectedShader,
        LoadedOriginalArmyFlagMaterialVariation? actualVariation=null)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(source);
        var group=source.ReadDeclaredMaterialGroups(repository).ReadDeclaredDraw(drawIndex);
        uint groupOrdinal=group.DeclaredContainerGroupOrdinal??throw Error("actual source draw has no declared World material group");
        int manager=OriginalArmyFlagMaterialVariations.ReadManagerForDeclaredGroup(source.Category.Category,groupOrdinal);
        _repository=repository;Source=source;DrawIndex=drawIndex;_runtime=new(repository);
        Variation=actualVariation??OriginalArmyFlagMaterialVariations.Load(repository,source.Catalog,manager,forceVariant);
        if(Variation.ManagerRow.ManagerKind!=manager||Variation.OwnedForceVariant!=forceVariant)
            throw Error("material variation belongs to another actual declared World group/force");
        SelectedShaderInterface=actualSelectedShader??OriginalModelShaderResources.ResolveType16Interface(Variation.ShaderOwner,18);
        var provider=SelectedShaderInterface.Provider??throw Error("actual final table has no state18 shader consumer");
        if(SelectedShaderInterface.StateIndex!=18)throw Error("actual final table needs its state18 interface");
        Shader=OriginalModelShaderResources.LoadDeclaredG1S(repository,provider);
        TextureUses=OriginalArmyFlagMaterialVariations.ResolveTextureUsesForActualShader(Variation,Shader);
        _materialSource=OriginalArmyFlagNativeMaterials.ReadSource(Variation);
        if(!Variation.ControllerGateNonzero||OriginalArmySkinVertexAdapter.Select(source.Model.Geometry,drawIndex)!=OriginalArmySkinVertexProfile.FourInfluenceUnsigned8)
            throw Error("source controller gate or four-influence geometry is unavailable");
        var stages=Shader.ReadG1Bindings().Combinations.Rows[1].Stages;
        if(stages.Single(row=>row.StageCode==1).Entry?.ProgramIndex!=1||
            stages.Single(row=>row.StageCode==0).Entry?.ProgramIndex!=2)
            throw Error("actual final shader does not have the supported VS1/PS2 skin profile");
        OriginalArmySkinConstants.Validate(Shader.ReadResourceDefinitions(1));
        var draw=G1ModelDrawReader.Read(source.Model.Geometry).Draws.Single(d=>d.Index==drawIndex);
        PaletteIndex=checked((int)draw.UnknownValue8);
        _locals=source.Transforms.Transforms.Joints.SelectMany(j=>j.RawBytes).ToArray();
    }

    public OriginalRemakeArmyFlagVariantPreparedDraw Prepare(string key,OriginalRemakeArmyFlagVariantGbufferInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);ArgumentNullException.ThrowIfNull(input.ProducerImages);
        if(string.IsNullOrWhiteSpace(key)||!float.IsFinite(input.CurrentTimeSeconds)||input.CurrentTimeSeconds<0||
            input.CompleteVertexTemplate.Length!=6256||input.PreviousPixelTemplate.Length!=80)
            throw Error("requires a key, nonnegative explicit clock and complete source CB templates");
        OriginalArmyFlagNativeMaterial material;
        if(input.ActualMaterialRecord64 is { } currentRecord)
        {
            if(currentRecord.Length!=64||input.ActualMaterialId is not int currentId||
                string.IsNullOrWhiteSpace(input.ActualMaterialParameterSource))
                throw Error("actual material record needs all64 bytes, its mapped ID and parameter source");
            material=new(currentRecord.ToArray(),BinaryPrimitives.ReadUInt32LittleEndian(currentRecord.Span.Slice(28,4)),
                new(F(currentRecord.Span,32),F(currentRecord.Span,36),F(currentRecord.Span,40),F(currentRecord.Span,44)),
                currentRecord.Span[62],currentId,Variation,input.ActualMaterialParameterSource);
        }
        else material=OriginalArmyFlagNativeMaterials.Construct(_materialSource,input.ExplicitSpecularW,input.ExplicitSpecularSource);
        var clip=Source.Action0.AnimationData.Single().Clip;
        float duration=clip.FrameCount/clip.FrameRate;
        if(!(duration>0)||!float.IsFinite(duration))throw Error("source action0 has no finite playback duration");
        var action=Source.FreshAction() with{CurrentTimeSeconds=input.CurrentTimeSeconds%duration};
        _=input.World.ShaderTransform();
        var root=new OriginalArmyAnimationRootTransform(new(input.World.Scale34,1),input.World.Quaternion24,new(input.World.Position18,1));
        var animation=_runtime.SampleResolvedPose(action,Source.Transforms,_locals,root);
        var palette=OriginalArmyNativePaletteWriter.Build(Source.Transforms.Transforms,PaletteIndex,
            animation.Current.BuildMatrices(),Source.Transforms.Transforms.Joints.Count,
            OriginalArmySkinConstants.ReadPalette(input.CompleteVertexTemplate.Span),OriginalArmyNativePoseBufferKind.Current);
        var m=input.Camera.SourceWorldToProjection;
        byte[] vertex=OriginalArmySkinConstants.Write(Shader.ReadResourceDefinitions(1),input.CompleteVertexTemplate.Span,
            [m.M11,m.M12,m.M13,m.M14,m.M21,m.M22,m.M23,m.M24,m.M31,m.M32,m.M33,m.M34,m.M41,m.M42,m.M43,m.M44],palette.Rows,[true,true,false]);
        OriginalArmySkinVertexAdapter.ValidateGates(vertex,OriginalArmySkinVertexProfile.FourInfluenceUnsigned8);
        var draw=OriginalModelShaderResources.ResolveDrawMaterials(Source.ShaderBindings,DrawIndex);
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
        var nativeSource=new OriginalModelGbufferMaterialSource(Shader,draw,modes,actualFlags,
            actualSpecular,actualAlphaReference,null,actualMaterialId);
        var spec=OriginalModelGbufferMaterialSourceReader.PrepareSpecular(nativeSource,input.PreviousCachedMaterialFlags);
        Vector4 oldSpec=new(F(input.PreviousPixelTemplate.Span,0),F(input.PreviousPixelTemplate.Span,4),
            F(input.PreviousPixelTemplate.Span,8),F(input.PreviousPixelTemplate.Span,12));
        var pixelDefinition=Shader.ReadResourceDefinitions(2);
        bool hasAlphaParameter=pixelDefinition.Buffers.Any(b=>b.Variables.Any(v=>v.Name.Text=="vATest"));
        float? alpha=hasAlphaParameter?OriginalModelGbufferMaterialSourceReader.PrepareAlphaTest(actualAlphaReference,
            input.ExplicitBackendAlphaEnabled,input.ExplicitForcedAlphaReference):null;
        var pixel=OriginalModelGbufferConstants.Pixel(pixelDefinition,input.PreviousPixelTemplate.Span,
            new(spec.Value??oldSpec,OriginalMapStaticModelTransforms.ToShaderAffineRows(input.Camera.SourceWorldToView),actualMaterialId,alpha));
        var owned=new List<OriginalModelGbufferTextureUse>();var borrowed=new List<OriginalModelDeviceBinding>();
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
                    _images.Add(use.Slot.Index,image=OriginalArmyFlagMaterialVariations.LoadStaticImage(_repository,use));
                byte[] sampler=image.Sampler.EncodeD3D11Descriptor();
                owned.Add(new("Texture",use.TextureRegister,image.Container,image.Image.Index,image.NativeViewFormat,sampler));
                owned.Add(new("Sampler",use.SamplerRegister,image.Container,image.Image.Index,image.NativeViewFormat,sampler));
            }
        }
        var packet=new OriginalModelGbufferInstancePacket(key,new(Source.Model.Geometry,Source.Model.Source),Shader,1,DrawIndex,
            new(new Dictionary<uint,ReadOnlyMemory<byte>>{[0]=vertex},[],[]),
            new(new Dictionary<uint,ReadOnlyMemory<byte>>{[0]=pixel.Bytes},owned,borrowed),input.Raster)
            {ArmySkinProfile=OriginalArmySkinVertexProfile.FourInfluenceUnsigned8};
        return new(packet,animation,palette,material,spec.NextCachedFlags);
    }
    private static float F(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(at,4));
    private static InvalidDataException Error(string message)=>new("Source-created Army flag variant draw "+message+".");
}
