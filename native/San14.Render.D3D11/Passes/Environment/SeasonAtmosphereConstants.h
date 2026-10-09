#pragma once
// Current numeric input only. Include after the existing Sky/Cloud/Fog,
// Effect-camera and weather-consumer native owners. No device objects/history
// are made here; loaded shader/resource variants remain separate owners.
#include "SeasonFrame.h"
#include "AtmosphereCameraFrame.h"
#include "WeatherFogConstants.h"

namespace source_current_season_atmosphere {
using source_camera::A;using source_camera::S;using source_camera::M;using source_camera::D;
using source_camera::V3;using source_camera::V4;using source_camera::get;using source_camera::put;
using Shared=source_weather_fog::Shared;
using Fog96=std::array<uint8_t,96>;

inline const source_current_season::Light& selected(const source_current_season::Frame& current,uint32_t mask){
    auto light=source_current_season::firstMatching(current,mask);
    if(!light)throw std::runtime_error("Current atmosphere selected light is absent");return *light;
}
inline Fog96 fogRecord(const source_current_season::Light& light){
    Fog96 r;std::memcpy(r.data(),light.data.data()+0x150,r.size());return r;
}
// D3AF58 changes only full selected package Fog44, only source mode7 and
// Main1EC>0. This is not a reinterpretation of weather72 as every light.
inline Fog96 packageFog(const source_current_season::Light& light,float main1ec){
    auto r=fogRecord(light);
    if(get<uint32_t>(r.data()+0x10)==7&&main1ec>0)
        put(r.data()+0x44,A(M(S(1,main1ec),get<float>(r.data()+0x44)),main1ec));
    return r;
}
// Same OriginalSkyFogMath.Generate(7,2,enabled,full96) +97CCE0 and its
// inactive 97C64E neutral64 branch. First72 are the proven common arithmetic
// fields; the source mode and full96 owner select the original branch.
inline Shared sharedMode7Profile2(const Fog96& r,V3 rgb,bool enabled=true){
    std::array<uint8_t,72> fields;std::memcpy(fields.data(),r.data(),fields.size());
    auto result=source_weather_fog::shared(fields,rgb);
    if(!enabled||get<uint32_t>(r.data()+0x10)==0)
        result.scat={V4{0,0,0,get<float>(r.data()+0x14)},V4{0,0,0,get<float>(r.data()+0x18)},V4{},V4{0,0,0,1}};
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
inline void publishCloudLights(source_atmosphere_camera::Packet& packet,
    const source_current_season::Frame& current,const CloudLightInput& input){
    const auto* p=reinterpret_cast<const uint8_t*>(current.atmosphere.data());
    V3 sunRgb=get<V3>(p+200),moonRgb=get<V3>(p+216);V4 rawMoon=get<V4>(p+144);
    float sun=get<float>(p+188),moon=get<float>(p+192);
    if(sun>0&&M(input.rawSun.Y,-1)>source_weather_fog::F(0xbe19999a)){
        float intensity=A(A(M(moonRgb.Y,source_weather_fog::F(0x3eaaaaab)),M(moonRgb.X,source_weather_fog::F(0x3eaaaaab))),
            M(moonRgb.Z,source_weather_fog::F(0x3eaaaaab)));
        if(D(source_weather_fog::F(0x3b000000),input.geometry40)>intensity)moon=0;
    }
    put(packet.cloudPixel.data()+448,V4{M(sunRgb.X,sun),M(sunRgb.Y,sun),M(sunRgb.Z,sun),sun});
    // Actual Cloud11919E0 ctor230=BD75C28F is a retained loaded constant.
    float directionW=get<float>(packet.cloudPixel.data()+476);
    put(packet.cloudPixel.data()+464,V4{M(input.rawSun.X,-1),M(input.rawSun.Y,-1),M(input.rawSun.Z,-1),directionW});
    put(packet.cloudPixel.data()+480,V4{M(moonRgb.X,moon),M(moonRgb.Y,moon),M(moonRgb.Z,moon),moon});
    put(packet.cloudPixel.data()+496,V4{M(rawMoon.X,-1),M(rawMoon.Y,-1),M(rawMoon.Z,-1),0});
}
inline Shared publishPacket(source_atmosphere_camera::Packet& packet,
    const source_current_season::Frame& current,const CloudLightInput* cloudLights=nullptr){
    auto& profile=packet.profile;profile.preparedMain=current.main;
    const auto* p=reinterpret_cast<const uint8_t*>(current.atmosphere.data());
    std::memcpy(profile.skyTransform.data(),p,64);std::memcpy(profile.localToGlobal.data(),p+64,64);
    profile.sunProjection=get<V4>(p+128);profile.moonPosition=get<V4>(p+144);profile.altitude=get<V4>(p+160);
    profile.skyYOffset=get<float>(p+176);profile.sunSize=get<float>(p+180);profile.moonSize=get<float>(p+184);
    profile.skyInfoZ=get<float>(p+188);profile.skyInfoW=get<float>(p+192);profile.halfFovTangent=get<float>(p+196);
    // MainSky VS2/PS14/15 actual RDEF: four direct current light fields.
    std::memcpy(packet.skyPixel.data()+128,p+232,64);put(packet.skyPixel.data()+288,profile.altitude);
    const auto& global=selected(current,0x80);
    auto fog=packageFog(global,get<float>(current.main.data()+0x1ec));
    if(get<uint32_t>(fog.data()+0x10)!=7)
        throw std::runtime_error("Current Sky renderer mode4 needs its original selected mode4 producer/program");
    auto shared=sharedMode7Profile2(fog,lightRgb(global));
    put(packet.skyPixel.data(),shared.fog);put(packet.skyPixel.data()+16,shared.scat);
    put(packet.cloudPixel.data(),get<V4>(global.data.data()));
    put(packet.cloudPixel.data()+32,shared.fog);put(packet.cloudPixel.data()+48,shared.scat);
    V4 package=get<V4>(global.data.data()),moon=profile.moonPosition;
    put(packet.skyPixel.data()+124,package.X==moon.X&&package.Y==moon.Y&&package.Z==moon.Z&&package.W==moon.W?1.f:0.f);
    float clamp=M(get<float>(current.main.data()+0x278),100);
    put(packet.skyPixel.data()+332,M(clamp,.01f));put(packet.cloudPixel.data()+556,clamp);
    if(cloudLights)publishCloudLights(packet,current,*cloudLights);
    // Existing SSFog125 imports are raw mode0 feature0/PS64. Their two local
    // groups retain their own inactive Fog96; do not publish global weather.
    // An active mode7 record requires another actual SSFog program/resource
    // installation. This helper intentionally does not invent that selection.
    for(uint32_t mask:{2u,8u})if(get<uint32_t>(selected(current,mask).data.data()+0x160)==7)
        throw std::runtime_error("Current SSFog active mode7 requires its selected program/resources");
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
