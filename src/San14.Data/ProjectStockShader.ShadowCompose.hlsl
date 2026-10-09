cbuffer ShadowParameters : register(b0)
{
    float s_fade;
};
Texture2D<float4> t0 : register(t0);
Texture2D<float4> t1 : register(t1);
SamplerState s0 : register(s0);

struct Input
{
    float4 position : SV_Position;
    float4 color : COLOR;
    float2 uv : TEXCOORD;
};

float4 Common140(Input input) : SV_Target
{
    float packedAlpha = t1.Load(int3((int2)input.position.xy, 0)).a;
    uint lowerByte = (uint)(packedAlpha * 65535.0f) & 255u;
    float weight = mad(-(float)lowerByte, asfloat(0x3b808081u), 1.0f);
    float4 sample = t0.Sample(s0, input.uv);
    float alphaDelta = sample.a - 1.0f;
    alphaDelta = alphaDelta * s_fade;
    return float4(sample.rgb, mad(weight, alphaDelta, 1.0f));
}

float4 Common138(Input input) : SV_Target
{
    float4 sample = t0.Sample(s0, input.uv);
    float alphaDelta = sample.a - 1.0f;
    return float4(sample.rgb, mad(s_fade, alphaDelta, 1.0f));
}
