#pragma once
#include <algorithm>
#include <array>
#include <bit>
#include <cmath>
#include <cstdint>
#include <xmmintrin.h>

// CPU-only port of the existing source-backed factory7 DOF2 Prepare/Init.
// Inputs are the current named scalars, not a saved CB or an original process.
// Shader/media allocation remains with the real renderer. Preparing a frame
// does not advance the owner: install next only after the actual module return.
namespace San14::Render::DynamicDof {
struct Extent { bool present{}; std::uint16_t width{},height{}; };
struct Inputs {
    std::uint32_t contextHeight{}, contextWord{}, descriptorMode{};
    bool fix4k{}, multiResolution{}, changeFStop{}, firstExecution{};
    bool enabled{}, spriteEnabled{};
    int gatherType{}, diaphragmType{}, quality{};
    float focusDistance{}, lensCoefficient{}, gatherMaximum{}, userExposure{};
    int automaticExposure{}, bladeCount{};
    float screenAspect{}, spriteThreshold{}, stopPolyRate{};
    std::array<float,2> nearFar{}, anamorphic{1,1};
    std::array<float,3> heat{};
    std::array<float,4> viewport{0,0,1,1};
    Extent rear, scene, prefiltered, preComposite;
};
struct State {
    std::uint32_t warmupCount{}, shapeHeight{}, initializeHeight{};
    bool cocWriteSide{}, stagingSide{}, shape4k{}, shapeMulti{}, initialize4k{}, initializeMulti{};
};
struct Frame {
    std::array<float,36> constants{};
    std::uint32_t passMask{}, apertureSide{};
    bool apertureChanged{};
    State next;
};
inline float add(float a,float b) {return _mm_cvtss_f32(_mm_add_ss(_mm_set_ss(a),_mm_set_ss(b)));}
inline float sub(float a,float b) {return _mm_cvtss_f32(_mm_sub_ss(_mm_set_ss(a),_mm_set_ss(b)));}
inline float mul(float a,float b) {return _mm_cvtss_f32(_mm_mul_ss(_mm_set_ss(a),_mm_set_ss(b)));}
inline float div(float a,float b) {return _mm_cvtss_f32(_mm_div_ss(_mm_set_ss(a),_mm_set_ss(b)));}
inline float maximum(float a,float b) {return _mm_cvtss_f32(_mm_max_ss(_mm_set_ss(a),_mm_set_ss(b)));}
inline float negative(float value) {return std::bit_cast<float>(std::bit_cast<std::uint32_t>(value)^0x80000000u);}
inline constexpr std::array<unsigned,31> directoryPasses{
    53,54,55,56,57,58,59,62,63,64,65,66,67,68,70,72,73,74,75,76,77,69,71,78,79,80,81,82,83,60,61};

inline Frame prepare(const Inputs& in,const State& previous) {
    Frame out;out.next=previous;
    out.apertureChanged=in.changeFStop||previous.shape4k!=in.fix4k||
        previous.shapeMulti!=in.multiResolution||previous.shapeHeight!=in.contextHeight;
    int side=in.multiResolution?static_cast<int>(in.contextHeight)/120:9;
    if(!(side&1))++side;
    out.apertureSide=static_cast<std::uint32_t>(side);
    const float invRear=in.rear.present?div(1.f,in.rear.height):1.f;
    const float invSceneW=in.scene.present?div(1.f,in.scene.width):1.f;
    const float invSceneH=in.scene.present?div(1.f,in.scene.height):1.f;
    const float invPreW=in.prefiltered.present?div(1.f,in.prefiltered.width):1.f;
    const float invPreH=in.prefiltered.present?div(1.f,in.prefiltered.height):1.f;
    const float resolution=in.multiResolution?div(static_cast<float>(in.contextHeight),1080.f):1.f;
    const float gather=mul(resolution,in.gatherMaximum);
    const int gatherType=std::clamp(in.gatherType,0,4);
    const float step=div(mul(gather,4.f),static_cast<float>(gatherType));
    const int nativeType=in.multiResolution?gatherType*static_cast<int>(std::ceil(resolution)):gatherType;
    const float maxGather=maximum(gather,mul(invRear,2.25f));
    const auto ax=in.anamorphic[0],ay=in.anamorphic[1];
    const float anamorphicAspect=ax>ay?1.f:div(mul(ax,ax),mul(ay,ay));
    const float invNear=div(1.f,in.nearFar[0]),invFar=div(1.f,in.nearFar[1]);
    const float difference=sub(invFar,invNear);
    auto& cb=out.constants;
    cb[0]=in.focusDistance;cb[1]=in.lensCoefficient;cb[2]=invRear;
    cb[3]=in.automaticExposure>0?-1.f:in.userExposure;
    cb[4]=gather;cb[5]=static_cast<float>(nativeType);cb[6]=step;cb[7]=div(1.f,in.screenAspect);
    cb[8]=in.spriteThreshold;cb[9]=maxGather;cb[10]=static_cast<float>(in.bladeCount);cb[11]=in.stopPolyRate;
    cb[12]=difference;cb[13]=invNear;cb[14]=div(1.f,difference);cb[15]=negative(div(invNear,difference));
    cb[16]=invSceneW;cb[17]=invSceneH;cb[18]=invPreW;cb[19]=invPreH;
    cb[20]=div(1.f,anamorphicAspect);cb[21]=std::bit_cast<float>(in.contextWord);
    cb[22]=div(1.f,ax);cb[23]=div(1.f,ay);cb[24]=static_cast<float>(side);
    cb[25]=in.multiResolution?std::bit_cast<float>(0x3af2b9d6u):invRear;
    cb[26]=in.fix4k?1.f:div(1.f,ax);cb[27]=in.fix4k?div(ax,ay):div(1.f,ay);
    cb[28]=in.heat[0];cb[29]=in.heat[1];cb[30]=in.heat[2];cb[31]=0;
    const bool cropped=in.viewport[2]!=1.f||in.viewport[3]!=1.f;
    float clipX=1,clipY=1,rawX=0,rawY=0;
    if(cropped&&in.preComposite.present) {
        rawX=sub(static_cast<float>(_mm_cvtt_ss2si(_mm_set_ss(mul(in.preComposite.width,in.viewport[2])))),.5f);
        rawY=sub(static_cast<float>(_mm_cvtt_ss2si(_mm_set_ss(mul(in.preComposite.height,in.viewport[3])))),.5f);
        clipX=div(rawX,in.preComposite.width);clipY=div(rawY,in.preComposite.height);
        if(in.descriptorMode<2){rawX=0;rawY=0;}
    }
    cb[32]=clipX;cb[33]=clipY;cb[34]=rawX;cb[35]=rawY;

    std::uint32_t mask=in.firstExecution?0x800u:0x1000u;
    if(in.changeFStop||previous.initialize4k!=in.fix4k||previous.initializeMulti!=in.multiResolution||
        previous.initializeHeight!=in.contextHeight)mask|=1;
    if(in.enabled) {
        if(in.spriteEnabled) {
            mask|=in.fix4k?0x18u:0x14u;
            const int sprites=cropped&&in.descriptorMode>=2?(in.fix4k?30:29):(in.fix4k?6:5);
            mask|=1u<<sprites;mask|=in.diaphragmType==0?0x180u:0x600u;
        } else mask|=2;
        mask|=1u<<(cropped?(in.spriteEnabled?22:21):(in.spriteEnabled?14:13));
        const bool heat=in.heat[0]!=0;
        int compose=in.quality>=10?(heat?18:15):in.quality>=5?(heat?19:16):(heat?20:17);
        if(cropped)compose+=8;
        mask|=1u<<compose;
    }
    out.passMask=mask;
    out.next.warmupCount=in.firstExecution?2:previous.warmupCount;
    out.next.cocWriteSide=!previous.cocWriteSide;out.next.stagingSide=!previous.stagingSide;
    if(out.apertureChanged){out.next.shape4k=in.fix4k;out.next.shapeMulti=in.multiResolution;out.next.shapeHeight=in.contextHeight;}
    out.next.initialize4k=in.fix4k;out.next.initializeMulti=in.multiResolution;out.next.initializeHeight=in.contextHeight;
    return out;
}
// Called only at the actual source factory finish hook; first-Init and finish
// are distinct events. No elapsed time or request ordinal substitutes for it.
inline void finish(State& state) {if(state.warmupCount)--state.warmupCount;}
}
