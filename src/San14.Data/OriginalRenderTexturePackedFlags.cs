using System.Buffers.Binary;

namespace San14.Data;

/// <summary>The RT wrapper embeds its backend texture descriptor at +10:
/// wrapper+46/+48 are descriptor+36/+38. This receipt describes an owned
/// successful RT2D loader result, not the current state of an original process.</summary>
public sealed class OriginalRenderTexturePackedFlagsReceipt
{
    private readonly byte[] sampler;
    internal OriginalRenderTexturePackedFlagsReceipt(OriginalRenderTextureRuntimeDescriptor descriptor,
        ushort constructorFlags,ushort flags,byte[] key,string evidence)
    {Descriptor=descriptor;ConstructorFlags36=constructorFlags;Flags36=flags;sampler=key;SourceEvidence=evidence;}
    public OriginalRenderTextureRuntimeDescriptor Descriptor{get;}
    public string OwnerIdentity=>Descriptor.OwnerIdentity;
    public ushort ConstructorFlags36{get;}
    public ushort Flags36{get;}
    public ushort Sampler38=>BinaryPrimitives.ReadUInt16LittleEndian(sampler);
    public ReadOnlyMemory<byte> CompleteSamplerKey38=>sampler;
    public string SourceEvidence{get;}
}

public static class OriginalRenderTexturePackedFlags
{
    /// <summary>Port of the successful native 2D construction + sampler loader +
    /// view selection. The caller must finish its owned view selection before
    /// accepting the receipt. Failed native96C6B0 leaves the previous flag and
    /// cannot be represented as a completed loader. Arrays need a separate
    /// constructor route and are deliberately not accepted by this API.</summary>
    public static OriginalRenderTexturePackedFlagsReceipt ConstructAfterSuccessful2DLoader(
        LoadedOriginalDeferredSamplerNativeSource native,OriginalRenderTextureRuntimeDescriptor descriptor,
        string successfulOwnedViewSelectionEvidence)
    {
        ArgumentNullException.ThrowIfNull(native);ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(successfulOwnedViewSelectionEvidence);
        OriginalRenderTextureResolutionFrame.ValidateDeclaration(descriptor.Declaration);
        if(descriptor.Kind!=OriginalRenderTextureKind.Texture2D||descriptor.ArrayDepth is not null)
            throw new NotSupportedException("Packed RT2D flags do not prove the array constructor route.");
        if(descriptor.LevelNum is <1 or >31||descriptor.ColorSpace>1||descriptor.OutputColorSpace>2)
            throw new InvalidDataException("Packed RT2D flags require an admitted source mip/view mode.");
        _=OriginalRenderTextureFormatMath.Resolve(descriptor.PixelFormat,descriptor.ColorSpace,descriptor.OutputColorSpace);
        //96AFC0: native code low7, fresh non-array bits7..9=0, levels bits10..14.
        //C06FAE: OutputColorSpace2 resolves to the declared ColorSpace before
        //94EA60 passes the constructor's initial view selector (stack50).
        byte targetMode=descriptor.OutputColorSpace==2?descriptor.ColorSpace:descriptor.OutputColorSpace;
        ushort low=(ushort)((descriptor.PixelFormat&0x7F)|((descriptor.LevelNum&31)<<10));
        ushort constructor=(ushort)(low|(targetMode<<15));
        //C07059..C07070 ->96BD40 ->96C6B0: successful selection replaces bit15.
        ushort final=(ushort)(low|(descriptor.ColorSpace<<15));
        //The sampler depends on unchanged source sampler fields. A native
        //format/view clone changes none of those fields or this 8B key.
        byte[] key=OriginalDeferredSamplerSources.FinalRenderTextureKey(native,descriptor.Declaration);
        return new(descriptor,constructor,final,key,
            native.ResourceUri+";TC:96AFC0..96B0ED/94EE63..94EEBB/C06FAE..C07152;"+
            "descriptor-origin=wrapper+10;successful96BD40/96C6B0-bit15;"+descriptor.SourceProvenance+";"+
            successfulOwnedViewSelectionEvidence);
    }
}
