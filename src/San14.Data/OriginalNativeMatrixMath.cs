using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace San14.Data;

/// <summary>Original 3F6EB0 full cofactor/determinant inverse, with the exact SSE operation sequence.</summary>
public static class OriginalNativeMatrixMath
{
    public static Matrix4x4 Inverse3F6EB0(Matrix4x4 input, out float determinant)
    {
        if (!Sse.IsSupported) throw new PlatformNotSupportedException("Original matrix inverse requires x64 SSE.");
        if (!Finite(input)) throw new InvalidDataException("Original matrix inverse input is not finite.");
        var x0_0 = Vector128.Create(input.M11, input.M12, input.M13, input.M14);
        var x1_0 = Vector128.Create(input.M21, input.M22, input.M23, input.M24);
        var x2_0 = Vector128.Create(input.M31, input.M32, input.M33, input.M34);
        var x3_0 = Vector128.Create(input.M41, input.M42, input.M43, input.M44);
        // Ported from the locked SC/TC SSA in ce-skylight-input-consumer-20261001/native-math.json.
        var x13_1 = x0_0; // 003F6EF5
        var x12_1 = x0_0; // 003F6EF9
        var x12_2 = Sse.Shuffle(x12_1, x1_0, 0x44); // 003F6EFD
        var x13_2 = Sse.Shuffle(x13_1, x1_0, 0xEE); // 003F6F02
        var x14_1 = x12_2; // 003F6F07
        var x1_1 = x2_0; // 003F6F0B
        var x11_1 = x13_2; // 003F6F0E
        var x1_2 = Sse.Shuffle(x1_1, x3_0, 0x44); // 003F6F12
        var x12_3 = Sse.Shuffle(x12_2, x1_2, 0xDD); // 003F6F16
        var x14_2 = Sse.Shuffle(x14_1, x1_2, 0x88); // 003F6F1B
        var x10_1 = x12_3; // 003F6F20
        var x2_1 = Sse.Shuffle(x2_0, x3_0, 0xEE); // 003F6F24
        var x5_1 = x12_3; // 003F6F28
        var x11_2 = Sse.Shuffle(x11_1, x2_1, 0x88); // 003F6F2C
        var x13_3 = Sse.Shuffle(x13_2, x2_1, 0xDD); // 003F6F31
        var x1_3 = x11_2; // 003F6F36
        var x1_4 = Sse.Shuffle(x1_3, x11_2, 0xEE); // 003F6F3A
        var x0_1 = x13_3; // 003F6F3F
        var x0_2 = Sse.Shuffle(x0_1, x13_3, 0x50); // 003F6F43
        var x9_1 = x13_3; // 003F6F48
        var x1_5 = Sse.Multiply(x1_4, x0_2); // 003F6F4C
        var x4_1 = x13_3; // 003F6F4F
        var x9_2 = Sse.Shuffle(x9_1, x13_3, 0xEE); // 003F6F53
        var x0_3 = x11_2; // 003F6F58
        var x0_4 = Sse.Shuffle(x0_3, x11_2, 0x50); // 003F6F5C
        var x9_3 = Sse.Multiply(x9_2, x0_4); // 003F6F61
        var x0_5 = x12_3; // 003F6F65
        var x0_6 = Sse.Shuffle(x0_5, x12_3, 0x50); // 003F6F69
        var x10_2 = Sse.Shuffle(x10_1, x12_3, 0xEE); // 003F6F6E
        var x4_2 = Sse.Shuffle(x4_1, x12_3, 0xDD); // 003F6F73
        var x9_4 = Sse.Subtract(x9_3, x1_5); // 003F6F78
        var x5_2 = Sse.Shuffle(x5_1, x12_3, 0x49); // 003F6F7C
        var x1_6 = x14_2; // 003F6F81
        var x1_7 = Sse.Shuffle(x1_6, x14_2, 0xEE); // 003F6F85
        var x1_8 = Sse.Multiply(x1_7, x0_6); // 003F6F8A
        var x0_7 = x14_2; // 003F6F8D
        var x0_8 = Sse.Shuffle(x0_7, x14_2, 0x50); // 003F6F91
        var x7_1 = x9_4; // 003F6F96
        var x10_3 = Sse.Multiply(x10_2, x0_8); // 003F6F9A
        var x2_2 = x9_4; // 003F6F9E
        var x0_9 = x13_3; // 003F6FA2
        var x0_10 = Sse.Shuffle(x0_9, x12_3, 0x88); // 003F6FA6
        var x10_4 = Sse.Subtract(x10_3, x1_8); // 003F6FAB
        var x1_9 = x11_2; // 003F6FAF
        var x1_10 = Sse.Shuffle(x1_9, x14_2, 0xDD); // 003F6FB3
        var x1_11 = Sse.Multiply(x1_10, x0_10); // 003F6FB8
        var x0_11 = x11_2; // 003F6FBB
        var x0_12 = Sse.Shuffle(x0_11, x14_2, 0x88); // 003F6FBF
        var x4_3 = Sse.Multiply(x4_2, x0_12); // 003F6FC4
        var x8_1 = x10_4; // 003F6FC7
        var x3_1 = x10_4; // 003F6FCB
        var x4_4 = Sse.Subtract(x4_3, x1_11); // 003F6FCF
        var x1_12 = x12_3; // 003F6FD2
        var x1_13 = Sse.Shuffle(x1_12, x12_3, 0x9E); // 003F6FD6
        var x7_2 = Sse.Shuffle(x7_1, x4_4, 0x5D); // 003F6FDB
        var x0_13 = x7_2; // 003F6FDF
        var x8_2 = Sse.Shuffle(x8_1, x4_4, 0xFD); // 003F6FE2
        var x0_14 = Sse.Shuffle(x0_13, x9_4, 0x32); // 003F6FE7
        var x6_1 = x8_2; // 003F6FEC
        var x5_3 = Sse.Multiply(x5_2, x0_14); // 003F6FF0
        var x0_15 = x14_2; // 003F6FF3
        var x0_16 = Sse.Shuffle(x0_15, x14_2, 0x12); // 003F6FF7
        var x7_3 = Sse.Shuffle(x7_2, x9_4, 0x99); // 003F6FFC
        var x7_4 = Sse.Multiply(x7_3, x0_16); // 003F7001
        var x0_17 = x13_3; // 003F7004
        var x0_18 = Sse.Shuffle(x0_17, x13_3, 0x49); // 003F7008
        var x6_2 = Sse.Shuffle(x6_1, x10_4, 0x32); // 003F700D
        var x6_3 = Sse.Multiply(x6_2, x0_18); // 003F7012
        var x0_19 = x11_2; // 003F7015
        var x0_20 = Sse.Shuffle(x0_19, x11_2, 0x12); // 003F7019
        var x8_3 = Sse.Shuffle(x8_2, x10_4, 0x99); // 003F701E
        var x8_4 = Sse.Multiply(x8_3, x0_20); // 003F7023
        var x0_21 = x9_4; // 003F7027
        var x2_3 = Sse.Shuffle(x2_2, x4_4, 0x04); // 003F702B
        var x0_22 = Sse.Shuffle(x0_21, x2_3, 0x93); // 003F702F
        var x1_14 = Sse.Multiply(x1_13, x0_22); // 003F7033
        var x0_23 = x9_4; // 003F7036
        var x3_2 = Sse.Shuffle(x3_1, x4_4, 0xA4); // 003F703A
        var x5_4 = Sse.Subtract(x5_3, x1_14); // 003F703E
        var x1_15 = x14_2; // 003F7041
        var x1_16 = Sse.Shuffle(x1_15, x14_2, 0x7B); // 003F7045
        var x0_24 = Sse.Shuffle(x0_23, x2_3, 0x26); // 003F704A
        var x1_17 = Sse.Multiply(x1_16, x0_24); // 003F704E
        var x0_25 = x13_3; // 003F7051
        var x0_26 = Sse.Shuffle(x0_25, x13_3, 0x9E); // 003F7055
        var x12_4 = Sse.Shuffle(x12_3, x12_3, 0x33); // 003F705A
        var x13_4 = Sse.Shuffle(x13_3, x13_3, 0x33); // 003F705F
        var x7_5 = Sse.Subtract(x7_4, x1_17); // 003F7064
        var x1_18 = x10_4; // 003F7067
        var x1_19 = Sse.Shuffle(x1_18, x3_2, 0x93); // 003F706B
        var x1_20 = Sse.Multiply(x1_19, x0_26); // 003F706F
        var x0_27 = x11_2; // 003F7072
        var x0_28 = Sse.Shuffle(x0_27, x11_2, 0x7B); // 003F7076
        var x11_3 = Sse.Shuffle(x11_2, x11_2, 0x8D); // 003F707B
        var x2_4 = x7_5; // 003F7080
        var x6_4 = Sse.Subtract(x6_3, x1_20); // 003F7083
        var x1_21 = x10_4; // 003F7086
        var x1_22 = Sse.Shuffle(x1_21, x3_2, 0x26); // 003F708A
        var x1_23 = Sse.Multiply(x1_22, x0_28); // 003F708E
        var x0_29 = x9_4; // 003F7091
        var x0_30 = Sse.Shuffle(x0_29, x4_4, 0x4A); // 003F7095
        var x0_31 = Sse.Shuffle(x0_30, x0_30, 0x2C); // 003F7099
        var x12_5 = Sse.Multiply(x12_4, x0_31); // 003F709D
        var x0_32 = x14_2; // 003F70A1
        var x0_33 = Sse.Shuffle(x0_32, x14_2, 0x8D); // 003F70A5
        var x8_5 = Sse.Subtract(x8_4, x1_23); // 003F70AA
        var x9_5 = Sse.Shuffle(x9_4, x4_4, 0x4C); // 003F70AE
        var x1_24 = x10_4; // 003F70B3
        var x1_25 = Sse.Shuffle(x1_24, x4_4, 0xEA); // 003F70B7
        var x10_5 = Sse.Shuffle(x10_4, x4_4, 0xEC); // 003F70BB
        var x4_5 = x6_4; // 003F70C0
        var x1_26 = Sse.Shuffle(x1_25, x1_25, 0x2C); // 003F70C3
        var x13_5 = Sse.Multiply(x13_4, x1_26); // 003F70C7
        var x1_27 = x5_4; // 003F70CB
        var x9_6 = Sse.Shuffle(x9_5, x9_5, 0x93); // 003F70CE
        var x5_5 = Sse.Subtract(x5_4, x12_5); // 003F70D3
        var x0_34 = Sse.Multiply(x0_33, x9_6); // 003F70D7
        var x1_28 = Sse.Add(x1_27, x12_5); // 003F70DB
        var x10_6 = Sse.Shuffle(x10_5, x10_5, 0x93); // 003F70DF
        var x4_6 = Sse.Add(x4_5, x13_5); // 003F70E4
        var x10_7 = Sse.Multiply(x10_6, x11_3); // 003F70E8
        var x6_5 = Sse.Subtract(x6_4, x13_5); // 003F70EC
        var x1_29 = Sse.Shuffle(x1_28, x5_5, 0xD8); // 003F70F0
        var x2_5 = Sse.Subtract(x2_4, x0_34); // 003F70F4
        var x1_30 = Sse.Shuffle(x1_29, x1_29, 0xD8); // 003F70F7
        var x7_6 = Sse.Add(x7_5, x0_34); // 003F70FB
        var x0_35 = x14_2; // 003F70FE
        var x5_6 = x8_5; // 003F7102
        var x0_36 = Sse.Multiply(x0_35, x1_30); // 003F7106
        var x5_7 = Sse.Subtract(x5_6, x10_7); // 003F7109
        var x4_7 = Sse.Shuffle(x4_6, x6_5, 0xD8); // 003F710D
        var x8_6 = Sse.Add(x8_5, x10_7); // 003F7111
        var x2_6 = Sse.Shuffle(x2_5, x7_6, 0xD8); // 003F7115
        var x2_7 = Sse.Shuffle(x2_6, x2_6, 0xD8); // 003F7119
        var x14_3 = Sse.Shuffle(x14_2, x0_36, 0x40); // 003F711D
        var x4_8 = Sse.Shuffle(x4_7, x4_7, 0xD8); // 003F7122
        var x14_4 = Sse.Add(x14_3, x0_36); // 003F7126
        var x5_8 = Sse.Shuffle(x5_7, x8_6, 0xD8); // 003F712A
        var x5_9 = Sse.Shuffle(x5_8, x5_8, 0xD8); // 003F712F
        var x0_37 = Sse.Shuffle(x0_36, x14_4, 0x30); // 003F7133
        var x0_38 = Sse.Add(x0_37, x14_4); // 003F7138
        var x0_39 = Sse.Shuffle(x0_38, x0_38, 0xAA); // 003F713C
        determinant = x0_39.GetElement(0); // 003F7145
        var x3_3 = Vector128.Create(1f); // 003F7148
        var x3_4 = Sse.Divide(x3_3, x0_39); // 003F714F
        var x0_40 = x3_4; // 003F7152
        var x0_41 = Sse.Multiply(x0_40, x1_30); // 003F7155
        var x1_31 = x3_4; // 003F7158
        var x1_32 = Sse.Multiply(x1_31, x2_7); // 003F715B
        var x2_8 = x3_4; // 003F715E
        var x2_9 = Sse.Multiply(x2_8, x4_8); // 003F7161
        var x3_5 = Sse.Multiply(x3_4, x5_9); // 003F7164
        var result = new Matrix4x4(
            x0_41.GetElement(0), x0_41.GetElement(1), x0_41.GetElement(2), x0_41.GetElement(3),
            x1_32.GetElement(0), x1_32.GetElement(1), x1_32.GetElement(2), x1_32.GetElement(3),
            x2_9.GetElement(0), x2_9.GetElement(1), x2_9.GetElement(2), x2_9.GetElement(3),
            x3_5.GetElement(0), x3_5.GetElement(1), x3_5.GetElement(2), x3_5.GetElement(3));
        if (!float.IsFinite(determinant) || determinant == 0 || !Finite(result))
            throw new InvalidDataException("Original matrix inverse has a singular or nonfinite result.");
        return result;
    }

    private static bool Finite(Matrix4x4 m) =>
        float.IsFinite(m.M11) && float.IsFinite(m.M12) && float.IsFinite(m.M13) && float.IsFinite(m.M14) &&
        float.IsFinite(m.M21) && float.IsFinite(m.M22) && float.IsFinite(m.M23) && float.IsFinite(m.M24) &&
        float.IsFinite(m.M31) && float.IsFinite(m.M32) && float.IsFinite(m.M33) && float.IsFinite(m.M34) &&
        float.IsFinite(m.M41) && float.IsFinite(m.M42) && float.IsFinite(m.M43) && float.IsFinite(m.M44);
}
