cbuffer ScreenParameters : register(b0)
{
    float4 ktglViewport;
    float4 ktglColorScale;
};

struct Input
{
    float3 position : POSITION;
    float4 color : COLOR;
    float2 uv : TEXCOORD;
};

struct Output
{
    float4 position : SV_Position;
    float4 color : COLOR;
    float2 uv : TEXCOORD;
};

Output main(Input input)
{
    Output output;
    output.position = float4(mad(input.position.xy, ktglViewport.xy, ktglViewport.zw), input.position.z, 1.0f);
    output.color = input.color * ktglColorScale;
    output.uv = input.uv;
    return output;
}
