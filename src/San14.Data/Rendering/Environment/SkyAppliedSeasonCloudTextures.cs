using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Actually declared, loaded source seasonal Cloud noise/cirrus
/// groups. These are source resources, not prepared GPU descriptors, inferred
/// shader slots, a current Cloud CB or an assertion about native SRV format.</summary>
public sealed record SkyAppliedSeasonCloudTextureSources(MapSeasonCameraPackage Package,
    SkyCloudSourceTextureGroup Noise,SkyCloudSourceTextureGroup Cirrus,
    SkyCloudSourceTextureGroup SkylightSta,string SourceProvenance)
{
    internal void RequireSelectedPackage(MapSeasonCameraPackage actual)
    {
        if(Package.Camera.ObjectId!=actual.Camera.ObjectId||
            Package.Objects.Source.ResourceUri!=actual.Objects.Source.ResourceUri||
            Package.Objects.Source.DecodedSha256!=actual.Objects.Source.DecodedSha256||
            Package.Action.Selection!=actual.Action.Selection)
            throw new InvalidDataException("Loaded Cloud texture sources belong to another selected package generation.");
    }
    internal SkyAppliedSeasonCloudTextureSources ForCurrent(SkySeasonMainCameraSource current)
    {
        current.Selection.Validate();
        if(current.Selection.SecondPackage is not null)
            throw new NotSupportedException("Loaded Cloud resources require the actual direct selected package.");
        RequireSelectedPackage(current.Selection.FirstPackage);
        return this with{SourceProvenance=current.SourceProvenance+
            ";actual-declared-season-Cloud-Noise/Cirrus->DAB638B2->UTF8names->Static/G1T;"+
            "loaded-full-volume-mips-and-source-samplers/current-package-association;GPUformat/CB/current-variant-separate"};
    }
}

public sealed class SkyCloudSourceTextureGroup
{
    private readonly LoadedRdbAsset table;
    private readonly StaticTextureBinding[] images;
    private readonly IReadOnlyList<StaticTextureBinding> entries;
    private readonly byte[][] samplerKeys,samplerDescriptors;
    private readonly uint[] shaderViewFormats;
    internal SkyCloudSourceTextureGroup(uint objectId,LoadedRdbAsset table,StaticTextureBinding[] images,string provenance)
    {
        ObjectId=objectId;this.table=table;this.images=images;SourceProvenance=provenance;
        entries=Array.AsReadOnly(images);samplerKeys=new byte[images.Length][];samplerDescriptors=new byte[images.Length][];
        shaderViewFormats=new uint[images.Length];
        for(int i=0;i<images.Length;i++)
        {
            var image=images[i].Image;
            var sampler=G1TextureSamplerReader.ReadRetainedSource(image);
            samplerKeys[i]=sampler.RawKey.ToArray();samplerDescriptors[i]=sampler.EncodeD3D11Descriptor();
            shaderViewFormats[i]=ReadShaderViewFormat(image);
        }
    }
    public uint ObjectId{get;}
    public string TableResourceUri=>table.ResourceUri;
    public string TableSha256=>table.DecodedSha256;
    public string SourceProvenance{get;}
    public IReadOnlyList<StaticTextureBinding> Entries=>entries;
    public byte[] ReadSourceSamplerKey(int entry){RequireEntry(entry);return samplerKeys[entry].ToArray();}
    public byte[] ReadSourceSamplerDescriptor(int entry){RequireEntry(entry);return samplerDescriptors[entry].ToArray();}
    /// <summary>Original modern system0/2 extra+0B lowbit, then the proved
    /// format table. Retained whole source mip chains; no runtime skipmip.</summary>
    public uint ReadSourceShaderViewFormat(int entry)
    {
        RequireEntry(entry);return shaderViewFormats[entry];
    }
    private static uint ReadShaderViewFormat(G1TextureImage image)
    {
        if(image.TextureSystem is not (0 or 2)||image.LayerCount!=1||image.RawHeader.Length!=20||
            (image.RawHeader[7]>>4)!=1||BinaryPrimitives.ReadUInt32LittleEndian(image.RawHeader.AsSpan(8,4))!=12)
            throw new NotSupportedException("Cloud source view requires its exact original single12B extra header.");
        return ModelTextureFormatMath.Resolve(image.PixelFormat,(byte)(image.RawHeader[19]&1)).ShaderViewFormat;
    }
    private void RequireEntry(int entry)
    {
        if((uint)entry>=images.Length)throw new InvalidDataException("Actual Cloud group entry is absent.");
    }
}

