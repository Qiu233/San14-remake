// CE source current Tree2 wind CPU producer; no D3D device or process access.
#pragma once
#include <array>
#include <bit>
#include <cstddef>
#include <cstdint>
#include <cstring>
#include <span>
#include <stdexcept>
#include <cmath>
#include <xmmintrin.h>
#include "../../Runtime/FrameClock.h"
#include "TreeWindEnvironment.h"

namespace tree2_source {
inline float add(float a,float b) { return _mm_cvtss_f32(_mm_add_ss(_mm_set_ss(a),_mm_set_ss(b))); }
inline float sub(float a,float b) { return _mm_cvtss_f32(_mm_sub_ss(_mm_set_ss(a),_mm_set_ss(b))); }
inline float mul(float a,float b) { return _mm_cvtss_f32(_mm_mul_ss(_mm_set_ss(a),_mm_set_ss(b))); }
inline float div(float a,float b) { return _mm_cvtss_f32(_mm_div_ss(_mm_set_ss(a),_mm_set_ss(b))); }
inline float sqr(float a) { return _mm_cvtss_f32(_mm_sqrt_ss(_mm_set_ss(a))); }
inline float read(std::span<const std::uint8_t> b,std::size_t at) {
    float v; std::memcpy(&v,b.data()+at,4);return v;
}
inline void write(std::span<std::uint8_t> b,std::size_t at,float v) { std::memcpy(b.data()+at,&v,4); }
inline void writeUint(std::span<std::uint8_t> b,std::size_t at,std::uint32_t v) { std::memcpy(b.data()+at,&v,4); }
inline float sin5(float a,bool positive) {
    const float q=mul(a,a), q2=mul(q,q);
    const float b=positive?add(mul(sub(6.f,q),20.f),q2):sub(mul(sub(q,6.f),20.f),q2);
    return mul(mul(b,a),0.008333333767950535f);
}
inline float cos6(float a,bool positive) {
    const float q=mul(a,a),q2plus=add(mul(q,q),360.f);
    const float b=positive?sub(mul(q2plus,sub(30.f,q)),10080.f):add(mul(q2plus,sub(q,30.f)),10080.f);
    return mul(b,0.0013888889225199819f);
}
// Scalar197420 sine uses the same014590 polynomial and range reduction.
inline float sourceSin(float angle) {
    if(angle==0.f)return 0.f;
    bool positive=angle>=0.f;float a=std::fabs(angle);
    while(a>=6.2831854820251465f)a=add(a,-6.2831854820251465f);
    if(a>=3.1415927410125732f){a=add(a,-3.1415927410125732f);positive=!positive;}
    if(a>=1.5707963705062866f)a=sub(3.1415927410125732f,a);
    if(a<0.668318510055542f)return sin5(a,positive);
    return cos6(sub(1.5707963705062866f,a),positive);
}
struct WorldRandom { // Shared World AB5310 initializer, retain across owners.
    std::uint32_t state=12345;
    std::uint32_t next16(){state=state*0x10DCDu+1u;return state>>16;}
};
struct WindGenerator {
    WindGeneratorParameters parameters;
    float phaseRadians{},elapsedSeconds{},restDuration{},targetStrength{},strength{};
    uint32_t stage{};
};
struct WindOwner {
    WindGenerator generator;
    std::array<float,2> phase{}; // D01580 explicit24F0 zero.
    std::array<float,4> direction{1.f,0.f,0.f,0.f}; //141179580.
};
struct CurrentWind {
    std::array<float,4> target{},direction{};
    float strength{},phase{},shaderScale{},renderTime{},deltaSeconds{};
    std::uint32_t randomState{},generatorStage{};
};
// Call at actual D12A80 wind-update occurrences (selectedView0 or6), with the
// accepted Main and the SAME Common clock publication used by the renderer.
// This scoped port implements the actual held DF4=DF8=0 branch. It retains all
// four AB54C0 scalar stages, sine and shared LCG even when amplitude is zero.
inline CurrentWind advance(WindOwner& owner,WorldRandom& random,
    const WindEnvironment& environment,const NativeFrameClockPublication& clock) {
    if(environment.firstJitter!=0.f||environment.secondJitter!=0.f)
        throw std::runtime_error("Positive Tree2 jitter requires actual seeded104A180 source");
    float x=environment.directionQuaternion[0],y=environment.directionQuaternion[1],z=environment.directionQuaternion[2],w=environment.directionQuaternion[3];
    const float a=add(sub(y,mul(z,0.f)),mul(w,0.f));
    const float b=add(sub(mul(z,0.f),x),mul(w,0.f));
    const float c=add(sub(mul(x,0.f),mul(y,0.f)),w);
    const float d=add(add(mul(x,0.f),mul(y,0.f)),z);
    std::array<float,4> target{
        add(add(mul(a,w),sub(mul(c,y),mul(b,z))),mul(d,x)),
        add(add(mul(b,w),sub(mul(a,z),mul(c,x))),mul(d,y)),
        add(add(mul(c,w),sub(mul(b,x),mul(a,y))),mul(d,z)),0.f};
    float reciprocal=div(1.f,sqr(add(add(mul(target[1],target[1]),mul(target[0],target[0])),mul(target[2],target[2]))));
    for(int i=0;i<3;++i)target[i]=mul(target[i],reciprocal);
    auto& g=owner.generator;
    g.parameters=environment.generatorParameters;
    const float lower=sub(g.parameters.baseStrength,mul(g.parameters.strengthVariation,.5f));
    g.parameters.minimumStrength=lower<=g.parameters.minimumStrength?lower:(0.f<=g.parameters.minimumStrength?g.parameters.minimumStrength:0.f);
    const float dt=clock.effective3350;
    float admitted=add(g.parameters.baseStrength,mul(g.parameters.strengthVariation,.5f));
    if(g.targetStrength<admitted){admitted=sub(g.parameters.baseStrength,mul(g.parameters.strengthVariation,.5f));if(admitted<g.targetStrength)admitted=g.targetStrength;}
    g.targetStrength=admitted;
    const auto stage=g.stage;bool wave=false;
    if(stage==0){
        g.strength=g.parameters.minimumStrength;g.elapsedSeconds=add(dt,g.elapsedSeconds);
        if(g.restDuration<g.elapsedSeconds){
            g.stage=1u;g.phaseRadians=0.f;
            g.targetStrength=add(mul(sub(.5f,div(float(random.next16()),65535.f)),g.parameters.strengthVariation),g.parameters.baseStrength);
        }
    }else if(stage==1){
        g.phaseRadians=add(div(mul(dt,3.1415927f),g.parameters.riseSeconds<=.01f?.01f:g.parameters.riseSeconds),g.phaseRadians);
        if(g.phaseRadians>3.1415927f){g.phaseRadians=3.1415927f;g.elapsedSeconds=0.f;g.stage=2u;}wave=true;
    }else if(stage==2){
        g.elapsedSeconds=add(dt,g.elapsedSeconds);if(g.parameters.peakSeconds<g.elapsedSeconds){g.elapsedSeconds=0.f;g.stage=3u;}g.strength=g.targetStrength;
    }else if(stage==3){
        g.phaseRadians=add(div(mul(dt,3.1415927f),g.parameters.fallSeconds<=.01f?.01f:g.parameters.fallSeconds),g.phaseRadians);
        if(g.phaseRadians>6.2831855f){
            g.stage=0;g.phaseRadians=g.elapsedSeconds=0.f;
            g.restDuration=add(mul(sub(.5f,div(float(random.next16()),65535.f)),g.parameters.restVariation),g.parameters.restSeconds);
        }wave=true;
    }
    if(wave)g.strength=add(mul(mul(add(sourceSin(sub(g.phaseRadians,1.5707964f)),1.f),sub(g.targetStrength,g.parameters.minimumStrength)),.5f),g.parameters.minimumStrength);
    if(g.strength<=0.f)g.strength=0.f;
    const float allScale=environment.strengthScale;
    float scaled=mul(mul(allScale,.01f),g.strength);
    float speed=sqr(scaled<30.f?(0.f<=scaled?scaled:0.f):30.f);
    if(speed>1.f){const float delta=sub(speed,1.f);speed=add(div(add(delta,delta),sub(sqr(30.f),1.f)),1.f);}
    owner.phase[0]=add(mul(mul(speed,dt),6.2831855f),owner.phase[0]);
    if(owner.phase[0]>3600.f){owner.phase[0]=sub(owner.phase[0],3600.f);owner.phase[1]=add(owner.phase[1],1.f);}
    constexpr float smoothing=std::bit_cast<float>(0x3C23D70Au);
    for(int i=0;i<4;++i)owner.direction[i]=add(mul(smoothing,target[i]),mul(sub(1.f,smoothing),owner.direction[i]));
    auto& sd=owner.direction;
    reciprocal=div(1.f,sqr(add(add(mul(sd[0],sd[0]),mul(sd[1],sd[1])),mul(sd[2],sd[2]))));
    for(int i=0;i<3;++i)sd[i]=mul(sd[i],reciprocal);
    return {target,sd,g.strength,add(mul(owner.phase[1],3600.f),owner.phase[0]),
        mul(sqr(mul(mul(g.strength,allScale),.01f)),.1f),clock.common254,dt,
        random.state,g.stage};
}
// BF58A0 actual renderer initializer. Resource admission is deliberately a
// caller input; this numeric history does not create any selected-View owner.
struct GraphicsHistory {
    std::array<float,4> interaction{},previousInteraction{};
    std::array<std::array<float,4>,16> wind{},previousWind{};
    std::uint32_t interactionSide=0;
    std::array<std::uint32_t,16> phaseSide{};
    float previousRenderTime=0.f;
};
struct WindAdmissionInputs {
    std::uint32_t selectedViewIndex{},commonFlags{};
    bool selectedViewPresent{},commonPresent{},cameraOrdinary{},shaderPresent{};
    bool pointOwnerBufferPresent{},spotOwnerBufferPresent{},areaOwnerBufferPresent{};
    bool windTexturesCreated{},selectorPhasePairCreated{};
    float windExtent{};
};
inline bool windAdmitted(const WindAdmissionInputs& a) {
    return a.selectedViewIndex<32&&a.selectedViewPresent&&a.commonPresent&&(a.commonFlags&0x2600)!=0
        &&a.cameraOrdinary&&a.windExtent>0.f&&a.pointOwnerBufferPresent&&a.spotOwnerBufferPresent
        &&a.areaOwnerBufferPresent&&a.shaderPresent&&a.windTexturesCreated&&a.selectorPhasePairCreated;
}
struct InteractionAdmissionInputs {
    std::uint32_t selectedViewIndex{},commonFlags{};
    bool selectedViewPresent{},commonPresent{},cameraOrdinary{},shaderPresent{};
    bool ownerBufferPresent{},resourcePairCreated{};
    float interactionExtent{};
};
inline bool interactionAdmitted(const InteractionAdmissionInputs& a) {
    return a.selectedViewIndex<32&&a.selectedViewPresent&&a.commonPresent&&(a.commonFlags&0x200000)!=0
        &&a.cameraOrdinary&&a.interactionExtent>0.f&&a.ownerBufferPresent&&a.shaderPresent&&a.resourcePairCreated;
}
inline std::int32_t cvtt(float x) { return _mm_cvttss_si32(_mm_set_ss(x)); }
inline float quantize(float position,float span,std::uint32_t resolution) {
    const auto step=std::bit_cast<std::int32_t>(std::uint32_t(cvtt(span))/resolution);
    if(step<=0)return 0.f;
    return float(cvtt(position)/step*step);
}
inline void applyAdmittedWindows(GraphicsHistory& graphics,const WindEnvironment& environment,
    std::uint32_t selector,bool interactionAdmitted,bool windAdmitted) {
    if(selector>=16)throw std::runtime_error("Tree2 wind selector range");
    if(interactionAdmitted){
        graphics.previousInteraction=graphics.interaction;
        const float span=environment.interactionExtent,half=mul(span,.5f);
        graphics.interaction={quantize(sub(environment.cameraPosition[0],half),span,512),0.f,
            quantize(sub(environment.cameraPosition[2],half),span,512),0.f};
        graphics.interactionSide=1-graphics.interactionSide;
    }
    if(windAdmitted){
        graphics.previousWind[selector]=graphics.wind[selector];
        const float span=environment.windExtent,half=mul(span,.5f);
        for(int i=0;i<3;++i)graphics.wind[selector][i]=quantize(sub(environment.cameraPosition[i],half),span,64);
        graphics.wind[selector][3]=0.f;graphics.phaseSide[selector]=1-graphics.phaseSide[selector];
    }
}
// Exact caller guard: a failed D7BEC0 gate preserves fresh/history origins and
// side. Invoke only where the actual admitted resource update is scheduled.
inline bool beginWindUpdate(GraphicsHistory& graphics,const WindEnvironment& environment,
    std::uint32_t selector,const WindAdmissionInputs& admission) {
    if(!windAdmitted(admission))return false;
    applyAdmittedWindows(graphics,environment,selector,false,true);return true;
}
inline bool beginInteractionUpdate(GraphicsHistory& graphics,const WindEnvironment& environment,
    const InteractionAdmissionInputs& admission) {
    if(!interactionAdmitted(admission))return false;
    applyAdmittedWindows(graphics,environment,0,true,false);return true;
}
inline void finishRenderNode(GraphicsHistory& graphics,const CurrentWind& current) {
    graphics.previousRenderTime=current.renderTime; // BF8610, after node work.
}
inline void writeView(std::span<std::uint8_t> view,const CurrentWind& current) {
    if(view.size()<0x698)throw std::runtime_error("Tree2 current View size");
    for(int i=0;i<4;++i)write(view,0x680+4*i,current.direction[i]);
    write(view,0x690,current.strength);write(view,0x694,current.phase);
}
// Changes current globals/windows only. Material184..207/228 and matrices are
// retained from actual imported template. Caller owns source camera matrices.
inline void writeVsFrame(std::span<std::uint8_t> cb,const WindEnvironment& environment,
    const CurrentWind& current,const GraphicsHistory& graphics,std::uint32_t groupFlags) {
    if(cb.size()<272)throw std::runtime_error("Tree2 VS constant buffer size");
    const auto selector=(groupFlags>>1)&15u;write(cb,160,environment.strengthScale);
    for(int i=0;i<3;++i)write(cb,164+4*i,current.direction[i]);
    write(cb,176,current.shaderScale);write(cb,180,current.phase);
    write(cb,208,graphics.interaction[0]);write(cb,212,graphics.interaction[2]);
    const float ie=environment.interactionExtent<=1.f?1.f:environment.interactionExtent;
    write(cb,216,ie);write(cb,220,ie);writeUint(cb,224,512);
    const float we=environment.windExtent<=1.f?1.f:environment.windExtent;
    for(int i=0;i<3;++i){write(cb,232+8*i,graphics.wind[selector][i]);write(cb,236+8*i,we);}
    writeUint(cb,256,64);
}
// D7BEC0 ->141179* setters; the three counts MUST come from actual admitted
// selectedView owner+48. No zero defaults or resource creation are inferred.
inline void writeComputeFrame(std::span<std::uint8_t> cb,const WindEnvironment& environment,
    const CurrentWind& current,const GraphicsHistory& graphics,std::uint32_t selector,
    std::uint32_t pointCount,std::uint32_t spotCount,std::uint32_t areaCount) {
    if(cb.size()<64||selector>=16)throw std::runtime_error("Tree2 CS constant buffer/selector range");
    for(int i=0;i<3;++i)write(cb,4*i,mul(current.direction[i],current.strength));
    writeUint(cb,12,pointCount);writeUint(cb,16,spotCount);writeUint(cb,20,areaCount);writeUint(cb,24,8);
    const float extent=environment.windExtent;write(cb,28,extent);
    const float factor=div(64.f,extent);
    for(int i=0;i<3;++i){
        write(cb,32+4*i,graphics.wind[selector][i]);
        writeUint(cb,44+4*i,std::bit_cast<std::uint32_t>(cvtt(mul(sub(graphics.wind[selector][i],graphics.previousWind[selector][i]),factor))));
    }
    const float dt=sub(current.renderTime,graphics.previousRenderTime);
    write(cb,56,0.f<=dt?dt:0.f);
    const float scale=environment.strengthScale;write(cb,60,0.f<=scale?scale:0.f);
}
} // namespace tree2_source
