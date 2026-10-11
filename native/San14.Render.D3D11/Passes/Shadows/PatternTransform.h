#pragma once
#include "ShadowReceiverMaterial.h"
#include "../../Camera/SkyPatternCamera.h"

namespace San14::Render {
struct OrdinaryPatternCameraSettings {
    std::array<float,2> scale{},velocity{};
    std::array<float,3> angles{};
    bool mergedOverrideEnabled{};
};
struct PatternLightModifier {
    bool present{},preservePatternOverride{};
    float strength{};
    std::array<float,2> planeSub{},scale{},velocity{},offset{};
};

inline std::array<float,7> patternModifierGroupKey(const PatternLightModifier& modifier) {
    if(!modifier.present)return {0,0,0,32000,32000,0,0};
    return {modifier.strength,modifier.velocity[0],modifier.velocity[1],
        modifier.scale[0],modifier.scale[1],modifier.offset[0],modifier.offset[1]};
}
inline bool samePatternModifierGroup(const PatternLightModifier& first,const PatternLightModifier& second) {
    return patternModifierGroupKey(first)==patternModifierGroupKey(second);
}

inline ReceiverMatrix ordinaryPatternMatrix(const std::array<float,2>& scale,
    const std::array<float,2>& velocity,const std::array<float,2>& offset,
    const std::array<float,3>& angles,float commonClock) {
    using source_camera::A;using source_camera::S;using source_camera::M;using source_camera::D;
    ReceiverMatrix result;
    if(angles[0]==0&&angles[1]==0&&angles[2]==0) {
        result={D(1,scale[0]),0,0,0, 0,0,-1,0, 0,D(1,scale[1]),0,0, 0,0,0,1};
    } else {
        // Range reduction requires finite angles; selected material packing
        // remains responsible for the other actually consumed float values.
        for(float angle:angles)if(!std::isfinite(angle))
            throw std::runtime_error("Ordinary Pattern rotation requires finite angles");
        result={scale[0],0,0,0, 0,scale[1],0,0, 0,0,1,0, 0,0,0,1};
        const auto halfX=M(scale[0],.5f),halfY=M(scale[1],.5f);
        result=source_sky_pattern::translate(result,{source_camera::flip(halfX),source_camera::flip(halfY),0,0});
        result=source_camera::compose(result,{1,0,0,0, 0,0,1,0, 0,-1,0,0, 0,0,0,1});
        constexpr float radians=.017453292f;
        const auto yawAngle=M(angles[2],radians);
        const std::array<float,2> yaw{std::sin(yawAngle),std::cos(yawAngle)};
        for(unsigned row=0;row<4;++row) {
            const unsigned i=row*4;const auto x=result[i],z=result[i+2];
            result[i]=A(M(z,yaw[0]),M(x,yaw[1]));
            result[i+2]=S(M(z,yaw[1]),M(x,yaw[0]));
        }
        const auto xAngle=M(angles[0],radians),yAngle=M(angles[1],radians);
        const std::array<float,2> xRotation{std::sin(xAngle),std::cos(xAngle)},
            yRotation{std::sin(yAngle),std::cos(yAngle)};
        const auto sx=xRotation[0],cx=xRotation[1],sy=yRotation[0],cy=yRotation[1];
        for(unsigned row=0;row<4;++row) {
            const unsigned i=row*4;const auto x=result[i],y=result[i+1],z=result[i+2];
            result[i]=A(A(M(y,M(sy,sx)),M(x,cy)),M(z,M(sy,cx)));
            result[i+1]=S(M(y,cx),M(z,sx));
            result[i+2]=A(S(M(y,M(cy,sx)),M(x,sy)),M(z,M(cy,cx)));
        }
        result=source_sky_pattern::translate(result,{halfX,halfY,0,0});
        bool wrote;result=source_sky_pattern::affineInverse(result,wrote);
        if(!wrote)throw std::runtime_error("Singular rotated Pattern transform requires its retained inverse scratch owner");
    }
    result[12]=A(M(commonClock,velocity[0]),offset[0]);
    result[13]=A(M(commonClock,velocity[1]),offset[1]);
    return result;
}

inline bool installOrdinaryPattern(ShadowReceiverPatternState& state,
    const OrdinaryPatternCameraSettings& camera,const PatternLightModifier& modifier,
    bool mergedPresent,float commonClock) {
    auto scale=camera.scale,velocity=camera.velocity;std::array<float,2> offset{};
    bool changed=false;
    if(modifier.present&&!modifier.preservePatternOverride) {
        state.scale=modifier.strength;
        state.extra={ReceiverVector{modifier.planeSub[0],modifier.planeSub[1],0,0},ReceiverVector{}};
        changed=true;
        if(!(mergedPresent&&camera.mergedOverrideEnabled)) {
            scale=modifier.scale;velocity=modifier.velocity;offset=modifier.offset;
        }
    }
    if(scale[0]!=32000||scale[1]!=32000||velocity[0]!=0||velocity[1]!=0||
        offset[0]!=0||offset[1]!=0||camera.angles[0]!=0||camera.angles[1]!=0||camera.angles[2]!=0) {
        state.worldToPattern=ordinaryPatternMatrix(scale,velocity,offset,camera.angles,commonClock);
        changed=true;
    }
    return changed;
}
} // namespace San14::Render
