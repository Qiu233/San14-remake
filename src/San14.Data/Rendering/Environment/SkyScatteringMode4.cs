using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Complete current package RGB sources at native97CF70. Selector0
/// uses the fog record's first color; nonzero uses the actual ambient vector
/// plus the optional48-byte hemisphere rows. Directional absence is explicit.</summary>
public sealed record SkyMode4PackageColors(Vector3? DirectionalRgb,
    byte AmbientSelector122,Vector4 Ambient30,IReadOnlyList<Vector3>? HemisphereRgb);

/// <summary>Exact native97CF70 four-row Scat writer. Inputs are a complete
/// current fog record and package colors, not a normalized LightData default.
/// The lazy conversion factor comes from the original logf(2) source kernel.</summary>
public static class SkyScatteringMode4
{
    /// <summary>All native mode4 Fog reads use the known named fields;
    /// allocation tail bytes are not needed by this consumer.</summary>
    public static SkyScatteringPacket Generate(SkyWeatherKnownFogRows knownFog,
        SkyMode4PackageColors currentColors,LoadedSkyAtmosphereNativeSource source)
    {
        ArgumentNullException.ThrowIfNull(knownFog);
        return Generate(knownFog.Values,currentColors,source);
    }

    public static SkyScatteringPacket Generate(SkyWeatherFogValues fog,
        SkyMode4PackageColors currentColors,LoadedSkyAtmosphereNativeSource source)
    {
        ArgumentNullException.ThrowIfNull(fog);
        return GenerateResolved(fog.Background,fog.RayleighCoefficients,fog.MieCoefficients,
            fog.Density,fog.Anisotropy,fog.PlanetRadius,currentColors,source);
    }

    public static SkyScatteringPacket Generate(ReadOnlySpan<byte> currentFog96,
        SkyMode4PackageColors currentColors,LoadedSkyAtmosphereNativeSource source)
    {
        ArgumentNullException.ThrowIfNull(currentColors);ArgumentNullException.ThrowIfNull(source);
        if(currentFog96.Length!=96)throw new InvalidDataException("Mode4 Scat requires the complete current96-byte fog record.");
        return GenerateResolved(new(Read(currentFog96,0),Read(currentFog96,4),Read(currentFog96,8),Read(currentFog96,12)),
            new(Read(currentFog96,0x20),Read(currentFog96,0x24),Read(currentFog96,0x28)),
            new(Read(currentFog96,0x30),Read(currentFog96,0x34),Read(currentFog96,0x38)),
            Read(currentFog96,0x2C),Read(currentFog96,0x3C),Read(currentFog96,0x1C),currentColors,source);
    }

    private static SkyScatteringPacket GenerateResolved(Vector4 background,Vector3 rayleigh,Vector3 mie,
        float density,float g,float ambientScale,SkyMode4PackageColors currentColors,LoadedSkyAtmosphereNativeSource source)
    {
        ArgumentNullException.ThrowIfNull(currentColors);ArgumentNullException.ThrowIfNull(source);
        Vector3 direction=currentColors.DirectionalRgb??Vector3.Zero;
        Finite(direction.X);Finite(direction.Y);Finite(direction.Z);
        Vector4 ambient;
        if(currentColors.AmbientSelector122==0)
            ambient=new(Checked(background.X),Checked(background.Y),Checked(background.Z),Checked(background.W));
        else
        {
            ambient=currentColors.Ambient30;
            Finite(ambient.X);Finite(ambient.Y);Finite(ambient.Z);Finite(ambient.W);
            if(currentColors.HemisphereRgb is { } hemispheres)
                foreach(var rgb in hemispheres)
                {Finite(rgb.X);Finite(rgb.Y);Finite(rgb.Z);ambient=new(A(ambient.X,rgb.X),A(ambient.Y,rgb.Y),A(ambient.Z,rgb.Z),ambient.W);}
        }
        float ax=Checked(rayleigh.X),ay=Checked(rayleigh.Y),az=Checked(rayleigh.Z);
        float bx=Checked(mie.X),by=Checked(mie.Y),bz=Checked(mie.Z);
        Finite(density);Finite(g);
        float x=A(bx,ax),y=A(by,ay),z=A(bz,az);
        float Ratio(float coefficient)=>coefficient<source.CoefficientLowerBound||coefficient>source.CoefficientUpperBound
            ?D(density,coefficient):coefficient;
        float rx=M(Ratio(x),direction.X),ry=M(Ratio(y),direction.Y),rz=M(Ratio(z),direction.Z);
        float c=source.ConversionLog2,one=source.One;
        Vector4 row0=new(M(x,c),M(y,c),M(z,c),Checked(background.W));
        float phase=M(M(S(one,g),source.MiePhaseFactor),S(one,g));
        Vector4 row3=new(M(rx,M(phase,bx)),M(ry,M(phase,by)),M(rz,M(phase,bz)),A(M(g,g),one));
        Vector4 row2=new(M(rx,M(ax,source.RayleighPhaseFactor)),M(ry,M(ay,source.RayleighPhaseFactor)),
            M(rz,M(az,source.RayleighPhaseFactor)),M(g,source.NegativeTwo));
        Finite(ambientScale);
        Vector4 row1=new(M(ambient.X,ambientScale),M(ambient.Y,ambientScale),M(ambient.Z,ambientScale),ambient.W);
        Vector4[] values=[row0,row1,row2,row3];byte[] bytes=new byte[64];
        for(int row=0;row<4;row++)
        {
            Vector4 value=values[row];Finite(value.X);Finite(value.Y);Finite(value.Z);Finite(value.W);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(row*16),value.X);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(row*16+4),value.Y);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(row*16+8),value.Z);
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(row*16+12),value.W);
        }
        return new(4,Array.AsReadOnly(values),bytes);
    }
    private static float Read(ReadOnlySpan<byte> input,int offset)
        =>BinaryPrimitives.ReadSingleLittleEndian(input.Slice(offset,4));
    private static float Checked(float value)
    {Finite(value);return value;}
    private static void Finite(float value)
    {if(!float.IsFinite(value))throw new InvalidDataException("Mode4 Scat current input/result is outside the finite active packet domain.");}
    [MethodImpl(MethodImplOptions.NoInlining)]private static float A(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float S(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float M(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float D(float a,float b)=>a/b;
}
