#pragma once
#include "CloudVolumeMaterial.h"
// Current fixed Main/season WORK Cloud consumes node3's already advanced
// global phase. It never advances animation or consumes Main Bayer/history.
struct NativeAmbientCloudAxis {UINT sourcePhase=0,sharedIndex=0,cbOffset=0;float position=0,scale=0;};
struct NativeAmbientCloudContinuous {
    std::array<std::vector<char>,6> vertexBytes,pixelBytes;
    std::array<ComPtr<ID3D11Buffer>,6> vertexConstants,pixelConstants;
    std::vector<NativeAmbientCloudAxis> axes;
};
static NativeAmbientCloudContinuous readNativeAmbientCloudContinuous(const fs::path& source,const NativeMainCloud& sharedNode3Cloud) {
    NativeAmbientCloudContinuous result;std::ifstream rows(source/"animation.txt");UINT sourcePhase,cbOffset;float wind,period,position,scale;
    if(!rows)throw std::runtime_error("WORK Cloud shared animation source absent");
    while(rows>>sourcePhase>>cbOffset>>wind>>period>>position>>scale){
        auto found=std::find_if(sharedNode3Cloud.axes.begin(),sharedNode3Cloud.axes.end(),[&](const auto& axis){return axis.sourcePhase==sourcePhase;});
        if(found==sharedNode3Cloud.axes.end())throw std::runtime_error("WORK Cloud phase is absent from shared node3 source");
        // Initial media identify the axis; node three owns its current weather.
        result.axes.push_back({sourcePhase,UINT(found-sharedNode3Cloud.axes.begin()),cbOffset,found->position,found->scale});
    }
    for(UINT face=0;face<6;++face){result.vertexBytes[face]=load(source/("base-face"+std::to_string(face)+"-vs-b0.bin"));result.pixelBytes[face]=load(source/("base-face"+std::to_string(face)+"-ps-b0.bin"));}
    return result;
}
static NativeAmbientCloudContinuous prepareNativeAmbientCloudContinuous(ID3D11Device* device,const fs::path& source,
    NativeAmbient& ambient,const NativeMainCloud& sharedNode3Cloud) {
    auto result=readNativeAmbientCloudContinuous(source,sharedNode3Cloud);
    for(UINT face=0;face<6;++face){result.vertexConstants[face]=buffer(device,result.vertexBytes[face],D3D11_BIND_CONSTANT_BUFFER);
        result.pixelConstants[face]=nativeMainCloudBuffer(device,result.pixelBytes[face]);
        // One-time attachment replaces the old constructor immutable CBs.
        // The ambient owner and this numerical updater retain the same buffers.
        ambient.faces.value().cloudConstants[face].vs=result.vertexConstants[face];ambient.faces.value().cloudConstants[face].ps=result.pixelConstants[face];}
    return result;
}
__declspec(noinline) static float nativeAmbientCloudOffsetAdd(float a,float b){return a+b;}
__declspec(noinline) static float nativeAmbientCloudOffsetMul(float a,float b){return a*b;}
static void updateNativeAmbientCloudPixels(NativeAmbientCloudContinuous& cloud,const NativeMainCloud& sharedNode3Cloud,
    const San14::Render::CloudVolumeMaterial* volume=nullptr) {
    for(auto& bytes:cloud.pixelBytes)for(const auto& axis:cloud.axes){
        const auto& shared=sharedNode3Cloud.axes[axis.sharedIndex];
        const auto offset=volume&&shared.weatherChannel?
            San14::Render::cloudVolumeOffset(*volume,*shared.weatherChannel):std::nullopt;
        float value=offset?*offset:nativeAmbientCloudOffsetMul(nativeAmbientCloudOffsetAdd(shared.phase,axis.position),axis.scale);
        std::memcpy(bytes.data()+axis.cbOffset,&value,4);
    }
}
static void updateNativeAmbientCloudContinuous(ID3D11DeviceContext* context,NativeAmbientCloudContinuous& cloud,const NativeMainCloud& sharedNode3Cloud,
    const San14::Render::CloudVolumeMaterial* volume=nullptr) {
    updateNativeAmbientCloudPixels(cloud,sharedNode3Cloud,volume);
    for(UINT face=0;face<6;++face)context->UpdateSubresource(cloud.pixelConstants[face].Get(),0,nullptr,cloud.pixelBytes[face].data(),0,0);
}
