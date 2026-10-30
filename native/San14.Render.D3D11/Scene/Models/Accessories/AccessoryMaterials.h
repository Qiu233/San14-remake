#pragma once
#include "../../../Runtime/PreparedFileInput.h"
#include "AccessoryMaterialInputs.h"
#include "AccessoryGlyphRenderer.h"
#include <map>
#include <memory>
#include <optional>

struct NativeAccessoryMaterial {
    std::array<ComPtr<ID3D11ShaderResourceView>,16> textures;
    std::array<ComPtr<ID3D11SamplerState>,16> samplers;
    accessory_glyph::Primary glyph;
};
// A dynamic image job is independent of the scene's inherited drawing state.
// Subsequent scene programs install their own shaders, streams and constants.
class NativeAccessoryProducerScope {
public:
    explicit NativeAccessoryProducerScope(ID3D11DeviceContext* context):context_(context) {
        std::array<ID3D11RenderTargetView*,D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT> targets{};
        ID3D11DepthStencilView* depth=nullptr;
        context_->OMGetRenderTargets(UINT(targets.size()),targets.data(),&depth);
        for(unsigned i=0;i<targets.size();++i)targets_[i].Attach(targets[i]);
        depthTarget_.Attach(depth);
        viewportCount_=UINT(viewports_.size());context_->RSGetViewports(&viewportCount_,viewports_.data());
        context_->OMGetBlendState(&blend_,blendFactor_.data(),&sampleMask_);
        context_->OMGetDepthStencilState(&depthState_,&stencilReference_);
        context_->RSGetState(&raster_);
    }
    NativeAccessoryProducerScope(const NativeAccessoryProducerScope&)=delete;
    NativeAccessoryProducerScope& operator=(const NativeAccessoryProducerScope&)=delete;
    ~NativeAccessoryProducerScope() {
        std::array<ID3D11RenderTargetView*,D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT> targets{};
        for(unsigned i=0;i<targets.size();++i)targets[i]=targets_[i].Get();
        context_->OMSetRenderTargets(UINT(targets.size()),targets.data(),depthTarget_.Get());
        context_->RSSetViewports(viewportCount_,viewports_.data());
        context_->OMSetBlendState(blend_.Get(),blendFactor_.data(),sampleMask_);
        context_->OMSetDepthStencilState(depthState_.Get(),stencilReference_);
        context_->RSSetState(raster_.Get());
    }
private:
    ID3D11DeviceContext* context_;
    std::array<ComPtr<ID3D11RenderTargetView>,D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT> targets_;
    ComPtr<ID3D11DepthStencilView> depthTarget_;
    std::array<D3D11_VIEWPORT,D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE> viewports_{};
    UINT viewportCount_{},sampleMask_{},stencilReference_{};
    std::array<float,4> blendFactor_{};
    ComPtr<ID3D11BlendState> blend_;
    ComPtr<ID3D11DepthStencilState> depthState_;
    ComPtr<ID3D11RasterizerState> raster_;
};
inline NativeAccessoryMaterial prepareNativeAccessoryStaticMaterial(ID3D11Device* device,const fs::path& directory) {
    NativeAccessoryMaterial material;unsigned slot,resource,view;std::string file;
    NativePreparedInputFile textures(directory/"texture-formats.txt");
    if(!textures)throw std::runtime_error("Accessory static texture declarations absent");
    while(textures>>slot>>resource>>view>>file)
        material.textures.at(slot)=loadNativeModelTexture(device,directory/file,DXGI_FORMAT(resource),DXGI_FORMAT(view));
    if(!textures.eof())throw std::runtime_error("Accessory static texture declaration malformed");
    NativePreparedInputFile samplers(directory/"samplers-native.txt");
    if(!samplers)throw std::runtime_error("Accessory static sampler declarations absent");
    while(samplers>>slot>>file)material.samplers.at(slot)=accessory_glyph::sampler(device,directory/file);
    if(!samplers.eof())throw std::runtime_error("Accessory sampler declaration malformed");
    return material;
}
// Requests describe actual inputs; only a normally completed image producer
// installs an entry. Old full identities remain valid for their borrowing draws.
class NativeAccessoryMaterials {
public:
    NativeAccessoryMaterials(ID3D11Device* device,fs::path source):source_(std::move(source)),
        pole_(prepareNativeAccessoryStaticMaterial(device,source_/"default-pole")) {}
    void prepare(ID3D11Device* device,ID3D11DeviceContext* context,const San14::Render::CityGateFlagKey& flag) {
        for(unsigned manager:{0u,1u}) {
            NativeAccessoryMaterialKey key{manager,flag};if(completed_.contains(key))continue;
            const auto relative=nativeAccessoryMaterialDirectory(key);const auto directory=source_/relative;
            const auto declaration=readNativeAccessoryMaterialInput(directory,key);
            auto job=accessory_glyph::read(directory/"glyph-job.bin");
            if(job.size()!=48&&job.size()!=144)throw std::runtime_error("Accessory glyph job extent");
            std::array<unsigned,8> header{};std::memcpy(header.data(),job.data(),32);
            if(header[0]!=manager||header[1]!=unsigned(flag.force)||header[2]!=declaration.proxy||header[3]!=flag.glyph)
                throw std::runtime_error("Accessory glyph producer input differs from full material identity");
            auto material=prepareNativeAccessoryStaticMaterial(device,directory);
            if(!producer_)producer_.emplace(device,source_);
            accessory_glyph::Atlas* atlas=nullptr;
            if(header[7]) {
                auto found=atlases_.find(flag.glyph);
                if(found==atlases_.end())found=atlases_.emplace(flag.glyph,accessory_glyph::Atlas::create(device,source_,flag.glyph)).first;
                atlas=&found->second;
            }
            {
                NativeAccessoryProducerScope drawingState(context);
                material.glyph=producer_->execute(device,context,source_,relative,atlas);
            }
            material.textures.at(declaration.texture)=material.glyph.srv;
            material.samplers.at(declaration.sampler)=material.glyph.sourceSampler;
            completed_.emplace(key,std::move(material));
        }
    }
    const NativeAccessoryMaterial& material(unsigned manager,const San14::Render::CityGateFlagKey& flag) const {
        return completed_.at({manager,flag});
    }
    const NativeAccessoryMaterial& pole() const {return pole_;}
private:
    fs::path source_;
    NativeAccessoryMaterial pole_;
    std::optional<accessory_glyph::StockProducer> producer_;
    std::map<unsigned,accessory_glyph::Atlas> atlases_;
    std::map<NativeAccessoryMaterialKey,NativeAccessoryMaterial> completed_;
};
