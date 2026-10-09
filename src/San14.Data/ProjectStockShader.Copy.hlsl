cbuffer PixelParameters : register(b0)
{
    float vATest;
};
Texture2D<float4> tex : register(t0);
SamplerState smp : register(s0);

struct Input
{
    float4 position : SV_Position;
    float4 color : COLOR;
    float2 uv : TEXCOORD;
};

float4 Default3(Input input) : SV_Target
{
    float4 sample = tex.Sample(smp, input.uv);
    clip(mad(sample.a, input.color.a, -vATest));
    return sample * input.color;
}

float4 Default1(Input input) : SV_Target
{
    clip(input.color.a - vATest);
    float4 sample = tex.Sample(smp, input.uv);
    return float4(sample.rgb * input.color.rgb, input.color.a);
}
