#pragma once
#include <array>
#include <cstddef>
#include <cstdint>
#include <optional>

namespace source_current_cloud_weather {
// Parameters have independent ownership from the binary camera/profile layout.
template<size_t Dimensions> struct NoiseLayer {
    std::array<float,Dimensions> period{},position{},velocity{};
};
struct Range {float lower{},upper{};};
struct Endpoints {float clear{},cloudy{};};
struct Conditions {
    float cloudiness{},humidity{},saturation{},clampValue{};
    // Both writes must be present in the accepted publication, even though
    // program/quality selection itself belongs to the render resource owner.
    bool cloudQualityPublished{},ambientQualityPublished{};
};
enum class AnimationChannel {
    BaseX,BaseY,BaseZ,DetailX,DetailY,DetailZ,DistortX,DistortY,
    CirrusX,CirrusY,CirrusDistortX,CirrusDistortY,Reserved0X,Reserved0Y,Reserved1X,Reserved1Y
};
// The animation file identifies these channels using legacy phase addresses.
// Resolve that identity once on import; animation and weather use this enum.
inline std::optional<AnimationChannel> decodeAnimationChannel(uint32_t phase){
    constexpr std::array<uint32_t,16> phases{0xe0,0xe4,0xe8,0x120,0x124,0x128,
        0x160,0x164,0x190,0x194,0x1b0,0x1b4,0x1d8,0x1dc,0x1f8,0x1fc};
    for(size_t i=0;i<phases.size();++i)if(phases[i]==phase)return static_cast<AnimationChannel>(i);
    return std::nullopt;
}
} // namespace source_current_cloud_weather
