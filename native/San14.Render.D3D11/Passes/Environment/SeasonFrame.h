#pragma once
// Same-request season interchange packet. Imported layout is decoded here;
// loaded textures, programs and retained pass histories remain session resources.
#include "../Shadows/SeasonShadowInputs.h"
#include "CloudWeatherInputs.h"
#include "../PostProcessing/SeasonPostInputs.h"
#include "../../Camera/CameraState.h"
#include <array>
#include <cstdint>
#include <cstring>
#include <span>
#include <stdexcept>
#include <string>
#include <vector>

namespace source_current_season {
struct Word {int32_t offset;uint32_t bits;};
struct Light {
    uint32_t object,mask;
    uint32_t terrainMaskScalarBits{};
    std::array<uint8_t,0x328> data;
    source_current_season_shadow::LightGeometry shadowGeometry;
};
struct ExposureLight {uint32_t auxiliarySelector{},object{},present{},type{},flags{};std::array<float,3> rgb{};};
static_assert(sizeof(ExposureLight)==32);
struct HdrWrite {uint32_t offset;std::vector<uint8_t> bytes;};
struct HdrPass {std::string identity;uint32_t slot,constantBytes;std::vector<HdrWrite> writes;};
struct Frame {
    uint32_t mode=0,season=0;int64_t cameraIdentity=0,worldOrdinal=0;
    std::array<uint8_t,0x3c0> main{};
    std::vector<Word> words;std::array<Light,5> lights{};
    std::array<float,74> atmosphere{};
    ExposureLight exposure;
    std::vector<HdrPass> hdr;
    source_current_season_shadow::Settings shadow;
    source_current_season_shadow::ReceiverStyle shadowReceiver;
    source_current_cloud_weather::Conditions cloudWeather;
    source_current_season_post::Settings post;
};
template<class T> inline T read(std::span<const uint8_t> bytes,size_t at){
    T result;std::memcpy(&result,bytes.data()+at,sizeof(result));return result;
}
// Sparse Main fields are an interchange detail. Read them once at import;
// shadow preparation consumes named values and never queries offset tables.
template<class T> inline std::optional<T> importedMainField(const Frame& frame,int offset){
    if(offset>=0&&size_t(offset)+sizeof(T)<=frame.main.size())
        return read<T>(frame.main,size_t(offset));
    for(const auto& word:frame.words)if(word.offset==offset){
        T result;std::memcpy(&result,&word.bits,sizeof(result));return result;
    }
    return std::nullopt;
}
inline void decodeShadowInputs(Frame& frame){
    auto& settings=frame.shadow;
    settings.selectedLightGroup=importedMainField<int32_t>(frame,0xa00);
    const auto direction=importedMainField<uint32_t>(frame,0xa04);
    settings.directionChannel=direction?std::optional(static_cast<source_current_season_shadow::DirectionChannel>(
        *direction>=1&&*direction<=3?*direction:0)):std::nullopt;
    settings.planeScale=importedMainField<float>(frame,0xa28);
    settings.maximumDistance=importedMainField<float>(frame,0x9ec);
    settings.nearDistance=importedMainField<float>(frame,0x9f0);
    settings.farDistanceOverride=importedMainField<float>(frame,0xa2c);
    settings.sampleLoopCount=importedMainField<uint32_t>(frame,0xa38);
    settings.qualityLevel=importedMainField<uint32_t>(frame,0xa88);
    const auto mode=importedMainField<uint32_t>(frame,0xa3c);
    settings.compactMode=mode?std::optional(static_cast<source_current_season_shadow::CompactMode>(*mode)):std::nullopt;
    settings.rectangleOverride=importedMainField<uint32_t>(frame,0xa84);
    settings.cascadeSplits={importedMainField<float>(frame,0x9f4),
        importedMainField<float>(frame,0x9f8),importedMainField<float>(frame,0x9fc)};
    settings.halfFovTangent=frame.atmosphere[196/4];
    frame.shadowReceiver={read<int32_t>(frame.main,0x3ac),
        read<float>(frame.main,0x27c),read<float>(frame.main,0x3bc)};
    for(auto& light:frame.lights){
        for(size_t channel=0;channel<light.shadowGeometry.directions.size();++channel)
            light.shadowGeometry.directions[channel]=read<source_camera::V4>(light.data,channel*0x20);
        light.shadowGeometry.receiverAmbientAlpha=read<float>(light.data,0xec);
    }
}
inline void decodeCloudWeatherInputs(Frame& frame){
    frame.cloudWeather={read<float>(frame.main,0x1d0),read<float>(frame.main,0x1d8),
        read<float>(frame.main,0x22c),read<float>(frame.main,0x278),
        importedMainField<uint32_t>(frame,0x674).has_value(),
        importedMainField<uint32_t>(frame,0x7a0).has_value()};
}
inline void decodePostInputs(Frame& frame){
    auto& post=frame.post;
    post.camera=source_camera::decodeCameraTransform(frame.main);
    for(size_t i=0;i<frame.lights.size();++i)
        post.lights[i]={frame.lights[i].object,frame.lights[i].mask};
    post.selectedSunDirection=read<std::array<float,3>>(frame.main,0x2b8);
    post.adaptation=read<std::array<float,2>>(frame.main,0x1c4);
    post.exposureRange=read<std::array<float,2>>(frame.main,0x1b8);
    post.heat={importedMainField<float>(frame,0x13c0),importedMainField<float>(frame,0x13c4)};
    post.heatCoefficient=importedMainField<float>(frame,0x13c8);
    post.spriteThreshold=importedMainField<float>(frame,0x1324);
    post.userExposure=read<float>(frame.main,0x170);
    post.depthOfFieldMode=importedMainField<uint32_t>(frame,0x1320);
    const auto exposureFlags=importedMainField<uint32_t>(frame,0x1510);
    if(exposureFlags)post.automaticExposure=(*exposureFlags&1)!=0;
    post.transition={importedMainField<uint32_t>(frame,0x13f0),
        importedMainField<float>(frame,0x13f4),importedMainField<float>(frame,0x13f8)};
}
inline Frame decode(std::span<const uint8_t> bytes){
    if(bytes.size()<32+0x3c0)throw std::runtime_error("Current season packet length");
    Frame r;r.mode=read<uint32_t>(bytes,0);r.season=read<uint32_t>(bytes,4);
    uint32_t count=read<uint32_t>(bytes,8),lights=read<uint32_t>(bytes,12);
    if(lights!=5||bytes.size()<32+0x3c0+size_t(count)*8+lights*(16+0x328)+296+sizeof(ExposureLight))
        throw std::runtime_error("Current season packet layout");
    r.cameraIdentity=read<int64_t>(bytes,16);r.worldOrdinal=read<int64_t>(bytes,24);
    std::memcpy(r.main.data(),bytes.data()+32,r.main.size());size_t at=32+0x3c0;
    r.words.resize(count);for(auto& w:r.words){w=read<Word>(bytes,at);at+=8;}
    for(auto& l:r.lights){l.object=read<uint32_t>(bytes,at);l.mask=read<uint32_t>(bytes,at+4);
        l.terrainMaskScalarBits=read<uint32_t>(bytes,at+8);
        std::memcpy(l.data.data(),bytes.data()+at+16,l.data.size());at+=16+l.data.size();}
    std::memcpy(r.atmosphere.data(),bytes.data()+at,296);at+=296;
    r.exposure=read<ExposureLight>(bytes,at);at+=sizeof(ExposureLight);
    if(at<bytes.size()){
        auto word=[&](){if(at+4>bytes.size())throw std::runtime_error("Current HDR packet length");
            uint32_t value=read<uint32_t>(bytes,at);at+=4;return value;};
        uint32_t passes=word();r.hdr.reserve(passes);
        for(uint32_t i=0;i<passes;i++){
            HdrPass pass;uint32_t length=word();pass.slot=word();pass.constantBytes=word();uint32_t writes=word();
            if(length>bytes.size()-at)throw std::runtime_error("Current HDR identity length");
            pass.identity.assign(reinterpret_cast<const char*>(bytes.data()+at),length);at+=length;
            pass.writes.reserve(writes);for(uint32_t j=0;j<writes;j++){
                HdrWrite write;write.offset=word();uint32_t n=word();
                if(n>bytes.size()-at||size_t(write.offset)+n>pass.constantBytes)throw std::runtime_error("Current HDR write extent");
                write.bytes.assign(bytes.begin()+at,bytes.begin()+at+n);at+=n;pass.writes.push_back(std::move(write));
            }
            r.hdr.push_back(std::move(pass));
        }
    }
    if(at!=bytes.size())throw std::runtime_error("Current HDR packet tail");
    decodeShadowInputs(r);
    decodeCloudWeatherInputs(r);
    decodePostInputs(r);
    return r;
}
// World selection belongs to its ordinary owner. This is the original ordered
// first-intersecting group query over this same five-record publication.
inline const Light* firstMatching(const Frame& frame,uint32_t queryMask){
    for(const auto& l:frame.lights)if(l.mask&queryMask)return &l;return nullptr;
}
inline void publishHdr(const HdrPass& pass,std::span<char> currentConstant){
    if(currentConstant.size()!=pass.constantBytes)throw std::runtime_error("Current HDR loaded CB changed");
    for(const auto& write:pass.writes)std::memcpy(currentConstant.data()+write.offset,write.bytes.data(),write.bytes.size());
}
} // namespace source_current_season
