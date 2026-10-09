using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

public static partial class DirectionalLightConstruction
{
    // Project-owned schema for the resource declaration and packed light state.
    // These are property widths, initial values and field destinations, never native instructions.
    private sealed record FieldShape(uint NameHash, uint Type, int Count, byte[] Initial);
    private readonly record struct FieldCopy(int Ordinal, int Destination, int Length, int SourceOffset = 0);
    private static readonly FieldShape[] SourceShapes = [
        Shape(0x27D889E5u, 8, 3), // 0
        Shape(0xF52EA910u, 8, 4, 0x0u, 0x0u, 0x0u, 0x3F800000u), // 1
        Shape(0x754E8B8Du, 8, 3, 0x3F800000u, 0x3F800000u, 0x3F800000u), // 2
        Shape(0x05D92ACCu, 1, 1), // 3
        Shape(0xD7E3FF50u, 1, 1), // 4
        Shape(0x2DAB8C43u, 1, 1), // 5
        Shape(0xE6126451u, 1, 1), // 6
        Shape(0xDB26951Du, 5, 1, 0x2000u), // 7
        Shape(0xC0C5A5B5u, 5, 1), // 8
        Shape(0xB65C0542u, 8, 4, 0x0u, 0x0u, 0x0u, 0x3F800000u), // 9
        Shape(0xF600D81Bu, 8, 3), // 10
        Shape(0x0705E343u, 8, 4, 0x0u, 0x0u, 0x0u, 0x3F800000u), // 11
        Shape(0xFDB27D7Au, 8, 3), // 12
        Shape(0x95E12FCBu, 8, 3), // 13
        Shape(0xB1205167u, 5, 2), // 14
        Shape(0x028E5549u, 8, 4, 0x0u, 0x0u, 0x0u, 0x3F800000u), // 15
        Shape(0xDA9AB52Bu, 8, 3), // 16
        Shape(0xD74A096Du, 8, 3), // 17
        Shape(0x5E8410AAu, 5, 1), // 18
        Shape(0xB01A6CDFu, 8, 3, 0x3F800000u, 0x3F800000u, 0x3F800000u), // 19
        Shape(0xBCC18265u, 8, 1, 0x3F800000u), // 20
        Shape(0x9004EA95u, 8, 3, 0x3F800000u, 0x3F800000u, 0x3F800000u), // 21
        Shape(0xE04F9B9Au, 8, 3, 0x3F800000u, 0x3F800000u, 0x3F800000u), // 22
        Shape(0xEB70E034u, 8, 1, 0x3F800000u), // 23
        Shape(0x216C0856u, 8, 3, 0x3F800000u, 0x3F800000u, 0x3F800000u), // 24
        Shape(0x216C84F0u, 8, 1, 0x3F800000u), // 25
        Shape(0x58264100u, 5, 2, 0xFFFFFFFFu), // 26
        Shape(0x2EAA596Cu, 8, 3), // 27
        Shape(0x78DBF2ADu, 8, 3, 0x3F800000u, 0x3F800000u, 0x3F800000u), // 28
        Shape(0xC30D8BEEu, 8, 3), // 29
        Shape(0x0D3F252Fu, 8, 3, 0x3F800000u, 0x3F800000u, 0x3F800000u), // 30
        Shape(0xCA039C01u, 8, 3), // 31
        Shape(0xC6052AE0u, 8, 3, 0x3F800000u, 0x3F800000u, 0x3F800000u), // 32
        Shape(0xC206B9BFu, 8, 3), // 33
        Shape(0xFBD8684Eu, 8, 3), // 34
        Shape(0xBBDB893Au, 8, 1, 0x3C23D70Au), // 35
        Shape(0x996E1E0Bu, 8, 1), // 36
        Shape(0x9B47712Au, 8, 1), // 37
        Shape(0x9D20C449u, 8, 1), // 38
        Shape(0xA2644CADu, 8, 1, 0x46FA0000u), // 39
        Shape(0xA616F2EBu, 8, 1, 0x46FA0000u), // 40
        Shape(0x4439EF34u, 1, 8), // 41
        Shape(0x72774CA0u, 5, 3), // 42
        Shape(0xD107D830u, 8, 1), // 43
        Shape(0xFDB556A9u, 8, 1, 0x459C4000u), // 44
        Shape(0x737EADA4u, 8, 1, 0x3F000000u), // 45
        Shape(0x5314E64Au, 8, 1, 0x3C23D70Au), // 46
        Shape(0x5DFA8CD3u, 8, 1, 0x3D23D70Au), // 47
        Shape(0x11B16155u, 8, 1, 0x3D23D70Au), // 48
        Shape(0xD9B4A58Au, 8, 1, 0x3F800000u), // 49
        Shape(0xA9DD24ADu, 8, 3, 0x40A00000u, 0x40A00000u, 0x40A00000u), // 50
        Shape(0xC230D096u, 8, 1, 0x41200000u), // 51
        Shape(0x4B5494D4u, 8, 1, 0x41200000u), // 52
        Shape(0xCC8A8D89u, 8, 1, 0x46FA0000u), // 53
        Shape(0x67DF510Bu, 8, 1, 0x46FA0000u), // 54
        Shape(0x80953024u, 8, 1, 0x46FA0000u), // 55
        Shape(0x09B8F462u, 8, 1, 0x46FA0000u), // 56
        Shape(0xA7DFA945u, 5, 1), // 57
        Shape(0x57AFC144u, 8, 4, 0x0u, 0x0u, 0x0u, 0x3F800000u), // 58
        Shape(0x056422D9u, 8, 3), // 59
        Shape(0x9EFA1768u, 8, 1), // 60
        Shape(0x37B9F6AAu, 8, 1), // 61
        Shape(0x3B6C9CE8u, 8, 1), // 62
        Shape(0xD6D7F8D2u, 5, 1), // 63
        Shape(0xB2F674C5u, 8, 1, 0x3F800000u), // 64
        Shape(0xAFE76568u, 8, 1, 0x3C449BA6u), // 65
        Shape(0xAA0E4A51u, 8, 1), // 66
        Shape(0x183E0816u, 5, 1), // 67
        Shape(0xE6136468u, 8, 1, 0x3F800000u), // 68
        Shape(0x1D204E21u, 8, 1, 0x42200000u), // 69
        Shape(0xA8889604u, 1, 1), // 70
        Shape(0x8A44928Du, 1, 1), // 71
        Shape(0x05BE2F9Cu, 8, 1, 0x3F800000u), // 72
        Shape(0x5B107EAAu, 8, 1, 0x3F800000u), // 73
        Shape(0x5CC55749u, 8, 1, 0x3F800000u), // 74
        Shape(0x5E7A2FE8u, 8, 1, 0x3F800000u), // 75
        Shape(0x6ADA34C2u, 8, 1, 0x3F800000u), // 76
        Shape(0x4F49EF33u, 8, 1, 0x3F800000u), // 77
        Shape(0x4F4A2782u, 8, 1, 0x3F800000u), // 78
        Shape(0xE3EB705Eu, 8, 1, 0x3F800000u), // 79
        Shape(0xAAD35562u, 8, 1, 0x3F800000u), // 80
        Shape(0x35D24BBBu, 8, 1), // 81
        Shape(0x243A17A2u, 8, 1), // 82
        Shape(0x125DBB57u, 8, 1, 0x3F800000u), // 83
        Shape(0xD288A15Fu, 8, 1, 0x38D1B717u), // 84
        Shape(0x8157802Fu, 8, 1, 0x3F800000u), // 85
        Shape(0x75A0F14Eu, 8, 1, 0x3F800000u), // 86
        Shape(0x69EA626Du, 8, 1, 0x3F800000u), // 87
        Shape(0xA5AAC51Cu, 8, 1, 0x3F800000u), // 88
        Shape(0x8AEC76D7u, 8, 1, 0x3F800000u), // 89
        Shape(0xDCCC00F7u, 8, 1, 0x3F800000u), // 90
        Shape(0x2AC69744u, 8, 1, 0x3F800000u), // 91
        Shape(0xFBC9D815u, 8, 1, 0x3F800000u), // 92
        Shape(0xE110502Du, 8, 1, 0x3F800000u), // 93
        Shape(0x6F505F83u, 8, 4, 0x3F800000u, 0x3F800000u, 0x3F800000u, 0x40000000u), // 94
        Shape(0x3CFAC144u, 8, 4, 0x3F800000u, 0x3F800000u, 0x3F800000u, 0x40000000u), // 95
        Shape(0x0AA52305u, 8, 4, 0x3F800000u, 0x3F800000u, 0x3F800000u, 0x40000000u), // 96
        Shape(0xD84F84C6u, 8, 4, 0x3F800000u, 0x3F800000u, 0x3F800000u, 0x40000000u), // 97
        Shape(0xA5F9E687u, 8, 4, 0x3F800000u, 0x3F800000u, 0x3F800000u, 0x40000000u), // 98
        Shape(0x73A44848u, 8, 4, 0x3F800000u, 0x3F800000u, 0x3F800000u, 0x40000000u), // 99
        Shape(0x414EAA09u, 8, 4, 0x3F800000u, 0x3F800000u, 0x3F800000u, 0x40000000u), // 100
        Shape(0x0EF90BCAu, 8, 4, 0x3F800000u, 0x3F800000u, 0x3F800000u, 0x40000000u), // 101
        Shape(0xDCA36D8Bu, 8, 4, 0x3F800000u, 0x3F800000u, 0x3F800000u, 0x40000000u), // 102
        Shape(0xAA4DCF4Cu, 8, 4, 0x3F800000u, 0x3F800000u, 0x3F800000u, 0x40000000u), // 103
        Shape(0xAB52F314u, 8, 4, 0x3F800000u, 0x3F800000u, 0x3F800000u, 0x40000000u), // 104
        Shape(0x92F4C973u, 8, 4, 0x3F800000u, 0x3F800000u, 0x3F800000u, 0x40000000u), // 105
        Shape(0x7A969FD2u, 8, 4, 0x3F800000u, 0x3F800000u, 0x3F800000u, 0x40000000u), // 106
        Shape(0x62387631u, 8, 4, 0x3F800000u, 0x3F800000u, 0x3F800000u, 0x40000000u), // 107
        Shape(0x49DA4C90u, 8, 4, 0x3F800000u, 0x3F800000u, 0x3F800000u, 0x40000000u), // 108
        Shape(0x317C22EFu, 8, 4, 0x3F800000u, 0x3F800000u, 0x3F800000u, 0x40000000u), // 109
        Shape(0x556972D8u, 5, 1), // 110
        Shape(0xFCE22644u, 8, 1), // 111
        Shape(0x47AB397Eu, 8, 1, 0x41A00000u), // 112
        Shape(0xD982DF94u, 8, 1, 0x3F800000u), // 113
        Shape(0xE70BE82Eu, 8, 1), // 114
        Shape(0x8D184691u, 8, 1, 0x3F800000u), // 115
        Shape(0xC136E4BCu, 8, 1, 0x3F800000u), // 116
        Shape(0x7EE891A3u, 8, 1), // 117
        Shape(0x9A64BAC5u, 8, 1), // 118
        Shape(0x567F8C3Fu, 8, 1), // 119
        Shape(0xF57EDDEDu, 8, 1), // 120
        Shape(0xE74D8D09u, 8, 1), // 121
        Shape(0xDC4A0E2Bu, 8, 1), // 122
        Shape(0xC5A1EA83u, 8, 1, 0x3F800000u), // 123
        Shape(0x4F49EE98u, 8, 1, 0x3F800000u), // 124
        Shape(0x4F4A26E7u, 8, 1, 0x3F800000u), // 125
        Shape(0xF72CB905u, 8, 1, 0x3F800000u), // 126
        Shape(0x56018586u, 8, 1, 0x3F800000u), // 127
        Shape(0xA212C787u, 8, 1, 0x3F800000u), // 128
        Shape(0x49BAFD81u, 8, 1, 0x3F800000u), // 129
        Shape(0x068DD30Cu, 8, 1), // 130
        Shape(0x24150C46u, 8, 1, 0x3F800000u), // 131
        Shape(0x7F66FA31u, 8, 1), // 132
        Shape(0x32B9C54Eu, 8, 1), // 133
        Shape(0x52771F68u, 8, 1, 0x3F800000u), // 134
        Shape(0xD92B8B44u, 8, 1), // 135
        Shape(0x956B1B72u, 8, 1, 0x3F800000u), // 136
        Shape(0xE21D0A23u, 8, 1), // 137
        Shape(0x11BC7932u, 8, 1), // 138
        Shape(0xC4DCD05Eu, 8, 1, 0x41700000u), // 139
        Shape(0x55F41F87u, 8, 1, 0x41200000u), // 140
        Shape(0x0ADD42B1u, 8, 3, 0x3F800000u, 0x3F800000u, 0x3F800000u), // 141
        Shape(0x81BD1707u, 8, 1), // 142
        Shape(0xD9843F56u, 8, 1), // 143
        Shape(0xD2D2B3ACu, 8, 1), // 144
        Shape(0x88E6D68Au, 8, 1), // 145
        Shape(0x74B28F82u, 8, 3), // 146
        Shape(0x28C86C17u, 5, 1), // 147
        Shape(0xF6EC42E5u, 8, 1), // 148
        Shape(0x5C0F9274u, 8, 1), // 149
        Shape(0x4B9873A0u, 8, 1, 0x41700000u), // 150
        Shape(0xD4B20649u, 8, 1, 0x41200000u), // 151
        Shape(0xDCA18473u, 8, 3, 0x3F800000u, 0x3F800000u, 0x3F800000u), // 152
        Shape(0xEB985294u, 8, 1, 0x3F800000u), // 153
        Shape(0x077838B4u, 8, 1), // 154
        Shape(0x7522DE15u, 8, 1), // 155
        Shape(0x2F221D06u, 8, 1, 0x42340000u), // 156
        Shape(0x914E514Eu, 8, 1, 0x3F800000u), // 157
        Shape(0x31FA3265u, 5, 1), // 158
        Shape(0x9AB3211Fu, 8, 1, 0x3F800000u), // 159
        Shape(0x6F486116u, 5, 1, 0x3E8u), // 160
        Shape(0xAEBE6302u, 5, 1, 0x64u), // 161
        Shape(0x1439399Bu, 5, 1, 0x1388u), // 162
        Shape(0x2E7D08DAu, 8, 1), // 163
        Shape(0x84156E77u, 8, 1), // 164
        Shape(0xC7668D7Du, 8, 1), // 165
        Shape(0x4520C762u, 8, 1), // 166
        Shape(0xE04B53F1u, 8, 3), // 167
        Shape(0xC2291D90u, 8, 3), // 168
        Shape(0xA406E72Fu, 8, 3), // 169
        Shape(0x85E4B0CEu, 8, 3), // 170
        Shape(0x10641492u, 8, 1, 0x3F800000u), // 171
        Shape(0x0442EC6Au, 8, 1, 0x3F800000u), // 172
        Shape(0x390CC1C2u, 8, 1, 0x3F800000u), // 173
        Shape(0x915AD809u, 8, 1), // 174
        Shape(0x7FFCD1D4u, 8, 1), // 175
        Shape(0x6946B326u, 8, 1), // 176
        Shape(0x2FCAA686u, 5, 1), // 177
        Shape(0x3A2A830Eu, 1, 1), // 178
        Shape(0xDED6F548u, 1, 1), // 179
        Shape(0xA8A3603Du, 8, 1), // 180
        Shape(0xE0481EE1u, 8, 1), // 181
        Shape(0xD8DCBF0Cu, 8, 1), // 182
        Shape(0xD57D64F1u, 8, 1), // 183
        Shape(0xD4D0B952u, 8, 1), // 184
        Shape(0x38815A4Cu, 8, 1, 0x3F800000u), // 185
        Shape(0x3115FA77u, 8, 1, 0x3F800000u), // 186
        Shape(0x2DB6A05Cu, 8, 1, 0x3F800000u), // 187
        Shape(0x2D09F4BDu, 8, 1, 0x3F800000u), // 188
        Shape(0x92EB8FE4u, 8, 4), // 189
        Shape(0x59AD05F3u, 8, 1), // 190
        Shape(0x8F785F5Au, 8, 1), // 191
        Shape(0xE160D88Au, 8, 1), // 192
        Shape(0x3A3E80E4u, 8, 1), // 193
        Shape(0x20097BC2u, 8, 1), // 194
        Shape(0x21E2CEE1u, 8, 1), // 195
        Shape(0x23BC2200u, 8, 1), // 196
        Shape(0x1E3028A3u, 8, 1), // 197
        Shape(0x1B9F05D9u, 8, 4), // 198
        Shape(0xE2607BE8u, 8, 1), // 199
        Shape(0x182BD54Fu, 8, 1), // 200
        Shape(0x6A144E7Fu, 8, 1), // 201
        Shape(0xC2F1F6D9u, 8, 1), // 202
        Shape(0xA8BCF1B7u, 8, 1), // 203
        Shape(0xAA9644D6u, 8, 1), // 204
        Shape(0xAC6F97F5u, 8, 1), // 205
        Shape(0xA6E39E98u, 8, 1), // 206
        Shape(0x884DAFD4u, 8, 4), // 207
        Shape(0x4F0F25E3u, 8, 1), // 208
        Shape(0x84DA7F4Au, 8, 1), // 209
        Shape(0xD6C2F87Au, 8, 1), // 210
        Shape(0x2FA0A0D4u, 8, 1), // 211
        Shape(0x156B9BB2u, 8, 1), // 212
        Shape(0x1744EED1u, 8, 1), // 213
        Shape(0x191E41F0u, 8, 1), // 214
        Shape(0x13924893u, 8, 1), // 215
        Shape(0x37A3D1D3u, 8, 4), // 216
        Shape(0xFE6547E2u, 8, 1), // 217
        Shape(0x3430A149u, 8, 1), // 218
        Shape(0x86191A79u, 8, 1), // 219
        Shape(0xDEF6C2D3u, 8, 1), // 220
        Shape(0xC4C1BDB1u, 8, 1), // 221
        Shape(0xC69B10D0u, 8, 1), // 222
        Shape(0xC87463EFu, 8, 1), // 223
        Shape(0xC2E86A92u, 8, 1), // 224
        Shape(0x101A0171u, 4, 1), // 225
        Shape(0x3D45A430u, 8, 1), // 226
        Shape(0x4C403289u, 5, 1), // 227
        Shape(0xF73E3F9Bu, 1, 1), // 228
        Shape(0xE11E9440u, 1, 1), // 229
        Shape(0xCFA19AC1u, 1, 1), // 230
        Shape(0xBE24A142u, 1, 1), // 231
        Shape(0xACA7A7C3u, 1, 1), // 232
        Shape(0x9B2AAE44u, 1, 1), // 233
        Shape(0x89ADB4C5u, 1, 1), // 234
        Shape(0x7830BB46u, 1, 1), // 235
        Shape(0x66B3C1C7u, 1, 1), // 236
        Shape(0x5536C848u, 1, 1), // 237
        Shape(0x43B9CEC9u, 1, 1), // 238
        Shape(0x29376891u, 1, 1), // 239
        Shape(0x0B153230u, 1, 1), // 240
        Shape(0xECF2FBCFu, 1, 1), // 241
        Shape(0xCED0C56Eu, 1, 1), // 242
        Shape(0xB0AE8F0Du, 1, 1), // 243
        Shape(0x928C58ACu, 1, 1), // 244
        Shape(0x59A21FD2u, 1, 1), // 245
        Shape(0xB37D8A13u, 1, 1), // 246
        Shape(0x0D58F454u, 1, 1), // 247
        Shape(0x67345E95u, 1, 1), // 248
        Shape(0xC10FC8D6u, 1, 1), // 249
        Shape(0x1AEB3317u, 1, 1), // 250
        Shape(0x74C69D58u, 1, 1), // 251
        Shape(0xCEA20799u, 1, 1), // 252
        Shape(0x287D71DAu, 1, 1), // 253
        Shape(0x8258DC1Bu, 1, 1), // 254
        Shape(0xFED723E3u, 1, 1), // 255
        Shape(0xE06901C2u, 1, 1), // 256
        Shape(0xC1FADFA1u, 1, 1), // 257
        Shape(0xA38CBD80u, 1, 1), // 258
        Shape(0x851E9B5Fu, 1, 1), // 259
        Shape(0x66B0793Eu, 1, 1), // 260
        Shape(0x9A960DB2u, 0, 1), // 261
        Shape(0xEB3FEBB3u, 0, 1), // 262
        Shape(0x3BE9C9B4u, 0, 1), // 263
        Shape(0x8C93A7B5u, 0, 1), // 264
        Shape(0xDD3D85B6u, 0, 1), // 265
        Shape(0x2DE763B7u, 0, 1), // 266
        Shape(0x7E9141B8u, 0, 1), // 267
        Shape(0xCF3B1FB9u, 0, 1), // 268
        Shape(0x1FE4FDBAu, 0, 1), // 269
        Shape(0x708EDBBBu, 0, 1), // 270
        Shape(0xC69A5183u, 0, 1), // 271
        Shape(0x8B2C33A2u, 0, 1), // 272
        Shape(0x4FBE15C1u, 0, 1), // 273
        Shape(0x144FF7E0u, 0, 1), // 274
        Shape(0xD8E1D9FFu, 0, 1), // 275
        Shape(0x9D73BC1Eu, 0, 1), // 276
        Shape(0x55059ECEu, 8, 3), // 277
        Shape(0xDDB914C3u, 8, 3), // 278
        Shape(0x4A67BEBEu, 8, 3), // 279
        Shape(0xF9BDE0BDu, 8, 3), // 280
        Shape(0xCEABBBBCu, 1, 1), // 281
        Shape(0x47113921u, 8, 1), // 282
        Shape(0x40653B87u, 8, 3), // 283
        Shape(0x9B7B5EA9u, 8, 3, 0x3F800000u, 0x3F800000u, 0x3F800000u), // 284
        Shape(0xAA47A5FBu, 8, 4), // 285
        Shape(0xFAF183FCu, 8, 4), // 286
        Shape(0x4B9B61FDu, 8, 4), // 287
        Shape(0x9C453FFEu, 8, 4), // 288
        Shape(0xECEF1DFFu, 8, 4), // 289
        Shape(0x3D98FC00u, 8, 4), // 290
        Shape(0x8E42DA01u, 8, 4), // 291
        Shape(0xDEECB802u, 8, 4), // 292
        Shape(0x2F969603u, 8, 4), // 293
        Shape(0x80407404u, 8, 4), // 294
        Shape(0xD64BE9CCu, 8, 4), // 295
        Shape(0x9ADDCBEBu, 8, 4), // 296
        Shape(0x5F6FAE0Au, 8, 4), // 297
        Shape(0x24019029u, 8, 4), // 298
        Shape(0xE8937248u, 8, 4), // 299
        Shape(0xAD255467u, 8, 4), // 300
        Shape(0xEDE00FEDu, 8, 1, 0x3F800000u), // 301
        Shape(0x952A6114u, 8, 3, 0x3F800000u, 0x3F800000u, 0x3F800000u), // 302
        Shape(0x663875ECu, 8, 3, 0x3F800000u, 0x3F800000u, 0x3F800000u), // 303
        Shape(0xBBD74026u, 8, 4), // 304
        Shape(0x425CFAC5u, 8, 4), // 305
        Shape(0xC8E2B564u, 8, 4), // 306
        Shape(0x4F687003u, 8, 4), // 307
        Shape(0xD5EE2AA2u, 8, 4), // 308
        Shape(0x5C73E541u, 8, 4), // 309
        Shape(0xE2F99FE0u, 8, 4), // 310
        Shape(0x697F5A7Fu, 8, 4), // 311
        Shape(0xF005151Eu, 8, 4), // 312
        Shape(0x768ACFBDu, 8, 4), // 313
        Shape(0x2BA9B6F5u, 8, 4), // 314
        Shape(0x75DB5036u, 8, 4), // 315
        Shape(0xC00CE977u, 8, 4), // 316
        Shape(0x0A3E82B8u, 8, 4), // 317
        Shape(0x54701BF9u, 8, 4), // 318
        Shape(0x9EA1B53Au, 8, 4), // 319
        Shape(0x1255B656u, 8, 1), // 320
        Shape(0xD6E79875u, 8, 1), // 321
        Shape(0xF5D5FE80u, 8, 4), // 322
        Shape(0xA9702012u, 8, 4), // 323
        Shape(0xF7AF519Fu, 8, 4), // 324
        Shape(0xBE58E853u, 8, 4), // 325
        Shape(0xF988A4BEu, 8, 4), // 326
        Shape(0xD341B094u, 8, 4), // 327
        Shape(0xFB61F7DDu, 8, 4), // 328
        Shape(0xE82A78D5u, 8, 4), // 329
        Shape(0xFD3B4AFCu, 8, 4), // 330
        Shape(0xFD134116u, 8, 4), // 331
        Shape(0xFF149E1Bu, 8, 4), // 332
        Shape(0x11FC0957u, 8, 4), // 333
        Shape(0x00EDF13Au, 8, 4), // 334
        Shape(0x26E4D198u, 8, 4), // 335
        Shape(0x02C74459u, 8, 4), // 336
        Shape(0x3BCD99D9u, 8, 4), // 337
        Shape(0x04A09778u, 8, 4), // 338
        Shape(0x50B6621Au, 8, 4), // 339
        Shape(0x0679EA97u, 8, 4), // 340
        Shape(0x659F2A5Bu, 8, 4), // 341
        Shape(0xB6E275CFu, 8, 4), // 342
        Shape(0x4764E223u, 8, 4), // 343
        Shape(0xF0338690u, 8, 4), // 344
        Shape(0xCF952202u, 8, 4), // 345
        Shape(0x29849751u, 8, 4), // 346
        Shape(0x57C561E1u, 8, 4), // 347
        Shape(0x62D5A812u, 8, 4), // 348
        Shape(0xDFF5A1C0u, 8, 4), // 349
        Shape(0x9C26B8D3u, 8, 4), // 350
        Shape(0x6825E19Fu, 8, 4), // 351
        Shape(0xD577C994u, 8, 4), // 352
        Shape(0xF056217Eu, 8, 4), // 353
        Shape(0x6427BC47u, 8, 1), // 354
        Shape(0xA5F933FAu, 8, 1), // 355
        Shape(0x3B0ABA5Bu, 8, 4), // 356
        Shape(0x1C9C983Au, 8, 4), // 357
        Shape(0xFE2E7619u, 8, 4), // 358
        Shape(0xDFC053F8u, 8, 4), // 359
        Shape(0xC15231D7u, 8, 4), // 360
        Shape(0xA2E40FB6u, 8, 4), // 361
        Shape(0x8475ED95u, 8, 4), // 362
        Shape(0x6607CB74u, 8, 4), // 363
        Shape(0x4799A953u, 8, 4), // 364
        Shape(0x292B8732u, 8, 4), // 365
        Shape(0x3C76386Au, 8, 4), // 366
        Shape(0x8D20166Bu, 8, 4), // 367
        Shape(0xDDC9F46Cu, 8, 4), // 368
        Shape(0x2E73D26Du, 8, 4), // 369
        Shape(0x7F1DB06Eu, 8, 4), // 370
        Shape(0xCFC78E6Fu, 8, 4), // 371
        Shape(0xDEFA3BA6u, 8, 1, 0x3F800000u), // 372
        Shape(0x35E0871Cu, 8, 1, 0x3F800000u), // 373
        Shape(0x016ABD7Bu, 8, 1), // 374
        Shape(0x76FD51D1u, 8, 1), // 375
        Shape(0x0C1E638Cu, 8, 1), // 376
        Shape(0x0C1E8B82u, 8, 1, 0x41000000u), // 377
        Shape(0x670CB6A8u, 8, 1), // 378
        Shape(0xB9A5C422u, 8, 1, 0x3F000000u), // 379
        Shape(0x8509C9D3u, 5, 1), // 380
        Shape(0xAEE9F013u, 8, 1, 0x3F800000u), // 381
        Shape(0xE5D49275u, 8, 1, 0x3F800000u), // 382
        Shape(0x482B4C1Cu, 8, 1, 0x3F800000u), // 383
        Shape(0xD7251CCBu, 8, 1, 0x3F800000u), // 384
        Shape(0x5B5569CCu, 8, 1, 0x3F800000u), // 385
        Shape(0xDF85B6CDu, 8, 1, 0x3F800000u), // 386
        Shape(0x63B603CEu, 8, 1, 0x3F800000u), // 387
        Shape(0xE1DB7E7Du, 8, 1, 0x3F800000u), // 388
        Shape(0x6A0BBE5Cu, 8, 1, 0x3F800000u), // 389
        Shape(0xF23BFE3Bu, 8, 1, 0x3F800000u), // 390
        Shape(0x7A6C3E1Au, 8, 1, 0x3F800000u), // 391
        Shape(0xE8B074B7u, 8, 3, 0x3F800000u, 0x3F800000u, 0x3F800000u), // 392
        Shape(0x70E0B496u, 8, 3, 0x3F800000u, 0x3F800000u, 0x3F800000u), // 393
        Shape(0xF910F475u, 8, 3, 0x3F800000u, 0x3F800000u, 0x3F800000u), // 394
        Shape(0x81413454u, 8, 3, 0x3F800000u, 0x3F800000u, 0x3F800000u), // 395
        Shape(0x7F3A1ACCu, 8, 3, 0x3F800000u, 0x3F800000u, 0x3F800000u), // 396
        Shape(0x81136DEBu, 8, 3, 0x3F800000u, 0x3F800000u, 0x3F800000u), // 397
        Shape(0x82ECC10Au, 8, 3, 0x3F800000u, 0x3F800000u, 0x3F800000u), // 398
        Shape(0x84C61429u, 8, 3, 0x3F800000u, 0x3F800000u, 0x3F800000u), // 399
        Shape(0x5A899175u, 8, 3, 0x3F800000u, 0x3F800000u, 0x3F800000u), // 400
        Shape(0x5C62E494u, 8, 3, 0x3F800000u), // 401
        Shape(0x5E3C37B3u, 8, 3), // 402
        Shape(0x60158AD2u, 8, 3), // 403
        Shape(0xAE1E2F02u, 8, 4, 0x40AAAAA4u, 0x40AAAAA4u, 0x41CCCCCDu, 0x41CCCCCDu), // 404
        Shape(0x9F277ACBu, 8, 4, 0x3F555550u, 0xBF000000u, 0xC0E66666u, 0xC1400000u), // 405
        Shape(0xACCD4B44u, 8, 3, 0x3F000000u, 0x3F000000u, 0x3F000000u), // 406
        Shape(0x46799F69u, 8, 3, 0x3F800000u, 0x3F800000u, 0x3F800000u), // 407
        Shape(0x990920BCu, 8, 3, 0x3F800000u, 0x3F800000u, 0x3F800000u), // 408
        Shape(0x2E37FDEAu, 8, 1, 0x41200000u), // 409
        Shape(0x2AD2EAB3u, 8, 1, 0x3F800000u), // 410
        Shape(0xC8BA2029u, 8, 1, 0x41C0CCCDu), // 411
        Shape(0xB33722BAu, 8, 1, 0xC27D999Au), // 412
        Shape(0x9A58ABECu, 1, 1), // 413
        Shape(0xD0768B13u, 8, 1, 0x4230CCCDu), // 414
        Shape(0x95F67262u, 8, 1, 0xC2DE999Au), // 415
        Shape(0x08C5FAF0u, 1, 1), // 416
        Shape(0xB53291BBu, 5, 1), // 417
        Shape(0x117EEF52u, 8, 1), // 418
        Shape(0x99A64163u, 8, 1), // 419
        Shape(0x959BE195u, 1, 1), // 420
        Shape(0x5CDA1BA6u, 1, 1), // 421
        Shape(0x5C8D7EA4u, 1, 1, 0x1u), // 422
        Shape(0xFAB30F55u, 8, 1, 0x3F800000u), // 423
        Shape(0x24D473E9u, 8, 3, 0x3F800000u, 0x3F800000u, 0x3F800000u), // 424
        Shape(0x01F9CF2Fu, 8, 1), // 425
        Shape(0x983D847Au, 8, 1), // 426
        Shape(0xF09AA849u, 8, 1, 0x43AF0000u), // 427
        Shape(0x9790B39Du, 8, 1), // 428
        Shape(0x3E554A05u, 8, 3, 0x3F800000u, 0x3F800000u, 0x3F800000u), // 429
        Shape(0xCB250F37u, 8, 1), // 430
        Shape(0xAC608EF2u, 8, 1), // 431
        Shape(0x246B9279u, 8, 1), // 432
        Shape(0x7A1C38FEu, 1, 1), // 433
        Shape(0x423766A0u, 8, 1), // 434
        Shape(0xB4D98822u, 8, 1), // 435
        Shape(0xEC179996u, 8, 1), // 436
        Shape(0x80597C7Bu, 8, 1), // 437
        Shape(0x8E299EA4u, 5, 1), // 438
        Shape(0xFCA82A76u, 8, 1), // 439
        Shape(0x89351110u, 5, 1), // 440
        Shape(0x61FC02F4u, 5, 1), // 441
        Shape(0xA37748A9u, 8, 1), // 442
        Shape(0x9F78D788u, 8, 1), // 443
        Shape(0x9B7A6667u, 8, 1), // 444
        Shape(0x977BF546u, 8, 1), // 445
        Shape(0x937D8425u, 8, 1), // 446
        Shape(0x8F7F1304u, 8, 1), // 447
        Shape(0x8B80A1E3u, 8, 1), // 448
        Shape(0x878230C2u, 8, 1), // 449
        Shape(0x09F6B621u, 8, 1), // 450
        Shape(0x2591C7F2u, 8, 1), // 451
        Shape(0xE882C246u, 8, 1, 0x41700000u), // 452
        Shape(0x6AF7713Du, 8, 1, 0x41200000u), // 453
        Shape(0x4AB23D53u, 8, 3, 0x3F800000u, 0x3F800000u, 0x3F800000u), // 454
        Shape(0x01F3BFE3u, 8, 1), // 455
        Shape(0xD31F8134u, 8, 1), // 456
        Shape(0x65A33188u, 8, 1, 0x41700000u), // 457
        Shape(0x2FA98CFFu, 8, 1, 0x41200000u), // 458
        Shape(0xDE545E15u, 8, 3, 0x3F800000u, 0x3F800000u, 0x3F800000u), // 459
        Shape(0x05288AA2u, 5, 1), // 460
        Shape(0x8380F5A2u, 8, 1), // 461
        Shape(0xE1BCBA93u, 8, 1, 0x3F800000u), // 462
        Shape(0x84E351E4u, 8, 1, 0x3F800000u), // 463
        Shape(0x7CA93A36u, 8, 1), // 464
        Shape(0xFE6E4545u, 8, 1), // 465
        Shape(0xC76FBD1Fu, 1, 1), // 466
        Shape(0xFE80308Eu, 8, 1), // 467
        Shape(0x4B9B4B94u, 1, 1), // 468
        Shape(0x1C1B8839u, 8, 3), // 469
        Shape(0x93F16EC0u, 8, 1), // 470
        Shape(0x716D5320u, 8, 3), // 471
        Shape(0xF59DA021u, 8, 3), // 472
        Shape(0x01719D65u, 8, 1, 0x3F800000u), // 473
        Shape(0x414D50C3u, 8, 1, 0x3F800000u), // 474
        Shape(0xF208E8D7u, 8, 1), // 475
        Shape(0x2B59F998u, 8, 1), // 476
        Shape(0x64AB0A59u, 8, 1), // 477
        Shape(0x9DFC1B1Au, 8, 1), // 478
        Shape(0xD74D2BDBu, 8, 1), // 479
        Shape(0x109E3C9Cu, 8, 1), // 480
        Shape(0x49EF4D5Du, 8, 1), // 481
        Shape(0x83405E1Eu, 8, 1), // 482
        Shape(0x6A81F3D4u, 8, 3, 0x3F800000u, 0x3F800000u, 0x3F800000u), // 483
        Shape(0x0B97F17Eu, 5, 1), // 484
        Shape(0x8B75BEBAu, 8, 1), // 485
        Shape(0xF5DF0F9Bu, 8, 1), // 486
        Shape(0x826C4960u, 8, 1), // 487
        Shape(0x210824EEu, 8, 1, 0x3F800000u), // 488
        Shape(0x0E264D18u, 8, 1, 0x3F800000u), // 489
        Shape(0xCD33DB3Bu, 8, 1), // 490
        Shape(0xEFDF70B5u, 8, 2, 0xBF800000u), // 491
        Shape(0xFFE91A48u, 8, 1), // 492
        Shape(0x4317ED8Eu, 8, 2), // 493
        Shape(0xDB5D92E7u, 8, 2, 0x3F800000u, 0x3F800000u), // 494
        Shape(0xB9D15939u, 8, 1), // 495
        Shape(0xA6C9CB25u, 8, 2), // 496
        Shape(0x2649C5BBu, 1, 1), // 497
        Shape(0xD2BA5E74u, 8, 1), // 498
        Shape(0x7D117C8Du, 8, 1, 0x3F800000u), // 499
        Shape(0x8383BFA1u, 8, 1), // 500
        Shape(0x7F854E80u, 8, 1), // 501
        Shape(0x688747B8u, 8, 1), // 502
        Shape(0xECB794B9u, 8, 1), // 503
        Shape(0x70E7E1BAu, 8, 1), // 504
        Shape(0xF5182EBBu, 8, 1), // 505
        Shape(0x79487BBCu, 8, 1), // 506
        Shape(0xFD78C8BDu, 8, 1), // 507
        Shape(0xBC916EDFu, 8, 1), // 508
        Shape(0xF5E27FA0u, 8, 1), // 509
        Shape(0x528B5B68u, 8, 1), // 510
        Shape(0x435C62C7u, 8, 1), // 511
        Shape(0x342D6A26u, 8, 1), // 512
        Shape(0x24FE7185u, 8, 1), // 513
        Shape(0x15CF78E4u, 8, 1), // 514
        Shape(0x06A08043u, 8, 1), // 515
        Shape(0x560887C1u, 8, 1, 0x3F800000u), // 516
        Shape(0x91D918DCu, 8, 1), // 517
        Shape(0x81A915BEu, 8, 1), // 518
        Shape(0x05D962BFu, 8, 1), // 519
        Shape(0x8A09AFC0u, 8, 1), // 520
        Shape(0x0E39FCC1u, 8, 1), // 521
        Shape(0x6488D697u, 8, 1), // 522
        Shape(0xE8B92398u, 8, 1), // 523
        Shape(0x6CE97099u, 8, 1), // 524
        Shape(0xF119BD9Au, 8, 1), // 525
        Shape(0xF77187A2u, 8, 1), // 526
        Shape(0xE8428F01u, 8, 1), // 527
        Shape(0xD9139660u, 8, 1), // 528
        Shape(0xC9E49DBFu, 8, 1), // 529
        Shape(0x8BDC6C29u, 8, 1), // 530
        Shape(0x7CAD7388u, 8, 1), // 531
        Shape(0x6D7E7AE7u, 8, 1), // 532
        Shape(0x5E4F8246u, 8, 1), // 533
        Shape(0x2EB832A9u, 8, 1), // 534
        Shape(0xA46F421Au, 8, 1), // 535
        Shape(0xBB0A56C2u, 5, 1), // 536
        Shape(0x6ACF7382u, 5, 1), // 537
        Shape(0xF45B6783u, 5, 1), // 538
        Shape(0x5BA07AE1u, 5, 1), // 539
        Shape(0x2DAC7844u, 5, 1), // 540
        Shape(0x4C718240u, 5, 1), // 541
        Shape(0x528A5FBEu, 8, 1, 0x3F800000u), // 542
        Shape(0xB0CC8850u, 8, 3), // 543
        Shape(0xFA95C232u, 8, 3), // 544
        Shape(0xEBA9F018u, 8, 1), // 545
        Shape(0x3B1F64B1u, 8, 1), // 546
        Shape(0x00C1E78Eu, 8, 1), // 547
        Shape(0x8C0825F3u, 5, 1), // 548
        Shape(0xD9D15973u, 8, 1), // 549
        Shape(0x0F73F325u, 8, 1), // 550
        Shape(0xB4032341u, 8, 1, 0x3F800000u), // 551
        Shape(0x71903129u, 8, 1, 0x3F800000u), // 552
        Shape(0xE86D4A68u, 8, 4, 0x3F800000u, 0x3F800000u, 0x3F800000u), // 553
        Shape(0xF31241B3u, 8, 1), // 554
        Shape(0x3D6C5BA6u, 8, 1), // 555
        Shape(0xDF5156ECu, 8, 1), // 556
        Shape(0x4A531908u, 8, 1, 0x3F800000u), // 557
        Shape(0xD7580FB4u, 8, 1), // 558
        Shape(0x8B8A92ABu, 8, 1, 0x3F800000u), // 559
        Shape(0xFC010718u, 8, 1), // 560
    ];
    private static FieldShape Shape(uint nameHash, uint type, int count, params uint[] initial)
    {
        int width = type is 0 or 1 ? 1 : 4;
        byte[] value = new byte[count * width];
        for (int i = 0; i < initial.Length; i++)
            if (width == 1) value[i] = (byte)initial[i];
            else BinaryPrimitives.WriteUInt32LittleEndian(value.AsSpan(i * 4, 4), initial[i]);
        return new(nameHash, type, count, value);
    }
    private static readonly FieldCopy[] PackedFields = [
        new(2, 0x10, 12),
        new(10, 0x30, 12),
        new(12, 0x50, 12),
        new(59, 0x70, 12),
        new(72, 0x80, 4),
        new(73, 0x84, 4),
        new(74, 0x88, 4),
        new(75, 0x8C, 4),
        new(0, 0x90, 12),
        new(13, 0xE0, 12),
        new(516, 0xEC, 4),
        new(16, 0x100, 12),
        new(17, 0x110, 12),
        new(19, 0x1B0, 12),
        new(20, 0x1BC, 4),
        new(21, 0x1C0, 12),
        new(22, 0x1CC, 12),
        new(23, 0x1D8, 4),
        new(24, 0x1DC, 12),
        new(25, 0x1E8, 4),
        new(26, 0x1EC, 8),
        new(27, 0x1FC, 12),
        new(28, 0x208, 12),
        new(29, 0x214, 12),
        new(30, 0x220, 12),
        new(31, 0x22C, 12),
        new(32, 0x238, 12),
        new(33, 0x244, 12),
        new(34, 0x250, 12),
        new(55, 0x25C, 4),
        new(56, 0x260, 4),
        new(93, 0x264, 4),
        new(301, 0x268, 4),
        new(545, 0x26C, 4),
        new(35, 0x270, 4),
        new(483, 0x274, 12),
        new(91, 0x280, 4),
        new(92, 0x284, 4),
        new(464, 0x288, 4),
        new(465, 0x28C, 4),
        new(36, 0x290, 4),
        new(37, 0x294, 4),
        new(38, 0x298, 4),
        new(60, 0x29C, 4),
        new(39, 0x2A0, 4),
        new(40, 0x2A4, 4),
        new(61, 0x2A8, 4),
        new(62, 0x2AC, 4),
        new(434, 0x2B0, 4),
        new(435, 0x2B4, 4),
        new(64, 0x2B8, 4),
        new(69, 0x2BC, 4),
        new(65, 0x2C0, 4),
        new(66, 0x2C4, 4),
        new(84, 0x2C8, 4),
        new(68, 0x2CC, 4),
        new(116, 0x2D0, 4),
        new(171, 0x2D4, 4),
        new(381, 0x2D8, 4),
        new(382, 0x2DC, 4),
        new(383, 0x2E0, 4),
        new(172, 0x2E4, 4),
        new(173, 0x2E8, 4),
        new(174, 0x2EC, 4),
        new(175, 0x2F0, 4),
        new(176, 0x2F4, 4),
        new(354, 0x2F8, 4),
        new(76, 0x2FC, 4),
        new(77, 0x300, 4),
        new(78, 0x304, 4),
        new(79, 0x308, 4),
        new(302, 0x30C, 12),
        new(80, 0x318, 4),
        new(303, 0x31C, 12),
        new(123, 0x328, 4),
        new(124, 0x32C, 4),
        new(125, 0x330, 4),
        new(534, 0x33C, 4),
        new(535, 0x340, 4),
        new(81, 0x344, 4),
        new(82, 0x348, 4),
        new(83, 0x34C, 4),
        new(115, 0x350, 4),
        new(85, 0x354, 4),
        new(86, 0x358, 4),
        new(87, 0x35C, 4),
        new(113, 0x360, 4),
        new(355, 0x364, 4),
        new(88, 0x368, 4),
        new(114, 0x36C, 4),
        new(488, 0x370, 4),
        new(489, 0x374, 4),
        new(89, 0x378, 4),
        new(90, 0x37C, 4),
        new(372, 0x380, 4),
        new(542, 0x384, 4),
        new(94, 0x388, 16),
        new(95, 0x398, 16),
        new(96, 0x3A8, 16),
        new(97, 0x3B8, 16),
        new(98, 0x3C8, 16),
        new(99, 0x3D8, 16),
        new(100, 0x3E8, 16),
        new(101, 0x3F8, 16),
        new(102, 0x408, 16),
        new(103, 0x418, 16),
        new(104, 0x428, 16),
        new(105, 0x438, 16),
        new(106, 0x448, 16),
        new(107, 0x458, 16),
        new(108, 0x468, 16),
        new(109, 0x478, 16),
        new(229, 0x488, 1),
        new(230, 0x489, 1),
        new(231, 0x48A, 1),
        new(232, 0x48B, 1),
        new(233, 0x48C, 1),
        new(234, 0x48D, 1),
        new(235, 0x48E, 1),
        new(236, 0x48F, 1),
        new(237, 0x490, 1),
        new(238, 0x491, 1),
        new(239, 0x492, 1),
        new(240, 0x493, 1),
        new(241, 0x494, 1),
        new(242, 0x495, 1),
        new(243, 0x496, 1),
        new(244, 0x497, 1),
        new(245, 0x498, 1),
        new(246, 0x499, 1),
        new(247, 0x49A, 1),
        new(248, 0x49B, 1),
        new(249, 0x49C, 1),
        new(250, 0x49D, 1),
        new(251, 0x49E, 1),
        new(252, 0x49F, 1),
        new(253, 0x4A0, 1),
        new(254, 0x4A1, 1),
        new(255, 0x4A2, 1),
        new(256, 0x4A3, 1),
        new(257, 0x4A4, 1),
        new(258, 0x4A5, 1),
        new(259, 0x4A6, 1),
        new(260, 0x4A7, 1),
        new(261, 0x4A8, 1),
        new(262, 0x4A9, 1),
        new(263, 0x4AA, 1),
        new(264, 0x4AB, 1),
        new(265, 0x4AC, 1),
        new(266, 0x4AD, 1),
        new(267, 0x4AE, 1),
        new(268, 0x4AF, 1),
        new(269, 0x4B0, 1),
        new(270, 0x4B1, 1),
        new(271, 0x4B2, 1),
        new(272, 0x4B3, 1),
        new(273, 0x4B4, 1),
        new(274, 0x4B5, 1),
        new(275, 0x4B6, 1),
        new(276, 0x4B7, 1),
        new(285, 0x4B8, 16),
        new(286, 0x4C8, 16),
        new(287, 0x4D8, 16),
        new(288, 0x4E8, 16),
        new(289, 0x4F8, 16),
        new(290, 0x508, 16),
        new(291, 0x518, 16),
        new(292, 0x528, 16),
        new(293, 0x538, 16),
        new(294, 0x548, 16),
        new(295, 0x558, 16),
        new(296, 0x568, 16),
        new(297, 0x578, 16),
        new(298, 0x588, 16),
        new(299, 0x598, 16),
        new(300, 0x5A8, 16),
        new(304, 0x5B8, 16),
        new(305, 0x5C8, 16),
        new(306, 0x5D8, 16),
        new(307, 0x5E8, 16),
        new(308, 0x5F8, 16),
        new(309, 0x608, 16),
        new(310, 0x618, 16),
        new(311, 0x628, 16),
        new(312, 0x638, 16),
        new(313, 0x648, 16),
        new(314, 0x658, 16),
        new(315, 0x668, 16),
        new(316, 0x678, 16),
        new(317, 0x688, 16),
        new(318, 0x698, 16),
        new(319, 0x6A8, 16),
        new(322, 0x6B8, 16),
        new(323, 0x6C8, 16),
        new(324, 0x6D8, 16),
        new(325, 0x6E8, 16),
        new(326, 0x6F8, 16),
        new(327, 0x708, 16),
        new(328, 0x718, 16),
        new(329, 0x728, 16),
        new(330, 0x738, 16),
        new(331, 0x748, 16),
        new(332, 0x758, 16),
        new(333, 0x768, 16),
        new(334, 0x778, 16),
        new(335, 0x788, 16),
        new(336, 0x798, 16),
        new(337, 0x7A8, 16),
        new(338, 0x7B8, 16),
        new(339, 0x7C8, 16),
        new(340, 0x7D8, 16),
        new(341, 0x7E8, 16),
        new(342, 0x7F8, 16),
        new(343, 0x808, 16),
        new(344, 0x818, 16),
        new(345, 0x828, 16),
        new(346, 0x838, 16),
        new(347, 0x848, 16),
        new(348, 0x858, 16),
        new(349, 0x868, 16),
        new(350, 0x878, 16),
        new(351, 0x888, 16),
        new(352, 0x898, 16),
        new(353, 0x8A8, 16),
        new(356, 0x8B8, 16),
        new(357, 0x8C8, 16),
        new(358, 0x8D8, 16),
        new(359, 0x8E8, 16),
        new(360, 0x8F8, 16),
        new(361, 0x908, 16),
        new(362, 0x918, 16),
        new(363, 0x928, 16),
        new(364, 0x938, 16),
        new(365, 0x948, 16),
        new(366, 0x958, 16),
        new(367, 0x968, 16),
        new(368, 0x978, 16),
        new(369, 0x988, 16),
        new(370, 0x998, 16),
        new(371, 0x9A8, 16),
        new(111, 0x9B8, 4),
        new(112, 0x9BC, 4),
        new(156, 0x9C0, 4),
        new(320, 0x9C4, 4),
        new(321, 0x9C8, 4),
        new(117, 0x9CC, 4),
        new(118, 0x9D0, 4),
        new(153, 0x9D4, 4),
        new(154, 0x9D8, 4),
        new(473, 0x9DC, 4),
        new(474, 0x9E0, 4),
        new(126, 0x9E4, 4),
        new(128, 0x9E8, 4),
        new(129, 0x9EC, 4),
        new(130, 0x9F0, 4),
        new(131, 0x9F4, 4),
        new(134, 0x9F8, 4),
        new(135, 0x9FC, 4),
        new(136, 0xA00, 4),
        new(461, 0xA04, 4),
        new(137, 0xA08, 4),
        new(138, 0xA0C, 4),
        new(139, 0xA10, 4),
        new(140, 0xA14, 4),
        new(141, 0xA18, 12),
        new(450, 0xA24, 4),
        new(451, 0xA28, 4),
        new(452, 0xA2C, 4),
        new(453, 0xA30, 4),
        new(454, 0xA34, 12),
        new(148, 0xA40, 4),
        new(149, 0xA44, 4),
        new(150, 0xA48, 4),
        new(151, 0xA4C, 4),
        new(152, 0xA50, 12),
        new(455, 0xA5C, 4),
        new(456, 0xA60, 4),
        new(457, 0xA64, 4),
        new(458, 0xA68, 4),
        new(459, 0xA6C, 12),
        new(142, 0xA78, 4),
        new(143, 0xA7C, 4),
        new(144, 0xA80, 4),
        new(145, 0xA84, 4),
        new(146, 0xA88, 12),
        new(155, 0xA94, 4),
        new(157, 0xA98, 4),
        new(159, 0xA9C, 4),
        new(550, 0xAA0, 4),
        new(551, 0xAA4, 4),
        new(555, 0xAA8, 4),
        new(554, 0xAAC, 4),
        new(552, 0xAB0, 4),
        new(553, 0xAB4, 16),
        new(556, 0xAC4, 4),
        new(557, 0xAC8, 4),
        new(558, 0xACC, 4),
        new(559, 0xAD0, 4),
        new(466, 0xAD4, 1),
        new(490, 0xAD8, 4),
        new(491, 0xADC, 8),
        new(517, 0xAE4, 4),
        new(492, 0xAE8, 4),
        new(493, 0xAEC, 8),
        new(494, 0xAF4, 8),
        new(495, 0xAFC, 4),
        new(496, 0xB00, 8),
        new(498, 0xB08, 4),
        new(119, 0xB0C, 4),
        new(120, 0xB10, 4),
        new(121, 0xB14, 4),
        new(122, 0xB18, 4),
        new(282, 0xB1C, 4),
        new(283, 0xB20, 12),
        new(284, 0xB2C, 12),
        new(423, 0xB38, 4),
        new(424, 0xB3C, 12),
        new(425, 0xB48, 4),
        new(426, 0xB4C, 4),
        new(427, 0xB50, 4),
        new(439, 0xB54, 4),
        new(467, 0xB58, 4),
        new(485, 0xB5C, 4),
        new(486, 0xB60, 4),
        new(487, 0xB64, 4),
        new(421, 0xB68, 1),
        new(422, 0xB69, 1),
        new(178, 0xB6A, 1),
        new(228, 0xB6B, 1),
        new(281, 0xB6C, 1),
        new(468, 0xB6D, 1),
        new(497, 0xB6E, 1),
        new(127, 0xB70, 4),
        new(549, 0xB74, 4),
        new(428, 0xB78, 4),
        new(132, 0xB7C, 4),
        new(133, 0xB80, 4),
        new(163, 0xB84, 4),
        new(164, 0xB88, 4),
        new(165, 0xB8C, 4),
        new(166, 0xB90, 4),
        new(179, 0xB94, 1),
        new(180, 0xB98, 4),
        new(181, 0xB9C, 4),
        new(182, 0xBA0, 4),
        new(183, 0xBA4, 4),
        new(184, 0xBA8, 4),
        new(185, 0xBAC, 4),
        new(186, 0xBB0, 4),
        new(187, 0xBB4, 4),
        new(188, 0xBB8, 4),
        new(189, 0xBBC, 16),
        new(190, 0xBCC, 4),
        new(191, 0xBD0, 4),
        new(192, 0xBD4, 4),
        new(193, 0xBD8, 4),
        new(194, 0xBDC, 4),
        new(195, 0xBE0, 4),
        new(196, 0xBE4, 4),
        new(197, 0xBE8, 4),
        new(277, 0xBEC, 12),
        new(198, 0xBFC, 16),
        new(199, 0xC0C, 4),
        new(200, 0xC10, 4),
        new(201, 0xC14, 4),
        new(202, 0xC18, 4),
        new(203, 0xC1C, 4),
        new(204, 0xC20, 4),
        new(205, 0xC24, 4),
        new(206, 0xC28, 4),
        new(278, 0xC2C, 12),
        new(207, 0xC3C, 16),
        new(208, 0xC4C, 4),
        new(209, 0xC50, 4),
        new(210, 0xC54, 4),
        new(211, 0xC58, 4),
        new(212, 0xC5C, 4),
        new(213, 0xC60, 4),
        new(214, 0xC64, 4),
        new(215, 0xC68, 4),
        new(279, 0xC6C, 12),
        new(216, 0xC7C, 16),
        new(217, 0xC8C, 4),
        new(218, 0xC90, 4),
        new(219, 0xC94, 4),
        new(220, 0xC98, 4),
        new(221, 0xC9C, 4),
        new(222, 0xCA0, 4),
        new(223, 0xCA4, 4),
        new(224, 0xCA8, 4),
        new(280, 0xCAC, 12),
        new(225, 0xCBC, 4),
        new(226, 0xCC0, 4),
        new(384, 0xCC4, 4),
        new(385, 0xCC8, 4),
        new(386, 0xCCC, 4),
        new(387, 0xCD0, 4),
        new(388, 0xCD4, 4),
        new(389, 0xCD8, 4),
        new(390, 0xCDC, 4),
        new(391, 0xCE0, 4),
        new(392, 0xCE4, 12),
        new(393, 0xCF0, 12),
        new(394, 0xCFC, 12),
        new(395, 0xD08, 12),
        new(396, 0xD14, 12),
        new(397, 0xD20, 12),
        new(398, 0xD2C, 12),
        new(399, 0xD38, 12),
        new(400, 0xD44, 12),
        new(401, 0xD50, 12),
        new(402, 0xD5C, 12),
        new(403, 0xD68, 12),
        new(404, 0xD74, 16),
        new(405, 0xD84, 16),
        new(406, 0xD94, 12),
        new(407, 0xDA0, 12),
        new(408, 0xDAC, 12),
        new(409, 0xDB8, 4),
        new(410, 0xDBC, 4),
        new(411, 0xDC0, 4),
        new(412, 0xDC4, 4),
        new(413, 0xDC8, 1),
        new(414, 0xDCC, 4),
        new(415, 0xDD0, 4),
        new(416, 0xDD4, 1),
        new(418, 0xDD8, 4),
        new(419, 0xDDC, 4),
        new(420, 0xDE0, 1),
        new(560, 0xDE4, 4),
        new(429, 0xDE8, 12),
        new(430, 0xDF4, 4),
        new(431, 0xDF8, 4),
        new(432, 0xDFC, 4),
        new(433, 0xE00, 1),
        new(436, 0xE04, 4),
        new(437, 0xE08, 4),
        new(469, 0xE0C, 12),
        new(470, 0xE18, 4),
        new(442, 0xE38, 4),
        new(443, 0xE3C, 4),
        new(444, 0xE40, 4),
        new(445, 0xE44, 4),
        new(446, 0xE48, 4),
        new(447, 0xE4C, 4),
        new(448, 0xE50, 4),
        new(449, 0xE54, 4),
        new(500, 0xE58, 4),
        new(501, 0xE5C, 4),
        new(502, 0xE60, 4),
        new(503, 0xE64, 4),
        new(504, 0xE68, 4),
        new(505, 0xE6C, 4),
        new(506, 0xE70, 4),
        new(507, 0xE74, 4),
        new(518, 0xE78, 4),
        new(519, 0xE7C, 4),
        new(520, 0xE80, 4),
        new(521, 0xE84, 4),
        new(522, 0xE88, 4),
        new(523, 0xE8C, 4),
        new(524, 0xE90, 4),
        new(525, 0xE94, 4),
        new(475, 0xE98, 4),
        new(476, 0xE9C, 4),
        new(477, 0xEA0, 4),
        new(478, 0xEA4, 4),
        new(479, 0xEA8, 4),
        new(480, 0xEAC, 4),
        new(481, 0xEB0, 4),
        new(482, 0xEB4, 4),
        new(508, 0xEB8, 4),
        new(509, 0xEBC, 4),
        new(510, 0xEC0, 4),
        new(511, 0xEC4, 4),
        new(512, 0xEC8, 4),
        new(513, 0xECC, 4),
        new(514, 0xED0, 4),
        new(515, 0xED4, 4),
        new(526, 0xED8, 4),
        new(527, 0xEDC, 4),
        new(528, 0xEE0, 4),
        new(529, 0xEE4, 4),
        new(530, 0xEE8, 4),
        new(531, 0xEEC, 4),
        new(532, 0xEF0, 4),
        new(533, 0xEF4, 4),
        new(462, 0xEF8, 4),
        new(463, 0xEFC, 4),
        new(499, 0xF00, 4),
        new(543, 0xF04, 12),
        new(544, 0xF10, 12),
        new(4, 0xF1C, 1),
        new(5, 0xF1D, 1),
        new(6, 0xF1E, 1),
        new(70, 0xF1F, 1),
        new(71, 0xF20, 1),
        new(546, 0xF24, 4),
        new(547, 0xF28, 4),
        new(43, 0xF30, 4),
        new(44, 0xF34, 4),
        new(45, 0xF38, 4),
        new(46, 0xF3C, 4),
        new(47, 0xF40, 4),
        new(48, 0xF44, 4),
        new(49, 0xF48, 4),
        new(50, 0xF4C, 12),
        new(51, 0xF58, 4),
        new(52, 0xF5C, 4),
        new(53, 0xF60, 4),
        new(54, 0xF64, 4),
        new(373, 0xFC0, 4),
        new(374, 0xFC4, 4),
        new(375, 0xFC8, 4),
        new(376, 0xFCC, 4),
        new(377, 0xFD0, 4),
        new(378, 0xFD4, 4),
        new(379, 0xFD8, 4),
        new(380, 0xFDC, 4),
        new(471, 0x1090, 12),
        new(472, 0x10A0, 12),
        new(41, 0x10B0, 8),
    ];

