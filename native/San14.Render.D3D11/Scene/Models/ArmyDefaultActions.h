#pragma once
#include "ArmyPose.h"
struct NativeArmyDefaultAction {
    uint32_t category{},rawActionId{},ordinal{},clipFileId{},grouped{};
    float controllerRate{},clipDuration{},loopDeclaration{};
};
struct NativeArmyDefaultActions {std::array<uint32_t,2> fps{};std::vector<NativeArmyDefaultAction> rows;};
inline NativeArmyDefaultActions prepareNativeArmyDefaultActions(const std::filesystem::path& directory){
    using native_accessory_pose_detail::Reader;Reader in(directory/"army-default-actions.bin");std::array<char,8> magic{};in.read(magic.data(),8);
    if(magic!=std::array<char,8>{'A','R','M','Y','A','C','T','1'})throw std::runtime_error("Army actual default action source schema");
    const auto count=in.get<uint32_t>();
    NativeArmyDefaultActions r;r.rows.resize(count);for(auto& fps:r.fps){fps=in.get<uint32_t>();if(fps==0)throw std::runtime_error("Army actual source FPS is zero");}
    for(unsigned category=0;category<count;++category){auto& row=r.rows[category];row.category=in.get<uint32_t>();row.rawActionId=in.get<uint32_t>();
        row.ordinal=in.get<uint32_t>();row.clipFileId=in.get<uint32_t>();row.grouped=in.get<uint32_t>();row.controllerRate=in.get<float>();
        row.clipDuration=in.get<float>();row.loopDeclaration=in.get<float>();
        if(row.category!=category||row.rawActionId!=0||!row.clipFileId||row.grouped>1||!std::isfinite(row.controllerRate)||row.controllerRate<=0||
            !std::isfinite(row.clipDuration)||row.clipDuration<=0||!std::isfinite(row.loopDeclaration))throw std::runtime_error("Army actual default action source fields");}
    if(in.at!=in.bytes.size())throw std::runtime_error("Army default action source trailing bytes");return r;
}
