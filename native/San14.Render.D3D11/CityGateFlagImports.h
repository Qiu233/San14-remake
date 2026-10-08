#pragma once
#include "CityGateWorldPublication.h"
#include <map>
#include <fstream>

namespace San14::Render {
using CityGateFlagImports=std::map<std::pair<int,int>,CityGateFlagKey>;
inline CityGateFlagImports loadCityGateFlagImports(const fs::path& source,const NativeCityAccessoryGpu& gpu) {
    std::ifstream input(source/"current-flag-keys.bin",std::ios::binary);
    const auto count=primaryRead<unsigned>(input);CityGateFlagImports keys;
    for(unsigned i=0;i<count;++i) {
        const int manager=primaryRead<int>(input);CityGateFlagKey key;
        key.force=primaryRead<int>(input);key.color=primaryRead<int>(input);key.glyph=primaryRead<unsigned>(input);
        if((manager!=0&&manager!=1)||!gpu.materials.count({manager,key.force}))
            throw std::runtime_error("City/Gate flag source has no completed current GPU material");
        if(!keys.emplace(std::make_pair(manager,key.force),key).second)
            throw std::runtime_error("City/Gate flag source identity is duplicated");
    }
    return keys;
}
inline void requireCurrentCityGateFlagImports(const CityGateWorldPublication& current,const CityGateFlagImports& imported) {
    for(const auto& key:current.flags)for(int manager:{0,1}) {
        const auto found=imported.find({manager,key.force});
        if(found==imported.end()||found->second.color!=key.color||found->second.glyph!=key.glyph)
            throw std::runtime_error("Current City/Gate force glyph/color requires its actual material import before publication");
    }
}
}
