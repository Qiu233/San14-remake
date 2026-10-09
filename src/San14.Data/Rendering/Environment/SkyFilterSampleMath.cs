using System.Buffers.Binary;

namespace San14.Data;

public sealed record SkyFilterGeneratedSamples(uint SampleCount, ushort SourceWidth, byte LevelCount,
    uint DestinationMip, float RoughnessFourthPower, float TexelSolidAngle,
    NativeLogDispatch LogDispatch, NativeLogTables LogTables, SkyFilterSamplePacket Packet);

/// <summary>Instruction-order port of the original mip sample producer. All
/// parameters and the work limit are explicit; no active quality/variant is inferred.</summary>
public static class SkyFilterSampleMath
{
    private static float Add(float a,float b)=>NativeFloat.Add(a,b);
    private static float Sub(float a,float b)=>NativeFloat.Subtract(a,b);
    private static float Mul(float a,float b)=>NativeFloat.Multiply(a,b);
    private static float Div(float a,float b)=>NativeLogarithm.Divide(a,b);
    private static float F(uint bits)=>BitConverter.UInt32BitsToSingle(bits);

    public static SkyFilterGeneratedSamples GenerateMip(uint sampleCount, ushort sourceWidth,
        byte levelCount, uint mip, NativeLogTables tables, NativeLogDispatch dispatch, uint maximumSamples)
    {
        ArgumentNullException.ThrowIfNull(tables);
        if(sampleCount==0||sampleCount>maximumSamples||sampleCount>int.MaxValue/8||sourceWidth==0||
            levelCount is <2 or >31||mip==0||mip>=levelCount)
            throw new InvalidDataException("Sky filter producer requires explicit finite dimensions, mip, sample count and sufficient work budget.");
        float step=Div(1,levelCount-1),roughness=Mul(mip,step);
        float alpha=Mul(roughness,roughness);alpha=Mul(alpha,roughness);alpha=Mul(alpha,roughness);
        float omega=Div(F(0x40490FDB),Mul(Mul(sourceWidth,6),sourceWidth));
        float logTwo=NativeLogarithm.Log(2,tables,dispatch),total=0;
        byte[] directions=new byte[checked((int)sampleCount*8)],weights=new byte[checked((int)sampleCount*4)];
        for(uint index=0;index<sampleCount;index++)
        {
            float phi=Mul(Div(index,sampleCount),F(0x40C90FDB));
            float xi=Mul(ReverseBits(index),F(0x2F800000));
            float denominator=Add(Mul(Sub(alpha,1),xi),1);
            float cosine=MathF.Sqrt(Div(Sub(1,xi),denominator));
            float sine=MathF.Sqrt(Sub(1,Mul(cosine,cosine)));
            var trig=NativeTrigonometry.SinCos014590(phi);
            float hx=Mul(trig.Cos,sine),hy=Mul(trig.Sin,sine),clamped=Math.Clamp(cosine,0,1),twice=Add(clamped,clamped);
            float x=Mul(hx,twice),y=Mul(hy,twice),z=Sub(Mul(cosine,twice),1);
            float weight=Math.Clamp(z,0,1);total=Add(total,weight);
            float d=Add(Mul(Mul(clamped,clamped),Sub(alpha,1)),1);
            float pdf=Div(alpha,Mul(Mul(d,F(0x40490FDB)),d));
            float argument=Div(Div(1,Mul(pdf,sampleCount)),omega);
            float lod=Add(Div(Mul(NativeLogarithm.Log(argument,tables,dispatch),0.5f),logTwo),1);
            lod=MathF.Max(lod,0); // Native has no upper-mip clamp.
            int at=checked((int)index*8);
            BinaryPrimitives.WriteUInt16LittleEndian(directions.AsSpan(at),EncodeHalf9BD050(x));
            BinaryPrimitives.WriteUInt16LittleEndian(directions.AsSpan(at+2),EncodeHalf9BD050(y));
            BinaryPrimitives.WriteUInt16LittleEndian(directions.AsSpan(at+4),EncodeHalf9BD050(z));
            BinaryPrimitives.WriteUInt16LittleEndian(directions.AsSpan(at+6),EncodeHalf9BD050(lod));
            BinaryPrimitives.WriteSingleLittleEndian(weights.AsSpan(checked((int)index*4)),weight);
        }
        var packet=new SkyFilterSamplePacket(mip,directions,weights,total);
        return new(sampleCount,sourceWidth,levelCount,mip,alpha,omega,dispatch,tables,packet);
    }

    public static ushort EncodeHalf9BD050(float value)=>EncodeHalfBits9BD050(BitConverter.SingleToUInt32Bits(value));

    /// <summary>Preserves original truncation, zero-exponent and overflow branches;
    /// deliberately differs from IEEE Half conversion.</summary>
    public static ushort EncodeHalfBits9BD050(uint word)
    {
        uint sign=(word&0x80000000)>>16,mantissa=word&0x7FFFFF;
        int exponent=(int)((word>>23)&255)-112;
        if(exponent>=32)return (ushort)(sign|0x7C00);
        if(exponent>=0)return (ushort)(sign|((uint)exponent<<10)|(mantissa>>13));
        if(exponent<=-19)return (ushort)sign;
        return (ushort)(sign|((mantissa|0x800000)>>(13-exponent)));
    }

    private static uint ReverseBits(uint value)
    {
        value=(value<<16)|(value>>16);
        value=((value>>1)&0x55555555)|((value&0x55555555)<<1);
        value=((value>>2)&0x33333333)|((value&0x33333333)<<2);
        value=((value>>4)&0x0F0F0F0F)|((value&0x0F0F0F0F)<<4);
        return ((value>>8)&0x00FF00FF)|((value&0x00FF00FF)<<8);
    }
}
