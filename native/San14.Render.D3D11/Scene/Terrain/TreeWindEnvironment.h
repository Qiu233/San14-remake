#pragma once
#include <array>
#include <cstdint>

namespace tree2_source {
struct WindGeneratorParameters {
    float baseStrength{},strengthVariation{},riseSeconds{},fallSeconds{};
    float peakSeconds{},minimumStrength{},restSeconds{},restVariation{};
};
// Values consumed by vegetation simulation. The accepted environment is owned
// by GroundDetails; it contains neither a camera packet nor a borrowed span.
struct WindEnvironment {
    std::array<float,3> cameraPosition{};
    bool ordinaryCamera{};
    std::array<float,4> directionQuaternion{};
    WindGeneratorParameters generatorParameters;
    float firstJitter{},secondJitter{};
    float interactionExtent{},windExtent{},strengthScale{};
};

} // namespace tree2_source
