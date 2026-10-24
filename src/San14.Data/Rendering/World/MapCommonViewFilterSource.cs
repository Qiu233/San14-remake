using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Numerics;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Exact selected source CommonViewSetting fields8C/90/94/98/9C. Its identity
/// can be shared by resource and embedded-depth-program owners without making
/// an original-process pointer claim.</summary>
public sealed class MapCommonViewFilterSource
{
    private MapCommonViewFilterSource(LoadedObjects source,DokObject settings,uint mode,int samples,float dispersion,int depthType,int resolution)
    {Source=source;Settings=settings;Mode90=mode;SampleCount94=samples;GaussianDispersion9C=dispersion;DepthFilterType98=depthType;TransparencyResolution8C=resolution;}
    public LoadedObjects Source { get; }
    public DokObject Settings { get; }
    public uint Mode90 { get; }
    public int SampleCount94 { get; }
    public float GaussianDispersion9C { get; }
    public int DepthFilterType98 { get; }
    public int TransparencyResolution8C { get; }
    /// <summary>E9F33D MOVSS followed by1056B90 SHUFPS0 broadcasts the scalar
    /// current Common9C. Adjacent Common words are not dispersion lanes.</summary>
    public Vector4 PreparedGaussianDispersion => new(GaussianDispersion9C);
    public string SourceProvenance=>Source.Source.ResourceUri+$"#{Settings.ObjectId:X8}@{Source.Source.DecodedSha256};"+
        "TC-D02098/ D020A2:constructor90=1/94=2;D22AA5/D22AAC:property60->8C;D2292D/D22938:property61/62;D2315B..D23179:loader8C/90/94/98;D1E50D..D1E52E:current-copy";

    public static MapCommonViewFilterSource LoadGameDefault(GameResourceRepository repository)
        => ReadTemplate(repository.LoadObjects("KIDSSystemResource",0x6D011726),0x899F101B);
    public static MapCommonViewFilterSource ReadTemplate(LoadedObjects source,uint selectedObjectId)
    {
        ArgumentNullException.ThrowIfNull(source);
        if(source.Source.OverrideSource is not null||Convert.ToHexString(SHA256.HashData(source.Source.Bytes))!=source.Source.DecodedSha256)
            throw new InvalidDataException("Source Common filter defaults require unedited exact resource bytes.");
        var settings=DokReader.Read(source.Source.Bytes).Objects.Single(o=>o.ObjectId==selectedObjectId);
        if(settings.Kind!=DokObjectKind.Inline||settings.ClassId!=0xEF3D849D||settings.Properties.Count!=102)
            throw new InvalidDataException("Common filter source requires the actual102-property CommonViewSetting class.");
        uint Word(int ordinal,uint hash,uint type=5)
        {
            var field=settings.Properties[ordinal];
            if(field.Index!=ordinal||field.NameHash!=hash||field.TypeCode!=type||field.ElementCount!=1||field.RawValue.Length!=4)
                throw new InvalidDataException("Common90/94 source ordinal/type differs from its exact loader pointer table.");
            return BinaryPrimitives.ReadUInt32LittleEndian(field.RawValue);
        }
        var dispersion=settings.Properties[63];
        if(dispersion.Index!=63||dispersion.NameHash!=0xFD4C0435||dispersion.TypeCode!=8||
            dispersion.ElementCount!=1||dispersion.RawValue.Length!=4)
            throw new InvalidDataException("Common9C source scalar differs from the exact loader property63.");
        float scalar=BinaryPrimitives.ReadSingleLittleEndian(dispersion.RawValue);
        if(!float.IsFinite(scalar))throw new InvalidDataException("Source Common Gaussian dispersion is outside its finite preparation domain.");
        return new(source,settings,Word(61,0xD5222D67),unchecked((int)Word(62,0xF4BAA840)),scalar,
            unchecked((int)Word(69,0x5692BB6E,4)),unchecked((int)Word(60,0xA29E490D)));
    }
    /// <summary>Explicit offline source comparison. Normal frame consumers
    /// use the immutable parsed fields without re-reading resource bytes.</summary>
    public void Validate()
    {
        var canonical=ReadTemplate(Source,Settings.ObjectId);
        if(canonical.Mode90!=Mode90||canonical.SampleCount94!=SampleCount94||canonical.DepthFilterType98!=DepthFilterType98||canonical.TransparencyResolution8C!=TransparencyResolution8C||
            BitConverter.SingleToUInt32Bits(canonical.GaussianDispersion9C)!=BitConverter.SingleToUInt32Bits(GaussianDispersion9C))
            throw new InvalidDataException("Source Common filter field generation changed.");
    }
    /// <summary>D1E410 copies fields90/94 from the selected owned source settings
    /// to the current Common packet. Other fields/overlays keep their owners.</summary>
    public MapCommonViewFilterCurrent PrepareAppliedCurrent(long frameNumber,
        string commonSettingsOwnerIdentity,string currentViewOwnerIdentity)
    {
        if(frameNumber<1||string.IsNullOrWhiteSpace(commonSettingsOwnerIdentity)||string.IsNullOrWhiteSpace(currentViewOwnerIdentity))
            throw new InvalidDataException("Applied Common filter packet requires its accepted frame and selected Settings/View owners.");
        return new(this,frameNumber,commonSettingsOwnerIdentity,currentViewOwnerIdentity);
    }
}

public sealed class MapCommonViewFilterCurrent
{
    internal MapCommonViewFilterCurrent(MapCommonViewFilterSource source,long frame,string settings,string view)
    {Source=source;FrameNumber=frame;CommonSettingsOwnerIdentity=settings;CurrentViewOwnerIdentity=view;}
    public MapCommonViewFilterSource Source { get; }
    public long FrameNumber { get; }
    public string CommonSettingsOwnerIdentity { get; }
    public string CurrentViewOwnerIdentity { get; }
    public uint Mode90=>Source.Mode90;
    public int SampleCount94=>Source.SampleCount94;
    public float GaussianDispersion9C=>Source.GaussianDispersion9C;
    public int DepthFilterType98=>Source.DepthFilterType98;
    public int TransparencyResolution8C=>Source.TransparencyResolution8C;
    public Vector4 PreparedGaussianDispersion=>Source.PreparedGaussianDispersion;
    public string SourceProvenance=>Source.SourceProvenance+$";applied-settings={CommonSettingsOwnerIdentity};View={CurrentViewOwnerIdentity};frame={FrameNumber}";
    public void ValidateFrame(long frameNumber,string settingsOwner,string viewOwner)
    {
        if(frameNumber!=FrameNumber||settingsOwner!=CommonSettingsOwnerIdentity||viewOwner!=CurrentViewOwnerIdentity)
            throw new InvalidDataException("Current Common filter fields belong to another accepted frame or Settings/View owner.");
    }
}
