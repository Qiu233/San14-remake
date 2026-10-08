// Original 3F6EB0 cofactor inverse: exact SSE operation sequence.
// Generated from src/San14.Data/OriginalNativeMatrixMath.cs's locked SSA.
// Shared only by native camera consumers; no generic graphics framework.
#pragma once
#include <array>
#include <xmmintrin.h>

namespace source_matrix {
using Matrix = std::array<float, 16>;
inline Matrix inverse(const Matrix& input, float* determinant = nullptr) {
    const auto x0_0 = _mm_loadu_ps(input.data());
    const auto x1_0 = _mm_loadu_ps(input.data() + 4);
    const auto x2_0 = _mm_loadu_ps(input.data() + 8);
    const auto x3_0 = _mm_loadu_ps(input.data() + 12);
    // Ported from the locked SC/TC SSA in ce-skylight-input-consumer-20261001/native-math.json.
    auto x13_1 = x0_0; // 003F6EF5
    auto x12_1 = x0_0; // 003F6EF9
    auto x12_2 = _mm_shuffle_ps(x12_1, x1_0, 0x44); // 003F6EFD
    auto x13_2 = _mm_shuffle_ps(x13_1, x1_0, 0xEE); // 003F6F02
    auto x14_1 = x12_2; // 003F6F07
    auto x1_1 = x2_0; // 003F6F0B
    auto x11_1 = x13_2; // 003F6F0E
    auto x1_2 = _mm_shuffle_ps(x1_1, x3_0, 0x44); // 003F6F12
    auto x12_3 = _mm_shuffle_ps(x12_2, x1_2, 0xDD); // 003F6F16
    auto x14_2 = _mm_shuffle_ps(x14_1, x1_2, 0x88); // 003F6F1B
    auto x10_1 = x12_3; // 003F6F20
    auto x2_1 = _mm_shuffle_ps(x2_0, x3_0, 0xEE); // 003F6F24
    auto x5_1 = x12_3; // 003F6F28
    auto x11_2 = _mm_shuffle_ps(x11_1, x2_1, 0x88); // 003F6F2C
    auto x13_3 = _mm_shuffle_ps(x13_2, x2_1, 0xDD); // 003F6F31
    auto x1_3 = x11_2; // 003F6F36
    auto x1_4 = _mm_shuffle_ps(x1_3, x11_2, 0xEE); // 003F6F3A
    auto x0_1 = x13_3; // 003F6F3F
    auto x0_2 = _mm_shuffle_ps(x0_1, x13_3, 0x50); // 003F6F43
    auto x9_1 = x13_3; // 003F6F48
    auto x1_5 = _mm_mul_ps(x1_4, x0_2); // 003F6F4C
    auto x4_1 = x13_3; // 003F6F4F
    auto x9_2 = _mm_shuffle_ps(x9_1, x13_3, 0xEE); // 003F6F53
    auto x0_3 = x11_2; // 003F6F58
    auto x0_4 = _mm_shuffle_ps(x0_3, x11_2, 0x50); // 003F6F5C
    auto x9_3 = _mm_mul_ps(x9_2, x0_4); // 003F6F61
    auto x0_5 = x12_3; // 003F6F65
    auto x0_6 = _mm_shuffle_ps(x0_5, x12_3, 0x50); // 003F6F69
    auto x10_2 = _mm_shuffle_ps(x10_1, x12_3, 0xEE); // 003F6F6E
    auto x4_2 = _mm_shuffle_ps(x4_1, x12_3, 0xDD); // 003F6F73
    auto x9_4 = _mm_sub_ps(x9_3, x1_5); // 003F6F78
    auto x5_2 = _mm_shuffle_ps(x5_1, x12_3, 0x49); // 003F6F7C
    auto x1_6 = x14_2; // 003F6F81
    auto x1_7 = _mm_shuffle_ps(x1_6, x14_2, 0xEE); // 003F6F85
    auto x1_8 = _mm_mul_ps(x1_7, x0_6); // 003F6F8A
    auto x0_7 = x14_2; // 003F6F8D
    auto x0_8 = _mm_shuffle_ps(x0_7, x14_2, 0x50); // 003F6F91
    auto x7_1 = x9_4; // 003F6F96
    auto x10_3 = _mm_mul_ps(x10_2, x0_8); // 003F6F9A
    auto x2_2 = x9_4; // 003F6F9E
    auto x0_9 = x13_3; // 003F6FA2
    auto x0_10 = _mm_shuffle_ps(x0_9, x12_3, 0x88); // 003F6FA6
    auto x10_4 = _mm_sub_ps(x10_3, x1_8); // 003F6FAB
    auto x1_9 = x11_2; // 003F6FAF
    auto x1_10 = _mm_shuffle_ps(x1_9, x14_2, 0xDD); // 003F6FB3
    auto x1_11 = _mm_mul_ps(x1_10, x0_10); // 003F6FB8
    auto x0_11 = x11_2; // 003F6FBB
    auto x0_12 = _mm_shuffle_ps(x0_11, x14_2, 0x88); // 003F6FBF
    auto x4_3 = _mm_mul_ps(x4_2, x0_12); // 003F6FC4
    auto x8_1 = x10_4; // 003F6FC7
    auto x3_1 = x10_4; // 003F6FCB
    auto x4_4 = _mm_sub_ps(x4_3, x1_11); // 003F6FCF
    auto x1_12 = x12_3; // 003F6FD2
    auto x1_13 = _mm_shuffle_ps(x1_12, x12_3, 0x9E); // 003F6FD6
    auto x7_2 = _mm_shuffle_ps(x7_1, x4_4, 0x5D); // 003F6FDB
    auto x0_13 = x7_2; // 003F6FDF
    auto x8_2 = _mm_shuffle_ps(x8_1, x4_4, 0xFD); // 003F6FE2
    auto x0_14 = _mm_shuffle_ps(x0_13, x9_4, 0x32); // 003F6FE7
    auto x6_1 = x8_2; // 003F6FEC
    auto x5_3 = _mm_mul_ps(x5_2, x0_14); // 003F6FF0
    auto x0_15 = x14_2; // 003F6FF3
    auto x0_16 = _mm_shuffle_ps(x0_15, x14_2, 0x12); // 003F6FF7
    auto x7_3 = _mm_shuffle_ps(x7_2, x9_4, 0x99); // 003F6FFC
    auto x7_4 = _mm_mul_ps(x7_3, x0_16); // 003F7001
    auto x0_17 = x13_3; // 003F7004
    auto x0_18 = _mm_shuffle_ps(x0_17, x13_3, 0x49); // 003F7008
    auto x6_2 = _mm_shuffle_ps(x6_1, x10_4, 0x32); // 003F700D
    auto x6_3 = _mm_mul_ps(x6_2, x0_18); // 003F7012
    auto x0_19 = x11_2; // 003F7015
    auto x0_20 = _mm_shuffle_ps(x0_19, x11_2, 0x12); // 003F7019
    auto x8_3 = _mm_shuffle_ps(x8_2, x10_4, 0x99); // 003F701E
    auto x8_4 = _mm_mul_ps(x8_3, x0_20); // 003F7023
    auto x0_21 = x9_4; // 003F7027
    auto x2_3 = _mm_shuffle_ps(x2_2, x4_4, 0x04); // 003F702B
    auto x0_22 = _mm_shuffle_ps(x0_21, x2_3, 0x93); // 003F702F
    auto x1_14 = _mm_mul_ps(x1_13, x0_22); // 003F7033
    auto x0_23 = x9_4; // 003F7036
    auto x3_2 = _mm_shuffle_ps(x3_1, x4_4, 0xA4); // 003F703A
    auto x5_4 = _mm_sub_ps(x5_3, x1_14); // 003F703E
    auto x1_15 = x14_2; // 003F7041
    auto x1_16 = _mm_shuffle_ps(x1_15, x14_2, 0x7B); // 003F7045
    auto x0_24 = _mm_shuffle_ps(x0_23, x2_3, 0x26); // 003F704A
    auto x1_17 = _mm_mul_ps(x1_16, x0_24); // 003F704E
    auto x0_25 = x13_3; // 003F7051
    auto x0_26 = _mm_shuffle_ps(x0_25, x13_3, 0x9E); // 003F7055
    auto x12_4 = _mm_shuffle_ps(x12_3, x12_3, 0x33); // 003F705A
    auto x13_4 = _mm_shuffle_ps(x13_3, x13_3, 0x33); // 003F705F
    auto x7_5 = _mm_sub_ps(x7_4, x1_17); // 003F7064
    auto x1_18 = x10_4; // 003F7067
    auto x1_19 = _mm_shuffle_ps(x1_18, x3_2, 0x93); // 003F706B
    auto x1_20 = _mm_mul_ps(x1_19, x0_26); // 003F706F
    auto x0_27 = x11_2; // 003F7072
    auto x0_28 = _mm_shuffle_ps(x0_27, x11_2, 0x7B); // 003F7076
    auto x11_3 = _mm_shuffle_ps(x11_2, x11_2, 0x8D); // 003F707B
    auto x2_4 = x7_5; // 003F7080
    auto x6_4 = _mm_sub_ps(x6_3, x1_20); // 003F7083
    auto x1_21 = x10_4; // 003F7086
    auto x1_22 = _mm_shuffle_ps(x1_21, x3_2, 0x26); // 003F708A
    auto x1_23 = _mm_mul_ps(x1_22, x0_28); // 003F708E
    auto x0_29 = x9_4; // 003F7091
    auto x0_30 = _mm_shuffle_ps(x0_29, x4_4, 0x4A); // 003F7095
    auto x0_31 = _mm_shuffle_ps(x0_30, x0_30, 0x2C); // 003F7099
    auto x12_5 = _mm_mul_ps(x12_4, x0_31); // 003F709D
    auto x0_32 = x14_2; // 003F70A1
    auto x0_33 = _mm_shuffle_ps(x0_32, x14_2, 0x8D); // 003F70A5
    auto x8_5 = _mm_sub_ps(x8_4, x1_23); // 003F70AA
    auto x9_5 = _mm_shuffle_ps(x9_4, x4_4, 0x4C); // 003F70AE
    auto x1_24 = x10_4; // 003F70B3
    auto x1_25 = _mm_shuffle_ps(x1_24, x4_4, 0xEA); // 003F70B7
    auto x10_5 = _mm_shuffle_ps(x10_4, x4_4, 0xEC); // 003F70BB
    auto x4_5 = x6_4; // 003F70C0
    auto x1_26 = _mm_shuffle_ps(x1_25, x1_25, 0x2C); // 003F70C3
    auto x13_5 = _mm_mul_ps(x13_4, x1_26); // 003F70C7
    auto x1_27 = x5_4; // 003F70CB
    auto x9_6 = _mm_shuffle_ps(x9_5, x9_5, 0x93); // 003F70CE
    auto x5_5 = _mm_sub_ps(x5_4, x12_5); // 003F70D3
    auto x0_34 = _mm_mul_ps(x0_33, x9_6); // 003F70D7
    auto x1_28 = _mm_add_ps(x1_27, x12_5); // 003F70DB
    auto x10_6 = _mm_shuffle_ps(x10_5, x10_5, 0x93); // 003F70DF
    auto x4_6 = _mm_add_ps(x4_5, x13_5); // 003F70E4
    auto x10_7 = _mm_mul_ps(x10_6, x11_3); // 003F70E8
    auto x6_5 = _mm_sub_ps(x6_4, x13_5); // 003F70EC
    auto x1_29 = _mm_shuffle_ps(x1_28, x5_5, 0xD8); // 003F70F0
    auto x2_5 = _mm_sub_ps(x2_4, x0_34); // 003F70F4
    auto x1_30 = _mm_shuffle_ps(x1_29, x1_29, 0xD8); // 003F70F7
    auto x7_6 = _mm_add_ps(x7_5, x0_34); // 003F70FB
    auto x0_35 = x14_2; // 003F70FE
    auto x5_6 = x8_5; // 003F7102
    auto x0_36 = _mm_mul_ps(x0_35, x1_30); // 003F7106
    auto x5_7 = _mm_sub_ps(x5_6, x10_7); // 003F7109
    auto x4_7 = _mm_shuffle_ps(x4_6, x6_5, 0xD8); // 003F710D
    auto x8_6 = _mm_add_ps(x8_5, x10_7); // 003F7111
    auto x2_6 = _mm_shuffle_ps(x2_5, x7_6, 0xD8); // 003F7115
    auto x2_7 = _mm_shuffle_ps(x2_6, x2_6, 0xD8); // 003F7119
    auto x14_3 = _mm_shuffle_ps(x14_2, x0_36, 0x40); // 003F711D
    auto x4_8 = _mm_shuffle_ps(x4_7, x4_7, 0xD8); // 003F7122
    auto x14_4 = _mm_add_ps(x14_3, x0_36); // 003F7126
    auto x5_8 = _mm_shuffle_ps(x5_7, x8_6, 0xD8); // 003F712A
    auto x5_9 = _mm_shuffle_ps(x5_8, x5_8, 0xD8); // 003F712F
    auto x0_37 = _mm_shuffle_ps(x0_36, x14_4, 0x30); // 003F7133
    auto x0_38 = _mm_add_ps(x0_37, x14_4); // 003F7138
    auto x0_39 = _mm_shuffle_ps(x0_38, x0_38, 0xAA); // 003F713C
    if (determinant) *determinant = _mm_cvtss_f32(x0_39); // 003F7145
    auto x3_3 = _mm_set1_ps(1.f); // 003F7148
    auto x3_4 = _mm_div_ps(x3_3, x0_39); // 003F714F
    auto x0_40 = x3_4; // 003F7152
    auto x0_41 = _mm_mul_ps(x0_40, x1_30); // 003F7155
    auto x1_31 = x3_4; // 003F7158
    auto x1_32 = _mm_mul_ps(x1_31, x2_7); // 003F715B
    auto x2_8 = x3_4; // 003F715E
    auto x2_9 = _mm_mul_ps(x2_8, x4_8); // 003F7161
    auto x3_5 = _mm_mul_ps(x3_4, x5_9); // 003F7164
    Matrix result;
    _mm_storeu_ps(result.data(), x0_41);
    _mm_storeu_ps(result.data() + 4, x1_32);
    _mm_storeu_ps(result.data() + 8, x2_9);
    _mm_storeu_ps(result.data() + 12, x3_5);
    return result;
}
} // namespace source_matrix
