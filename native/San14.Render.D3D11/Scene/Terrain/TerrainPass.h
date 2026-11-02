#pragma once
#include "../../Runtime/PreparedFileInput.h"
// A retained terrain pass owns its imported inputs and mutable camera buffers.
// Targets, frame camera and season are borrowed only for the current request.
#include "SeasonTerrainPalette.h"
#include "TerrainCameraConstants.h"
#include "TerrainRasterState.h"
#include "TerrainShadowGpu.h"
#include <optional>
#include "../../Device/TerrainPrograms.h"

namespace San14::Render {
class TerrainPass {
  public:
    TerrainPass(ID3D11Device *device, ID3D11DeviceContext *context, const fs::path &input,
                bool groupedTerrain,std::shared_ptr<const NativeTerrainPrograms> programs={}):raster_(device) {
        prepareProgramsAndGeometry(device,input,groupedTerrain,std::move(programs));
        bindInitialGeometry(context);
        prepareMaterials(device, context, input);
        shadow_.prepare(device,input/"shadow");
    }
    TerrainPass(const TerrainPass &) = delete;
    TerrainPass &operator=(const TerrainPass &) = delete;

    // Camera resources are prepared after the static model inputs, at their
    // existing session initialization stage. No file or device work in draw.
    void prepareCamera(ID3D11Device *device, const fs::path &input, UINT indexCount,
                       const fs::path &seasonalResources) {
        camera_ = prepareNativeTerrainCameraGpu(
            device, input, programs_->layout.Get(), positions_.Get(), indices_.Get(), programs_->vertex.Get(),
            programs_->pixel.Get(), programs_->coarseVertex.Get(), programs_->coarsePixel.Get(), indexCount,programs_.get());
        seasonPalette_.reset();
        if (!seasonalResources.empty()) {
            seasonPalette_ = source_current_season_terrain::readPaletteBank(seasonalResources);
            source_current_season_terrain::prepareNativeCurrent(*seasonPalette_, camera_);
        }
    }

    void prepareFrame(ID3D11DeviceContext* context,const source_camera::Frame& current) {
        updateNativeTerrainCameraGpu(context,camera_,current);
    }
    unsigned drawShadows(ID3D11DeviceContext* context,const source_shadow_camera::Frame& frame,
        const NativeShadowRasterGpu& raster,unsigned family) {
        return shadow_.draw(context,camera_,vertexViews_[8],vertexSamplers_[8],frame,raster,family);
    }

    UINT draw(ID3D11DeviceContext *context, const source_camera::Frame &current,
              std::optional<uint32_t> season, NativeTerrainCameraDrawBindings bindings,
              NativeSourceDepthState& logical,UINT stencilReference,uint32_t colorMasks) {
        if (season) {
            if (!seasonPalette_)
                throw std::runtime_error("Current terrain season requires its prepared palette bank");
            source_current_season_terrain::patchNativeCurrent(*seasonPalette_, *season,
                                                              camera_);
        }
        bindings.vertexViews = vertexViews_;
        bindings.pixelViews = pixelViews_;
        bindings.vertexSamplers = vertexSamplers_;
        bindings.pixelSamplers = pixelSamplers_;
        raster_.begin(context,logical,stencilReference,colorMasks);
        const auto draws=drawNativeTerrainCameraGpu(context, camera_, bindings);
        raster_.finish(context,logical);
        return draws;
    }

