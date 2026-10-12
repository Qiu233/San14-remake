#pragma once
#include "CityModelAdmission.h"
#include <array>
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <istream>
#include <stdexcept>
#include <string>
#include <vector>

inline bool nativePrimaryRecordAdmitted(const NativeCityModelMasks& masks,
    std::uint32_t allowedRecords,unsigned record) {
    return masks.firstSlot&&(allowedRecords&(1u<<record))!=0;
}

struct NativePrimaryAssetDraw {
    unsigned draw{},templateIndex{},queue{},finalTable{},allowedRecords{},shaderFile{},vs{},ps{};
    std::uint64_t table{};
    std::array<char,160> vertex{};std::array<char,80> pixel{};
};
struct NativePrimaryAsset {
    unsigned category{},unsupported{},exclusion{};float forceDistance{},bias{};
    std::vector<NativePrimaryAssetDraw> draws;
};
struct NativePrimaryPoseRows {unsigned draw{};std::array<float,12> normal{},position{};};
inline std::array<char,160> prepareNativePrimaryVertex(const NativePrimaryAssetDraw& asset,
    const NativePrimaryPoseRows& pose) {
    auto vertex=asset.vertex;
    std::memcpy(vertex.data()+64,pose.normal.data(),48);
    std::memcpy(vertex.data()+112,pose.position.data(),48);
    return vertex;
}
struct NativePrimaryUpdate {
    unsigned index{},asset{},present{},mode{};
    NativeModelWorldAdmission world;
    std::array<float,20> obb{};
    std::vector<NativePrimaryPoseRows> draws;
};
// Immutable profiles and template metadata contain no World census.
struct NativeCityTemplateBank {
    std::vector<NativePrimaryAsset> assets;
    std::vector<NativePrimaryAssetDraw> templates;
    std::vector<unsigned> sortFields;
    unsigned commonMode{},maxDraws{},recordCount{};
};
template<class T> inline T primaryRead(std::istream& s){T v{};s.read(reinterpret_cast<char*>(&v),sizeof(v));if(!s)throw std::runtime_error("Current primary source payload truncated");return v;}
inline std::vector<NativePrimaryUpdate> readNativeCityPopulationUpdate(std::istream& s) {
    if(primaryRead<unsigned>(s)!=0x31555043u)throw std::runtime_error("Current primary CPU1 source schema");
    const auto count=primaryRead<unsigned>(s);std::vector<NativePrimaryUpdate> rows(count);
    for(auto& r:rows){r.index=primaryRead<unsigned>(s);r.asset=primaryRead<unsigned>(s);r.present=primaryRead<unsigned>(s);
        auto& w=r.world;w.world328=primaryRead<unsigned>(s);w.world32C=primaryRead<unsigned>(s);w.world330=primaryRead<unsigned>(s);
        w.eligibility=primaryRead<unsigned>(s);w.exclusion78=primaryRead<unsigned>(s);w.caster10C=primaryRead<std::int32_t>(s);w.opacityB0=primaryRead<float>(s);
        r.mode=primaryRead<unsigned>(s);r.obb=primaryRead<std::array<float,20>>(s);auto n=primaryRead<unsigned>(s);r.draws.resize(n);
        for(auto& d:r.draws){d.draw=primaryRead<unsigned>(s);d.normal=primaryRead<std::array<float,12>>(s);d.position=primaryRead<std::array<float,12>>(s);}
    }return rows;
}
inline NativeCityTemplateBank readNativeCityTemplateBank(const std::filesystem::path& directory) {
    NativeCityTemplateBank result;unsigned templateCount{};
    std::ifstream catalog(directory/"template-catalog.txt");
    if(!(catalog>>templateCount>>result.commonMode))throw std::runtime_error("Primary template catalog absent");
    std::string extra;if(catalog>>extra)throw std::runtime_error("Primary template catalog tail");
    std::ifstream order(directory/"sort-fields.txt");unsigned field;
    if(!order)throw std::runtime_error("Primary opaque sort fields absent");
    while(order>>field)result.sortFields.push_back(field);
    if(!order.eof()||result.sortFields!=std::vector<unsigned>{14,3})
        throw std::runtime_error("Unsupported primary opaque sort fields");
    std::ifstream records(directory/"record-count.txt");
    if(!(records>>result.recordCount)||result.recordCount>32)
        throw std::runtime_error("Primary ordered kind1 record count absent");
    if(records>>extra)throw std::runtime_error("Primary record count tail");
    std::ifstream assets(directory/"asset-profiles.bin",std::ios::binary);
    if(primaryRead<unsigned>(assets)!=0x31505343u)throw std::runtime_error("Primary CSP1 resource schema");
    const auto count=primaryRead<unsigned>(assets);result.maxDraws=primaryRead<unsigned>(assets);
    result.assets.resize(count);result.templates.resize(templateCount);
    std::vector<bool> populated(templateCount);
    for(unsigned index=0;index<count;++index) {
        if(primaryRead<unsigned>(assets)!=index)throw std::runtime_error("Primary resource asset order differs");
        auto& asset=result.assets[index];asset.category=primaryRead<unsigned>(assets);
        const auto draws=primaryRead<unsigned>(assets);asset.unsupported=primaryRead<unsigned>(assets);
        asset.exclusion=primaryRead<unsigned>(assets);asset.forceDistance=primaryRead<float>(assets);asset.bias=primaryRead<float>(assets);
        if(draws>result.maxDraws)throw std::runtime_error("Primary resource draw count exceeds declared capacity");
        asset.draws.resize(draws);
        for(auto& draw:asset.draws) {
            draw.draw=primaryRead<unsigned>(assets);draw.templateIndex=primaryRead<unsigned>(assets);draw.queue=primaryRead<unsigned>(assets);
            draw.table=primaryRead<std::uint64_t>(assets);draw.finalTable=primaryRead<unsigned>(assets);draw.allowedRecords=primaryRead<unsigned>(assets);
            draw.shaderFile=primaryRead<unsigned>(assets);draw.vs=primaryRead<unsigned>(assets);draw.ps=primaryRead<unsigned>(assets);
            draw.vertex=primaryRead<std::array<char,160>>(assets);draw.pixel=primaryRead<std::array<char,80>>(assets);
            if(draw.templateIndex>=templateCount||(draw.queue!=54&&draw.queue!=55))
                throw std::runtime_error("Primary renderer/template resource is unsupported");
            result.templates[draw.templateIndex]=draw;populated[draw.templateIndex]=true;
        }
    }
    if(assets.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Primary CSP1 resource tail");
    for(bool present:populated)if(!present)throw std::runtime_error("Primary template has no actual resource profile");
    return result;
}
