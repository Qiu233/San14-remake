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
struct WindOwner {
    std::array<float,37> generator{};
    std::array<float,2> phase{}; // D01580 explicit24F0 zero.
    std::array<float,4> direction{1.f,0.f,0.f,0.f}; //141179580.
};
struct CurrentWind {
    std::array<float,4> target{},direction680{};
    float strength690{},phase694{},shaderScale{},common254{},effective3350{};
    std::uint32_t randomState{},generatorStage{};
};
// Call at actual D12A80 wind-update occurrences (selectedView0 or6), with the
// accepted Main and the SAME Common clock publication used by the renderer.
// This scoped port implements the actual held DF4=DF8=0 branch. It retains all
// four AB54C0 scalar stages, sine and shared LCG even when amplitude is zero.
inline CurrentWind advance(WindOwner& owner,WorldRandom& random,
    std::span<const std::uint8_t> main,const NativeFrameClockPublication& clock) {
    if(main.size()<0x18C0)throw std::runtime_error("Tree2 wind requires actual prepared Main18C0");
    if(read(main,0xDF4)!=0.f||read(main,0xDF8)!=0.f)
        throw std::runtime_error("Positive Tree2 jitter requires actual seeded104A180 source");
    float x=read(main,0xDC0),y=read(main,0xDC4),z=read(main,0xDC8),w=read(main,0xDCC);
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
    for(int i=0;i<8;++i)g[i]=read(main,0xDD0+4*i);
    const float lower=sub(g[0],mul(g[1],.5f));
    g[5]=lower<=g[5]?lower:(0.f<=g[5]?g[5]:0.f);
    g[14]=g[15]=0.f; // AB53F0(...,0): duration zero, direct target publication.
    for(int i=0;i<4;++i)g[20+i]=g[24+i]=target[i];
    g[28]=g[29]=0.f;
    const float dt=clock.effective3350;
    g[16]=add(dt,g[16]);g[13]=dt;
    float admitted=add(g[0],mul(g[1],.5f));
    if(g[11]<admitted){admitted=sub(g[0],mul(g[1],.5f));if(admitted<g[11])admitted=g[11];}
    g[11]=admitted;
    const auto stage=std::bit_cast<std::uint32_t>(g[36]);bool wave=false;
    if(stage==0){
        g[12]=g[5];g[9]=add(dt,g[9]);
        if(g[10]<g[9]){
            g[36]=std::bit_cast<float>(1u);g[8]=0.f;
            g[11]=add(mul(sub(.5f,div(float(random.next16()),65535.f)),g[1]),g[0]);
        }
    }else if(stage==1){
        g[8]=add(div(mul(dt,3.1415927f),g[2]<=.01f?.01f:g[2]),g[8]);
        if(g[8]>3.1415927f){g[8]=3.1415927f;g[9]=0.f;g[36]=std::bit_cast<float>(2u);}wave=true;
    }else if(stage==2){
        g[9]=add(dt,g[9]);if(g[4]<g[9]){g[9]=0.f;g[36]=std::bit_cast<float>(3u);}g[12]=g[11];
    }else if(stage==3){
        g[8]=add(div(mul(dt,3.1415927f),g[3]<=.01f?.01f:g[3]),g[8]);
        if(g[8]>6.2831855f){
            g[36]=g[8]=g[9]=0.f;
            g[10]=add(mul(sub(.5f,div(float(random.next16()),65535.f)),g[7]),g[6]);
        }wave=true;
    }
    if(wave)g[12]=add(mul(mul(add(sourceSin(sub(g[8],1.5707964f)),1.f),sub(g[11],g[5])),.5f),g[5]);
    if(g[12]<=0.f)g[12]=0.f;
    const float allScale=read(main,0x143C);
    float scaled=mul(mul(allScale,.01f),g[12]);
    float speed=sqr(scaled<30.f?(0.f<=scaled?scaled:0.f):30.f);
    if(speed>1.f){const float delta=sub(speed,1.f);speed=add(div(add(delta,delta),sub(sqr(30.f),1.f)),1.f);}
    owner.phase[0]=add(mul(mul(speed,dt),6.2831855f),owner.phase[0]);
    if(owner.phase[0]>3600.f){owner.phase[0]=sub(owner.phase[0],3600.f);owner.phase[1]=add(owner.phase[1],1.f);}
    constexpr float smoothing=std::bit_cast<float>(0x3C23D70Au);
    for(int i=0;i<4;++i)owner.direction[i]=add(mul(smoothing,target[i]),mul(sub(1.f,smoothing),owner.direction[i]));
    auto& sd=owner.direction;
    reciprocal=div(1.f,sqr(add(add(mul(sd[0],sd[0]),mul(sd[1],sd[1])),mul(sd[2],sd[2]))));
    for(int i=0;i<3;++i)sd[i]=mul(sd[i],reciprocal);
    return {target,sd,g[12],add(mul(owner.phase[1],3600.f),owner.phase[0]),
        mul(sqr(mul(mul(g[12],allScale),.01f)),.1f),clock.common254,dt,
        random.state,std::bit_cast<std::uint32_t>(g[36])};
}
// BF58A0 actual renderer initializer. Resource admission is deliberately a
// caller input; this numeric history does not create any selected-View owner.
struct GraphicsHistory {
    std::array<float,4> interaction{},previousInteraction{};
    std::array<std::array<float,4>,16> wind{},previousWind{};
    std::uint32_t interactionSide=0;
    std::array<std::uint32_t,16> phaseSide{};
    float previousRenderTime474=0.f;
};
struct WindAdmissionInputs {
    std::uint32_t selectedViewIndex{},commonFlags{};
    bool selectedViewPresent{},commonPresent{},cameraOrdinary{},shaderPresent{};
    bool pointOwnerBufferPresent{},spotOwnerBufferPresent{},areaOwnerBufferPresent{};
    bool windTexturesCreated{},selectorPhasePairCreated{};
    float main1438{};
};
inline bool windAdmitted(const WindAdmissionInputs& a) {
    return a.selectedViewIndex<32&&a.selectedViewPresent&&a.commonPresent&&(a.commonFlags&0x2600)!=0
        &&a.cameraOrdinary&&a.main1438>0.f&&a.pointOwnerBufferPresent&&a.spotOwnerBufferPresent
        &&a.areaOwnerBufferPresent&&a.shaderPresent&&a.windTexturesCreated&&a.selectorPhasePairCreated;
}
struct InteractionAdmissionInputs {
    std::uint32_t selectedViewIndex{},commonFlags{};
    bool selectedViewPresent{},commonPresent{},cameraOrdinary{},shaderPresent{};
    bool ownerBufferPresent{},resourcePairCreated{};
    float main141C{};
};
inline bool interactionAdmitted(const InteractionAdmissionInputs& a) {
    return a.selectedViewIndex<32&&a.selectedViewPresent&&a.commonPresent&&(a.commonFlags&0x200000)!=0
        &&a.cameraOrdinary&&a.main141C>0.f&&a.ownerBufferPresent&&a.shaderPresent&&a.resourcePairCreated;
}
inline std::int32_t cvtt(float x) { return _mm_cvttss_si32(_mm_set_ss(x)); }
inline float quantize(float position,float span,std::uint32_t resolution) {
    const auto step=std::bit_cast<std::int32_t>(std::uint32_t(cvtt(span))/resolution);
    if(step<=0)return 0.f;
    return float(cvtt(position)/step*step);
}
inline void applyAdmittedWindows(GraphicsHistory& graphics,std::span<const std::uint8_t> camera,
    std::uint32_t selector,bool interactionAdmitted,bool windAdmitted) {
    if(camera.size()<0x18C0||selector>=16)throw std::runtime_error("Tree2 source camera/selector range");
    if(interactionAdmitted){
        graphics.previousInteraction=graphics.interaction;
        const float span=read(camera,0x141C),half=mul(span,.5f);
        graphics.interaction={quantize(sub(read(camera,0x20),half),span,512),0.f,
            quantize(sub(read(camera,0x28),half),span,512),0.f};
        graphics.interactionSide=1-graphics.interactionSide;
    }
    if(windAdmitted){
        graphics.previousWind[selector]=graphics.wind[selector];
        const float span=read(camera,0x1438),half=mul(span,.5f);
        for(int i=0;i<3;++i)graphics.wind[selector][i]=quantize(sub(read(camera,0x20+4*i),half),span,64);
        graphics.wind[selector][3]=0.f;graphics.phaseSide[selector]=1-graphics.phaseSide[selector];
    }
}
// Exact caller guard: a failed D7BEC0 gate preserves fresh/history origins and
// side. Invoke only where the actual admitted resource update is scheduled.
inline bool beginWindUpdate(GraphicsHistory& graphics,std::span<const std::uint8_t> camera,
    std::uint32_t selector,const WindAdmissionInputs& admission) {
    if(!windAdmitted(admission))return false;
    applyAdmittedWindows(graphics,camera,selector,false,true);return true;
}
inline bool beginInteractionUpdate(GraphicsHistory& graphics,std::span<const std::uint8_t> camera,
    const InteractionAdmissionInputs& admission) {
    if(!interactionAdmitted(admission))return false;
    applyAdmittedWindows(graphics,camera,0,true,false);return true;
}
inline void finishRenderNode(GraphicsHistory& graphics,const CurrentWind& current) {
    graphics.previousRenderTime474=current.common254; // BF8610, after node work.
}
inline void writeView(std::span<std::uint8_t> view,const CurrentWind& current) {
    if(view.size()<0x698)throw std::runtime_error("Tree2 current View size");
    for(int i=0;i<4;++i)write(view,0x680+4*i,current.direction680[i]);
    write(view,0x690,current.strength690);write(view,0x694,current.phase694);
}
// Changes current globals/windows only. Material184..207/228 and matrices are
// retained from actual imported template. Caller owns source camera matrices.
inline void writeVsFrame(std::span<std::uint8_t> cb,std::span<const std::uint8_t> main,
    const CurrentWind& current,const GraphicsHistory& graphics,std::uint32_t groupFlags) {
    if(cb.size()<272||main.size()<0x18C0)throw std::runtime_error("Tree2 VS/source Main size");
    const auto selector=(groupFlags>>1)&15u;write(cb,160,read(main,0x143C));
    for(int i=0;i<3;++i)write(cb,164+4*i,current.direction680[i]);
    write(cb,176,current.shaderScale);write(cb,180,current.phase694);
    write(cb,208,graphics.interaction[0]);write(cb,212,graphics.interaction[2]);
    const float ie=read(main,0x141C)<=1.f?1.f:read(main,0x141C);
    write(cb,216,ie);write(cb,220,ie);writeUint(cb,224,512);
    const float we=read(main,0x1438)<=1.f?1.f:read(main,0x1438);
    for(int i=0;i<3;++i){write(cb,232+8*i,graphics.wind[selector][i]);write(cb,236+8*i,we);}
    writeUint(cb,256,64);
}
// D7BEC0 ->141179* setters; the three counts MUST come from actual admitted
// selectedView owner+48. No zero defaults or resource creation are inferred.
inline void writeComputeFrame(std::span<std::uint8_t> cb,std::span<const std::uint8_t> main,
    const CurrentWind& current,const GraphicsHistory& graphics,std::uint32_t selector,
    std::uint32_t pointCount,std::uint32_t spotCount,std::uint32_t areaCount) {
    if(cb.size()<64||main.size()<0x18C0||selector>=16)throw std::runtime_error("Tree2 CS/source range");
    for(int i=0;i<3;++i)write(cb,4*i,mul(current.direction680[i],current.strength690));
    writeUint(cb,12,pointCount);writeUint(cb,16,spotCount);writeUint(cb,20,areaCount);writeUint(cb,24,8);
    const float extent=read(main,0x1438);write(cb,28,extent);
    const float factor=div(64.f,extent);
    for(int i=0;i<3;++i){
        write(cb,32+4*i,graphics.wind[selector][i]);
        writeUint(cb,44+4*i,std::bit_cast<std::uint32_t>(cvtt(mul(sub(graphics.wind[selector][i],graphics.previousWind[selector][i]),factor))));
    }
    const float dt=sub(current.common254,graphics.previousRenderTime474);
    write(cb,56,0.f<=dt?dt:0.f);
    const float scale=read(main,0x143C);write(cb,60,0.f<=scale?scale:0.f);
}
} // namespace tree2_source
