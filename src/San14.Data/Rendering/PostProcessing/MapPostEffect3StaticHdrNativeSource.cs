using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The original72B Static descriptor construction for a selected
/// single-mip HDR volume. The owner's current GPU view and frame lease
/// remain separate. Descriptor+38 is not the module's manager sampler+48.</summary>
public sealed class MapPostEffect3StaticHdrNativeRecipe
{
    private readonly G1TextureSamplerRecipe sampler;
    internal MapPostEffect3StaticHdrNativeRecipe(StaticTextureBinding binding,
        G1TextureSamplerRecipe sampler,uint resource,uint ordinary,uint alternate,ushort packed,LoadedDeferredSamplerNativeSource native,string evidence)
        =>(Binding,this.sampler,NativeResourceFormat,NativeShaderViewFormat,NativeAlternateShaderViewFormat,
            NativePackedDescriptor36,Native,SourceEvidence)=(binding,sampler,resource,ordinary,alternate,packed,native,evidence);
    public StaticTextureBinding Binding{get;}
    public LoadedDeferredSamplerNativeSource Native{get;}
    public string Locale=>Native.Locale;
    public uint SourceStaticObjectId=>Binding.Definition.ObjectId;
    public uint SourceTextureFileId=>Binding.Resource.Source.FileId;
    public uint SourceTid=>(uint)Binding.ImageIndex;
    public uint NativeResourceFormat{get;}
    public uint NativeShaderViewFormat{get;}
    public uint NativeAlternateShaderViewFormat{get;}
    public uint NativeShaderViewDimension=>8; // D3D11_SRV_DIMENSION_TEXTURE3D.
    public uint NativeFirstMipLevel=>0;
    public uint NativeShaderMipLevelCount=>1;
    public ushort NativePackedDescriptor36{get;}
    public ReadOnlyMemory<byte> NativeDescriptorSamplerKey38=>sampler.RawKey.ToArray();
    public ushort NativeDescriptorSamplerWord38=>BinaryPrimitives.ReadUInt16LittleEndian(sampler.RawKey);
    public byte NativeDescriptorSamplerLow4=>(byte)(NativeDescriptorSamplerWord38&15);
    public G1TextureSamplerRecipe Sampler=>sampler with {RawKey=sampler.RawKey.ToArray()};
    /// <summary>Source952010 explicitly zeroes+28. Both actual container
    /// metadata size and image record count are zero, so never replaces
    /// it. copies this null pointer into the Static CPU descriptor.</summary>
    public ReadOnlyMemory<byte>? NativeDescriptorMetadata28Chain=>null;
    public bool NativeDescriptorMetadata28IsProvenNull=>true;
    public uint NativeGlobalMetadataByteCount=>0;
    public uint NativeImageMetadataRecordCount=>0;
    public int NativeStaticDescriptorByteCount=>72;
    public bool RuntimeMipTrimmingCannotChangeThisSingleMip=>true;
    public bool IsModuleManagerSamplerWrapper=>false;
    public bool ActiveContextIdentityVerified=>false;
    public bool ProvesGpuTextureReady=>false;
    public string SourceEvidence{get;}
    // The physical source was verified during construction. A current request
    // checks its declaration/child identity; it does not reload or rehash it.
    public void RequireCurrentSource()=>LoadedMapPostEffect3StaticHdrNativeSource.ValidateLoadedBindingIdentity(Binding);
    public bool MatchesLoadedBinding(StaticTextureBinding binding)
        =>Binding.Definition.ObjectId==binding.Definition.ObjectId&&Binding.ImageIndex==binding.ImageIndex&&
            ReferenceEquals(Binding.Definitions.Source.Bytes,binding.Definitions.Source.Bytes)&&
            ReferenceEquals(Binding.Resource.Source.Bytes,binding.Resource.Source.Bytes)&&
            Binding.Definitions.Source.ResourceUri==binding.Definitions.Source.ResourceUri&&
            Binding.Definitions.Source.DecodedSha256==binding.Definitions.Source.DecodedSha256&&
            Binding.Resource.Source.ResourceUri==binding.Resource.Source.ResourceUri&&
            Binding.Resource.Source.DecodedSha256==binding.Resource.Source.DecodedSha256;
}

