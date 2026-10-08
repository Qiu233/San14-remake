using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

public sealed record OriginalSky2InitializerParameters(Vector4 AltitudeInfo, Vector4 StableDimensions);

/// <summary>Closed scalar/vector parts of the original 118DC30 Sky2 preparation.
/// Inputs are explicit original named fields. This does not select a current
/// camera, recover resource pointers, build the sky rotation, or manage dirty state.</summary>
public static class OriginalSky2ParameterPreparation
{
    /// <summary>Literal values from the actual Sky2 initializer 118CFA0.
    /// These are initializer values, not an assertion about a current cache.</summary>
    public static OriginalSky2InitializerParameters OriginalInitializer() => new(
        new(F(0x45F9D000), F(0x44960000), F(0x4AC21780), F(0x4AC48880)),
        new(F(0x42000000), F(0x43000000), F(0x42000000), F(0x41000000)));

    /// <summary>g_vViewPos: prepared camera+0, explicit shader+16A0 altitude,
    /// shader+1438 offset and shader+14C0's sixteen floats in native row order.</summary>
    public static Vector4 PrepareViewPosition(Vector4 explicitCameraPosition,
        Vector4 explicitAltitudeInfo, float explicitSkyYOffset, ReadOnlySpan<float> explicitSkyTransform16)
    {
        Finite(explicitCameraPosition);Finite(explicitAltitudeInfo);Finite(explicitSkyYOffset);
        if (explicitSkyTransform16.Length != 16)
            throw new InvalidDataException("Original Sky2 transform requires exactly16 native-order floats.");
        foreach (float v in explicitSkyTransform16) Finite(v);
        float x = Mul(explicitCameraPosition.X, F(0x3C23D70A));
        float y = Mul(explicitCameraPosition.Y, F(0x3C23D70A));
        y = Add(y, explicitAltitudeInfo.Z);y = Add(y, explicitSkyYOffset);
        float z = Mul(explicitCameraPosition.Z, F(0x3C23D70A)), w = explicitCameraPosition.W;
        var m = explicitSkyTransform16;
        // Preserve the source SSE reduction order: X starts Y+X; the other
        // components start X+Y. Do not use Matrix4x4.Transform or contract FMA.
        float a = Add(Mul(y,m[4]),Mul(x,m[0]));a = Add(a,Mul(z,m[8]));a = Add(a,Mul(w,m[12]));
        float b = Add(Mul(x,m[1]),Mul(y,m[5]));b = Add(b,Mul(z,m[9]));b = Add(b,Mul(w,m[13]));
        float c = Add(Mul(x,m[2]),Mul(y,m[6]));c = Add(c,Mul(z,m[10]));c = Add(c,Mul(w,m[14]));
        float d = Add(Mul(x,m[3]),Mul(y,m[7]));d = Add(d,Mul(z,m[11]));d = Add(d,Mul(w,m[15]));
        return new(a,b,c,d);
    }

    /// <summary>g_vSkyInfo0. Camera+138 is supplied as an explicit scalar,
    /// without assigning an unproved FOV meaning. Controller ZW is +7C,+78.</summary>
    public static Vector4 PrepareSkyInfo0(float explicitCameraScalar138, int explicitTargetHeight,
        bool explicitSunLightPresent, float explicitSunSize1490,
        bool explicitMoonLightPresent, float explicitMoonSize1498,
        Vector2? explicitControllerSkyInfoZW)
    {
        Finite(explicitCameraScalar138);
        if (explicitTargetHeight <= 0 || explicitTargetHeight > int.MaxValue / 2)
            throw new InvalidDataException("Original Sky2 height must have a positive nonoverflowing int32 double.");
        // Native LEA doubles the int32 height before CVTDQ2PS and DIVSS.
        int doubledHeight = checked(explicitTargetHeight * 2);
        float pixelScalar = Div(explicitCameraScalar138, (float)doubledHeight);
        float x = 1, y = 1;
        if (explicitSunLightPresent)
        {
            Finite(explicitSunSize1490);
            x = MaxSecond(pixelScalar, Mul(Mul(explicitSunSize1490,F(0x3C187400)),F(0x3F000000)));
        }
        if (explicitMoonLightPresent)
        {
            Finite(explicitMoonSize1498);
            y = MaxSecond(pixelScalar, Mul(Mul(explicitMoonSize1498,F(0x3C142917)),F(0x3F000000)));
        }
        Vector2 zw = explicitControllerSkyInfoZW ?? Vector2.One;
        Finite(zw.X);Finite(zw.Y);
        return new(x,y,zw.X,zw.Y);
    }

