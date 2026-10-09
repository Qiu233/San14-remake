namespace San14.Data;

public enum OriginalMapPostEffect3BufferDimension { Typed, Structured, Raw }
public sealed record OriginalMapPostEffect3BufferShape(int ParameterIndex,
    OriginalMapPostEffect3BufferDimension Dimension,uint ByteCount,uint NativeUsage,
    uint DeviceUsage,uint CpuAccessFlags,uint BindFlags,uint MiscFlags,uint StructureByteStride,
    uint ViewFormat,uint ViewElementCount,uint ShaderResourceDimension,uint ShaderResourceFlags,
    uint UnorderedAccessDimension,uint UnorderedAccessFlags)
{
    public bool CreatesShaderResourceView=>(BindFlags&8)!=0;
    public bool CreatesUnorderedAccessView=>(BindFlags&0x80)!=0;
}

/// <summary> buffer requests -> actual allocator v30/v38/v40.
/// Typed-buffer format table14BF8C0 differs from texture table14BF2C0.</summary>
public static class OriginalMapPostEffect3AllocationBufferShapeReader
{
    public static OriginalMapPostEffect3BufferShape Read(OriginalMapPostEffect3BufferAllocation allocation)
    {
        ArgumentNullException.ThrowIfNull(allocation);var p=allocation.Parameter;
        var dimension=p.NativeKind switch {
            0=>OriginalMapPostEffect3BufferDimension.Typed,
            1=>OriginalMapPostEffect3BufferDimension.Structured,
            2=>OriginalMapPostEffect3BufferDimension.Raw,
            _=>throw new NotSupportedException("3SPK buffer kind has no native creator join.")};
        uint usage=p.RawValue20;
        (uint deviceUsage,uint cpuAccess)=usage switch {
            0=>(0u,0u),2=>(2u,0x10000u),
            _=>throw new NotSupportedException("3SPK buffer usage is outside the installed allocation subset.")};
        uint binds=p.RawValue1C;
        if((binds&~0x88u)!=0)throw new NotSupportedException("3SPK buffer bind flags are outside the installed creator subset.");
        uint format=0,stride=0,elementBytes,misc=0;
        bool flagBit2=(p.NativeFlags&4)!=0;
        if(dimension==OriginalMapPostEffect3BufferDimension.Typed)
        {
            (format,elementBytes)=p.NativeFormatOrStride switch {
                6=>(10u,8u),24=>(42u,4u),
                _=>throw new NotSupportedException("3SPK typed buffer format has no installed table join.")};
            misc=flagBit2?0x10u:0u;
        }
        else if(dimension==OriginalMapPostEffect3BufferDimension.Structured)
        {
            // ..37 forces stride1 when the direct-byte flag is set.
            stride=(p.NativeFlags&2)!=0?1u:p.NativeFormatOrStride;
            elementBytes=stride;misc=0x40;
        }
        else
        {
            format=39;elementBytes=4;misc=flagBit2?0x30u:0x20u;
        }
        if(elementBytes==0)throw new InvalidDataException("3SPK native buffer view division has zero element stride.");
        uint count=allocation.ByteCount/elementBytes;
        bool raw=dimension==OriginalMapPostEffect3BufferDimension.Raw;
        return new(p.Index,dimension,allocation.ByteCount,usage,deviceUsage,cpuAccess,binds,misc,stride,
            format,count,raw?11u:1u,raw?1u:0u,1,raw?1u:0u);
    }
}
