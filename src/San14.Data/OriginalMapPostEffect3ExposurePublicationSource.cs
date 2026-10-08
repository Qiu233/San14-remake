using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Pinned post-Apply stock draw parameters for owned Main View0.
/// The source draws one texel into its retained node1 exposure target. This
/// recipe does not grant either source pixels or a completed device draw.</summary>
public sealed class LoadedOriginalMapPostEffect3ExposurePublicationSource
{
    private readonly G1TextureSamplerRecipe sampler;
    private readonly LoadedOriginalMapPostEffect3EyeExposureInitializationSource allocationSource;
    internal LoadedOriginalMapPostEffect3ExposurePublicationSource(LoadedOriginalDeferredSamplerNativeSource native,
        G1TextureSamplerRecipe sampler,LoadedOriginalMapPostEffect3EyeExposureInitializationSource allocationSource,string evidence)
        =>(Native,this.sampler,this.allocationSource,SourceEvidence)=(native,sampler,allocationSource,evidence);
    public LoadedOriginalDeferredSamplerNativeSource Native{get;}
    public string SourceEvidence{get;}
    public G1TextureSamplerRecipe InputSampler=>sampler with{RawKey=sampler.RawKey.ToArray()};
    public void RequireAllocation(OriginalMapPostEffect3AllocationPlan actualAllocation)=>allocationSource.ValidateFor(actualAllocation);
    public void RequireCurrent(OriginalMapPostEffect3ControlledAuxFields actualAux,OriginalMapCopy97CurrentStockPacket actualStock)
    {
        ArgumentNullException.ThrowIfNull(actualAux);ArgumentNullException.ThrowIfNull(actualStock);
        actualAux.ValidateFrame(actualStock.FrameNumber);actualStock.ValidateCurrent();
        if(!actualAux.CustomPixelShader108Absent||!actualStock.CurrentCustomVertexShaderIsAbsent)
            throw new NotSupportedException("Exposure publication needs the actual absent CommonAux108/custom stock VS; custom shader type5 is not implemented.");
        if(actualStock.CurrentScreenScale29E0!=System.Numerics.Vector2.One)
            throw new NotSupportedException("Exposure publication coverage currently requires the owned source unit screen scale.");
        var color=actualStock.CurrentColorScale29F0;
        if(!float.IsFinite(color.X)||!float.IsFinite(color.Y)||!float.IsFinite(color.Z)||!float.IsFinite(color.W))
            throw new InvalidDataException("Exposure publication lacks its actual finite retained stock color words.");
    }
    /// <summary>EB85AF destination rectangle(0,0,1,1); EB8595/85BE/85D1
    /// UV(0,0,1/sourceWidth,1/sourceHeight). Actual61 is2x1. Stock49 receives
    /// packed white and z=0; alpha-test-disabled constants are separate.</summary>
    public byte[] BuildQuad()
    {
        float[][] rows=[[0,0,0,0,0],[1,0,0,.5f,0],[0,1,0,0,1],[1,1,0,.5f,1]];
        byte[] bytes=new byte[96];
        for(int i=0;i<4;i++)
        {
            for(int c=0;c<3;c++)BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i*24+c*4,4),rows[i][c]);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i*24+12,4),uint.MaxValue);
            for(int c=0;c<2;c++)BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i*24+16+c*4,4),rows[i][c+3]);
        }
        return bytes;
    }
}

public static class OriginalMapPostEffect3ExposurePublicationSource
{
    public static LoadedOriginalMapPostEffect3ExposurePublicationSource Load(OriginalGameRepository repository,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(repository);
        return OriginalDeferredSamplerSources.ReadPostApplyExposurePublicationSource(
            OriginalDeferredSamplerSources.Load(repository.SourceDirectory,locale));
    }
}

