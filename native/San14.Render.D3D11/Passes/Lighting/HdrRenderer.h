// Normal node78, node123 and node166 fullscreen draws into retained node73 HDR.
// Current bank selection owns bindings and buffers; logical depth/raster state
// remains inherited at each selected fullscreen draw.
#include <optional>
#include "HdrLightingState.h"
#include "Interop/HdrProgramBindingInputs.h"
static void applySourceModelStencil(ID3D11Device* device,NativeModelInputs& models,const fs::path& source) {
    std::ifstream rows(source/"model-references.txt");std::string directory;
    UINT enabled,reference,readMask,writeMask,compare,fail,depthFail,pass;
    if(!rows)throw std::runtime_error("Source model stencil references absent");
    while(rows>>directory>>enabled>>reference>>readMask>>writeMask>>compare>>fail>>depthFail>>pass) {
        auto found=std::find_if(models.draws.begin(),models.draws.end(),[&](const auto& draw){return draw.identity==directory;});
        if(found==models.draws.end())continue; // The source census can cover a larger imported population.
        D3D11_DEPTH_STENCIL_DESC ds{};found->depth->GetDesc(&ds);ds.StencilEnable=enabled;
        ds.StencilReadMask=BYTE(readMask);ds.StencilWriteMask=BYTE(writeMask);ds.FrontFace.StencilFunc=D3D11_COMPARISON_FUNC(compare);
        ds.FrontFace.StencilFailOp=D3D11_STENCIL_OP(fail);ds.FrontFace.StencilDepthFailOp=D3D11_STENCIL_OP(depthFail);ds.FrontFace.StencilPassOp=D3D11_STENCIL_OP(pass);ds.BackFace=ds.FrontFace;
        check(device->CreateDepthStencilState(&ds,&found->depth),"source ordinary model stencil");found->stencilReference=reference;
    }
}
struct NativeSourceHdrTarget {
    ComPtr<ID3D11Texture2D> texture;
    std::string logicalKey;
    DXGI_FORMAT shaderFormat{},targetFormat{};
};
static NativeSourceHdrTarget createSourceHdr(ID3D11Device* device,const fs::path& firstPass,
    UINT width,UINT height) {
    std::string key;UINT resource,shader,target,w,h;
    std::ifstream input(firstPass/"target.txt");input>>key>>resource>>shader>>target>>w>>h;
    if(!input||w!=width||h!=height)throw std::runtime_error("Source HDR target extent mismatch");
    NativeSourceHdrTarget result;result.logicalKey=key;
    result.shaderFormat=DXGI_FORMAT(shader);result.targetFormat=DXGI_FORMAT(target);
    D3D11_TEXTURE2D_DESC td{};td.Width=w;td.Height=h;td.ArraySize=td.MipLevels=1;td.SampleDesc.Count=1;
    td.Format=DXGI_FORMAT(resource);td.BindFlags=D3D11_BIND_RENDER_TARGET|D3D11_BIND_SHADER_RESOURCE;
    check(device->CreateTexture2D(&td,nullptr,&result.texture),"actual node73 HDR");return result;
}
struct NativeSourceHdrSpecularImages {
    std::array<ID3D11ShaderResourceView*,2> views{};
    std::array<std::string_view,2> logicalKeys{};
};
enum class NativeSourceHdrProducer { SampledD1,Ambient,Refraction,Transmitted,BlurredDepth };
struct NativeSourceHdrConstant {
    bool vertex;
    UINT slot;
    std::string file;
    std::vector<char> bytes;
    ComPtr<ID3D11Buffer> buffer;
};
struct NativeSourceHdrSampler {
    UINT slot;
    std::array<D3D11_SAMPLER_DESC,2> descriptions;
    std::array<ComPtr<ID3D11SamplerState>,2> states;
    bool paired{};
};
struct NativeSourceHdrDynamicBinding {
    UINT slot;
    NativeSourceHdrProducer producer;
    std::string role;
};
struct NativeSourceHdrPass {
    std::string identity;
    std::size_t lightingConstantIndex{};
    unsigned specularSourceIndex{};
    ComPtr<ID3D11VertexShader> vs;
    ComPtr<ID3D11PixelShader> ps;
    std::vector<NativeSourceHdrConstant> constants;
    ComPtr<ID3D11Buffer> material;
    ComPtr<ID3D11ShaderResourceView> materialView;
    std::array<ID3D11ShaderResourceView*,32> fixedPixelViews{};
    std::vector<NativeSourceHdrDynamicBinding> dynamicBindings;
    std::vector<NativeSourceHdrSampler> samplers;
    ComPtr<ID3D11RenderTargetView> target;
    D3D11_VIEWPORT viewport{};
    UINT count=0,topology=0;
    UINT stencilEnable=0,stencilCompare=0,reference=0,readMask=0;
    UINT depthTest=0,cull=0;
    ComPtr<ID3D11BlendState> blend;
};
struct NativeSourceHdrDepthObject {
    D3D11_DEPTH_STENCIL_DESC description;
    ComPtr<ID3D11DepthStencilState> state;
};
struct NativeSourceHdrRasterObject {
    ComPtr<ID3D11RasterizerState> parent,state;
    UINT cull;
};
struct NativeSourceHdrPasses {
    std::vector<NativeSourceHdrPass> passes;
    std::array<ComPtr<ID3D11ShaderResourceView>,4> gbuffer;
    ComPtr<ID3D11ShaderResourceView> ggx;
    // Actual logical descriptions determine the inherited depth fields.
    // These small object caches are filled when an entry state is first used.
    std::vector<NativeSourceHdrDepthObject> depthObjects;
    std::vector<NativeSourceHdrRasterObject> rasterObjects;
};
static ComPtr<ID3D11Buffer> sourceHdrDefaultBuffer(ID3D11Device* device,
    const std::vector<char>& bytes,UINT bind) {
    D3D11_BUFFER_DESC description{};description.ByteWidth=UINT(bytes.size());
    description.Usage=D3D11_USAGE_DEFAULT;description.BindFlags=bind;
    D3D11_SUBRESOURCE_DATA initial{};initial.pSysMem=bytes.data();
    ComPtr<ID3D11Buffer> result;
    check(device->CreateBuffer(&description,&initial,&result),"source HDR complete DEFAULT buffer");
    return result;
}
static bool sameSourceHdrDepthDescription(const D3D11_DEPTH_STENCIL_DESC& a,const D3D11_DEPTH_STENCIL_DESC& b) {
    auto face=[](const D3D11_DEPTH_STENCILOP_DESC& x,const D3D11_DEPTH_STENCILOP_DESC& y) {
        return x.StencilFailOp==y.StencilFailOp&&x.StencilDepthFailOp==y.StencilDepthFailOp&&x.StencilPassOp==y.StencilPassOp&&x.StencilFunc==y.StencilFunc;
    };
    return a.DepthEnable==b.DepthEnable&&a.DepthWriteMask==b.DepthWriteMask&&a.DepthFunc==b.DepthFunc&&
        a.StencilEnable==b.StencilEnable&&a.StencilReadMask==b.StencilReadMask&&a.StencilWriteMask==b.StencilWriteMask&&
        face(a.FrontFace,b.FrontFace)&&face(a.BackFace,b.BackFace);
}
static ID3D11DepthStencilState* sourceHdrDepthObject(ID3D11Device* device,NativeSourceHdrPasses& cache,
    const D3D11_DEPTH_STENCIL_DESC& requested) {
    for(auto& object:cache.depthObjects)if(sameSourceHdrDepthDescription(object.description,requested))return object.state.Get();
    NativeSourceHdrDepthObject object{requested,{}};
    check(device->CreateDepthStencilState(&requested,&object.state),"source HDR inherited depth/stencil");
    cache.depthObjects.push_back(std::move(object));return cache.depthObjects.back().state.Get();
}
static ID3D11RasterizerState* sourceHdrRasterObject(ID3D11Device* device,ID3D11DeviceContext* context,
    NativeSourceHdrPasses& cache,UINT cull) {
    ComPtr<ID3D11RasterizerState> parent;context->RSGetState(&parent);
    for(auto& object:cache.rasterObjects) {
        if(object.cull==cull&&(object.parent.Get()==parent.Get()||object.state.Get()==parent.Get()))return object.state.Get();
    }
    // Raster fields remain active. Read a newly encountered immutable parent
    // once; no hardware depth/stencil GetDesc is used to infer source values.
    D3D11_RASTERIZER_DESC rs{};rs.FillMode=D3D11_FILL_SOLID;rs.DepthClipEnable=TRUE;
    if(parent)parent->GetDesc(&rs);rs.CullMode=D3D11_CULL_MODE(cull);
    NativeSourceHdrRasterObject object{parent,{},cull};
    check(device->CreateRasterizerState(&rs,&object.state),"source HDR inherited fullscreen raster");
    cache.rasterObjects.push_back(std::move(object));return cache.rasterObjects.back().state.Get();
}
static void executeSourceHdrPasses(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical,
    NativeSourceHdrPasses& cache,ID3D11ShaderResourceView* sampledD1,ID3D11DepthStencilView* dsv,
    ID3D11ShaderResourceView* ambient,ID3D11ShaderResourceView* refraction,
    ID3D11ShaderResourceView* transmitted,
    ID3D11ShaderResourceView* blurredDepth=nullptr,float currentCommonClock=0.f,
    uint32_t view19AC=~0u,uint32_t view19B0=~0u) {
    // These selected layouts have no direct Time field. The only indirect
    // clock reader is late383 Hatching UV: source reset rates40/44 are zero,
    // so D724C0/9A00E0 leaves its exact identity/-0/+0 words unchanged.
    (void)currentCommonClock; (void)view19AC; (void)view19B0;
    context->OMSetRenderTargets(0,nullptr,nullptr);
    for(auto& pass:cache.passes) {
        for(auto& cb:pass.constants) {
            auto raw=cb.buffer.Get();
            if(cb.vertex)context->VSSetConstantBuffers(cb.slot,1,&raw);else context->PSSetConstantBuffers(cb.slot,1,&raw);
        }
        auto views=pass.fixedPixelViews;
        for(auto& binding:pass.dynamicBindings) {
            ID3D11ShaderResourceView* selected=nullptr;
            switch(binding.producer) {
                case NativeSourceHdrProducer::SampledD1:selected=sampledD1;break;
                case NativeSourceHdrProducer::Ambient:selected=ambient;break;
                case NativeSourceHdrProducer::Refraction:selected=refraction;break;
                case NativeSourceHdrProducer::Transmitted:selected=transmitted;break;
                case NativeSourceHdrProducer::BlurredDepth:selected=blurredDepth;break;
            }
            if(!selected)throw std::runtime_error("Source HDR producer absent: "+binding.role);
            views[binding.slot]=selected;
        }
        context->PSSetShaderResources(0,UINT(views.size()),views.data());
        for(auto& sampler:pass.samplers){auto raw=sampler.states[San14::Render::hdrSamplerSide(sampler.paired,pass.specularSourceIndex)].Get();context->PSSetSamplers(sampler.slot,1,&raw);}
        D3D11_DEPTH_STENCIL_DESC saved=logical.description;UINT savedReference=logical.reference;
        // The actual source writer changes testing and per-record stencil.
        // The exported write/compare declarations are not additional writers.
        D3D11_DEPTH_STENCIL_DESC ds=saved;ds.DepthEnable=pass.depthTest;
        ds.StencilEnable=pass.stencilEnable;ds.StencilReadMask=BYTE(pass.readMask);
        ds.FrontFace.StencilFunc=D3D11_COMPARISON_FUNC(pass.stencilCompare);
        ds.FrontFace.StencilFailOp=ds.FrontFace.StencilDepthFailOp=ds.FrontFace.StencilPassOp=D3D11_STENCIL_OP_KEEP;ds.BackFace=ds.FrontFace;
        auto depth=sourceHdrDepthObject(device,cache,ds);
        auto raster=sourceHdrRasterObject(device,context,cache,pass.cull);
        context->IASetInputLayout(nullptr);context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY(pass.topology));
        context->VSSetShader(pass.vs.Get(),nullptr,0);context->PSSetShader(pass.ps.Get(),nullptr,0);
        context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);context->GSSetShader(nullptr,nullptr,0);
        context->RSSetState(raster);context->RSSetViewports(1,&pass.viewport);
        logical.bind(context,depth,ds,pass.reference);context->OMSetBlendState(pass.blend.Get(),nullptr,~0u);
        auto raw=pass.target.Get();context->OMSetRenderTargets(1,&raw,dsv);context->Draw(pass.count,0);
        context->OMSetRenderTargets(0,nullptr,nullptr);
        std::array<ID3D11ShaderResourceView*,32> empty{};context->PSSetShaderResources(0,UINT(empty.size()),empty.data());
        // Actual source end forces testing on, restores entry stencil/ref,
        // retains write/compare and leaves its source cull/blend installed.
        saved.DepthEnable=TRUE;
        logical.bind(context,sourceHdrDepthObject(device,cache,saved),saved,savedReference);
        std::cout<<"source HDR "<<pass.identity<<" draw="<<pass.count<<" stencil="<<pass.reference<<'/'<<pass.readMask<<" no-clear\n";
    }
}
