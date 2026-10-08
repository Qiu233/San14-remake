using System.Numerics;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>CE1CCA80 mode2: three 7A0 members around the current Unit10,
/// using its raw Unit20 quaternion. This branch never writes scale.</summary>
public static class OriginalArmySiegePose
{
    public static OriginalArmyInstancePoseWrite WriteSiege(int instanceIndex,
        Vector3 unitPosition10, Vector4 rawUnitQuaternion20)
    {
        if ((uint)instanceIndex >= 3) throw new ArgumentOutOfRangeException(nameof(instanceIndex));
        float angle = Product(Add(Product(instanceIndex, 120), 30), 0.017453292f);
        var (sin, cos) = OriginalArmyAnimationSampler.SourceSinCos(angle);
        // 1CF960 rotates the source (234,0,0,1), then adds Unit10 XYZ.
        var position = new Vector4(Add(Add(Product(0, sin), Product(234, cos)), unitPosition10.X),
            Add(0, unitPosition10.Y),
            Add(Subtract(Product(0, cos), Product(234, sin)), unitPosition10.Z), 1);
        return new(-1, 0x7A0, instanceIndex, position, rawUnitQuaternion20);
    }

    [MethodImpl(MethodImplOptions.NoInlining)] private static float Product(float a, float b) => a * b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Add(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float Subtract(float a, float b) => a - b;
}
