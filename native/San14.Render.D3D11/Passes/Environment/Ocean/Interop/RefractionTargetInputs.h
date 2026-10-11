#pragma once
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <map>
#include <stdexcept>
#include <string>

struct NativeRefractionTargetDeclaration {
    std::string logicalKey;
    std::uint32_t resourceFormat{},shaderFormat{},targetFormat{},width{},height{};
};
inline std::map<std::string,NativeRefractionTargetDeclaration> readNativeRefractionTargets(
    const std::filesystem::path& path) {
    std::ifstream input(path);
    if (!input) throw std::runtime_error("Refraction target declarations are absent");
    std::map<std::string,NativeRefractionTargetDeclaration> result;
    std::string role;NativeRefractionTargetDeclaration target;
    while (input>>role>>target.logicalKey>>target.resourceFormat>>target.shaderFormat>>target.targetFormat>>target.width>>target.height) {
        if (target.logicalKey.size()!=8||target.logicalKey.find_first_not_of("0123456789ABCDEFabcdef")!=std::string::npos||
            target.width==0||target.height==0||!result.emplace(role,target).second)
            throw std::runtime_error("Refraction target declaration format");
    }
    if (!input.eof()) throw std::runtime_error("Refraction target declaration is truncated");
    for (const auto* role:{"RefractionDepth","TransmittedSpecular","FilteredSpecular","BlurredDepth"})
        if (!result.contains(role)) throw std::runtime_error("Refraction target producer declaration is missing");
    return result;
}
inline void requireNativeRefractionMedia(const NativeRefractionTargetDeclaration& target,
    std::uint32_t resource,std::uint32_t shader,std::uint32_t output,std::uint32_t width,std::uint32_t height) {
    if (target.resourceFormat!=resource||target.shaderFormat!=shader||target.targetFormat!=output||
        target.width!=width||target.height!=height)
        throw std::runtime_error("Refraction program media does not support the selected target format or extent");
}
