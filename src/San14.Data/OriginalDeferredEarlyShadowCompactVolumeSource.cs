using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

public sealed record OriginalDeferredEarlyShadowCompactPassNumericFields(int Index,
    Vector4 ClipMinimum, Vector4 ClipMaximum, Matrix4x4 LightView, Matrix4x4 AxisBasis,
    bool HasIntersectionPoints);

/// <summary>1058260/1061760/A06350's complete numeric pass outputs for an
/// actual nonempty type390=1 work inventory. Final caster5D0 is separate.</summary>
public sealed class OriginalDeferredEarlyShadowCompactVolumeNumericFields
{
    internal OriginalDeferredEarlyShadowCompactVolumeNumericFields(
        IReadOnlyList<OriginalDeferredEarlyShadowCompactPassNumericFields?> passes,uint effectiveMask)
        => (Passes,EffectiveMaskBC4)=(passes,effectiveMask);
    public IReadOnlyList<OriginalDeferredEarlyShadowCompactPassNumericFields?> Passes { get; }
    public uint EffectiveMaskBC4 { get; }

    /// <summary>Only actual clip bounds/unit40/axis60, BE0 light views and
    /// effectiveBC4. Caster accumulation20/30,dirty44,touchedBC8, final5D0,
    /// resources and130 pointer retirement remain distinct producer stages.</summary>
    public void WriteOwnControllerNumericFields(Span<byte> actualOwnedCpu)
    {
        OriginalDeferredEarlyShadowCompactConstructor.RequireStorage(actualOwnedCpu.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(actualOwnedCpu[0xBC4..],EffectiveMaskBC4);
        foreach(var pass in Passes)
        {
            if(pass is null)continue;
            int unit=OriginalDeferredEarlyShadowCompactConstructor.PassBoundsOffset(pass.Index);
            V(actualOwnedCpu,unit,pass.ClipMinimum);V(actualOwnedCpu,unit+0x10,pass.ClipMaximum);
            BinaryPrimitives.WriteUInt32LittleEndian(actualOwnedCpu[(unit+0x40)..],pass.HasIntersectionPoints?1u:0u);
            M(actualOwnedCpu,unit+0x60,pass.AxisBasis);M(actualOwnedCpu,0xBE0+pass.Index*64,pass.LightView);
        }
    }
    private static void V(Span<byte> bytes,int at,Vector4 v)
    {
        BinaryPrimitives.WriteSingleLittleEndian(bytes[at..],v.X);BinaryPrimitives.WriteSingleLittleEndian(bytes[(at+4)..],v.Y);
        BinaryPrimitives.WriteSingleLittleEndian(bytes[(at+8)..],v.Z);BinaryPrimitives.WriteSingleLittleEndian(bytes[(at+12)..],v.W);
    }
    private static void M(Span<byte> bytes,int at,Matrix4x4 m)
    {
        V(bytes,at,new(m.M11,m.M12,m.M13,m.M14));V(bytes,at+16,new(m.M21,m.M22,m.M23,m.M24));
        V(bytes,at+32,new(m.M31,m.M32,m.M33,m.M34));V(bytes,at+48,new(m.M41,m.M42,m.M43,m.M44));
    }
}

public sealed class OriginalDeferredEarlyShadowCurrentCompactVolumeFields
{
    internal OriginalDeferredEarlyShadowCurrentCompactVolumeFields(OriginalDeferredEarlyShadowCurrentCompactFields compact,
        OriginalDeferredEarlyShadowCurrentWorkPublication work,OriginalDeferredEarlyShadowCompactVolumeNumericFields numeric)
        =>(Compact,ConsumedWork,Numeric)=(compact,work,numeric);
    public OriginalDeferredEarlyShadowCurrentCompactFields Compact { get; }
    public OriginalDeferredEarlyShadowCurrentWorkPublication ConsumedWork { get; }
    public OriginalDeferredEarlyShadowCompactVolumeNumericFields Numeric { get; }
    public string SourceEvidence=>Compact.SourceEvidence+";"+ConsumedWork.SourceEvidence+
        ";TC:1058260->1061760-ownCamera130/134-pass-lens/restored;A06350-A07030/A040E0/A03BB0/A04C00/A075F0;"+
        "BE0-own-light-view/unit0-10-clip/unit60-axis/BC4-effective;successful-v48-clears130;not-final-caster5D0";
}

public static class OriginalDeferredEarlyShadowCompactVolumeSource
{
    /// <summary>Consumes the true current130 binding only after the complete
    /// selectedv48 numeric producer succeeds. The same own-camera owner is
    /// retained; caller cannot replace the inventory by Main-visible objects.</summary>
    public static OriginalDeferredEarlyShadowCurrentCompactVolumeFields PrepareCurrent(
        OriginalDeferredEarlyShadowCurrentCompactFields actualCompact,
        OriginalDeferredEarlyShadowCurrentWorkPublication actualWork,
        OriginalNativeCameraTangentDispatch tangentDispatch)
    {
        ArgumentNullException.ThrowIfNull(actualCompact);ArgumentNullException.ThrowIfNull(actualWork);
        var own=actualCompact.OwnCamera;
        if(!ReferenceEquals(actualWork.OwnCamera,own)||actualWork.Type390!=1)
            throw new InvalidDataException("Selected compactv48 requires the same actual type1 ownCamera/work130 publication.");
        OriginalDeferredEarlyShadowCurrentMainFields.RequireActualMain(own.Main.ActualMainInputs,own.Main.PreparedMain);
        var prefix=own.Numeric.OwnCameraPrefix150;
        float half=OriginalNativeFloat.Multiply(BinaryPrimitives.ReadSingleLittleEndian(prefix.Span[0x138..]),.5f);
        float tangent=OriginalNativeCameraTangent.Tangent1020A90(half,tangentDispatch);
        OriginalDeferredEarlyShadowCompactVolumeNumericFields? numeric=null;
        actualWork.ConsumeCurrentMatrices(matrices=>numeric=PrepareNumericFieldsFromTangent(prefix.Span,
            own.Direction.Numeric.Camera160,actualCompact.Numeric.PassEndsBB0ToBBC,actualCompact.Numeric.PassCount,matrices,tangent));
        return new(actualCompact,actualWork,numeric!);
    }

