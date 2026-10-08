#pragma once
// Current selected programs and actual F103/retained-L80 radiance are imported
// once. Only source named weather fields are written into the retained full CBs.
#include "weather-fog-camera-continuous-native.h"

namespace source_weather_fog_consumers {
struct Write {std::string name;unsigned offset{},size{};};
struct Route {std::string family,identity;bool vertex{};unsigned slot{};source_camera::V3 actualRgb{};std::vector<Write> writes;};
using Profile=std::vector<Route>;
inline Profile readProfile(const std::filesystem::path& source){
    std::ifstream input(source/"routes.txt");if(!input)throw std::runtime_error("Weather Fog actual consumer routes absent");
    Profile result;std::string family,identity,stage;unsigned slot,r,g,b,count;
    while(input>>family>>identity>>stage>>slot>>std::hex>>r>>g>>b>>std::dec>>count){
        Route route{family,identity,stage=="VS",slot,{source_weather_fog::F(r),source_weather_fog::F(g),source_weather_fog::F(b)}, {}};
        for(unsigned i=0;i<count;++i){Write w;input>>w.name>>w.offset>>w.size;route.writes.push_back(w);}
        if(!input)throw std::runtime_error("Weather Fog consumer field import incomplete");result.push_back(std::move(route));
    }return result;
}
inline void apply(const Route& route,const source_weather_fog::Frame& weather,uint8_t* bytes,size_t size){
    if(route.writes.empty())return;auto fields=source_weather_fog::shared(weather.main72,route.actualRgb);
    for(const auto& w:route.writes){const void* src=nullptr;unsigned required=0;
        if(w.name=="fog"){src=&fields.fog;required=16;}
        else if(w.name=="cFog"){src=&fields.color;required=16;}
        else if(w.name=="Scat"){src=fields.scat.data();required=64;}
        if(src==nullptr||w.size!=required||size_t(w.offset)+required>size)throw std::runtime_error("Weather Fog actual named CB layout mismatch");
        std::memcpy(bytes+w.offset,src,required);
    }
}
}

#ifndef SOURCE_WEATHER_FOG_CPU_ONLY
using NativeWeatherFogConsumers=source_weather_fog_consumers::Profile;
static NativeWeatherFogConsumers prepareNativeWeatherFogConsumers(const fs::path& source){return source_weather_fog_consumers::readProfile(source);}
static void updateNativeWeatherFogHdr(ID3D11DeviceContext* context,const NativeWeatherFogConsumers& profile,
    const source_weather_fog::Frame& weather,NativeSourceHdrPasses& retained){
    for(auto& pass:retained.passes)for(const auto& route:profile)if(route.family=="hdr"&&route.identity==pass.identity&&!route.writes.empty()){
        auto cb=std::find_if(pass.constants.begin(),pass.constants.end(),[&](const auto& c){return c.vertex==route.vertex&&c.slot==route.slot;});
        if(cb==pass.constants.end())throw std::runtime_error("Weather Fog retained HDR CB slot absent");
        source_weather_fog_consumers::apply(route,weather,reinterpret_cast<uint8_t*>(cb->bytes.data()),cb->bytes.size());
        context->UpdateSubresource(cb->buffer.Get(),0,nullptr,cb->bytes.data(),0,0);
    }
}
static void updateNativeWeatherFogEffects(ID3D11DeviceContext* context,const NativeWeatherFogConsumers& profile,
    const source_weather_fog::Frame& weather,NativeEffectCameraContinuous& camera,NativeEffectInputs& retained){
    for(auto& current:camera.constants)for(const auto& route:profile)if(route.family=="effect"&&std::to_string(current.pass)==route.identity
        &&current.vertex==route.vertex&&current.slot==route.slot&&!route.writes.empty()){
        source_weather_fog_consumers::apply(route,weather,current.bytes.data(),current.bytes.size());
        auto& pass=retained.passes.at(current.pass);
        auto cb=std::find_if(pass.constants.begin(),pass.constants.end(),[&](const auto& c){return c.vertex==route.vertex&&c.slot==route.slot;});
        if(cb==pass.constants.end())throw std::runtime_error("Weather Fog retained Effect CB slot absent");
        context->UpdateSubresource(cb->buffer.Get(),0,nullptr,current.bytes.data(),0,0);
    }
}
#endif
