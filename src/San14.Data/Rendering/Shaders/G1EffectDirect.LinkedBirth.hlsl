ByteAddressBuffer MainParticles : register(t1);
cbuffer LinkedBirthConstants : register(b0)
{
    uint capacity, createCount, mainStartIndex, sourceAddress;
    uint definitionAddress, genericAddress, randomNumber, mainCapacity;
    uint mainGenericAddress, counterOffset; float probability; uint linkedBirthPadding;
};
[numthreads(64, 1, 1)]
void LinkedBirth(uint3 thread : SV_DispatchThreadID)
{
    if (thread.x == 0) ParticleBuffer.Store(counterOffset, ParticleBuffer.Load(counterOffset) % capacity);
    DeviceMemoryBarrierWithGroupSync();
    if (thread.x >= createCount) return;
    uint seed = (thread.x + randomNumber) * 214013u + 2531011u;
    uint random = (thread.x + 1) * ((seed >> 16) & 32767u);
    if (RandomUnit(random) >= probability) return;
    uint parent = (thread.x + mainStartIndex) % mainCapacity, allocation;
    ParticleBuffer.InterlockedAdd(counterOffset, 1, allocation);
    uint slot = allocation % capacity, layout = Relative(genericAddress + 0x14);
    StoreWord(capacity, slot, 4, (ParticleWord(capacity, slot, 4) & 65535) | 0x70000);
    StoreWord(capacity, slot, 0x40, parent);
    [unroll] for (uint field = 32; field < 48; field += 4)
        StoreWord(capacity, slot, field, Word(layout + field));
    StoreNumber(capacity, slot, 0x5C, 0);
    [unroll] for (uint lane = 0; lane < 3; ++lane)
    {
        float color = Number(layout + 0x30 + lane * 4);
        if ((Word(genericAddress + 0x210) & 1) == 0)
            color *= DefinitionScalar(definitionAddress, 0x5C + lane * 4, sourceAddress, random);
        StoreNumber(capacity, slot, 0x30 + lane * 4, color);
    }
    StoreWord(capacity, slot, 0x3C, Word(layout + 0x3C));
    StoreWord(capacity, slot, 0x48, Word(layout + 0x48));
    // Type ten's actual program contains render(9), size(1), and the three
    // baseline copies. All scalar RNG draws remain in that source order.
    float scale = DefinitionScalar(definitionAddress, 0x4C, sourceAddress, random);
    if ((Word(genericAddress + 8) & 0x20000) != 0) scale = 1;
    StoreNumber(capacity, slot, Field(genericAddress, 0xA4), scale * Number(layout + Field(genericAddress, 0xA4)));
    [unroll] for (uint lane = 0; lane < 3; ++lane)
        StoreNumber(capacity, slot, Field(genericAddress, 0xEC + lane * 4), ParticleNumber(capacity, slot, Field(genericAddress, 0xB0 + lane * 4)));
    uint mainLife = Field(mainGenericAddress, 0x30), life = Field(genericAddress, 0x30);
    StoreWord(capacity, slot, life, MainParticles.Load(mainCapacity * mainLife + parent * 4));
}
