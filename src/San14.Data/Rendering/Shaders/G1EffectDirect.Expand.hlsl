RWByteAddressBuffer Vertices : register(u0);
RWByteAddressBuffer Particles : register(u1);
cbuffer ExpansionConstants : register(b0)
{
    row_major float4x4 worldToClip;
    float4 billboardRight;
    float4 billboardUp;
    float2 cameraClipScale; uint capacity; uint indirectOffset;
    uint genericType; uint3 expansionPadding;
};
uint ReadWord(uint slot, uint field) { return Particles.Load(capacity * field + slot * 4); }
float ReadNumber(uint slot, uint field) { return asfloat(ReadWord(slot, field)); }
void WriteVertex(uint index, float4 position, uint color, uint parameter, float2 uv)
{
    uint offset = index * 32;
    Vertices.Store4(offset, asuint(position));
    Vertices.Store4(offset + 16, uint4(color, parameter, asuint(uv.x), asuint(uv.y)));
}
[numthreads(32, 1, 1)]
void ExpandDraw(uint3 thread : SV_DispatchThreadID)
{
    uint slot = thread.x;
    if (slot >= capacity || (ReadWord(slot, 4) & 0x60000) != 0x60000) return;
    float2 size = float2(ReadNumber(slot, 0x34), ReadNumber(slot, 0x38));
    float4 position = float4(ReadNumber(slot, 0x50), ReadNumber(slot, 0x54), ReadNumber(slot, 0x58), ReadNumber(slot, 0x5C));
    float4 clip = mul(position, worldToClip);
    float3 extent = float3(abs((size.x + size.y) * cameraClipScale), 0);
    if (!all(clip.xyz + extent > -abs(clip.w)) || !all(clip.xyz - extent < abs(clip.w))) return;
    float4 right = billboardRight, up = billboardUp;
    if (genericType == 1)
    {
        float sine, cosine; sincos(ReadNumber(slot, 0x3C), sine, cosine);
        right = billboardRight * cosine + billboardUp * sine;
        up = billboardUp * cosine - billboardRight * sine;
    }
    float4 corners[4];
    float4 horizontal = right * (size.x * 0.5), vertical = up * (size.y * 0.5);
    corners[0] = position - horizontal + vertical;
    corners[1] = position - horizontal - vertical;
    corners[2] = position + horizontal + vertical;
    corners[3] = position + horizontal - vertical;
    uint renderingType = ReadWord(slot, 0x28) >> 16;
    if (genericType == 1 && renderingType == 34)
    {
        [unroll] for (uint corner = 0; corner < 4; ++corner) corners[corner].w = size.x * 0.5;
    }
    uint flags = ReadWord(slot, 0x20) >> 16, color = ReadWord(slot, 0x30);
    if ((flags & 8) != 0) color |= 0xFF000000;
    uint parameter = (ReadWord(slot, 0x2C) >> 16) & 255;
    if ((flags & 32) != 0) parameter += 128;
    parameter <<= 24;
    float4 rectangle = float4(ReadNumber(slot, 0x40), ReadNumber(slot, 0x44), ReadNumber(slot, 0x48), ReadNumber(slot, 0x4C));
    uint first;
    Particles.InterlockedAdd(indirectOffset, 6, first);
    WriteVertex(first, corners[0], color, parameter, rectangle.xy);
    WriteVertex(first + 1, corners[1], color, parameter, rectangle.xw);
    WriteVertex(first + 2, corners[2], color, parameter, rectangle.zy);
    WriteVertex(first + 3, corners[2], color, parameter, rectangle.zy);
    WriteVertex(first + 4, corners[1], color, parameter, rectangle.xw);
    WriteVertex(first + 5, corners[3], color, parameter, rectangle.zw);
}
