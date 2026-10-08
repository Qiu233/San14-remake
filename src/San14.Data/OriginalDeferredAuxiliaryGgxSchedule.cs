using System.Buffers.Binary;

namespace San14.Data;

/// <summary>One original filter draw, after the selected target mip0 copy has
/// succeeded. A source view is one native mip while retaining parent sampler metadata.</summary>
public sealed record OriginalDeferredAuxiliaryFilterWrite(int PassOrdinal,uint SourceTextureId,uint SourceMip,
    uint TargetTextureId,uint TargetMip,OriginalDeferredAuxiliaryPixelPass Program,
    ReadOnlyMemory<byte> PixelConstantBuffer0)
{
    public uint VertexCount => 3;
    public ReadOnlyMemory<byte> VertexConstantBuffer0 { get; init; }
    public OriginalDeferredAuxiliaryExtent Viewport { get; init; }
    public string NumericProfile { get; init; } = "source-native-tangent-and-binary32-operations";
    public bool NativeRoundingVerified { get; init; } = true;
}

public static class OriginalDeferredAuxiliaryGgxSchedule
{
    /// <summary>E9ECB0/1056140: type2 reads the preceding mip; type3 reads
    /// mip0. Current camera fields are radians at148 and aspect at14C, after
    /// the accepted camera copy. Missing or inactive current inputs do not write.</summary>
    public static IReadOnlyList<OriginalDeferredAuxiliaryFilterWrite> Read(
        LoadedOriginalDeferredAuxiliaryPixelSourceRecipes source,OriginalDeferredTransparencyImageSelection selection,
        uint currentContextFlags3F0,uint currentFilterType90,int currentSampleCount94,
        float currentCameraFov148,float currentCameraAspect14C,OriginalNativeCameraTangentDispatch tangentDispatch)
    {
        ArgumentNullException.ThrowIfNull(source);
        if((currentContextFlags3F0&0x400)==0 || (currentContextFlags3F0&0x80000000)!=0)
            throw new InvalidDataException("Current native auxiliary filter gate does not execute.");
        if(currentFilterType90 is not (2 or 3) || currentSampleCount94<=0 ||
            !float.IsFinite(currentCameraFov148) || !float.IsFinite(currentCameraAspect14C) || currentCameraAspect14C<=0)
            throw new InvalidDataException("GGX schedule requires resolved current native filter and camera inputs.");
        var target=source.SelectSpecular(selection);
        if(target.MipCount!=7)throw new InvalidDataException("Actual specular GGX target does not have its seven original mips.");
        float tangent=OriginalNativeCameraTangent.Tangent1020A90(currentCameraFov148*0.5f,tangentDispatch);
        float projection=1f/tangent;
        var program=source.FilterShader.ResolveFilterPass(currentFilterType90,0,0);
        if(program.Stages.Single(s=>s.StageCode==0).Definitions.Buffers.Single().ByteSize!=16 ||
            program.Stages.Single(s=>s.StageCode==1).Definitions.Buffers.Count!=0)
            throw new InvalidDataException("GGX original vertex/pixel constant layout differs.");
        uint objectId=target.Texture.Object.ObjectId;
        return Array.AsReadOnly(Enumerable.Range(0,6).Select(k=>
        {
            float roughness=(float)(k+1)/6f,squared=roughness*roughness;
            byte[] constants=new byte[16];
            Write(constants,0,projection);Write(constants,4,currentCameraAspect14C);
            Write(constants,8,currentSampleCount94);Write(constants,12,squared*squared);
            return new OriginalDeferredAuxiliaryFilterWrite(k,objectId,currentFilterType90==2?(uint)k:0,
                objectId,(uint)k+1,program,constants){Viewport=target.Surfaces[k+1].Extent};
        }).ToArray());
    }
    private static void Write(byte[] bytes,int offset,float value)
        => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset,4),BitConverter.SingleToUInt32Bits(value));
}
