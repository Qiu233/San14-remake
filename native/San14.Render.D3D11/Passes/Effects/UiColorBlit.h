#pragma once

namespace current_effect {
// Backend conversion for equal-sized current UI images. Texture Load preserves
// the selected source view's logical RGBA; the actual destination RTV performs
// its declared format conversion. There is no CPU readback or invented image.
class UiColorBlit {
    ComPtr<ID3D11VertexShader> vertex;
    ComPtr<ID3D11PixelShader> pixel;
    ComPtr<ID3D11PixelShader> offscreenResolve;
    ComPtr<ID3D11Buffer> resolveConstants;
    ComPtr<ID3D11BlendState> blend;
    ComPtr<ID3D11DepthStencilState> depth;
    ComPtr<ID3D11RasterizerState> raster;
public:
    explicit UiColorBlit(ID3D11Device* device) {
        constexpr char shader[]=R"(
Texture2D<float4> Image : register(t0);
cbuffer ResolveConstants : register(b0) {float InverseGamma;float3 Padding;};
float4 V(uint id:SV_VertexID):SV_Position {float2 xy=float2((id<<1)&2,id&2);return float4(xy*float2(2,-2)+float2(-1,1),0,1);}
float4 P(float4 p:SV_Position):SV_Target {return Image.Load(int3(int2(p.xy),0));}
// The fresh offscreen8361F814 owner has a null ToneScale image. In the
// original E88DA0 PS this skips every tone/LUT branch and retains this final
// log/exp gamma operation. Alpha is the accumulated background factor.
float4 R(float4 p:SV_Position):SV_Target {float4 c=Image.Load(int3(int2(p.xy),0));return float4(exp2(log2(abs(c.rgb))*InverseGamma),c.a);}
)";
        auto compile=[&](const char* entry,const char* profile) {
            ComPtr<ID3DBlob> code,errors;
            const auto hr=D3DCompile(shader,sizeof(shader)-1,"ui-color-blit",nullptr,nullptr,entry,profile,D3DCOMPILE_ENABLE_STRICTNESS,0,&code,&errors);
            if(FAILED(hr)){std::string message="UI color blit shader";if(errors)message.append(": ").append(static_cast<const char*>(errors->GetBufferPointer()),errors->GetBufferSize());throw std::runtime_error(message);}return code;
        };
        const auto vs=compile("V","vs_5_0"),ps=compile("P","ps_5_0"),resolve=compile("R","ps_5_0");
        check(device->CreateVertexShader(vs->GetBufferPointer(),vs->GetBufferSize(),nullptr,&vertex),"UI color blit VS");
        check(device->CreatePixelShader(ps->GetBufferPointer(),ps->GetBufferSize(),nullptr,&pixel),"UI color blit PS");
        check(device->CreatePixelShader(resolve->GetBufferPointer(),resolve->GetBufferSize(),nullptr,&offscreenResolve),"UI source offscreen gamma PS");
        D3D11_BUFFER_DESC cb{};cb.ByteWidth=16;cb.Usage=D3D11_USAGE_DEFAULT;cb.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
        check(device->CreateBuffer(&cb,nullptr,&resolveConstants),"UI source offscreen gamma constants");
        D3D11_BLEND_DESC bs{};for(auto& rt:bs.RenderTarget){rt.SrcBlend=rt.SrcBlendAlpha=D3D11_BLEND_ONE;
            rt.DestBlend=rt.DestBlendAlpha=D3D11_BLEND_ZERO;rt.BlendOp=rt.BlendOpAlpha=D3D11_BLEND_OP_ADD;rt.RenderTargetWriteMask=15;}
        check(device->CreateBlendState(&bs,&blend),"UI color blit replace state");
        D3D11_DEPTH_STENCIL_DESC ds{};ds.DepthFunc=D3D11_COMPARISON_ALWAYS;ds.StencilReadMask=ds.StencilWriteMask=255;
        ds.FrontFace.StencilFunc=D3D11_COMPARISON_ALWAYS;ds.FrontFace.StencilFailOp=ds.FrontFace.StencilDepthFailOp=ds.FrontFace.StencilPassOp=D3D11_STENCIL_OP_KEEP;ds.BackFace=ds.FrontFace;
        check(device->CreateDepthStencilState(&ds,&depth),"UI color blit no-depth state");
        D3D11_RASTERIZER_DESC rs{};rs.FillMode=D3D11_FILL_SOLID;rs.CullMode=D3D11_CULL_NONE;rs.DepthClipEnable=TRUE;
        check(device->CreateRasterizerState(&rs,&raster),"UI color blit raster");
    }
    void draw(ID3D11DeviceContext* context,ID3D11ShaderResourceView* image,ID3D11RenderTargetView* target,UINT width,UINT height) {
        drawWith(context,image,target,width,height,pixel.Get());
    }
    void resolve(ID3D11DeviceContext* context,ID3D11ShaderResourceView* image,ID3D11RenderTargetView* target,UINT width,UINT height,float gamma) {
        if(!(gamma>0))throw std::invalid_argument("UI source offscreen gamma");
        const float constants[4]{1.f/gamma,0,0,0};context->UpdateSubresource(resolveConstants.Get(),0,nullptr,constants,0,0);
        auto cb=resolveConstants.Get();context->PSSetConstantBuffers(0,1,&cb);
        drawWith(context,image,target,width,height,offscreenResolve.Get());
        cb=nullptr;context->PSSetConstantBuffers(0,1,&cb);
    }
private:
    void drawWith(ID3D11DeviceContext* context,ID3D11ShaderResourceView* image,ID3D11RenderTargetView* target,UINT width,UINT height,ID3D11PixelShader* shader) {
        if(!image||!target)throw std::invalid_argument("UI color blit requires its actual image and target");
        context->OMSetRenderTargets(1,&target,nullptr);context->OMSetBlendState(blend.Get(),nullptr,0xFFFFFFFFu);
        context->OMSetDepthStencilState(depth.Get(),0);context->RSSetState(raster.Get());
        D3D11_VIEWPORT port{0,0,FLOAT(width),FLOAT(height),0,1};context->RSSetViewports(1,&port);
        context->IASetInputLayout(nullptr);context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
        context->VSSetShader(vertex.Get(),nullptr,0);context->PSSetShader(shader,nullptr,0);
        context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);context->GSSetShader(nullptr,nullptr,0);
        context->PSSetShaderResources(0,1,&image);context->Draw(3,0);
        image=nullptr;context->PSSetShaderResources(0,1,&image);context->OMSetRenderTargets(0,nullptr,nullptr);
    }
};
}
