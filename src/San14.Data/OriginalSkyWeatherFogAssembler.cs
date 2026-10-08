using System.Buffers.Binary;
using System.Numerics;

namespace San14.Data;

/// <summary>The actual reached AB6540 argument values. Producing these from
/// the selected Main V418 weather/atmosphere frame is a separate source stage;
/// arbitrary arguments do not establish a current Main owner publication.</summary>
public sealed record OriginalSkyWeatherFogAssemblerArguments(Vector4 Background,
    Vector4 RayleighCoefficients,Vector4 MieCoefficients,float Start14,float End18,
    float Radius1C,float Density2C,float Anisotropy3C,float Pivot40,float Blend44);

/// <summary>Only the 72 bytes actually written by AB6540. The remaining24 bytes
/// of the native Fog96 transport are retained, and have no invented value.</summary>
public sealed class OriginalSkyWeatherKnownFogRows
{
    private readonly byte[] known;
    internal OriginalSkyWeatherKnownFogRows(byte[] known)=>this.known=known;
    public ReadOnlyMemory<byte> Known72=>known.ToArray();
    public bool NativeWholeFog96IdentityPreserved=>false;
    public bool CoversMode4ScatteringReadSet=>true;
    public bool CoversMode7FinalFogReadSet=>true;
    public string SourceProvenance=>"native-TC-AB6540/SC-AB75D0-known-Fog00..47;"+
        "CE6180-no-init;Fog48..5F-retained-unknown;semantic-projection-only";
    /// <summary>Named consumer transport only. Unread24-byte padding is a
    /// backend representation; it is never native retained-byte evidence.</summary>
    public byte[] MaterializeNamedConsumerRecord()
    {
        byte[] record=new byte[96];known.CopyTo(record,0);return record;
    }
    /// <summary>Whole native storage is available only when its actual retained
    /// trailing allocation is explicitly supplied. No constructor zero implied.</summary>
    public byte[] MaterializeWithExplicitRetainedTail(ReadOnlySpan<byte> actualTail24)
    {
        if(actualTail24.Length!=24)throw new InvalidDataException("Native Fog96 requires exactly its retained24-byte tail.");
        byte[] record=new byte[96];known.CopyTo(record,0);actualTail24.CopyTo(record.AsSpan(72));return record;
    }
}

public static class OriginalSkyWeatherFogAssembler
{
    /// <summary>Actual mode7 named Fog projection: all reads are in Known72.
    /// Renderer profile and enable bit remain explicit current owner inputs.</summary>
    public static OriginalSkyFogPacket GenerateMode7Fog(OriginalSkyWeatherKnownFogRows known,
        int currentProfile,bool currentFogEnabled)
    {
        ArgumentNullException.ThrowIfNull(known);
        return OriginalSkyFogMath.Generate(7,currentProfile,currentFogEnabled,
            known.MaterializeNamedConsumerRecord());
    }
    public static OriginalSkyWeatherKnownFogRows Generate(OriginalSkyWeatherFogAssemblerArguments args)
    {
        ArgumentNullException.ThrowIfNull(args);
        foreach(float value in new[]{args.Background.X,args.Background.Y,args.Background.Z,args.Background.W,
            args.RayleighCoefficients.X,args.RayleighCoefficients.Y,args.RayleighCoefficients.Z,
            args.MieCoefficients.X,args.MieCoefficients.Y,args.MieCoefficients.Z,args.Start14,args.End18,
            args.Radius1C,args.Density2C,args.Anisotropy3C,args.Pivot40,args.Blend44})
            if(!float.IsFinite(value))throw new InvalidDataException("Known source Fog semantic writes require finite consumed words.");
        byte[] output=new byte[72];
        void Write(int at,float value)=>BinaryPrimitives.WriteSingleLittleEndian(output.AsSpan(at,4),value);
        void Vector(int at,Vector4 value){Write(at,value.X);Write(at+4,value.Y);Write(at+8,value.Z);Write(at+12,value.W);}
        Vector(0,args.Background);BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(0x10,4),7);
        Write(0x14,args.Start14);Write(0x18,args.End18);Write(0x1C,args.Radius1C);
        Vector(0x20,args.RayleighCoefficients);Write(0x2C,args.Density2C);
        Vector(0x30,args.MieCoefficients);Write(0x3C,args.Anisotropy3C);
        Write(0x40,args.Pivot40);Write(0x44,args.Blend44);
        return new(output);
    }
}