    private static byte[][] ReadFields(DokProperty[] properties)
    {
        if (properties.Length != SourceShapes.Length)
            throw new InvalidDataException("Directional declaration requires all 561 property fields.");
        var fields = new byte[properties.Length][];
        var byName = properties.ToDictionary(p => p.NameHash);
        for (int i = 0; i < fields.Length; i++)
        {
            var shape = SourceShapes[i];
            if (!byName.TryGetValue(shape.NameHash, out var p))
            { fields[i] = shape.Initial; continue; }
            if (p.ElementCount < 0 || p.RawValue.Length != (long)p.ElementSize * p.ElementCount)
                throw new InvalidDataException("Directional declaration has an invalid property extent.");
            // An optional array with a different count keeps its initialized value.
            fields[i] = p.TypeCode == shape.Type && p.ElementCount == shape.Count
                ? p.RawValue : shape.Initial;
        }
        if ((fields[3][0] & 2) != 0)
            throw new NotSupportedException("Directional optional current-field writer is not implemented.");
        return fields;
    }

    private static ConstructionResult ConstructOwned(DokProperty[] properties,
        DirectionalLightConstructionInputs input, ReadOnlySpan<byte> template)
    {
        byte[][] fields = ReadFields(properties);
        byte[] constructor = new byte[LightDataBytes];
        byte[] mask = Enumerable.Repeat((byte)1, LightDataBytes).ToArray();
        foreach (var (offset, length) in new (int,int)[] {
            (0x10C,4),(0x11C,4),(0x150,96),(0x1F4,8),(0xF21,3),(0xF2C,4),(0x10B8,8) })
        {
            template.Slice(offset,length).CopyTo(constructor.AsSpan(offset,length));
            Array.Clear(mask,offset,length);
        }
        foreach (var field in PackedFields)
            fields[field.Ordinal].AsSpan(field.SourceOffset,field.Length)
                .CopyTo(constructor.AsSpan(field.Destination,field.Length));
        foreach (int at in new[] {0x9C,0x334,0x338,0xE1C,0xE28,0x109C,0x10AC})
            PutFloat(constructor,at,1);
        PutFloat(constructor,0xE20,5000);PutFloat(constructor,0xE24,.5f);
        int[] quaternionFields = [1,9,11,58];
        for (int i=0;i<4;i++)
        {
            Vector4 q=NormalizeQuaternion(ReadVector(fields[quaternionFields[i]],0));
            PutVector(constructor,0xA0+i*16,q);
            PutVector(constructor,i*32,DirectionalLightSource.RotatePreparedQuaternion(q));
        }
        Vector4 hemi=NormalizeQuaternion(ReadVector(fields[15],0));
        PutVector(constructor,0xF0,hemi);
        PrepareHemisphere(constructor,hemi);
        bool swap = (ReadWord(fields[380],0)&2)!=0;
        int first=swap?32:0, second=swap?0:32;
        constructor.AsSpan(first,16).CopyTo(constructor.AsSpan(0xF80,16));
        constructor.AsSpan(second,16).CopyTo(constructor.AsSpan(0xF90,16));
        constructor.AsSpan(first+16,16).CopyTo(constructor.AsSpan(0xFA0,16));
        constructor.AsSpan(second+16,16).CopyTo(constructor.AsSpan(0xFB0,16));
        uint[] declared=DeclaredKeys(fields);
        ResolveResources(fields,declared,input.ResourceBindings,false,constructor);
        byte[] cpu=new byte[0x48];
        PutWord(cpu,0x30, (fields[3][0]&2)!=0 ? 0x70000000u : 0u);
        PutWord(cpu,0x34,ReadWord(fields[7],0));PutWord(cpu,0x38,ReadWord(fields[8],0));
        PutWord(cpu,0x3C,ReadWord(fields[160],0));PutWord(cpu,0x40,ReadWord(fields[161],0));
        PutWord(cpu,0x44,ReadWord(fields[162],0));
        byte[] prepared=constructor.ToArray();PrepareNullCamera(prepared,input.ContextFlags3E4);
        return new(constructor,prepared,cpu,mask,declared);
    }

