#pragma once
#include "../../../../Runtime/PreparedFileInput.h"
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <map>
#include <stdexcept>
#include <string>

struct NativeRefractionTargetDeclaration {
    std::string logicalKey;
    std::uint32_t resourceFormat{},shaderFormat{},targetFormat{},width{},height{},mipCount{};
};
inline std::map<std::string,NativeRefractionTargetDeclaration> readNativeRefractionTargets(
    const std::filesystem::path& path) {
    NativePreparedInputFile input(path);
    if (!input) throw std::runtime_error("Refraction target declarations are absent");
    std::map<std::string,NativeRefractionTargetDeclaration> result;
    std::string role;NativeRefractionTargetDeclaration target;
    while (input>>role>>target.logicalKey>>target.resourceFormat>>target.shaderFormat>>target.targetFormat>>target.width>>target.height>>target.mipCount) {
        unsigned maximum=1;for(auto dimension=target.width>target.height?target.width:target.height;dimension>1;dimension>>=1)++maximum;
        if (target.logicalKey.size()!=8||target.logicalKey.find_first_not_of("0123456789ABCDEFabcdef")!=std::string::npos||
            target.width==0||target.height==0||target.mipCount==0||target.mipCount>maximum||!result.emplace(role,target).second)
            throw std::runtime_error("Refraction target declaration format");
    }
    if (!input.eof()) throw std::runtime_error("Refraction target declaration is truncated");
    for (const auto* role:{"RefractionDepth","TransmittedSpecular","FilteredSpecular","HalfFilteredSpecular","BlurredDepth"})
        if (!result.contains(role)) throw std::runtime_error("Refraction target producer declaration is missing");
    return result;
}
inline void requireNativeRefractionMedia(const NativeRefractionTargetDeclaration& target,
    std::uint32_t resource,std::uint32_t shader,std::uint32_t output,std::uint32_t width,std::uint32_t height,std::uint32_t mipCount) {
    if (target.resourceFormat!=resource||target.shaderFormat!=shader||target.targetFormat!=output||
        target.width!=width||target.height!=height||target.mipCount!=mipCount)
        throw std::runtime_error("Refraction program media does not support the selected target format or extent");
}
