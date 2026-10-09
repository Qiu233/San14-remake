#pragma once
#include "ArmyDefaultActions.h"
#include <map>
#include <optional>
#include <span>

// Once-loaded original body0/1/2 players. Geometry/material/palette layout
// stays the existing category bank; only the nondefault clips are additional.
struct NativeArmyGraphicsAction {
    NativeArmyDefaultAction player;
    uint32_t playerFlags{};
    std::optional<NativeAccessoryPoseSource> pose;
};
struct NativeArmyGraphicsActions {
    std::map<std::pair<unsigned,unsigned>,NativeArmyGraphicsAction> rows;
};
inline NativeArmyGraphicsActions loadNativeArmyGraphicsActions(const std::filesystem::path& directory,
    std::span<const NativeAccessoryPoseSource> poses,std::span<const NativeArmyDefaultAction> defaults,unsigned firstCategory=0){
    native_accessory_pose_detail::Reader in(directory/"actions.bin");std::array<char,8> magic{};in.read(magic.data(),8);
    auto count=in.get<uint32_t>();
    if(magic!=std::array<char,8>{'A','R','M','Y','A','N','M','1'})
        throw std::runtime_error("Army graphics action source schema");
    NativeArmyGraphicsActions result;
    for(unsigned i=0;i<count;++i){
        NativeArmyGraphicsAction row;auto& p=row.player;
        p.category=in.get<uint32_t>();p.rawActionId=in.get<uint32_t>();p.ordinal=in.get<uint32_t>();p.clipFileId=in.get<uint32_t>();p.grouped=in.get<uint32_t>();
        p.controllerRate=in.get<float>();p.clipDuration=in.get<float>();p.loopDeclaration=in.get<float>();
        const float start=in.get<float>(),length=in.get<float>(),end=in.get<float>(),interpolation=in.get<float>();
        const auto continuation=in.get<uint32_t>(),object=in.get<uint32_t>(),flags=in.get<uint32_t>(),extra=in.get<uint32_t>();
        row.playerFlags=flags;
        const bool knownRaw=p.category<2?p.rawActionId<3:
            p.category>=4&&p.category<=6?(p.rawActionId==0||p.rawActionId==2):
            p.category>=7&&p.category<=9?(p.rawActionId==0||p.rawActionId==2||p.rawActionId==3):p.rawActionId==0;
        if(p.category<firstCategory||p.category-firstCategory>=poses.size()||!knownRaw||p.grouped!=(p.category<2?1u:0u)||
            !std::isfinite(p.controllerRate)||p.controllerRate<=0||!std::isfinite(p.clipDuration)||p.clipDuration<=0||
            start!=0||!std::isfinite(length)||length>0||end!=0||interpolation!=0||p.loopDeclaration>=0||
            continuation!=UINT32_MAX||object!=UINT32_MAX||extra!=0)
            throw std::runtime_error("Army graphics action requires its actual supported single default-loop player");
        if(p.rawActionId){
            row.pose=prepareNativeArmyPoseSource(directory/("category-"+std::to_string(p.category))/("action-"+std::to_string(p.rawActionId)));
            const auto& pose=*row.pose;const auto& existing=poses[p.category-firstCategory];
            if(pose.duration8!=p.clipDuration||pose.defaults.size()!=existing.defaults.size()||pose.parents!=existing.parents||
                pose.references.size()!=existing.references.size()||pose.palettes.size()!=existing.palettes.size()||
                std::memcmp(pose.defaults.data(),existing.defaults.data(),pose.defaults.size()*sizeof(NativeAccessoryPoseJoint))!=0||
                std::memcmp(pose.references.data(),existing.references.data(),pose.references.size()*sizeof(NativeAccessoryPoseMatrix))!=0)
                throw std::runtime_error("Army action and retained model skeleton/palette differ");
            for(unsigned palette=0;palette<pose.palettes.size();++palette){
                const auto& a=pose.palettes[palette];const auto& b=existing.palettes[palette];
                if(a.size()!=b.size()||std::memcmp(a.data(),b.data(),a.size()*sizeof(NativeAccessoryPosePaletteEntry))!=0)
                    throw std::runtime_error("Army action active palette differs from retained model");
            }
        }else {
            const auto& old=defaults[p.category-firstCategory];
            if(p.ordinal!=old.ordinal||p.clipFileId!=old.clipFileId||p.clipDuration!=old.clipDuration||p.controllerRate!=old.controllerRate||p.loopDeclaration!=old.loopDeclaration)
                throw std::runtime_error("Army default and current action imports differ");
        }
        if(!result.rows.emplace(std::make_pair(p.category,p.rawActionId),std::move(row)).second)
            throw std::runtime_error("Army graphics action source duplicate");
    }
    if(in.at!=in.bytes.size())throw std::runtime_error("Army graphics action source trailing bytes");
    return result;
}
inline NativeArmyGraphicsActions loadNativeArmyGraphicsActions(const std::filesystem::path& directory,const NativeArmySource& base){
    return loadNativeArmyGraphicsActions(directory,base.poses,base.defaultActions.rows);
}