    /// <summary>g_vSkyInfo1. Texture presence uses shader+1678/+1698/+1688;
    /// its W compares package-light and moon-light position float4 values.</summary>
    public static Vector4 PrepareSkyInfo1(bool explicitEarthTexturePresent,
        bool explicitMoonTexturePresent, bool explicitMilkyTexturePresent,
        float explicitMilkyScale14A0, float explicitMilkyStrength1484,
        Vector4 explicitPackageLightPosition, Vector4 explicitMoonLightPosition)
    {
        Finite(explicitPackageLightPosition);Finite(explicitMoonLightPosition);
        float milky = 0;
        if (explicitMilkyTexturePresent)
        {
            Finite(explicitMilkyScale14A0);Finite(explicitMilkyStrength1484);
            milky = Mul(explicitMilkyScale14A0,explicitMilkyStrength1484);
        }
        bool equal = explicitPackageLightPosition.X == explicitMoonLightPosition.X &&
            explicitPackageLightPosition.Y == explicitMoonLightPosition.Y &&
            explicitPackageLightPosition.Z == explicitMoonLightPosition.Z &&
            explicitPackageLightPosition.W == explicitMoonLightPosition.W;
        return new(explicitEarthTexturePresent ? 1 : 0,explicitMoonTexturePresent ? 1 : 0,milky,equal ? 1 : 0);
    }

    /// <summary>g_vClampValue for explicit native target+48 format0..127.
    /// The original format integer is required; this is not a DXGI/Vulkan map.
    /// Controller+1F0, when present, replaces W after the format/delta branch.</summary>
    public static Vector4 PrepareClampValue(int explicitNativeTargetFormat,
        Vector4 explicitClampDelta1610, float? explicitControllerValue1F0)
    {
        if (explicitNativeTargetFormat is < 0 or > 127)
            throw new NotSupportedException("Original Sky2 clamp supports only explicit native formats0..127.");
        Vector4 value = new(F(0x7F7FFFFF));
        ulong mask = explicitNativeTargetFormat < 64 ? 0x18000781CUL : 0x1C03E204000F000UL;
        bool subtract = (mask & (1UL << (explicitNativeTargetFormat & 63))) != 0;
        if (subtract)
        {
            value = explicitNativeTargetFormat switch
            {
                3 or 12 or 105 or 106 => new(F(0x477FE000)),
                14 => new(F(0x477C0000),F(0x477C0000),F(0x477C0000),1),
                107 => new(F(0x477E0000),F(0x477E0000),F(0x477C0000),1),
                _ => value
            };
            Finite(explicitClampDelta1610);
            value = new(Sub(value.X,explicitClampDelta1610.X),Sub(value.Y,explicitClampDelta1610.Y),
                Sub(value.Z,explicitClampDelta1610.Z),Sub(value.W,explicitClampDelta1610.W));
        }
        if (explicitControllerValue1F0 is float controller)
        {
            Finite(controller);
            value.W = Mul(controller,F(0x3C23D70A));
        }
        return value;
    }

    private static float F(uint bits) => BitConverter.UInt32BitsToSingle(bits);
    private static void Finite(Vector4 v) { Finite(v.X);Finite(v.Y);Finite(v.Z);Finite(v.W); }
    private static void Finite(float v)
    {
        if (!float.IsFinite(v)) throw new InvalidDataException("Original Sky2 parameter inputs must be finite.");
    }
    private static float MaxSecond(float first,float second) => first > second ? first : second;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Mul(float a,float b) => a*b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Add(float a,float b) => a+b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Sub(float a,float b) => a-b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Div(float a,float b) => a/b;
}
