using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>A complete imported half4 table. Consumers receive copied bytes;
/// sampling reads this private payload without depending on parser buffers.</summary>
public sealed class SkyAtmoRawTableData
{
    private readonly byte[] payload;
    public uint FileId { get; }
    public string ResourceUri { get; }
    public string DecodedSha256 { get; }
    public int ByteLength => payload.Length;

    internal SkyAtmoRawTableData(LoadedRdbAsset source)
    {
        FileId=source.FileId;ResourceUri=source.ResourceUri;DecodedSha256=source.DecodedSha256;
        payload=source.Bytes.ToArray();
    }
    public byte[] CopyBytes() => payload.ToArray();
    internal ushort ReadChannel(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset,2));
    internal bool SameResource(SkyAtmoRawTableData other)
        => FileId==other.FileId&&ResourceUri==other.ResourceUri&&DecodedSha256==other.DecodedSha256&&ByteLength==other.ByteLength;
}

/// <summary>One imported atmosphere declaration owns its settings and both
/// numeric tables. Scene preparation and ordinary sampling borrow this owner.</summary>
public sealed class LoadedSkyAtmoTableSources
{
    public uint ObjectId { get; }
    public uint ClassId => SkyAtmoTableResources.ClassId;
    public string DefinitionResourceUri { get; }
    public string DefinitionSha256 { get; }
    public SkyAtmoRawTableData Transmittance { get; }
    public SkyAtmoRawTableData Ambient2D { get; }
    public uint PlaneStaObjectId { get; }
    public int Width { get; }
    public int Height { get; }
    public uint Flags { get; }
    public Vector4 MieExtinction { get; }
    public Vector4 RayleighExtinction { get; }
    public float MieHeight { get; }
    public float RayleighHeight { get; }
    public float PlanetRadius { get; }
    public float AtmoTopHeight { get; }
    public int RowPitchBytes => checked(Width*8);
    public string SourceEvidence { get; }

    internal LoadedSkyAtmoTableSources(uint objectId,LoadedRdbAsset definitions,
        LoadedRdbAsset transmittance,LoadedRdbAsset ambient2D,
        uint plane,int width,int height,uint flags,Vector4 mie,Vector4 rayleigh,
        float mieHeight,float rayleighHeight,float planetRadius,float atmoTopHeight)
    {
        ObjectId=objectId;DefinitionResourceUri=definitions.ResourceUri;DefinitionSha256=definitions.DecodedSha256;
        Transmittance=new(transmittance);Ambient2D=new(ambient2D);
        PlaneStaObjectId=plane;Width=width;Height=height;Flags=flags;
        MieExtinction=mie;RayleighExtinction=rayleigh;MieHeight=mieHeight;RayleighHeight=rayleighHeight;
        PlanetRadius=planetRadius;AtmoTopHeight=atmoTopHeight;
        SourceEvidence=$"{DefinitionResourceUri}@{DefinitionSha256}#object={ObjectId:X8}/class={ClassId:X8};"+
            $"TTable={Transmittance.ResourceUri}@{Transmittance.DecodedSha256};"+
            $"Amb2d={Ambient2D.ResourceUri}@{Ambient2D.DecodedSha256};imported-atmosphere-half4-tables";
    }

    public Vector4 SampleGrid(float heightCoordinate,float angleCoordinate,
        SkyAtmoRawTable table=SkyAtmoRawTable.Transmittance)
        => SkyAtmoTableResources.SampleGrid(this,heightCoordinate,angleCoordinate,table);

    public bool SameResources(LoadedSkyAtmoTableSources other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return ReferenceEquals(this,other)||ObjectId==other.ObjectId&&PlaneStaObjectId==other.PlaneStaObjectId&&
            Width==other.Width&&Height==other.Height&&Flags==other.Flags&&
            Same(MieExtinction,other.MieExtinction)&&Same(RayleighExtinction,other.RayleighExtinction)&&
            Same(MieHeight,other.MieHeight)&&Same(RayleighHeight,other.RayleighHeight)&&
            Same(PlanetRadius,other.PlanetRadius)&&Same(AtmoTopHeight,other.AtmoTopHeight)&&
            Transmittance.SameResource(other.Transmittance)&&Ambient2D.SameResource(other.Ambient2D);
    }
    private static bool Same(float left,float right)
        => BitConverter.SingleToUInt32Bits(left)==BitConverter.SingleToUInt32Bits(right);
    private static bool Same(Vector4 left,Vector4 right)
        => Same(left.X,right.X)&&Same(left.Y,right.Y)&&Same(left.Z,right.Z)&&Same(left.W,right.W);
}