  private:
    TerrainShadowGpu shadow_;
    void prepareProgramsAndGeometry(ID3D11Device *device,const fs::path& input,bool groupedTerrain,
            std::shared_ptr<const NativeTerrainPrograms> programs) {
        if(programs&&!groupedTerrain)throw std::runtime_error("Resident Landscape programs require the declared grouped terrain source");
        programs_=programs?std::move(programs):NativeTerrainPrograms::load(device,input,groupedTerrain);
        positions_ = buffer(device, load(input / "positions.bin"), D3D11_BIND_VERTEX_BUFFER);
        initialInstances_ = buffer(device, load(input / "instances.bin"), D3D11_BIND_VERTEX_BUFFER);
        indices_ = buffer(device, load(input / "indices.bin"), D3D11_BIND_INDEX_BUFFER);
        initialVertexConstants_ =
            buffer(device, load(input / (groupedTerrain ? "near-vs-b0.bin" : "vs-b0.bin")),
                   D3D11_BIND_CONSTANT_BUFFER);
        initialPixelConstants_ =
            buffer(device, load(input / (groupedTerrain ? "near-ps-b0.bin" : "ps-b0.bin")),
                   D3D11_BIND_CONSTANT_BUFFER);
        if (groupedTerrain) {
            initialCoarseVertexConstants_ =
                buffer(device, load(input / "coarse-vs-b0.bin"), D3D11_BIND_CONSTANT_BUFFER);
            initialCoarsePixelConstants_ =
                buffer(device, load(input / "coarse-ps-b0.bin"), D3D11_BIND_CONSTANT_BUFFER);
        }
    }
    void bindInitialGeometry(ID3D11DeviceContext *context) {
        ID3D11Buffer *vb[]{positions_.Get(), initialInstances_.Get()};
        UINT strides[]{12, 56}, vbOffsets[]{0, 0};
        context->IASetInputLayout(programs_->layout.Get());
        context->IASetVertexBuffers(0, 2, vb, strides, vbOffsets);
        context->IASetIndexBuffer(indices_.Get(), DXGI_FORMAT_R16_UINT, 0);
        context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
        context->VSSetShader(programs_->vertex.Get(), nullptr, 0);
        context->PSSetShader(programs_->pixel.Get(), nullptr, 0);
        auto cb = initialVertexConstants_.Get();
        context->VSSetConstantBuffers(0, 1, &cb);
        cb = initialPixelConstants_.Get();
        context->PSSetConstantBuffers(0, 1, &cb);
    }
    void prepareMaterials(ID3D11Device *device, ID3D11DeviceContext *context, const fs::path &input) {
        NativePreparedInputFile textureRows(input / "textures.txt");
        std::string stage, name;
        UINT slot;
        while (textureRows >> stage >> slot >> name) {
            auto texture = loadTexture(device, input / name);
            D3D11_TEXTURE2D_DESC d{};
            texture->GetDesc(&d);
            ComPtr<ID3D11ShaderResourceView> view;
            check(device->CreateShaderResourceView(texture.Get(), nullptr, &view), "source texture view");
            auto raw = view.Get();
            if (stage == "VS") {
                context->VSSetShaderResources(slot, 1, &raw);
                vertexViews_.at(slot) = raw;
            } else {
                context->PSSetShaderResources(slot, 1, &raw);
                pixelViews_.at(slot) = raw;
            }
            textures_.push_back(texture);
            views_.push_back(view);
        }
        NativePreparedInputFile samplerRows(input / "samplers.txt");
        UINT filter, u, v, w, aniso, comparison;
        float bias, minLod, maxLod;
        while (samplerRows >> stage >> slot >> filter >> u >> v >> w >> bias >> aniso >> comparison >>
               minLod >> maxLod) {
            D3D11_SAMPLER_DESC d{};
            d.Filter = D3D11_FILTER(filter);
            d.AddressU = D3D11_TEXTURE_ADDRESS_MODE(u);
            d.AddressV = D3D11_TEXTURE_ADDRESS_MODE(v);
            d.AddressW = D3D11_TEXTURE_ADDRESS_MODE(w);
            d.MipLODBias = bias;
            d.MaxAnisotropy = aniso;
            d.ComparisonFunc = D3D11_COMPARISON_FUNC(comparison);
            d.MinLOD = minLod;
            d.MaxLOD = maxLod;
            ComPtr<ID3D11SamplerState> sampler;
            check(device->CreateSamplerState(&d, &sampler), "source sampler");
            auto raw = sampler.Get();
            if (stage == "VS") {
                context->VSSetSamplers(slot, 1, &raw);
                vertexSamplers_.at(slot) = raw;
            } else {
                context->PSSetSamplers(slot, 1, &raw);
                pixelSamplers_.at(slot) = raw;
            }
            samplers_.push_back(sampler);
        }
    }

    TerrainRasterState raster_;
    std::shared_ptr<const NativeTerrainPrograms> programs_;
    ComPtr<ID3D11Buffer> positions_, initialInstances_, indices_;
    ComPtr<ID3D11Buffer> initialVertexConstants_, initialPixelConstants_;
    ComPtr<ID3D11Buffer> initialCoarseVertexConstants_, initialCoarsePixelConstants_;
    std::vector<ComPtr<ID3D11Texture2D>> textures_;
    std::vector<ComPtr<ID3D11ShaderResourceView>> views_;
    std::array<ID3D11ShaderResourceView *, 16> vertexViews_{}, pixelViews_{};
    std::vector<ComPtr<ID3D11SamplerState>> samplers_;
    std::array<ID3D11SamplerState *, 16> vertexSamplers_{}, pixelSamplers_{};
    NativeTerrainCameraGpu camera_;
    std::optional<source_current_season_terrain::PaletteBank> seasonPalette_;
};
} // namespace San14::Render
