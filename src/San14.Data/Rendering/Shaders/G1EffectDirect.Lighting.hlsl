ByteAddressBuffer Objects : register(t0);
ByteAddressBuffer Linked : register(t1);
ByteAddressBuffer Main : register(t2);
RWBuffer<float4> Positions : register(u0);
RWBuffer<float3> Attributes : register(u1);
RWBuffer<uint> ExtraAttributes : register(u2);
RWBuffer<float> ScatterAttributes : register(u3);
cbuffer PointLightConstants : register(b0)
{
    uint capacity, genericAddress, mainCapacity, mainGenericAddress;
    uint extraFlags, extraCount, attenuationType; float occlusion;
    float2 scatter; float exposure; uint firstLight;
};
uint ObjectWord(uint at) { return Objects.Load(at); }
int MainField(uint field) { return asint(ObjectWord(mainGenericAddress + 0x20 + field * 4)); }
float LinkedNumber(uint slot, uint field) { return asfloat(Linked.Load(capacity * field + slot * 4)); }
float MainNumber(uint parent, int field) { return asfloat(Main.Load(mainCapacity * field + parent * 4)); }
float3 MainVector(uint parent, int field)
{ return float3(MainNumber(parent, field), MainNumber(parent, field + 4), MainNumber(parent, field + 8)); }
[numthreads(64,1,1)]
void ProjectPointLights(uint3 thread : SV_DispatchThreadID)
{
    uint slot = thread.x;
    if (slot >= capacity) return;
    uint output = firstLight + slot;
    if ((Linked.Load(capacity * 4 + slot * 4) & 0x40000) == 0)
    { Positions[output] = float4(0,0,0,-1); return; }
    float radius = LinkedNumber(slot, 0x44);
    if (radius <= 0) { Positions[output] = float4(0,0,0,-1); return; }
    uint parent = Linked.Load(capacity * 0x40 + slot * 4);
    uint flags = ObjectWord(genericAddress + 0x210), inheritance = ObjectWord(genericAddress + 0x214);
    uint radiusIndex = (flags & 12) != 0 ? inheritance & 65535 : 0;
    uint offsetIndex = (flags & 12) != 0 ? inheritance >> 16 : 0;
    if ((flags & 4) != 0)
    {
        int field = MainField(radiusIndex + 33);
        if (field >= 0) radius *= MainNumber(parent, field);
        if (radius <= 0) { Positions[output] = float4(0,0,0,-1); return; }
    }
    float3 color = float3(LinkedNumber(slot,0x30), LinkedNumber(slot,0x34), LinkedNumber(slot,0x38));
    if ((flags & 2) != 0)
    {
        uint packed = Main.Load(mainCapacity * MainField(1) + parent * 4);
        color *= float3((packed >> 16) & 255, (packed >> 8) & 255, packed & 255) / 255.0;
    }
    if ((flags & 64) != 0) color /= max(exposure, 0.000011920929);
    float fade = max(LinkedNumber(slot,0x48), 0.000011920929);
    float3 position = MainVector(parent, MainField(6));
    int offsetField = asint(ObjectWord(genericAddress + 0x5C));
    if (offsetField >= 0)
    {
        float3 offset = float3(LinkedNumber(slot,offsetField), LinkedNumber(slot,offsetField+4), LinkedNumber(slot,offsetField+8));
        if ((flags & 8) != 0)
        { int field = MainField(offsetIndex + 33); if (field >= 0) offset *= MainNumber(parent,field); }
        int basis = MainField(5);
        if ((flags & 16) != 0 && basis >= 0)
            offset = offset.x * MainVector(parent,basis) + offset.y * MainVector(parent,basis+16) + offset.z * MainVector(parent,basis+32);
        position += offset;
    }
    bool varyingAttenuation = (attenuationType & 128) != 0;
    bool thirdAttribute = attenuationType == 0 || (varyingAttenuation && (attenuationType & 8) != 0);
    uint stride = thirdAttribute ? 3 : 2;
    uint attenuation = attenuationType == 3 || varyingAttenuation ? Linked.Load(capacity * 0x28 + slot * 4) >> 16 : attenuationType;
    float exponent = LinkedNumber(slot,0x3C);
    float inverse = 1.0 / (radius * fade);
    float3 factors = attenuation == 2 ? float3(inverse * inverse, exponent + 1, 0) :
        attenuation == 1 ? float3(1,0,(1-fade)/(fade*radius*radius)) : float3(1,0,0);
    Positions[output] = float4(position,radius);
    Attributes[output*stride] = color; Attributes[output*stride+1] = factors;
    if (thirdAttribute) Attributes[output*stride+2] = float3(0,-1,0);
    uint at = output*extraCount;
    if ((extraFlags & 1) != 0) ExtraAttributes[at++] = attenuation;
    if ((extraFlags & 2) != 0) ExtraAttributes[at++] = (uint)(occlusion*255);
    if ((extraFlags & 4) != 0) ExtraAttributes[at] = Linked.Load(capacity * 0x2C + slot*4) & 65535;
    ScatterAttributes[output*2] = scatter.x; ScatterAttributes[output*2+1] = scatter.y;
}
