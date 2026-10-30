#pragma once
#include "../../../Runtime/PreparedFileInput.h"
#include "../HdrLightingState.h"
#include "../../Environment/WeatherFogRecordReader.h"
#include <filesystem>
#include <fstream>

namespace San14::Render::Interop {
inline constexpr std::size_t HdrLightingInputBytes = 176;
inline HdrLightingSettings decodeHdrLightingInputs(std::span<const std::uint8_t> input,
    std::array<HdrLightModifier, 5>& modifiers) {
    if (input.size() != HdrLightingInputBytes) throw std::runtime_error("HDR lighting current input length");
    std::size_t at = 0;
    auto word = [&]() {
        std::uint32_t value; std::memcpy(&value, input.data() + at, 4); at += 4; return value;
    };
    auto scalar = [&]() { auto bits = word(); float value; std::memcpy(&value, &bits, 4); return value; };
    auto flag = [&]() {
        auto value = word(); if (value > 1) throw std::runtime_error("HDR lighting boolean input"); return value != 0;
    };
    if (word() != 0x31434C48u) throw std::runtime_error("HDR lighting current input signature");
    HdrLightingSettings result;
    result.ambientMapsRate = scalar(); result.suppressAmbient = flag(); result.fogEnabled = flag();
    for (auto& modifier : modifiers) {
        modifier.present = flag(); modifier.adjustsLighting = flag();
        for (auto& factor : modifier.directionalFactors) factor = scalar();
        modifier.ambientFactor = scalar(); modifier.hemisphereFactor = scalar();
    }
    return result;
}
inline HdrLightState decodeHdrLight(std::span<const std::uint8_t> input, HdrLightModifier modifier) {
    if (input.size() != 0x328) throw std::runtime_error("HDR numeric light prefix length");
    using source_camera::get;
    HdrLightState result;
    for (std::size_t i = 0; i < result.directional.size(); ++i) {
        result.directional[i] = {get<HdrVector>(input.data() + i * 32),
            get<HdrVector>(input.data() + i * 32 + 16), get<float>(input.data() + 0x80 + i * 4)};
    }
    result.ambient = get<HdrVector>(input.data() + 0xE0);
    result.modifier = modifier;
    result.diffuse = {get<float>(input.data() + 0x31C), get<float>(input.data() + 0x320),
        get<float>(input.data() + 0x324), get<float>(input.data() + 0x300)};
    result.diffuseIntensity = get<float>(input.data() + 0x318);
    result.specular = {get<float>(input.data() + 0x30C), get<float>(input.data() + 0x310),
        get<float>(input.data() + 0x314), get<float>(input.data() + 0x304)};
    result.specularIntensity = get<float>(input.data() + 0x308);
    result.hemisphere = {get<HdrVector>(input.data() + 0x100), get<HdrVector>(input.data() + 0x110),
        get<HdrVector>(input.data() + 0x130), get<HdrVector>(input.data() + 0x140)};
    result.fog = source_weather_fog::decodeFog(input.data() + 0x150);
    result.preservesFreshHatching = (get<std::uint32_t>(input.data() + 0x1F8) & 0x40) != 0;
    result.preservesFreshShadowFade = (input[0x1F7] & 1) != 0;
    result.causticsUpdateDisabled = (get<std::uint32_t>(input.data() + 0x1F4) & 0x40000000u) != 0;
    return result;
}
inline HdrLightingLayout readHdrLightingLayout(const std::filesystem::path& path) {
    NativePreparedInputFile input(path, std::ios::binary);
    auto word = [&]() {
        std::uint32_t value{}; input.read(reinterpret_cast<char*>(&value), 4);
        if (!input) throw std::runtime_error("HDR lighting layout is truncated"); return value;
    };
    if (word() != 0x314C4C48u) throw std::runtime_error("HDR lighting layout signature");
    HdrLightingLayout result;
    result.slot = word(); result.constantBytes = word(); const auto count = word();
    if (result.slot >= 14 || result.constantBytes == 0 || result.constantBytes > 65536 ||
        result.constantBytes % 16 != 0 || count > 4096)
        throw std::runtime_error("HDR lighting constant layout format");
    result.writes.reserve(count);
    for (std::uint32_t i = 0; i < count; ++i) {
        auto field = word(); HdrLightingWrite write;
        write.destination = word(); write.source = word(); write.bytes = word();
        if (field >= HdrLightingFieldBytes.size() || write.bytes == 0 ||
            (write.destination | write.source | write.bytes) % 4 != 0 ||
            std::uint64_t(write.source) + write.bytes > HdrLightingFieldBytes[field] ||
            std::uint64_t(write.destination) + write.bytes > result.constantBytes)
            throw std::runtime_error("HDR lighting field range format");
        write.field = HdrLightingField(field); result.writes.push_back(write);
    }
    if (input.peek() != std::char_traits<char>::eof()) throw std::runtime_error("HDR lighting layout tail");
    return result;
}
} // namespace San14::Render::Interop
