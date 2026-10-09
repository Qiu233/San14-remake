namespace San14.Data;

public enum OriginalMapPostEffect3TextureDimension { Texture2D, Texture3D }

/// <summary>'s exact six format mapping outputs. The installed mapping
/// permits typeless resources with typed views; native codes are not DXGI codes.</summary>
public sealed record OriginalMapPostEffect3TextureFormats(uint ResourceFormat,
    uint ShaderResourceFormat0,uint ShaderResourceFormat1,uint RenderTargetFormat0,
    uint RenderTargetFormat1,uint UnorderedAccessFormat);

public sealed record OriginalMapPostEffect3TextureShape(int ParameterIndex,
    OriginalMapPostEffect3TextureDimension Dimension,uint Width,uint Height,uint Depth,
    int MipLevels,bool AllowsUnorderedAccess,bool HasRenderTargetPath,
    uint NativeFlagBit3,uint NativeFlagBit4,OriginalMapPostEffect3TextureFormats Formats,
    uint NativeAllocatorSlot);

/// <summary> -> allocator v20/v28 -> native D3D texture creation.
/// This only resolves declared resource shape, not pass choice or live views.</summary>
public static class OriginalMapPostEffect3AllocationShapeReader
{
    public static OriginalMapPostEffect3TextureShape Read(OriginalMapPostEffect3TextureAllocation allocation)
    {
        ArgumentNullException.ThrowIfNull(allocation);
        var p=allocation.Parameter;
        var dimension=p.NativeKind switch {
            0=>OriginalMapPostEffect3TextureDimension.Texture2D,
            1=>OriginalMapPostEffect3TextureDimension.Texture3D,
            _=>throw new NotSupportedException("3SPK texture shape kind has no source-supported creator.")};
        // Native v20 ignores request+10; v28 consumes it as Texture3D depth.
        uint depth=dimension==OriginalMapPostEffect3TextureDimension.Texture3D?p.RawValue20:1u;
        if(depth==0||depth>int.MaxValue)throw new NotSupportedException("3SPK volume depth is outside positive native signed extent.");
        uint smallest=Math.Min(allocation.Width,allocation.Height);
        if(dimension==OriginalMapPostEffect3TextureDimension.Texture3D)smallest=Math.Min(smallest,depth);
        int chain=0;for(uint n=smallest;n!=0;n>>=1)chain++;
        if(chain==0)throw new NotSupportedException("3SPK mip chain requires a positive texture extent.");
        int levels=p.RawSignedByte1C>=chain?chain:Math.Max(1,(int)p.RawSignedByte1C);
        bool uav=(p.NativeFlags&1)!=0,rt=dimension==OriginalMapPostEffect3TextureDimension.Texture2D&&(p.NativeFlags&2)!=0;
        return new(p.Index,dimension,allocation.Width,allocation.Height,depth,levels,uav,rt,
            (uint)((p.NativeFlags>>3)&1),(uint)((p.NativeFlags>>4)&1),ReadFormats(p.NativeFormat),allocation.NativeCreateSlot);
    }

    // Original callback, installed format codes only. Each tuple is an
    // exact positive table join, not a guessed family or a universal fallback.
    public static OriginalMapPostEffect3TextureFormats ReadFormats(uint code)=>code switch {
        0=>new(27,28,29,28,29,28),
        2=>new(39,41,0,41,0,41),
        3=>new(9,10,0,10,0,10),
        42=>new(61,61,0,61,0,61),
        70=>new(49,49,0,49,0,49),
        103=>new(27,30,0,30,0,30),
        104=>new(48,50,0,50,0,50),
        105=>new(33,34,0,34,0,34),
        106=>new(53,54,0,54,0,54),
        107=>new(26,26,0,26,0,26),
        _=>throw new NotSupportedException("3SPK native texture format is outside the installed source table subset.")};
}
