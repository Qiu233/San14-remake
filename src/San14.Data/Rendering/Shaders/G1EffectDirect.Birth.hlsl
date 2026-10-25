cbuffer BirthConstants : register(b0)
{
    uint capacity, createCount, startIndex, sourceAddress;
    uint definitionAddress, genericAddress, randomNumber, reserved;
};

void RenderBirth(uint slot, uint layout, inout uint random)
{
    [unroll] for (uint field = 32; field < 48; field += 4)
        StoreWord(capacity, slot, field, Word(layout + field));
    uint uv = Relative(genericAddress + 0x200), count = Word(uv + 12), type = Word(uv);
    uint first = Word(definitionAddress + 0x18) + RandomBits(random) % max(Word(definitionAddress + 0x1C), 1u);
    uint frame = type == 3 ? (uint)fmod(first * Number(uv + 8), count * Number(uv + 8)) : first % count;
    StoreWord(capacity, slot, 0x60, frame);
    StoreNumber(capacity, slot, 0x64, frame);
    StoreNumber(capacity, slot, 0x6C, 0);
    uint rectangle = uv + 16 + (type == 3 ? (uint)(frame / Number(uv + 8)) : frame) * 16;
    StoreVector(capacity, slot, 0x40, Vector(rectangle));
    uint color = Word(layout + 0x30), output = 0;
    [unroll] for (uint channel = 0; channel < 4; ++channel)
    {
        uint lane = channel == 0 ? 2 : channel == 2 ? 0 : channel;
        float scale = DefinitionScalar(definitionAddress, 0x5C + channel * 4, sourceAddress, random);
        uint value = (uint)clamp(scale * ((color >> (lane * 8)) & 255), 0, 255);
        output |= value << (lane * 8);
    }
    StoreWord(capacity, slot, 0x30, output);
    if ((Word(layout + 0x20) & 65535) == 0)
        StoreWord(capacity, slot, 0x3C, Word(layout + 0x3C));
}

[numthreads(64, 1, 1)]
void Birth(uint3 thread : SV_DispatchThreadID)
{
    if (thread.x >= createCount) return;
    uint slot = (thread.x + startIndex) % capacity;
    StoreWord(capacity, slot, 4, (ParticleWord(capacity, slot, 4) & 65535) | 0x70000);
    uint seed = (slot + randomNumber) * 214013u + 2531011u;
    uint random = (slot + 1) * ((seed >> 16) & 32767u);
    uint shape = Relative(sourceAddress + 0x80), flags = Word(definitionAddress + 4);
    float4 position = Shape(shape, random);
    row_major float4x4 orientation = EmissionFrame(sourceAddress, shape, position, flags);
    uint distributionField = definitionAddress + 0x28;
    if (Word(distributionField) != 0)
    {
        uint cursor = Relative(distributionField) + 12;
        while (Word(cursor) == 0) cursor += 4;
        // The supported direct definitions use identity or a literal matrix.
        uint literal = Relative(cursor + 4);
        row_major float4x4 distribution = Matrix(literal);
        position.xyz += mul(float4(distribution[3].xyz, 0), orientation).xyz;
        orientation = mul(distribution, orientation);
    }
    row_major float4x4 world = Matrix(sourceAddress + 0xB0);
    float4 inherited = mul(position, Matrix(sourceAddress + 0x130));
    inherited.xyz *= Number(definitionAddress + 0x70);
    if ((flags & 0x10) == 0 || (Word(genericAddress + 8) & 2) != 0) inherited = 0;
    uint layout = Relative(genericAddress + 0x14), program = Relative(genericAddress + 0x1C);
    uint operationCount = Word(program);
    [loop] for (uint index = 0; index < operationCount; ++index)
    {
        uint packed = Word(program + 4 + index * 4), operation = packed & 65535, subcode = packed >> 16;
        if (operation == 0) RenderBirth(slot, layout, random);
        else if (operation == 1)
            StoreVector(capacity, slot, Field(genericAddress, 0x38), mul(position, world));
        else if (operation == 2)
        {
            float4 velocity = Shape(Relative(definitionAddress + 0x2C), random); velocity.w = 0;
            velocity = mul(velocity, orientation);
            velocity.xyz = velocity.xyz * Number(sourceAddress + 0x20) + inherited.xyz;
            velocity.w = 0;
            StoreVector(capacity, slot, Field(genericAddress, 0x48), mul(velocity, world));
        }
        else if (operation == 5)
        {
            uint binding = subcode == 0 ? 0x44 : subcode == 1 ? 0x40 : subcode == 2 ? 0x48 : 0x34;
            uint field = subcode == 0 ? 4 : subcode == 1 ? 28 : subcode == 2 ? 29 : 40;
            float value = DefinitionScalar(definitionAddress, binding, sourceAddress, random);
            StoreNumber(capacity, slot, Field(genericAddress, 0x20 + field * 4), value);
        }
        else if (operation == 6)
        {
            float scale = DefinitionScalar(definitionAddress, 0x4C, sourceAddress, random);
            if ((Word(genericAddress + 8) & 0x20000) != 0) scale = 1;
            [loop] for (uint axis = 0; axis < subcode; ++axis)
            {
                uint field = Field(genericAddress, 0xA4 + axis * 4);
                uint templateField = field > Word(genericAddress + 0x150) ? field - Word(genericAddress + 0x154) : field;
                StoreNumber(capacity, slot, field, scale * Number(layout + templateField));
            }
        }
        else if (operation == 10)
        {
            uint lane = subcode == 0 ? 2 : subcode == 2 ? 0 : subcode;
            uint value = ((ParticleWord(capacity, slot, Field(genericAddress, 0x24)) >> (lane * 8)) & 255);
            StoreNumber(capacity, slot, Field(genericAddress, 0xB0 + subcode * 4), ((value * 4096 + 254) / 255) / 4096.0);
        }
        else if (operation == 15)
        {
            uint sourceIndex = subcode == 24 ? 15 : subcode - 15;
            uint source = Field(genericAddress, 0x20 + sourceIndex * 4);
            StoreWord(capacity, slot, Field(genericAddress, 0x20 + subcode * 4), ParticleWord(capacity, slot, source));
        }
        else if (operation == 18 && (Word(genericAddress + 8) & 2) != 0)
        {
            [unroll] for (uint axis = 0; axis < 2; ++axis)
            {
                uint field = Field(genericAddress, 0x114 + axis * 4);
                StoreNumber(capacity, slot, field, ParticleNumber(capacity, slot, field) * length(world[axis].xyz));
            }
        }
        // Opcode four is a CPU parent-reference operation; direct GPU storage
        // has no process pointers and the original birth kernel has no writer.
    }
}
