#pragma once
#include "../../Resources/ShaderFeatureSelection.h"
#include <algorithm>
#include <array>
#include <filesystem>
#include <fstream>
#include <sstream>
#include <stdexcept>
#include <string>
#include <type_traits>

namespace San14::Render {
using SkyPatternVariant = std::array<std::uint32_t, 9>;
struct SkyPatternSelectionInputs {
    std::uint32_t lowCostLevel{}, rayExponent{};
    bool earthPresent{}, skyDomeLightGroupNonzero{}, cirrusGroupPresent{};
    std::uint32_t lightFogMode{};
};
struct SkyPatternProgramReference {
    std::uint32_t combination{};
    std::int32_t vertex{}, pixel{};
    std::int32_t recipe{};
};
struct SkyPatternProgramSelection {
    std::size_t feature{}, variant{};
    SkyPatternProgramReference program;
};
class SkyPatternProgramBank {
public:
    static SkyPatternProgramBank read(const std::filesystem::path& directory) {
        std::ifstream header(directory / "bank.txt");
        std::size_t featureCount{}, variantCount{}, rowCount{};
        std::string extra;
        if (!(header >> featureCount >> variantCount >> rowCount) || header >> extra ||
            !featureCount || !variantCount || featureCount > std::numeric_limits<std::size_t>::max() / variantCount ||
            rowCount != featureCount * variantCount)
            throw std::runtime_error("Pattern program bank dimensions");
        SkyPatternProgramBank result;
        auto rows = [&](const char* name, auto& values, std::size_t count) {
            using Value = typename std::decay_t<decltype(values)>::value_type;
            std::ifstream input(directory / name, std::ios::binary);
            values.resize(count);
            input.read(reinterpret_cast<char*>(values.data()), std::streamsize(count * sizeof(Value)));
            if (!input || input.peek() != std::char_traits<char>::eof())
                throw std::runtime_error("Pattern program bank row extent");
        };
        static_assert(sizeof(ShaderFeatureKey) == 48);
        rows("features.bin", result.features_, featureCount);
        rows("variants.bin", result.variants_, variantCount);
        result.programs_.resize(rowCount);
        std::vector<bool> seen(rowCount);
        std::ifstream input(directory / "programs.txt");
        std::string line;
        while (std::getline(input, line)) {
            std::istringstream row(line);
            std::size_t feature{}, variant{};
            if (!(row >> feature)) {
                if (row.eof()) continue;
                throw std::runtime_error("Malformed Pattern program row");
            }
            SkyPatternProgramReference program;
            if (!(row >> variant >> program.combination >> program.vertex >> program.pixel >> program.recipe) ||
                row >> extra || feature >= featureCount || variant >= variantCount)
                throw std::runtime_error("Pattern program row format");
            const auto index = feature * variantCount + variant;
            if (seen[index]) throw std::runtime_error("Duplicate Pattern program row");
            seen[index] = true; result.programs_[index] = program;
        }
        if (std::find(seen.begin(), seen.end(), false) != seen.end())
            throw std::runtime_error("Pattern program bank is incomplete");
        return result;
    }
    SkyPatternVariant initialVariant() const { return variants_.front(); }
    static void applyCurrentVariant(const SkyPatternSelectionInputs& inputs, SkyPatternVariant& retained) {
        retained[0] = std::min(inputs.lowCostLevel, 2u);
        retained[1] = std::clamp(inputs.rayExponent, 4u, 6u);
        retained[2] = !(inputs.earthPresent && inputs.skyDomeLightGroupNonzero);
        retained[3] = inputs.cirrusGroupPresent;
        retained[4] = 0;
    }
    SkyPatternProgramSelection select(const SkyPatternSelectionInputs& inputs, SkyPatternVariant& retained) const {
        applyCurrentVariant(inputs,retained);
        const auto variant = std::find(variants_.begin(), variants_.end(), retained);
        if (variant == variants_.end()) throw std::runtime_error("Current Pattern variant is unsupported by the resource bank");
        const ShaderFeatureKey feature{0, 1, 0, 0, 0, 1, inputs.lightFogMode,
            inputs.lightFogMode == 7 ? 2u : 1u, 0, 0, 0, 0};
        const auto featureIndex = ShaderFeatureSelection::select(feature, features_);
        const auto variantIndex = std::size_t(variant - variants_.begin());
        return {featureIndex, variantIndex, programs_[featureIndex * variants_.size() + variantIndex]};
    }
private:
    std::vector<ShaderFeatureKey> features_;
    std::vector<SkyPatternVariant> variants_;
    std::vector<SkyPatternProgramReference> programs_;
};
} // namespace San14::Render
