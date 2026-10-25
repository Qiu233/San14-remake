cbuffer PostprocessConstants : register(b0)
{
    uint capacity, genericAddress; uint2 postprocessPadding;
};
[numthreads(64, 1, 1)]
void Postprocess(uint3 thread : SV_DispatchThreadID)
{
    uint slot = thread.x;
    if (slot >= capacity || (ParticleWord(capacity, slot, 4) & 0x40000) == 0) return;
    uint uv = Relative(genericAddress + 0x200), type = Word(uv), count = Word(uv + 12);
    if (type == 0) return; // The original direct dispatch has no type-zero UV update.
    float elapsed = ParticleNumber(capacity, slot, 0x6C), frame = ParticleNumber(capacity, slot, 0x64);
    if (type == 1) frame = fmod(Number(uv + 8) * elapsed + ParticleWord(capacity, slot, 0x60), count);
    else if (type == 2 || type == 3)
    {
        frame = fmod(frame + 1, Number(uv + 8) * count);
        StoreNumber(capacity, slot, 0x64, frame);
        frame = floor(frame) / Number(uv + 8);
    }
    float4 rectangle = Vector(uv + 16 + (uint)frame * 16);
    if (type == 4)
    {
        uint packed = Word(uv + 8);
        int u = (int)(packed << 16) >> 16, v = (int)packed >> 16;
        float2 offset = fmod(float2(u, v) * elapsed / 1024.0, ceil(abs(rectangle.zw - rectangle.xy)));
        rectangle += offset.xyxy;
    }
    StoreVector(capacity, slot, 0x40, rectangle);
}
