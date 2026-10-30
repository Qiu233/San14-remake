#pragma once
#include "../../Runtime/PreparedFileInput.h"
#include "ShadowReceiverInputs.h"
#include "ShadowReceiverMaterialInputs.h"
#include "../Environment/AmbientRenderer.h"
#include <map>
#include <optional>
#include <sstream>

namespace San14::Render {
struct NativeShadowReceiverRecipe {
    std::optional<ShadowReceiverMaterialLayout> layout;
    std::string unsupported;
};
struct NativeShadowReceiverProvider {
    ShadowReceiverProgramBank bank;
    std::map<std::int32_t,ComPtr<ID3D11VertexShader>> vertices;
    std::map<std::int32_t,ComPtr<ID3D11PixelShader>> pixels;
    std::map<std::int32_t,NativeShadowReceiverRecipe> recipes;
};
struct PatternSampling {
    ID3D11ShaderResourceView* view{};
    ID3D11SamplerState* sampler{};
};
class NativeShadowReceiverResources {
public:
    void prepare(ID3D11Device* device,const fs::path& root) {
        auto nodes=Interop::readShadowReceiverNodes(root/"nodes.tsv");
        NativePreparedInputFile atlasDeclaration(root/"shadow-atlas-object.txt");
        std::uint32_t atlasObject{};std::string extra;
        if(!(atlasDeclaration>>atlasObject)||atlasDeclaration>>extra)
            throw std::runtime_error("Receiver compact atlas declaration is absent or malformed");
        for(const auto& [index,node]:nodes)
            if((node.litObject&&node.litObject!=atlasObject)||
                (node.overrideLitObject&&node.overrideLitObject!=atlasObject))
                throw std::runtime_error("Receiver graph reference requires another compact shadow producer");
        std::map<std::uint32_t,NativeShadowReceiverProvider> providers;
        for(const auto& [index,node]:nodes)for(const auto id:{node.primary,node.noSub}) {
            if(providers.contains(id))continue;
            const auto directory=root/"program-bank"/std::to_string(id);
            NativeShadowReceiverProvider provider;
            provider.bank=ShadowReceiverProgramBank::read(directory);
            for(const auto& row:provider.bank.programs()) {
                if(row.vertex>=0&&!provider.vertices.contains(row.vertex)) {
                    const auto bytes=load(directory/"programs"/(std::to_string(row.vertex)+".dxbc"));
                    ComPtr<ID3D11VertexShader> shader;
                    check(device->CreateVertexShader(bytes.data(),bytes.size(),nullptr,&shader),"Receiver declared vertex program");
                    provider.vertices.emplace(row.vertex,std::move(shader));
                }
                if(row.pixel>=0&&!provider.pixels.contains(row.pixel)) {
                    const auto bytes=load(directory/"programs"/(std::to_string(row.pixel)+".dxbc"));
                    ComPtr<ID3D11PixelShader> shader;
                    check(device->CreatePixelShader(bytes.data(),bytes.size(),nullptr,&shader),"Receiver declared pixel program");
                    provider.pixels.emplace(row.pixel,std::move(shader));
                }
                if(row.recipe<0||provider.recipes.contains(row.recipe))continue;
                const auto recipeDirectory=directory/"recipes"/std::to_string(row.recipe);
                NativeShadowReceiverRecipe recipe;
                NativePreparedInputFile reason(recipeDirectory/"unsupported.txt");
                if(reason)recipe.unsupported.assign(std::istreambuf_iterator<char>(reason),{});
                else recipe.layout=Interop::readShadowReceiverMaterialLayout(recipeDirectory/"layout.bin");
                provider.recipes.emplace(row.recipe,std::move(recipe));
            }
            providers.emplace(id,std::move(provider));
        }
        auto textures=ambientTextures(device,root/"fallback","texture.tsv",1);
        if(textures.front().program!="Lit"||textures.front().slot!=0)
            throw std::runtime_error("Receiver fallback requires the declared Lit image");
        auto shadowSampler=ambientSampler(device,root/"shadow-sampler-desc.bin");
        auto pointSampler=ambientSampler(device,root/"point-sampler-desc.bin");
        auto defaultSampler=ambientSampler(device,root/"default-sampler-desc.bin");
        auto readLit=[&](const fs::path& directory) {
            auto values=ambientTextures(device,directory,"texture.tsv",1);
            if(values.front().program!="Lit"||values.front().slot!=0)
                throw std::runtime_error("Ordinary Pattern requires its actual Lit image");
            return std::move(values.front());
        };
        const auto textureInputs=Interop::readOrdinaryPatternTextureInputs(root);
        std::optional<AmbientNativeTextureBinding> merged;
        if(textureInputs.mergedPresent)merged=readLit(root/"merged");
        std::map<std::uint32_t,std::optional<AmbientNativeTextureBinding>> locals;
        for(const auto& input:textureInputs.locals) {
            std::optional<AmbientNativeTextureBinding> texture;
            if(input.present)texture=readLit(root/input.directory);
            locals.emplace(input.lightObject,std::move(texture));
        }
        nodes_=std::move(nodes);providers_=std::move(providers);
        fallback_=std::move(textures.front());shadowSampler_=std::move(shadowSampler);pointSampler_=std::move(pointSampler);
        merged_=std::move(merged);locals_=std::move(locals);defaultSampler_=std::move(defaultSampler);
    }
    const ShadowReceiverNode& node(std::uint32_t index) const{return nodes_.at(index);}
    const NativeShadowReceiverProvider& provider(std::uint32_t id) const{return providers_.at(id);}
    const AmbientNativeTextureBinding& fallback() const{return fallback_;}
    ID3D11SamplerState* shadowSampler() const{return shadowSampler_.Get();}
    ID3D11SamplerState* pointSampler() const{return pointSampler_.Get();}
    bool mergedPresent() const{return merged_.has_value();}
    PatternSampling ordinaryLit(std::uint32_t lightObject,bool mergedEnabled) const {
        const auto& texture=mergedEnabled&&merged_?merged_:locals_.at(lightObject);
        return texture?PatternSampling{texture->view.Get(),texture->sampler.Get()}:
            PatternSampling{nullptr,defaultSampler_.Get()};
    }
private:
    std::map<std::uint32_t,ShadowReceiverNode> nodes_;
    std::map<std::uint32_t,NativeShadowReceiverProvider> providers_;
    AmbientNativeTextureBinding fallback_;
    std::optional<AmbientNativeTextureBinding> merged_;
    std::map<std::uint32_t,std::optional<AmbientNativeTextureBinding>> locals_;
    ComPtr<ID3D11SamplerState> shadowSampler_,pointSampler_,defaultSampler_;
};
} // namespace San14::Render
