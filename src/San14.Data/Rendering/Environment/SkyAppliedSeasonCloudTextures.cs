using System.Buffers.Binary;
using System.Text;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Actually declared, loaded source seasonal Cloud noise/cirrus
/// groups. These are source resources, not prepared GPU descriptors, inferred
/// shader slots, a current Cloud CB or an assertion about native SRV format.</summary>
public sealed record SkyAppliedSeasonCloudTextureSources(MapSeasonCameraPackage Package,
    SkyCloudSourceTextureGroup Noise,SkyCloudSourceTextureGroup Cirrus,
    SkyCloudSourceTextureGroup? SkylightSta,string SourceProvenance)
{
    public uint DeclaredFallbackPlane { get; init; }
    public SkyCloudSourceTextureGroup? RenderingPlane { get; init; }
    public SkyCloudSourceTextureGroup RequireAtmospherePlane() => RenderingPlane ??
        throw new NotSupportedException("This operation requires the selected atmosphere provider's actual Plane.");
    public void RequireSelectedPackage(MapSeasonCameraPackage actual)
    {
        if(Package.Camera.ObjectId!=actual.Camera.ObjectId||
            Package.Objects.Source.ResourceUri!=actual.Objects.Source.ResourceUri||
            Package.Objects.Source.DecodedSha256!=actual.Objects.Source.DecodedSha256||
            Package.Action.Selection!=actual.Action.Selection)
            throw new InvalidDataException("Loaded Cloud texture sources belong to another selected package generation.");
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

/// <summary>The selected Camera declares noise, cirrus and atmosphere
/// providers. Their ordered UTF8 name tables resolve through the normal
/// resource repository to Static images, including valid resource overrides.</summary>
public static class SkyAppliedSeasonCloudTextures
{
    internal static SkyAppliedSeasonCloudTextureSources Read(GameResourceRepository repository,
        SkySeasonMainCameraSource actualSelectedSource)
    {
        ArgumentNullException.ThrowIfNull(repository);ArgumentNullException.ThrowIfNull(actualSelectedSource);
        var selection=actualSelectedSource.Selection;selection.Validate();
        if(selection.SecondPackage is not null)throw new NotSupportedException("Cloud source resources close the actual direct selected package branch.");
        return LoadPackage(repository,actualSelectedSource,selection.FirstPackage);
    }
    private static SkyAppliedSeasonCloudTextureSources LoadPackage(GameResourceRepository repository,
        SkySeasonMainCameraSource actualSelectedSource,MapSeasonCameraPackage package)
    {
        var camera=SkySceneSourceDefaults.ReadPerspectiveCamera(package.Objects,package.Camera.ObjectId);
        var definitions=repository.LoadObjects("system",0xDAB638B2);
        var noise=Group(repository,definitions,camera.References.CloudNoise);
        var cirrus=Group(repository,definitions,camera.References.CloudCirrus);
        var plane=actualSelectedSource.RenderingAtmosphere is { } provider
            ? Group(repository,definitions,provider.PlaneStaObjectId) : null;
        return new(package,noise,cirrus,camera.References.SkylightSta==0 ? null : plane,actualSelectedSource.SourceProvenance+";actual-declared-season-Cloud-Noise/Cirrus->DAB638B2->UTF8names->Static/G1T;"+
            "noise-count3/cirrus-count1;selected-primary-or-fallback-Plane;full-volume-mips-and-source-samplers;GPUformat/CB/current-variant-separate")
            { DeclaredFallbackPlane=camera.References.PlaneSta, RenderingPlane=plane };
    }
    private static SkyCloudSourceTextureGroup Group(GameResourceRepository repository,LoadedObjects definitions,uint groupId)
    {
        var group=definitions.Document.Objects.SingleOrDefault(o=>o.ObjectId==groupId);
        if(group is null||group.Kind!=DokObjectKind.Inline||group.ClassId!=0x3059B9C3||Scalar(group,0x8D16812D)!=0)
            throw new InvalidDataException("Actual Cloud Textures declaration changed.");
        var table=repository.LoadRdbAsset("system",Scalar(group,0x7A1E1EF8));
        string text=new UTF8Encoding(false,true).GetString(table.Bytes).TrimStart('\uFEFF');
        var rows=text.Split('\n').Select(line=>line.TrimEnd('\r')).Where(line=>line.Length>0&&!line.StartsWith(';')).ToArray();
        if(rows.Length==0)throw new InvalidDataException("Cloud texture group contains no declared slots.");
        var staticIds=new uint[rows.Length];
        for(int i=0;i<rows.Length;i++)
        {
            int separator=rows[i].IndexOf(',');
            if(separator<1||!int.TryParse(rows[i].AsSpan(0,separator),out int slot)||slot!=i||separator==rows[i].Length-1)
                throw new InvalidDataException("Cloud texture table requires ordered contiguous slots and resource names.");
            staticIds[i]=ResourceNameHash.Compute(rows[i][(separator+1)..]);
        }
        var bindings=StaticTextureResources.Load(repository,definitions,"system",staticIds).ToArray();
        return new(groupId,table,bindings,$"{definitions.Source.ResourceUri}@{definitions.Source.DecodedSha256}#object/{groupId:X8};"+
            $"typed-Textures/7A1E1EF8->{table.ResourceUri}@{table.DecodedSha256};literal-ordered-slots;8D16812D=0");
    }
    private static uint Scalar(DokObject o,uint name)
    {
        var fields=o.Properties.Where(p=>p.NameHash==name).ToArray();
        if(fields.Length!=1)throw new InvalidDataException("Cloud resource declaration lacks a unique required field.");
        var p=fields[0];
        if(p.TypeCode!=5||p.ElementCount!=1||p.ElementSize!=4||p.RawValue.Length!=4)
            throw new InvalidDataException("Cloud source group field changed from its exact type5 scalar.");
        return BinaryPrimitives.ReadUInt32LittleEndian(p.RawValue);
    }
}
