using System.Buffers.Binary;

namespace San14.Data;

/// <summary>The two real manager wrapper key recipes. These values establish
/// sampler creation, not installation in a particular shader stage or frame.</summary>
public sealed class OriginalMapPostEffect3ManagerSamplerSources
{
    private readonly byte[] linear,point;
    internal OriginalMapPostEffect3ManagerSamplerSources(byte[] linearKey,byte[] pointKey,string evidence,string locale="TC")
        =>(linear,point,SourceEvidence,Locale)=(linearKey,pointKey,evidence,locale);
    public string SourceEvidence{get;}
    public string Locale{get;}
    public byte[] LinearKey=>linear.ToArray();
    public byte[] PointKey=>point.ToArray();
}

public static partial class OriginalDeferredSamplerSources
{
    /// <summary>11443A0/1145960 create two eight by eight native RT wrappers,
    /// then edit wrapper+48 and install them via114AAD0 into manager+30/+38.
    /// The currently verified SC/TC source profiles are admitted. No RDEF sampler name
    /// or backend null/default state participates in this recipe.</summary>
    public static OriginalMapPostEffect3ManagerSamplerSources ReadPostEffect3ManagerSamplers(
        LoadedOriginalDeferredSamplerNativeSource native)
    {
        ArgumentNullException.ThrowIfNull(native);
        return LoadedPostEffect3ManagerSamplers.GetValue(native, ReadPostEffect3ManagerSamplersAtImport);
    }

    private static OriginalMapPostEffect3ManagerSamplerSources ReadPostEffect3ManagerSamplersAtImport(
        LoadedOriginalDeferredSamplerNativeSource native)
    {
        if(native.Locale is not ("TC" or "SC"))throw new NotSupportedException("3SPK manager sampler writers are verified for SC/TC.");
        byte[] exe=File.ReadAllBytes(native.PhysicalPath);
        VerifyHash(exe,native.ExecutableSha256,"3SPK sampler executable");
        var image=new NativeImage(exe);byte[] text=image.DecodeText(out int start);
        VerifyHash(text,native.DecodedTextSha256,"3SPK sampler decoded text");
        void Check(int rva,int size,string hash)
        {
            var actual=OriginalMapPostEffect3NativeSourceProfile.CodeWindow(native.Locale,"OriginalMapPostEffect3SamplerSources.cs",rva,size,hash);
            VerifyHash(text.AsSpan(actual.Rva-start,size),actual.Sha,"3SPK manager sampler writer");
        }
        Check(0x11443A0,0x11D,"3343B12E7E72AB01BA83BD12091F31CD6034160A7C946F59A8C1E6E207728422");
        Check(0x1145960,0x108,"AF2D17C5632CADBBB5E63C8668863E311C480FA1D5BDFB90B154D487B32970C7");
        Check(0x114AAD0,0x6A,"B6CAF2744BB7ABFB40D7388540C58869356FBC0BDB0321AE15978DE913A4D52D");
        // Creator requests level1; preserve constructor high word and bias.
        byte[] linear=RenderTextureKey(native,1),point=RenderTextureKey(native,1);
        void Write(byte[] key,ushort filter)
        {
            ushort lo=BinaryPrimitives.ReadUInt16LittleEndian(key);
            lo=(ushort)((lo&0x03FF)|filter);
            lo=(ushort)((lo&0xFFF5)|5);
            BinaryPrimitives.WriteUInt16LittleEndian(key,lo);
        }
        Write(linear,0x5400);Write(point,0x4000);
        return new(linear,point,native.ResourceUri+$";{native.Locale}:paired-source-profile;TC-reference-role-anchors;"+";TC11443A0/1145960->952D90(level1)->wrapper48;114AAD0:linear30/point38;slot-installation-not-established",native.Locale);
    }
}