    /// <summary>Pure original type1, nonempty-work numeric kernel. Actual
    /// pointers, record7C capacity and source publication are not inferred.</summary>
    public static OriginalDeferredEarlyShadowCompactVolumeNumericFields PrepareNumericFieldsFromTangent(
        ReadOnlySpan<byte> actualOwnCameraPrefix150,Vector4 actualOwnDirection160,
        IReadOnlyList<float> actualEndsBB0,int actualPassCount,IReadOnlyList<Matrix4x4> actualWorkMatrices,
        float nativeTangentOfHalfFov)
    {
        if(actualOwnCameraPrefix150.Length!=0x150||actualPassCount is <1 or >4||actualEndsBB0.Count!=4)
            throw new ArgumentException("Selected compactv48 needs actual150 ownCamera/count1..4/four endpoints.");
        if(actualWorkMatrices.Count==0)
            throw new NotSupportedException("The empty-work CPU4A8/unit48 source branch requires its separate actual producer.");
        Vector4 eye=Read(actualOwnCameraPrefix150,0),target=Read(actualOwnCameraPrefix150,0x10),up=Read(actualOwnCameraPrefix150,0x20);
        var forward=OriginalDeferredEarlyShadowConvexVolumeMath.NormalizeForwardA06350(eye,target);
        var basis=OriginalDeferredEarlyShadowConvexVolumeMath.OwnFrustumBasisA07030(eye,forward,up);
        var light=OriginalDeferredEarlyShadowLightViewSource.PrepareNumericFields(eye,actualOwnDirection160);
        var boxes=actualWorkMatrices.Select(OriginalDeferredEarlyShadowConvexVolumeMath.WorkBoxA03BB0).ToArray();
        float near=BinaryPrimitives.ReadSingleLittleEndian(actualOwnCameraPrefix150[0x130..]),
            aspect=BinaryPrimitives.ReadSingleLittleEndian(actualOwnCameraPrefix150[0x13C..]);
        float horizontal=OriginalNativeFloat.Multiply(nativeTangentOfHalfFov,aspect);
        var passes=new OriginalDeferredEarlyShadowCompactPassNumericFields?[actualPassCount];
        uint effective=0;
        for(int pass=0;pass<actualPassCount;pass++)
        {
            float far=actualEndsBB0[pass];
            if(far>near)
            {
                var frustum=OriginalDeferredEarlyShadowConvexVolumeMath.FrustumA040E0(basis,near,far,horizontal,nativeTangentOfHalfFov);
                var points=new List<Vector4>(boxes.Length*8);
                foreach(var box in boxes)points.AddRange(OriginalDeferredEarlyShadowConvexVolumeMath.IntersectA04C00(box,frustum));
                var bounds=OriginalDeferredEarlyShadowConvexVolumeMath.LightBoundsA075F0(points,light.LightView);
                bool any=points.Count!=0;
                passes[pass]=new(pass,bounds.Minimum,bounds.Maximum,light.LightView,light.LightToWorldRigid,any);
                if(any)effective|=1u<<pass;
            }
            near=far;
        }
        return new(Array.AsReadOnly(passes),effective);
    }
    private static Vector4 Read(ReadOnlySpan<byte> bytes,int at)=>new(BinaryPrimitives.ReadSingleLittleEndian(bytes[at..]),
        BinaryPrimitives.ReadSingleLittleEndian(bytes[(at+4)..]),BinaryPrimitives.ReadSingleLittleEndian(bytes[(at+8)..]),
        BinaryPrimitives.ReadSingleLittleEndian(bytes[(at+12)..]));
}
