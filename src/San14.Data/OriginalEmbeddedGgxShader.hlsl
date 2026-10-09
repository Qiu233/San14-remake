// Project implementation of the map's two-channel GGX ambient integral.
// Linear roughness spans [0, 2]; the Fresnel weight depends on the view angle.
cbuffer GgxParameters : register(b0)
{
    float Width;
    float Height;
    uint SampleCount;
    float MaxLinearRoughness;
    uint ThreadCountX;
    uint ThreadCountY;
};

uint ReverseBits32(uint value)
{
    value = (value << 16) | (value >> 16);
    value = ((value & 0x55555555u) << 1) | ((value & 0xaaaaaaaau) >> 1);
    value = ((value & 0x33333333u) << 2) | ((value & 0xccccccccu) >> 2);
    value = ((value & 0x0f0f0f0fu) << 4) | ((value & 0xf0f0f0f0u) >> 4);
    return ((value & 0x00ff00ffu) << 8) | ((value & 0xff00ff00u) >> 8);
}

float2 main(float4 position : SV_Position) : SV_Target
{
    precise float nDotV = position.x / Width;
    precise float linearRoughness = position.y / Height;
    linearRoughness = linearRoughness * MaxLinearRoughness;
    precise float roughness4 = linearRoughness * linearRoughness;
    roughness4 = roughness4 * roughness4;
    precise float roughness8 = roughness4 * roughness4;
    precise float viewX = sqrt(1.0f - nDotV * nDotV);
    precise float sampleCount = (float)SampleCount;
    precise float distributionScale = roughness8 - 1.0f;
    precise float visibilityK = roughness4 / 2.0f;
    visibilityK = visibilityK * visibilityK;
    precise float visibilityScale = 1.0f - visibilityK;
    precise float fresnel = 1.0f - nDotV;
    precise float fresnel4 = fresnel * fresnel;
    fresnel4 = fresnel4 * fresnel4;
    fresnel = fresnel * fresnel4;
    precise float baseWeight = 1.0f - fresnel;
    precise float2 accumulated = 0.0f;
    [loop]
    for (uint sample = 0u; sample < SampleCount; ++sample)
    {
        precise float azimuth = (float)sample / sampleCount;
        precise float radial = (float)ReverseBits32(sample) * 2.3283064365386963e-10f;
        azimuth = azimuth * 6.283185307179586f;
        precise float numerator = 1.0f - radial;
        precise float denominator = distributionScale * radial;
        denominator = denominator + 1.0f;
        precise float hZ = sqrt(numerator / denominator);
        precise float hRadius = sqrt(1.0f - hZ * hZ);
        precise float sinPhi, cosPhi;
        sincos(azimuth, sinPhi, cosPhi);
        precise float hX = hRadius * cosPhi;
        precise float hY = sinPhi * hRadius;
        precise float vDotH = dot(float2(viewX, nDotV), float2(hX, hZ));
        precise float reflectedScale = vDotH * 2.0f;
        precise float3 reflected = float3(hX, hY, hZ) * reflectedScale;
        reflected = reflected - float3(viewX, 0.0f, nDotV);
        precise float nDotL = saturate(reflected.z);
        [branch]
        if (nDotL > 0.0f)
        {
            precise float lDotH = saturate(dot(reflected, float3(hX, hY, hZ)));
            precise float nDotH = saturate(hZ);
            precise float visibilityDenominator = lDotH * lDotH;
            visibilityDenominator = visibilityScale * visibilityDenominator;
            visibilityDenominator = visibilityK + visibilityDenominator;
            precise float visibility = 1.0f / visibilityDenominator;
            visibility = visibility * nDotL;
            precise float contribution = vDotH * visibility;
            contribution = contribution / nDotH;
            precise float baseContribution = baseWeight * contribution;
            precise float fresnelContribution = fresnel * contribution;
            accumulated.x = accumulated.x + baseContribution;
            accumulated.y = accumulated.y + fresnelContribution;
        }
    }
    precise float2 result = saturate(accumulated / sampleCount);
    return result;
}
