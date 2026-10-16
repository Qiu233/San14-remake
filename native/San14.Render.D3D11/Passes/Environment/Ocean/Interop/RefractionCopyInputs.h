#pragma once
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <stdexcept>

// A copy input describes the source selected by the render graph. The physical
// source owner supplies its own identity and description during preparation.
struct NativeRefractionCopySource {
    std::uint32_t sourceObjectId{};
    std::uint32_t resourceFormat{}, shaderFormat{}, width{}, height{};
};
inline NativeRefractionCopySource readNativeRefractionCopySource(const std::filesystem::path& directory) {
    NativeRefractionCopySource result;
    std::ifstream input(directory / "source.txt");
    input >> std::hex >> result.sourceObjectId >> std::dec >> result.resourceFormat >> result.shaderFormat >> result.width >> result.height;
    if (!input || result.width == 0 || result.height == 0)
        throw std::runtime_error("Refraction copy source declaration is invalid");
    return result;
}
inline void requireNativeRefractionCopySource(const NativeRefractionCopySource& source,
    std::uint32_t sourceObjectId, std::uint32_t resourceFormat, std::uint32_t shaderFormat,
    std::uint32_t width, std::uint32_t height) {
    if (source.sourceObjectId != sourceObjectId || source.resourceFormat != resourceFormat ||
        source.shaderFormat != shaderFormat || source.width != width || source.height != height)
        throw std::runtime_error("Refraction copy source does not match its physical owner");
}
