using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>C44ED0's finite weather group on the caller's actual prepared
/// Camera. Reads the current fields after V418 and writes only its21 words.
/// This does not construct a Camera or resolve its resource/STA owners.</summary>
public static class PreparedCameraWeather
{
    public const int CameraByteLength = 0x18C0;

    public static void ApplyCurrentFields(Span<byte> sameCamera)
    {
        if (sameCamera.Length != CameraByteLength)
            throw new InvalidDataException("Camera weather requires the actual complete18C0 target storage.");
        float cloud = F(sameCamera, 0x1D0), humidity = F(sameCamera, 0x1D8);
        float temperature = F(sameCamera, 0x1D4), precipitation = F(sameCamera, 0x1DC);
        float cloudHumidity = Range(humidity, F(sameCamera, 0x78C), F(sameCamera, 0x790));
        float amount = M(Clamp(M(cloud, F(sameCamera, 0x644))), cloudHumidity);
        float baseWeight = Range(amount, F(sameCamera, 0x6E0), F(sameCamera, 0x6DC));
        float detailWeight = Range(amount, F(sameCamera, 0x718), F(sameCamera, 0x714));
        float cirrusHumidity = Range(humidity, F(sameCamera, 0x794), F(sameCamera, 0x798));
        float snow = Clamp(M(temperature, .2f));
        for (int lane = 0; lane < 3; lane++)
        {
            W(sameCamera, 0x6B8 + lane * 4,
                Blend(F(sameCamera, 0x6D0 + lane * 4), F(sameCamera, 0x6C4 + lane * 4), baseWeight));
            W(sameCamera, 0x6F0 + lane * 4,
                Blend(F(sameCamera, 0x708 + lane * 4), F(sameCamera, 0x6FC + lane * 4), detailWeight));
        }
        W(sameCamera, 0x640, amount);
        W(sameCamera, 0x628, Blend(F(sameCamera, 0x62C), F(sameCamera, 0x630), Range(humidity, F(sameCamera, 0x634), 1)));
        W(sameCamera, 0x64C, M(Clamp(Blend(F(sameCamera, 0x650), F(sameCamera, 0x654), cloud)), cloudHumidity));
        W(sameCamera, 0x658, A(M(Max(F(sameCamera, 0x660), F(sameCamera, 0x65C)), humidity), M(S(1, humidity), F(sameCamera, 0x65C))));
        W(sameCamera, 0x748, M(S(1, cirrusHumidity), Clamp(Blend(F(sameCamera, 0x74C), F(sameCamera, 0x750), cloud))));
        W(sameCamera, 0x754, M(S(1, cirrusHumidity), Clamp(Blend(F(sameCamera, 0x758), F(sameCamera, 0x75C), cloud))));
        // These stores preserve the original bits, including signed zero.
        sameCamera.Slice(0x1DC, 4).CopyTo(sameCamera.Slice(0x1250, 4));
        sameCamera.Slice(0x1D4, 4).CopyTo(sameCamera.Slice(0x1258, 4));
        W(sameCamera, 0x113C, M(precipitation > 0 ? 1 : 0, snow));
        W(sameCamera, 0x1140, M(Clamp(D(precipitation,
            Max(BitConverter.UInt32BitsToSingle(0x37480000), F(sameCamera, 0x119C)))), snow));
        W(sameCamera, 0x1154, Clamp(S(1, F(sameCamera, 0x1144))));
        W(sameCamera, 0xF24, temperature <= F(sameCamera, 0xF50) ? 1 : 0);
        W(sameCamera, 0xF0C, SnowGate(temperature, F(sameCamera, 0xF50), F(sameCamera, 0xF54), F(sameCamera, 0xF08)));
        W(sameCamera, 0xF14, SnowGate(temperature, F(sameCamera, 0xF50), F(sameCamera, 0xF58), F(sameCamera, 0xF10)));
        W(sameCamera, 0x1020, M(F(sameCamera, 0x101C), F(sameCamera, 0x1E0)));
    }

    private static float SnowGate(float temperature, float threshold, float end, float basis)
    {
        float denominator = S(threshold, end);
        if (denominator == 0) return temperature > threshold ? -1 : basis;
        float weight = Clamp(D(S(threshold, temperature), denominator));
        float squared = M(weight, weight);
        float fourth = M(squared, squared);
        return S(M(fourth, basis), S(1, fourth));
    }
    private static float Range(float value, float low, float high) => high <= low
        ? value < low ? 0 : 1 : Clamp(D(S(value, low), S(high, low)));
    private static float Blend(float low, float high, float weight) => A(M(S(1, weight), low), M(weight, high));
    private static float Clamp(float value) => value >= 1 ? 1 : value <= 0 ? 0 : value;
    private static float Max(float a, float b) => a > b ? a : b;
    private static float F(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadSingleLittleEndian(bytes[offset..]);
    private static void W(Span<byte> bytes, int offset, float value) => BinaryPrimitives.WriteSingleLittleEndian(bytes[offset..], value);
    private static float A(float a, float b) => NativeFloat.Add(a, b);
    private static float S(float a, float b) => NativeFloat.Subtract(a, b);
    private static float M(float a, float b) => NativeFloat.Multiply(a, b);
    [MethodImpl(MethodImplOptions.NoInlining)] private static float D(float a, float b) => a / b;
}
