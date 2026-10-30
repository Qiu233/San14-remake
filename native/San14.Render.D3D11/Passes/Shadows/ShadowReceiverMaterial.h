#pragma once
#include "SeasonShadowInputs.h"
#include "../../Camera/CameraMatrix.h"
#include "../../Camera/CameraState.h"
#include <cstring>
#include <span>
#include <stdexcept>
#include <string>

namespace San14::Render {
using ReceiverVector=source_camera::V4;
using ReceiverMatrix=source_camera::Matrix;

struct ShadowReceiverPatternState {
    ReceiverMatrix worldToPattern;
    float scale{};
    std::array<ReceiverVector,2> extra{};
    static ShadowReceiverPatternState construct() {
        // A drawable shader has already completed factory preparation, which
        // resets its Pattern module. Bare parameter construction (identity,
        // strength one) is not an observable entry state for these consumers.
        const float inverse=source_camera::D(1,32000);
        return {{inverse,0,0,0, 0,0,-1,0, 0,inverse,0,0, 0,0,0,1},0,{}};
    }
    void reset() {*this=construct();}
};
inline bool installShadowReceiverPattern(ShadowReceiverPatternState& state,float scale,
    const source_camera::Matrix* selectedMatrix) {
    bool changed=false;
    if(scale!=0){state.scale=scale;changed=true;}
    if(selectedMatrix){state.worldToPattern=*selectedMatrix;changed=true;}
    return changed;
}

// Retained cascade values are independent of the selected shader's physical
// layout. A smaller current pass set changes only its actually published rows.
struct ShadowReceiverShadowValues {
    std::array<ReceiverMatrix,4> worldToShadow{};
    std::array<ReceiverVector,4> atlasTransforms{};
    ReceiverVector depthPlane{},splitEnds{},atlasScale{},shadowDepthPlane{};
    std::int32_t sampleLoopCount{};
    std::array<ReceiverVector,12> lightPlanes{};
    std::optional<std::array<std::uint32_t,3>> shadowEnabled;
    std::optional<ReceiverVector> secondaryAtlasScale;

    static ShadowReceiverShadowValues allocateFresh() {
        //97A580 constructs all four input68/token2 matrix backings as identity,
        //independently of the current Compact cascade count. The original
        //inactive atlas/plane bytes have no default writer: value-initializing
        //their owned storage is this project's allocation policy, not a claim
        //that those original fields had zero defaults. Active writers replace
        //their real ranges; subsequent updates retain this same backing.
        ShadowReceiverShadowValues values{};
        const ReceiverMatrix identity{1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1};
        values.worldToShadow.fill(identity);
        return values;
    }
};
struct ShadowReceiverSubShadowValues {
    std::int32_t count{};
    std::array<ReceiverMatrix,4> worldToShadow{};
    ReceiverVector atlasScale{};
    std::array<ReceiverVector,12> lightPlanes{};
};
struct ShadowReceiverMaterial {
    ReceiverVector direction{},ambient{};
    ShadowReceiverPatternState pattern{ShadowReceiverPatternState::construct()};
    std::optional<ShadowReceiverShadowValues> shadow;
    std::optional<ShadowReceiverSubShadowValues> subShadow;
    ReceiverMatrix viewToWorld{},projectionToWorld{};
    source_current_season_shadow::ReceiverBias receiverBias;
    float depthFade{};
};

inline constexpr std::array<std::uint32_t,22> ShadowReceiverFieldBytes{
    16,16,64,4,256,64,16,16,12,16,16,16,4,256,16,64,64,8,4,4,192,192};
inline constexpr std::array<const char*,22> ShadowReceiverFieldNames{
    "litDir","ambLit","mW2Pt","fPtn","mW2S","vSMOf","vS2VZ","vSMDF","bSM",
    "scSM","scSM2","sdplane","nSubSdw","mSubW2S","scSubSM","mV2W","mP2W",
    "srbSlp","fDOFade","nSSLoop","vLPlane","vSubLPlane"};
struct ShadowReceiverFieldView {const void* data;std::size_t bytes;bool integer;};
inline ShadowReceiverFieldView shadowReceiverField(const ShadowReceiverMaterial& material,std::uint32_t id) {
    if(id>=ShadowReceiverFieldBytes.size())throw std::runtime_error("Unknown receiver material semantic");
    auto missing=[&]() -> const void* {throw std::runtime_error(std::string("Current receiver semantic requires its producer: ")+ShadowReceiverFieldNames[id]);};
    const void* data=nullptr;
    switch(id) {
        case 0:data=&material.direction;break;
        case 1:data=&material.ambient;break;
        case 2:data=&material.pattern.worldToPattern;break;
        case 3:data=&material.pattern.scale;break;
        case 4:data=material.shadow?&material.shadow->worldToShadow:missing();break;
        case 5:data=material.shadow?&material.shadow->atlasTransforms:missing();break;
        case 6:data=material.shadow?&material.shadow->depthPlane:missing();break;
        case 7:data=material.shadow?&material.shadow->splitEnds:missing();break;
        case 8:data=material.shadow&&material.shadow->shadowEnabled?&*material.shadow->shadowEnabled:missing();break;
        case 9:data=material.shadow?&material.shadow->atlasScale:missing();break;
        case 10:data=material.shadow&&material.shadow->secondaryAtlasScale?&*material.shadow->secondaryAtlasScale:missing();break;
        case 11:data=material.shadow?&material.shadow->shadowDepthPlane:missing();break;
        case 12:data=material.subShadow?&material.subShadow->count:missing();break;
        case 13:data=material.subShadow?&material.subShadow->worldToShadow:missing();break;
        case 14:data=material.subShadow?&material.subShadow->atlasScale:missing();break;
        case 15:data=&material.viewToWorld;break;
        case 16:data=&material.projectionToWorld;break;
        case 17:data=&material.receiverBias;break;
        case 18:data=&material.depthFade;break;
        case 19:data=material.shadow?&material.shadow->sampleLoopCount:missing();break;
        case 20:data=material.shadow?&material.shadow->lightPlanes:missing();break;
        case 21:data=material.subShadow?&material.subShadow->lightPlanes:missing();break;
    }
    return {data,ShadowReceiverFieldBytes[id],id==8||id==12||id==19};
}
} // namespace San14::Render
