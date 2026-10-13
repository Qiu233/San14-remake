#pragma once
#include "AccessoryPose.h"
#include "../ArmyDefaultActions.h"
#include "../ArmyWorldPresentation.h"
#include <string>

struct NativeCategory2Binding {unsigned draw{},palette{};int materialManager{};};
inline std::vector<NativeCategory2Binding> readNativeCategory2Bindings(const std::filesystem::path& directory) {
    native_accessory_pose_detail::Reader input(directory/"bindings.bin");
    if(input.get<unsigned>()!=0x31474341u)throw std::runtime_error("Accessory geometry requires ACG1 declarations");
    const auto count=input.get<unsigned>();std::vector<NativeCategory2Binding> result(count);
    for(unsigned index=0;index<count;++index) {
        auto& row=result[index];row.draw=input.get<unsigned>();row.palette=input.get<unsigned>();row.materialManager=input.get<int>();
        if(row.draw!=index||row.materialManager< -1||row.materialManager>1)
            throw std::runtime_error("Accessory geometry draw/material association unsupported");
    }
    if(input.at!=input.bytes.size())throw std::runtime_error("Accessory geometry declarations have trailing data");
    return result;
}
struct NativeCategory2Inputs {
    std::vector<NativeCategory2Binding> bindings;
    std::vector<std::array<char,80>> pixels;
    std::vector<std::vector<char>> vertices;
    std::vector<std::array<unsigned,3>> skinControls;
    NativeAccessoryPoseSource pose;
    NativeArmyDefaultAction defaultAction;
    std::array<unsigned,2> fps{};
    unsigned capacity{};
    ArmyWorldPresentation constructor;
};
inline NativeCategory2Inputs readNativeCategory2Inputs(const std::filesystem::path& input) {
    NativeCategory2Inputs imported;auto* result=&imported;const auto geometry=input/"geometry";
    result->bindings=readNativeCategory2Bindings(geometry);
    result->pose=prepareNativeAccessoryPoseSource(input/"pose-native");
    std::ifstream list(geometry/"models.txt");std::string relative;unsigned index=0;
    if(!list)throw std::runtime_error("Accessory model resource list absent");
    while(list>>relative) {
        native_accessory_pose_detail::Reader pixel(geometry/relative/"ps-b0.bin"),skin(geometry/relative/"skin-controls.bin"),vertex(geometry/relative/"vs-b0.bin");
        if(pixel.bytes.size()!=80||skin.bytes.size()!=12||vertex.bytes.size()!=6256)throw std::runtime_error("Accessory material/skin resource profile extent");
        std::array<unsigned,3> controls{};skin.read(controls.data(),12);
        if(controls!=std::array<unsigned,3>{1,1,0})throw std::runtime_error("Accessory geometry requires its four-influence skin branch");
        result->vertices.emplace_back(vertex.bytes.begin(),vertex.bytes.end());result->skinControls.push_back(controls);
        if(result->bindings.at(index).palette>=result->pose.palettes.size())throw std::runtime_error("Accessory draw palette absent");
        std::array<char,80> bytes{};pixel.read(bytes.data(),80);result->pixels.push_back(bytes);++index;
    }
    if(!index||index!=result->bindings.size())throw std::runtime_error("Accessory model and geometry binding counts differ");
    using native_accessory_pose_detail::Reader;
    Reader action(geometry/"default-action.bin");auto& row=result->defaultAction;
    row.category=action.get<unsigned>();row.rawActionId=action.get<unsigned>();row.ordinal=action.get<unsigned>();
    row.clipFileId=action.get<unsigned>();row.grouped=action.get<unsigned>();row.controllerRate=action.get<float>();
    row.clipDuration=action.get<float>();row.loopDeclaration=action.get<float>();
    for(auto& fps:result->fps)fps=action.get<unsigned>();
    if(action.at!=action.bytes.size()||row.category!=2||row.rawActionId!=0||row.grouped!=0||
        !row.clipFileId||!std::isfinite(row.controllerRate)||row.controllerRate<=0||
        !std::isfinite(row.loopDeclaration)||row.loopDeclaration>=0||
        row.clipDuration!=result->pose.duration8||!result->fps[0]||!result->fps[1])
        throw std::runtime_error("Accessory default action differs from its actual pose");
    Reader pool(geometry/"pool.bin");result->capacity=pool.get<unsigned>();const auto kind=pool.get<unsigned>(),flags=pool.get<unsigned>();
    if(pool.at!=pool.bytes.size()||!result->capacity||kind!=1||flags!=0)
        throw std::runtime_error("Accessory physical model pool declaration unsupported");
    Reader constructor(geometry/"constructor-world.bin");constructor.read(&result->constructor.world,48);constructor.read(result->constructor.bounds.data(),80);
    if(constructor.at!=constructor.bytes.size())throw std::runtime_error("Accessory physical constructor extent");
    return imported;
}
