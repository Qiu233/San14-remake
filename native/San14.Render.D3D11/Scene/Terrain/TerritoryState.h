#pragma once
// Original CE 18CA70 ScreenMap style writer and original 3F6EB0 camera inverse.
// The caller owns current display controls. No fitted camera, screenshot color,
// frame ordinal, original-process pointer, or per-request file access is used.
#include "../../Camera/CameraMatrix.h"
#include <array>
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <stdexcept>

namespace source_territory {
// Actual Map initializer CE1895D0 calls CE8820(...,8), replacing the
// ScreenMap constructor's1078=2. CE8250 also initializes1080=true.
constexpr std::uint32_t initializedStencilMask=8;
constexpr bool initializedWaterTerritoryEnabled=true;
// EB0780 keeps this raster/stencil selector separate from shader107C.
constexpr std::uint32_t drawStencilReference(std::uint32_t providerDrawFlags,std::uint32_t screenMap1078) {
    return (providerDrawFlags&2)?screenMap1078|0x50u:screenMap1078;
}
constexpr unsigned byteCount=6576, paletteCount=128, layerCount=7;
using Vector=std::array<float,4>;
using Constants=std::array<std::uint8_t,byteCount>;
enum Table {FillAlpha,Width0,BorderAlpha,FrameWidth,FrameAlpha,Width6,LineAlpha19,
    CellAlpha11,Width1,LineAlpha11,Style1Width0,Width45,SpecialWidth45,Curvature,ZoomValues,TableCount};
struct Profile {
    std::uint32_t typedBufferFormat=0,typedBufferElementBytes=0;
    std::array<std::array<std::int32_t,7>,TableCount> tables{};
    std::array<std::array<float,3>,4> rgb{};
    float frameCutoff=0;
    Constants constructor{};
    std::array<Vector,paletteCount> baseCell{},baseLine{};
    std::array<std::int32_t,paletteCount> allocatedColor{};
};
struct Controls {std::int32_t styleSelector=0,width4Selector=0,width5Selector=-1;
    bool hideAllocatedFill=false;std::uint32_t screenMapMask=6,providerFlags=0;};
struct Selection {std::int32_t band=0,fraction=0;double distance=0;Controls controls;};
struct Owner {Constants constants{};};
inline Profile loadProfile(const std::filesystem::path& directory) {
    std::ifstream input(directory/"profile.bin",std::ios::binary);
    if(!input)throw std::runtime_error("Original ScreenMap source profile is absent");
    auto read=[&](void* value,std::size_t bytes){input.read(static_cast<char*>(value),bytes);
        if(!input)throw std::runtime_error("Original ScreenMap source profile is incomplete");};
    std::uint32_t magic;read(&magic,4);
    if(magic!=0x33565450u)throw std::runtime_error("ScreenMap PTV3 style profile layout differs");
    Profile result;read(&result.typedBufferFormat,4);read(&result.typedBufferElementBytes,4);
    if(result.typedBufferFormat!=62||result.typedBufferElementBytes!=1)
        throw std::runtime_error("Original ScreenMap format36 is outside R8_UINT/1B");
    read(result.tables.data(),sizeof(result.tables));read(result.rgb.data(),sizeof(result.rgb));
    read(&result.frameCutoff,4);read(result.constructor.data(),result.constructor.size());
    read(result.baseCell.data(),sizeof(result.baseCell));read(result.baseLine.data(),sizeof(result.baseLine));
    read(result.allocatedColor.data(),sizeof(result.allocatedColor));
    return result;
}
inline Owner construct(const Profile& source) {return {source.constructor};}
inline void writeFloat(Constants& bytes,unsigned at,float value){std::memcpy(bytes.data()+at,&value,4);}
inline void writeWord(Constants& bytes,unsigned at,std::uint32_t value){std::memcpy(bytes.data()+at,&value,4);}
inline void writeVector(Constants& bytes,unsigned at,const Vector& value){std::memcpy(bytes.data()+at,value.data(),16);}
inline float readFloat(const Constants& bytes,unsigned at){float value;std::memcpy(&value,bytes.data()+at,4);return value;}
// Retain the original float32 multiply/add ordering under normal MSVC /fp:precise.
__declspec(noinline) inline float product(float a,float b){return a*b;}
__declspec(noinline) inline float sum(float a,float b){return a+b;}
inline float interpolate(const Profile& source,Table table,const Selection& selection) {
    if(selection.band==6)return float(source.tables[table][6]);
    float fraction=selection.fraction/10000.f;
    return sum(product(float(source.tables[table][selection.band]),1.f-fraction),
        product(float(source.tables[table][selection.band+1]),fraction));
}
inline void writeCurrent(const Profile& source,Owner& owner,const Selection& selection,
    const std::array<std::uint8_t,0x3c0>& cameraPrefix,float currentMainB1C) {
    if(selection.band<0||selection.band>6||selection.fraction<0||selection.fraction>9999)
        throw std::runtime_error("Original ScreenMap needs the actual seven-band controller selection");
    auto& bytes=owner.constants;const auto& controls=selection.controls;
    source_matrix::Matrix view{},worldToProjection{};
    std::memcpy(view.data(),cameraPrefix.data()+0x80,64);
    std::memcpy(worldToProjection.data(),cameraPrefix.data()+0x100,64);
    auto viewToWorld=source_matrix::inverse(view),projectionToWorld=source_matrix::inverse(worldToProjection);
    std::memcpy(bytes.data(),viewToWorld.data(),64);
    std::memcpy(bytes.data()+64,projectionToWorld.data(),64);
    std::memcpy(bytes.data()+128,view.data(),64);
    writeFloat(bytes,192+8,currentMainB1C-500.f);
    auto value=[&](Table table){return interpolate(source,table,selection);};
    float fill=controls.styleSelector==3?1.f:value(FillAlpha)/100000.f;
    float cell11=value(CellAlpha11)/100000.f;
    if(fill<.05f)fill=0;if(cell11<.05f)cell11=0;
    float border=value(BorderAlpha)/100000.f,line11=value(LineAlpha11)/100000.f;
    for(unsigned slot=25;slot<128;slot++) {
        if(source.allocatedColor[slot]==60)break;
        auto cell=source.baseCell[slot];cell[3]=controls.hideAllocatedFill?0:fill;
        Vector line=controls.hideAllocatedFill?Vector{.25f,.25f,.25f,border}:Vector{cell[0],cell[1],cell[2],border};
        writeVector(bytes,208+slot*16,cell);writeVector(bytes,2256+slot*16,line);
    }
    float frameWidth=value(FrameWidth);if(source.frameCutoff>frameWidth)frameWidth=0;
    writeFloat(bytes,4320,frameWidth/100.f);
    writeFloat(bytes,4320+16,value(controls.styleSelector==1?Style1Width0:Width0));
    writeFloat(bytes,4320+32,value(Width1)/100.f);
    writeFloat(bytes,4320+5*16,value(controls.width4Selector==10?SpecialWidth45:Width45));
    writeFloat(bytes,4320+6*16,value(controls.width5Selector==10?SpecialWidth45:Width45));
    writeFloat(bytes,4320+7*16,value(Width6)/100.f);
    writeVector(bytes,208+11*16,{source.rgb[1][0],source.rgb[1][1],source.rgb[1][2],cell11});
    writeVector(bytes,2256+11*16,{source.rgb[2][0],source.rgb[2][1],source.rgb[2][2],line11});
    for(unsigned slot=19;slot<24;slot++)writeVector(bytes,2256+slot*16,
        {source.rgb[3][0],source.rgb[3][1],source.rgb[3][2],value(LineAlpha19)/100000.f});
    writeVector(bytes,208+15*16,{1,1,1,selection.band==0?0.f:5.f/255.f});
    auto quantized=static_cast<std::uint8_t>(static_cast<std::int32_t>(product(line11,255.f)));
    writeVector(bytes,2256+15*16,{source.rgb[2][0],source.rgb[2][1],source.rgb[2][2],quantized/255.f});
    writeVector(bytes,4304,{source.rgb[0][0],source.rgb[0][1],source.rgb[0][2],value(FrameAlpha)/100000.f});
    writeFloat(bytes,4512,value(Curvature)/10.f);
    // CE8160 copies ScreenMap107C into the shader packet. EB0780's
    // providerFlags bit2 modifies only the independent ScreenMap1078
    // stencil reference passed to E726E0, never this shader layer mask.
    std::uint32_t mask=controls.screenMapMask;
    writeWord(bytes,4516,mask);
    std::uint32_t remaining=mask>>1,highest=0;
    for(std::uint32_t layer=1;remaining!=0&&layer<=layerCount;layer++,remaining>>=2)
        if(remaining&1)highest=layer;
    writeFloat(bytes,192+12,float(highest));
}
} // namespace source_territory
