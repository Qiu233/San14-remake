#pragma once
#include "../../../Runtime/PreparedFileInput.h"
#include "../HdrProgramBank.h"
#include <filesystem>
#include <fstream>

namespace San14::Render::Interop {
inline HdrCurrentSelectionSettings decodeHdrSelectionSettings(std::span<const std::uint8_t> input) {
    if (input.size() != 20) throw std::runtime_error("HDR current selection settings extent");
    std::array<std::uint32_t, 5> words;
    std::memcpy(words.data(), input.data(), input.size());
    if (words[0] != 0x31435348u || words[4] > 1) throw std::runtime_error("HDR current selection settings signature");
    return {words[1], words[2], words[3], words[4]};
}
struct ImportedHdrProgramBank {
    HdrProgramBank bank;
    std::uint32_t constructorFlags{};
    std::vector<std::uint32_t> recipes;
};
inline ImportedHdrProgramBank readHdrProgramBank(const std::filesystem::path& path) {
    NativePreparedInputFile input(path, std::ios::binary);
    auto word = [&] {
        std::uint32_t value{};
        input.read(reinterpret_cast<char*>(&value), sizeof(value));
        if (!input) throw std::runtime_error("HDR program bank is truncated");
        return value;
    };
    if (word() != 0x31504248u) throw std::runtime_error("HDR program bank signature");
    const auto node = word(), view = word(), group = word(), plugins = word(), flags = word();
    const auto featureCount = word(), variantCount = word(), combinationCount = word();
    if (view != 0 || plugins > 31 || featureCount == 0 || variantCount == 0 ||
        featureCount > 65536 || variantCount > 65536 ||
        std::uint64_t(featureCount) * variantCount != combinationCount || combinationCount > 1048576)
        throw std::runtime_error("HDR program bank dimensions or unsupported View slot");
    std::vector<HdrFeatureKey> features;
    features.reserve(featureCount);
    for (std::uint32_t i = 0; i < featureCount; ++i)
        features.push_back({word(), word(), word(), word(), word(), word(), word(), word(), word(), word(), word(), word()});
    std::vector<HdrVariantKey> variants(variantCount);
    for (auto& variant : variants) for (auto& value : variant.words) value = word();
    std::vector<HdrProgramReference> programs;
    std::vector<std::uint32_t> recipes;
    programs.reserve(combinationCount); recipes.reserve(combinationCount);
    for (std::uint32_t i = 0; i < combinationCount; ++i) {
        programs.push_back({word(), word(), word()}); recipes.push_back(word());
    }
    if (input.peek() != std::char_traits<char>::eof()) throw std::runtime_error("HDR program bank trailing bytes");
    return {HdrProgramBank(node, view, group,
        {(plugins & 1) != 0, (plugins & 2) != 0, (plugins & 4) != 0, (plugins & 8) != 0, (plugins & 16) != 0},
        std::move(features), std::move(variants), std::move(programs)), flags, std::move(recipes)};
}
} // namespace San14::Render::Interop