/// <summary>Hash-pinned native loader reconstruction for the original single-
/// mip system2 format01/03 family. Unknown layouts do not acquire a default
/// view format, key or null metadata proof.</summary>
internal readonly record struct StaticHdrFormatRule(uint Resource,uint LinearView,uint SrgbView);

public sealed class LoadedMapPostEffect3StaticHdrNativeSource
{
    private readonly StaticHdrFormatRule[] formats;
    private MapPostEffect3StaticHdrNativeRecipe? loadedRecipe;
    internal LoadedMapPostEffect3StaticHdrNativeSource(StaticHdrFormatRule[] formats,LoadedDeferredSamplerNativeSource native,string evidence)
        =>(this.formats,Native,SourceEvidence)=(formats,native,evidence);
    public LoadedDeferredSamplerNativeSource Native{get;}
    public string Locale=>Native.Locale;
    public string SourceEvidence{get;}
    public MapPostEffect3StaticHdrNativeRecipe ReadConstruction(StaticTextureBinding binding)
    {
        var image=ValidateBindingAtLoad(binding);
        var sampler=G1TextureSamplerReader.ReadRetainedSource(image);
        byte format=image.PixelFormat;
        ushort packed=(ushort)(format|(image.TextureSystem<<7)|(image.MipCount<<10)|((image.RawHeader[19]&1)<<15));
        var formatRule=formats[format];
        uint ordinary=formatRule.LinearView,alternate=formatRule.SrgbView;
        uint selected=(packed&0x8000)==0?ordinary:alternate;
        if(selected==0)throw new NotSupportedException("Selected original HDR volume has no native view for its source selector bit.");
        loadedRecipe=new(binding,sampler,formatRule.Resource,selected,alternate,packed,Native,SourceEvidence+
            $";Static{binding.Definition.ObjectId:X8}/context{binding.Resource.Source.FileId:X8}/TID{binding.ImageIndex};"+
            $"{binding.Resource.Source.ResourceUri}@{binding.Resource.Source.DecodedSha256};"+
            $"-descriptor38-key{Convert.ToHexString(sampler.RawKey)}/28-zero"+
            "-actual-globalMetadata0-and-imageMetadataCount0;-single-mip-no-trim"+
            $"-format{format:X2}/resource{formatRule.Resource}/selectedSRV{selected}/source-image19-selector"+
            "--exact72B;-ID16-null;module30/38-are-separate-manager-sampler-carriers");
        return loadedRecipe;
    }
    public MapPostEffect3StaticHdrNativeRecipe ReadSelectedCurrent(MapPostEffect3CurrentLutLookup actualLookup)
    {
        ArgumentNullException.ThrowIfNull(actualLookup);
        if(actualLookup.ExecutableLocale!=Locale||!actualLookup.IsHdrLookup||!actualLookup.LookupSucceeded||actualLookup.SourceObjectKey==0||
            actualLookup.ResolvedDeclaration is not{} declaration||
            actualLookup.SelectedStaticTexture is not{} binding)
            throw new InvalidDataException("Native Static HDR construction requires the actual successful HDR lookup and selected child.");
        if(actualLookup.SourceSelector>=declaration.ChildCount||
            declaration.ChildObjectKeys[(int)actualLookup.SourceSelector]!=binding.Definition.ObjectId)
            throw new InvalidDataException("Native Static HDR child differs from the actual LUT selector and ordered source array.");
        ValidateLoadedBindingIdentity(binding);
        if(loadedRecipe is{} recipe&&recipe.MatchesLoadedBinding(binding))return recipe;
        return ReadConstruction(binding);
    }
    internal static void ValidateLoadedBindingIdentity(StaticTextureBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        if(binding.Definition.Kind!=DokObjectKind.Inline||binding.Definition.ClassId!=0xFF7DBFD4||
            Field(binding.Definition,0x6C7321D2,5)!=binding.Resource.Source.FileId||
            Field(binding.Definition,0x00200571,4)!=binding.ImageIndex||
            (uint)binding.ImageIndex>=binding.Resource.Container.Textures.Count)
            throw new InvalidDataException("Static HDR binding differs from its explicit context/TID declaration.");
    }
    private static G1TextureImage ValidateBindingAtLoad(StaticTextureBinding binding)
    {
        ValidateLoadedBindingIdentity(binding);
        MapPostEffect3LutSource.ValidateCurrentSource(binding.Definitions);
        var objects=DokReader.Read(binding.Definitions.Source.Bytes).Objects.Where(o=>o.ObjectId==binding.Definition.ObjectId).ToArray();
        if(objects.Length!=1||objects[0].Kind!=DokObjectKind.Inline||objects[0].ClassId!=0xFF7DBFD4||
            Field(objects[0],0x6C7321D2,5)!=binding.Resource.Source.FileId||
            Field(objects[0],0x00200571,4)!=binding.ImageIndex)
            throw new InvalidDataException("Static HDR binding differs from its loaded original declaration bytes.");
        var source=binding.Resource.Source;
        if(source.ResourceUri!=GameResourceId.Rdb("RRPreview",source.FileId).ToString())
            throw new InvalidDataException("Static HDR bytes differ from their loaded original RRPreview source.");
        var container=G1TextureContainerReader.Read(source.Bytes);
        if(container.RawVersion!="0600"||container.Platform!=10||container.Textures.Count!=1||binding.ImageIndex!=0)
            throw new NotSupportedException("Native HDR construction requires the supported Windows0600 single-image volume family.");
        var image=container.Textures[binding.ImageIndex];
        if(image.TextureSystem!=2||image.PixelFormat is not(1 or 3)||image.MipCount!=1||image.LayerCount!=1||
            image.Depth<=1||image.SubresourceLayout!=G1TextureSubresourceLayout.VolumeMipChain||
            image.RawHeader.Length!=20||(image.RawHeader[7]>>4)!=1||
            !binding.Image.RawHeader.AsSpan().SequenceEqual(image.RawHeader)||
            binding.Image.Width!=image.Width||binding.Image.Height!=image.Height||binding.Image.Depth!=image.Depth||
            binding.Image.PixelFormat!=image.PixelFormat||binding.Image.MipCount!=image.MipCount)
            throw new NotSupportedException("Selected Static HDR image is outside the source-supported single-mip volume layout.");
        uint globalMetadata=BinaryPrimitives.ReadUInt32LittleEndian(source.Bytes.AsSpan(24));
        uint imageMetadata=BinaryPrimitives.ReadUInt32LittleEndian(image.RawHeader)>>28;
        if(globalMetadata!=0||imageMetadata!=0)
            throw new NotSupportedException("Selected Static HDR source contains metadata; its actual record chain must be reconstructed before use.");
        return image;
    }
    private static uint Field(DokObject obj,uint hash,ushort type)
    {
        var rows=obj.Properties.Where(p=>p.NameHash==hash).ToArray();
        if(rows.Length!=1||rows[0].TypeCode!=type||rows[0].ElementCount!=1||rows[0].ElementSize!=4||rows[0].RawValue.Length!=4)
            throw new InvalidDataException("Static HDR needs its exact scalar context/TID fields.");
        return BinaryPrimitives.ReadUInt32LittleEndian(rows[0].RawValue);
    }
}

public static class MapPostEffect3StaticHdrNativeSource
{
    public static LoadedMapPostEffect3StaticHdrNativeSource Load(GameResourceRepository repository,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(repository);
        return RenderSamplerSourceAdapters.ReadStaticHdrNativeSource(RenderSamplerSourceAdapters.Load(repository.SourceDirectory,locale));
    }
}

public static partial class RenderSamplerSourceAdapters
{
    internal static LoadedMapPostEffect3StaticHdrNativeSource ReadStaticHdrNativeSource(LoadedDeferredSamplerNativeSource native)
    {
        StaticHdrFormatRule[] formats=[
            new(27,28,29), // RGBA8 typeless resource linear and sRGB views.
            new(90,87,91), // BGRA8 typeless resource linear and sRGB views.
            new(39,41,0),  // R32 typeless resource float view.
            new(9,10,0)];  // RGBA16 typeless resource float view.
        return new(formats,native,RenderRuleIdentity.For("static-hdr-format",native.Locale)+
            ";resource-linear-and-srgb-format-rules;source-image-selector;single-mip-static-descriptor");
    }
}
