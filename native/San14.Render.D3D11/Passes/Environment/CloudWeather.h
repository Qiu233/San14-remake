#pragma once
// Weather response is evaluated before the shared cloud animation clock.
// Profiles and accepted conditions are ordinary values; binary layouts appear
// only in their importers and in the reflected constant-buffer publisher.
#include "SeasonFrame.h"
#include "CloudWeatherProfiles.h"
#include <cfloat>

namespace source_current_cloud_weather {
using source_camera::A;using source_camera::S;using source_camera::M;using source_camera::D;
using source_weather_fog::F;
inline const float MinimumNoisePeriod=F(0x3a9c4000);
inline const float MaximumHgAsymmetry=F(0x3f7fff38);
inline const float MinimumHgAsymmetry=F(0xbf7fff38);
inline const float FourPi=F(0x41490fdb);
inline constexpr float DensityDistance=1000,AtmosphereHeight=8000000,ReservedNoisePeriod=2000000;
inline float clamp(float x){return x>=1?1.f:(0>x?0.f:x);}
inline float weight(float x,Range range){return range.upper>range.lower?
    clamp(D(S(x,range.lower),S(range.upper,range.lower))):(x>=range.lower?1.f:0.f);}
inline float size(float x){return x>=FLT_MAX?FLT_MAX:(x>MinimumNoisePeriod?x:MinimumNoisePeriod);}
inline float signedClamp(float x){return x>=MaximumHgAsymmetry?MaximumHgAsymmetry:
    (x>MinimumHgAsymmetry?x:MinimumHgAsymmetry);}
inline float blend(Endpoints values,float factor){return A(M(S(1,factor),values.clear),M(factor,values.cloudy));}
template<size_t N> inline void clampPeriods(NoiseLayer<N>& layer){for(auto& period:layer.period)period=size(period);}
struct Frame {
    float cloudy{},planetRadius{},bottomAltitude{},thickness{},raySamples{},minimumRaySamples{};
    float coverage{},density{},detailAmount{},erosion{},sampleStep{},baseErosion{},detailErosion{};
    std::array<float,2> hgAsymmetry{};float hgWeight{},hgIntensity{},saturation{};
    NoiseLayer<3> base,detail;
    NoiseLayer<2> distort,cirrus,cirrusDistort;
    std::array<float,3> distortAmplitude{};
    std::array<float,2> cirrusDistortAmplitude{};
    float cirrusAltitude{},cirrusCoverage{};
    Endpoints cirrusCoverageResponse;
    V4 rayleigh{},mie{};
};
inline Frame prepareCurrent(const Profiles& profiles,uint32_t season,const Conditions& current){
    const Profile* selected=nullptr;for(const auto& profile:profiles.seasons)
        if(profile.season==season){selected=&profile;break;}
    if(!selected)throw std::runtime_error("Cloud weather requires loaded actual season");
    const auto& p=*selected;
    if(!current.cloudQualityPublished||!current.ambientQualityPublished)
        throw std::runtime_error("Cloud final Main quality write absent");
    Frame out;out.cloudy=current.cloudiness;out.planetRadius=p.planetRadius;
    const float humidity=current.humidity,complement=S(1,out.cloudy);
    float cw=clamp(M(out.cloudy,p.cloudCoverageFactor));
    float ew=clamp(A(M(complement,p.erosion.clear),M(out.cloudy,p.erosion.cloudy)));
    float hw=weight(humidity,p.humidityResponse);cw=M(cw,hw);ew=M(ew,hw);
    out.coverage=cw;out.erosion=ew;
    out.density=A(M(humidity,p.density.cloudy>p.density.clear?p.density.cloudy:p.density.clear),
        M(S(1,humidity),p.density.clear));
    float h=weight(humidity,{p.humidDetailThreshold,1});out.detailAmount=blend(p.detail,h);
    float periodWeight=weight(cw,p.detailPeriodResponse),velocityWeight=weight(cw,p.detailVelocityResponse);
    out.base=p.base;out.detail.position=p.detailPosition;
    for(size_t i=0;i<3;++i){out.detail.period[i]=blend({p.clearDetailPeriod[i],p.cloudyDetailPeriod[i]},periodWeight);
        out.detail.velocity[i]=blend({p.clearDetailVelocity[i],p.cloudyDetailVelocity[i]},velocityWeight);}
    out.distort=p.distort;out.cirrus=p.cirrus;out.cirrusDistort=p.cirrusDistort;
    clampPeriods(out.base);clampPeriods(out.detail);clampPeriods(out.distort);
    clampPeriods(out.cirrus);clampPeriods(out.cirrusDistort);
    out.distortAmplitude=p.distortAmplitude;out.cirrusDistortAmplitude=p.cirrusDistortAmplitude;
    float coverage=clamp(A(M(complement,p.cirrusCoverage.clear),M(out.cloudy,p.cirrusCoverage.cloudy)));
    float hc=weight(humidity,p.cirrusHumidityResponse);
    out.cirrusCoverage=M(S(1,hc),coverage);out.cirrusCoverageResponse=p.cirrusCoverage;
    out.bottomAltitude=p.bottomAltitude;out.thickness=p.thickness;
    out.raySamples=float(p.raySamples);out.minimumRaySamples=float(p.minimumRaySamples);
    out.sampleStep=p.sampleStep;out.baseErosion=p.baseErosion;out.detailErosion=p.detailErosion;
    out.hgAsymmetry={signedClamp(p.hgAsymmetry[0]),signedClamp(p.hgAsymmetry[1])};
    out.hgWeight=p.hgWeight;out.hgIntensity=p.hgIntensity;out.saturation=current.saturation;
    out.cirrusAltitude=p.cirrusAltitude;
    out.rayleigh=source_weather_fog::adjust({M(p.rayleigh.X,.01f),M(p.rayleigh.Y,.01f),M(p.rayleigh.Z,.01f),0},current.saturation);
    out.rayleigh.W=A(AtmosphereHeight,p.planetRadius);
    out.mie={M(p.mie.X,.01f),M(p.mie.Y,.01f),M(p.mie.Z,.01f),0};
    return out;
}
inline Frame prepareCurrent(const Profiles& profiles,const source_current_season::Frame& current){
    return prepareCurrent(profiles,current.season,current.cloudWeather);
}
struct AnimationInput {float velocity{},period{},position{};};
template<size_t N> inline AnimationInput animationInput(const NoiseLayer<N>& layer,size_t axis){
    return {layer.velocity[axis],layer.period[axis],layer.position[axis]};
}
inline AnimationInput animationInput(const Frame& frame,AnimationChannel channel){
    switch(channel){
        case AnimationChannel::BaseX:return animationInput(frame.base,0);
        case AnimationChannel::BaseY:return animationInput(frame.base,1);
        case AnimationChannel::BaseZ:return animationInput(frame.base,2);
        case AnimationChannel::DetailX:return animationInput(frame.detail,0);
        case AnimationChannel::DetailY:return animationInput(frame.detail,1);
        case AnimationChannel::DetailZ:return animationInput(frame.detail,2);
        case AnimationChannel::DistortX:return animationInput(frame.distort,0);
        case AnimationChannel::DistortY:return animationInput(frame.distort,1);
        case AnimationChannel::CirrusX:return animationInput(frame.cirrus,0);
        case AnimationChannel::CirrusY:return animationInput(frame.cirrus,1);
        case AnimationChannel::CirrusDistortX:return animationInput(frame.cirrusDistort,0);
        case AnimationChannel::CirrusDistortY:return animationInput(frame.cirrusDistort,1);
        default:return {0,ReservedNoisePeriod,0};
    }
}
// These reflected fields belong to weather. Camera/light/history fields are
// independently published and must survive this partial buffer update.
enum class ShaderField : uint32_t {BaseTexScale,DetailTexScale,DistortTexScale,CirrusTexScale,
    CirrusDistortTexScale,RaySampleNum,EarthRadius,CloudParams1,CloudParams2,CloudParams0,
    DistortScale,CirrusParams,CirrusDistortScale,ErodeScale,HgParams0,HgParams1,
    RayleighExtinction,MieExtinction,StableDim,ClampValue};
inline const std::array<const char*,20> names{{"g_vBaseTexScale","g_vDetailTexScale","g_vDistortTexScale","g_vCirrusTexScale","g_vCirrusDistortTexScale","g_vRaySampleNum","g_vEarthRadius","g_vCloudParams1","g_vCloudParams2","g_vCloudParams0","g_vDistortScale","g_vCirrusParams","g_vCirrusDistortScale","g_vErodeScale","g_vHgParams0","g_vHgParams1","g_vSky2RayleighExtinction","g_vSky2MieExtinction","g_vStableDim","g_vClampValue"}};
struct LayoutWord {ShaderField field;uint32_t offset,count;};
struct Layout {uint32_t byteCount{};std::vector<LayoutWord> fields;bool work{};};
inline Layout readLayout(const std::filesystem::path& path,bool work){
    Layout out;out.work=work;std::ifstream in(path);
    if(!(in>>out.byteCount))throw std::runtime_error("Cloud weather RDEF layout absent");
    uint32_t field,offset,count;while(in>>field>>offset>>count){
        if(field>=names.size()||offset+count*4>out.byteCount)throw std::runtime_error("Cloud weather RDEF range");
        out.fields.push_back({static_cast<ShaderField>(field),offset,count});}
    return out;
}
template<size_t N> inline std::array<float,4> inversePeriods(const NoiseLayer<N>& layer){
    std::array<float,4> result{};for(size_t i=0;i<N;++i)result[i]=D(1,layer.period[i]);return result;
}
inline std::array<float,4> field(const Frame& f,ShaderField index,bool work,float clampValue){
    // Keep float32 evaluation order, including the two anisotropy squares.
    const float bottom=A(f.bottomAltitude,f.planetRadius),h0=M(f.hgAsymmetry[0],f.hgAsymmetry[0]),
        h1=M(f.hgAsymmetry[1],f.hgAsymmetry[1]);
    switch(index){
      case ShaderField::BaseTexScale:return inversePeriods(f.base);
      case ShaderField::DetailTexScale:return inversePeriods(f.detail);
      case ShaderField::DistortTexScale:return inversePeriods(f.distort);
      case ShaderField::CirrusTexScale:return inversePeriods(f.cirrus);
      case ShaderField::CirrusDistortTexScale:return inversePeriods(f.cirrusDistort);
      case ShaderField::RaySampleNum:return {S(f.raySamples,f.minimumRaySamples),f.minimumRaySamples,0,0};
      case ShaderField::EarthRadius:return {f.planetRadius,bottom,A(bottom,f.thickness),A(f.cirrusAltitude,f.planetRadius)};
      case ShaderField::CloudParams1:return {S(1,f.coverage),f.density,f.detailAmount,D(f.erosion,DensityDistance)};
      case ShaderField::CloudParams2:return {D(f.sampleStep,4),M(f.sampleStep,2.5f),D(1,DensityDistance),1};
      case ShaderField::CloudParams0:return {f.bottomAltitude,f.thickness,D(1,f.thickness),AtmosphereHeight};
      case ShaderField::DistortScale:return {M(D(1,f.detail.period[0]),f.distortAmplitude[0]),
        M(D(1,f.detail.period[1]),f.distortAmplitude[1]),M(D(1,f.detail.period[2]),f.distortAmplitude[2]),f.saturation};
      case ShaderField::CirrusParams:return {f.cirrusAltitude,S(1,f.cirrusCoverage),
        f.cirrusCoverageResponse.clear,f.cirrusCoverageResponse.cloudy};
      case ShaderField::CirrusDistortScale:return {M(f.cirrusDistortAmplitude[0],D(1,f.cirrus.period[0])),
        M(f.cirrusDistortAmplitude[1],D(1,f.cirrus.period[1])),0,0};
      case ShaderField::ErodeScale:return {f.baseErosion,f.detailErosion,0,0};
      case ShaderField::HgParams0:return {M(D(S(1,h0),FourPi),f.hgIntensity),M(f.hgAsymmetry[0],-2),A(h0,1),f.hgWeight};
      case ShaderField::HgParams1:return {M(D(S(1,h1),FourPi),f.hgIntensity),M(f.hgAsymmetry[1],-2),A(h1,1),0};
      case ShaderField::RayleighExtinction:return work?std::array<float,4>{0,0,0,f.rayleigh.W}:
        std::array<float,4>{f.rayleigh.X,f.rayleigh.Y,f.rayleigh.Z,f.rayleigh.W};
      case ShaderField::MieExtinction:return work?std::array<float,4>{0,0,0,0}:
        std::array<float,4>{f.mie.X,f.mie.Y,f.mie.Z,f.mie.W};
      case ShaderField::StableDim:return {32,128,32,8};
      case ShaderField::ClampValue:return {0,0,0,clampValue};
      default:throw std::runtime_error("Cloud weather field");
    }
}
inline void publish(const Frame& frame,const Layout& layout,std::span<char> bytes,float clampValue){
    if(bytes.size()!=layout.byteCount)throw std::runtime_error("Cloud weather loaded CB differs from actual RDEF");
    for(const auto& row:layout.fields){auto value=field(frame,row.field,layout.work,clampValue);
        // RaySampleNum.w is owned by the camera FOV publisher.
        uint32_t n=row.field==ShaderField::RaySampleNum?3:row.count;
        std::memcpy(bytes.data()+row.offset,value.data(),n*4);}
}
} // namespace source_current_cloud_weather

