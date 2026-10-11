#pragma once
// Selected seasonal light, compact cascade geometry and receiver style.
// Resource allocation and retained camera/atlas history belong to ShadowPass.
#include "../Environment/SeasonFrame.h"
#include "ShadowCameraConstants.h"

namespace source_current_season_shadow {
using source_camera::V4;
using source_camera::A;using source_camera::S;using source_camera::M;using source_camera::D;
struct Parameters {
    int selectedLightGroup{};
    uint32_t selectedMask{},selectedLightObject{};
    float planeScale{},maximumDistance{},nearDistance{},farDistanceOverride{},halfFovTangent{};
    int cascadeCount{};std::array<float,4> cascadeEnds{};
    V4 direction{},basisB{},basisC{};
    uint32_t sampleLoopCount{},cascadeMask{};
};
template<class T> inline T required(const std::optional<T>& value) {
    if(!value)throw std::runtime_error("Selected seasonal shadow requires a missing imported setting");
    return *value;
}
inline float maxss(float first,float second){return first>second?first:second;}
inline std::array<V4,2> planeBasis(V4 direction) {
    const float x=direction.X,y=direction.Y,z=direction.Z;
    const float ax=std::fabs(x),ay=std::fabs(y),az=std::fabs(z);
    if(ay>ax&&az>=ax){
        const float pair=A(M(z,z),M(y,y));
        const float inverse=D(1,std::sqrt(pair));
        const float inverseSecond=D(1,std::sqrt(M(A(M(x,x),pair),pair)));
        return {V4{0,M(inverse,z),source_camera::flip(M(inverse,y)),0},
            V4{source_camera::flip(M(inverseSecond,pair)),M(M(x,y),inverseSecond),M(M(z,x),inverseSecond),0}};
    }
    if(ay<=ax&&az>ay){
        const float pair=A(M(z,z),M(x,x));
        const float inverse=D(1,std::sqrt(pair));
        const float inverseSecond=D(1,std::sqrt(M(A(M(y,y),pair),pair)));
        return {V4{source_camera::flip(M(z,inverse)),0,M(inverse,x),0},
            V4{M(M(x,y),inverseSecond),source_camera::flip(M(inverseSecond,pair)),M(M(z,y),inverseSecond),0}};
    }
    const float pair=A(M(y,y),M(x,x));
    const float inverse=D(1,std::sqrt(pair));
    const float inverseSecond=D(1,std::sqrt(M(A(M(z,z),pair),pair)));
    return {V4{M(y,inverse),source_camera::flip(M(inverse,x)),0,0},
        V4{M(M(z,x),inverseSecond),M(M(z,y),inverseSecond),source_camera::flip(M(inverseSecond,pair)),0}};
}
// This route consumes an unclamped light direction. Angle-limited directions
// need their own producer and must not be approximated by this cascade builder.
inline Parameters prepareUnclampedCompact(const source_current_season::Frame& current) {
    const auto& settings=current.shadow;
    Parameters p;p.selectedLightGroup=required(settings.selectedLightGroup);
    if(p.selectedLightGroup<=0)return p;
    if(p.selectedLightGroup>32)throw std::runtime_error("Selected shadow light group exceeds its mask domain");
    p.selectedMask=1u<<unsigned(p.selectedLightGroup-1);
    const auto* light=source_current_season::firstMatching(current,p.selectedMask);
    if(!light)throw std::runtime_error("Selected shadow group has no matching light");
    p.selectedLightObject=light->object;
    const auto channel=required(settings.directionChannel);
    p.planeScale=required(settings.planeScale);
    p.direction=light->shadowGeometry.directions[static_cast<size_t>(channel)];p.direction.W=0;
    const auto basis=planeBasis(p.direction);
    p.basisB={M(basis[0].X,p.planeScale),M(basis[0].Y,p.planeScale),M(basis[0].Z,p.planeScale),M(basis[0].W,p.planeScale)};
    p.basisC={M(basis[1].X,p.planeScale),M(basis[1].Y,p.planeScale),M(basis[1].Z,p.planeScale),M(basis[1].W,p.planeScale)};
    p.maximumDistance=required(settings.maximumDistance);p.nearDistance=required(settings.nearDistance);
    p.farDistanceOverride=required(settings.farDistanceOverride);p.sampleLoopCount=required(settings.sampleLoopCount);
    // Quality admission remains part of the imported recipe. Atlas dimensions
    // are owned by the loaded render target, not recomputed from this setting.
    (void)required(settings.qualityLevel);
    const auto mode=required(settings.compactMode);
    if(required(settings.rectangleOverride)!=0)throw std::runtime_error("Shadow rectangle override requires its atlas producer");
    switch(mode){case CompactMode::Single:p.cascadeCount=1;break;
        case CompactMode::TwoCascades:p.cascadeCount=2;break;
        case CompactMode::ThreeCascades:p.cascadeCount=3;break;
        case CompactMode::FourCascades:p.cascadeCount=4;break;
        default:throw std::runtime_error("Shadow mode requires another cascade owner family");}
    const float first=required(settings.cascadeSplits[0]),second=required(settings.cascadeSplits[1]),third=required(settings.cascadeSplits[2]);
    const float s1=first>=p.maximumDistance?p.maximumDistance:maxss(0,first);
    const float s2=second>=p.maximumDistance?p.maximumDistance:maxss(s1,second);
    const float s3=third>=p.maximumDistance?p.maximumDistance:maxss(s2,third);
    p.cascadeEnds.fill(p.maximumDistance);if(p.cascadeCount>=2)p.cascadeEnds[0]=s1;if(p.cascadeCount>=3)p.cascadeEnds[1]=s2;if(p.cascadeCount==4)p.cascadeEnds[2]=s3;
    float previous=p.cascadeEnds[0];for(int i=0;i<p.cascadeCount;++i)p.cascadeEnds[i]=previous=maxss(p.cascadeEnds[i],previous);
    for(int i=p.cascadeCount;i<4;++i)p.cascadeEnds[i]=previous;
    p.cascadeMask=0x80000000u|((1u<<unsigned(p.cascadeCount))-1u);
    p.halfFovTangent=settings.halfFovTangent;
    return p;
}
// Publish before camera preparation and draw planning. This changes numeric
// settings without replacing caster catalogs, atlas allocation or history.
inline void publish(const Parameters& current,source_shadow_camera::Profile& cpu,source_shadow_gpu::Profile& gpu) {
    if(current.selectedLightGroup<=0)return;
    cpu.ownFar=current.maximumDistance;cpu.farOverride=current.farDistanceOverride;
    cpu.count=current.cascadeCount;cpu.ends=current.cascadeEnds;cpu.mask22C=current.cascadeMask;
    cpu.direction=current.direction;cpu.tangent=current.halfFovTangent;
    gpu.mainNear=current.nearDistance;gpu.mainFar=current.maximumDistance;
    gpu.basisB=current.basisB;gpu.basisC=current.basisC;gpu.direction=current.direction;gpu.loops=current.sampleLoopCount;
}
}
