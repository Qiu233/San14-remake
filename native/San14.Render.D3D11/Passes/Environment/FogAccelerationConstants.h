// Retained source node1 camera CBs; include after FogAccel and Model helpers.
#pragma once
#include "FogAccelerationCamera.h"
inline source_aux_camera::FogAccelerationConstants prepareNativeFogAccelerationCamera(
    ID3D11Device* device,const fs::path& source,NativeFogAcceleration& fog) {
    source_aux_camera::FogAccelerationConstants constants;
    const auto vertex=load(source/"pass1"/"vs-b0.bin"),pixel=load(source/"pass1"/"ps-b0.bin");
    if(vertex.size()!=constants.vertex.size()||pixel.size()!=constants.pixel.size())
        throw std::runtime_error("Source FogAccel complete camera CB extent differs");
    std::memcpy(constants.vertex.data(),vertex.data(),vertex.size());
    std::memcpy(constants.pixel.data(),pixel.data(),pixel.size());
    fog.passes[1].vertexConstants[0]=modelContinuousConstantBuffer(device,vertex.data(),UINT(vertex.size()));
    fog.passes[1].pixelConstants[0]=modelContinuousConstantBuffer(device,pixel.data(),UINT(pixel.size()));
    return constants;
}
inline void updateNativeFogAccelerationCamera(ID3D11DeviceContext* context,
    source_aux_camera::FogAccelerationConstants& constants,NativeFogAcceleration& fog,
    const source_camera::Prefix& camera) {
    source_aux_camera::writeCurrentFogAcceleration(constants,camera.data());
    context->UpdateSubresource(fog.passes[1].vertexConstants[0].Get(),0,nullptr,constants.vertex.data(),0,0);
    context->UpdateSubresource(fog.passes[1].pixelConstants[0].Get(),0,nullptr,constants.pixel.data(),0,0);
}
