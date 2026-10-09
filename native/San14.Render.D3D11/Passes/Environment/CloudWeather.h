#pragma once
// Source C452A8..C45693 ->1191AA0 weather input ->1191FE0/1193A90
// numeric consumer. Loaded selected Camera words stay separate from current
// Main weather and from the retained phase/history owner.
#include "SeasonFrame.h"
#include "WeatherFogFrame.h"
#include <fstream>
#include <cfloat>

namespace source_current_cloud_weather {
using source_camera::A;using source_camera::S;using source_camera::M;using source_camera::D;
using source_camera::V4;using source_weather_fog::F;
struct Profile {uint32_t season{};std::array<uint32_t,0x7a4/4> words{};float planet{};V4 rayleigh{},mie{};};
struct Profiles {std::vector<Profile> seasons;};
inline Profiles readProfiles(const std::filesystem::path& path){
    std::ifstream in(path,std::ios::binary);Profiles result;
    auto word=[&](){uint32_t u{};in.read(reinterpret_cast<char*>(&u),4);if(!in)throw std::runtime_error("Cloud weather source truncated");return u;};
    result.seasons.resize(word());for(auto& p:result.seasons){p.season=word();uint32_t n=word();for(uint32_t i=0;i<n;++i){uint32_t at=word(),bits=word();if(at%4||at>=0x7a4)throw std::runtime_error("Cloud weather source offset");p.words[at/4]=bits;}
        p.planet=F(word());float* r=&p.rayleigh.X;float* m=&p.mie.X;for(int i=0;i<4;++i)r[i]=F(word());for(int i=0;i<4;++i)m[i]=F(word());}
    if(in.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Cloud weather source trailing bytes");return result;
}
inline float mainFloat(const source_current_season::Frame& current,uint32_t offset){return source_current_season::read<float>(current.main,offset);}
inline float clamp(float x){return x>=1?1.f:(0>x?0.f:x);}
inline float weight(float x,float lo,float hi){return hi>lo?clamp(D(S(x,lo),S(hi,lo))):(x>=lo?1.f:0.f);}
inline float size(float x){return x>=FLT_MAX?FLT_MAX:(x>F(0x3a9c4000)?x:F(0x3a9c4000));}
inline float signedClamp(float x){return x>=F(0x3f7fff38)?F(0x3f7fff38):(x>F(0xbf7fff38)?x:F(0xbf7fff38));}
struct Frame {std::array<uint32_t,0x7a4/4> weather{};std::array<float,0x294/4> input{};V4 rayleigh{},mie{};float cloudy{},planetRadius{};};
inline Frame prepareCurrent(const Profiles& profiles,const source_current_season::Frame& current){
    const Profile* selected=nullptr;for(const auto& p:profiles.seasons)if(p.season==current.season){selected=&p;break;}
    if(!selected)throw std::runtime_error("Cloud weather requires loaded actual season");const auto& p=*selected;
    Frame out;out.planetRadius=p.planet;out.weather=p.words;out.cloudy=mainFloat(current,0x1d0);float humidity=mainFloat(current,0x1d8);
    // Main post-selection owns these final quality writes, after the selected
    // Camera's declared values. Program selection stays with its resource owner.
    for(uint32_t offset:{0x674u,0x7a0u}){
        const auto found=std::find_if(current.words.begin(),current.words.end(),[&](auto w){return uint32_t(w.offset)==offset;});
        if(found==current.words.end())throw std::runtime_error("Cloud final Main quality write absent");out.weather[offset/4]=found->bits;
    }
    auto R=[&](int at){return F(out.weather[at/4]);};auto W=[&](int at,float value){std::memcpy(&out.weather[at/4],&value,4);};
    W(0x1d0,out.cloudy);W(0x1d8,humidity);
    float cw=clamp(M(out.cloudy,R(0x644))),complement=S(1,out.cloudy),ew=clamp(A(M(complement,R(0x650)),M(out.cloudy,R(0x654))));
    float hw=weight(humidity,R(0x78c),R(0x790));cw=M(cw,hw);ew=M(ew,hw);
    float coefficient=A(M(humidity,R(0x660)>R(0x65c)?R(0x660):R(0x65c)),M(S(1,humidity),R(0x65c)));
    float h=weight(humidity,R(0x634),1),detail=A(M(S(1,h),R(0x62c)),M(h,R(0x630)));
    float bw=weight(cw,R(0x6e0),R(0x6dc)),dw=weight(cw,R(0x718),R(0x714));
    for(int i=0;i<3;++i){W(0x6b8+i*4,A(M(S(1,bw),R(0x6d0+i*4)),M(bw,R(0x6c4+i*4))));W(0x6f0+i*4,A(M(S(1,dw),R(0x708+i*4)),M(dw,R(0x6fc+i*4))));}
    float cs=clamp(A(M(complement,R(0x74c)),M(out.cloudy,R(0x750)))),dist=clamp(A(M(complement,R(0x758)),M(out.cloudy,R(0x75c)))),hc=weight(humidity,R(0x794),R(0x798));
    W(0x748,M(S(1,hc),cs));W(0x754,M(S(1,hc),dist));W(0x640,cw);W(0x628,detail);W(0x64c,ew);W(0x658,coefficient);
    auto& v=out.input;auto I=[&](int at)->float&{return v[at/4];};
    for(auto literal:std::initializer_list<std::pair<int,uint32_t>>{{0xa8,0x447a0000},{0xb0,0x3f800000},{0x1a0,0},{0x1a4,0},{0x1c0,0},{0x1c4,0},{0x1e0,0x49f42400},{0x1e4,0x49f42400},{0x200,0x49f42400},{0x204,0x49f42400},{0x22c,0x4af42400},{0x230,0xbd75c28f}})I(literal.first)=F(literal.second);
    auto Copy=[&](int target,int source,int n=1){for(int i=0;i<n;++i)I(target+i*4)=R(source+i*4);};
    Copy(0x9c,0x624);Copy(0xb4,0x628);I(0x90)=float(out.weather[0x638/4]);I(0x94)=float(out.weather[0x63c/4]);
    Copy(0x98,0x640);Copy(0xa0,0x648);Copy(0xa4,0x64c);Copy(0xac,0x658);Copy(0xb8,0x67c);Copy(0x260,0x664);Copy(0x264,0x668);I(0x268)=float(out.weather[0x66c/4]);Copy(0x26c,0x670);
    I(0xd8)=mainFloat(current,0x22c);I(0xc8)=signedClamp(R(0x680));I(0xcc)=signedClamp(R(0x684));Copy(0xd0,0x688);Copy(0xd4,0x68c);
    Copy(0xf0,0x690,3);Copy(0x100,0x69c,3);Copy(0x110,0x6a8,3);Copy(0xc0,0x6b4);Copy(0xc4,0x71c);
    Copy(0x130,0x6b8,3);Copy(0x140,0x6e4,3);Copy(0x150,0x6f0,3);Copy(0x168,0x720,2);Copy(0x170,0x728,2);Copy(0x178,0x730,2);Copy(0x180,0x738,3);
    Copy(0x218,0x744,4);Copy(0x198,0x764,2);Copy(0x1a8,0x76c,2);Copy(0x1b8,0x774,2);Copy(0x1c8,0x77c,4);
    for(int at:{0xf0,0xf4,0xf8,0x130,0x134,0x138,0x168,0x16c,0x198,0x19c,0x1b8,0x1bc})I(at)=size(I(at));
    I(0x228)=p.planet;I(0x60)=32;I(0x64)=128;I(0x68)=32;I(0x6c)=8;
    out.rayleigh=source_weather_fog::adjust({M(p.rayleigh.X,.01f),M(p.rayleigh.Y,.01f),M(p.rayleigh.Z,.01f),0},I(0xd8));
    out.rayleigh.W=A(I(0x22c),p.planet);out.mie={M(p.mie.X,.01f),M(p.mie.Y,.01f),M(p.mie.Z,.01f),0};return out;
}
struct Axis {uint32_t phase;int wind,size,position;};
inline constexpr std::array<Axis,16> axes{{
 {0xe0,0x110,0xf0,0x100},{0xe4,0x114,0xf4,0x104},{0xe8,0x118,0xf8,0x108},
 {0x120,0x150,0x130,0x140},{0x124,0x154,0x134,0x144},{0x128,0x158,0x138,0x148},
 {0x160,0x178,0x168,0x170},{0x164,0x17c,0x16c,0x174},{0x190,0x1a8,0x198,0x1a0},{0x194,0x1ac,0x19c,0x1a4},
 {0x1b0,0x1c8,0x1b8,0x1c0},{0x1b4,0x1cc,0x1bc,0x1c4},{0x1d8,0x1f0,0x1e0,0},{0x1dc,0x1f4,0x1e4,0},
 {0x1f8,0x210,0x200,0},{0x1fc,0x214,0x204,0}}};
// These actual named RDEF fields are weather-only. Eye/FOV, litDir, fog,
// Scat, Sun/Moon, previousW2P, Bayer and all texture-offset phases are excluded.
inline const std::array<const char*,20> names{{"g_vBaseTexScale","g_vDetailTexScale","g_vDistortTexScale","g_vCirrusTexScale","g_vCirrusDistortTexScale","g_vRaySampleNum","g_vEarthRadius","g_vCloudParams1","g_vCloudParams2","g_vCloudParams0","g_vDistortScale","g_vCirrusParams","g_vCirrusDistortScale","g_vErodeScale","g_vHgParams0","g_vHgParams1","g_vSky2RayleighExtinction","g_vSky2MieExtinction","g_vStableDim","g_vClampValue"}};
struct LayoutWord {uint32_t field,offset,count;};struct Layout {uint32_t byteCount{};std::vector<LayoutWord> fields;bool work{};};
inline Layout readLayout(const std::filesystem::path& path,bool work){Layout out;out.work=work;std::ifstream in(path);if(!(in>>out.byteCount))throw std::runtime_error("Cloud weather RDEF layout absent");LayoutWord row;while(in>>row.field>>row.offset>>row.count){if(row.field>=names.size()||row.offset+row.count*4>out.byteCount)throw std::runtime_error("Cloud weather RDEF range");out.fields.push_back(row);}return out;}
inline std::array<float,4> field(const Frame& f,uint32_t index,bool work,float clampValue){
    auto R=[&](int at){return f.input[at/4];};
    if(index<5){int base=std::array<int,5>{0xf0,0x130,0x168,0x198,0x1b8}[index],n=index<2?3:2;std::array<float,4> out{};for(int i=0;i<n;++i)out[i]=D(1,R(base+i*4));return out;}
    float bottom=A(R(0x9c),R(0x228)),h0=M(R(0xc8),R(0xc8)),h1=M(R(0xcc),R(0xcc));
    switch(index){case 5:return {S(R(0x90),R(0x94)),R(0x94),0,0}; //w is the camera's source FOV, never overwritten here.
      case 6:return {R(0x228),bottom,A(bottom,R(0xa0)),A(R(0x218),R(0x228))};
      case 7:return {S(1,R(0x98)),R(0xac),R(0xb4),D(R(0xa4),R(0xa8))};
      case 8:return {D(R(0xb8),4),M(R(0xb8),2.5f),D(1,R(0xa8)),R(0xb0)};
      case 9:return {R(0x9c),R(0xa0),D(1,R(0xa0)),R(0x22c)};
      case 10:return {M(D(1,R(0x130)),R(0x180)),M(D(1,R(0x134)),R(0x184)),M(D(1,R(0x138)),R(0x188)),R(0xd8)};
      case 11:return {R(0x218),S(1,R(0x21c)),R(0x220),R(0x224)};
      case 12:return {M(R(0x1d0),D(1,R(0x198))),M(R(0x1d4),D(1,R(0x19c))),0,0};
      case 13:return {R(0xc0),R(0xc4),0,0};
      case 14:return {M(D(S(1,h0),F(0x41490fdb)),R(0xd4)),M(R(0xc8),-2),A(h0,1),R(0xd0)};
      case 15:return {M(D(S(1,h1),F(0x41490fdb)),R(0xd4)),M(R(0xcc),-2),A(h1,1),0};
      case 16:return work?std::array<float,4>{0,0,0,f.rayleigh.W}:std::array<float,4>{f.rayleigh.X,f.rayleigh.Y,f.rayleigh.Z,f.rayleigh.W};
      case 17:return work?std::array<float,4>{0,0,0,0}:std::array<float,4>{f.mie.X,f.mie.Y,f.mie.Z,f.mie.W};
      case 18:return {32,128,32,8};case 19:return {0,0,0,clampValue};default:throw std::runtime_error("Cloud weather field");}
}
inline void publish(const Frame& frame,const Layout& layout,std::span<char> bytes,float clampValue){
    if(bytes.size()!=layout.byteCount)throw std::runtime_error("Cloud weather loaded CB differs from actual RDEF");
    for(const auto& row:layout.fields){auto value=field(frame,row.field,layout.work,clampValue);uint32_t n=row.field==5?3:row.count;
        std::memcpy(bytes.data()+row.offset,value.data(),n*4);}
}
} // namespace source_current_cloud_weather

#ifndef SOURCE_CURRENT_CLOUD_WEATHER_CPU_ONLY
struct NativeCurrentCloudWeather {source_current_cloud_weather::Profiles profiles;source_current_cloud_weather::Layout main,work;};
static NativeCurrentCloudWeather readNativeCurrentCloudWeather(const fs::path& source){return {source_current_cloud_weather::readProfiles(source/"profiles.bin"),source_current_cloud_weather::readLayout(source/"main-layout.txt",false),source_current_cloud_weather::readLayout(source/"work-layout.txt",true)};}
static source_current_cloud_weather::Frame publishNativeCurrentCloudWeatherBeforeClock(const NativeCurrentCloudWeather& source,
    const source_current_season::Frame& current,NativeMainCloud& cloud,NativeAmbientCloudContinuous& work){
    auto frame=source_current_cloud_weather::prepareCurrent(source.profiles,current);cloud.cloudy=frame.cloudy;
    for(auto& axis:cloud.axes)for(const auto& input:source_current_cloud_weather::axes)if(axis.sourcePhase==input.phase){axis.wind=frame.input[input.wind/4];axis.period=frame.input[input.size/4];axis.position=input.position?frame.input[input.position/4]:0;axis.scale=source_camera::D(1,axis.period);}
    for(int i=0;i<3;++i){cloud.baseWind[i]=frame.input[(0x110+i*4)/4];cloud.basePeriod[i]=frame.input[(0xf0+i*4)/4];}
    for(auto& axis:work.axes){const auto& shared=cloud.axes[axis.sharedIndex];axis.position=shared.position;axis.scale=shared.scale;}
    // Current278/Controller1F0 is already written by the atmosphere owner.
    const float value=source_camera::M(source_current_cloud_weather::mainFloat(current,0x278),100);
    for(auto& pass:cloud.passes)if(pass.name=="pass1")source_current_cloud_weather::publish(frame,source.main,pass.pbytes,value);
    for(auto& bytes:work.pixelBytes)source_current_cloud_weather::publish(frame,source.work,bytes,value);
    // No phase, pendingVector, previous camera, Bayer, side or history mutation.
    return frame;
}
#endif
