#pragma once
// Current numeric input only. Include after the existing Sky/Cloud/Fog,
// Effect-camera and weather-consumer native owners. No device objects/history
// are made here; loaded shader/resource variants remain separate owners.
#include "SeasonFrame.h"
#include "AtmosphereCameraFrame.h"
#include "WeatherFogConstants.h"
#include "WeatherFogRecordReader.h"

namespace source_current_season_atmosphere {
using source_camera::A;using source_camera::S;using source_camera::M;using source_camera::D;
using source_camera::V3;using source_camera::V4;using source_camera::get;using source_camera::put;
using Shared=source_weather_fog::Shared;
using FogState=source_weather_fog::FogState;
using FogMode=source_weather_fog::FogMode;

inline const source_current_season::Light& selected(const source_current_season::Frame& current,uint32_t mask){
    auto light=source_current_season::firstMatching(current,mask);
    if(!light)throw std::runtime_error("Current atmosphere selected light is absent");return *light;
}
inline FogState fogRecord(const source_current_season::Light& light){
    // Decode only the consumed prefix. The source light retains its unread tail.
    return source_weather_fog::decodeFog(light.data.data()+0x150);
}
inline FogState packageFog(const source_current_season::Light& light,float fogBlend){
    auto result=fogRecord(light);
    if(result.mode==FogMode::Scattering&&fogBlend>0)
        result.blend=A(M(S(1,fogBlend),result.blend),fogBlend);
    return result;
}
inline Shared sharedMode7Profile2(const FogState& fog,V3 rgb,bool enabled=true){
    auto result=source_weather_fog::shared(fog,rgb);
    if(!enabled||fog.mode==FogMode::Inactive)
        result.scat={V4{0,0,0,fog.startDistance},V4{0,0,0,fog.endDistance},V4{},V4{0,0,0,1}};
    return result;
}
// The fresh Sky/Cloud context uses profile one when its current Fog is
// inactive. It still publishes mode-four scattering from the retained record;
// its ambient package is genuinely zero and has no hemisphere rows.
inline Shared sharedSkyCloud(const FogState& fog,V3 rgb) {
    if(fog.mode==FogMode::Scattering)return sharedMode7Profile2(fog,rgb);
    if(fog.mode!=FogMode::Inactive)
        throw std::runtime_error("Unsupported Sky/Cloud atmosphere mode");
    Shared result;result.color=fog.background;
    const float x=A(fog.mie.X,fog.rayleigh.X),y=A(fog.mie.Y,fog.rayleigh.Y),z=A(fog.mie.Z,fog.rayleigh.Z);
    const float bound=source_weather_fog::MinimumFogDistance;
    auto ratio=[&](float value){return value<-bound||value>bound?D(fog.density,value):value;};
    const float rx=M(ratio(x),rgb.X),ry=M(ratio(y),rgb.Y),rz=M(ratio(z),rgb.Z);
    const float conversion=D(-1.f,std::log(2.f));
    const float phase=M(M(S(1.f,fog.anisotropy),source_weather_fog::IsotropicMiePhase),S(1.f,fog.anisotropy));
    result.scat={
        V4{M(x,conversion),M(y,conversion),M(z,conversion),fog.background.W},
        V4{M(0.f,fog.planetRadius),M(0.f,fog.planetRadius),M(0.f,fog.planetRadius),0.f},
        V4{M(rx,M(fog.rayleigh.X,source_weather_fog::RayleighPhase)),
           M(ry,M(fog.rayleigh.Y,source_weather_fog::RayleighPhase)),
           M(rz,M(fog.rayleigh.Z,source_weather_fog::RayleighPhase)),M(fog.anisotropy,-2.f)},
        V4{M(rx,M(phase,fog.mie.X)),M(ry,M(phase,fog.mie.Y)),M(rz,M(phase,fog.mie.Z)),A(M(fog.anisotropy,fog.anisotropy),1.f)}};
    return result;
}
inline V3 lightRgb(const source_current_season::Light& light){return get<V3>(light.data.data()+0x10);}
inline Shared sharedLight(const source_current_season::Light& light,bool enabled=true){
    return sharedMode7Profile2(fogRecord(light),lightRgb(light),enabled);
}
// Original Cloud Sun/Moon inputs differ from Sky2's normalized/rotated shader
// lights. SunProjection cannot reconstruct rawSun byte-exactly. Supply the
// current managed Atmosphere.Controller.SunPosition and selected Camera614
// geometry40; no guessed inverse transform is used.
struct CloudLightInput {V4 rawSun;float geometry40;};
struct SeasonEnvironment {
    source_atmosphere_camera::CelestialEnvironment celestial;
    source_atmosphere_camera::DirectEnvironment direct;
    V3 sunRgb{},moonRgb{};
    source_camera::Matrix skyLights{};
    float opacityClamp{};
    float skyHeightOffset{};
    V4 moonFaceDirection{};
    float milkyIntensity{};
    V4 stableDimensions{};
};
inline SeasonEnvironment decodeEnvironment(const source_current_season::Frame& current) {
    SeasonEnvironment result;
    auto& celestial=result.celestial;
    const auto* fields=reinterpret_cast<const uint8_t*>(current.atmosphere.data());
    celestial.skyTransform=get<source_camera::Matrix>(fields);
    celestial.localToGlobal=get<source_camera::Matrix>(fields+64);
    celestial.sunProjection=get<V4>(fields+128);celestial.moonPosition=get<V4>(fields+144);celestial.altitude=get<V4>(fields+160);
    celestial.sunSize=get<float>(fields+180);celestial.moonSize=get<float>(fields+184);
    celestial.sunIntensity=get<float>(fields+188);celestial.moonIntensity=get<float>(fields+192);celestial.halfFovTangent=get<float>(fields+196);
    result.sunRgb=get<V3>(fields+200);result.moonRgb=get<V3>(fields+216);
    result.skyLights=get<source_camera::Matrix>(fields+232);
    result.skyHeightOffset=get<float>(fields+176);
    result.moonFaceDirection=get<V4>(fields+280);
    result.milkyIntensity=get<float>(fields+296);
    result.stableDimensions=get<V4>(fields+300);
    result.direct=source_atmosphere_camera::decodeDirectEnvironment(current.main);
    result.opacityClamp=get<float>(current.main.data()+0x278);
    return result;
}
inline void publishCloudLights(source_atmosphere_camera::Packet& packet,
    const SeasonEnvironment& environment,const CloudLightInput& input){
    const auto sunRgb=environment.sunRgb,moonRgb=environment.moonRgb;const auto rawMoon=environment.celestial.moonPosition;
    float sun=environment.celestial.sunIntensity,moon=environment.celestial.moonIntensity;
    if(sun>0&&M(input.rawSun.Y,-1)>source_weather_fog::F(0xbe19999a)){
        float intensity=A(A(M(moonRgb.Y,source_weather_fog::F(0x3eaaaaab)),M(moonRgb.X,source_weather_fog::F(0x3eaaaaab))),
            M(moonRgb.Z,source_weather_fog::F(0x3eaaaaab)));
        if(D(source_weather_fog::F(0x3b000000),input.geometry40)>intensity)moon=0;
    }
    put(packet.cloudPixel.data()+448,V4{M(sunRgb.X,sun),M(sunRgb.Y,sun),M(sunRgb.Z,sun),sun});
    // Actual Cloud11919E0 ctor230=BD75C28F is a retained loaded constant.
    float directionW=packet.profile.cloudSunDirectionW;
    put(packet.cloudPixel.data()+464,V4{M(input.rawSun.X,-1),M(input.rawSun.Y,-1),M(input.rawSun.Z,-1),directionW});
    put(packet.cloudPixel.data()+480,V4{M(moonRgb.X,moon),M(moonRgb.Y,moon),M(moonRgb.Z,moon),moon});
    put(packet.cloudPixel.data()+496,V4{M(rawMoon.X,-1),M(rawMoon.Y,-1),M(rawMoon.Z,-1),0});
}
inline Shared publishPacket(source_atmosphere_camera::Packet& packet,
    const source_current_season::Frame& current,const CloudLightInput* cloudLights=nullptr){
    const auto environment=decodeEnvironment(current);
    packet.environment=environment.celestial;packet.direct=environment.direct;
    const auto& profile=packet.environment;
    // Four current light fields in the sky shader layout.
    std::memcpy(&packet.sky.sunRgb,environment.skyLights.data(),16);
    std::memcpy(&packet.sky.sunDirection,environment.skyLights.data()+4,16);
    std::memcpy(&packet.sky.moonRgb,environment.skyLights.data()+8,16);
    std::memcpy(&packet.sky.moonDirection,environment.skyLights.data()+12,16);
    packet.sky.altitudeInfo=profile.altitude;
    packet.sky.stableDimensions=environment.stableDimensions;
    packet.sky.skyInfo1.Z=packet.milkyTexturePresent?environment.milkyIntensity:0.f;
    packet.skyPolicy=current.skyPolicy;
    const auto& global=selected(current,0x80);
    auto fog=packageFog(global,packet.direct.fogBlend);
    auto shared=sharedSkyCloud(fog,lightRgb(global));
    packet.sky.fog=shared.fog;packet.sky.scattering=shared.scat;
    put(packet.cloudPixel.data(),get<V4>(global.data.data()));
    put(packet.cloudPixel.data()+32,shared.fog);put(packet.cloudPixel.data()+48,shared.scat);
    V4 package=get<V4>(global.data.data()),moon=profile.moonPosition;
    packet.sky.skyInfo1.W=package.X==moon.X&&package.Y==moon.Y&&package.Z==moon.Z&&package.W==moon.W?1.f:0.f;
    float clamp=M(environment.opacityClamp,100);
    packet.sky.clampValue.W=M(clamp,.01f);put(packet.cloudPixel.data()+556,clamp);
    if(cloudLights)publishCloudLights(packet,environment,*cloudLights);
    // Each Fog draw selects and publishes its own current local light through
    // the Height Fog program owner. Global weather remains Sky/Cloud input.
    return shared;
}
inline void applyEffectFog(const source_weather_fog_consumers::Route& route,
    const Shared& fields,std::span<uint8_t> bytes){
    for(const auto& write:route.writes){const void* data=nullptr;size_t length=0;
        if(write.name=="fog"){data=&fields.fog;length=16;}
        else if(write.name=="cFog"){data=&fields.color;length=16;}
        else if(write.name=="Scat"){data=fields.scat.data();length=64;}
        if(!data||write.size!=length||write.offset+length>bytes.size())
            throw std::runtime_error("Current effect Fog actual named layout changed");
        std::memcpy(bytes.data()+write.offset,data,length);
    }
}
} // namespace source_current_season_atmosphere

