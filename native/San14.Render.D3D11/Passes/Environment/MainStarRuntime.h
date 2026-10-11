#pragma once
#include "MainStarInputs.h"
#include "MainStarMaterialState.h"
#include "StarTwinklePhase.h"
#include "WeatherFogFrame.h"
#include <algorithm>
#include <cmath>
#include <limits>
#include <optional>
#include <vector>

namespace San14::Render {
namespace main_star_math {
using source_camera::A;using source_camera::S;using source_camera::M;using source_camera::D;
using source_camera::V3;using source_camera::V4;
inline float bits(std::uint32_t raw) {float value;std::memcpy(&value,&raw,4);return value;}
inline V3 add(V3 a,V3 b) {return {A(a.X,b.X),A(a.Y,b.Y),A(a.Z,b.Z)};}
inline V3 subtract(V3 a,V3 b) {return {S(a.X,b.X),S(a.Y,b.Y),S(a.Z,b.Z)};}
inline V3 scale(V3 value,float scalar) {return {M(value.X,scalar),M(value.Y,scalar),M(value.Z,scalar)};}
inline float dot(V3 a,V3 b) {return A(A(M(a.X,b.X),M(a.Y,b.Y)),M(a.Z,b.Z));}
inline float length(V3 value) {return std::sqrt(dot(value,value));}
inline V3 xyz(V4 value) {return {value.X,value.Y,value.Z};}
inline V4 transform(V4 value,const source_camera::Matrix& matrix) {
    V4 result;float* output=&result.X;
    for(unsigned column=0;column<4;++column)
        output[column]=A(A(A(M(value.X,matrix[column]),M(value.Y,matrix[4+column])),
            M(value.Z,matrix[8+column])),M(value.W,matrix[12+column]));
    return result;
}
inline V3 direction(float angleA,float angleB) {
    const auto a=source_camera::sinCos(angleA),b=source_camera::sinCos(angleB);
    return {M(a[0],b[1]),b[0],M(a[1],b[1])};
}
inline std::uint32_t capacity(float value) {
    if(!std::isfinite(value)||value<0||double(value)>double(std::numeric_limits<std::uint32_t>::max()/32))
        throw std::runtime_error("Main Star shower capacity is outside the supported buffer range");
    return static_cast<std::uint32_t>(std::trunc(value));
}
} // namespace main_star_math

struct MainStarShowerVertex {
    source_camera::V3 current{},previous{};
    float intensity{};
    std::uint32_t color{};
};
static_assert(sizeof(MainStarShowerVertex)==32);
struct MainStarShowerParticle {
    enum class State { Inactive, NewlyEmitted, Moving };
    State state=State::Inactive;
    source_camera::V3 position{},velocity{};
    float cameraW{},age{},inner{},innerFade{},outerFade{},outer{};
};
struct MainStarShowerConfiguration {
    float angleA{},angleB{},lifetime{};
    source_camera::V3 baseVelocity{};
    std::vector<MainStarShowerParticle> particles;
    std::vector<MainStarShowerVertex> output;
};

class MainStarRuntime {
public:
    void bindTwinklePhase(StarTwinklePhase& phase) {phase_=&phase;}
    float phase() const {return phase_->value;}
    float emissionAccumulator() const {return emissionAccumulator_;}
    const std::vector<MainStarShowerVertex>& output() const {return configuration_.output;}
    std::size_t showerCapacity() const {return configuration_.particles.size();}
    bool outputDirty() const {return dirty_;}
    void acceptUpload() {dirty_=false;}
    const MainStarCurrentInputs& inputs() const {return inputs_;}

