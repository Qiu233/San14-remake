#pragma once
#include "../../../Runtime/PreparedFileInput.h"
#include "../MapViewSettings.h"
#include <filesystem>
#include <fstream>
#include <stdexcept>

namespace San14::Render::Interop {
inline ImportedMapView readMapViewProfile(const std::filesystem::path& path) {
    NativePreparedInputFile input(path,std::ios::binary);
    ImportedMapView result;
    std::uint32_t magic{}, count{}, commonPresent{};
    std::uint32_t modelSelector{}, queueFlags{}, secondaryFlags{}, queueState{};
    auto read=[&](auto& value) { input.read(reinterpret_cast<char*>(&value),sizeof(value)); };
    auto& definition=result.definition;
    auto& camera=result.camera;
    read(magic);read(count);read(camera.shadow.lightGroup);read(camera.shadow.planeScale);
    read(camera.shadow.cascadeMode);read(camera.oceanGroup);read(camera.rainGroup);
    read(definition.terrainExcludedGroups);read(result.initialReceiveShadowMode);
    read(commonPresent);read(definition.common.featureFlags);read(definition.common.outerLightGroupOverride);
    read(definition.common.excludedCategories);read(modelSelector);
    read(queueFlags);read(secondaryFlags);read(queueState);
    if(magic!=0x31505756u||count>32)throw std::runtime_error("Source View import schema");
    definition.common.present=commonPresent!=0;
    definition.modelStencil={
        (queueFlags&0x20)?ModelGroupSign::Negative:ModelGroupSign::Nonnegative,
        modelSelector?1u<<((modelSelector-1)&31):0,
        (queueFlags&0x40)!=0,(secondaryFlags&4)!=0,
        queueState!=15&&(secondaryFlags&1)==0};
    definition.records.resize(count);
    for(auto& record:definition.records) {
        std::uint32_t classification{};
        read(record.groupMask);read(record.primitiveCapacity);read(classification);
        if(classification&1)throw std::runtime_error("Alternate source counter classification");
        record.usesOrdinaryCounter=(classification&1)==0;
    }
    if(!input)throw std::runtime_error("Source View profile truncated");
    return result;
}
} // namespace San14::Render::Interop
