#pragma once
// Numeric CurrentSeason -> existing pool6C8 Compact / caster / receiver owners.
// No device allocation, source I/O, owner/history reset or guessed angle clamp.
#include "../Environment/SeasonFrame.h"
#include "ShadowCameraConstants.h"

namespace source_current_season_shadow {
using source_camera::V4;
using source_camera::A;using source_camera::S;using source_camera::M;using source_camera::D;
struct Parameters {
    int selectedSignedA00{};uint32_t selectedMask{},selectedObject{},directionSelectorA04{},modeA3C{},qualityA88{};
    float scaleA28{},ownFar9EC{},mainNear9F0{},farOverrideA2C{},tangent{};
    int count{};std::array<float,4> ends{};
    V4 direction{},camera170{},basisB{},basisC{};
    uint32_t loopsA38{},mask22C{};
};
template<class T> inline T mainWord(const source_current_season::Frame& current,int offset) {
    if(offset>=0&&size_t(offset)+sizeof(T)<=current.main.size())
        return source_current_season::read<T>(current.main,size_t(offset));
    for(const auto& word:current.words)if(word.offset==offset){T value;std::memcpy(&value,&word.bits,4);return value;}
    throw std::runtime_error("Current season shadow requires its actual selected Main word");
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
// This is the source-unclamped normal Aux/argument route. A reached angle clamp
// must run actual CB78C0/9B3C00 first; this helper does not silently approximate it.
inline Parameters prepareUnclampedCompact(const source_current_season::Frame& current) {
    Parameters p;p.selectedSignedA00=mainWord<int32_t>(current,0xa00);
    if(p.selectedSignedA00<=0)return p; // Original selected-shadow gate is closed.
    if(p.selectedSignedA00>32)throw std::runtime_error("Current selected shadow group differs from its source mask domain");
    p.selectedMask=1u<<unsigned(p.selectedSignedA00-1);
    const auto* light=source_current_season::firstMatching(current,p.selectedMask);
    if(!light)throw std::runtime_error("Current selected shadow group has no actual light record");
    p.selectedObject=light->object;p.directionSelectorA04=mainWord<uint32_t>(current,0xa04);
    p.scaleA28=mainWord<float>(current,0xa28);
    const size_t at=p.directionSelectorA04==1?0x20:p.directionSelectorA04==2?0x40:p.directionSelectorA04==3?0x60:0;
    p.direction=source_current_season::read<V4>(light->data,at);p.direction.W=0;
    p.camera170=source_current_season::read<V4>(light->data,0x10);
    const auto basis=planeBasis(p.direction);
    p.basisB={M(basis[0].X,p.scaleA28),M(basis[0].Y,p.scaleA28),M(basis[0].Z,p.scaleA28),M(basis[0].W,p.scaleA28)};
    p.basisC={M(basis[1].X,p.scaleA28),M(basis[1].Y,p.scaleA28),M(basis[1].Z,p.scaleA28),M(basis[1].W,p.scaleA28)};
    p.ownFar9EC=mainWord<float>(current,0x9ec);p.mainNear9F0=mainWord<float>(current,0x9f0);
    p.farOverrideA2C=mainWord<float>(current,0xa2c);p.loopsA38=mainWord<uint32_t>(current,0xa38);
    p.qualityA88=mainWord<uint32_t>(current,0xa88);p.modeA3C=mainWord<uint32_t>(current,0xa3c);
    if(mainWord<uint32_t>(current,0xa84)!=0)throw std::runtime_error("Current shadow rectangle override requires its original A44..A80/atlas writer");
    switch(p.modeA3C){case 0:p.count=1;break;case 9:p.count=2;break;case 10:p.count=3;break;case 11:p.count=4;break;
        default:throw std::runtime_error("Current shadow mode requires a different actual owner family");}
    const float first=mainWord<float>(current,0x9f4),second=mainWord<float>(current,0x9f8),third=mainWord<float>(current,0x9fc);
    const float s1=first>=p.ownFar9EC?p.ownFar9EC:maxss(0,first);
    const float s2=second>=p.ownFar9EC?p.ownFar9EC:maxss(s1,second);
    const float s3=third>=p.ownFar9EC?p.ownFar9EC:maxss(s2,third);
    p.ends.fill(p.ownFar9EC);if(p.count>=2)p.ends[0]=s1;if(p.count>=3)p.ends[1]=s2;if(p.count==4)p.ends[2]=s3;
    float previous=p.ends[0];for(int i=0;i<p.count;++i)p.ends[i]=previous=maxss(p.ends[i],previous);
    for(int i=p.count;i<4;++i)p.ends[i]=previous;
    p.mask22C=0x80000000u|((1u<<unsigned(p.count))-1u);
    p.tangent=current.atmosphere[196/4]; // Actual same-Main original half-FOV tangent.
    return p;
}
// Call before prepareCurrent(cpu,currentCamera,catalog,sameRetainedOwner), then
// before preparePlan/receiverConstants. Selected record binding/caster admission
// must remain the actual same source owner; this numeric update does not create it.
inline void publish(const Parameters& current,source_shadow_camera::Profile& cpu,source_shadow_gpu::Profile& gpu) {
    if(current.selectedSignedA00<=0)return;
    cpu.ownFar=current.ownFar9EC;cpu.farOverride=current.farOverrideA2C;
    cpu.count=current.count;cpu.ends=current.ends;cpu.mask22C=current.mask22C;
    cpu.direction=current.direction;cpu.tangent=current.tangent;
    gpu.mainNear=current.mainNear9F0;gpu.mainFar=current.ownFar9EC;
    gpu.basisB=current.basisB;gpu.basisC=current.basisC;gpu.direction=current.direction;gpu.loops=current.loopsA38;
}
// Existing normal ready-STA/white, PS5/768B, kind0/selector4, wrapper40 absent:
// D76780 arg18=true clears effective E0 RGB but retains alpha. PatternMap keeps
// its constructor matrix; its scalar uses actual Camera27C, not a Sun direction.
// This updates only the three non-shadow named lanes in that exact recipe.
inline void publishNormalPs5ReceiverBase(const source_current_season::Frame& current,
        const Parameters& parameters,std::array<char,768>& constants) {
    if(parameters.selectedSignedA00<=0)return;
    if(mainWord<int32_t>(current,0x3ac)!=0)
        throw std::runtime_error("Current receiver requires another actual Pattern camera branch");
    const auto* light=source_current_season::firstMatching(current,parameters.selectedMask);
    const std::array<uint32_t,3> cleared{};
    std::memcpy(constants.data(),cleared.data(),12);
    std::memcpy(constants.data()+12,light->data.data()+0xec,4);
    std::memcpy(constants.data()+80,current.main.data()+0x27c,4); // PS5 actual fPtn.
    std::memcpy(constants.data()+752,current.main.data()+0x3bc,4); // PS5 actual fDOFade.
}
}
