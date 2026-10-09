Texture2D<float4> t0 : register(t0);

float main(float4 position : SV_Position) : SV_Depth
{
    return t0.Load(int3((int2)position.xy, 0)).x;
}
