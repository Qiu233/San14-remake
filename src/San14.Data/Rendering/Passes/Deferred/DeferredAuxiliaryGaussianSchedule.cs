using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

public interface IDeferredAuxiliaryExponential
{
    string Revision { get; }
    bool NativeRoundingVerified { get; }
    float Evaluate(float value);
}

/// <summary>The source Gaussian calls exp on a binary32 exponent. This explicit
/// .NET implementation uses the same formula; native library rounding awaits
/// later comparison. It supplies no guessed weights or source coefficients.</summary>
public sealed class DotNetDeferredAuxiliaryExponential : IDeferredAuxiliaryExponential
{
    public static DotNetDeferredAuxiliaryExponential Instance { get; } = new();
    public string Revision => "source-gaussian-formula-dotnet-mathf-exp-v1";
    public bool NativeRoundingVerified => false;
    public float Evaluate(float value) => MathF.Exp(value);
}

public static class DeferredAuxiliaryGaussianSchedule
{
    public static IReadOnlyList<DeferredAuxiliaryFilterWrite> ReadSpecular(
        LoadedDeferredAuxiliaryPixelSourceRecipes source,DeferredTransparencyImageSelection selection,
        uint currentContextFlags3F0,uint currentGaussianSize94,float currentDispersion9C,
        IDeferredAuxiliaryExponential? exponential=null)
    {
        RequireGate(currentContextFlags3F0);
        ArgumentNullException.ThrowIfNull(source);
        var target=source.SelectSpecular(selection);var scratch=source.SelectSpecularIntermediate(selection);
        exponential??=DotNetDeferredAuxiliaryExponential.Instance;
        // E9F33D reads one current scalar;1056B90 broadcasts it into the four
        // native kernel lanes. Adjacent CommonView fields are separate values.
        byte[] weights=Weights(currentGaussianSize94,target.Extent.Width,new Vector4(currentDispersion9C),exponential);
        var writes=new List<DeferredAuxiliaryFilterWrite>();
        for(int k=0;k<6;k++)
        {
            var viewport=target.Surfaces[k+1].Extent;
            writes.Add(Draw(source.FilterShader,k*2,target.Texture.Object.ObjectId,(uint)k,scratch.Texture.Object.ObjectId,0,
                viewport,currentGaussianSize94,0,weights,Parameters(1f/viewport.Width,0,0,0),exponential));
            writes.Add(Draw(source.FilterShader,k*2+1,scratch.Texture.Object.ObjectId,0,target.Texture.Object.ObjectId,(uint)k+1,
                viewport,currentGaussianSize94,1,weights,Parameters(1f/scratch.Extent.Height,
                    (float)viewport.Width/scratch.Extent.Width,(float)viewport.Height/scratch.Extent.Height,0),exponential));
        }
        return writes.AsReadOnly();
    }

    /// <summary>The native depth Gaussian retains source feature(1,3) and the
    /// initializer dispersion25. The caller must resolve C9AAE0 mode1.</summary>
    public static IReadOnlyList<DeferredAuxiliaryFilterWrite> ReadDepth(
        LoadedDeferredAuxiliaryPixelSourceRecipes source,uint currentContextFlags3F0,
        uint currentDepthFilterType,IDeferredAuxiliaryExponential? exponential=null)
    {
        RequireGate(currentContextFlags3F0);
        ArgumentNullException.ThrowIfNull(source);
        if(currentDepthFilterType!=1)throw new InvalidDataException("Depth Gaussian schedule requires the resolved native mode1 branch.");
        exponential??=DotNetDeferredAuxiliaryExponential.Instance;
        var target=source.BlurredDepth;var scratch=source.DepthIntermediate;var viewport=target.Extent;
        byte[] weights=Weights(3,target.Extent.Width,new Vector4(25),exponential);
        return Array.AsReadOnly(new[]{
            Draw(source.DepthFilterShader,0,source.MainDepth.Object.ObjectId,0,scratch.Texture.Object.ObjectId,0,
                viewport,3,0,weights,Parameters(1f/viewport.Width,0,0,0),exponential),
            Draw(source.DepthFilterShader,1,scratch.Texture.Object.ObjectId,0,target.Texture.Object.ObjectId,0,
                viewport,3,1,weights,Parameters(1f/scratch.Extent.Height,
                    (float)viewport.Width/scratch.Extent.Width,(float)viewport.Height/scratch.Extent.Height,0),exponential)});
    }

