ByteAddressBuffer MainParticles : register(t1);
cbuffer LinkedUpdateConstants : register(b0)
{
    uint capacity; float seconds; uint genericAddress, mainCapacity;
    uint mainGenericAddress; uint3 linkedUpdatePadding;
};
float LinkedCurve(uint curve, float time)
{
    uint kind = Word(curve + 4);
    float4 first = Vector(curve + 16);
    float phase = saturate(time * first.y + first.x);
    if (kind == 0) return phase * first.w + first.z;
    float second = saturate(time * first.w + first.z);
    float3 values = Vector(curve + 32).xyz;
    return (phase * values.y + values.x) + second * values.z;
}
[numthreads(64, 1, 1)]
void LinkedUpdate(uint3 thread : SV_DispatchThreadID)
{
    uint slot = thread.x;
    if (slot >= capacity) return;
    uint header = ParticleWord(capacity, slot, 4);
    if ((header & 0x40000) == 0) return;
    uint parent = ParticleWord(capacity, slot, 0x40);
    uint mainHeader = MainParticles.Load(mainCapacity * 4 + parent * 4);
    float age = ParticleNumber(capacity, slot, 0x5C) + seconds;
    float mainAge = asfloat(MainParticles.Load(mainCapacity * Field(mainGenericAddress, 0x2C) + parent * 4));
    if ((mainHeader & 0x40000) == 0 || age != mainAge)
    { StoreWord(capacity, slot, 4, header & ~0x40000); return; }
    StoreNumber(capacity, slot, 0x5C, age);
    uint table = Relative(genericAddress + 0x208), count = Word(genericAddress + 0x204);
    [loop] for (uint index = 0; index < count; ++index)
    {
        uint controller = Relative(table + index * 4);
        float time = ParticleNumber(capacity, slot, Word(controller + 0x10));
        time /= ParticleNumber(capacity, slot, Word(controller + 0x14));
        float scale = ParticleNumber(capacity, slot, Word(controller + 0x18));
        StoreNumber(capacity, slot, Word(controller + 8), LinkedCurve(controller + 0x20, time) * scale);
    }
}
