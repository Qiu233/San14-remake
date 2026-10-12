#pragma once
#include "SkyTextureIdentity.h"
#include <array>
#include <cstddef>
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <limits>
#include <vector>
#include <utility>

namespace San14::Render {
struct SkyAtmosphereSelection {
    std::uint32_t staticObjectKey{},fallbackPlaneObjectKey{};
};
struct SkyAtmosphereSettings {
    std::uint32_t objectKey{}, planeKey{}, width{}, height{}, flags{};
    std::uint32_t transmittanceKey{}, ambientKey{};
    std::array<float, 4> mieExtinction{}, rayleighExtinction{};
    float mieHeight{}, rayleighHeight{}, planetRadius{}, atmosphereTopHeight{};
};
struct SkyAtmospherePlaneInput {
    std::uint32_t staticObjectKey{};
    SkyTextureIdentity image;
};
struct SkyAtmosphereInputs {
    SkyAtmosphereSettings settings;
    std::vector<std::uint8_t> transmittance, ambient;
    std::vector<SkyAtmospherePlaneInput> plane;
};

namespace Interop {
inline SkyAtmosphereSelection readSkyAtmosphereSelection(const std::filesystem::path& directory) {
    std::ifstream input(directory/"selection.bin",std::ios::binary);
    std::array<std::uint32_t,3> words{};
    input.read(reinterpret_cast<char*>(words.data()),sizeof(words));
    if(!input||words[0]!=0x31534153u||input.peek()!=std::char_traits<char>::eof())
        throw std::runtime_error("Atmosphere SAS1 selection is absent or malformed");
    return {words[1],words[2]};
}
inline SkyAtmosphereInputs readSkyAtmosphereInputs(const std::filesystem::path& directory) {
    std::ifstream input(directory / "settings.bin", std::ios::binary);
    auto read = [&]<class T>() {
        T value{};
        input.read(reinterpret_cast<char*>(&value), sizeof(value));
        if (!input) throw std::runtime_error("Incomplete atmosphere SAT1 settings");
        return value;
    };
    if (read.operator()<std::uint32_t>() != 0x31544153u)
        throw std::runtime_error("Atmosphere SAT1 settings version");
    SkyAtmosphereInputs result;
    auto& settings = result.settings;
    settings.objectKey = read.operator()<std::uint32_t>();
    settings.planeKey = read.operator()<std::uint32_t>();
    settings.width = read.operator()<std::uint32_t>();
    settings.height = read.operator()<std::uint32_t>();
    settings.flags = read.operator()<std::uint32_t>();
    settings.transmittanceKey = read.operator()<std::uint32_t>();
    settings.ambientKey = read.operator()<std::uint32_t>();
    for (auto& value : settings.mieExtinction) value = read.operator()<float>();
    for (auto& value : settings.rayleighExtinction) value = read.operator()<float>();
    settings.mieHeight = read.operator()<float>();
    settings.rayleighHeight = read.operator()<float>();
    settings.planetRadius = read.operator()<float>();
    settings.atmosphereTopHeight = read.operator()<float>();
    if (input.peek() != std::char_traits<char>::eof())
        throw std::runtime_error("Unexpected atmosphere SAT1 settings tail");
    if (!settings.objectKey || !settings.planeKey || !settings.width || !settings.height)
        throw std::runtime_error("Atmosphere settings require actual declarations and table dimensions");
    const auto cells = std::uint64_t(settings.width) * settings.height;
    if (cells > std::numeric_limits<std::size_t>::max() / 8 ||
        cells > std::uint64_t(std::numeric_limits<std::streamsize>::max()) / 8)
        throw std::runtime_error("Atmosphere raw table extent exceeds the supported byte range");
    const auto bytes = std::size_t(cells * 8);
    auto table = [&](const char* name) {
        const auto path = directory / name;
        if (std::filesystem::file_size(path) != bytes)
            throw std::runtime_error("Atmosphere raw half4 table extent differs from its declaration");
        std::vector<std::uint8_t> payload(bytes);
        std::ifstream raw(path, std::ios::binary);
        raw.read(reinterpret_cast<char*>(payload.data()), std::streamsize(bytes));
        if (!raw) throw std::runtime_error("Incomplete atmosphere raw half4 table");
        return payload;
    };
    if (settings.transmittanceKey) result.transmittance = table("transmittance-half4.bin");
    if (settings.ambientKey) result.ambient = table("ambient-half4.bin");

    std::ifstream plane(directory / "plane.tsv");
    if (!plane) throw std::runtime_error("Atmosphere Plane mapping is missing");
    std::string line;
    while (std::getline(plane, line)) {
        std::istringstream row(line);
        std::uint32_t ordinal{};
        if (!(row >> ordinal)) {
            if (row.eof()) continue;
            throw std::runtime_error("Malformed atmosphere Plane ordinal");
        }
        if (ordinal != result.plane.size() || ordinal >= 4)
            throw std::runtime_error("Atmosphere Plane requires contiguous ordered entries");
        SkyAtmospherePlaneInput entry;
        auto& image = entry.image;
        if (!(row >> entry.staticObjectKey >> image.resourceUri >> image.imageIndex >> image.viewFormat >>
            image.sourceSha256 >> image.samplerSha256) || !entry.staticObjectKey)
            throw std::runtime_error("Incomplete atmosphere Plane identity");
        std::string extra;
        if (row >> extra) throw std::runtime_error("Unexpected atmosphere Plane identity tail");
        result.plane.push_back(std::move(entry));
    }
    if (result.plane.size() != 3 && result.plane.size() != 4)
        throw std::runtime_error("Atmosphere Plane requires three or four actual images");
    return result;
}
} // namespace Interop
} // namespace San14::Render