#ifndef SOURCE_CURRENT_CLOUD_WEATHER_CPU_ONLY
struct NativeCurrentCloudWeather {source_current_cloud_weather::Profiles profiles;source_current_cloud_weather::Layout main,work;};
static NativeCurrentCloudWeather readNativeCurrentCloudWeather(const fs::path& source){return {source_current_cloud_weather::readProfiles(source/"profiles.bin"),source_current_cloud_weather::readLayout(source/"main-layout.txt",false),source_current_cloud_weather::readLayout(source/"work-layout.txt",true)};}
static source_current_cloud_weather::Frame publishNativeCurrentCloudWeatherBeforeClock(const NativeCurrentCloudWeather& source,
    const source_current_season::Frame& current,NativeMainCloud& cloud,NativeAmbientCloudContinuous& work){
    auto frame=source_current_cloud_weather::prepareCurrent(source.profiles,current);cloud.cloudy=frame.cloudy;
    for(auto& axis:cloud.axes)if(axis.weatherChannel){
        const auto input=source_current_cloud_weather::animationInput(frame,*axis.weatherChannel);
        axis.wind=input.velocity;axis.period=input.period;axis.position=input.position;axis.scale=source_camera::D(1,axis.period);}
    cloud.baseWind=frame.base.velocity;cloud.basePeriod=frame.base.period;
    for(auto& axis:work.axes){const auto& shared=cloud.axes[axis.sharedIndex];axis.position=shared.position;axis.scale=shared.scale;}
    // Current clamp is supplied by the atmosphere owner, before this publication.
    const float value=source_camera::M(current.cloudWeather.clampValue,100);
    for(auto& pass:cloud.passes)if(pass.name=="pass1")source_current_cloud_weather::publish(frame,source.main,pass.pbytes,value);
    for(auto& bytes:work.pixelBytes)source_current_cloud_weather::publish(frame,source.work,bytes,value);
    // No phase, pendingVector, previous camera, Bayer, side or history mutation.
    return frame;
}
#endif
