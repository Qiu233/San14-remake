#pragma once
#include <cstdint>

struct NativeModelCollectionCaller {
    std::uint32_t registeredViews{},successfulViews{},firstMask{},secondMask{},shadowMask{};
    bool commonPresent{};
    std::uint32_t commonFlags{};
};
struct NativeModelWorldAdmission {
    std::uint32_t world328{},world32C{},world330{},eligibility{},exclusion78{};
    std::int32_t caster10C{};
    float opacityB0{};
};
// The older single-model importer retains its combined input boundary.
struct NativeCityModelAdmission : NativeModelWorldAdmission {
    std::uint32_t registered3330{},successful3340{},callerFirst{},callerSecond{},callerShadow{};
    std::uint32_t common240Present{},commonFlags8{};
};
struct NativeCityModelMasks {
    std::uint32_t first{},second{},shadow{};
    bool firstSlot{},secondSlot{},shadowSlot{};
};
inline NativeCityModelMasks nativeCityModelCurrentMasks(const NativeModelWorldAdmission& world,
    const NativeModelCollectionCaller& caller) {
    NativeCityModelMasks result;
    result.first=world.eligibility?caller.firstMask&world.world328:0;
    result.second=world.eligibility?caller.secondMask&world.world32C:0;
    result.shadow=world.eligibility&&world.caster10C>=0?caller.shadowMask&world.world330:0;
    if(caller.commonPresent&&(caller.commonFlags&0x800)==0)result.shadow=0;
    // The source ordered comparison accepts unordered opacity.
    const bool opacity=!(world.opacityB0<=0.f);
    result.firstSlot=opacity&&(result.first&caller.successfulViews)!=0;
    result.secondSlot=opacity&&(result.second&caller.successfulViews)!=0;
    result.shadowSlot=opacity&&(result.shadow&caller.successfulViews)!=0;
    return result;
}
inline NativeCityModelMasks nativeCityModelCurrentMasks(const NativeCityModelAdmission& source) {
    return nativeCityModelCurrentMasks(source,{source.registered3330,source.successful3340,
        source.callerFirst,source.callerSecond,source.callerShadow,source.common240Present!=0,source.commonFlags8});
}
