#pragma once
#include "../../Runtime/PreparedFileInput.h"
#include <algorithm>
#include <array>
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <map>
#include <span>
#include <stdexcept>
#include <tuple>
#include <vector>

namespace source_height_fog_programs {
enum class Semantic:uint32_t {Projection,InverseView,Colors,Parameters,Fluctuation,Mask,
    LightDirection,LightColor,Eye,Fog,FogColor,Scattering};
enum class TextureRole:uint32_t {Mask,Depth,SceneColor,Acceleration};
struct Field {Semantic semantic{};uint32_t offset{},words{},elements{},stride{};};
struct StageLayout {int32_t slot=-1;uint32_t bytes{};std::vector<Field> fields;};
struct Key {
    uint32_t feature{},background{};
    std::array<int32_t,3> types{};
    bool operator==(const Key&) const=default;
    bool operator<(const Key& other) const {
        return std::tie(feature,background,types)<std::tie(other.feature,other.background,other.types);
    }
};
struct Program {
    Key key;uint32_t combination{},vertex{},pixel{};
    StageLayout vertexLayout,pixelLayout;
    std::vector<std::pair<TextureRole,uint32_t>> textures;
    std::vector<uint32_t> samplers;
};
struct Bank {
    std::map<Key,Program> entries;
    std::map<uint32_t,std::vector<char>> bytecode;
    const Program& select(const Key& key) const {
        auto found=entries.find(key);
        if(found==entries.end())throw std::runtime_error("Current Height Fog program is not in the imported bank");
        return found->second;
    }
};
template<class T> inline T read(std::istream& input) {
    T result{};input.read(reinterpret_cast<char*>(&result),sizeof(result));
    if(!input)throw std::runtime_error("Height Fog program bank is truncated");return result;
}
inline StageLayout readStage(std::istream& input) {
    StageLayout result;result.slot=read<int32_t>(input);result.bytes=read<uint32_t>(input);
    uint32_t count=read<uint32_t>(input);
    if(result.slot<-1||result.slot>=14||result.bytes>65536||result.bytes%16||count>12||
        (result.slot==-1?(result.bytes!=0||count!=0):result.bytes==0))
        throw std::runtime_error("Height Fog constant stage format");
    std::array<bool,12> seen{};
    for(uint32_t index=0;index<count;++index) {
        Field field;auto semantic=read<uint32_t>(input);
        field.offset=read<uint32_t>(input);field.words=read<uint32_t>(input);
        field.elements=read<uint32_t>(input);field.stride=read<uint32_t>(input);
        static constexpr std::array<std::pair<uint32_t,uint32_t>,12> shapes{{
            {4,1},{16,1},{4,3},{4,3},{1,60},{4,1},{4,1},{4,1},{4,1},{4,1},{4,1},{4,4}}};
        if(semantic>=seen.size()||seen[semantic]||field.words==0||field.words>16||field.elements==0||field.elements>60||
            field.stride<field.words*4||uint64_t(field.offset)+uint64_t(field.elements-1)*field.stride+field.words*4>result.bytes)
            throw std::runtime_error("Height Fog named field format");
        if(field.words!=shapes[semantic].first||field.elements!=shapes[semantic].second)
            throw std::runtime_error("Height Fog semantic field shape");
        seen[semantic]=true;field.semantic=Semantic(semantic);result.fields.push_back(field);
    }
    return result;
}
inline Bank readBank(std::istream& input) {
    if(read<uint32_t>(input)!=0x31474648)throw std::runtime_error("Height Fog program bank signature");
    uint32_t count=read<uint32_t>(input);
    if(count==0||count>4096)throw std::runtime_error("Height Fog program bank count");
    Bank result;
    for(uint32_t index=0;index<count;++index) {
        Program program;program.key.feature=read<uint32_t>(input);program.key.background=read<uint32_t>(input);
        for(auto& type:program.key.types)type=read<int32_t>(input);
        if(program.key.feature>1||program.key.background>1||
            (program.key.background&&program.key.feature)||
            std::any_of(program.key.types.begin(),program.key.types.end(),[](int32_t type){return type<-1||type>2;}))
            throw std::runtime_error("Height Fog selection key format");
        program.combination=read<uint32_t>(input);program.vertex=read<uint32_t>(input);program.pixel=read<uint32_t>(input);
        program.vertexLayout=readStage(input);program.pixelLayout=readStage(input);
        uint32_t textures=read<uint32_t>(input);if(textures>4)throw std::runtime_error("Height Fog texture count");
        std::array<bool,32> slots{};
        for(uint32_t item=0;item<textures;++item) {
            uint32_t role=read<uint32_t>(input),slot=read<uint32_t>(input);
            if(role>3||slot>=slots.size()||slots[slot])throw std::runtime_error("Height Fog texture route format");
            slots[slot]=true;program.textures.emplace_back(TextureRole(role),slot);
        }
        uint32_t samplers=read<uint32_t>(input);if(samplers>1)throw std::runtime_error("Height Fog sampler count");
        for(uint32_t item=0;item<samplers;++item) {
            uint32_t slot=read<uint32_t>(input);if(slot>=16)throw std::runtime_error("Height Fog sampler slot");
            program.samplers.push_back(slot);
        }
        if(!result.entries.emplace(program.key,std::move(program)).second)
            throw std::runtime_error("Duplicate Height Fog selection key");
    }
    if(input.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Height Fog program bank tail");
    return result;
}
inline Bank loadBank(const std::filesystem::path& directory) {
    NativePreparedInputFile input(directory/"program-bank.bin",std::ios::binary);Bank bank=readBank(input);
    for(const auto& pair:bank.entries)for(uint32_t program:{pair.second.vertex,pair.second.pixel}) {
        if(bank.bytecode.contains(program))continue;
        auto path=directory/"programs"/(std::to_string(program)+".dxbc");
        NativePreparedInputFile bytes(path,std::ios::binary|std::ios::ate);auto length=bytes.tellg();
        if(!bytes||length<=0)throw std::runtime_error("Height Fog selected bytecode is absent");
        std::vector<char> data(static_cast<size_t>(length));bytes.seekg(0);bytes.read(data.data(),data.size());
        if(!bytes)throw std::runtime_error("Height Fog selected bytecode is truncated");
        bank.bytecode.emplace(program,std::move(data));
    }
    return bank;
}
} // namespace source_height_fog_programs
