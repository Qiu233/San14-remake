using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>Finite current10581E0/10612E0/1061380 numerical output. The
/// same fresh constructor, own Camera, work/v48 and independent caster
/// owners supply each pass. Atlas and depth-target publication are separate.</summary>
public sealed class DeferredEarlyShadowCurrentCompactFinalFields
{
    internal DeferredEarlyShadowCurrentCompactFinalFields(
        DeferredEarlyShadowCurrentCasterFields caster,
        DeferredEarlyShadowCurrentForceFitFields forceFit,
        IReadOnlyList<DeferredEarlyShadowCompactFinalPassNumericFields> passes,
        DeferredEarlyShadowCompactSamplerNumericFields sampler)
    {
        (Caster,ForceFit,Passes,Sampler)=(caster,forceFit,passes,sampler);
        TouchedMask230=caster.Numeric.TouchedMaskBC8;
    }
    public DeferredEarlyShadowCurrentCasterFields Caster { get; }
    public DeferredEarlyShadowCurrentForceFitFields ForceFit { get; }
    public IReadOnlyList<DeferredEarlyShadowCompactFinalPassNumericFields> Passes { get; }
    public DeferredEarlyShadowCompactSamplerNumericFields Sampler { get; }
    public uint TouchedMask230 { get; }
    public string SourceEvidence=>Caster.SourceEvidence+";"+ForceFit.SourceEvidence+
        ";TC:10581E0-v50/10612E0/A059B0-actual-clip-caster-final5D0;"+
        "1061380-depthPlane6D0/relativeEnds6E0;not-atlas/depth-target-population";

    public Matrix4x4 RequireShadow5D0(int index)
    {
        if((uint)index>=(uint)Passes.Count)throw new ArgumentOutOfRangeException(nameof(index));
        return Passes[index].Shadow5D0??throw new InvalidDataException(
            "Original A059B0 left this final5D0 unwritten; the retained destination needs its actual preceding owner.");
    }

    /// <summary>Writes only domains actually written by the selected
    /// numeric branches. Null A0/870/5D0 values retain their real destination.
    /// This never fills unowned fields to construct an entire ShadowCPU.</summary>
    public void WriteOwnedNumericFields(Span<byte> actualOwnedCpu)
    {
        DeferredEarlyShadowCompactConstructor.RequireStorage(actualOwnedCpu.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(actualOwnedCpu[0x230..],TouchedMask230);
        foreach(var pass in Passes)
        {
            int unit=DeferredEarlyShadowCompactConstructor.PassBoundsOffset(pass.Index);
            BinaryPrimitives.WriteUInt32LittleEndian(actualOwnedCpu[(unit+0x48)..],ForceFit.UnitForceFit48);
            Matrix(actualOwnedCpu,0xBE0+pass.Index*64,pass.CenteredLightViewBE0);
            if(pass.Shadow5D0 is{}shadow)Matrix(actualOwnedCpu,0x5D0+pass.Index*64,shadow);
            if(pass.DepthSpanA0 is{}span)BinaryPrimitives.WriteSingleLittleEndian(actualOwnedCpu[(unit+0xA0)..],span);
            if(pass.DepthVector870 is{}depth)Vector(actualOwnedCpu,0x870+pass.Index*32,depth);
            if(pass.DepthVector880 is{}tail)Vector(actualOwnedCpu,0x880+pass.Index*32,tail);
        }
        Sampler.WriteNumericFields(actualOwnedCpu.Slice(0x6D0,32));
    }
    private static void Matrix(Span<byte> bytes,int at,Matrix4x4 m)
    {
        Vector(bytes,at,new(m.M11,m.M12,m.M13,m.M14));Vector(bytes,at+16,new(m.M21,m.M22,m.M23,m.M24));
        Vector(bytes,at+32,new(m.M31,m.M32,m.M33,m.M34));Vector(bytes,at+48,new(m.M41,m.M42,m.M43,m.M44));
    }
    private static void Vector(Span<byte> bytes,int at,Vector4 v)
    {
        BinaryPrimitives.WriteSingleLittleEndian(bytes[at..],v.X);BinaryPrimitives.WriteSingleLittleEndian(bytes[(at+4)..],v.Y);
        BinaryPrimitives.WriteSingleLittleEndian(bytes[(at+8)..],v.Z);BinaryPrimitives.WriteSingleLittleEndian(bytes[(at+12)..],v.W);
    }
}

public static class DeferredEarlyShadowCompactFinalSource
{
    /// <summary>The current fresh-constructor retained-zero4A8 route.
    /// Invoke after the actual caster callback population has accumulated
    /// its bounds. The first complete5D0 supplies the sampler plane.</summary>
    public static DeferredEarlyShadowCurrentCompactFinalFields PrepareCurrent(
        DeferredEarlyShadowCurrentCasterFields actualCaster,
        DeferredEarlyShadowCurrentForceFitFields actualForceFit)
    {
        ArgumentNullException.ThrowIfNull(actualCaster);ArgumentNullException.ThrowIfNull(actualForceFit);
        var volume=actualCaster.Volume;var compact=volume.Compact;var own=compact.OwnCamera;
        if(!ReferenceEquals(actualForceFit.Compact,compact))
            throw new InvalidDataException("Final shadow projection needs the same current compact constructor's4A8 field.");
        DeferredEarlyShadowCurrentMainFields.RequireActualMain(own.Main.ActualMainInputs,own.Main.PreparedMain);
        var passes=new DeferredEarlyShadowCompactFinalPassNumericFields[compact.Numeric.PassCount];
        for(int i=0;i<passes.Length;i++)
        {
            var pass=actualCaster.Numeric.Passes[i]??throw new NotSupportedException(
                "This final pass's retained light-view domain has no actual v48 producer in the supported current scope.");
            var clip=volume.Numeric.Passes[i]??throw new InvalidDataException("Current caster/clip owners differ.");
            passes[i]=DeferredEarlyShadowCompactFinalMatrixSource.PrepareNumericNormalFields(i,pass,
                clip.HasIntersectionPoints,own.Numeric.OwnCameraPrefix150.Span,actualForceFit.UnitForceFit48);
        }
        Matrix4x4 first=passes[0].Shadow5D0??throw new InvalidDataException(
            "The current first final5D0 was not written;1061380 needs its actual retained prior destination.");
        var sampler=DeferredEarlyShadowCompactSamplerSource.PrepareNumericFields(first,
            own.Numeric.OwnCameraPrefix150.Span,compact.Numeric.PassEndsBB0ToBBC);
        return new(actualCaster,actualForceFit,Array.AsReadOnly(passes),sampler);
    }
}
