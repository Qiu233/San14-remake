#pragma once
#include "RainDropInputs.h"
#include "../../../Camera/CameraTransform.h"
#include <algorithm>
#include <cmath>
#include <limits>
#include <memory>

namespace San14::Render::rain {
// E3C2B0's retained manager values. These clocks advance only at node4 of an
// accepted native Main traversal; queued managed camera proposals do not mutate them.
struct Wetness {
    float lastClock{},rippleFade{},rippleRate{},fadeOutStart{},slopeFlow{};
    float surfaceScale{1.05f},surfacePower{1.f};
    std::uint32_t transition{};
};
struct RainParameters {float density{},velocity{},size{},randomScale{};};
struct SnowParameters {float probability{},density{},velocity{},size{},randomScale{};};
inline RainParameters prepareRain(float power) {
    float size,random,density;
    if(power<=.2f){size=.05f;random=.4f;density=power*.01061033f/.2f;}
    else if(power>15.f){size=.15f;random=.6f;density=power*.029473137f/15.f;}
    else {
        const bool upper=power>3.f;
        const float p0=upper?3.f:.2f,p1=upper?15.f:3.f;
        const float s0=upper?.1f:.05f,s1=upper?.15f:.1f;
        const float r0=upper?.5f:.4f,r1=upper?.6f:.5f;
        const float a=s0/.1f,b=s1/.1f,w=(power-p0)/(p1-p0);
        density=(p1*.006631456f/(b*b*b)-p0*.006631456f/(a*a*a))*w+p0*.006631456f/(a*a*a);
        size=std::pow((power*.001f*.006631456f)/density,1.f/3.f);
        random=(r1-r0)*w+r0;
    }
    return {density/(random*random+1.f),std::min(std::sqrt(size/.1f)*650.f,900.f),size+size,random};
}
inline SnowParameters prepareSnow(float power,float temperature) {
    float size,random,density;
    if(power<=.5f){size=.5f;random=.4f;density=power*.0009146836f*2.f;}
    else if(power>=5.f){size=1.f;random=.5f;density=power*.0011433546f/5.f;}
    else {
        const float w=(power-.5f)/4.5f;
        density=(5.f*.0018293672f/8.f-.5f*.0018293672f)*w+.5f*.0018293672f;
        size=std::pow((power*.125f*.0018293672f)/density,1.f/3.f);
        random=.1f*w+.4f;
    }
    float multiplier,inverseCube;
    if(temperature<=-10.f){multiplier=.5f;inverseCube=8.f;}
    else if(temperature>=3.f){multiplier=1.f;inverseCube=1.f;}
    else {
        const bool upper=temperature>0.f;
        const float t0=upper?0.f:-10.f,t1=upper?3.f:0.f;
        const float s0=upper?.9f:.5f,s1=upper?1.f:.9f;
        inverseCube=(1.f/(s1*s1*s1)-1.f/(s0*s0*s0))*((temperature-t0)/(t1-t0))+1.f/(s0*s0*s0);
        multiplier=1.f/std::pow(inverseCube,1.f/3.f);
    }
    size=multiplier*size+multiplier*size;
    return {std::clamp(1.f-(temperature-3.f),0.f,1.f),inverseCube*density/(random*random+1.f),
        std::pow(size,0.17399999499321f)*150.9f,size,random};
}
inline std::uint32_t capacity(float value) {
    if(!std::isfinite(value)||value<0.f||double(value)>double(std::numeric_limits<std::uint32_t>::max()-63))
        throw std::runtime_error("Rain current particle capacity is outside its source allocation range");
    return static_cast<std::uint32_t>(value);
}
inline std::uint32_t rainCapacity(const Inputs& input,const RainParameters& rain) {
    return capacity((std::max(input.splashLifeSeconds,0.f)+std::abs(std::abs(input.heightRange)/rain.velocity))*
        input.drawRadius*input.drawRadius*rain.density);
}
inline std::uint32_t snowCapacity(const Inputs& input,const SnowParameters& snow) {
    return capacity(std::abs(std::abs(input.heightRange)/snow.velocity)*input.drawRadius*input.drawRadius*snow.density);
}
// The backend owns real native buffers. With no authored particle textures,
// nodes157-159 return before shader preparation and never read their contents.
class ParticleBackend {
    float radius_,top_,bottom_;
    std::uint32_t count_;
    source_camera::CameraTransform camera_{};
    bool hasCamera_{},cameraDirty_{};
    float pendingSeconds_{};
    ComPtr<ID3D11Buffer> particles_,vertices_,indirect_,collisionCount_;
    static ComPtr<ID3D11Buffer> raw(ID3D11Device* device,std::uint32_t bytes,UINT flags,UINT misc,
        const void* initial=nullptr) {
        D3D11_BUFFER_DESC desc{};desc.ByteWidth=bytes;desc.Usage=D3D11_USAGE_DEFAULT;
        desc.BindFlags=flags;desc.MiscFlags=misc;
        D3D11_SUBRESOURCE_DATA data{};data.pSysMem=initial;
        ComPtr<ID3D11Buffer> result;
        check(device->CreateBuffer(&desc,initial?&data:nullptr,&result),"Rain particle buffer");return result;
    }
public:
    ParticleBackend(ID3D11Device* device,const Inputs& input,std::uint32_t count)
        :radius_(input.drawRadius),top_(input.bottom+input.heightRange),bottom_(input.bottom),count_((count+63u)&~63u) {
        constexpr auto rawFlags=D3D11_BIND_SHADER_RESOURCE|D3D11_BIND_UNORDERED_ACCESS;
        const auto particleBytes=std::uint64_t(count_)*44u,vertexBytes=std::uint64_t(count_)*192u;
        if(particleBytes>std::numeric_limits<UINT>::max()||vertexBytes>std::numeric_limits<UINT>::max())throw std::runtime_error("Rain buffer byte extent");
        particles_=raw(device,UINT(particleBytes),rawFlags,D3D11_RESOURCE_MISC_BUFFER_ALLOW_RAW_VIEWS);
        vertices_=raw(device,UINT(vertexBytes),rawFlags|D3D11_BIND_VERTEX_BUFFER,D3D11_RESOURCE_MISC_BUFFER_ALLOW_RAW_VIEWS);
        const std::array<std::uint32_t,8> indirect{0,1,0,0,0,1,count_*6u,0};
        indirect_=raw(device,32,D3D11_BIND_UNORDERED_ACCESS,D3D11_RESOURCE_MISC_DRAWINDIRECT_ARGS,indirect.data());
        const std::uint32_t zero=0;collisionCount_=raw(device,4,D3D11_BIND_UNORDERED_ACCESS,0,&zero);
    }
    bool sameExtent(const Inputs& input)const{return radius_==input.drawRadius&&top_==input.bottom+input.heightRange&&bottom_==input.bottom;}
    void acceptCamera(const source_camera::CameraTransform& camera,float delta) {
        if(!hasCamera_||std::memcmp(&camera_,&camera,sizeof(camera))!=0)cameraDirty_=true;
        camera_=camera;hasCamera_=true;pendingSeconds_+=delta;
    }
    std::uint32_t count()const{return count_;}
    float pendingSeconds()const{return pendingSeconds_;}
};
class Manager {
    std::uint32_t provider_{},rainShader_{},snowShader_{};
    std::array<std::uint64_t,3> scopes_{};
    Wetness wetness_;
    std::unique_ptr<ParticleBackend> backend_;
    static float approach(float current,float target,float delta,float fadeIn,float fadeOut) {
        constexpr float epsilon=1.1920929e-5f;
        if(target<current)return std::max(0.f,current-delta/std::max(epsilon,fadeOut));
        if(target>current)return std::min(target,current+delta/std::max(epsilon,fadeIn));
        return current;
    }
public:
    void node4(ID3D11Device* device,const Inputs& input,const source_camera::CameraTransform& camera,
        std::uint32_t hostFlags,std::uint32_t rainGroups,float currentClock,float precipitation,float temperature,
        MainRenderPhaseState& phase) {
        if(provider_!=input.provider||rainShader_!=input.rainShader||snowShader_!=input.snowShader||scopes_!=input.scopes) {
            backend_.reset();wetness_={};provider_=input.provider;rainShader_=input.rainShader;snowShader_=input.snowShader;scopes_=input.scopes;
        }
        const unsigned mode=(hostFlags&3u)+1u;
        const float power=mode==2?precipitation:input.declaredPrecipitation;
        const float actualTemperature=mode==2?temperature:input.declaredTemperature;
        if(backend_&&!backend_->sameExtent(input))backend_.reset();
        if(!backend_&&input.drawRadius>0.f&&input.heightRange>0.f&&power>0.f&&input.group!=0) {
            std::uint32_t count=0;
            if(mode==2)count=std::max(rainCapacity(input,prepareRain(20.f)),snowCapacity(input,prepareSnow(20.f,-60.f)));
            else {
                if(input.textures.hasRainPair())count=rainCapacity(input,prepareRain(power));
                if(input.textures.snowFragment)count=std::max(count,snowCapacity(input,prepareSnow(power,actualTemperature)));
            }
            if(count&&mode>=1&&mode<=2)backend_=std::make_unique<ParticleBackend>(device,input,count);
        }
        const float delta=std::max(input.minimumInterval,currentClock-wetness_.lastClock);
        if(backend_){backend_->acceptCamera(camera,delta);phase.publishThinSurface(rainGroups!=0);}
        // C44ED0 derives both targets from this actual prepared weather frame.
        const float liquid=std::clamp(temperature*.2f,0.f,1.f);
        const float targetFade=(precipitation>0.f?1.f:0.f)*liquid;
        const float targetRate=std::clamp(precipitation/std::max(1.1920929e-5f,input.rippleMaximumPrecipitation),0.f,1.f)*liquid;
        const auto oldFade=wetness_.rippleFade;
        std::uint32_t transition=0;
        if(targetFade<oldFade){if((wetness_.transition&3u)!=2)wetness_.fadeOutStart=oldFade;transition=2;}
        else if(targetFade>oldFade)transition=1;
        wetness_.rippleFade=approach(oldFade,targetFade,delta,input.fadeInSeconds,input.fadeOutSeconds);
        wetness_.rippleRate=approach(wetness_.rippleRate,targetRate,delta,input.fadeInSeconds,input.fadeOutSeconds);
        wetness_.surfacePower=std::pow(std::clamp(1.f-wetness_.rippleFade,0.f,1.f),8.f);
        wetness_.surfaceScale=wetness_.surfacePower*1.05f;
        wetness_.slopeFlow=(input.rippleRateScale>0.f?wetness_.rippleRate/input.rippleRateScale:0.f)*input.rippleFlowSpeedSlope;
        wetness_.transition=(wetness_.transition&~3u)|transition;wetness_.lastClock=currentClock;
        phase.publishRainWetness(wetness_.rippleFade>0.f);
    }
    const Wetness& wetness()const{return wetness_;}
    bool hasBackend()const{return backend_!=nullptr;}
    // E9F980 returns before E3FD70/E45980 when both authored families are null.
    bool admitsParticleNode(const Inputs& input,unsigned node)const {
        if(node!=157&&node!=158&&node!=159)throw std::runtime_error("Rain source node");
        if(!input.textures.hasParticles())return false;
        if(node!=157&&!(input.commonFlags&1u))return false;
        if(!backend_)return false;
        if(node==159)return input.textures.snowFragment!=0;
        if(node==158)return input.textures.hasRainPair();
        return true;
    }
    void executeParticleNode(const Inputs& input,unsigned node) {
        if(!admitsParticleNode(input,node))return;
        // A nonempty authored family needs the particle compute/draw consumer.
        // Its absence is explicit; a resource override cannot become an empty successful draw.
        throw std::runtime_error("Rain nonempty particle compute/draw consumer is not installed");
    }
};
} // namespace San14::Render::rain
