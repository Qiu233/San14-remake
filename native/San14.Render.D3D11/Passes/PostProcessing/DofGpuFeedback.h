#pragma once
#include <d3dcompiler.h>
#include <algorithm>
#include <array>
#include <stdexcept>
#include <string>

namespace San14::Render {
// The controller's two historical copies remain GPU resident. Before57
// latches both clear decisions before this application overwrites either
// counter. Original scatter draws continue to use their original Buffer5.
class DofGpuFeedback {
public:
    void createSide(ID3D11Device* device,UINT side,UINT bytes) {
        if(bytes!=32)throw std::runtime_error("DOF controller feedback must contain both complete indirect records");
        auto& out=sides_.at(side);
        D3D11_BUFFER_DESC d{};d.ByteWidth=bytes;d.Usage=D3D11_USAGE_DEFAULT;
        d.BindFlags=D3D11_BIND_SHADER_RESOURCE;d.MiscFlags=D3D11_RESOURCE_MISC_BUFFER_ALLOW_RAW_VIEWS;
        check(device->CreateBuffer(&d,nullptr,&out.buffer),"DOF resident controller feedback");
        D3D11_SHADER_RESOURCE_VIEW_DESC s{};s.Format=DXGI_FORMAT_R32_TYPELESS;
        s.ViewDimension=D3D11_SRV_DIMENSION_BUFFEREX;s.BufferEx.NumElements=bytes/4;
        s.BufferEx.Flags=D3D11_BUFFEREX_SRV_FLAG_RAW;
        check(device->CreateShaderResourceView(out.buffer.Get(),&s,&out.view),"DOF resident feedback raw view");
    }
    void prepare(ID3D11Device* device) {
        if(!sides_[0].buffer||!sides_[1].buffer)throw std::runtime_error("DOF controller requires both historical sides");
        D3D11_BUFFER_DESC d{};d.ByteWidth=32;d.Usage=D3D11_USAGE_DEFAULT;
        d.BindFlags=D3D11_BIND_UNORDERED_ACCESS;
        d.MiscFlags=D3D11_RESOURCE_MISC_DRAWINDIRECT_ARGS|D3D11_RESOURCE_MISC_BUFFER_ALLOW_RAW_VIEWS;
        check(device->CreateBuffer(&d,nullptr,&clearArguments_),"DOF clear indirect arguments");
        D3D11_UNORDERED_ACCESS_VIEW_DESC u{};u.Format=DXGI_FORMAT_R32_TYPELESS;
        u.ViewDimension=D3D11_UAV_DIMENSION_BUFFER;u.Buffer.NumElements=8;u.Buffer.Flags=D3D11_BUFFER_UAV_FLAG_RAW;
        check(device->CreateUnorderedAccessView(clearArguments_.Get(),&u,&clearArgumentsView_),"DOF clear argument UAV");
        static constexpr char control[]=R"hlsl(
ByteAddressBuffer Previous : register(t0);
RWByteAddressBuffer ClearArguments : register(u0);
[numthreads(1,1,1)] void PrepareClears(uint3 id : SV_DispatchThreadID) {
    ClearArguments.Store4(0,uint4(3,Previous.Load(4)!=0?1u:0u,0,0));
    ClearArguments.Store4(16,uint4(3,Previous.Load(20)!=0?1u:0u,0,0));
}
)hlsl";
        static constexpr char fill[]=R"hlsl(
float4 ClearVertex(uint id : SV_VertexID) : SV_Position {
    const float2 p[3]={float2(-1,-1),float2(-1,3),float2(3,-1)};
    return float4(p[id],0,1);
}
float4 ClearPixel() : SV_Target0 { return float4(0,0,0,0); }
)hlsl";
        auto cs=compile(control,sizeof(control)-1,"PrepareClears","cs_5_0");
        check(device->CreateComputeShader(cs->GetBufferPointer(),cs->GetBufferSize(),nullptr,&prepareShader_),"DOF GPU clear control shader");
        auto vs=compile(fill,sizeof(fill)-1,"ClearVertex","vs_5_0");
        check(device->CreateVertexShader(vs->GetBufferPointer(),vs->GetBufferSize(),nullptr,&clearVertex_),"DOF zero fill vertex shader");
        auto ps=compile(fill,sizeof(fill)-1,"ClearPixel","ps_5_0");
        check(device->CreatePixelShader(ps->GetBufferPointer(),ps->GetBufferSize(),nullptr,&clearPixel_),"DOF zero fill pixel shader");
        D3D11_BLEND_DESC blend{};auto& target=blend.RenderTarget[0];
        target.SrcBlend=target.SrcBlendAlpha=D3D11_BLEND_ONE;
        target.DestBlend=target.DestBlendAlpha=D3D11_BLEND_ZERO;
        target.BlendOp=target.BlendOpAlpha=D3D11_BLEND_OP_ADD;
        target.RenderTargetWriteMask=D3D11_COLOR_WRITE_ENABLE_ALL;
        check(device->CreateBlendState(&blend,&clearBlend_),"DOF zero fill blend");
        D3D11_DEPTH_STENCIL_DESC depth{};depth.DepthWriteMask=D3D11_DEPTH_WRITE_MASK_ZERO;depth.DepthFunc=D3D11_COMPARISON_ALWAYS;
        depth.StencilReadMask=depth.StencilWriteMask=D3D11_DEFAULT_STENCIL_READ_MASK;
        depth.FrontFace.StencilFailOp=depth.FrontFace.StencilDepthFailOp=depth.FrontFace.StencilPassOp=D3D11_STENCIL_OP_KEEP;
        depth.FrontFace.StencilFunc=D3D11_COMPARISON_ALWAYS;depth.BackFace=depth.FrontFace;
        check(device->CreateDepthStencilState(&depth,&clearDepth_),"DOF zero fill depth");
        D3D11_RASTERIZER_DESC raster{};raster.FillMode=D3D11_FILL_SOLID;raster.CullMode=D3D11_CULL_NONE;raster.DepthClipEnable=TRUE;
        check(device->CreateRasterizerState(&raster,&clearRaster_),"DOF zero fill raster");
    }
    ID3D11Buffer* sideBuffer(UINT side)const{return sides_.at(side).buffer.Get();}
    // Called at the original Before57 point, before current Buffer5 writes.
    // Unbind UAV/SRV immediately so later Copy and indirect consumption have
    // standard D3D11 read/write transitions, without a CPU completion wait.
    void prepareClearArguments(ID3D11DeviceContext* context,UINT side) {
        auto* input=sides_.at(side).view.Get();auto* output=clearArgumentsView_.Get();
        context->CSSetShaderResources(0,1,&input);
        context->CSSetUnorderedAccessViews(0,1,&output,nullptr);
        context->CSSetShader(prepareShader_.Get(),nullptr,0);context->Dispatch(1,1,1);
        ID3D11ShaderResourceView* noInput=nullptr;ID3D11UnorderedAccessView* noOutput=nullptr;
        context->CSSetShaderResources(0,1,&noInput);context->CSSetUnorderedAccessViews(0,1,&noOutput,nullptr);
        context->CSSetShader(nullptr,nullptr,0);
    }
    void clearTarget(ID3D11DeviceContext* context,ID3D11RenderTargetView* target,UINT width,UINT height,UINT offset) {
        // Full target/mip coverage, independent of scatter viewport/scissor.
        // Zero instances leaves every existing bit untouched; one writes +0.
        context->OMSetRenderTargets(1,&target,nullptr);
        context->OMSetBlendState(clearBlend_.Get(),nullptr,~0u);context->OMSetDepthStencilState(clearDepth_.Get(),0);
        context->RSSetState(clearRaster_.Get());
        D3D11_VIEWPORT viewport{0,0,float(width),float(height),0,1};context->RSSetViewports(1,&viewport);
        context->IASetInputLayout(nullptr);context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
        context->VSSetShader(clearVertex_.Get(),nullptr,0);context->PSSetShader(clearPixel_.Get(),nullptr,0);
        context->DrawInstancedIndirect(clearArguments_.Get(),offset);
    }
