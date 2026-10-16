#pragma once
#include "../../Camera/CameraTangent.h"
#include <algorithm>
#include <array>
#include <cmath>
#include <cstdint>
#include <stdexcept>
#include <vector>

namespace contact_occlusion {
// Typed current values. Resource formats, G1 record layouts and GPU objects
// belong to their respective import/allocation adapters.
struct Settings {
    float radius{},endRadius{},rangeStart{},rangeEnd{},maxRadiusRate{},contrast{},maxOcclusion{},angleBiasDegrees{};
    unsigned quality{},resolution{},useNormal{},useViewDepth{},rayCount{},stepCount{},blurSize{};
    float blurSharpness{};
    unsigned blurLinearFilter{},blurAtHalfSize{},suppressHalo{};
};
struct Modifier {
    bool present{},suppressCopy{};
    float fade=1,radius=1,endRadius=1,rangeStart=1,rangeEnd=1,maxRadiusRate=1;
    float contrast=1,maxOcclusion=1,angleBias=1,stepCount=1;
};
struct LightRecord {unsigned groupMask{};Modifier modifier;};
struct CommonPublication {bool common138{},common140{};};
enum class Composition {Default3,Common138,Common140};
struct Group {unsigned mask{};float fade{};Composition composition=Composition::Default3;};
struct VariantSelection {
    bool usesViewDepth{},linearBlur{},suppressHalo{},splitRange{};
    unsigned quality{},normalMode{},requestedBlurKernel{},groupCount{},blurCapabilityValue{};
};
struct CurrentFrame {
    bool cameraGate{},halfResolution{},usesViewDepth{};
    unsigned stencilReadMask{};
    VariantSelection selection;
    std::vector<Group> groups;
    std::vector<unsigned> passes;
    std::array<float,4> depth{},view{},blur{};
    std::vector<std::array<float,4>> radius,ray,occlusion,radiusRange;
};

namespace arithmetic {
// Explicit scalar roundings preserve the already ported binary32 mechanism
// even when this header is included in a differently optimized consumer.
__declspec(noinline) inline float add(float a,float b) {return a+b;}
__declspec(noinline) inline float subtract(float a,float b) {return a-b;}
__declspec(noinline) inline float multiply(float a,float b) {return a*b;}
__declspec(noinline) inline float divide(float a,float b) {return a/b;}
inline void finite(float value) {
    if(!std::isfinite(value))throw std::runtime_error("Contact occlusion requires finite current controls");
}
inline float tangent(float value) {return source_camera::tangentSse(value);}
}

inline unsigned blurKernel(unsigned size,bool linear) {
    unsigned kernel=(size&(linear?~3u:~1u))+1;
    kernel=std::min(kernel,33u);
    if(!linear) {
        if((kernel&1u)==0)++kernel;
    } else if(((kernel-1)&3u)!=0) {
        kernel=((std::max(kernel,1u)+2)&~3u)+1;
    }
    return kernel;
}

/// The actual View group mask is supplied at execution after population.
/// Ordered duplicate light masks intentionally retain last applicable copy.
inline CurrentFrame prepareCurrent(const Settings& settings,const source_camera::CameraOptics& lens,
    unsigned width,unsigned height,unsigned actualActiveGroups,unsigned hostClass,
    const std::vector<LightRecord>& orderedLights,const CommonPublication& common,
    float minimumDepth=0,float maximumDepth=1) {
    using namespace arithmetic;
    CurrentFrame frame;
    frame.cameraGate=settings.contrast>0 && (settings.radius>0 ||
        (settings.radius==0 && settings.endRadius>0 && settings.rangeEnd>settings.rangeStart)) && settings.quality!=3;
    if(!frame.cameraGate)return frame;
    const unsigned innerCount=hostClass==1?4:7;
    frame.stencilReadMask=hostClass==1?15:127;
    std::vector<Modifier> modifiers;
    for(unsigned inner=0;inner<innerCount;++inner) {
        unsigned bit=1u<<inner;
        if((actualActiveGroups&bit)==0)continue;
        Modifier current;
        for(const auto& record:orderedLights)
            if(record.groupMask==bit && record.modifier.present && !record.modifier.suppressCopy)
                current=record.modifier;
        if(!(multiply(settings.maxRadiusRate,current.maxRadiusRate)>0 &&
             multiply(settings.contrast,current.contrast)>0 &&
             multiply(settings.maxOcclusion,current.maxOcclusion)>0 && current.fade>0))continue;
        Composition composition=hostClass==1&&common.common140?Composition::Common140:
            current.fade!=1&&common.common138?Composition::Common138:Composition::Default3;
        frame.groups.push_back({bit,current.fade,composition});
        modifiers.push_back(current);
    }
    if(frame.groups.empty())return frame;
    if(width==0||height==0)throw std::runtime_error("Contact occlusion requires the actual positive target extent");
    for(float value:{settings.radius,settings.endRadius,settings.rangeStart,settings.rangeEnd,
        settings.maxRadiusRate,settings.contrast,settings.maxOcclusion,settings.angleBiasDegrees,
        settings.blurSharpness,lens.nearPlane,lens.farPlane,lens.aspectRatio,lens.viewSpan,minimumDepth,maximumDepth})finite(value);
    if(lens.nearPlane<=0||lens.farPlane<=lens.nearPlane||lens.aspectRatio<=0||maximumDepth<=minimumDepth)
        throw std::runtime_error("Contact occlusion lens or viewport depth range is invalid");

    frame.halfResolution=(settings.resolution<2?settings.resolution:0)==0;
    frame.usesViewDepth=settings.useViewDepth!=0;
    if(frame.halfResolution && (width<2||height<2))
        throw std::runtime_error("Contact occlusion half-resolution target is empty");
    unsigned sampleWidth=frame.halfResolution?width>>1:width;
    unsigned sampleHeight=frame.halfResolution?height>>1:height;
    float nearArgument=lens.reversedDepth?lens.farPlane:lens.nearPlane;
    float farArgument=lens.reversedDepth?lens.nearPlane:lens.farPlane;
    float denominator=multiply(multiply(farArgument,nearArgument),subtract(maximumDepth,minimumDepth));
    frame.depth={-divide(subtract(farArgument,nearArgument),denominator),
        divide(subtract(multiply(maximumDepth,farArgument),multiply(minimumDepth,nearArgument)),denominator),
        multiply(lens.farPlane,.9997f),0};
    float tangent=source_camera::tangentSse(multiply(lens.viewSpan,.5f));
    frame.view={multiply(multiply(tangent,2),lens.aspectRatio),multiply(tangent,-2),
        multiply(multiply(tangent,-1),lens.aspectRatio),tangent};
    unsigned quality=settings.quality<3?settings.quality:0;
    frame.selection={frame.usesViewDepth,settings.blurLinearFilter!=0,settings.suppressHalo!=0,false,
        quality,settings.useNormal!=0&&quality!=0?5u:0u,
        blurKernel(settings.blurSize,settings.blurLinearFilter!=0),static_cast<unsigned>(frame.groups.size()),
        (settings.blurSize&(settings.blurLinearFilter?~3u:~1u))+1};
    float rays=static_cast<float>(std::max(settings.rayCount,2u));
    for(const auto& modifier:modifiers) {
        for(float value:{modifier.fade,modifier.radius,modifier.endRadius,modifier.rangeStart,modifier.rangeEnd,
            modifier.maxRadiusRate,modifier.contrast,modifier.maxOcclusion,modifier.angleBias,modifier.stepCount})finite(value);
        float rawRadius=multiply(settings.radius,modifier.radius);
        float rawEndRadius=multiply(settings.endRadius,modifier.endRadius);
        float radius=std::max(rawRadius,1.f),endRadius=std::max(rawEndRadius,1.f);
        float start=std::max(multiply(settings.rangeStart,modifier.rangeStart),0.f);
        float end=multiply(settings.rangeEnd,modifier.rangeEnd);
        bool split=rawRadius!=rawEndRadius&&rawEndRadius>0;
        if(end<=start) {
            split=false;
            end=add(multiply(start,1.0000119f),1.1920929e-05f);
        }
        frame.selection.splitRange|=split;
        float rate=std::clamp(multiply(settings.maxRadiusRate,modifier.maxRadiusRate),0.f,1.f);
        float contrast=std::max(multiply(settings.contrast,modifier.contrast),0.f);
        float maxOcclusion=std::clamp(multiply(settings.maxOcclusion,modifier.maxOcclusion),0.f,1.f);
        float angle=std::max(multiply(multiply(settings.angleBiasDegrees,modifier.angleBias),.017453292f),0.f);
        float stepValue=std::floor(multiply(static_cast<float>(settings.stepCount),modifier.stepCount));
        if(static_cast<double>(stepValue)>2147483647.0)
            throw std::runtime_error("Contact occlusion step count exceeds its supported integer range");
        float steps=std::max(stepValue,1.f);
        float squared=multiply(radius,radius);
        frame.radius.push_back({squared,-divide(1,squared),
            divide(multiply(multiply(radius,-.5f),static_cast<float>(sampleWidth)),frame.view[2]),
            multiply(static_cast<float>(std::min(sampleWidth,sampleHeight)),rate)});
        frame.ray.push_back({rays,steps,divide(1,rays),arithmetic::tangent(angle)});
        frame.occlusion.push_back({contrast,maxOcclusion,divide(static_cast<float>(sampleWidth),4),
            divide(static_cast<float>(sampleHeight),4)});
        float rangeScale=-divide(1,subtract(end,start));
        frame.radiusRange.push_back({radius,endRadius,rangeScale,multiply(rangeScale,start)});
    }
    float halfRadius=static_cast<float>((frame.selection.requestedBlurKernel-1)>>1);
    float gaussianWidth=multiply(add(halfRadius,1),.5f);
    frame.blur={halfRadius,divide(frame.selection.linearBlur?1.442695f:1.f,
        multiply(add(gaussianWidth,gaussianWidth),gaussianWidth)),
        multiply(frame.selection.linearBlur?1.6651093f:1.f,std::max(settings.blurSharpness,0.f)),0};
    if(frame.usesViewDepth)frame.passes.push_back(0);
    if(frame.halfResolution)frame.passes.push_back(1);
    frame.passes.insert(frame.passes.end(),{2,3,4});
    return frame;
}
} // namespace contact_occlusion