    private static uint[] DeclaredKeys(byte[][] fields)
    {
        int[] textureFields=[67,110,147,158,177,227,417,438,440,536,538,540,441,537,539,541,460,484,548];
        return new[] {ReadWord(fields[14],0),ReadWord(fields[14],4)}
            .Concat(textureFields.Select(i=>ReadWord(fields[i],0))).Append(ReadWord(fields[63],0)).ToArray();
    }
    private static IReadOnlyList<uint> ResolveResources(byte[][] fields,uint[] declared,
        IReadOnlyList<DirectionalLightResourceBinding> bindings,bool enumerate,byte[]? light)
    {
        var requests=new HashSet<uint>();
        ulong Resolve(uint key,uint requiredClass)
        {
            if(key==0)return 0;
            requests.Add(key);
            var b=bindings.SingleOrDefault(b=>b.SourceObjectKey==key);
            if(b is null)
            {
                if(enumerate)return 0;
                throw new InvalidDataException($"Directional resource {key:X8} needs an explicit lookup result.");
            }
            return b.CpuDataReady&&(requiredClass==0||b.InterfaceClassId==requiredClass)?b.DataHandle:0;
        }
        ulong providerData=Resolve(declared[21],0x970DB365);
        for(int i=0;i<21;i++)
        {
            ulong handle=Resolve(declared[i],0xB672FAA2);
            if(i==1&&handle==0)handle=Resolve(0x80F9DB7B,0xB672FAA2);
            if(light is not null)PutHandle(light,0xFE0+i*8,handle);
        }
        if(light is not null)PutHandle(light,0x1088,providerData);
        for(int i=0;i<3;i++)
        {
            ulong handle=Resolve(ReadWord(fields[42],i*4),i==1?0x3059B9C3u:0xB672FAA2u);
            if(light is not null)PutHandle(light,0xF68+i*8,handle);
        }
        Resolve(ReadWord(fields[18],0),0);Resolve(ReadWord(fields[57],0),0);
        if(!enumerate&&!requests.SetEquals(bindings.Select(b=>b.SourceObjectKey)))
            throw new InvalidDataException("Directional bindings include an unused source key.");
        return Array.AsReadOnly(requests.Order().ToArray());
    }