public static partial class OriginalDeferredSamplerSources
{
    internal static LoadedOriginalMapPostEffect3ExposurePublicationSource ReadPostApplyExposurePublicationSource(
        LoadedOriginalDeferredSamplerNativeSource native)
    {
        byte[] exe=File.ReadAllBytes(native.PhysicalPath);VerifyHash(exe,native.ExecutableSha256,"Post-Apply exposure executable");
        var image=new NativeImage(exe);byte[] text=image.DecodeText(out int start);
        VerifyHash(text,native.DecodedTextSha256,"Post-Apply exposure decoded text");
        void Check(int rva,int count,string hash)
        {
            var actual=OriginalMapPostEffect3NativeSourceProfile.CodeWindow(native.Locale,"OriginalMapPostEffect3ExposurePublicationSource.cs",rva,count,hash);
            VerifyHash(text.AsSpan(actual.Rva-start,count),actual.Sha,"Post-Apply exposure source window");
        }
        Check(0xEB84C0,329,"C957D98A4CA91EE229C7EAFDE8BAB7C390AF12A96D7EB0B044E275EFD2F66ED0");
        Check(0xEB99DF,114,"9FFE6BD39EAE2398082ED5252904EF5933F2CA4696CE968935AA7CD353021D7E");
        Check(0x95A370,302,"87C8929F3B33304874C693AC9C197B74AFAA96DC1A71E8B96379CE8903F35E60");
        Check(0x956A60,85,"E7D83ED2A7CFA76E797006D9CB13E64C999E0996A23D13C23EFC771CFE6F1D93");
        Check(0xD4A730,45,"41F62383D23DA49373334063C0F90A2918CC0629A11B35AD2F36994822ED652F");
        Check(0x9999C0,62,"A97F6B3DEFE9CF69355406BEFD647167736894D43510E69AD9B57ACBCD39E3A4");
        Check(0x959250,226,"242CAF2EB0A0687BAE472F78084B081451AAD073E783D43539CD443F57E6E3A8");
        Check(0x876F30,258,"11B515D24EE746A19EF26B044F6455DE9419A34C318AA63B36DB2E3AEEE01FF3");
        Check(0x96AFC0,302,"2605FA9EBE7FD31645C180D923FCA51DAB5FC9C4CADFCCB8C0438E87D5B0F456");
        byte[] rectangle=image.Raw(OriginalMapPostEffect3NativeSourceProfile.DataRva(native.Locale,"OriginalMapPostEffect3ExposurePublicationSource.cs",0x13A19C0),16).ToArray();
        VerifyHash(rectangle,"D25B949C65312B91424A3CABD3065DFE18BC73E7CD8893229E9494CD75C16D92","Exposure publication unit rectangle");
        if(!rectangle.AsSpan().SequenceEqual(Convert.FromHexString("00000000000000000000803F0000803F")))
            throw new InvalidDataException("Original post-Apply exposure rectangle differs from the source unit rectangle.");
        // The creation proof checks the exact owned61 declaration and its
        // single-side M1 creator. Normal View0 performs no temporary61 swap;
        // the generic packet consumes this actual wrapper descriptor key.
        var allocation=ReadEyeExposureInitializationSource(native);
        var sampler=DecodeKey(native,Convert.FromHexString("1514300000000000"));
        return new(native,sampler,allocation,native.ResourceUri+"@"+native.ExecutableSha256+
            ";TC:EB8515-target-global50/EB85AF-unit-rect/EB85D1-inverse-source-extent-UV;"+
            "EB99F0-packed-white/EB9A0C-stock49-z0;Aux108-typed-null->956A60-type3;"+
            "D4A730-alphaTestOff->vATest=-FLTMAX;9999C0-blendOff;RGB7/alpha1;"+
            "959250-depthOff/cullNONE/clip0->ScreenEnd/targetPop;"+
            "owned61-single-side-M1/96AFC0-wrapper-key1514300000000000/876F30-read-descriptor38;"+
            "stock32B-VS/default3-16B-PS;source-current104-R32_FLOAT2x1-mip0;"+
            "source-parameters-only/no-GPU-completion-or-right-texel-replacement");
    }
}