/// <summary>Direct canonical applied package Noise/Cirrus keys→original
/// Textures source→literal UTF8 slot/name tables→named Static→exact G1T/TID.
/// No directory search, missing resource substitution or fake null noise.</summary>
public static class SkyAppliedSeasonCloudTextures
{
    private const string DefinitionsHash="357F6A380CF19EB2A86B439AF24CF2BACDD38074AAF9BC3AE99F0EAD21EE9271";
    public static SkyAppliedSeasonCloudTextureSources Load(GameResourceRepository repository,
        SkySeasonMainCameraSource actualSelectedSource)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(actualSelectedSource);
        var selection=actualSelectedSource.Selection;selection.Validate();
        if(selection.SecondPackage is not null)throw new NotSupportedException("Cloud source resources close the actual direct selected package branch.");
        var package=SkySeasonPackageTextures.Read(selection.FirstPackage).Package;
        var camera=SkySceneSourceDefaults.ReadPerspectiveCamera(package.Objects,package.Camera.ObjectId);
        if(camera.References.CloudNoise!=0xF4A532C4||camera.References.CloudCirrus!=0x7E43F947)
            throw new NotSupportedException("Applied Cloud package no longer declares its exact original active noise/cirrus family.");
        var definitions=repository.LoadObjects("system",0xDAB638B2);Require(definitions.Source,"DAB638B2",DefinitionsHash);
        var canonical=new LoadedObjects(DokReader.Read(definitions.Source.Bytes),definitions.Source);
        var noise=Group(repository,canonical,0xF4A532C4,0x5CB6E108,"4EF3B765C34C1D9A7B6BA2CB80A89D56F52DD54062230DF1D0D13D5AEB683757",
            ["EE1ResourceStaticTexture［volumetriccloud_base_TID000］","EE1ResourceStaticTexture［volumetriccloud_detail_TID001］","EE1ResourceStaticTexture［volumetriccloud_distort_TID002］"],
            [0x200F0F43,0xEB9E7E04,0xDC81E99D]);
        var cirrus=Group(repository,canonical,0x7E43F947,0x84DD2B25,"CB2E75E87FD3DC0D2855A106D27D20836F38A01B1BBB25E484C59BE79C8B2785",
            ["EE1ResourceStaticTexture［volumetriccloud_cirrus_TID000］"],[0x92DF1F30]);
        if(camera.References.SkylightSta!=0x0490C334)
            throw new NotSupportedException("Cloud primary current STA source requires the actual loaded Skylight0490C334 family.");
        var staProvider=canonical.Document.Objects.Single(o=>o.ObjectId==camera.References.SkylightSta);
        if(staProvider.ClassId!=0x1AA5B65E||staProvider.Properties.Count!=12||Scalar(staProvider,0xE67B59E3)!=0x98026BD5)
            throw new InvalidDataException("Cloud current Skylight primary provider declaration changed.");
        var sta=Group(repository,canonical,0x98026BD5,0x546F00D7,"8B2BE83D0712260B78DA3A8E9E28D60091E0803C4627AD68AD2BB626BDCA14D6",
            ["EE1ResourceStaticTexture［skylightHiAltO3_sctr_trans_amb_amb2d_table_TID000］",
             "EE1ResourceStaticTexture［skylightHiAltO3_sctr_trans_amb_amb2d_table_TID001］",
             "EE1ResourceStaticTexture［skylightHiAltO3_sctr_trans_amb_amb2d_table_TID002］",
             "EE1ResourceStaticTexture［skylightHiAltO3_sctr_trans_amb_amb2d_table_TID003］"],
            [0xF81F0562,0xF8ED83E3,0xF9BC0264,0xFA8A80E5]);
        for(int i=0;i<3;i++)ValidateImage(noise.Entries[i],"B9DA942C","AA076FF358FE3635B073ABF9180FF5DAB5F03EFE995495CCDC5914EE7D86848D",
            i,0x5F,i<2?2:0,i==1?32:128,i==1?32:128,i==0?128:i==1?32:1,i==1?4:6);
        ValidateImage(cirrus.Entries[0],"B2E4126F","D66A14D947E69DD0D0D0D48281C1C73EE48E10DA9793A5D85C2C8D7057947986",0,0x59,0,2048,2048,1,1);
        for(int i=0;i<4;i++)ValidateImage(sta.Entries[i],"7FC7BE7D","A40492CD7DF66B5750B137571DBFBC784D67B86417ECD60F4FE24C2416A25B2F",i,
            3,i==0?2:0,i==0?32:i==2?1024:64,i==0?128:i==2?1:256,i==0?256:1,1);
        return new(package,noise,cirrus,sta,actualSelectedSource.SourceProvenance+";actual-declared-season-Cloud-Noise/Cirrus->DAB638B2->UTF8names->Static/G1T;"+
            "noise-count3/cirrus-count1;full-volume-mips-and-source-samplers;GPUformat/CB/current-variant-separate");
    }
    private static SkyCloudSourceTextureGroup Group(GameResourceRepository repository,LoadedObjects definitions,
        uint groupId,uint tableId,string hash,string[] names,uint[] staticIds)
    {
        var group=definitions.Document.Objects.Single(o=>o.ObjectId==groupId);
        if(group.Kind!=DokObjectKind.Inline||group.ClassId!=0x3059B9C3||group.Properties.Count!=2||
            Scalar(group,0x7A1E1EF8)!=tableId||Scalar(group,0x8D16812D)!=0)
            throw new InvalidDataException("Actual Cloud Textures declaration changed.");
        var table=repository.LoadRdbAsset("system",tableId);Require(table,tableId.ToString("X8"),hash);
        if(!table.Bytes.AsSpan().StartsWith(new byte[]{0xEF,0xBB,0xBF}))throw new InvalidDataException("Cloud source slot table lost its original UTF8 BOM.");
        string text=new UTF8Encoding(false,true).GetString(table.Bytes.AsSpan(3));
        var rows=text.Split('\n').Select(line=>line.TrimEnd('\r')).Where(line=>line.Length>0&&!line.StartsWith(';')).ToArray();
        if(rows.Length!=names.Length)throw new InvalidDataException("Cloud source table does not contain all literal slots.");
        for(int i=0;i<rows.Length;i++)
            if(rows[i]!=i+","+names[i]||ResourceNameHash.Compute(names[i])!=staticIds[i])
                throw new InvalidDataException("Cloud source UTF8 name/index or native resource hash changed.");
        var bindings=StaticTextureResources.Load(repository,definitions,"system",staticIds).ToArray();
        return new(groupId,table,bindings,$"{definitions.Source.ResourceUri}@{definitions.Source.DecodedSha256}#object/{groupId:X8};"+
            $"typed-Textures/7A1E1EF8->{table.ResourceUri}@{table.DecodedSha256};literal-ordered-slots;8D16812D=0");
    }
    private static uint Scalar(DokObject o,uint name)
    {
        var p=o.Properties.Single(p=>p.NameHash==name);
        if(p.TypeCode!=5||p.ElementCount!=1||p.ElementSize!=4||p.RawValue.Length!=4)
            throw new InvalidDataException("Cloud source group field changed from its exact type5 scalar.");
        return BinaryPrimitives.ReadUInt32LittleEndian(p.RawValue);
    }
    private static void ValidateImage(StaticTextureBinding b,string file,string hash,int tid,
        int format,int system,int width,int height,int depth,int mips)
    {
        Require(b.Resource.Source,file,hash);
        var i=G1TextureContainerReader.Read(b.Resource.Source.Bytes).Textures[tid];
        if(b.ImageIndex!=tid||i.PixelFormat!=format||i.TextureSystem!=system||i.Width!=width||i.Height!=height||i.Depth!=depth||
            i.MipCount!=mips||i.LayerCount!=1||i.SubresourceLayout!=(system==2?G1TextureSubresourceLayout.VolumeMipChain:G1TextureSubresourceLayout.SingleLayerMipChain))
            throw new InvalidDataException("Cloud exact original G1T/TID shape, encoding or complete mip layout changed.");
        for(int m=0;m<mips;m++)
            _=system==2?i.GetEncodedVolumeMip(b.Resource.Source.Bytes,m):i.GetEncodedMip(b.Resource.Source.Bytes,m);
    }
    private static void Require(LoadedRdbAsset s,string file,string hash)
    {if(s.ResourceUri!="san14://rdb/system/"+file||s.OverrideSource is not null||s.DecodedSha256!=hash||Convert.ToHexString(SHA256.HashData(s.Bytes))!=hash)
        throw new InvalidDataException("Cloud actual original source identity/hash changed.");}
}