    std::uint32_t expectedCount(const MainStarSettings& settings) const {
        using namespace main_star_math;
        const float value=M(D(shellSpan(),M(settings.showerSpeed,1000.f)),settings.meteorIncidents);
        return capacity(value)+1;
    }
    std::optional<MainStarShowerConfiguration> prepareConfiguration(const MainStarSettings& settings) const {
        using namespace main_star_math;
        const float radians=bits(0x3c8efa35u);
        const float angleA=M(settings.peakRightAscension,radians),angleB=M(settings.peakDeclination,radians);
        const auto expected=expectedCount(settings);
        if(configured_&&configuration_.angleA==angleA&&configuration_.angleB==angleB&&
            configuration_.particles.size()==expected)return std::nullopt;
        const float epsilon=bits(0x37480000u);
        float magnitude=M(settings.showerSpeed,1000.f);if(magnitude<epsilon)magnitude=epsilon;
        MainStarShowerConfiguration result;
        result.angleA=angleA;result.angleB=angleB;result.lifetime=settings.meteorIncidents;
        result.baseVelocity=scale(direction(angleA,angleB),-magnitude);
        const auto count=capacity(A(M(D(shellSpan(),magnitude),settings.meteorIncidents),1.f));
        result.particles.resize(count);result.output.resize(count);
        for(auto& particle:result.particles) {
            particle.inner=innerRadius();particle.innerFade=innerFadeRadius();
            particle.outerFade=outerFadeRadius();particle.outer=outerRadius();
        }
        // A project-owned fresh output is explicitly invisible until generated.
        // This does not assume the source allocator cleared its raw payload.
        for(auto& vertex:result.output)vertex.intensity=bits(0x00800000u);
        return result;
    }
    void commitConfiguration(MainStarShowerConfiguration configuration) {
        configuration_=std::move(configuration);configured_=true;randomState_=0;dirty_=true;
        // Buffer reconstruction does not reset the phase, emission accumulator
        // or prior transformed camera; those belong to the retained runtime.
    }
    void updateNode3(const MainStarCurrentInputs& inputs,float delta,
        const source_camera::CameraTransform& raisedCamera,float controllerHeightTerm,
        bool updateOrientation,const source_weather_fog::Power& power) {
        using namespace main_star_math;
        inputs_=inputs;controllerDelta_=M(inputs.settings.blinkSpeed,delta);
        if(expectedCount(inputs.settings)<=1||!(delta>0))return;
        if(!configured_)throw std::runtime_error("Main Star shower update has no configured pool");
        if(updateOrientation)
            for(unsigned row=0;row<4;++row)for(unsigned column=0;column<4;++column)
                generationOrientation_[row*4+column]=inputs.controllerMatrix[column*4+row];
        const float height=A(A(radius(),M(raisedCamera.eye.Y,.01f)),controllerHeightTerm);
        const V4 camera=transform({M(raisedCamera.eye.X,.01f),height,M(raisedCamera.eye.Z,.01f),raisedCamera.eye.W},generationOrientation_);
        const float inverseHeight=D(1.f,height),horizonDistance=std::sqrt(S(M(height,height),radiusSquared()));
        const float horizonCos=-M(horizonDistance,inverseHeight);
        const auto cameraOverHeight=scale(xyz(camera),inverseHeight);
        const auto ground=scale(cameraOverHeight,radius());
        emissionAccumulator_=A(emissionAccumulator_,delta);
        velocityToDelta_=D(source_weather_fog::powSse(2.f,inputs.settings.shutterSpeedScale,power),delta);
        float fraction=inputs.settings.meteorIncidentsFade;
        if(fraction<0)fraction=0;else if(fraction>1)fraction=1;
        if(fraction!=0) {
            emissionInterval_=D(1.f,M(fraction,configuration_.lifetime));
            const float requested=M(M(fraction,configuration_.lifetime),delta);
            auto emissions=capacity(requested);
            if(!emissions&&emissionAccumulator_>emissionInterval_) {
                emissions=1;emissionAccumulator_=0;
            }
            for(std::uint32_t emission=0;emission<emissions;++emission) {
                auto free=std::find_if(configuration_.particles.begin(),configuration_.particles.end(),
                    [](const auto& particle){return particle.state==MainStarShowerParticle::State::Inactive;});
                if(free==configuration_.particles.end())break;
                emit(*free,ground,cameraOverHeight,camera.W);
            }
        }
        for(std::size_t index=0;index<configuration_.particles.size();++index) {
            auto& particle=configuration_.particles[index];auto& vertex=configuration_.output[index];
            if(particle.state==MainStarShowerParticle::State::Inactive)continue;
            const auto relative=subtract(particle.position,xyz(camera));
            const float distance=length(relative),radial=length(particle.position);
            const float directionDot=dot(scale(relative,D(1.f,distance)),scale(xyz(camera),inverseHeight));
            if((directionDot<horizonCos&&distance>horizonDistance)||particle.age>configuration_.lifetime||
                radial>outerRadius()||radial<particle.inner) {
                particle.state=MainStarShowerParticle::State::Inactive;
                vertex={};vertex.intensity=bits(0x00800000u);continue;
            }
            if(particle.state==MainStarShowerParticle::State::NewlyEmitted) {
                vertex.current=vertex.previous=relative;particle.state=MainStarShowerParticle::State::Moving;
            }else {
                vertex.previous=subtract(add(xyz(previousCamera_),vertex.current),xyz(camera));
                const float gravity=-M(M(D(radius(),radial),D(radius(),radial)),9.8062f);
                particle.velocity=add(particle.velocity,scale(particle.position,D(gravity,radial)));
                particle.position=add(particle.position,scale(particle.velocity,delta));
                vertex.current=subtract(particle.position,xyz(camera));
            }
            float intensity=6.33607f;
            if(distance>200000.f) {const float ratio=D(200000.f,distance);intensity=M(intensity,M(ratio,ratio));}
            if(radial<particle.innerFade)intensity=M(intensity,D(S(radial,particle.inner),S(particle.innerFade,particle.inner)));
            else if(radial>particle.outerFade)intensity=M(intensity,D(S(particle.outer,radial),S(particle.outer,particle.outerFade)));
            vertex.intensity=intensity;
            vertex.color=((randomHigh()%255u)<<24)|0x00ffffffu;
            particle.age=A(particle.age,delta);
        }
        previousCamera_=camera;dirty_=true;
    }
    MainStarMaterialState prepareDraw(const source_camera::CameraTransform& raisedCamera,
        std::uint32_t viewportWidth,std::uint32_t viewportHeight,std::uint32_t controllerWidth,
        const std::array<source_camera::V4,16>& table,
        bool texturePresent,const source_weather_fog::Power& power) {
        using namespace main_star_math;
        phase_->advance(raisedCamera.eye.Y,controllerDelta_);
        const float size=M(inputs_.settings.diameterTexel,.5f);
        float angular=D(raisedCamera.optics.viewSpan,float(controllerWidth));angular=M(angular,angular);
        float intensity=M(source_weather_fog::powSse(10.f,bits(0xc10126eau),power),1.f);
        intensity=M(intensity,inputs_.controllerBrightness);intensity=M(intensity,bits(0x3fa2f983u));
        intensity=D(intensity,angular);intensity=D(intensity,bits(0x3e5edc7fu));
        intensity=M(intensity,D(256.f,M(size,size)));
        MainStarMaterialState result;
        result.eyeToClip=source_camera::compose(inputs_.controllerMatrix,raisedCamera.worldToClip);
        result.phase={phase_->value,16,3,0};result.twinkleTable=table;
        result.starInfo={intensity,size,D(bits(0x3b000000u),inputs_.settings.exposureScale),texturePresent?1.f:0.f};
        result.pointScale={D(1.f,float(viewportWidth)),D(1.f,float(viewportHeight)),0,0};
        result.showerInfo0={D(D(float(viewportHeight),raisedCamera.optics.viewSpan),inputs_.settings.diameterTexel),velocityToDelta_,0,0};
        result.showerInfo1=result.starInfo;
        return result;
    }
private:
    static float radius() {return main_star_math::bits(0x4ac21780u);}
    static float radiusSquared() {return main_star_math::bits(0x561327a0u);}
    static float outerRadius() {return main_star_math::A(radius(),150000.f);}
    static float outerFadeRadius() {return main_star_math::A(radius(),100000.f);}
    static float innerFadeRadius() {return main_star_math::A(radius(),70000.f);}
    static float innerRadius() {return main_star_math::A(radius(),50000.f);}
    static float shellSpan() {return std::sqrt(main_star_math::S(main_star_math::M(outerRadius(),outerRadius()),main_star_math::M(innerRadius(),innerRadius())));}
    std::uint32_t randomHigh() {randomState_=randomState_*0x10dcdu+1u;return randomState_>>16;}
    float uniform() {return main_star_math::D(float(randomHigh()),65535.f);}
    void emit(MainStarShowerParticle& particle,source_camera::V3 ground,
        source_camera::V3 cameraOverHeight,float cameraW) {
        using namespace main_star_math;
        const float pi=bits(0x40490fdbu),halfPi=bits(0x3fc90fdbu);
        const float angleA=A(configuration_.angleA,S(M(uniform(),pi),halfPi));
        const float angleB=A(configuration_.angleB,S(M(uniform(),pi),halfPi));
        const float outer=A(outerFadeRadius(),M(S(outerRadius(),outerFadeRadius()),uniform()));
        const float inner=A(innerRadius(),M(S(innerFadeRadius(),innerRadius()),uniform()));
        const auto launch=direction(angleA,angleB);
        const float projection=dot(launch,cameraOverHeight);
        const float distance=S(std::sqrt(A(M(M(radiusSquared(),projection),projection),
            S(M(outer,outer),radiusSquared()))),M(radius(),projection));
        particle.position=add(ground,scale(launch,distance));particle.cameraW=cameraW;
        particle.velocity=configuration_.baseVelocity;particle.age=0;
        particle.outer=outer;particle.outerFade=D(outer,1.00468f);
        particle.inner=inner;particle.innerFade=M(inner,1.00468f);
        particle.state=MainStarShowerParticle::State::NewlyEmitted;
    }
    MainStarCurrentInputs inputs_;
    MainStarShowerConfiguration configuration_;
    bool configured_{},dirty_{};
    std::uint32_t randomState_{};
    StarTwinklePhase* phase_{};
    float controllerDelta_{},emissionAccumulator_{},emissionInterval_{},velocityToDelta_{};
    source_camera::V4 previousCamera_{};
    source_camera::Matrix generationOrientation_{1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1};
};
} // namespace San14::Render
