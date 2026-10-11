#pragma once
// Height layer inputs, current camera and local light are composed only after
// selecting the imported program. This publication never advances a clock.
#include "ScreenSpaceFogLayer.h"
#include "../../Camera/CameraState.h"
#include "HeightFogInputs.h"
#include "HeightFogProgramInputs.h"
namespace source_current_height_fog {
using namespace source_camera;
// Hourly reduction retains float32 arithmetic and signed double fractional
// reduction. Phase belongs to the current layer producer, not GPU resources.
inline float phase(float velocity,float clock) {
    auto fraction=[](float value){return float(double(value)-std::trunc(double(value)));};
    float cycles=std::floor(D(clock,3600.f));
    float local=S(clock,M(cycles,3600.f));
    float hourlyFraction=fraction(M(3600.f,velocity));
    return fraction(A(M(velocity,local),M(hourlyFraction,cycles)));
}
struct Layer {int type=-1;V4 color{},parameter{};std::array<float,20> values{},velocities{};};
struct Frame {std::array<Layer,3> layers;V4 mask{};};
inline Frame prepareCurrent(const San14::Render::HeightFogSettings& settings,float ordinaryEyeZ) {
    Frame result;
    for(unsigned layer=0;layer<3;++layer) {
        const auto& input=settings.layers[layer];auto& value=result.layers[layer];
        value.type=source_current_ssfog::layerType(input.mode);
        if(value.type<0)continue;
        float start=input.heightStart,end=input.heightEnd;
        if(int32_t(input.mode)<0){start=A(start,ordinaryEyeZ);end=A(end,ordinaryEyeZ);}
        value.color={input.color[0],input.color[1],input.color[2],input.color[3]};
        float x=D(1,S(end,start)),z=0,w=1;
        if(input.distanceFadeEnabled!=0&&input.distanceEnd>=input.distanceStart&&input.distanceStart!=input.distanceEnd) {
            z=D(1,S(input.distanceEnd,input.distanceStart));w=flip(M(z,input.distanceStart));
        }
        value.parameter={x,flip(M(x,start)),z,w};
        for(unsigned wave=0;wave<4;++wave) {
            const auto& source=input.waves[wave];unsigned index=wave*5;
            value.values[index]=source.amplitude;value.values[index+1]=source.frequencyX;
            value.velocities[index+2]=M(input.velocity,source.velocityX);
            value.values[index+3]=source.frequencyY;
            value.velocities[index+4]=M(input.velocity,source.velocityY);
        }
    }
    float x=D(1,settings.mask[0]),y=D(1,settings.mask[1]);
    result.mask={x,y,flip(M(settings.mask[2],x)),flip(M(settings.mask[3],y))};return result;
}
inline source_height_fog_programs::Key selection(const Frame& fields,bool background,uint32_t rawLightMode) {
    source_height_fog_programs::Key key;key.background=background?1u:0u;
    if(!background) {
        if(rawLightMode!=0&&rawLightMode!=7)throw std::runtime_error("Current Height Fog raw light mode is not supported");
        key.feature=rawLightMode==7?1u:0u;
    }
    for(size_t layer=0;layer<3;++layer)key.types[layer]=fields.layers[layer].type;
    return key;
}
struct Values {
    Frame height;
    V4 projection{},eye{},lightDirection{},lightColor{},fog{},fogColor{};
    Matrix inverseView{};
    std::array<V4,4> scattering{};
    std::array<float,60> fluctuations{};
    bool hasLocalLight=false;
};
inline float component(V4 value,unsigned index) {
    switch(index){case 0:return value.X;case 1:return value.Y;case 2:return value.Z;default:return value.W;}
}
inline float word(const Values& values,source_height_fog_programs::Semantic semantic,unsigned index) {
    using S=source_height_fog_programs::Semantic;
    switch(semantic) {
        case S::Projection:return component(values.projection,index);
        case S::InverseView:return values.inverseView[index];
        case S::Colors:return component(values.height.layers[index/4].color,index%4);
        case S::Parameters:return component(values.height.layers[index/4].parameter,index%4);
        case S::Fluctuation:return values.fluctuations[index];
        case S::Mask:return component(values.height.mask,index);
        case S::LightDirection:return component(values.lightDirection,index);
        case S::LightColor:return component(values.lightColor,index);
        case S::Eye:return component(values.eye,index);
        case S::Fog:return component(values.fog,index);
        case S::FogColor:return component(values.fogColor,index);
        case S::Scattering:return component(values.scattering[index/4],index%4);
    }
    throw std::runtime_error("Height Fog field has no current producer");
}
inline void publishCurrent(const source_height_fog_programs::StageLayout& layout,
    const Values& values,std::span<char> bytes) {
    if(bytes.size()!=layout.bytes)throw std::runtime_error("Height Fog selected constant extent");
    using S=source_height_fog_programs::Semantic;
    for(const auto& field:layout.fields) {
        // Without a season packet, the real initial light values remain in
        // their imported constants. Camera and height values remain current.
        if(!values.hasLocalLight&&(field.semantic==S::LightDirection||field.semantic==S::LightColor||
            field.semantic==S::Fog||field.semantic==S::FogColor||field.semantic==S::Scattering))continue;
        for(uint32_t element=0;element<field.elements;++element)for(uint32_t lane=0;lane<field.words;++lane) {
            float value=word(values,field.semantic,element*field.words+lane);
            std::memcpy(bytes.data()+field.offset+element*field.stride+lane*4,&value,4);
        }
    }
}
} // namespace source_current_height_fog
#ifndef SOURCE_CURRENT_HEIGHT_FOG_CPU_ONLY
static void publishNativeCurrentHeightFog(ID3D11Device* device,const source_current_season::Frame* current,
    NativeMapFog& fog,const source_atmosphere_camera::Frame& camera,float clock,std::optional<unsigned> viewFlags) {
    const auto& settings=current?current->heightFog:fog.initialHeight;
    auto fields=source_current_height_fog::prepareCurrent(settings,camera.fogEye.Z);
    struct Pending {
        size_t passIndex{};
        std::optional<NativeMapFogProgram> replacement;
        std::vector<std::vector<char>> constants;
    };
    std::vector<Pending> pending;
    for(size_t index=0;index<fog.passes.size();++index) {
        auto& pass=fog.passes[index];
        if(viewFlags&&!source_ssfog_population::admitted(pass.populationGate,*viewFlags))continue;
        const source_current_season::Light* light=nullptr;
        uint32_t mode=0;
        if(current&&!pass.background) {
            light=&source_current_season_atmosphere::selected(*current,pass.populationGate.recordMask78);
            mode=source_camera::get<uint32_t>(light->data.data()+0x160);
        }
        auto key=current?source_current_height_fog::selection(fields,pass.background,mode):
            (pass.program.description?pass.program.description->key:source_height_fog_programs::Key{});
        if(!current&&!pass.program.description)
            throw std::runtime_error("A newly admitted Height Fog channel needs its actual current light publication");
        const auto& description=fog.programs.select(key);
        Pending candidate;candidate.passIndex=index;
        if(!pass.program.description||!(pass.program.description->key==key))
            candidate.replacement=prepareNativeMapFogProgram(device,fog,pass,description);
        auto& program=candidate.replacement?*candidate.replacement:pass.program;
        source_current_height_fog::Values values;values.height=fields;
        values.projection=camera.fogProjection;values.inverseView=camera.fogInverseView;values.eye=camera.fogEye;
        if(light&&description.key.feature==1) {
            auto shared=source_current_season_atmosphere::sharedLight(*light);
            values.hasLocalLight=true;
            values.lightDirection=source_camera::get<source_camera::V4>(light->data.data());
            values.lightColor=source_camera::get<source_camera::V4>(light->data.data()+16);
            values.fog=shared.fog;values.fogColor=shared.color;values.scattering=shared.scat;
        }
        for(unsigned layer=0;layer<3;++layer)for(unsigned scalar=0;scalar<20;++scalar) {
            const auto& source=fields.layers[layer];
            values.fluctuations[layer*20+scalar]=scalar%5==2||scalar%5==4?
                source_current_height_fog::phase(source.velocities[scalar],clock):source.values[scalar];
        }
        for(const auto& constant:program.constants) {
            auto bytes=constant.bytes;
            source_current_height_fog::publishCurrent(*constant.description,values,bytes);
            candidate.constants.push_back(std::move(bytes));
        }
        pending.push_back(std::move(candidate));
    }
    // All admitted program creation and current field preparation succeeded.
    // Installing this candidate only moves owned resources and prepared bytes.
    for(auto& candidate:pending) {
        auto& pass=fog.passes[candidate.passIndex];
        if(candidate.replacement)pass.program=std::move(*candidate.replacement);
        for(size_t index=0;index<candidate.constants.size();++index)
            pass.program.constants[index].bytes=std::move(candidate.constants[index]);
    }
}
#endif
