#pragma once
#include <array>
#include <cmath>

namespace San14::Render {
struct OceanMaterialUvSettings {
    float normalScale{},noiseScaleX{},noiseScaleZ{};
    float heightScaleX{},heightScaleZ{},heightOffsetX{},heightOffsetZ{};
};
struct OceanTextureWaveSettings {
    float amplitude{},length{},width{},kurtosis{},speed{},scatter{},shoalingScale{};
};
struct OceanTextureMaterialSettings {
    OceanMaterialUvSettings uv;
    OceanTextureWaveSettings wave;
};
// Owning texture bindings decide whether a material setter is installed. The
// GPU child may be empty while an owning binding remains present.
struct OceanMaterialTextureBindings {
    bool normal{},noise{},height{};
};
struct OceanTextureMaterialState {
    OceanMaterialUvSettings uv{1000.0f,10000.0f,10000.0f,1.0f,1.0f,0.0f,0.0f};
    float normalRotationDegrees{};
    OceanTextureWaveSettings wave;
};
inline float oceanMaterialScale(float value) {
    return value<=1e-6f?1e-6f:value;
}
inline void applyOceanTextureMaterialSettings(OceanTextureMaterialState& state,
    const OceanTextureMaterialSettings& requested,OceanMaterialTextureBindings bindings) {
    state.wave=requested.wave;
    if(bindings.normal)state.uv.normalScale=oceanMaterialScale(requested.uv.normalScale);
    if(bindings.noise) {
        state.uv.noiseScaleX=oceanMaterialScale(requested.uv.noiseScaleX);
        state.uv.noiseScaleZ=oceanMaterialScale(requested.uv.noiseScaleZ);
    }
    if(bindings.height) {
        state.uv.heightScaleX=oceanMaterialScale(requested.uv.heightScaleX);
        state.uv.heightScaleZ=oceanMaterialScale(requested.uv.heightScaleZ);
        state.uv.heightOffsetX=requested.uv.heightOffsetX;
        state.uv.heightOffsetZ=requested.uv.heightOffsetZ;
    }
}
inline std::array<float,4> oceanNormalUv(const OceanTextureMaterialState& state) {
    const float radians=state.normalRotationDegrees*0.017453292f;
    const float inverseScale=1.0f/state.uv.normalScale;
    return {inverseScale,inverseScale,std::sin(radians),std::cos(radians)};
}
inline std::array<float,4> oceanHeightUv(const OceanTextureMaterialState& state) {
    return {1.0f/state.uv.heightScaleX,1.0f/state.uv.heightScaleZ,
        state.uv.heightOffsetX,state.uv.heightOffsetZ};
}
inline std::array<float,2> oceanTextureWaveSpread(float scatter) {
    float x=1.0f,z=scatter;
    const float lengthSquared=x*x+z*z;
    if(std::abs(lengthSquared-1.0f)>0.01f) {
        const float inverseLength=1.0f/std::sqrt(lengthSquared);
        x*=inverseLength;z*=inverseLength;
    }
    return {x,z};
}
} // namespace San14::Render
