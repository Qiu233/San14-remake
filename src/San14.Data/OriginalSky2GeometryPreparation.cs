using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

public sealed record OriginalSky2SunScreenPosition(Vector4 Position, bool InFront);

/// <summary>Closed original Sky2 rotation and Sun screen-position preparation.
/// All angles, prepared camera values and light selection are explicit inputs.</summary>
public static class OriginalSky2GeometryPreparation
{
    /// <summary>118C7C0: shader1430/1434/143C angles, in original radians.
    /// Returns sixteen floats in native row order for shader14C0.</summary>
    public static float[] PrepareSkyTransform(float explicitAngle1430,
        float explicitAngle1434, float explicitAngle143C)
    {
        Finite(explicitAngle1430);Finite(explicitAngle1434);Finite(explicitAngle143C);
        float[] rows = [1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1];
        var y = OriginalNativeTrigonometry.SinCos014590(explicitAngle143C);
        // 3F6CD0: right Y, preserving Z*sin + X*cos order.
        for (int row = 0; row < 4; row++)
        {
            int a = row*4;float x = rows[a], z = rows[a+2];
            rows[a] = A(M(z,y.Sin),M(x,y.Cos));
            rows[a+2] = S(M(z,y.Cos),M(x,y.Sin));
        }
        var xRotation = OriginalNativeTrigonometry.SinCos014590(explicitAngle1434);
        // 3F6AC0: right X.
        for (int row = 0; row < 4; row++)
        {
            int a = row*4;float v = rows[a+1], z = rows[a+2];
            rows[a+1] = S(M(v,xRotation.Cos),M(z,xRotation.Sin));
            rows[a+2] = A(M(v,xRotation.Sin),M(z,xRotation.Cos));
        }
        var zRotation = OriginalNativeTrigonometry.SinCos014590(explicitAngle1430);
        // 573AB0: right Z.
        for (int row = 0; row < 4; row++)
        {
            int a = row*4;float x = rows[a], v = rows[a+1];
            rows[a] = S(M(x,zRotation.Cos),M(v,zRotation.Sin));
            rows[a+1] = A(M(x,zRotation.Sin),M(v,zRotation.Cos));
        }
        return rows;
    }

    /// <summary>Token12 g_vSunScreenPos from 118EC52..118EDD9.
    /// Absent Sun means no original token update: return null to retain the
    /// caller's previous position and InFront state. The supplied vector is the
    /// prepared projection input: normalized negated Sun XYZ and negated original
    /// W. A raw light position cannot be supplied here. Scalars retain source-offset names.</summary>
    public static OriginalSky2SunScreenPosition? PrepareSunScreenPosition(bool explicitSunLightPresent,
        Vector4 explicitPreparedNegatedSunPosition, ReadOnlySpan<float> explicitCameraMatrixF0,
        float explicitCameraScalar138, float explicitCameraScalar13C,
        int explicitTargetWidth, int explicitTargetHeight, float explicitSunSize1490)
    {
        if (!explicitSunLightPresent) return null;
        Finite(explicitPreparedNegatedSunPosition.X);Finite(explicitPreparedNegatedSunPosition.Y);
        Finite(explicitPreparedNegatedSunPosition.Z);Finite(explicitPreparedNegatedSunPosition.W);
        Finite(explicitCameraScalar138);Finite(explicitCameraScalar13C);Finite(explicitSunSize1490);
        if (explicitCameraMatrixF0.Length != 16)
            throw new InvalidDataException("Original Sun screen preparation needs exactly16 native-order camera matrix floats.");
        foreach (float v in explicitCameraMatrixF0) Finite(v);
        if (explicitTargetWidth <= 0 || explicitTargetHeight <= 0)
            throw new InvalidDataException("Original Sun screen target dimensions must be positive.");
        // Upstream native light preparation owns sign and XYZ normalization.
        // This helper starts at the prepared registers entering 118EC52.
        float x = explicitPreparedNegatedSunPosition.X, y = explicitPreparedNegatedSunPosition.Y,
            z = explicitPreparedNegatedSunPosition.Z, w = explicitPreparedNegatedSunPosition.W;
        var m = explicitCameraMatrixF0;
        float clipX = A(M(y,m[4]),M(x,m[0]));clipX = A(clipX,M(z,m[8]));clipX = A(clipX,M(w,m[12]));
        float clipY = A(M(y,m[5]),M(x,m[1]));clipY = A(clipY,M(z,m[9]));clipY = A(clipY,M(w,m[13]));
        float clipW = A(M(y,m[7]),M(x,m[3]));clipW = A(clipW,M(z,m[11]));clipW = A(clipW,M(w,m[15]));
        float inverseW = D(1,clipW), screenX = M(clipX,inverseW), screenY = M(clipY,inverseW);
        bool inFront = clipW > 0;
        if (!inFront) screenX = screenY = -1000;
        float targetScalar = D(2,(float)Math.Max(explicitTargetWidth,explicitTargetHeight));
        float radius = M(M(explicitSunSize1490,BitConverter.UInt32BitsToSingle(0x3C187400)),.5f);
        float cameraRatio = D(radius,explicitCameraScalar138);
        float maximum = targetScalar > cameraRatio ? targetScalar : cameraRatio;
        // No epsilon substitution. Original IEEE division may yield NaN/Inf
        // when camera138 is zero; caller decides whether to reject that result.
        return new(new(screenX,screenY,explicitCameraScalar13C,A(maximum,maximum)),inFront);
    }

    private static void Finite(float v)
    {
        if (!float.IsFinite(v)) throw new InvalidDataException("Original Sky2 geometry inputs must be finite.");
    }
    private static float M(float a,float b) => OriginalNativeFloat.Multiply(a,b);
    private static float A(float a,float b) => OriginalNativeFloat.Add(a,b);
    private static float S(float a,float b) => OriginalNativeFloat.Subtract(a,b);
    [MethodImpl(MethodImplOptions.NoInlining)] private static float D(float a,float b) => a/b;
}
