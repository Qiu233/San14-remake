#pragma once
// Numeric same-request packet from OriginalD3D11CurrentSeasonFrame. Loaded
// textures, programs, History/Generic owners remain native session resources.
#include <array>
#include <cstdint>
#include <cstring>
#include <span>
#include <stdexcept>
#include <string>
#include <vector>

namespace source_current_season {
struct Word {int32_t offset;uint32_t bits;};
struct Light {uint32_t object,mask;std::array<uint8_t,0x328> data;};
struct ExposureLight {uint32_t auxiliarySelector{},object{},present{},type{},flags{};std::array<float,3> rgb{};};
static_assert(sizeof(ExposureLight)==32);
struct HdrWrite {uint32_t offset;std::vector<uint8_t> bytes;};
struct HdrPass {std::string identity;uint32_t slot,constantBytes;std::vector<HdrWrite> writes;};
struct Frame {
    uint32_t mode=0,season=0;int64_t cameraIdentity=0,worldOrdinal=0;
    std::array<uint8_t,0x3c0> main{};
    std::vector<Word> words;std::array<Light,5> lights{};
    std::array<float,74> atmosphere{};
    ExposureLight exposure;
    std::vector<HdrPass> hdr;
};
template<class T> inline T read(std::span<const uint8_t> bytes,size_t at){
    T result;std::memcpy(&result,bytes.data()+at,sizeof(result));return result;
}
inline Frame decode(std::span<const uint8_t> bytes){
    if(bytes.size()<32+0x3c0)throw std::runtime_error("Current season packet length");
    Frame r;r.mode=read<uint32_t>(bytes,0);r.season=read<uint32_t>(bytes,4);
    uint32_t count=read<uint32_t>(bytes,8),lights=read<uint32_t>(bytes,12);
    if(lights!=5||bytes.size()<32+0x3c0+size_t(count)*8+lights*(16+0x328)+296+sizeof(ExposureLight))
        throw std::runtime_error("Current season packet layout");
    r.cameraIdentity=read<int64_t>(bytes,16);r.worldOrdinal=read<int64_t>(bytes,24);
    std::memcpy(r.main.data(),bytes.data()+32,r.main.size());size_t at=32+0x3c0;
    r.words.resize(count);for(auto& w:r.words){w=read<Word>(bytes,at);at+=8;}
    for(auto& l:r.lights){l.object=read<uint32_t>(bytes,at);l.mask=read<uint32_t>(bytes,at+4);
        std::memcpy(l.data.data(),bytes.data()+at+16,l.data.size());at+=16+l.data.size();}
    std::memcpy(r.atmosphere.data(),bytes.data()+at,296);at+=296;
    r.exposure=read<ExposureLight>(bytes,at);at+=sizeof(ExposureLight);
    if(at<bytes.size()){
        auto word=[&](){if(at+4>bytes.size())throw std::runtime_error("Current HDR packet length");
            uint32_t value=read<uint32_t>(bytes,at);at+=4;return value;};
        uint32_t passes=word();r.hdr.reserve(passes);
        for(uint32_t i=0;i<passes;i++){
            HdrPass pass;uint32_t length=word();pass.slot=word();pass.constantBytes=word();uint32_t writes=word();
            if(length>bytes.size()-at)throw std::runtime_error("Current HDR identity length");
            pass.identity.assign(reinterpret_cast<const char*>(bytes.data()+at),length);at+=length;
            pass.writes.reserve(writes);for(uint32_t j=0;j<writes;j++){
                HdrWrite write;write.offset=word();uint32_t n=word();
                if(n>bytes.size()-at||size_t(write.offset)+n>pass.constantBytes)throw std::runtime_error("Current HDR write extent");
                write.bytes.assign(bytes.begin()+at,bytes.begin()+at+n);at+=n;pass.writes.push_back(std::move(write));
            }
            r.hdr.push_back(std::move(pass));
        }
    }
    if(at!=bytes.size())throw std::runtime_error("Current HDR packet tail");
    return r;
}
// World selection belongs to its ordinary owner. This is the original ordered
// first-intersecting group query over this same five-record publication.
inline const Light* firstMatching(const Frame& frame,uint32_t queryMask){
    for(const auto& l:frame.lights)if(l.mask&queryMask)return &l;return nullptr;
}
inline void publishHdr(const HdrPass& pass,std::span<char> currentConstant){
    if(currentConstant.size()!=pass.constantBytes)throw std::runtime_error("Current HDR loaded CB changed");
    for(const auto& write:pass.writes)std::memcpy(currentConstant.data()+write.offset,write.bytes.data(),write.bytes.size());
}
} // namespace source_current_season
