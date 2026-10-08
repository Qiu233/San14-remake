// Original EB7BF8 current-camera LensProjection and exact post named CB fields.
// CPU only. Prepare fixed loaded source/light once; update before post Begin.
#pragma once
#include <array>
#include <cmath>
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <stdexcept>
#include <xmmintrin.h>

struct NativeNormalPostCamera {
    float threshold{},limit{},epsilon{},scale{};
    std::array<float,4> absentLightProjection{};
    std::array<float,3> selectedSunF80{};
    std::int32_t actualProjectionConvention{};
    std::uint32_t selectedLightPresent{};
};
struct NativeNormalPostCameraProjection {
    std::array<float,4> value{};
    bool sourceValid{},selectedLightPresent{};
};
inline NativeNormalPostCamera prepareNativeNormalPostCamera(const std::filesystem::path& source) {
    NativeNormalPostCamera result;std::ifstream input(source/"projection-profile.bin",std::ios::binary);
    input.read(reinterpret_cast<char*>(&result.threshold),4);input.read(reinterpret_cast<char*>(&result.limit),4);
    input.read(reinterpret_cast<char*>(&result.epsilon),4);input.read(reinterpret_cast<char*>(&result.scale),4);
    input.read(reinterpret_cast<char*>(result.absentLightProjection.data()),16);
    input.read(reinterpret_cast<char*>(result.selectedSunF80.data()),12);
    input.read(reinterpret_cast<char*>(&result.actualProjectionConvention),4);input.read(reinterpret_cast<char*>(&result.selectedLightPresent),4);
    if(!input)throw std::runtime_error("Original post current-camera projection source profile absent");return result;
}
inline float postCameraAdd(float a,float b){return _mm_cvtss_f32(_mm_add_ss(_mm_set_ss(a),_mm_set_ss(b)));}
inline float postCameraMul(float a,float b){return _mm_cvtss_f32(_mm_mul_ss(_mm_set_ss(a),_mm_set_ss(b)));}
inline float postCameraDiv(float a,float b){return _mm_cvtss_f32(_mm_div_ss(_mm_set_ss(a),_mm_set_ss(b)));}
inline float postCameraSub(float a,float b){return _mm_cvtss_f32(_mm_sub_ss(_mm_set_ss(a),_mm_set_ss(b)));}
inline float postCameraRead(const void* bytes,std::size_t offset){float value;std::memcpy(&value,static_cast<const char*>(bytes)+offset,4);return value;}

template<class Byte>
inline NativeNormalPostCameraProjection calculateNativeNormalPostCamera(const NativeNormalPostCamera& source,
    const std::array<Byte,0x3C0>& cameraPrefix) {
    static_assert(sizeof(Byte)==1,"Actual complete source Camera prefix has byte elements");
    if(!source.selectedLightPresent)return {source.absentLightProjection,false,false};
    const auto* bytes=cameraPrefix.data();auto C=[&](std::size_t offset){return postCameraRead(bytes,offset);};
    float x=source.selectedSunF80[0],y=source.selectedSunF80[1],z=source.selectedSunF80[2];
    float squared=postCameraAdd(postCameraAdd(postCameraMul(x,x),postCameraMul(y,y)),postCameraMul(z,z));
    float inverse=postCameraDiv(1.f,_mm_cvtss_f32(_mm_sqrt_ss(_mm_set_ss(squared))));
    const float negativeFar=-C(0x144);
    x=postCameraAdd(postCameraMul(negativeFar,postCameraMul(x,inverse)),C(0x10));
    y=postCameraAdd(postCameraMul(negativeFar,postCameraMul(y,inverse)),C(0x14));
    z=postCameraAdd(postCameraMul(negativeFar,postCameraMul(z,inverse)),C(0x18));
    float cx=postCameraAdd(postCameraAdd(postCameraAdd(postCameraMul(y,C(0x110)),postCameraMul(x,C(0x100))),postCameraMul(z,C(0x120))),C(0x130));
    float cy=postCameraAdd(postCameraAdd(postCameraAdd(postCameraMul(x,C(0x104)),postCameraMul(y,C(0x114))),postCameraMul(z,C(0x124))),C(0x134));
    float cz=postCameraAdd(postCameraAdd(postCameraAdd(postCameraMul(x,C(0x108)),postCameraMul(y,C(0x118))),postCameraMul(z,C(0x128))),C(0x138));
    float cw=postCameraAdd(postCameraAdd(postCameraAdd(postCameraMul(x,C(0x10C)),postCameraMul(y,C(0x11C))),postCameraMul(z,C(0x12C))),C(0x13C));
    inverse=postCameraDiv(1.f,cw);float px=postCameraMul(cx,inverse),py=postCameraMul(cy,inverse),pz=postCameraMul(cz,inverse);
    if(!std::isfinite(px)||!std::isfinite(py)||!std::isfinite(pz)||!std::isfinite(cw))
        throw std::runtime_error("Original current-camera lens projection reached its unsupported nonfinite branch");
    const float threshold=source.actualProjectionConvention==2?0.f:source.threshold;
    bool valid=!(threshold>pz)&&!(0.f>cw)&&std::fabs(px)<=source.limit&&std::fabs(py)<=source.limit;
    float fov=C(0x148);float vertical=postCameraDiv(source.scale,fov>source.epsilon?fov:source.epsilon);
    float aspect=C(0x14C);float horizontal=postCameraDiv(vertical,aspect>source.epsilon?aspect:source.epsilon);
    return {{px,py,valid?horizontal:0.f,valid?vertical:0.f},valid,true};
}

