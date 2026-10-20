#pragma once
#include "../../Passes/Shadows/ShadowCameraFrame.h"
#include "../../Passes/Shadows/ShadowRaster.h"

namespace San14::Render {
// Geometry and height sampling belong to TerrainPass. This owner adds only
// LSSM's program, mutable projection and normal Host shadow policy.
class TerrainShadowGpu {
    ComPtr<ID3D11VertexShader> vertex_;
    ComPtr<ID3D11Buffer> constants_;
    unsigned flags_{},nearMode_{},distantMode_{};
public:
    void prepare(ID3D11Device* device,const fs::path& input) {
        std::ifstream policy(input/"policy.txt");
        policy>>flags_>>nearMode_>>distantMode_;
        if(!policy||nearMode_!=11||distantMode_!=9)
            throw std::runtime_error("Landscape shadow requires its actual normal/distant LSSM role");
        if(flags_&0x2000u)
            throw std::runtime_error("Landscape custom fit requires its same-View bounds publication");
        auto bytes=load(input/"vs.dxbc");
        check(device->CreateVertexShader(bytes.data(),bytes.size(),nullptr,&vertex_),"original Landscape depth-class5 VS");
        D3D11_BUFFER_DESC d{};d.ByteWidth=64;d.Usage=D3D11_USAGE_DEFAULT;d.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
        check(device->CreateBuffer(&d,nullptr,&constants_),"Landscape current shadow projection");
    }

    unsigned draw(ID3D11DeviceContext* context,const NativeTerrainCameraGpu& terrain,
        ID3D11ShaderResourceView* height,ID3D11SamplerState* sampler,
        const source_shadow_camera::Frame& frame,const NativeShadowRasterGpu& raster,
        unsigned family) {
        if(!(flags_&0x1000u)||!terrain.instanceCount)return 0;
        const auto mode=family?distantMode_:nearMode_;
        if(mode==1||(mode>=6&&mode<=8))throw std::runtime_error("Landscape LSP shadow owner is not imported");
        context->IASetInputLayout(terrain.layout.Get());
        ID3D11Buffer* streams[]{terrain.positions.Get(),terrain.instances.Get()};
        const UINT strides[]{12,56},offsets[]{0,0};
        context->IASetVertexBuffers(0,2,streams,strides,offsets);
        context->IASetIndexBuffer(terrain.indices.Get(),DXGI_FORMAT_R16_UINT,0);
        context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
        context->VSSetShader(vertex_.Get(),nullptr,0);context->PSSetShader(nullptr,nullptr,0);
        auto cb=constants_.Get();context->VSSetConstantBuffers(0,1,&cb);
        context->VSSetShaderResources(8,1,&height);context->VSSetSamplers(8,1,&sampler);
        unsigned draws=0;
        for(unsigned pass=0;pass<unsigned(frame.passCount);++pass) {
            if(!frame.passes[pass].shadowAvailable)continue;
            const auto& a=frame.atlas[pass];
            D3D11_VIEWPORT viewport{float(a.x-1),float(a.y-1),float(a.width+2),float(a.height+2),0,1};
            D3D11_RECT scissor{a.x,a.y,a.x+a.width,a.y+a.height};
            context->RSSetViewports(1,&viewport);context->RSSetScissorRects(1,&scissor);
            context->RSSetState(raster.passes.at(family).at(pass).Get());
            context->UpdateSubresource(constants_.Get(),0,nullptr,frame.passes[pass].shadow.data(),0,0);
            for(const auto& batch:terrain.batches) {
                context->DrawIndexedInstanced(terrain.indexCount,UINT(batch.count),0,0,UINT(batch.start));
                ++draws;
            }
        }
        return draws;
    }
};
} // namespace San14::Render
