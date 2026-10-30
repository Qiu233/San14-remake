#pragma once
#include "../../Runtime/PreparedFileInput.h"
#include "ReflectionSceneFrame.h"
#include <filesystem>
#include <fstream>
#include <map>
#include <cstring>
namespace San14::Render::Reflection {
struct SceneSourceChild {
    std::uint32_t rootIndex{},objectId{},classId{};std::int32_t flags{};
    std::uint32_t staticInstanceIndex{},nodeFlags44{},eligibility{},world328{},world32C{},world330{},exclusion{};
    std::int32_t casterFlags{};float opacity{};
};
struct SceneSource {
    std::uint32_t rootObjectId{},capacity{},externalObjectId{},commonPresent{},commonFlags14{},view0CaptureMode{};
    Float3 position{},scale{};
    std::vector<SceneSourceChild> children;
    std::vector<FieldDefinition> fields;
};
inline SceneSource readSceneSource(const std::filesystem::path& path) {
    NativePreparedInputFile input(path,std::ios::binary);if(!input)throw std::runtime_error("Reflection actual Scene source package absent");
    auto read=[&]<class T>(T& value){input.read(reinterpret_cast<char*>(&value),sizeof(value));if(!input)throw std::runtime_error("Reflection Scene source ends inside a field");};
    std::array<char,8> magic{};read(magic);
    if(magic!=std::array<char,8>{'R','S','F','R','O','O','T','1'})throw std::runtime_error("Reflection Scene source signature");
    std::uint32_t version{},childCount{},fieldCount{};read(version);if(version!=1)throw std::runtime_error("Reflection Scene source version");
    SceneSource result;read(result.rootObjectId);read(result.capacity);read(result.externalObjectId);
    read(result.commonPresent);read(result.commonFlags14);read(result.view0CaptureMode);read(childCount);read(fieldCount);
    read(result.position);read(result.scale);
    if(!result.capacity||childCount>result.capacity||fieldCount>childCount||result.commonPresent>1)
        throw std::runtime_error("Reflection Scene source finite declaration shape");
    result.children.resize(childCount);std::map<std::uint32_t,bool> names;
    for(std::uint32_t i=0;i<childCount;++i){auto& r=result.children[i];
        read(r.rootIndex);read(r.objectId);read(r.classId);read(r.flags);read(r.staticInstanceIndex);read(r.nodeFlags44);
        read(r.eligibility);read(r.world328);read(r.world32C);read(r.world330);read(r.exclusion);read(r.casterFlags);read(r.opacity);
        if(r.rootIndex!=i||!names.emplace(r.objectId,true).second)throw std::runtime_error("Reflection Scene child source identity/order");
        if(r.classId!=0xC4B9B28D&&r.staticInstanceIndex!=0xFFFFFFFFu)throw std::runtime_error("Non-Model source child borrows a Model mapping");
    }
    result.fields.resize(fieldCount);std::map<std::uint32_t,bool> fieldNames;
    for(auto& f:result.fields){read(f.rootIndex);read(f.objectId);read(f.dimensions);read(f.cellSize);read(f.tileSize);read(f.gBufSize);
        read(f.viewRange);read(f.groupMask);read(f.maxPrimitive);read(f.probeShadowMapSize);read(f.origin);read(f.quaternion);
        if(f.rootIndex>=result.children.size()||result.children[f.rootIndex].objectId!=f.objectId||
            result.children[f.rootIndex].classId!=0xE6379BA2||!fieldNames.emplace(f.objectId,true).second)
            throw std::runtime_error("Reflection Field source child identity/type");
    }
    for(const auto& child:result.children)if(child.classId==0xE6379BA2&&!fieldNames.contains(child.objectId))
        throw std::runtime_error("Actual Field source declaration was omitted from the Scene package");
    char extra;if(input.read(&extra,1))throw std::runtime_error("Reflection Scene source trailing bytes");
    return result;
}
} // namespace San14::Render::Reflection
