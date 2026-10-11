#pragma once
#include <cstdint>
#include <optional>
#include <sstream>
#include <stdexcept>
#include <string>

namespace San14::Render {
// An imported immutable image and its effective sampler identify the physical
// sampling resource independently of any consumer's shader register.
struct SkyTextureIdentity {
    std::string resourceUri;
    std::uint32_t imageIndex{}, viewFormat{};
    std::string sourceSha256, samplerSha256;
    bool operator==(const SkyTextureIdentity&) const = default;
};

struct SkyTextureBindingInput {
    std::string program;
    std::uint32_t slot{}, dimension{}, width{}, height{}, depth{}, levels{};
    std::uint32_t resourceFormat{}, viewFormat{};
    std::string prefix;
    SkyTextureIdentity identity;
};

namespace Interop {
inline std::optional<SkyTextureBindingInput> readSkyTextureBindingRow(const std::string& line) {
    std::istringstream row(line);
    SkyTextureBindingInput result;
    if (!(row >> result.program)) return std::nullopt;
    auto& identity = result.identity;
    if (!(row >> result.slot >> result.dimension >> result.width >> result.height >> result.depth >>
        result.levels >> result.resourceFormat >> result.viewFormat >> result.prefix >>
        identity.resourceUri >> identity.imageIndex >> identity.sourceSha256 >> identity.samplerSha256))
        throw std::runtime_error("Incomplete Sky texture identity row");
    std::string extra;
    if (row >> extra) throw std::runtime_error("Unexpected Sky texture identity row tail");
    identity.viewFormat = result.viewFormat;
    return result;
}
} // namespace Interop
} // namespace San14::Render
