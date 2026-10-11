#pragma once
#include "../../Camera/CameraTransform.h"
#include <array>
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <span>
#include <stdexcept>

namespace San14::Render {
struct MainStarSettings {
    float exposureScale{},diameterTexel{},blinkSpeed{};
    float peakRightAscension{},peakDeclination{},showerSpeed{};
    float meteorIncidents{},meteorIncidentsFade{},shutterSpeedScale{};
};
struct MainStarCurrentInputs {
    MainStarSettings settings;
    source_camera::Matrix controllerMatrix{};
    float controllerBrightness{};
};
namespace Interop {
inline constexpr std::size_t MainStarInputBytes=108;
inline MainStarCurrentInputs decodeMainStarInputs(std::span<const std::uint8_t> bytes) {
    if(bytes.size()!=MainStarInputBytes)
        throw std::runtime_error("Main Star current MSS1 extent");
    std::uint32_t magic{};std::memcpy(&magic,bytes.data(),4);
    if(magic!=0x3153534Du)throw std::runtime_error("Main Star current MSS1 version");
    std::array<float,9> values{};std::memcpy(values.data(),bytes.data()+4,sizeof(values));
    MainStarCurrentInputs result;
    result.settings={values[0],values[1],values[2],values[3],values[4],values[5],values[6],values[7],values[8]};
    std::memcpy(result.controllerMatrix.data(),bytes.data()+40,sizeof(result.controllerMatrix));
    std::memcpy(&result.controllerBrightness,bytes.data()+104,4);
    return result;
}
inline MainStarCurrentInputs readMainStarInputs(const std::filesystem::path& path) {
    std::array<std::uint8_t,MainStarInputBytes> bytes{};
    std::ifstream stream(path,std::ios::binary);
    stream.read(reinterpret_cast<char*>(bytes.data()),bytes.size());
    if(!stream||stream.peek()!=std::char_traits<char>::eof())
        throw std::runtime_error("Initial Main Star input absent or malformed");
    return decodeMainStarInputs(bytes);
}
} // namespace Interop
} // namespace San14::Render
