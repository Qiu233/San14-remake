using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>A final source descriptor key, after its selected producer and
/// plugin writes. It is not an observation of a current original GPU state.</summary>
public sealed record DeferredSamplerSourceInput(uint Register,string ResourceName,
    string SourceResourceUri,string SourceIdentity,byte[] SourceKey);
public sealed record DeferredSamplerSourceBinding(uint Register,string SamplerName,
    uint TextureRegister,string TextureName,string SourceResourceUri,string SourceIdentity,
    G1TextureSamplerRecipe Recipe);
public sealed record LoadedDeferredSamplerSources(LoadedShaderContainer Shader,
    LoadedDeferredSamplerNativeSource Native,LoadedDeferredTexturePlan TexturePlan,
    IReadOnlyList<DeferredSamplerSourceBinding> Bindings)
{
    /// <summary>Original null SRVs whose texture and sampler declarations are
    /// removed by the proven source-null shader specialization.</summary>
    public IReadOnlyList<DeferredSourceNull2D> SourceNulls { get; init; }=[];
    /// <summary>The separately proved fresh selected Camera/ClusterLightList
    /// null t0 state. Its removed sampler has native backend-default metadata
    /// and no selected descriptor key or device sampler handle.</summary>
    public DeferredSourceNullIes? FreshSourceIesNull { get; init; }
}

public static partial class RenderSamplerSourceAdapters
{
    private static readonly (uint Register,string Texture,uint Dimension,short Encoded)[] ConsumerRows=
    [ (0,"sIES",5,32),(1,"sVolumetricFogMap",8,52),(2,"sDepthMap",4,35),
      (3,"sSplShadow",10,46),(4,"sSslShadow",5,51),(5,"sGxAmbLut",4,54),
      (6,"sHatchingShadow",4,36),(7,"sAmbSpc",9,27),(8,"sTrnsSpc",4,34),
      (9,"sTrnsDpt",4,4),(10,"sRamp",4,16),(11,"sDepth",4,8),
      (15,"sGBuf3",5,131) ];

    public static DeferredSamplerSourceInput FromRetainedImage(LoadedDeferredSamplerNativeSource native,
        uint register,LoadedTextureContainer source,int imageIndex)
    {
        ArgumentNullException.ThrowIfNull(source);RequireResourceIdentity(source.Source);
        var container=G1TextureContainerReader.Read(source.Source.Bytes);
        if((uint)imageIndex>=container.Textures.Count)throw new InvalidDataException("Deferred source sampler image is absent.");
        G1TextureImage image=container.Textures[imageIndex];
        G1TextureSamplerRecipe recipe=G1TextureSamplerReader.ReadRetainedSource(image);
        G1TextureSamplerRecipe converted=DecodeKey(native,recipe.RawKey);
        if(!converted.EncodeD3D11Descriptor().AsSpan().SequenceEqual(recipe.EncodeD3D11Descriptor()))
            throw new InvalidDataException("Deferred G1T sampler differs from the verified native key conversion.");
        var row=Row(register);
        uint dimension=image.TextureSystem switch{0=>4u,2=>8u,3=>5u,
            _=>throw new InvalidDataException("Deferred retained image uses an unresolved original texture system.")};
        if(dimension!=row.Dimension)throw new InvalidDataException("Deferred source image dimension differs from its exact consumer.");
        return Input(register,source.Source.ResourceUri+"#image/"+imageIndex,source.Source.DecodedSha256,recipe.RawKey);
    }

