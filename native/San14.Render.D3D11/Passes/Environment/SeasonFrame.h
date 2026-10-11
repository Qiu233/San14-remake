#pragma once
// Same-request season interchange packet. Imported layout is decoded here;
// loaded textures, programs and retained pass histories remain session resources.
#include "../Shadows/SeasonShadowInputs.h"
#include "CloudWeatherInputs.h"
#include "HeightFogInputs.h"
#include "../Lighting/Interop/HdrLightingInput.h"
#include "../Lighting/Interop/HdrProgramBankInput.h"
#include "Ocean/OceanWaveSettings.h"
#include "Ocean/OceanSurfaceSettings.h"
#include "Ocean/OceanOpticsSettings.h"
#include "Ocean/Interop/OceanTextureMaterialInput.h"
#include "Ocean/Interop/OceanBubbleMaterialInput.h"
#include "../PostProcessing/SeasonPostInputs.h"
#include "../../Camera/CameraState.h"
#include "../../Scene/View/Interop/CommonRenderPolicyInput.h"
#include <array>
#include <cstdint>
#include <cstring>
#include <span>
#include <stdexcept>
#include <string>
#include <vector>

namespace source_current_season {
struct Word {int32_t offset;uint32_t bits;};
struct ShadowModes {uint8_t receive{},local{};};
struct Light {
    uint32_t object,mask;
    uint32_t terrainMaskScalarBits{};
    ShadowModes shadowModes;
    std::array<uint8_t,0x328> data;
    source_current_season_shadow::LightGeometry shadowGeometry;
    San14::Render::HdrLightState lighting;
};
struct ExposureLight {uint32_t auxiliarySelector{},object{},present{},type{},flags{};std::array<float,3> rgb{};};
static_assert(sizeof(ExposureLight)==32);
struct Frame {
    uint32_t mode=0,season=0;int64_t cameraIdentity=0,worldOrdinal=0;
    std::array<uint8_t,0x3c0> main{};
    std::vector<Word> words;std::array<Light,5> lights{};
    std::array<float,74> atmosphere{};
    ExposureLight exposure;
    std::optional<San14::Render::HdrLightingSettings> hdrLighting;
    std::optional<San14::Render::HdrCurrentSelectionSettings> hdrSelection;
    source_current_season_shadow::Settings shadow;
    source_current_season_shadow::ReceiverStyle shadowReceiver;
    source_current_cloud_weather::Conditions cloudWeather;
    std::optional<std::uint32_t> oceanLightGroup;
    San14::Render::OceanWaveSettings oceanWave;
    San14::Render::OceanSurfaceSettings oceanSurface;
    San14::Render::OceanOpticsSettings oceanOptics;
    San14::Render::OceanTextureMaterialSettings oceanTextureMaterial;
    San14::Render::OceanBubbleMaterialSettings oceanBubbleMaterial;
    San14::Render::CommonRenderPolicy commonRenderPolicy;
    San14::Render::HeightFogSettings heightFog;
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
inline void decodeOceanWaveInputs(Frame& frame) {
    auto required=[&](int offset) {
        const auto value=importedMainField<float>(frame,offset);
        if(!value)throw std::runtime_error("Current Ocean wave scalar absent");
        return *value;
    };
    frame.oceanWave={required(0xb20),required(0xb24),required(0xb28),
        required(0xb2c),required(0xb30),required(0xb34)};
    San14::Render::validateOceanWaveSettings(frame.oceanWave);
}
inline void decodeOceanSurfaceInputs(Frame& frame) {
    auto required=[&]<class T>(int offset) {
        const Word* found=nullptr;
        for(const auto& word:frame.words)if(word.offset==offset) {
            if(found)throw std::runtime_error("Duplicate current Ocean surface scalar");
            found=&word;
        }
        if(!found)throw std::runtime_error("Current Ocean surface scalar absent");
        T result;std::memcpy(&result,&found->bits,sizeof(result));return result;
    };
    frame.oceanSurface={required.operator()<float>(0xb1c),required.operator()<float>(0xb3c),
        required.operator()<float>(0xba8),required.operator()<float>(0xbac),
        required.operator()<float>(0xb9c),required.operator()<float>(0xb54),
        required.operator()<uint32_t>(0xd18),
        {required.operator()<float>(0xc24),required.operator()<float>(0xc5c)}};
    San14::Render::validateOceanSurfaceSettings(frame.oceanSurface);
}
inline void decodeOceanOpticsInputs(Frame& frame) {
    auto required=[&](int offset) {
        const Word* found=nullptr;
        for(const auto& word:frame.words)if(word.offset==offset) {
            if(found)throw std::runtime_error("Duplicate current Ocean optics scalar");
            found=&word;
        }
        if(!found)throw std::runtime_error("Current Ocean optics scalar absent");
        float result;std::memcpy(&result,&found->bits,sizeof(result));return result;
    };
    frame.oceanOptics={required(0xb08),required(0xb0c),
        {required(0xb60),required(0xb64),required(0xb68),required(0xb6c)},
        {required(0xb70),required(0xb74),required(0xb78)},required(0xb80),required(0xb84)};
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
        l.shadowModes={read<uint8_t>(bytes,at+12),read<uint8_t>(bytes,at+13)};
        std::memcpy(l.data.data(),bytes.data()+at+16,l.data.size());at+=16+l.data.size();}
    std::memcpy(r.atmosphere.data(),bytes.data()+at,296);at+=296;
    r.exposure=read<ExposureLight>(bytes,at);at+=sizeof(ExposureLight);
    constexpr auto materialBytes=San14::Render::Interop::OceanTextureMaterialInputBytes;
    if(bytes.size()-at<materialBytes)throw std::runtime_error("Current Ocean texture material block absent");
    r.oceanTextureMaterial=San14::Render::Interop::decodeOceanTextureMaterialSettings(bytes.subspan(at,materialBytes));
    at+=materialBytes;
    constexpr auto bubbleBytes=San14::Render::Interop::OceanBubbleMaterialInputBytes;
    if(bytes.size()-at<bubbleBytes)throw std::runtime_error("Current Ocean bubble material block absent");
    r.oceanBubbleMaterial=San14::Render::Interop::decodeOceanBubbleMaterialSettings(bytes.subspan(at,bubbleBytes));
    at+=bubbleBytes;
    constexpr auto commonBytes=San14::Render::Interop::CommonRenderPolicyInputBytes;
    if(bytes.size()-at<commonBytes)throw std::runtime_error("Current Common render policy block absent");
    r.commonRenderPolicy=San14::Render::Interop::decodeCommonRenderPolicy(bytes.subspan(at,commonBytes));
    at+=commonBytes;
    constexpr auto heightBytes=San14::Render::Interop::HeightFogInputBytes;
    if(bytes.size()-at<heightBytes)throw std::runtime_error("Current Height Fog block absent");
    r.heightFog=San14::Render::Interop::decodeHeightFogSettings(bytes.subspan(at,heightBytes));
    at+=heightBytes;
    if (at < bytes.size()) {
        std::array<San14::Render::HdrLightModifier, 5> modifiers;
        if (bytes.size() - at < San14::Render::Interop::HdrLightingInputBytes)
            throw std::runtime_error("Current HDR lighting packet is truncated");
        r.hdrLighting = San14::Render::Interop::decodeHdrLightingInputs(
            bytes.subspan(at, San14::Render::Interop::HdrLightingInputBytes), modifiers);
        for (std::size_t i = 0; i < r.lights.size(); ++i)
            r.lights[i].lighting = San14::Render::Interop::decodeHdrLight(r.lights[i].data, modifiers[i]);
        at += San14::Render::Interop::HdrLightingInputBytes;
        if (at < bytes.size()) {
            if (bytes.size() - at < 20) throw std::runtime_error("Current HDR selection settings are truncated");
            r.hdrSelection = San14::Render::Interop::decodeHdrSelectionSettings(bytes.subspan(at,20));
            at += 20;
            if (bytes.size() - at != 44 || read<std::uint32_t>(bytes,at) != 0x31434F48u)
                throw std::runtime_error("Current HDR occlusion rates format");
            at += 4;
            for (auto& light : r.lights) {
                light.lighting.modifier.occlusionRates = read<std::array<float,2>>(bytes,at);
                at += 8;
            }
        }
    }
    if (at != bytes.size()) throw std::runtime_error("Current HDR lighting packet tail");
    decodeShadowInputs(r);
    decodeCloudWeatherInputs(r);
    r.oceanLightGroup=importedMainField<std::uint32_t>(r,0xac0);
    decodeOceanWaveInputs(r);
    decodeOceanSurfaceInputs(r);
    decodeOceanOpticsInputs(r);
    decodePostInputs(r);
    return r;
}
// A current season owns its group declaration. Only a caller without a season
// uses the initial View camera setting; missing current data is not absence.
inline std::uint32_t currentOceanLightGroup(const Frame* current,std::uint32_t initialGroup) {
    if(!current)return initialGroup;
    if(!current->oceanLightGroup)throw std::runtime_error("Current Ocean light group is absent");
    return *current->oceanLightGroup;
}
// World selection belongs to its ordinary owner. This is the original ordered
// first-intersecting group query over this same five-record publication.
inline const Light* firstMatching(const Frame& frame,uint32_t queryMask){
    for(const auto& l:frame.lights)if(l.mask&queryMask)return &l;return nullptr;
}
} // namespace source_current_season
