Texture2D<float4> t0 : register(t0);
Texture2D<float4> t1 : register(t1);
Texture2DArray<float4> t2 : register(t2);
SamplerState s0 : register(s0);
SamplerState s1 : register(s1);
SamplerState s2 : register(s2);

struct Input
{
    float4 position : SV_Position;
    float4 color : COLOR;
    float2 uv : TEXCOORD;
};

struct Output
{
    float4 ab : SV_Target0;
    float4 ns : SV_Target1;
    float4 rs0 : SV_Target2;
    float4 rs1 : SV_Target3;
};

Output main(Input input)
{
    Output output;
    output.ab = t0.Sample(s0, input.uv);
    output.ns = t1.Sample(s1, input.uv);
    output.rs0 = t2.Sample(s2, float3(input.uv, 0.0f));
    output.rs1 = t2.Sample(s2, float3(input.uv, 1.0f));
    return output;
}
