// A procedural triangle for the GGX integral's Draw(3), without a vertex buffer.
float4 main(uint vertexId : SV_VertexID) : SV_Position
{
    float2 corner = float2((vertexId << 1) & 2u, vertexId & 2u);
    return float4(corner * float2(2.0f, -2.0f) + float2(-1.0f, 1.0f), 0.0f, 1.0f);
}
