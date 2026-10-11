#pragma once
#include "ShadowReceiverProgramBank.h"
#include <algorithm>
#include <filesystem>
#include <fstream>
#include <map>
#include <sstream>
#include <vector>

namespace San14::Render {
struct ShadowReceiverNode {
    std::uint32_t index{},process{},primary{},noSub{},lightGroup{},colorMask{},depthObject{},litObject{},overrideLitObject{};
};
struct PatternLocalTextureInput {
    std::uint32_t lightObject{};
    bool present{};
    std::filesystem::path directory;
};
struct OrdinaryPatternTextureInputs {
    bool mergedPresent{};
    std::vector<PatternLocalTextureInput> locals;
};
namespace Interop {
inline OrdinaryPatternTextureInputs readOrdinaryPatternTextureInputs(const std::filesystem::path& root) {
    OrdinaryPatternTextureInputs result;
    std::ifstream merged(root/"merged-presence.bin",std::ios::binary);
    std::uint32_t present{};merged.read(reinterpret_cast<char*>(&present),4);
    if(!merged||present>1||merged.peek()!=std::char_traits<char>::eof())
        throw std::runtime_error("Merged Pattern presence format");
    result.mergedPresent=present!=0;
    std::ifstream rows(root/"local-textures.tsv");
    if(!rows)throw std::runtime_error("Ordinary Pattern local resource declarations are absent");
    std::string line;
    while(std::getline(rows,line)) {
        std::istringstream row(line);PatternLocalTextureInput input;std::string directory,tail;
        if(!(row>>input.lightObject>>present>>directory)||row>>tail||present>1)
            throw std::runtime_error("Ordinary Pattern local resource declaration format");
        if(std::any_of(result.locals.begin(),result.locals.end(),[&](const auto& value){return value.lightObject==input.lightObject;}))
            throw std::runtime_error("Duplicate ordinary Pattern light resource declaration");
        input.present=present!=0;input.directory=directory;result.locals.push_back(std::move(input));
    }
    return result;
}
inline std::map<std::uint32_t,ShadowReceiverNode> readShadowReceiverNodes(const std::filesystem::path& path) {
    std::ifstream input(path);if(!input)throw std::runtime_error("Receiver nodes declaration absent");
    std::map<std::uint32_t,ShadowReceiverNode> result;std::string line,extra;
    while(std::getline(input,line)) {
        std::istringstream row(line);ShadowReceiverNode node;
        if(!(row>>node.index)) {if(row.eof())continue;throw std::runtime_error("Receiver node declaration format");}
        if(!(row>>node.process>>node.primary>>node.noSub>>node.lightGroup>>node.colorMask>>node.depthObject>>node.litObject>>node.overrideLitObject)||
            row>>extra||(node.index!=68&&node.index!=119)||(node.process!=0&&node.process!=0x50)||
            !node.primary||!node.noSub||node.colorMask>15||!result.emplace(node.index,node).second)
            throw std::runtime_error("Receiver node declaration fields");
    }
    if(result.size()!=2)throw std::runtime_error("Receiver requires both actual node declarations");
    return result;
}
} // namespace Interop
} // namespace San14::Render