// Full existing CB arrays stay allocated and retain unrelated source fields.
// Current four source camera requests have inactive Z/W. This helper reports
// active projection honestly; active cold/halo/ghost preparation is separate.
template<class StaticConstantMap>
inline bool applyNativeNormalPostCameraConstants(StaticConstantMap& constants,const NativeNormalPostCameraProjection& projection) {
    // A nonzero W also changes LensFlare2's reciprocal at CB17+0. This
    // field-only adapter lacks its StarBurst/screen inputs; reject before any
    // template mutation. The actual current color union has no cold CB17.
    if(projection.value[3]!=0.f)for(const auto& item:constants) {
        const auto& key=item.first;
        if(key.size()>=8&&key.compare(key.size()-8,8,"cb17.bin")==0)
            throw std::runtime_error("Active cold Lens CB17 requires its complete original reciprocal writer inputs");
    }
    unsigned compositeCount=0;bool compositeActive=false;
    for(auto& item:constants) {
        const auto& key=item.first;auto& bytes=item.second;
        bool composite=key.size()>=8&&key.compare(key.size()-8,8,"cb22.bin")==0;
        bool cold=key.size()>=8&&key.compare(key.size()-8,8,"cb17.bin")==0;
        if(composite) {
            if(bytes.size()!=208)throw std::runtime_error("Complete source Composite CB22 must be208B");
            float x=postCameraAdd(postCameraMul(projection.value[0],.5f),.5f);
            float y=postCameraSub(.5f,postCameraMul(projection.value[1],.5f));
            float active=projection.value[2]>0.f&&projection.value[3]>0.f&&postCameraRead(bytes.data(),56)>0.f?1.f:0.f;
            const std::array<float,4> named{x,y,active,0.f};std::memcpy(bytes.data()+32,named.data(),16);
            compositeActive=compositeActive||active!=0.f;++compositeCount;
        }
        if(cold) {
            if(bytes.size()!=96)throw std::runtime_error("Complete source Lens CB17 must be96B");
            std::memcpy(bytes.data()+16,projection.value.data(),16);
        }
    }
    if(compositeCount!=3)throw std::runtime_error("Normal post source union must retain three complete CB22 templates");
    return compositeActive;
}
template<class Byte,class ContinuousPostOwner>
inline NativeNormalPostCameraProjection updateNativeNormalPostCameraBeforeBegin(const NativeNormalPostCamera& source,
    const std::array<Byte,0x3C0>& currentCameraPrefix,ContinuousPostOwner& owner) {
    if(owner.awaitingReturn)throw std::runtime_error("Return the current post invocation before updating its next camera fields");
    auto projection=calculateNativeNormalPostCamera(source,currentCameraPrefix);
    applyNativeNormalPostCameraConstants(owner.staticConstants,projection);return projection;
}
