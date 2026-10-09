#pragma once
#include "SceneEnvironment.h"
#include "../Passes/Environment/SeasonFrame.h"

namespace San14::Render::SceneEnvironmentPacket {
template<class T> inline T read(std::span<const uint8_t> bytes,size_t offset){
    T value;std::memcpy(&value,bytes.data()+offset,sizeof(value));return value;
}
inline SceneCameraInputs decodeCamera(const source_camera::Prefix& packet){
    SceneCameraInputs camera;
    camera.eye=read<std::array<float,4>>(packet,0x10);
    camera.position=read<std::array<float,3>>(packet,0x20);
    camera.ordinary=(read<uint32_t>(packet,0x158)&1)==0;
    camera.weatherHeight=read<float>(packet,0x210);
    camera.exposure=read<float>(packet,0x170);
    camera.luminance=read<float>(packet,0x17C);
    return camera;
}
inline void publishWord(tree2_source::WindEnvironment& vegetation,uint32_t& exposureFlags,int offset,uint32_t bits){
    float value;std::memcpy(&value,&bits,4);
    switch(offset){
    case 0xDC0:case 0xDC4:case 0xDC8:case 0xDCC:
        vegetation.directionQuaternion[size_t(offset-0xDC0)/4]=value;break;
    case 0xDD0:vegetation.generatorParameters.baseStrength=value;break;
    case 0xDD4:vegetation.generatorParameters.strengthVariation=value;break;
    case 0xDD8:vegetation.generatorParameters.riseSeconds=value;break;
    case 0xDDC:vegetation.generatorParameters.fallSeconds=value;break;
    case 0xDE0:vegetation.generatorParameters.peakSeconds=value;break;
    case 0xDE4:vegetation.generatorParameters.minimumStrength=value;break;
    case 0xDE8:vegetation.generatorParameters.restSeconds=value;break;
    case 0xDEC:vegetation.generatorParameters.restVariation=value;break;
    case 0xDF4:vegetation.firstJitter=value;break;
    case 0xDF8:vegetation.secondJitter=value;break;
    case 0x141C:vegetation.interactionExtent=value;break;
    case 0x1438:vegetation.windExtent=value;break;
    case 0x143C:vegetation.strengthScale=value;break;
    case 0x1510:exposureFlags=bits;break;
    }
}
// MAINNUM1 remains a prepared-media interchange version. Its unused shadow
// words are validated for compatibility but never become runtime scene state.
inline InitialSceneEnvironment decodeInitial(std::span<const char> packet){
    constexpr size_t prefixOffset=8,prefixBytes=0x3C0-prefixOffset,headerBytes=20;
    constexpr std::array<uint32_t,37> offsets{
        0x9EC,0x9F0,0x9F4,0x9F8,0x9FC,0xA00,0xA04,0xA08,0xA10,0xA18,0xA20,
        0xA28,0xA2C,0xA38,0xA3C,0xA40,0xA84,0xA88,
        0xB1C,0xDC0,0xDC4,0xDC8,0xDCC,0xDD0,0xDD4,0xDD8,0xDDC,0xDE0,0xDE4,0xDE8,0xDEC,
        0xDF4,0xDF8,0x141C,0x1438,0x143C,0x1510};
    if(packet.size()!=headerBytes+prefixBytes+offsets.size()*8||std::memcmp(packet.data(),"MAINNUM1",8)!=0)
        throw std::runtime_error("Initial scene environment packet format");
    const auto bytes=std::span(reinterpret_cast<const uint8_t*>(packet.data()),packet.size());
    if(read<uint32_t>(bytes,8)!=prefixOffset||read<uint32_t>(bytes,12)!=prefixBytes||read<uint32_t>(bytes,16)!=offsets.size())
        throw std::runtime_error("Initial scene environment packet schema");
    InitialSceneEnvironment initial;
    initial.vegetation.cameraPosition=read<std::array<float,3>>(bytes,headerBytes+0x20-prefixOffset);
    initial.vegetation.ordinaryCamera=(read<uint32_t>(bytes,headerBytes+0x158-prefixOffset)&1)==0;
    size_t cursor=headerBytes+prefixBytes;
    for(uint32_t offset:offsets){
        if(read<uint32_t>(bytes,cursor)!=offset)throw std::runtime_error("Initial scene environment field order");
        uint32_t bits=read<uint32_t>(bytes,cursor+4);
        if(offset==0xB1C)std::memcpy(&initial.territoryReferenceHeight,&bits,4);
        publishWord(initial.vegetation,initial.exposureRectangleFlags,int(offset),bits);cursor+=8;
    }
    return initial;
}
inline void acceptSeason(SceneEnvironment& environment,const source_current_season::Frame& season){
    auto vegetation=environment.vegetation();uint32_t exposureFlags=environment.exposureRectangleFlags();
    for(const auto& word:season.words){
        if(word.offset<0||size_t(word.offset)+4>0x18C0)throw std::runtime_error("Current season environment word offset");
        publishWord(vegetation,exposureFlags,word.offset,word.bits);
    }
    environment.acceptCamera(season.main,decodeCamera(season.main));
    environment.acceptVegetation(vegetation,exposureFlags);
}
} // namespace San14::Render::SceneEnvironmentPacket
