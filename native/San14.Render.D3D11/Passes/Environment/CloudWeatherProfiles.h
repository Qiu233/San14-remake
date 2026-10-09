#pragma once
#include "CloudWeatherInputs.h"
#include "WeatherFogFrame.h"
#include <fstream>

namespace source_current_cloud_weather {
using source_camera::V4;
struct Profile {
    uint32_t season{};
    float planetRadius{},bottomAltitude{},thickness{};
    uint32_t raySamples{},minimumRaySamples{};
    Endpoints detail{},erosion{},density{},cirrusCoverage{};
    float humidDetailThreshold{},cloudCoverageFactor{},sampleStep{},baseErosion{},detailErosion{};
    Range humidityResponse{},cirrusHumidityResponse{},detailPeriodResponse{},detailVelocityResponse{};
    std::array<float,2> hgAsymmetry{};
    float hgWeight{},hgIntensity{};
    NoiseLayer<3> base;
    std::array<float,3> clearDetailPeriod{},cloudyDetailPeriod{},detailPosition{},
        clearDetailVelocity{},cloudyDetailVelocity{},distortAmplitude{};
    NoiseLayer<2> distort,cirrus,cirrusDistort;
    std::array<float,2> cirrusDistortAmplitude{};
    float cirrusAltitude{};
    V4 rayleigh{},mie{};
};
struct Profiles {std::vector<Profile> seasons;};
inline Profiles readProfiles(const std::filesystem::path& path){
    std::ifstream in(path,std::ios::binary);Profiles result;
    auto word=[&](){uint32_t u{};in.read(reinterpret_cast<char*>(&u),4);
        if(!in)throw std::runtime_error("Cloud weather source truncated");return u;};
    result.seasons.resize(word());
    for(auto& p:result.seasons){
        p.season=word();
        // Sparse source storage exists only in this decoder. Zero defaults,
        // ordered duplicate writes and range rejection retain the file contract.
        std::array<uint32_t,0x7a4/4> words{};
        uint32_t count=word();for(uint32_t i=0;i<count;++i){uint32_t at=word(),bits=word();
            if(at%4||at>=0x7a4)throw std::runtime_error("Cloud weather source offset");words[at/4]=bits;}
        auto scalar=[&](size_t at){return source_weather_fog::F(words[at/4]);};
        auto vector=[&]<size_t N>(size_t at,std::array<float,N>& values){
            for(size_t i=0;i<N;++i)values[i]=scalar(at+i*4);};
        p.bottomAltitude=scalar(0x624);p.detail={scalar(0x62c),scalar(0x630)};
        p.humidDetailThreshold=scalar(0x634);p.raySamples=words[0x638/4];p.minimumRaySamples=words[0x63c/4];
        p.cloudCoverageFactor=scalar(0x644);p.thickness=scalar(0x648);
        p.erosion={scalar(0x650),scalar(0x654)};p.density={scalar(0x65c),scalar(0x660)};
        p.sampleStep=scalar(0x67c);vector(0x680,p.hgAsymmetry);p.hgWeight=scalar(0x688);p.hgIntensity=scalar(0x68c);
        vector(0x690,p.base.period);vector(0x69c,p.base.position);vector(0x6a8,p.base.velocity);
        p.baseErosion=scalar(0x6b4);vector(0x6c4,p.cloudyDetailPeriod);vector(0x6d0,p.clearDetailPeriod);
        p.detailPeriodResponse={scalar(0x6e0),scalar(0x6dc)};vector(0x6e4,p.detailPosition);
        vector(0x6fc,p.cloudyDetailVelocity);vector(0x708,p.clearDetailVelocity);
        p.detailVelocityResponse={scalar(0x718),scalar(0x714)};p.detailErosion=scalar(0x71c);
        vector(0x720,p.distort.period);vector(0x728,p.distort.position);vector(0x730,p.distort.velocity);
        vector(0x738,p.distortAmplitude);p.cirrusAltitude=scalar(0x744);
        p.cirrusCoverage={scalar(0x74c),scalar(0x750)};
        vector(0x764,p.cirrus.period);vector(0x76c,p.cirrus.velocity);
        vector(0x774,p.cirrusDistort.period);vector(0x77c,p.cirrusDistort.velocity);
        vector(0x784,p.cirrusDistortAmplitude);
        p.humidityResponse={scalar(0x78c),scalar(0x790)};
        p.cirrusHumidityResponse={scalar(0x794),scalar(0x798)};
        p.planetRadius=source_weather_fog::F(word());
        float* r=&p.rayleigh.X;float* m=&p.mie.X;
        for(int i=0;i<4;++i)r[i]=source_weather_fog::F(word());
        for(int i=0;i<4;++i)m[i]=source_weather_fog::F(word());
    }
    if(in.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Cloud weather source trailing bytes");
    return result;
}
} // namespace source_current_cloud_weather
