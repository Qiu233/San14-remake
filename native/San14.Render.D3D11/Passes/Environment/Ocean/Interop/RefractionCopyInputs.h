#pragma once
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <stdexcept>
#include <string>
#include <string_view>

// A copy input describes the source selected by the render graph. The physical
// source owner supplies its own identity and description during preparation.
struct NativeRefractionCopySource {
    std::string logicalKey;
    std::uint32_t resourceFormat{}, shaderFormat{}, width{}, height{};
};
inline NativeRefractionCopySource readNativeRefractionCopySource(const std::filesystem::path& directory) {
    NativeRefractionCopySource result;
    std::ifstream input(directory / "source.txt");
    input >> result.logicalKey >> result.resourceFormat >> result.shaderFormat >> result.width >> result.height;
    if (!input || result.logicalKey.size() != 8 ||
        result.logicalKey.find_first_not_of("0123456789ABCDEFabcdef") != std::string::npos ||
        result.width == 0 || result.height == 0)
        throw std::runtime_error("Refraction copy source declaration is invalid");
    return result;
}
inline void requireNativeRefractionCopySource(const NativeRefractionCopySource& source,
    std::string_view logicalKey, std::uint32_t resourceFormat, std::uint32_t shaderFormat,
    std::uint32_t width, std::uint32_t height) {
    if (source.logicalKey != logicalKey || source.resourceFormat != resourceFormat ||
        source.shaderFormat != shaderFormat || source.width != width || source.height != height)
        throw std::runtime_error("Refraction copy source does not match its physical owner");
}