    public static DeferredSamplerSourceInput FromRenderTexture(LoadedDeferredSamplerNativeSource native,
        uint register,RenderTextureDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);RequireResourceIdentity(source.Source.Source);
        var original=new LoadedObjects(DokReader.Read(source.Source.Source.Bytes),source.Source.Source);
        RenderTextureDefinition parsed=RenderTextureResources.Read(original,source.Object.ObjectId);
        if(parsed.Object.ClassId!=source.Object.ClassId||parsed.Object.Kind!=source.Object.Kind||
            (parsed with{Source=source.Source,Object=source.Object})!=source)
            throw new InvalidDataException("Deferred render sampler definition differs from its original typed fields.");
        var row=Row(register);uint dimension=source.Kind==RenderTextureKind.Texture2D?4u:5u;
        if(dimension!=row.Dimension)throw new InvalidDataException("Deferred render sampler source dimension differs from its consumer.");
        return Input(register,source.Source.Source.ResourceUri+"#object/"+source.Object.ObjectId.ToString("X8"),
            source.Source.Source.DecodedSha256,FinalRenderTextureKey(native,source));
    }

    public static DeferredSamplerSourceInput FromAmbientCube(LoadedDeferredSamplerNativeSource native,
        CubeRenderTextureDefinition source)
    {
        ArgumentNullException.ThrowIfNull(source);RequireResourceIdentity(source.Source.Source);
        var original=new LoadedObjects(DokReader.Read(source.Source.Source.Bytes),source.Source.Source);
        var parsed=CubeRenderTextureResources.Read(original,source.Object.ObjectId);
        if(parsed.Object.ClassId!=source.Object.ClassId||parsed.Object.Kind!=source.Object.Kind||
            (parsed with{Source=source.Source,Object=source.Object})!=source)
            throw new InvalidDataException("Deferred ambient cube sampler definition differs from its original typed fields.");
        return Input(7,source.Source.Source.ResourceUri+"#object/"+source.Object.ObjectId.ToString("X8")+"/ambient-address-write",
            source.Source.Source.DecodedSha256,AmbientSpecularKey(CubeTextureKey(native,source.LevelNum)));
    }

    /// <summary>A provider which has followed the original final descriptor
    /// selection may retain its complete key here. The provider owns proving
    /// selection and plugin writes; no shader role supplies missing key bits.</summary>
    public static DeferredSamplerSourceInput FromProducerKey(LoadedDeferredSamplerNativeSource native,
        uint register,LoadedRdbAsset source,string sourceFragment,ReadOnlySpan<byte> originalFinalKey)
    {
        ArgumentNullException.ThrowIfNull(source);RequireResourceIdentity(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFragment);
        _=DecodeKey(native,originalFinalKey);
        return Input(register,source.ResourceUri+"#"+sourceFragment,source.DecodedSha256,originalFinalKey);
    }

    /// <summary>Source SubPoint/SubSpotLightShadows preflight. Its Filter value
    /// controls comparison; positive native clamped count replaces all filter
    /// codes. Zero count retains the supplied descriptor's original low bits.</summary>
    public static byte[] SubShadowKey(ReadOnlySpan<byte> originalKey,byte sourceFilter,uint nativeClampedCount)
    {
        if(originalKey.Length!=8||sourceFilter>2)throw new InvalidDataException("Sub-shadow sampler requires its complete key and original Filter0..2.");
        byte[] result=originalKey.ToArray();ushort lo=BinaryPrimitives.ReadUInt16LittleEndian(result),hi=BinaryPrimitives.ReadUInt16LittleEndian(result.AsSpan(2));
        hi=(ushort)((hi&0xFFF7)|(sourceFilter==2?8:0));
        if(nativeClampedCount>0)lo=(ushort)((lo&0x03FF)|(sourceFilter==2?0x1400:0));
        BinaryPrimitives.WriteUInt16LittleEndian(result,lo);BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(2),hi);return result;
    }

    /// <summary>The caller supplies the actual selected source cube-array and
    /// native clamped count. Locked9D8E811F SubPointLightShadows.Filter is2.</summary>
    public static DeferredSamplerSourceInput FromSubPointCubeArray(LoadedDeferredSamplerNativeSource native,
        LoadedObjects selectedSource,uint selectedObjectId,uint nativeClampedCount)
        =>FromProducerKey(native,3,selectedSource.Source,
            $"object/{selectedObjectId:X8}/CubeArrayRT-constructor/SubPointLightShadows-Filter2/count-{nativeClampedCount}",
            SubShadowKey(FinalCubeArrayTextureKey(native,selectedSource,selectedObjectId),2,nativeClampedCount));

    /// <summary>The caller supplies the actual selected source RT2DArray and
    /// native clamped count. Locked9D8E811F SubSpotLightShadows.Filter is2.</summary>
    public static DeferredSamplerSourceInput FromSubSpotRenderArray(LoadedDeferredSamplerNativeSource native,
        RenderTextureDefinition selectedSource,uint nativeClampedCount)
    {
        var original=FromRenderTexture(native,4,selectedSource);
        return original with{SourceKey=SubShadowKey(original.SourceKey,2,nativeClampedCount),
            SourceResourceUri=original.SourceResourceUri+$"/SubSpotLightShadows-Filter2/count-{nativeClampedCount}"};
    }

    /// <summary>Complete explicit combo0 contract. D1 inherits its source target,
    /// GGX its native generator wrapper. s15 inherits the actual base preparation
    /// path for RR (encoded83 -> J+960 -> J+100 -> PS slot15); its RDEF name remains
    /// ShadowPointSampler. The fourteen declared source plugins retain this
    /// descriptor/key; extra caller-added plugins require their separate plan.</summary>
    public static LoadedDeferredSamplerSources Resolve(LoadedShaderContainer shader,
        LoadedDeferredSamplerNativeSource native,LoadedDeferredTexturePlan textures,
        IReadOnlyList<DeferredSamplerSourceInput> selectedProducerInputs,
        IReadOnlyList<DeferredSourceNull2D>? explicitSourceNulls=null,
        DeferredSourceNullIes? explicitSourceIesNull=null)
    {
        ArgumentNullException.ThrowIfNull(shader);ArgumentNullException.ThrowIfNull(native);
        ArgumentNullException.ThrowIfNull(textures);ArgumentNullException.ThrowIfNull(selectedProducerInputs);
        ValidateShader(shader);
        var nulls=(explicitSourceNulls??[]).ToArray();
        if(nulls.Any(n=>n is null)||nulls.Select(n=>n.PixelRegister).Distinct().Count()!=nulls.Length)
            throw new InvalidDataException("Deferred sampler null source proofs are absent or duplicated.");
        foreach(var proof in nulls)proof.Validate();
        var removed=nulls.Select(n=>n.PixelRegister).ToHashSet();
        if(explicitSourceIesNull is not null){explicitSourceIesNull.Validate();removed.Add(0);}
        if(textures.Shader.Source.ResourceUri!=shader.Source.ResourceUri||textures.Shader.Source.FileId!=shader.Source.FileId||
            textures.ResourceBindings.Combination.Index!=0||textures.Inputs.Count!=5)
            throw new InvalidDataException("Deferred sampler texture plan differs from the exact original combo0 source.");
        uint[] external=new uint[]{0,1,2,3,4,6,7,8,9,10}.Where(r=>!removed.Contains(r)).ToArray();
        if(selectedProducerInputs.Count!=external.Length||selectedProducerInputs.Any(i=>i is null)||
            !selectedProducerInputs.Select(i=>i.Register).Order().SequenceEqual(external))
            throw new InvalidDataException("Deferred samplers require every selected source producer for s0..4/s6..10 exactly once.");
        var input=selectedProducerInputs.ToDictionary(i=>i.Register);
        DeferredTextureInput DepthOrRr(string role,short encoded,uint register,uint objectId)
        {
            var row=textures.Inputs.SingleOrDefault(i=>i.Role==role);
            if(row is null||row.EncodedParameter!=encoded||row.ResourceMapping.PrimaryEncoded!=encoded||
                row.ResourceMapping.SlotIndex!=(short)register||row.PixelBinding.BindPoint!=register||row.Texture.Object.ObjectId!=objectId)
                throw new InvalidDataException("Deferred sampler base preparation source differs from the original role/encoded/slot/object chain.");
            return row;
        }
        var depth=DepthOrRr("D1",8,11,0xE13DDB4F);var rr=DepthOrRr("RR",131,15,0x85C125A6);
        input.Add(11,FromRenderTexture(native,11,depth.Texture));
        input.Add(15,FromRenderTexture(native,15,rr.Texture));
        input.Add(5,Input(5,native.ResourceUri+"/generated-ggx-wrapper/mip1",native.RenderRuleIdentity,RenderTextureKey(native,1)));
        var declarations=shader.ReadResourceDefinitions(1).Bindings.Where(b=>b.InputType==3).ToArray();
        var mappings=shader.ReadG1ResourceBindings(0);var output=new List<DeferredSamplerSourceBinding>(13);
        foreach(var row in ConsumerRows)
        {
            if(removed.Contains(row.Register))continue;
            string name=row.Register==15?"__smpsShadowPointSampler":"__smp"+row.Texture;
            var declaration=declarations.SingleOrDefault(d=>d.BindPoint==row.Register);
            var mapping=mappings.Entries.SingleOrDefault(d=>d.Name==row.Texture);
            if(declaration is null||declaration.Name.Text!=name||declaration.BindCount!=1||declaration.Dimension!=0||
                mapping is null||mapping.PrimaryEncoded!=row.Encoded||mapping.SlotIndex!=(short)row.Register)
                throw new InvalidDataException("Deferred sampler original name/register/encoded preparation mapping differs.");
            var chosen=input[row.Register];
            if(chosen.ResourceName!=row.Texture||string.IsNullOrWhiteSpace(chosen.SourceResourceUri)||string.IsNullOrWhiteSpace(chosen.SourceIdentity))
                throw new InvalidDataException("Deferred sampler selected producer identity differs from its source consumer.");
            var recipe=DecodeKey(native,chosen.SourceKey);
            if(((recipe.Filter&0x80)!=0)!=(row.Register is 3 or 4))
                throw new InvalidDataException("Deferred source sampler comparison enable differs from its executable declaration.");
            output.Add(new(row.Register,name,row.Register,row.Texture,chosen.SourceResourceUri,chosen.SourceIdentity,recipe));
        }
        return new(shader,native,textures,output.AsReadOnly()){
            SourceNulls=Array.AsReadOnly(nulls),FreshSourceIesNull=explicitSourceIesNull};
    }

    private static DeferredSamplerSourceInput Input(uint register,string uri,string identity,ReadOnlySpan<byte> key)
    {
        var row=Row(register);if(key.Length!=8)throw new InvalidDataException("Deferred sampler source key is incomplete.");
        return new(register,row.Texture,uri,identity,key.ToArray());
    }
    private static (uint Register,string Texture,uint Dimension,short Encoded) Row(uint register)
    {
        foreach(var row in ConsumerRows)if(row.Register==register)return row;
        throw new InvalidDataException("Register is outside the complete original deferred sampler inventory.");
    }
    private static void RequireResourceIdentity(LoadedRdbAsset source)
    {
        if(source.OverrideSource is not null||string.IsNullOrWhiteSpace(source.ResourceUri))
            throw new InvalidDataException("Deferred sampler resource requires its original source URI.");
    }
    private static void ValidateShader(LoadedShaderContainer source)
    {
        RequireResourceIdentity(source.Source);
        if(source.Source.FileId!=0x9D8E811F||source.Source.ResourceUri!="san14://rdb/system/9D8E811F"||
            !source.Container.StageTables.Single(t=>t.StageCode==0).Entries.Any(e=>e.ProgramIndex==1)||
            source.ReadResourceDefinitions(1).Bindings.Count!=42)
            throw new InvalidDataException("Deferred sampler source differs from the exact original PS1 identity.");
    }
}
