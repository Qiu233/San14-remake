#pragma once
// Only numeric admission/variant inputs, not a replacement SSFog draw owner.
#include "current-season-client-input/current-season-native.h"
#include "camera-continuous-native.h"
namespace source_current_ssfog {
struct Profile {std::array<uint32_t,4> thirdMode{};};
inline Profile readProfile(const std::filesystem::path& path) {
 std::ifstream s(path,std::ios::binary);Profile result;
 if(source_camera::read<uint32_t>(s)!=4)throw std::runtime_error("SSFog source season count");
 for(unsigned i=0;i<4;++i){auto season=source_camera::read<uint32_t>(s);if(season>=4)throw std::runtime_error("SSFog source season range");result.thirdMode[season]=source_camera::read<uint32_t>(s);}
 return result;
}
inline int layerType(uint32_t mode) {auto masked=mode&0x7fffffffu;return masked>=1&&masked<=3?int(masked)-1:-1;}
struct Selection {bool admitted{};uint32_t rawMode{},rendererFeature{};std::array<int32_t,10> variant{};};
inline Selection prepareCurrent(const Profile& profile,const source_current_season::Frame& current,uint32_t currentViewFlags19AC,uint32_t localMask,bool background) {
 auto word=[&](int offset){for(const auto& w:current.words)if(w.offset==offset)return w.bits;throw std::runtime_error("SSFog current layer word absent");};
 uint32_t third=profile.thirdMode.at(current.season);for(const auto& value:current.words)if(value.offset==0x94c)third=value.bits;
 Selection out;out.admitted=background||(localMask&currentViewFlags19AC&0xfu)!=0;
 if(!background){const auto found=std::find_if(current.lights.begin(),current.lights.end(),[&](const auto& light){return light.mask==localMask;});
 if(found==current.lights.end())throw std::runtime_error("SSFog current raw local light absent");out.rawMode=source_camera::get<uint32_t>(found->data.data()+0x160);
 // Normal D3C310 HeightFogType3 reads raw L, not F103's modifier scope.
 if(out.rawMode!=0&&out.rawMode!=7)throw std::runtime_error("SSFog current non0/7 light route not imported");out.rendererFeature=out.rawMode==7?1u:0u;}
 out.variant={1,1,0,background?1:0,3,layerType(word(0x84c)),layerType(word(0x868)),layerType(third),3,1};return out;
}
} // namespace source_current_ssfog
