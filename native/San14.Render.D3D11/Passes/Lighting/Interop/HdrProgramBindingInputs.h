#pragma once
#include "../../../Runtime/PreparedFileInput.h"
#include <array>
#include <filesystem>
#include <fstream>
#include <sstream>
#include <stdexcept>
#include <string>
#include <string_view>
#include <vector>

namespace San14::Render {
struct HdrTextureRecipe {
    unsigned slot{};
    std::string role;
    std::array<std::string,2> keys;
};
struct HdrSamplerFiles {
    unsigned slot{};
    std::array<std::string,2> files;
    bool paired{};
};
inline unsigned hdrSamplerSide(bool paired,unsigned sourceIndex) {
    if(sourceIndex>1)throw std::runtime_error("HDR specular source index");
    return paired?sourceIndex:0u;
}
inline const std::string& selectHdrSpecularKey(const HdrTextureRecipe& texture,unsigned sourceIndex,
    const std::array<std::string_view,2>& physicalKeys) {
    const auto side=hdrSamplerSide(true,sourceIndex);
    if(texture.role!="TranslucentSpecular"||texture.keys[side]!=physicalKeys[side])
        throw std::runtime_error("Selected HDR specular image does not match its actual physical owner");
    return texture.keys[side];
}
namespace Interop {
inline std::vector<HdrTextureRecipe> readHdrTextureRecipes(std::istream& input) {
    if (!input) throw std::runtime_error("HDR selected texture declarations are absent");
    std::vector<HdrTextureRecipe> result;std::string line;
    while (std::getline(input,line)) {
        if (line.find_first_not_of(" \t\r")==std::string::npos) continue;
        std::istringstream row(line);HdrTextureRecipe texture;std::string extra;
        if (!(row>>texture.slot>>texture.role>>texture.keys[0])||texture.slot>=32)
            throw std::runtime_error("HDR selected texture declaration format");
        if (texture.role=="TranslucentSpecular") {
            if (!(row>>texture.keys[1])) throw std::runtime_error("HDR late specular requires both declared images");
        } else texture.keys[1]=texture.keys[0];
        if (row>>extra) throw std::runtime_error("HDR selected texture declaration has extra columns");
        result.push_back(std::move(texture));
    }
    return result;
}
inline std::vector<HdrTextureRecipe> readHdrTextureRecipes(const std::filesystem::path& path) {
    NativePreparedInputFile input(path);
    return readHdrTextureRecipes(input);
}
inline std::vector<HdrSamplerFiles> readHdrSamplerFiles(std::istream& input) {
    if (!input) throw std::runtime_error("HDR selected sampler declarations are absent");
    std::vector<HdrSamplerFiles> result;std::string line;
    while (std::getline(input,line)) {
        if (line.find_first_not_of(" \t\r")==std::string::npos) continue;
        std::istringstream row(line);HdrSamplerFiles sampler;std::string extra;
        if (!(row>>sampler.slot>>sampler.files[0])||sampler.slot>=16)
            throw std::runtime_error("HDR selected sampler declaration format");
        if (row>>sampler.files[1]) sampler.paired=true;
        else sampler.files[1]=sampler.files[0];
        if (row>>extra) throw std::runtime_error("HDR selected sampler declaration has extra columns");
        result.push_back(std::move(sampler));
    }
    return result;
}
inline std::vector<HdrSamplerFiles> readHdrSamplerFiles(const std::filesystem::path& path) {
    NativePreparedInputFile input(path);
    return readHdrSamplerFiles(input);
}
} // namespace Interop
} // namespace San14::Render