    private static void PrepareNullCamera(byte[] light,uint contextFlags3E4)
    {
        for(int i=0;i<4;i++)
            PutVector(light,i*32,DirectionalLightSource.RotatePreparedQuaternion(ReadVector(light,0xA0+i*16)));
        PrepareHemisphere(light,ReadVector(light,0xF0));
        ClassifyModifiers(light,contextFlags3E4);
    }
    private static void PrepareHemisphere(byte[] light,Vector4 quaternion)
    {
        PutVector(light,0x120,DirectionalLightSource.RotatePreparedQuaternion(quaternion,true));
        for(int component=0;component<4;component++)
        {
            float upper=ReadFloat(light,0x100+component*4),lower=ReadFloat(light,0x110+component*4);
            PutFloat(light,0x130+component*4,CMul(CSub(upper,lower),.5f));
            PutFloat(light,0x140+component*4,CMul(CAdd(lower,upper),.5f));
        }
    }
    private static Vector4 NormalizeQuaternion(Vector4 q)
    {
        float length=CAdd(CAdd(CAdd(CMul(q.Y,q.Y),CMul(q.X,q.X)),CMul(q.Z,q.Z)),CMul(q.W,q.W));
        float inv=CDiv(1,MathF.Sqrt(length));
        return new(CMul(inv,q.X),CMul(inv,q.Y),CMul(inv,q.Z),CMul(inv,q.W));
    }
    private static Vector4 ReadVector(byte[] bytes,int offset)=>new(ReadFloat(bytes,offset),ReadFloat(bytes,offset+4),ReadFloat(bytes,offset+8),ReadFloat(bytes,offset+12));
    private static float ReadFloat(byte[] bytes,int offset)=>BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(offset,4));
    private static uint ReadWord(byte[] bytes,int offset)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset,4));
    private static void PutFloat(byte[] bytes,int offset,float value)=>BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(offset,4),value);
    private static void PutWord(byte[] bytes,int offset,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset,4),value);
    private static void PutHandle(byte[] bytes,int offset,ulong value)=>BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset,8),value);
    private static void PutVector(byte[] bytes,int offset,Vector4 value)
    {PutFloat(bytes,offset,value.X);PutFloat(bytes,offset+4,value.Y);PutFloat(bytes,offset+8,value.Z);PutFloat(bytes,offset+12,value.W);}
    [MethodImpl(MethodImplOptions.NoInlining)]private static float CMul(float a,float b)=>a*b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float CAdd(float a,float b)=>a+b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float CSub(float a,float b)=>a-b;
    [MethodImpl(MethodImplOptions.NoInlining)]private static float CDiv(float a,float b)=>a/b;
}
