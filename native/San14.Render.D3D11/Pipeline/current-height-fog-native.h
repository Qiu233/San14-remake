#pragma once
// Current selected Main840..9C8 ->9B6D80/9B6E50 layer and998EE0 wave inputs.
// Uses the imported PS64/RDEF owner; camera, phase time and draw gates remain
// with their existing producers. No GPU objects or per-request resource I/O.
#include "current-ssfog-layer-native.h"
namespace source_current_height_fog {
using namespace source_camera;
static constexpr uint32_t mainBegin=0x840,wordCount=99;
struct Layout {uint32_t byteSize=0,color=0,parameter=0,fluctuation=0,mask=0,projection=0,inverseView=0;};
struct Profile {std::array<std::array<uint32_t,wordCount>,4> words{};Layout layout;};
inline Profile readProfile(const std::filesystem::path& source) {
 std::ifstream s(source,std::ios::binary);Profile p;if(read<uint32_t>(s)!=4)throw std::runtime_error("Current HeightFog season count");
 for(unsigned i=0;i<4;++i){auto season=read<uint32_t>(s);if(season>=4||read<uint32_t>(s)!=wordCount)throw std::runtime_error("Current HeightFog source shape");for(auto& word:p.words[season])word=read<uint32_t>(s);}
 p.layout=read<Layout>(s);if(p.layout.byteSize!=1152)throw std::runtime_error("Current HeightFog actual PS64 layout");return p;
}
struct Layer {int type=-1;V4 color{},parameter{};std::array<float,20> values{},velocities{};};
struct Frame {std::array<Layer,3> layers;V4 mask{};};
inline Frame prepareCurrent(const Profile& profile,const source_current_season::Frame& current) {
 auto window=profile.words.at(current.season);for(const auto& w:current.words)if(w.offset>=int(mainBegin)&&w.offset<int(mainBegin+wordCount*4))window[(unsigned(w.offset)-mainBegin)/4]=w.bits;
 auto u=[&](unsigned at){return window[(at-mainBegin)/4];};auto f=[&](unsigned at){uint32_t bits=u(at);float value;std::memcpy(&value,&bits,4);return value;};
 struct Input {unsigned mode,fade,distance,height,velocity,waves;};static constexpr std::array<Input,3> inputs{{
 {0x84c,0x840,0x844,0x850,0x88c,0x890},{0x868,0x8e0,0x8e4,0x86c,0x8ec,0x8f0},{0x94c,0x940,0x944,0x950,0x968,0x96c}}};
 Frame out;
 for(unsigned layer=0;layer<3;++layer){const auto& in=inputs[layer];auto& value=out.layers[layer];uint32_t mode=u(in.mode);value.type=source_current_ssfog::layerType(mode);
  if(value.type<0)continue;float start=f(in.height),end=f(in.height+4);if(int32_t(mode)<0){float z=get<float>(current.main.data()+0x18);start=A(start,z);end=A(end,z);}
  value.color={f(in.height+8),f(in.height+12),f(in.height+16),f(in.height+20)};float x=D(1,S(end,start)),z=0,w=1;
  if(u(in.fade)!=0&&f(in.distance+4)>=f(in.distance)&&f(in.distance)!=f(in.distance+4)){z=D(1,S(f(in.distance+4),f(in.distance)));w=flip(M(z,f(in.distance)));}
  value.parameter={x,flip(M(x,start)),z,w};
  for(unsigned wave=0;wave<4;++wave){unsigned at=in.waves+wave*20,index=wave*5;
   value.values[index]=f(at);value.values[index+1]=f(at+4);value.velocities[index+2]=M(f(in.velocity),f(at+8));value.values[index+3]=f(at+12);value.velocities[index+4]=M(f(in.velocity),f(at+16));}
 }
 float x=D(1,f(0x9bc)),y=D(1,f(0x9c0));out.mask={x,y,flip(M(f(0x9c4),x)),flip(M(f(0x9c8),y))};return out;
}
inline void publishCurrent(const Layout& layout,const Frame& current,std::span<char> bytes) {
 if(bytes.size()!=layout.byteSize)throw std::runtime_error("Current HeightFog retained CB size");
 auto* destination=reinterpret_cast<uint8_t*>(bytes.data());
 for(unsigned layer=0;layer<3;++layer){const auto& value=current.layers[layer];if(value.type<0)continue;
  put(destination+layout.color+layer*16,value.color);put(destination+layout.parameter+layer*16,value.parameter);
  for(unsigned wave=0;wave<4;++wave)for(unsigned component:{0u,1u,3u}){
   unsigned scalar=layer*20+wave*5+component;put(destination+layout.fluctuation+scalar*16,value.values[wave*5+component]);}
 }
 put(destination+layout.mask,current.mask);
}
inline float velocity(const Frame& current,unsigned scalar) {return current.layers[scalar/20].velocities[scalar%20];}
// CPU oracle applies the same closed nativeMapFogPhase equation. The GPU
// adapter updates only its velocities; normal execute remains the clock writer.
inline float phase(float velocity,float commonClock) {float cycles=std::floor(D(commonClock,3600)),local=S(commonClock,M(cycles,3600));
 auto fraction=[](float v){return float(double(v)-std::trunc(double(v)));};return fraction(A(M(velocity,local),M(fraction(M(3600,velocity)),cycles)));}
} // namespace source_current_height_fog
#ifndef SOURCE_CURRENT_HEIGHT_FOG_CPU_ONLY
static void publishNativeCurrentHeightFog(const source_current_height_fog::Profile& profile,
 const source_current_season::Frame& current,NativeMapFog& fog) {
 auto fields=source_current_height_fog::prepareCurrent(profile,current);
 // The currently imported VS1/PS64 profile has active layer types2/2/-1.
 if(fields.layers[0].type!=2||fields.layers[1].type!=2||fields.layers[2].type!=-1)
  throw std::runtime_error("Current HeightFog needs its selected shader variant installation");
 for(auto& pass:fog.passes)for(auto& constant:pass.constants)if(!constant.vertex&&constant.slot==0){
  source_current_height_fog::publishCurrent(profile.layout,fields,constant.bytes);
  for(auto& phase:constant.phases){unsigned scalar=(phase.offset-profile.layout.fluctuation)/16;
   if(fields.layers[scalar/20].type>=0)phase.velocity=source_current_height_fog::velocity(fields,scalar);}
 }
 // Existing camera writer updates1072..1151; execute then updates24 phases
 // from the same accepted CommonClock and uploads these retained buffers.
}
#endif
