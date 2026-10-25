// Project-owned direct-particle controller update. The normal declarations
// and SoA field readers are supplied by G1EffectDirect.Common.hlsl.
// Motion and UV/postprocess are separate source-ordered compute stages.
cbuffer UpdateConstants : register(b0)
{
    uint updateCapacity;
    float updateSeconds;
    uint updateGeneric;
    uint updatePadding;
};
cbuffer UpdateCameraConstants : register(b1)
{
    float4 updateViewZ;
    float updateFade;
    float updateAlphaScale;
    float2 updateCameraPadding;
};

float UpdateSmooth(float phase)
{
    return (3.0 - 2.0 * phase) * (phase * phase);
}

// Scalar descriptors contain an initial size/type/flags prefix. The resource
// validator admits only the implemented curve kinds before creating this kernel.
float UpdateCurve(uint curve, float current, float time, float scale, float step)
{
    uint kind = Word(curve + 4);
    float4 first = Vector(curve + 16);
    if (kind == 0 || kind == 2)
    {
        float phase = saturate(mad(time, first.y, first.x));
        if (kind == 2) phase = UpdateSmooth(phase);
        return scale * mad(phase, first.w, first.z);
    }
    if (kind == 1)
    {
        float elapsed = time - first.x;
        if (elapsed <= step)
        {
            current = scale * first.z;
            step = elapsed;
            if (elapsed <= 0.0) return current;
        }
        float target = scale * first.w;
        return (current - target) / mad(step, first.y, 1.0) + target;
    }
    float3 values = float3(Number(curve + 32), Number(curve + 36), Number(curve + 40));
    if (kind == 6 || kind == 8)
    {
        float elapsed = time - first.x;
        if (elapsed <= step)
        {
            current = scale * values.x;
            step = elapsed;
            if (elapsed <= 0.0) return current;
        }
        float phase = mad(time, first.w, first.z);
        if (phase > 0.0)
        {
            phase = saturate(phase);
            if (kind == 8) phase = UpdateSmooth(phase);
            return scale * mad(phase, values.z, values.y);
        }
        float target = scale * values.y;
        return (current - target) / mad(first.y, step, 1.0) + target;
    }
    float2 phase = saturate(mad(time.xx, first.yw, first.xz));
    if (kind == 9 || kind == 11) phase.x = UpdateSmooth(phase.x);
    if (kind == 5 || kind == 11) phase.y = UpdateSmooth(phase.y);
    return scale * mad(phase.y, values.z, mad(phase.x, values.y, values.x));
}

float UpdateScalarController(uint controller, uint slot, bool writeResult)
{
    uint kind = Word(controller + 4);
    uint destination = Word(controller + 8);
    float current = ParticleNumber(updateCapacity, slot, destination);
    float time = ParticleNumber(updateCapacity, slot, Word(controller + 16));
    float step = updateSeconds;
    float scale = 1.0;
    if (kind == 1 || kind == 3)
    {
        float inverse = 1.0 / ParticleNumber(updateCapacity, slot, Word(controller + 20));
        time *= inverse;
        step *= inverse;
    }
    if (kind == 2 || kind == 3)
        scale = ParticleNumber(updateCapacity, slot, Word(controller + 24));
    float value = UpdateCurve(controller + 32, current, time, scale, step);
    if (writeResult) StoreNumber(updateCapacity, slot, destination, value);
    return value;
}

void UpdatePackedChannel(uint slot, uint destinationByte, float value)
{
    uint shift = (destinationByte - 48u) * 8u;
    uint packed = ParticleWord(updateCapacity, slot, 48);
    uint channel = (uint)(value * 255.0);
    packed = (packed & ~(255u << shift)) | ((channel & 255u) << shift);
    StoreWord(updateCapacity, slot, 48, packed);
}

void UpdateController(uint controller, uint slot)
{
    uint kind = Word(controller + 4);
    if (kind <= 3)
    {
        UpdateScalarController(controller, slot, true);
        return;
    }
    // Seven converts a nested scalar directly; eight additionally retains its
    // scalar target. Thirteen wraps the nested result to the signed turn range.
    // Their nested descriptor was validated to be a supported scalar controller.
    float value = UpdateScalarController(controller + 16, slot, kind == 8);
    if (kind == 7 || kind == 8)
        UpdatePackedChannel(slot, Word(controller + 8), value);
    else
    {
        float turn = mad(value, 0.15915494309189535, 0.5);
        turn -= floor(turn);
        StoreNumber(updateCapacity, slot, Word(controller + 8), mad(turn, 6.283185307179586, -3.141592653589793));
    }
}

[numthreads(64, 1, 1)]
void Update(uint3 thread : SV_DispatchThreadID)
{
    uint slot = thread.x;
    if (slot >= updateCapacity) return;
    uint header = ParticleWord(updateCapacity, slot, 4);
    uint flags = header >> 16;
    if ((flags & 4u) == 0) return;
    uint policy = Word(updateGeneric + 528);
    float age = ParticleNumber(updateCapacity, slot, 108) + updateSeconds;
    StoreNumber(updateCapacity, slot, 108, age);
    if ((policy & 1u) != 0 && age * 1.0000119209289551 >= ParticleNumber(updateCapacity, slot, 104))
    {
        StoreWord(updateCapacity, slot, 4, header & ~0x00040000u);
        return;
    }
    uint count = Word(updateGeneric + 520);
    if (count != 0)
    {
        uint table = Relative(updateGeneric + 524);
        [loop] for (uint index = 0; index < count; ++index)
            UpdateController(Relative(table + index * 4), slot);
        if ((policy & 2u) != 0 && (ParticleWord(updateCapacity, slot, 48) >> 24) < 2u)
        {
            flags &= ~4u;
            StoreWord(updateCapacity, slot, 4, (header & 65535u) | (flags << 16));
        }
    }
    // The installed four families have no secondary controller here. The
    // managed preparation refuses other lists rather than silently skipping them.
    if ((policy & 65536u) != 0 && count != 0)
    {
        uint alpha = ParticleWord(updateCapacity, slot, 48) >> 24;
        flags = alpha >= 2u ? flags | 2u : flags & ~2u;
        StoreWord(updateCapacity, slot, 4, (header & 65535u) | (flags << 16));
    }
}
