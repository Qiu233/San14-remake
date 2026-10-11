#pragma once
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <map>
#include <stdexcept>
#include <string>
#include <vector>

struct NativeRefractionFilterTargetInput {
    std::string key;
    std::uint32_t width{},height{},mips{},resourceFormat{},shaderFormat{},targetFormat{};
};
struct NativeRefractionFilterRoute {
    std::uint32_t sourceIndex{},inputSlot{},sourceMip{},mipCount{},targetMip{};
    std::string directory,sourceKey,targetKey;
};
struct NativeRefractionFilterInputs {
    std::map<std::string,NativeRefractionFilterTargetInput> targets;
    std::vector<NativeRefractionFilterRoute> routes;
    std::string mainDepthKey;
};
inline NativeRefractionFilterInputs readNativeRefractionFilterInputs(const std::filesystem::path& directory) {
    NativeRefractionFilterInputs result;
    std::ifstream targets(directory/"targets.txt"),depth(directory/"source-depth.txt"),passes(directory/"passes.txt");
    if(!targets||!depth||!passes||!(depth>>result.mainDepthKey))
        throw std::runtime_error("Refraction filter resource declarations are absent");
    NativeRefractionFilterTargetInput target;
    while(targets>>target.key>>target.width>>target.height>>target.mips>>target.resourceFormat>>target.shaderFormat>>target.targetFormat) {
        if(!target.width||!target.height||!target.mips||!result.targets.emplace(target.key,target).second)
            throw std::runtime_error("Refraction filter target format");
    }
    if(!targets.eof())throw std::runtime_error("Refraction filter targets are truncated");
    NativeRefractionFilterRoute route;
    while(passes>>route.sourceIndex>>route.directory) {
        std::ifstream input(directory/route.directory/"input.txt"),output(directory/route.directory/"target.txt");
        if(route.sourceIndex>2||!(input>>route.inputSlot>>route.sourceKey>>route.sourceMip>>route.mipCount)||
            !(output>>route.targetKey>>route.targetMip)||route.inputSlot!=0||!route.mipCount)
            throw std::runtime_error("Refraction filter route format");
        const auto destination=result.targets.find(route.targetKey);
        if(destination==result.targets.end()||route.targetMip>=destination->second.mips)
            throw std::runtime_error("Refraction filter destination mip is absent");
        if(route.sourceKey==result.mainDepthKey) {
            if(route.sourceMip!=0||route.mipCount!=1)throw std::runtime_error("Refraction filter Main depth mip range");
        } else {
            const auto source=result.targets.find(route.sourceKey);
            if(source==result.targets.end()||std::uint64_t(route.sourceMip)+route.mipCount>source->second.mips)
                throw std::runtime_error("Refraction filter source mip is absent");
        }
        result.routes.push_back(route);
    }
    if(!passes.eof())throw std::runtime_error("Refraction filter route list is truncated");
    return result;
}
