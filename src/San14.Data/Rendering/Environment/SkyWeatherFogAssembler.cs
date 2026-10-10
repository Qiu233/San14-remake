using System.Numerics;

namespace San14.Data;

/// <summary>The actual reached AB6540 argument values. Producing these from
/// the selected Main V418 weather/atmosphere frame is a separate source stage;
/// arbitrary arguments do not establish a current Main owner publication.</summary>
public sealed record SkyWeatherFogAssemblerArguments(Vector4 Background,
    Vector4 RayleighCoefficients,Vector4 MieCoefficients,float Start14,float End18,
    float Radius1C,float Density2C,float Anisotropy3C,float Pivot40,float Blend44);

/// <summary>Only the 72 bytes actually written by AB6540. The remaining24 bytes
/// of the native Fog96 transport are retained, and have no invented value.</summary>
public sealed class SkyWeatherKnownFogRows
{
    internal SkyWeatherKnownFogRows(SkyWeatherFogValues values)=>Values=values;
    public SkyWeatherFogValues Values { get; }
    public ReadOnlyMemory<byte> Known72=>SkyWeatherFogEncoding.Encode(Values);
    public bool NativeWholeFog96IdentityPreserved=>false;
    public bool CoversMode4ScatteringReadSet=>true;
    public bool CoversMode7FinalFogReadSet=>true;
    public string SourceProvenance=>"native-TC-AB6540/SC-AB75D0-known-Fog00..47;"+
        "CE6180-no-init;Fog48..5F-retained-unknown;semantic-projection-only";
    /// <summary>Named consumer transport only. Unread24-byte padding is a
    /// backend representation; it is never native retained-byte evidence.</summary>
    public byte[] MaterializeNamedConsumerRecord()
    {
        return SkyWeatherFogEncoding.MaterializeRecord(Values,new byte[24]);
    }
    /// <summary>Whole native storage is available only when its actual retained
    /// trailing allocation is explicitly supplied. No constructor zero implied.</summary>
    public byte[] MaterializeWithExplicitRetainedTail(ReadOnlySpan<byte> actualTail24)
    {
        return SkyWeatherFogEncoding.MaterializeRecord(Values,actualTail24);
    }
}

public static class SkyWeatherFogAssembler
{
    /// <summary>Actual mode7 named Fog projection: all reads are in Known72.
    /// Renderer profile and enable bit remain explicit current owner inputs.</summary>
    public static SkyFogPacket GenerateMode7Fog(SkyWeatherKnownFogRows known,
        int currentProfile,bool currentFogEnabled)
    {
        ArgumentNullException.ThrowIfNull(known);
        return SkyFogMath.GenerateWeather(known.Values,currentProfile,currentFogEnabled);
    }
    public static SkyWeatherKnownFogRows Generate(SkyWeatherFogAssemblerArguments args)
    {
        ArgumentNullException.ThrowIfNull(args);
        foreach(float value in new[]{args.Background.X,args.Background.Y,args.Background.Z,args.Background.W,
            args.RayleighCoefficients.X,args.RayleighCoefficients.Y,args.RayleighCoefficients.Z,
            args.MieCoefficients.X,args.MieCoefficients.Y,args.MieCoefficients.Z,args.Start14,args.End18,
            args.Radius1C,args.Density2C,args.Anisotropy3C,args.Pivot40,args.Blend44})
            if(!float.IsFinite(value))throw new InvalidDataException("Known source Fog semantic writes require finite consumed words.");
        return new(new(args.Background,args.Start14,args.End18,args.Radius1C,
            new(args.RayleighCoefficients.X,args.RayleighCoefficients.Y,args.RayleighCoefficients.Z),args.Density2C,
            new(args.MieCoefficients.X,args.MieCoefficients.Y,args.MieCoefficients.Z),args.Anisotropy3C,
            args.Pivot40,args.Blend44));
    }
}