private:
    static ComPtr<ID3DBlob> compile(const char* source,size_t length,const char* entry,const char* profile) {
        ComPtr<ID3DBlob> code,errors;
        const HRESULT result=D3DCompile(source,length,"project-dof-gpu-feedback",nullptr,nullptr,entry,profile,
            D3DCOMPILE_ENABLE_STRICTNESS|D3DCOMPILE_OPTIMIZATION_LEVEL3,0,&code,&errors);
        if(FAILED(result))throw std::runtime_error(std::string("DOF GPU control shader compilation failed: ")+
            (errors?std::string(static_cast<const char*>(errors->GetBufferPointer()),errors->GetBufferSize()):entry));
        return code;
    }
    struct Side {ComPtr<ID3D11Buffer> buffer;ComPtr<ID3D11ShaderResourceView> view;};
    std::array<Side,2> sides_;
    ComPtr<ID3D11Buffer> clearArguments_;
    ComPtr<ID3D11UnorderedAccessView> clearArgumentsView_;
    ComPtr<ID3D11ComputeShader> prepareShader_;
    ComPtr<ID3D11VertexShader> clearVertex_;
    ComPtr<ID3D11PixelShader> clearPixel_;
    ComPtr<ID3D11BlendState> clearBlend_;
    ComPtr<ID3D11DepthStencilState> clearDepth_;
    ComPtr<ID3D11RasterizerState> clearRaster_;
};
}
