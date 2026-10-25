Buffer<uint> NoisePermutation : register(t125);
Buffer<float4> VectorNoise : register(t127);
RWBuffer<uint> LiveParticleCount : register(u1);
cbuffer MotionConstants : register(b0)
{
    uint capacity; float seconds; uint genericAddress; uint motionPadding;
};
// A vector component cannot be an indirectly addressed destination in DXBC.
// Explicit destinations let FXC retain the dynamic noise and auxiliary loops
// instead of trying to unroll their complete call tree.
void SetNoiseFloatComponent(inout float4 value, uint component, float scalar)
{
    if (component == 0) value.x = scalar;
    else if (component == 1) value.y = scalar;
    else if (component == 2) value.z = scalar;
    else value.w = scalar;
}
void SetNoiseIntComponent(inout int4 value, uint component, int scalar)
{
    if (component == 0) value.x = scalar;
    else if (component == 1) value.y = scalar;
    else if (component == 2) value.z = scalar;
    else value.w = scalar;
}
float3 Lattice(float4 coordinates, uint dimensions)
{
    float4 scaled = coordinates * 16, floors = floor(scaled), weight = 1 - (scaled - floors);
    int4 initial = (int4)floors, current = initial;
    float3 value = 0;
    [loop] for (uint corner = 0; corner < (1u << dimensions); ++corner)
    {
        float gain = weight.x;
        [loop] for (uint axis = 1; axis < dimensions; ++axis) gain *= weight[axis];
        int hash = current.x;
        [loop] for (uint lane = 1; lane < dimensions; ++lane) hash = NoisePermutation[hash & 255] + current[lane];
        value += gain * VectorNoise[NoisePermutation[hash & 255]].xyz;
        [loop] for (uint bit = 0; bit < dimensions; ++bit)
        {
            SetNoiseFloatComponent(weight, bit, 1 - weight[bit]);
            if (((corner >> bit) & 1) == 0) { SetNoiseIntComponent(current, bit, initial[bit] + 1); break; }
            SetNoiseIntComponent(current, bit, initial[bit]);
        }
    }
    return value;
}
float3 SampleLeaf(uint node, float3 position)
{
    uint type = Word(node + 4);
    if (type == 6)
    {
        float3 delta = position - Vector(node + 0x20).xyz;
        float3 value = cross(Vector(node + 0x30).xyz, delta);
        return value * (Number(node + 0x64) / max(Number(node + 0x60), dot(value, value)));
    }
    float4 source = float4(position, Number(node + 0x10)) - Vector(node + 0x30);
    float4 scales = float4(Vector(node + 0x20).xyz, Number(node + 0x64));
    uint mask = Word(node + 0x6C), dimensions = 0;
    float4 coordinates = 0;
    [unroll] for (uint axis = 0; axis < 4; ++axis)
        if ((mask & (1u << axis)) != 0)
        {
            SetNoiseFloatComponent(coordinates, dimensions, source[axis] * scales[axis]);
            ++dimensions;
        }
    float amplitude = Number(node + 0x60), frequency = 1;
    float3 result = 0;
    [loop] for (uint octave = 0; octave < Word(node + 0x68); ++octave)
    {
        result += amplitude * Lattice(coordinates * frequency, dimensions);
        frequency *= 2; amplitude *= 0.5;
    }
    return result;
}
float3 SampleAuxiliary(uint root, float3 position)
{
    uint stack[8], child[8]; uint depth = 1;
    stack[0] = root; child[0] = 0;
    float3 result = 0;
    [loop] while (depth != 0)
    {
        uint top = depth - 1, node = stack[top];
        if (Word(node + 4) != 0) { result += SampleLeaf(node, position); --depth; }
        else if (child[top] >= Word(node + 0x2C)) --depth;
        else
        {
            uint at = node + 0x30 + child[top]++ * 4;
            stack[depth] = Relative(at); child[depth] = 0; ++depth;
        }
    }
    return result;
}
[numthreads(64, 1, 1)]
void Motion(uint3 thread : SV_DispatchThreadID)
{
    uint slot = thread.x;
    if (slot >= capacity || (ParticleWord(capacity, slot, 4) & 0x40000) == 0) return;
    uint motionField = genericAddress + 0x204;
    if (Word(motionField) != 0 && seconds >= 1.1920929e-5)
    {
        uint descriptor = Relative(motionField);
        uint velocityField = Field(genericAddress, 0x48), positionField = Field(genericAddress, 0x44);
        float3 velocity = ParticleVector(capacity, slot, velocityField).xyz;
        float4 position = ParticleVector(capacity, slot, positionField);
        float mass = ParticleNumber(capacity, slot, Field(genericAddress, 0x90));
        float damping = ParticleNumber(capacity, slot, Field(genericAddress, 0x94));
        float3 force = 0;
        [loop] for (uint index = 0; index < Word(descriptor + 0x10); ++index)
        {
            uint operation = Relative(descriptor + 0x14 + index * 4), type = Word(operation + 4);
            if (type == 0) force += Vector(operation + 0x10).xyz * mass;
            else if (type == 2) force -= damping * velocity;
            else if (type == 3)
            {
                uint samplerAddress = operation + 0x10;
                float3 target = SampleAuxiliary(Relative(samplerAddress + 0x10), position.xyz);
                force += damping * (target - velocity);
            }
            // Type fifteen is deliberately absent from the original GPU force
            // dispatch. Its CPU expression writer must not be injected here.
        }
        velocity += force * (seconds / mass);
        position.xyz += seconds * velocity;
        StoreVector(capacity, slot, velocityField, float4(velocity, 0));
        StoreVector(capacity, slot, positionField, position);
    }
    InterlockedAdd(LiveParticleCount[0], 1);
}
