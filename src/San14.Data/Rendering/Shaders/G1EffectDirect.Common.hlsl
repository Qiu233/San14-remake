// Shared normal G1FX declaration readers. These are resource-format offsets,
// not addresses into an original executable or process.
ByteAddressBuffer EffectObjects : register(t0);
RWByteAddressBuffer ParticleBuffer : register(u0);
uint Word(uint at) { return EffectObjects.Load(at); }
float Number(uint at) { return asfloat(Word(at)); }
uint Relative(uint field) { return field + Word(field); }
float4 Vector(uint at) { return asfloat(EffectObjects.Load4(at)); }
row_major float4x4 Matrix(uint at)
{
    return float4x4(Vector(at), Vector(at + 16), Vector(at + 32), Vector(at + 48));
}
uint Field(uint generic, uint binding) { return Word(generic + binding); }
uint Address(uint capacity, uint slot, uint field) { return capacity * field + slot * 4; }
uint ParticleWord(uint capacity, uint slot, uint field) { return ParticleBuffer.Load(Address(capacity, slot, field)); }
float ParticleNumber(uint capacity, uint slot, uint field) { return asfloat(ParticleWord(capacity, slot, field)); }
void StoreWord(uint capacity, uint slot, uint field, uint value) { ParticleBuffer.Store(Address(capacity, slot, field), value); }
void StoreNumber(uint capacity, uint slot, uint field, float value) { StoreWord(capacity, slot, field, asuint(value)); }
float4 ParticleVector(uint capacity, uint slot, uint field)
{
    return float4(ParticleNumber(capacity, slot, field), ParticleNumber(capacity, slot, field + 4),
        ParticleNumber(capacity, slot, field + 8), ParticleNumber(capacity, slot, field + 12));
}
void StoreVector(uint capacity, uint slot, uint field, float4 value)
{
    [unroll] for (uint lane = 0; lane < 4; ++lane) StoreNumber(capacity, slot, field + lane * 4, value[lane]);
}
uint RandomBits(inout uint random)
{
    random = random * 214013u + 2531011u;
    return (random >> 16) & 32767u;
}
float RandomUnit(inout uint random) { return RandomBits(random) / 32767.0; }

// Prefix scalar expressions are evaluated on the GPU, in declaration order.
// A manual expression stack avoids shader recursion. The managed resource
// consumer validates expression kinds and the maximum depth before submission.
float Scalar(inout uint cursor, uint source, inout uint random)
{
    uint operations[32]; float left[32]; uint stage[32]; uint depth = 0;
    float value = 0; bool descending = true;
    [loop] while (true)
    {
        if (descending)
        {
            uint operation = Word(cursor); cursor += 4;
            if (operation == 0) { value = Number(cursor); cursor += 4; }
            else if (operation == 1) { value = Number(source + Word(cursor)); cursor += 4; }
            else if (operation == 2)
            {
                float center = Number(cursor), spread = Number(cursor + 4); cursor += 8;
                value = (RandomUnit(random) * 2 - 1) * spread + center;
            }
            else if (operation == 32)
            {
                value = Number(source + Word(cursor + 4)) * Number(source + Word(cursor)) /
                    Number(source + Word(cursor + 8)); cursor += 12;
            }
            else
            {
                operations[depth] = operation; stage[depth] = 0; ++depth;
                continue;
            }
            descending = false;
        }
        if (depth == 0) return value;
        uint top = depth - 1, operation = operations[top];
        if (operation == 14) { value *= Number(cursor); cursor += 4; --depth; }
        else if (stage[top] == 0) { left[top] = value; stage[top] = 1; descending = true; }
        else
        {
            if (operation == 12) value = left[top] - value;
            else if (operation == 13) value *= left[top];
            else value = left[top] / (value == 0 ? 1.1754944e-38 : value);
            --depth;
        }
    }
}
float DefinitionScalar(uint definition, uint binding, uint source, inout uint random)
{
    uint cursor = Relative(definition + binding) + 12;
    return Scalar(cursor, source, random);
}

float4 Shape(uint shape, inout uint random)
{
    uint type = Word(shape + 4);
    if (type == 10) return Vector(shape + 0x40);
    float radius = Number(shape + 0x50);
    float start = Number(shape + (type == 0 ? 0x58 : 0x54));
    float end = Number(shape + (type == 0 ? 0x5C : 0x58));
    float angle = RandomUnit(random) * (end - start) + start;
    float sine, cosine; sincos(angle, sine, cosine);
    float4 samplePosition;
    if (type == 6)
        samplePosition = float4(radius * sine, (RandomUnit(random) - 0.5) * Number(shape + 0x5C), radius * cosine, 1);
    else
    {
        float lower = cos(Number(shape + (type == 0 ? 0x64 : 0x60)));
        float upper = cos(Number(shape + (type == 0 ? 0x60 : 0x5C)));
        float height = RandomUnit(random) * (upper - lower) + lower;
        if (type == 0)
        {
            float outer = Number(shape + 0x54), ratio = radius / outer;
            ratio = ratio * ratio * ratio;
            radius = outer * pow((1 - ratio) * RandomUnit(random) + ratio, 1.0 / 3.0);
        }
        float radial = sqrt(1 - height * height) * radius;
        samplePosition = float4(radial * sine, radius * height, radial * cosine, 1);
    }
    return mul(samplePosition, Matrix(shape + 0x10));
}

row_major float4x4 EmissionFrame(uint source, uint shape, float4 position, uint flags)
{
    float3 x = float3(1, 0, 0), y = float3(0, 1, 0), z = float3(0, 0, 1);
    if ((Word(source + 4) & 128) != 0)
    {
        x = normalize(Vector(shape + 0x10).xyz);
        y = normalize(Vector(shape + 0x20).xyz);
        z = normalize(Vector(shape + 0x30).xyz);
    }
    uint mode = flags & 15;
    float3 direction = position.xyz - Vector(shape + 0x40).xyz;
    if (mode != 0 && dot(direction, direction) >= 1.1920929e-5)
    {
        if (mode == 1)
        {
            direction = normalize(direction);
            if (abs(dot(direction, y)) <= 0.9988079)
            { x = normalize(cross(y, direction)); y = cross(direction, x); }
            else
            {
                y = normalize(dot(direction, z) < 0 ? cross(x, direction) : cross(direction, x));
                x = cross(y, direction);
            }
        }
        else
        {
            direction -= y * dot(y, direction);
            direction = dot(direction, direction) >= 1.1920929e-5 ? normalize(direction) : z;
            x = cross(y, direction);
        }
        z = direction;
    }
    return float4x4(float4(x, 0), float4(y, 0), float4(z, 0), Vector(shape + 0x40));
}
