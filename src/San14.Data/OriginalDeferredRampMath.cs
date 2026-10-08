using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

public readonly record struct OriginalDeferredInt4(int X, int Y, int Z, int W);
public sealed record OriginalDeferredRimTableInputs(IReadOnlyList<Vector4> Colors,
    IReadOnlyList<OriginalDeferredInt4> Extents)
{
    /// <summary>First sixteen reflected rows only, after successful exact
    /// shared rim-table construction and with no subsequent table setters.</summary>
    public static OriginalDeferredRimTableInputs SuccessfulDataDefaults() => new(
        Array.AsReadOnly(Enumerable.Repeat(new Vector4(2,2,2,2),16).ToArray()),
        Array.AsReadOnly(Enumerable.Repeat(new OriginalDeferredInt4(0,0,-1,0),16).ToArray()));
}
public sealed record OriginalDeferredRampInputs(int Type, int TextureHeight,
    IReadOnlyList<Vector4> Parameters, IReadOnlyList<Vector4> RimParameters,
    IReadOnlyList<Vector4> SecondaryRimParameters, float RimThreshold)
{
    /// <summary>The exact PB2IndexedRamp successful default callback before
    /// any prepare-time rim modifiers. The mutable global type and selected
    /// texture height remain explicit inputs, never inferred from PE bytes.</summary>
    public static OriginalDeferredRampInputs SuccessfulDefaultCallback(int sourceGlobalType, int sourceTextureHeight) =>
        new(sourceGlobalType,sourceTextureHeight,Array.AsReadOnly(new Vector4[3]),
            Array.AsReadOnly(new Vector4[2]),Array.AsReadOnly(new Vector4[2]),1);
}

/// <summary>Original PB2IndexedRamp and shared rim-table byte projections.
/// Prepared blocks must already reflect original setters. This reader does
/// not replace the camera/angle-dependent rim preparation algorithms.</summary>
public static class OriginalDeferredRampMath
{
    /// <summary>TC9B1440 aliases table+20 and table+420 as native arrays64.
    /// The exact PS1 reflects the first sixteen rows of each. Extents are
    /// copied as int32 bits, despite their native float4-shaped alias.</summary>
    public static OriginalDeferredRimTableInputs ReadRimTablePrefix(ReadOnlySpan<byte> sourceTablePrefix)
    {
        if(sourceTablePrefix.Length!=0x520)
            throw new InvalidDataException("PS1 rim tables require the explicit shared table prefix0x520.");
        var colors=new Vector4[16];var extents=new OriginalDeferredInt4[16];
        for(int i=0;i<16;i++)
        {
            colors[i]=Vector(sourceTablePrefix,0x20+16*i);
            int at=0x420+16*i;
            extents[i]=new(Int(sourceTablePrefix,at),Int(sourceTablePrefix,at+4),Int(sourceTablePrefix,at+8),Int(sourceTablePrefix,at+12));
        }
        return new(Array.AsReadOnly(colors),Array.AsReadOnly(extents));
    }

    /// <summary>TC9F86F0's callback aliases module80/B0/D0 as arrays3/2/2;
    /// later source helpers update those same aliases. The type is the current
    /// global1A8BA18, height is prepared module68, and threshold is the caller's
    /// explicit final named parameter after any prepare-time rim update.</summary>
    public static OriginalDeferredRampInputs ReadPreparedModule(ReadOnlySpan<byte> preparedModulePrefix,
        int sourceGlobalType,float sourceFinalRimThreshold)
    {
        if(preparedModulePrefix.Length!=0xF0)
            throw new InvalidDataException("Prepared PB2IndexedRamp requires its explicit module prefix0xF0.");
        if(!float.IsFinite(sourceFinalRimThreshold))
            throw new InvalidDataException("PB2IndexedRamp requires the finite explicit final threshold.");
        var parameters=ReadRows(preparedModulePrefix,0x80,3);
        var rim=ReadRows(preparedModulePrefix,0xB0,2);
        var secondary=ReadRows(preparedModulePrefix,0xD0,2);
        return new(sourceGlobalType,Int(preparedModulePrefix,0x68),parameters,rim,secondary,sourceFinalRimThreshold);
    }
    private static IReadOnlyList<Vector4> ReadRows(ReadOnlySpan<byte> source,int at,int count)
    {
        var rows=new Vector4[count];for(int i=0;i<count;i++)rows[i]=Vector(source,at+16*i);
        return Array.AsReadOnly(rows);
    }
    private static Vector4 Vector(ReadOnlySpan<byte> source,int at)
    {
        var v=new Vector4(Float(source,at),Float(source,at+4),Float(source,at+8),Float(source,at+12));
        if(new[]{v.X,v.Y,v.Z,v.W}.Any(x=>!float.IsFinite(x)))
            throw new InvalidDataException("Original ramp/rim color parameter contains a nonfinite consumed value.");
        return v;
    }
    private static float Float(ReadOnlySpan<byte> source,int at)=>BinaryPrimitives.ReadSingleLittleEndian(source.Slice(at,4));
    private static int Int(ReadOnlySpan<byte> source,int at)=>BinaryPrimitives.ReadInt32LittleEndian(source.Slice(at,4));
}
