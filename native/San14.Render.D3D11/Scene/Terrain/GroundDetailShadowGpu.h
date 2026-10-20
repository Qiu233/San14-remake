#pragma once
#include "../Models/ModelRenderer.h"
#include "../../Passes/Shadows/ShadowRaster.h"

namespace ground_details {
// Caster programs borrow the normal template's geometry and primary material.
// Per-frame instances and wind remain owned by GroundDetails.
class GroundDetailShadowGpu {
    struct Caster {
        bool tree{},alpha{},localToProjection{};
        unsigned templateIndex{},sourceDraw{};
        std::vector<char> vertex,pixel;
        ComPtr<ID3D11VertexShader> vs;
        ComPtr<ID3D11PixelShader> ps;
        ComPtr<ID3D11InputLayout> layout;
        ComPtr<ID3D11Buffer> vertexConstants,pixelConstants;
    };
    std::vector<Caster> casters_;
    static ComPtr<ID3D11Buffer> constants(ID3D11Device* device,UINT bytes) {
        D3D11_BUFFER_DESC d{};d.ByteWidth=bytes;d.Usage=D3D11_USAGE_DEFAULT;
        d.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
        ComPtr<ID3D11Buffer> result;
        check(device->CreateBuffer(&d,nullptr,&result),"Ground caster mutable constants");
        return result;
    }
public:
    void loadResources(ID3D11Device* device,const fs::path& root,const NativeModelInputs& models) {
        std::ifstream rows(root/"shadow-draws.txt");
        if(!rows)throw std::runtime_error("Ground caster resource directory absent");
        std::string identity,directory;unsigned ti,draw,tree,alpha,vsBytes,psBytes,local;
        while(rows>>identity>>ti>>draw>>tree>>alpha>>vsBytes>>psBytes>>local>>directory) {
            auto index=casters_.size();
            if(index>=models.draws.size()||models.draws[index].identity!=identity||
               vsBytes!=(tree?272u:local?64u:112u)||psBytes!=(alpha?48u:0u))
                throw std::runtime_error("Ground caster source identity or layout differs");
            Caster c;c.templateIndex=ti;c.sourceDraw=draw;c.tree=tree!=0;c.alpha=alpha!=0;c.localToProjection=local!=0;
            const auto path=root/directory;
            auto vertex=::load(path/"vs.dxbc");
            check(device->CreateVertexShader(vertex.data(),vertex.size(),nullptr,&c.vs),"Ground original caster VS");
            bool instanced=false;c.layout=loadNativeModelLayout(device,path,vertex,instanced);
            c.vertex=::load(path/"vs-b0.bin");
            if(c.vertex.size()!=vsBytes)throw std::runtime_error("Ground caster vertex seed length differs");
            c.vertexConstants=constants(device,vsBytes);
            if(c.alpha) {
                auto pixel=::load(path/"ps.dxbc");
                check(device->CreatePixelShader(pixel.data(),pixel.size(),nullptr,&c.ps),"Ground original alpha caster PS");
                c.pixel=::load(path/"ps-b0.bin");
                if(c.pixel.size()!=psBytes)throw std::runtime_error("Ground caster pixel seed length differs");
                c.pixelConstants=constants(device,psBytes);
            }
            casters_.push_back(std::move(c));
        }
        if(!rows.eof()||casters_.size()!=models.draws.size())
            throw std::runtime_error("Ground caster directory is incomplete");
    }

    unsigned draw(ID3D11DeviceContext* context,size_t index,const NativeModelDraw& geometry,
        const std::vector<char>& currentVertex,ID3D11Buffer* instances,unsigned count,
        const source_camera::Matrix& shadow,const NativeShadowRasterSource& raster,unsigned pass) {
        auto& c=casters_.at(index);
        if(!count)return 0;
        if(c.localToProjection)
            throw std::runtime_error("Ground non-instanced caster requires its per-instance matrix writer");
        if(c.tree)std::memcpy(c.vertex.data()+64,currentVertex.data()+64,c.vertex.size()-64);
        std::memcpy(c.vertex.data(),shadow.data(),64);
        context->UpdateSubresource(c.vertexConstants.Get(),0,nullptr,c.vertex.data(),0,0);
        if(c.alpha) {
            const float bias[2]{raster.scale*.5f,raster.bias.at(raster.selector==0?0:pass)};
            std::memcpy(c.pixel.data(),bias,sizeof(bias));
            context->UpdateSubresource(c.pixelConstants.Get(),0,nullptr,c.pixel.data(),0,0);
        }
        if(geometry.vertexBuffers.size()!=2||geometry.strides[1]!=48)
            throw std::runtime_error("Ground caster requires its actual instance stream");
        ID3D11Buffer* buffers[2]{geometry.vertexBuffers[0].Get(),instances};
        UINT strides[2]{geometry.strides[0],48},offsets[2]{};
        context->IASetVertexBuffers(0,2,buffers,strides,offsets);
        context->IASetIndexBuffer(geometry.indices.Get(),geometry.indexBits==16?DXGI_FORMAT_R16_UINT:DXGI_FORMAT_R32_UINT,0);
        context->IASetInputLayout(c.layout.Get());
        context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY(geometry.topology));
        context->VSSetShader(c.vs.Get(),nullptr,0);context->PSSetShader(c.ps.Get(),nullptr,0);
        auto vc=c.vertexConstants.Get(),pc=c.pixelConstants.Get();
        context->VSSetConstantBuffers(0,1,&vc);context->PSSetConstantBuffers(0,1,&pc);
        ID3D11ShaderResourceView* texture=c.alpha?geometry.textures[0].Get():nullptr;
        ID3D11SamplerState* sampler=c.alpha?geometry.samplers[0].Get():nullptr;
        context->PSSetShaderResources(0,1,&texture);context->PSSetSamplers(0,1,&sampler);
        context->DrawIndexedInstanced(geometry.indexCount,count,geometry.indexStart,geometry.baseVertex,0);
        return 1;
    }
};
} // namespace ground_details
