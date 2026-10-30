// Include after model-native.h and the terrain.cpp load/check/buffer helpers.
// One fixed real-asset caster experiment. Root supplies the actual reached
// depth/raster state; this file never guesses a source depth compare policy.
#pragma once
#include "../../Runtime/PreparedFileInput.h"
#include <d3dcompiler.h>
#include <d3d11shader.h>

struct NativeShadowMap {
    ComPtr<ID3D11Texture2D> texture;
    ComPtr<ID3D11DepthStencilView> depth;
    ComPtr<ID3D11ShaderResourceView> view;
    UINT draws=0;
};
static NativeShadowMap produceNativeShadowMap(ID3D11Device* device,ID3D11DeviceContext* context,
    const fs::path& source,const fs::path& modelSource,const NativeModelInputs& models,
    ID3D11DepthStencilState* actualShadowDepth,ID3D11RasterizerState* actualShadowRaster) {
    UINT width,height,sourceClass;NativePreparedInputFile target(source/"target.txt");target>>width>>height>>sourceClass;
    if(!target||!actualShadowDepth||!actualShadowRaster)throw std::runtime_error("Actual shadow source target/depth/raster state absent");
    NativeShadowMap result;
    D3D11_TEXTURE2D_DESC td{};td.Width=width;td.Height=height;td.ArraySize=td.MipLevels=1;
    td.Format=DXGI_FORMAT_R32_TYPELESS;td.SampleDesc.Count=1;td.BindFlags=D3D11_BIND_DEPTH_STENCIL|D3D11_BIND_SHADER_RESOURCE;
    check(device->CreateTexture2D(&td,nullptr,&result.texture),"original EA shadow depth texture");
    D3D11_DEPTH_STENCIL_VIEW_DESC dd{};dd.Format=DXGI_FORMAT_D32_FLOAT;dd.ViewDimension=D3D11_DSV_DIMENSION_TEXTURE2D;
    check(device->CreateDepthStencilView(result.texture.Get(),&dd,&result.depth),"original EA DSV40");
    D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=DXGI_FORMAT_R32_FLOAT;sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sd.Texture2D.MipLevels=1;
    check(device->CreateShaderResourceView(result.texture.Get(),&sd,&result.view),"original EA SRV41");
    context->OMSetRenderTargets(0,nullptr,result.depth.Get());context->OMSetDepthStencilState(actualShadowDepth,0);
    context->ClearDepthStencilView(result.depth.Get(),D3D11_CLEAR_DEPTH,1.f,0); // Actual node18.
    context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);context->GSSetShader(nullptr,nullptr,0);
    context->PSSetShader(nullptr,nullptr,0); // Current ten category0 source primitives select a null PS.
    struct Pass {UINT index;D3D11_VIEWPORT viewport;D3D11_RECT scissor;};std::vector<Pass> passes;
    NativePreparedInputFile passFile(source/"passes.txt");UINT p;INT x,y,w,h,sx,sy,sw,sh;
    while(passFile>>p>>x>>y>>w>>h>>sx>>sy>>sw>>sh)passes.push_back({p,{float(x),float(y),float(w),float(h),0,1},{sx,sy,sx+sw,sy+sh}});
    NativePreparedInputFile rows(source/"draws.txt");UINT ordinal,mask;std::string relative,modelRelative;
    while(rows>>ordinal>>mask>>relative>>modelRelative) {
        const auto& model=models.draws.at(ordinal);auto dxbc=load(source/relative/"vs.dxbc");
        ComPtr<ID3D11VertexShader> vs;check(device->CreateVertexShader(dxbc.data(),dxbc.size(),nullptr,&vs),"original caster VS");
        ComPtr<ID3D11ShaderReflection> reflection;check(D3DReflect(dxbc.data(),dxbc.size(),__uuidof(ID3D11ShaderReflection),&reflection),"caster source ISGN");
        D3D11_SHADER_DESC signature{};reflection->GetDesc(&signature);
        struct Element {std::string name;UINT index,format,slot,offset;};std::vector<Element> elements;
        NativePreparedInputFile layoutFile(modelSource/modelRelative/"layout.txt");Element e;
        while(layoutFile>>e.name>>e.index>>e.format>>e.slot>>e.offset) {
            bool read=false;for(UINT i=0;i<signature.InputParameters;++i) {
                D3D11_SIGNATURE_PARAMETER_DESC input{};reflection->GetInputParameterDesc(i,&input);
                read|=input.SystemValueType==D3D_NAME_UNDEFINED&&e.name==input.SemanticName&&e.index==input.SemanticIndex;
            }
            if(read)elements.push_back(e);
        }
        std::vector<D3D11_INPUT_ELEMENT_DESC> desc;
        for(const auto& a:elements)desc.push_back({a.name.c_str(),a.index,DXGI_FORMAT(a.format),a.slot,a.offset,D3D11_INPUT_PER_VERTEX_DATA,0});
        ComPtr<ID3D11InputLayout> layout;check(device->CreateInputLayout(desc.data(),UINT(desc.size()),dxbc.data(),dxbc.size(),&layout),"real caster source layout");
        std::vector<ID3D11Buffer*> vbs;for(const auto& b:model.vertexBuffers)vbs.push_back(b.Get());std::vector<UINT> offsets(vbs.size(),0);
        context->IASetInputLayout(layout.Get());context->IASetVertexBuffers(0,UINT(vbs.size()),vbs.data(),model.strides.data(),offsets.data());
        context->IASetIndexBuffer(model.indices.Get(),model.indexBits==16?DXGI_FORMAT_R16_UINT:DXGI_FORMAT_R32_UINT,0);
        context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY(model.topology));context->VSSetShader(vs.Get(),nullptr,0);
        context->RSSetState(actualShadowRaster);
        for(const auto& pass:passes)if(mask&(1u<<pass.index)) {
            auto cb=buffer(device,load(source/relative/("pass-"+std::to_string(pass.index)+"-vs-b0.bin")),D3D11_BIND_CONSTANT_BUFFER);
            auto b=cb.Get();context->VSSetConstantBuffers(0,1,&b);context->RSSetViewports(1,&pass.viewport);context->RSSetScissorRects(1,&pass.scissor);
            context->DrawIndexed(model.indexCount,model.indexStart,model.baseVertex);++result.draws;
        }
    }
    // Root binds the next original pass and owns node20 restoration/order.
    context->OMSetRenderTargets(0,nullptr,nullptr);return result;
}
