using System.Buffers.Binary;

namespace San14.Data;

/// <summary>The actual mode11/type390=1 v78 controller reset. This success
/// belongs to1058680/1057510, not to any GPU resource allocation.</summary>
public sealed class DeferredEarlyShadowCurrentCompactResetFields
{
    internal DeferredEarlyShadowCurrentCompactResetFields(DeferredEarlyShadowCurrentCompactFields compact)
        => Compact=compact;
    public DeferredEarlyShadowCurrentCompactFields Compact { get; }
    public uint Type390=>1;
    public int PassCount=>Compact.Numeric.PassCount;
    public bool SourceV78Succeeded=>true;
    public string SourceEvidence=>Compact.SourceEvidence+
        ";D12470-actual-type390=1->selected17335F0.v78=1058680->1057510-own-controller-reset/AL1;"+
        "countBC0/reset-bounds0-10-20-30/flags40-44-54/BC4-BC8;no-GPU-resource-success-substitution";
    public void WriteOwnedControllerReset(Span<byte> actualOwnedCpu)
        =>DeferredEarlyShadowCompactResetSource.ApplyNumericReset(actualOwnedCpu,PassCount,Type390);
}

public static class DeferredEarlyShadowCompactResetSource
{
    public static DeferredEarlyShadowCurrentCompactResetFields PrepareCurrent(
        DeferredEarlyShadowCurrentCompactFields actualCompact)
    {
        ArgumentNullException.ThrowIfNull(actualCompact);
        // This own-camera input's D12470 producer always writes actual390=1.
        // No other direction/camera type has been admitted to that packet.
        var own=actualCompact.OwnCamera;
        DeferredEarlyShadowCurrentMainFields.RequireActualMain(own.Main.ActualMainInputs,own.Main.PreparedMain);
        return new(actualCompact);
    }

    /// <summary>Complete finite 1058680/1057510 reset. A different actual
    /// type returnsfalse without controller stores, as the source does.</summary>
    public static bool ApplyNumericReset(Span<byte> actualOwnedCpu,int actualPassCount,uint actualType390)
    {
        DeferredEarlyShadowCompactConstructor.RequireStorage(actualOwnedCpu.Length);
        if(actualType390!=1)return false;
        if((uint)actualPassCount>4u)throw new ArgumentOutOfRangeException(nameof(actualPassCount));
        for(int pass=0;pass<actualPassCount;pass++)
        {
            int unit=DeferredEarlyShadowCompactConstructor.PassBoundsOffset(pass);
            for(int bounds=0;bounds<=0x20;bounds+=0x20)
            {
                for(int component=0;component<3;component++)
                {
                    W(actualOwnedCpu,unit+bounds+component*4,0x7722B3DB);
                    W(actualOwnedCpu,unit+bounds+0x10+component*4,0xF722B3DB);
                }
                W(actualOwnedCpu,unit+bounds+0xC,0x3F800000);W(actualOwnedCpu,unit+bounds+0x1C,0x3F800000);
            }
            W(actualOwnedCpu,unit+0x40,0);W(actualOwnedCpu,unit+0x44,0);W(actualOwnedCpu,unit+0x54,0x7F7FFFFF);
        }
        BinaryPrimitives.WriteUInt64LittleEndian(actualOwnedCpu[0xBC4..],0);
        return true;
    }
    private static void W(Span<byte> bytes,int at,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(bytes[at..],value);
}
