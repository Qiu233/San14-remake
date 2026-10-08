#pragma once
// Loaded season declarations -> immutable HDR views. This bank contains no
// Main/Cloud/Ocean/Ambient/Post history. Bind before normal Post Begin.
#include "current-season-native.h"
#include <d3d11.h>
#include <wrl/client.h>
#include <filesystem>
#include <fstream>
#include <map>
#include <sstream>
#include <tuple>

namespace source_current_season_resources {
using Microsoft::WRL::ComPtr;
struct LutRow {
    uint32_t season{},camera{},hdrKey{},ldrKey{},selector{},staticObject{},file{},tid{},resourceFormat{},viewFormat{};
    std::filesystem::path path;
};
struct Catalog {std::vector<LutRow> luts;};
inline Catalog readCatalog(const std::filesystem::path& directory) {
    std::ifstream input(directory/"season-luts.txt");
    if(!input)throw std::runtime_error("Actual season LUT catalog absent");
    Catalog out;std::string line;
    while(std::getline(input,line)){
        if(line.empty())continue;
        std::istringstream row(line);LutRow item;std::string file,extra;
        if(!(row>>item.season>>item.camera>>item.hdrKey>>item.ldrKey>>item.selector>>item.staticObject
            >>item.file>>item.tid>>item.resourceFormat>>item.viewFormat>>file)||row>>extra)
            throw std::runtime_error("Actual season LUT row malformed");
        if(item.season>=4||item.hdrKey==0||item.staticObject==0)
            throw std::runtime_error("Actual season LUT row key");
        item.path=directory/file;out.luts.push_back(std::move(item));
    }
    return out;
}
inline uint32_t preparedWord(const source_current_season::Frame& current,int offset) {
    for(const auto& word:current.words)if(word.offset==offset)return word.bits;
    throw std::runtime_error("Current season LUT requires its actual prepared key");
}
inline const LutRow* selectCurrent(const Catalog& catalog,const source_current_season::Frame& current) {
    uint32_t hdr=preparedWord(current,0xeb8),ldr=preparedWord(current,0xebc);
    if(hdr==0)return nullptr; // Original null wrapper skips the persistent setter.
    for(const auto& row:catalog.luts)if(row.season==current.season&&row.hdrKey==hdr&&row.ldrKey==ldr)return &row;
    throw std::runtime_error("Current prepared LUT key is not in its actual loaded season");
}
struct HdrImage {
    uint32_t width{},height{},depth{},mips{},viewFormat{};
    struct Mip {uint32_t rowPitch{},slicePitch{};std::vector<uint8_t> bytes;};
    std::vector<Mip> levels;
};
inline HdrImage readHdrImage(const LutRow& row) {
    std::ifstream input(row.path,std::ios::binary);
    if(!input)throw std::runtime_error("Actual season HDR volume absent");
    auto word=[&](){uint32_t v{};input.read(reinterpret_cast<char*>(&v),4);
        if(!input)throw std::runtime_error("Actual season HDR volume truncated");return v;};
    HdrImage image;image.width=word();image.height=word();image.depth=word();image.mips=word();image.viewFormat=word();
    if(!image.width||!image.height||!image.depth||image.mips!=1||image.viewFormat!=row.viewFormat)
        throw std::runtime_error("Actual season HDR single-volume layout");
    image.levels.resize(image.mips);
    for(auto& mip:image.levels){mip.rowPitch=word();mip.slicePitch=word();mip.bytes.resize(word());
        input.read(reinterpret_cast<char*>(mip.bytes.data()),std::streamsize(mip.bytes.size()));
        if(!input||uint64_t(mip.rowPitch)*image.height!=mip.slicePitch||uint64_t(mip.slicePitch)*image.depth!=mip.bytes.size())
            throw std::runtime_error("Actual season HDR volume pitches");}
    if(input.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Actual season HDR trailing bytes");
    return image;
}
using PhysicalKey=std::tuple<uint32_t,uint32_t,uint32_t,uint32_t,uint32_t>;
inline PhysicalKey physicalKey(const LutRow& row){return {row.staticObject,row.file,row.tid,row.resourceFormat,row.viewFormat};}
struct HdrResource {ComPtr<ID3D11Texture3D> texture;ComPtr<ID3D11ShaderResourceView> view;};
struct Bank {Catalog catalog;std::map<PhysicalKey,HdrResource> resources;};
inline void requireHr(HRESULT result,const char* operation){if(FAILED(result))throw std::runtime_error(operation);}
// Initial creation or actual resource-load boundary only. Existing immutable
// objects are reused when additional season rows are installed lazily.
inline void loadResources(ID3D11Device* device,Bank& bank,Catalog catalog) {
    for(const auto& row:catalog.luts){
        auto key=physicalKey(row);if(bank.resources.contains(key))continue;
        auto image=readHdrImage(row);std::vector<D3D11_SUBRESOURCE_DATA> initial(image.levels.size());
        for(size_t i=0;i<initial.size();++i){initial[i].pSysMem=image.levels[i].bytes.data();
            initial[i].SysMemPitch=image.levels[i].rowPitch;initial[i].SysMemSlicePitch=image.levels[i].slicePitch;}
        D3D11_TEXTURE3D_DESC d{};d.Width=image.width;d.Height=image.height;d.Depth=image.depth;
        d.MipLevels=image.mips;d.Format=DXGI_FORMAT(row.resourceFormat);
        d.Usage=D3D11_USAGE_IMMUTABLE;d.BindFlags=D3D11_BIND_SHADER_RESOURCE;
        HdrResource resource;requireHr(device->CreateTexture3D(&d,initial.data(),&resource.texture),"Season HDR Texture3D creation failed");
        D3D11_SHADER_RESOURCE_VIEW_DESC s{};s.Format=DXGI_FORMAT(row.viewFormat);
        s.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE3D;s.Texture3D.MipLevels=image.mips;
        requireHr(device->CreateShaderResourceView(resource.texture.Get(),&s,&resource.view),"Season HDR SRV creation failed");
        bank.resources.emplace(key,std::move(resource));
    }
    bank.catalog=std::move(catalog);
}
// The current keys choose a loaded Static/TID. No IO, hashes or GPU creation
// here. The original module's manager sampler and proven-null metadata stay;
// do not replace that sampler with Static descriptor+38.
template<class NormalPostOwner>
inline const LutRow* installCurrent(const Bank& bank,const source_current_season::Frame& current,NormalPostOwner& post) {
    const auto* selected=selectCurrent(bank.catalog,current);if(!selected)return nullptr;
    post.externalViews["hdr-lut.tex"]=bank.resources.at(physicalKey(*selected)).view;
    return selected;
}
} // namespace source_current_season_resources
