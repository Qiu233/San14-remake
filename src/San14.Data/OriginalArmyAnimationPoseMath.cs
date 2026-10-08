using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>Original scalar pose arithmetic (TC 1CF770 and 16700). Inputs are
/// neither normalized nor interpreted as current original animation activity.</summary>
public static class OriginalArmyAnimationPoseMath
{
    /// <summary>Original parent × local quaternion multiplication and float grouping.</summary>
    public static Quaternion Multiply(Quaternion parent, Quaternion local)
    {
        float dot = Add(Add(Product(local.X, parent.X), Product(local.Y, parent.Y)),
            Product(local.Z, parent.Z));
        float x = Add(Subtract(Product(local.Z, parent.Y), Product(parent.Z, local.Y)),
            Add(Product(parent.X, local.W), Product(local.X, parent.W)));
        float y = Add(Subtract(Product(parent.Z, local.X), Product(local.Z, parent.X)),
            Add(Product(parent.Y, local.W), Product(local.Y, parent.W)));
        float z = Add(Subtract(Product(parent.X, local.Y), Product(local.X, parent.Y)),
            Add(Product(parent.Z, local.W), Product(local.Z, parent.W)));
        return new(x, y, z, Subtract(Product(local.W, parent.W), dot));
    }

    /// <summary>Original q × vector × conjugate(q), preserving the input W word.
    /// A nonunit quaternion scales xyz by its squared norm.</summary>
    public static Vector4 Rotate(Quaternion parent, Vector4 scaledLocalTranslation)
    {
        float dot = Add(Add(Product(scaledLocalTranslation.X, parent.X),
            Product(scaledLocalTranslation.Y, parent.Y)), Product(scaledLocalTranslation.Z, parent.Z));
        float x = Add(Subtract(Product(scaledLocalTranslation.Z, parent.Y),
            Product(parent.Z, scaledLocalTranslation.Y)), Product(scaledLocalTranslation.X, parent.W));
        float y = Add(Subtract(Product(parent.Z, scaledLocalTranslation.X),
            Product(scaledLocalTranslation.Z, parent.X)), Product(scaledLocalTranslation.Y, parent.W));
        float z = Add(Subtract(Product(parent.X, scaledLocalTranslation.Y),
            Product(scaledLocalTranslation.X, parent.Y)), Product(scaledLocalTranslation.Z, parent.W));
        float resultX = Add(Product(x, parent.W),
            Add(Subtract(Product(parent.Y, z), Product(parent.Z, y)), Product(parent.X, dot)));
        float resultY = Add(Product(y, parent.W),
            Add(Subtract(Product(parent.Z, x), Product(parent.X, z)), Product(parent.Y, dot)));
        float resultZ = Add(Product(z, parent.W),
            Add(Subtract(Product(parent.X, y), Product(parent.Y, x)), Product(parent.Z, dot)));
        return new(resultX, resultY, resultZ, scaledLocalTranslation.W);
    }

    // Preserve the original separate scalar SSE rounding; no FMA contraction.
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Product(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Add(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Subtract(float a, float b) => a - b;
}
