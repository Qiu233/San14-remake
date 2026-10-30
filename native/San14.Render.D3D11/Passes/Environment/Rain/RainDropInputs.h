#pragma once
#include <array>
#include <cstdint>
#include <cstring>
#include <span>
#include <stdexcept>

namespace San14::Render::rain {
struct TextureKeys {
    std::uint32_t drop{},splash{},heightMask{},snowFragment{};
    bool hasRainPair()const{return drop!=0&&splash!=0;}
    bool hasParticles()const{return hasRainPair()||snowFragment!=0;}
};
struct Inputs {
    std::uint32_t provider{},rainShader{},snowShader{};
    std::array<std::uint64_t,3> scopes{};
    TextureKeys textures;
    std::uint32_t group{},commonFlags{};
    float minimumInterval{},rippleRateScale{},rippleMaximumPrecipitation{},fadeInSeconds{},fadeOutSeconds{};
    float rippleFlowSpeedSlope{},drawRadius{},heightRange{},bottom{},declaredPrecipitation{},declaredTemperature{};
    float splashLifeSeconds{},rippleLifeSeconds{};
};
namespace Interop {
inline constexpr std::size_t InputBytes=120;
inline Inputs decode(std::span<const std::uint8_t> bytes) {
    if(bytes.size()!=InputBytes)throw std::runtime_error("Current Rain RDP1 packet extent");
    std::size_t at=0;
    auto word=[&](){std::uint32_t v;std::memcpy(&v,bytes.data()+at,4);at+=4;return v;};
    auto scalar=[&](){const auto bits=word();float v;std::memcpy(&v,&bits,4);return v;};
    if(word()!=0x31504452u)throw std::runtime_error("Current Rain RDP1 signature");
    Inputs result;result.provider=word();result.rainShader=word();result.snowShader=word();
    result.textures={word(),word(),word(),word()};result.group=word();result.commonFlags=word();
    result.minimumInterval=scalar();result.rippleRateScale=scalar();result.rippleMaximumPrecipitation=scalar();
    result.fadeInSeconds=scalar();result.fadeOutSeconds=scalar();result.rippleFlowSpeedSlope=scalar();
    result.drawRadius=scalar();result.heightRange=scalar();result.bottom=scalar();
    result.declaredPrecipitation=scalar();result.declaredTemperature=scalar();
    result.splashLifeSeconds=scalar();result.rippleLifeSeconds=scalar();
    if(word()!=0)throw std::runtime_error("Current Rain RDP1 reserved word");
    std::memcpy(result.scopes.data(),bytes.data()+at,24);
    return result;
}
} // namespace Interop
} // namespace San14::Render::rain
