using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>Pinned post-Apply stock draw parameters for owned Main View0.
/// The source draws one texel into its retained node1 exposure target. This
/// recipe does not grant either source pixels or a completed device draw.</summary>
public sealed class LoadedMapPostEffect3ExposurePublicationSource
{
    private readonly G1TextureSamplerRecipe sampler;
    private readonly LoadedMapPostEffect3EyeExposureInitializationSource allocationSource;
    internal LoadedMapPostEffect3ExposurePublicationSource(LoadedDeferredSamplerNativeSource native,
        G1TextureSamplerRecipe sampler,LoadedMapPostEffect3EyeExposureInitializationSource allocationSource,string evidence)
        =>(Native,this.sampler,this.allocationSource,SourceEvidence)=(native,sampler,allocationSource,evidence);
    public LoadedDeferredSamplerNativeSource Native{get;}
    public string SourceEvidence{get;}
    public G1TextureSamplerRecipe InputSampler=>sampler with{RawKey=sampler.RawKey.ToArray()};
    public void RequireAllocation(MapPostEffect3AllocationPlan actualAllocation)=>allocationSource.ValidateFor(actualAllocation);
    public void RequireCurrent(MapPostEffect3ControlledAuxFields actualAux,MapCopy97CurrentStockPacket actualStock)
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
    /// <summary> destination rectangle(0,0,1,1); /85BE/85D1
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

public static class MapPostEffect3ExposurePublicationSource
{
    public static LoadedMapPostEffect3ExposurePublicationSource Load(GameResourceRepository repository,string locale="TC")
    {
        ArgumentNullException.ThrowIfNull(repository);
        return RenderSamplerSourceAdapters.ReadPostApplyExposurePublicationSource(
            RenderSamplerSourceAdapters.Load(repository.SourceDirectory,locale));
    }
}

public static partial class RenderSamplerSourceAdapters
{
    internal static LoadedMapPostEffect3ExposurePublicationSource ReadPostApplyExposurePublicationSource(
        LoadedDeferredSamplerNativeSource native)
    {

        // The creation proof checks the exact owned61 declaration and its
        // single-side M1 creator. Normal View0 performs no temporary61 swap;
        // the generic packet consumes this actual wrapper descriptor key.
        var allocation=ReadEyeExposureInitializationSource(native);
        var sampler=DecodeKey(native,Convert.FromHexString("1514300000000000"));
        return new(native,sampler,allocation,native.RenderRuleIdentity+
            ";target-global50/-unit-rect/-inverse-source-extent-UV"+
            "-packed-white/-stock49-z0;Aux108-typed-null->-type3"+
            "-alphaTestOff->vATest=-FLTMAX;-blendOff;RGB7/alpha1"+
            "-depthOff/cullNONE/clip0->ScreenEnd/targetPop"+
            "owned61-single-side-M1/-wrapper-key1514300000000000/-read-descriptor38"+
            "stock32B-VS/default3-16B-PS;source-current104-R32_FLOAT2x1-mip0;"+
            "source-parameters-only/no-GPU-completion-or-right-texel-replacement");
    }
}
