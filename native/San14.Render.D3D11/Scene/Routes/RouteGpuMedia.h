#pragma once
#include "RouteOriginalPrograms.h"
#include "../../Device/RouteGpuReferences.h"
#include <map>
#include <memory>

namespace San14::Render {
struct NativeRouteGpuMedia final {
    struct Image {ComPtr<ID3D11ShaderResourceView> texture;ComPtr<ID3D11SamplerState> sampler;};

        std::shared_ptr<const RouteOriginalProgramMedia> program;
        ComPtr<ID3D11RasterizerState> raster;
        ComPtr<ID3D11BlendState> blend;
        std::map<unsigned,Image> groups;
        static std::shared_ptr<const NativeRouteGpuMedia> prepare(ID3D11Device* device,const fs::path& root) {
            auto out=std::make_shared<NativeRouteGpuMedia>();out->program=RouteOriginalProgramMedia::load(device,root);
            D3D11_RASTERIZER_DESC raster{};raster.FillMode=D3D11_FILL_SOLID;raster.CullMode=D3D11_CULL_BACK;
            raster.FrontCounterClockwise=TRUE;raster.DepthClipEnable=TRUE;
            check(device->CreateRasterizerState(&raster,&out->raster),"Route queue200 solid back-cull raster");
            D3D11_BLEND_DESC blend{};auto& target=blend.RenderTarget[0];
            target.BlendEnable=FALSE;target.RenderTargetWriteMask=D3D11_COLOR_WRITE_ENABLE_ALL;
            target.SrcBlend=target.SrcBlendAlpha=D3D11_BLEND_ONE;target.DestBlend=target.DestBlendAlpha=D3D11_BLEND_ZERO;
            target.BlendOp=target.BlendOpAlpha=D3D11_BLEND_OP_ADD;
            check(device->CreateBlendState(&blend,&out->blend),"Route actual queue200 RGBA blend-disabled state");
            NativePreparedInputFile groups(root/"texture-groups.txt");unsigned name,object,count;std::string relative;
            if(!groups)throw std::runtime_error("Route actual TextureGroups are absent");
            while(groups>>name>>object>>count>>relative) {
                //C15440's listOffset0 and C2FA00's category13 entry0 both
                //survive BF5FD0/BF47F0;97F630 selects actual image0/UV0.
                if(object!=0||count==0)throw std::runtime_error("Route TextureGroup's source-selected image is unsupported");
                NativePreparedInputFile images(root/relative/"images.txt");unsigned slot,resource,view;std::string texture,sampler;
                if(!(images>>slot>>resource>>view>>texture>>sampler)||slot!=0)throw std::runtime_error("Route selected image0 is absent");
                Image image;image.texture=loadNativeModelTexture(device,root/relative/texture,DXGI_FORMAT(resource),DXGI_FORMAT(view));
                auto bytes=load(root/relative/sampler);
                if(bytes.size()!=sizeof(D3D11_SAMPLER_DESC))throw std::runtime_error("Route G1T sampler extent is invalid");
                D3D11_SAMPLER_DESC d;std::memcpy(&d,bytes.data(),sizeof(d));
                check(device->CreateSamplerState(&d,&image.sampler),"Route actual selected G1T sampler");
                if(!out->groups.emplace(name,std::move(image)).second)throw std::runtime_error("Route TextureGroup identity is duplicated");
            }
            if(!groups.eof()||out->groups.empty())throw std::runtime_error("Route TextureGroup list is invalid");
            return out;
        }
        const Image& image(unsigned bank,unsigned role)const {
            static constexpr unsigned keys[2][4]{{0xD7157FCA,0x83DC9465,0x5A883F24,0x3133E9E3},
                {0xE57FC322,0xF7F0860D,0xCE9C30CC,0xA547DB8B}};
            auto at=groups.find(keys[bank][role]);if(at==groups.end())throw std::runtime_error("Route selected TextureGroup is absent");return at->second;
        }
    };

// The device owns only its current declarations. Existing scene borrowers keep
// their old immutable media alive after a complete replacement transaction.
class NativeRouteGpuResidency final {
    struct Entry {std::wstring identity;std::filesystem::path directory;std::shared_ptr<const NativeRouteGpuMedia> media;};
    std::map<std::wstring,Entry> current_;
public:
    void replace(ID3D11Device* device,const std::vector<NativeRouteGpuDeclaration>& sources) {
        if(sources.empty())throw std::runtime_error("Declared route GPU media closure is empty");
        std::map<std::wstring,Entry> candidate;
        for(const auto& source:sources) {
            if(source.logicalKey.empty()||source.identity.empty()||source.directory.empty())
                throw std::runtime_error("Route GPU media declaration is incomplete");
            std::shared_ptr<const NativeRouteGpuMedia> media;
            if(auto old=current_.find(source.logicalKey);old!=current_.end()&&old->second.identity==source.identity) {
                if(nativePreparedFilePath(old->second.directory)!=nativePreparedFilePath(source.directory))
                    throw std::runtime_error("Route generation identity changed its prepared directory");
                media=old->second.media;
            } else {
                NativePreparedInputFile input(source.directory/"route-gpu-media.txt");
                if(readLine(input)!="SAN14_ROUTE_GPU_MEDIA 1"||readWideLine(input)!=source.logicalKey||readWideLine(input)!=source.identity)
                    throw std::runtime_error("Route GPU package differs from its declared identity");
                media=NativeRouteGpuMedia::prepare(device,source.directory);
            }
            if(!candidate.emplace(source.logicalKey,Entry{source.identity,source.directory,std::move(media)}).second)
                throw std::runtime_error("Route GPU media logical key is duplicated");
        }
        current_.swap(candidate);
    }
    std::shared_ptr<const NativeRouteGpuMedia> borrow(const std::wstring& key,const std::wstring& identity) const {
        auto at=current_.find(key);
        if(at==current_.end()||at->second.identity!=identity)
            throw std::runtime_error("Requested route GPU generation is not resident");
        return at->second.media;
    }
    static std::string readLine(NativePreparedInputFile& input) {
        std::string line;if(!std::getline(input,line))throw std::runtime_error("Route GPU manifest is incomplete");
        if(!line.empty()&&line.back()=='\r')line.pop_back();return line;
    }
    static std::wstring readWideLine(NativePreparedInputFile& input) {
        auto line=readLine(input);std::wstring result;
        for(unsigned char c:line) {if(c<32||c>126)throw std::runtime_error("Route key/identity is not ASCII");result.push_back(wchar_t(c));}
        return result;
    }
};

} // namespace San14::Render
