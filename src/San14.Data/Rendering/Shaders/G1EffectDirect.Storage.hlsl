// Project-owned implementation of the direct particle storage layout.
// This file is compiled normally; the game never reads an original executable.
RWByteAddressBuffer ParticleBuffer : register(u0);
cbuffer StorageConstants : register(b0)
{
    uint4 header;
    uint4 prototype0;
    uint4 prototype1;
};

[numthreads(64, 1, 1)]
void Initialize(uint3 thread : SV_DispatchThreadID)
{
    uint capacity = header.x;
    if (thread.x == 0) ParticleBuffer.Store(header.y, 0);
    if (thread.x >= capacity) return;
    [unroll] for (uint word = 0; word < 4; ++word)
    {
        ParticleBuffer.Store((word * capacity + thread.x) * 4, prototype0[word]);
    }
    [unroll] for (uint word = 0; word < 4; ++word)
    {
        ParticleBuffer.Store(((word + 4) * capacity + thread.x) * 4, prototype1[word]);
    }
}

[numthreads(1, 1, 1)]
void ResetDraw(uint3 thread : SV_DispatchThreadID)
{
    uint offset = header.x;
    if (prototype0.x == 0)
        ParticleBuffer.Store4(offset, uint4(0, header.y, 0, 0));
    else
    {
        ParticleBuffer.Store4(offset, uint4(prototype0.y, 0, prototype0.z, 0));
        ParticleBuffer.Store(offset + 16, 0);
    }
}
