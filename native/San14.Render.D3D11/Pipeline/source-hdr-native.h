// Normal node78, node123 and node166 fullscreen draws into retained node73 HDR.
// Actual selection, bindings, CBs and per-record stencil come from the exporter.
#include <optional>
#include "hdr-population-gate-native.h"
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
static ComPtr<ID3D11Texture2D> createSourceHdr(ID3D11Device* device,const fs::path& firstPass,
    UINT width,UINT height) {
    std::string key;UINT resource,shader,target,w,h;
    std::ifstream input(firstPass/"target.txt");input>>key>>resource>>shader>>target>>w>>h;
    if(!input||w!=width||h!=height)throw std::runtime_error("Source HDR target extent mismatch");
    D3D11_TEXTURE2D_DESC td{};td.Width=w;td.Height=h;td.ArraySize=td.MipLevels=1;td.SampleDesc.Count=1;
    td.Format=DXGI_FORMAT(resource);td.BindFlags=D3D11_BIND_RENDER_TARGET|D3D11_BIND_SHADER_RESOURCE;
    ComPtr<ID3D11Texture2D> texture;check(device->CreateTexture2D(&td,nullptr,&texture),"actual node73 HDR");return texture;
}
enum class NativeSourceHdrProducer { SampledD1,Ambient,Refraction,Transmitted,Specular,BlurredDepth };
struct NativeSourceHdrConstant {
    bool vertex;
    UINT slot;
    std::string file;
    std::vector<char> bytes;
    ComPtr<ID3D11Buffer> buffer;
};
struct NativeSourceHdrSampler {
    UINT slot;
    D3D11_SAMPLER_DESC description;
    ComPtr<ID3D11SamplerState> state;
};
struct NativeSourceHdrDynamicBinding {
    UINT slot;
    NativeSourceHdrProducer producer;
    std::string role;
};
struct NativeSourceHdrPass {
    std::string identity;
    std::optional<source_hdr_population::Gate> populationGate;
    std::vector<char> vertexDxbc,pixelDxbc,materialBytes;
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
    UINT stencilEnable=0,stencilCompare=0,reference=0,readMask=0,writeMask=0;
    UINT depthTest=0,sourceDepthWrite=0,sourceDepthCompare=0,cull=0,blendEnabled=0;
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
static NativeSourceHdrPasses prepareSourceHdrPasses(ID3D11Device* device,
    const fs::path& source,const char* passList,ID3D11Texture2D* hdr,
    const std::array<ComPtr<ID3D11Texture2D>,5>& gbuffer,const std::array<DXGI_FORMAT,6>& formats,
    ID3D11Texture2D* ggx,bool useCurrentPopulation=false) {
    NativeSourceHdrPasses result;
    auto view=[&](ID3D11Texture2D* texture,DXGI_FORMAT format) {
        D3D11_TEXTURE2D_DESC td{};texture->GetDesc(&td);D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=format;
        if(td.ArraySize>1){sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2DARRAY;sd.Texture2DArray.ArraySize=td.ArraySize;sd.Texture2DArray.MipLevels=td.MipLevels;}
        else{sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sd.Texture2D.MipLevels=td.MipLevels;}
        ComPtr<ID3D11ShaderResourceView> resource;
        check(device->CreateShaderResourceView(texture,&sd,&resource),"source HDR exact SRV");return resource;
    };
    for(UINT i=0;i<4;++i)result.gbuffer[i]=view(gbuffer[i].Get(),formats[i]);
    result.ggx=view(ggx,DXGI_FORMAT_R16G16_FLOAT);
    std::ifstream passes(source/passList);std::string relative;
    if(!passes)throw std::runtime_error("Source HDR pass list absent");
    while(passes>>relative) {
        NativeSourceHdrPass pass;pass.identity=relative;fs::path directory=source/relative;
        if(useCurrentPopulation)pass.populationGate=source_hdr_population::load(directory);
        pass.vertexDxbc=load(directory/"vs.dxbc");pass.pixelDxbc=load(directory/"ps.dxbc");
        check(device->CreateVertexShader(pass.vertexDxbc.data(),pass.vertexDxbc.size(),nullptr,&pass.vs),"source HDR selected VS");
        check(device->CreatePixelShader(pass.pixelDxbc.data(),pass.pixelDxbc.size(),nullptr,&pass.ps),"source HDR selected PS");
        std::string stage,file,role,key;UINT slot;
        std::ifstream cbuffers(directory/"cbuffers.txt");
        if(!cbuffers)throw std::runtime_error("Source HDR constant buffer list absent");
        while(cbuffers>>stage>>slot>>file) {
            if(stage!="VS"&&stage!="PS")throw std::runtime_error("Unsupported source HDR constant stage: "+stage);
            NativeSourceHdrConstant cb{stage=="VS",slot,file,load(directory/file),{}};
            cb.buffer=sourceHdrDefaultBuffer(device,cb.bytes,D3D11_BIND_CONSTANT_BUFFER);
            pass.constants.push_back(std::move(cb));
        }
        if(fs::exists(directory/"material-table.bin")) {
            pass.materialBytes=load(directory/"material-table.bin");
            pass.material=sourceHdrDefaultBuffer(device,pass.materialBytes,D3D11_BIND_SHADER_RESOURCE);
            D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=DXGI_FORMAT_R32_UINT;
            sd.ViewDimension=D3D11_SRV_DIMENSION_BUFFER;sd.Buffer.NumElements=UINT(pass.materialBytes.size()/4);
            check(device->CreateShaderResourceView(pass.material.Get(),&sd,&pass.materialView),"source HDR material table");
        }
        std::ifstream textures(directory/"textures.txt");
        if(!textures)throw std::runtime_error("Source HDR texture list absent");
        while(textures>>slot>>role>>key) {
            ID3D11ShaderResourceView* fixed=nullptr;NativeSourceHdrProducer producer{};bool dynamic=false;
            if(role=="AB"||role=="GBufferAB")fixed=result.gbuffer[0].Get();
            else if(role=="NS"||role=="GBufferNS")fixed=result.gbuffer[1].Get();
            else if(role=="RS"||role=="GBufferRS")fixed=result.gbuffer[2].Get();
            else if(role=="RR"||role=="GBufferRR")fixed=result.gbuffer[3].Get();
            else if(role=="Ggx"||role=="GGX")fixed=result.ggx.Get();
            else if(role=="MaterialTable")fixed=pass.materialView.Get();
            else if(role=="D1"||role=="GBufferDepth"){producer=NativeSourceHdrProducer::SampledD1;dynamic=true;}
            else if(role=="AmbientSpecular"){producer=NativeSourceHdrProducer::Ambient;dynamic=true;}
            else if(role=="RefractionDepth"){producer=NativeSourceHdrProducer::Refraction;dynamic=true;}
            else if(role=="TransmittedSpecular"){producer=NativeSourceHdrProducer::Transmitted;dynamic=true;}
            else if(role=="TranslucentSpecular"){producer=NativeSourceHdrProducer::Specular;dynamic=true;}
            else if(role=="BlurredDepth"){producer=NativeSourceHdrProducer::BlurredDepth;dynamic=true;}
            else if(role!="SourceNullCaustics"&&!(role=="Hatching"&&key=="00000000"))throw std::runtime_error("Unknown source HDR role: "+role);
            pass.fixedPixelViews.at(slot)=fixed;
            if(dynamic)pass.dynamicBindings.push_back({slot,producer,role});
            else if(!fixed&&role!="SourceNullCaustics"&&!(role=="Hatching"&&key=="00000000"))throw std::runtime_error("Source HDR fixed producer absent: "+role);
        }
        std::ifstream samplerRows(directory/"samplers.txt");
        if(!samplerRows)throw std::runtime_error("Source HDR sampler list absent");
        while(samplerRows>>slot>>file) {
            auto bytes=load(directory/file);NativeSourceHdrSampler sampler{slot,{},{}};
            if(bytes.size()!=sizeof(sampler.description))throw std::runtime_error("Source HDR sampler requires52B");
            std::memcpy(&sampler.description,bytes.data(),sizeof(sampler.description));
            check(device->CreateSamplerState(&sampler.description,&sampler.state),"source HDR sampler");
            pass.samplers.push_back(std::move(sampler));
        }
        std::ifstream stencil(directory/"stencil.txt"),depth(directory/"depth.txt"),raster(directory/"raster-cull.txt"),blend(directory/"blend-enabled.txt");
        stencil>>pass.stencilEnable>>pass.stencilCompare>>pass.reference>>pass.readMask>>pass.writeMask;
        depth>>pass.depthTest>>pass.sourceDepthWrite>>pass.sourceDepthCompare;raster>>pass.cull;blend>>pass.blendEnabled;
        if(!stencil||!depth||!raster||!blend)throw std::runtime_error("Source HDR state absent");
        D3D11_BLEND_DESC bd{};bd.RenderTarget[0].BlendEnable=pass.blendEnabled;bd.RenderTarget[0].RenderTargetWriteMask=15;
        check(device->CreateBlendState(&bd,&pass.blend),"source HDR source blend");
        UINT resource,srvFormat,targetFormat,w,h;std::ifstream target(directory/"target.txt");
        target>>key>>resource>>srvFormat>>targetFormat>>w>>h;
        std::ifstream viewport(directory/"viewport.txt"),geometry(directory/"geometry.txt");
        viewport>>pass.viewport.TopLeftX>>pass.viewport.TopLeftY>>pass.viewport.Width>>pass.viewport.Height>>pass.viewport.MinDepth>>pass.viewport.MaxDepth;
        geometry>>pass.count>>pass.topology;
        if(!target||!viewport||!geometry)throw std::runtime_error("Source HDR viewport/geometry absent");
        D3D11_RENDER_TARGET_VIEW_DESC rd{};rd.Format=DXGI_FORMAT(targetFormat);rd.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D;
        check(device->CreateRenderTargetView(hdr,&rd,&pass.target),"retained node73 HDR RTV");
        result.passes.push_back(std::move(pass));
    }
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
    ID3D11ShaderResourceView* transmitted,ID3D11ShaderResourceView* specular=nullptr,
    ID3D11ShaderResourceView* blurredDepth=nullptr,float currentCommonClock=0.f,
    uint32_t view19AC=~0u,uint32_t view19B0=~0u) {
    // These selected layouts have no direct Time field. The only indirect
    // clock reader is late383 Hatching UV: source reset rates40/44 are zero,
    // so D724C0/9A00E0 leaves its exact identity/-0/+0 words unchanged.
    (void)currentCommonClock;
    context->OMSetRenderTargets(0,nullptr,nullptr);
    for(auto& pass:cache.passes) {
        if(pass.populationGate&&!source_hdr_population::admitted(*pass.populationGate,view19AC,view19B0))continue;
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
                case NativeSourceHdrProducer::Specular:selected=specular;break;
                case NativeSourceHdrProducer::BlurredDepth:selected=blurredDepth;break;
            }
            if(!selected)throw std::runtime_error("Source HDR producer absent: "+binding.role);
            views[binding.slot]=selected;
        }
        context->PSSetShaderResources(0,UINT(views.size()),views.data());
        for(auto& sampler:pass.samplers){auto raw=sampler.state.Get();context->PSSetSamplers(sampler.slot,1,&raw);}
        D3D11_DEPTH_STENCIL_DESC saved=logical.description;UINT savedReference=logical.reference;
        // The actual source writer changes testing and per-record stencil.
        // The exported write/compare declarations are not additional writers.
        D3D11_DEPTH_STENCIL_DESC ds=saved;ds.DepthEnable=pass.depthTest;
        ds.StencilEnable=pass.stencilEnable;ds.StencilReadMask=BYTE(pass.readMask);ds.StencilWriteMask=BYTE(pass.writeMask);
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
static void drawSourceHdrPasses(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical,
    const fs::path& source,const char* passList,ID3D11Texture2D* hdr,
    const std::array<ComPtr<ID3D11Texture2D>,5>& gbuffer,const std::array<DXGI_FORMAT,6>& formats,
    ID3D11ShaderResourceView* sampledD1,ID3D11DepthStencilView* dsv,ID3D11Texture2D* ggx,
    ID3D11ShaderResourceView* ambient,ID3D11ShaderResourceView* refraction,
    ID3D11ShaderResourceView* transmitted,ID3D11ShaderResourceView* specular=nullptr,
    ID3D11ShaderResourceView* blurredDepth=nullptr,float currentCommonClock=0.f) {
    auto cache=prepareSourceHdrPasses(device,source,passList,hdr,gbuffer,formats,ggx);
    executeSourceHdrPasses(device,context,logical,cache,sampledD1,dsv,ambient,refraction,transmitted,specular,blurredDepth,currentCommonClock);
}
