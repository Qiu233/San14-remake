// Include after ocean-native.h and source-hdr-native.h. The existing lab calls
// its deferred Ocean owner NativeOcean116. No shader/texture/IB is recreated.
#pragma once
#include "WaterGeometry.h"
#include <map>
#include <string>

struct NativeWaterCameraGpu {
    source_water_geometry::Profile profile;
    ComPtr<ID3D11Buffer> instances,farVertices;
    source_water_geometry::Frame current;
    source_water_camera::RefractionDerived refraction;
};

// Capacity and visibility ranges belong to the constructed grid.
// The final instance sentinel is included in that capacity.
inline NativeWaterCameraGpu prepareNativeWaterCameraGpu(ID3D11Device* device,
    const San14::Render::OceanGrid& grid,NativeOcean116& ocean) {
    NativeWaterCameraGpu result;
    result.profile=grid.visibility;
    D3D11_BUFFER_DESC description{};
    description.ByteWidth=result.profile.capacity*UINT(sizeof(source_water_geometry::Instance));
    description.Usage=D3D11_USAGE_DEFAULT;description.BindFlags=D3D11_BIND_VERTEX_BUFFER;
    check(device->CreateBuffer(&description,nullptr,&result.instances),"source Ocean current instance capacity");
    description.ByteWidth=UINT(sizeof(result.current.farVertices));
    check(device->CreateBuffer(&description,nullptr,&result.farVertices),"source Ocean current far quad");
    ocean.geometry.wave[1]=result.instances;ocean.geometry.farVertices=result.farVertices;
    return result;
}

// CPU geometry preparation publishes named camera and clip values. It also
// installs the camera retained for the following Ocean bubble update.
inline const source_water_geometry::Frame& writeNativeWaterCameraCpu(
    NativeWaterCameraGpu& camera,NativeOcean116& ocean,
    const source_camera::CameraTransform& transform,const std::array<float,8>& completedWaveBounds) {
    // NativeOcean116's ordinary constructor owns the CPU displacement wrapper.
    // Its empty GPU child does not remove that owner or its visibility effect.
    auto frame=source_water_geometry::prepareCurrent(camera.profile,transform,completedWaveBounds,true);
    if(frame.instances.size()>camera.profile.capacity)
        throw std::runtime_error("Source Ocean current instance capacity exceeded");
    ocean.materialCamera=source_water_camera::prepareCurrentCamera(transform);
    camera.refraction=ocean.materialCamera.refraction;
    if(frame.waterPlaneVisible) {
        ocean.waveClip=frame.waveClip;
        ocean.farClip=frame.farClip;
        ocean.bubbleRuntime.installGeometryCamera(
            {transform.eye.X,transform.eye.Y,transform.eye.Z,transform.eye.W},
            {transform.target.X,transform.target.Y,transform.target.Z,transform.target.W});
    }
    ocean.strips.clear();ocean.strips.reserve(frame.strips.size());
    for(const auto& strip:frame.strips) {
        NativeOceanStrip target;
        target.ordinal=UINT(ocean.strips.size());target.flat=strip.flat;
        target.indexCount=strip.indexCount;target.startIndex=strip.startIndex;
        target.baseVertex=static_cast<int>(strip.baseVertex);
        target.offsets={0,strip.instanceOffset,0};ocean.strips.push_back(target);
    }
    camera.current=std::move(frame);return camera.current;
}

// Every accepted camera request, after publishing that wave side's VB/AABB and
// before116. Instance stride0, original layouts and index ranges stay retained.
// executeNativeOcean116 composes and uploads the actual selected material layout.
inline const source_water_geometry::Frame& updateNativeWaterCameraGpu(
    NativeWaterCameraGpu& camera,ID3D11DeviceContext* context,NativeOcean116& ocean,
    const source_camera::CameraTransform& transform,const std::array<float,8>& completedWaveBounds) {
    const auto& frame=writeNativeWaterCameraCpu(camera,ocean,transform,completedWaveBounds);
    if(!frame.instances.empty()) {
        const UINT bytes=UINT(frame.instances.size()*sizeof(source_water_geometry::Instance));
        D3D11_BOX range{0,0,0,bytes,1,1};
        context->UpdateSubresource(camera.instances.Get(),0,&range,frame.instances.data(),0,0);
    }
    if(frame.waterPlaneVisible)
        context->UpdateSubresource(camera.farVertices.Get(),0,nullptr,frame.farVertices.data(),0,0);
    return frame;
}

// Once-exported selected HDR RDEF writes: position,direction,viewY,D2Z,W2Texture.
// These selected HDR writes borrow named refraction values independently of
// the Ocean shader layout.
struct NativeWaterRefractionWrite {unsigned kind=0,offset=0,sourceOffset=0,bytes=0;};
inline void applyNativeWaterRefractionConstant(const std::vector<NativeWaterRefractionWrite>& writes,
    const source_water_camera::RefractionDerived& current,std::vector<char>& bytes) {
    const void* values[]{current.position.data(),current.direction.data(),current.viewY.data(),
        current.depthToZ.data(),current.worldToTexture.data()};
    for(const auto& field:writes)std::memcpy(bytes.data()+field.offset,
        static_cast<const char*>(values[field.kind])+field.sourceOffset,field.bytes);
}
