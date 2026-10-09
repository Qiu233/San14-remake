cbuffer ToneParameters : register(b0)
{
    float4 vToneMapParams;
    float fGamma;
    float fToneCurveType;
};
Texture2D<float4> t0 : register(t0);
Texture2D<float4> t1 : register(t1);
Texture3D<float4> t2 : register(t2);
SamplerState s0 : register(s0);
SamplerState s1 : register(s1);
SamplerState s2 : register(s2);

struct Input
{
    float4 position : SV_Position;
    float4 color : COLOR;
    float2 uv : TEXCOORD;
};

float4 main(Input input) : SV_Target
{
    float4 scene = t0.Sample(s0, input.uv);
    float2 reference = t1.Sample(s1, float2(0.75f, 0.5f)).xy;
    float mode = reference.x;
    if (vToneMapParams.w < 0.0f && all(scene.rgb == 0.0f))
        mode = 0.0f;
    [branch]
    if (mode == 0.5f)
    {
        float exposure = t1.Sample(s1, float2(0.25f, 0.5f)).x;
        scene.rgb = scene.rgb * exposure;
        [branch]
        if (fToneCurveType == 0.0f)
        {
            float luminance = dot(scene.rgb,
                float3(asfloat(0x3e990abbu), asfloat(0x3f162c13u), asfloat(0x3dea747eu)));
            float numerator = mad(luminance, vToneMapParams.x, 1.0f);
            float denominator = luminance + 1.0f;
            float gain = numerator / denominator;
            scene.rgb = scene.rgb * gain;
        }
        else
        {
            [branch]
            if (fToneCurveType == 0.1f || fToneCurveType == 0.2f)
            {
                bool first = fToneCurveType < 0.2f;
                float4 coefficients = first
                    ? float4(asfloat(0x3e6147aeu), asfloat(0x3e99999au), asfloat(0x3cf5c28fu), asfloat(0x3b03126fu))
                    : float4(asfloat(0x3e19999au), asfloat(0x3f000000u), asfloat(0x3d4ccccdu), asfloat(0x3b83126fu));
                float3 numerator = mad(coefficients.x, scene.rgb, coefficients.z);
                numerator = mad(scene.rgb, numerator, coefficients.w);
                float3 denominator = mad(coefficients.x, scene.rgb, coefficients.y);
                denominator = mad(scene.rgb, denominator, asfloat(0x3d75c28fu));
                float3 curve = numerator / denominator;
                float correction = first ? asfloat(0xbd086595u) : asfloat(0xbd89374cu);
                curve = correction + curve;
                scene.rgb = reference.y * curve;
            }
            else
            {
                [branch]
                if (fToneCurveType < 0.0f)
                {
                    float3 coordinates = mad(scene.rgb, asfloat(0x40b1c71du), asfloat(0x3d449774u));
                    coordinates = log2(coordinates);
                    coordinates = saturate(mad(coordinates, asfloat(0x3d96870eu), asfloat(0x3ec5a683u)));
                    scene.rgb = t2.SampleLevel(s2, coordinates, 0.0f).rgb;
                }
            }
        }
    }
    float3 gammaCoordinates = log2(abs(scene.rgb));
    gammaCoordinates = gammaCoordinates * fGamma;
    return float4(exp2(gammaCoordinates), scene.a);
}