#ifndef SOURCE_CURRENT_SEASON_ATMOSPHERE_CPU_ONLY
static void publishNativeCurrentSeasonAtmosphere(NativeAtmosphereCamera& owner,NativeMainCloud& cloud,
    const source_current_season::Frame& current,
    const source_current_season_atmosphere::CloudLightInput* currentCloudLights=nullptr){
    source_current_season_atmosphere::publishPacket(owner.packet,current,currentCloudLights);
    for(auto& pass:cloud.passes)if(pass.name=="pass1"){
        // Normal camera writer follows, then Cloud execute repacks its owned
        // phases/Bayer/previousW2P and uploads. Preserve those retained fields.
        std::memcpy(pass.pbytes.data(),owner.packet.cloudPixel.data(),16);
        std::memcpy(pass.pbytes.data()+32,owner.packet.cloudPixel.data()+32,80);
        if(currentCloudLights)std::memcpy(pass.pbytes.data()+448,owner.packet.cloudPixel.data()+448,64);
        std::memcpy(pass.pbytes.data()+556,owner.packet.cloudPixel.data()+556,4);
    }
}
// Existing source consumer receipt says retained View1B20 L80 for all the
// admitted Effect7/22/125 routes; H04 modifies only vColorScale. Fetch the
// current L80 raw Fog96/radiance, not the route's old imported actualRgb.
// This updates those three named fields only, not other current light fields
// or another particle/program cohort. Existing route RDEF belongs to load.
static void updateNativeCurrentSeasonEffectFog(ID3D11DeviceContext* context,
    const NativeWeatherFogConsumers& routes,const source_current_season::Frame& current,
    NativeEffectCameraContinuous& camera,NativeEffectInputs& retained){
    auto fields=source_current_season_atmosphere::sharedLight(source_current_season_atmosphere::selected(current,0x80));
    for(auto& constant:camera.constants)for(const auto& route:routes)
        if(route.family=="effect"&&std::to_string(constant.pass)==route.identity&&
            constant.vertex==route.vertex&&constant.slot==route.slot&&!route.writes.empty()){
            source_current_season_atmosphere::applyEffectFog(route,fields,constant.bytes);
            auto& pass=retained.passes.at(constant.pass);
            auto buffer=std::find_if(pass.constants.begin(),pass.constants.end(),[&](const auto& cb){return cb.vertex==route.vertex&&cb.slot==route.slot;});
            if(buffer==pass.constants.end())throw std::runtime_error("Current Effect Fog retained CB slot absent");
            context->UpdateSubresource(buffer->buffer.Get(),0,nullptr,constant.bytes.data(),0,0);
        }
}
#endif