    public static byte[] Weights(uint gaussianSize,int baseTargetWidth,Vector4 dispersion,
        IDeferredAuxiliaryExponential? exponential=null)
    {
        if(gaussianSize is <1 or >7 || baseTargetWidth<=0 ||
            !float.IsFinite(dispersion.X)||!float.IsFinite(dispersion.Y)||!float.IsFinite(dispersion.Z)||!float.IsFinite(dispersion.W))
            throw new InvalidDataException("Gaussian source size, target width or four current dispersions are unresolved.");
        exponential??=DotNetDeferredAuxiliaryExponential.Instance;
        float epsilon=BitConverter.UInt32BitsToSingle(0x3A9C4000);
        float scaledWidth=(float)baseTargetWidth*(1f/256f),step=scaledWidth>0?1f/scaledWidth:scaledWidth;
        byte[] bytes=new byte[checked(((int)gaussianSize+1)*16)];
        float[] components=[dispersion.X,dispersion.Y,dispersion.Z,dispersion.W];
        for(int c=0;c<4;c++)
        {
            float[] values=new float[gaussianSize+1];values[0]=1f;float d=components[c],sum=1f;
            if(!(d>=-epsilon && d<=epsilon))
            {
                float inverse=1f/d;
                for(int j=1;j<values.Length;j++)
                {
                    float distance=(float)j*step;
                    float exponent=distance*(-0.5f);exponent*=distance;exponent*=inverse;
                    values[j]=exponential.Evaluate(exponent);
                    float twice=values[j]*2f;sum+=twice;
                }
                if(!(sum>=-epsilon && sum<=epsilon))
                    for(int j=0;j<values.Length;j++)values[j]/=sum;
            }
            for(int j=0;j<values.Length;j++)Write(bytes,j*16+c*4,values[j]);
        }
        return bytes;
    }

    private static DeferredAuxiliaryFilterWrite Draw(DeferredAuxiliaryShaderSource source,int ordinal,
        uint input,uint inputMip,uint output,uint outputMip,DeferredAuxiliaryExtent viewport,uint size,uint pass,
        byte[] weights,byte[] parameters,IDeferredAuxiliaryExponential exponential)
    {
        var program=source.ResolveFilterPass(1,size,pass);
        byte[] pixel=new byte[16+weights.Length];parameters.CopyTo(pixel,0);weights.CopyTo(pixel,16);
        var vertex=program.Stages.Single(s=>s.StageCode==1);
        if(program.Stages.Single(s=>s.StageCode==0).Definitions.Buffers.Single().ByteSize!=pixel.Length ||
            vertex.Definitions.Buffers.Count!=(pass==1?1:0) || (pass==1 && vertex.Definitions.Buffers[0].ByteSize!=16))
            throw new InvalidDataException("Original Gaussian stage constant layout differs from its native source packet.");
        return new(ordinal,input,inputMip,output,outputMip,program,pixel)
        {Viewport=viewport,VertexConstantBuffer0=pass==1?parameters:ReadOnlyMemory<byte>.Empty,
            NumericProfile=exponential.Revision,NativeRoundingVerified=exponential.NativeRoundingVerified};
    }
    private static byte[] Parameters(float x,float y,float z,float w)
    {byte[] bytes=new byte[16];Write(bytes,0,x);Write(bytes,4,y);Write(bytes,8,z);Write(bytes,12,w);return bytes;}
    private static void Write(byte[] bytes,int offset,float value)
        => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset,4),BitConverter.SingleToUInt32Bits(value));
    private static void RequireGate(uint flags)
    {if((flags&0x400)==0 || (flags&0x80000000)!=0)throw new InvalidDataException("Current native auxiliary filter gate does not execute.");}
}
