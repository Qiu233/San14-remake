#include "../../Runtime/PreparedFileInput.h"
// Original node68 NoSubShadow program applied to this experiment's native depth.
// CPU writers and the chosen bounded input scope are exported beside the DXBC.
static bool drawRs68(ID3D11Device* device,ID3D11DeviceContext* context,
    const fs::path& input,ID3D11Texture2D* rs,ID3D11Texture2D* geometryDepth,
    ID3D11DepthStencilView* geometryDsv,DXGI_FORMAT rsViewFormat,
    DXGI_FORMAT depthShaderFormat,UINT width,UINT height) {
    fs::path source=input/"rs68";
    if(!fs::exists(nativePreparedFilePath(source/"ps-b0.bin")))return false;
    auto vsCode=load(source/"nosub-vs.dxbc"),psCode=load(source/"nosub-ps.dxbc");
    ComPtr<ID3D11VertexShader> vs;ComPtr<ID3D11PixelShader> ps;
    check(device->CreateVertexShader(vsCode.data(),vsCode.size(),nullptr,&vs),"original68 VS");
    check(device->CreatePixelShader(psCode.data(),psCode.size(),nullptr,&ps),"original68 NoSub PS");
    auto constants=buffer(device,load(source/"ps-b0.bin"),D3D11_BIND_CONSTANT_BUFFER);
    D3D11_TEXTURE2D_DESC depthDesc{};geometryDepth->GetDesc(&depthDesc);depthDesc.BindFlags=D3D11_BIND_SHADER_RESOURCE;
    ComPtr<ID3D11Texture2D> depthCopy;check(device->CreateTexture2D(&depthDesc,nullptr,&depthCopy),"native68 DepthNs copy");
    context->CopyResource(depthCopy.Get(),geometryDepth);
    D3D11_SHADER_RESOURCE_VIEW_DESC view{};view.Format=depthShaderFormat;view.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;view.Texture2D.MipLevels=1;
    ComPtr<ID3D11ShaderResourceView> depthSrv;check(device->CreateShaderResourceView(depthCopy.Get(),&view,&depthSrv),"native68 depth SRV");
    std::string litChoice;NativePreparedInputFile(source/"lit.txt")>>litChoice;
    ComPtr<ID3D11Texture2D> shadow;ComPtr<ID3D11ShaderResourceView> litSrv;
    if(litChoice=="empty-shadow"){
        // The bounded terrain-only experiment has no Model caster. Execute the
        // real EA17/18 allocation/clear, rather than uploading a white image.
        D3D11_TEXTURE2D_DESC d{};d.Width=d.Height=4096;d.MipLevels=d.ArraySize=1;
        d.Format=DXGI_FORMAT_R32_TYPELESS;d.SampleDesc.Count=1;d.Usage=D3D11_USAGE_DEFAULT;d.BindFlags=D3D11_BIND_DEPTH_STENCIL|D3D11_BIND_SHADER_RESOURCE;
        check(device->CreateTexture2D(&d,nullptr,&shadow),"source EA depth");
        D3D11_DEPTH_STENCIL_VIEW_DESC ds{};ds.Format=DXGI_FORMAT_D32_FLOAT;ds.ViewDimension=D3D11_DSV_DIMENSION_TEXTURE2D;
        ComPtr<ID3D11DepthStencilView> shadowDsv;check(device->CreateDepthStencilView(shadow.Get(),&ds,&shadowDsv),"source EA DSV");
        context->ClearDepthStencilView(shadowDsv.Get(),D3D11_CLEAR_DEPTH,1,0);
        view.Format=DXGI_FORMAT_R32_FLOAT;check(device->CreateShaderResourceView(shadow.Get(),&view,&litSrv),"source EA SRV");
        std::cout<<"shadow17/18 nativeDepth=4096x4096 clear=1 modelCasters=0 scope=terrain-only\n";
    } else if(litChoice=="directional-diffuse"){
        shadow=loadTexture(device,source/"directional-diffuse.tex");
        check(device->CreateShaderResourceView(shadow.Get(),nullptr,&litSrv),"original directional diffuse SRV");
    } else if(litChoice!="null")throw std::runtime_error("rs68 lit.txt must state actual bounded input selection");
    D3D11_RENDER_TARGET_VIEW_DESC rtvDesc{};rtvDesc.Format=rsViewFormat;rtvDesc.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2DARRAY;rtvDesc.Texture2DArray.FirstArraySlice=3;rtvDesc.Texture2DArray.ArraySize=1;
    ComPtr<ID3D11RenderTargetView> target;check(device->CreateRenderTargetView(rs,&rtvDesc,&target),"RS3 RTV");
    D3D11_SAMPLER_DESC sampler{};sampler.Filter=D3D11_FILTER_MIN_MAG_MIP_POINT;sampler.AddressU=sampler.AddressV=sampler.AddressW=D3D11_TEXTURE_ADDRESS_CLAMP;sampler.MaxAnisotropy=1;sampler.ComparisonFunc=D3D11_COMPARISON_LESS_EQUAL;sampler.MaxLOD=0;
    std::string samplerStage;UINT samplerSlot,filter=0,u=3,v=3,w=3,aniso=1,comparison=4;float bias=0,minLod=0,maxLod=0;
    NativePreparedInputFile samplerFile(source/"ea-source-loader-sampler.txt");
    if(litChoice=="null"){
        auto sourceDescriptor=load(source/"null-backend-sampler.bin");
        if(sourceDescriptor.size()!=sizeof(sampler))throw std::runtime_error("Original null backend sampler must be 52 bytes");
        std::memcpy(&sampler,sourceDescriptor.data(),sizeof(sampler));
    }else if(litChoice=="directional-diffuse"){
        auto sourceDescriptor=load(source/"lit-sampler.bin");
        if(sourceDescriptor.size()!=sizeof(sampler))throw std::runtime_error("Original diffuse sampler must be 52 bytes");
        std::memcpy(&sampler,sourceDescriptor.data(),sizeof(sampler));
    }else if(samplerFile){samplerFile>>samplerStage>>samplerSlot>>filter>>u>>v>>w>>bias>>aniso>>comparison>>minLod>>maxLod;
        sampler.Filter=D3D11_FILTER(filter);sampler.AddressU=D3D11_TEXTURE_ADDRESS_MODE(u);sampler.AddressV=D3D11_TEXTURE_ADDRESS_MODE(v);sampler.AddressW=D3D11_TEXTURE_ADDRESS_MODE(w);sampler.MipLODBias=bias;sampler.MaxAnisotropy=aniso;sampler.ComparisonFunc=D3D11_COMPARISON_FUNC(comparison);sampler.MinLOD=minLod;sampler.MaxLOD=maxLod;}
    ComPtr<ID3D11SamplerState> samplerState;check(device->CreateSamplerState(&sampler,&samplerState),"native68 point/clamp sampler");
    D3D11_DEPTH_STENCIL_DESC depthState{};depthState.DepthEnable=FALSE;depthState.DepthWriteMask=D3D11_DEPTH_WRITE_MASK_ZERO;depthState.DepthFunc=D3D11_COMPARISON_ALWAYS;
    UINT compare=3,reference=0,readMask=255;NativePreparedInputFile(source/"raster.txt")>>compare>>reference>>readMask;
    depthState.StencilEnable=TRUE;depthState.StencilReadMask=uint8_t(readMask);depthState.StencilWriteMask=0;
    depthState.FrontFace.StencilFunc=depthState.BackFace.StencilFunc=D3D11_COMPARISON_FUNC(compare);
    depthState.FrontFace.StencilFailOp=depthState.FrontFace.StencilDepthFailOp=depthState.FrontFace.StencilPassOp=D3D11_STENCIL_OP_KEEP;depthState.BackFace=depthState.FrontFace;
    ComPtr<ID3D11DepthStencilState> ds;check(device->CreateDepthStencilState(&depthState,&ds),"native68 keep stencil");
    D3D11_BLEND_DESC blend{};blend.RenderTarget[0].RenderTargetWriteMask=D3D11_COLOR_WRITE_ENABLE_ALPHA;ComPtr<ID3D11BlendState> blendState;check(device->CreateBlendState(&blend,&blendState),"native68 alpha-only");
    D3D11_RASTERIZER_DESC raster{};raster.FillMode=D3D11_FILL_SOLID;raster.CullMode=D3D11_CULL_NONE;raster.DepthClipEnable=TRUE;
    ComPtr<ID3D11RasterizerState> rsState;check(device->CreateRasterizerState(&raster,&rsState),"native68 raster");
    context->IASetInputLayout(nullptr);context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
    context->VSSetShader(vs.Get(),nullptr,0);context->PSSetShader(ps.Get(),nullptr,0);auto cb=constants.Get();context->PSSetConstantBuffers(0,1,&cb);
    ID3D11ShaderResourceView* inputs[]{litSrv.Get(),depthSrv.Get()};context->PSSetShaderResources(0,2,inputs);
    auto ss=samplerState.Get();context->PSSetSamplers(0,1,&ss);
    context->OMSetBlendState(blendState.Get(),nullptr,~0u);context->OMSetDepthStencilState(ds.Get(),reference);context->RSSetState(rsState.Get());
    D3D11_VIEWPORT vp{0,0,float(width),float(height),0,1};context->RSSetViewports(1,&vp);auto targetRaw=target.Get();context->OMSetRenderTargets(1,&targetRaw,geometryDsv);
    context->Draw(3,0);context->OMSetRenderTargets(0,nullptr,nullptr);ID3D11ShaderResourceView* nullViews[2]{};context->PSSetShaderResources(0,2,nullViews);
    std::cout<<"node68 originalNoSubShader draw=3 mask=8 stencil="<<compare<<'/'<<reference<<'/'<<readMask<<" lit="<<litChoice<<" producerScope="<<(source/"scope.txt").string()<<'\n';return true;
}

struct NativeRs69Fallback {
    UINT state40{}; // Retained A024EF37 owner, shared by69/120/164.
    struct State {
        ComPtr<ID3D11BlendState> inheritedBlend,blend;
        ComPtr<ID3D11RasterizerState> inheritedRaster,raster;
        D3D11_DEPTH_STENCIL_DESC depthDescription;
        ComPtr<ID3D11DepthStencilState> depth;
    };
    bool enabled=false;
    UINT mask=0,width=0,height=0;
    float color[4]{};
    ComPtr<ID3D11VertexShader> vs;
    ComPtr<ID3D11PixelShader> ps;
    ComPtr<ID3D11Buffer> constants;
    ComPtr<ID3D11RenderTargetView> target;
    std::vector<State> states;
};

static NativeRs69Fallback prepareRs69Fallback(ID3D11Device* device,const fs::path& input,
    ID3D11Texture2D* rs,DXGI_FORMAT viewFormat,UINT width,UINT height) {
    NativeRs69Fallback result;result.width=width;result.height=height;
    NativePreparedInputFile recipe(input/"rs69"/"fallback.txt");if(!recipe)return result;
    UINT enabled;recipe>>enabled>>result.mask>>result.color[0]>>result.color[1]>>result.color[2]>>result.color[3];
    if(!recipe)throw std::runtime_error("Source69 fallback recipe is incomplete");
    if(!enabled)return result;
    result.enabled=true;
    auto vsBytes=load(input/"rs68"/"nosub-vs.dxbc");
    check(device->CreateVertexShader(vsBytes.data(),vsBytes.size(),nullptr,&result.vs),"69 fullscreen source VS");
    // Original D1B660 builtin fill literal, compiled once as backend glue.
    const char* fill="cbuffer FillColor:register(b0){float4 color;} float4 main():SV_Target{return color;}";
    ComPtr<ID3DBlob> code,error;
    check(D3DCompile(fill,std::strlen(fill),"original-D1B660-literal-fill",nullptr,nullptr,"main","ps_5_0",
        D3DCOMPILE_OPTIMIZATION_LEVEL3,0,&code,&error),"69 literal fill backend glue");
    check(device->CreatePixelShader(code->GetBufferPointer(),code->GetBufferSize(),nullptr,&result.ps),"69 literal fill PS");
    std::vector<char> cb(16);std::memcpy(cb.data(),result.color,16);
    result.constants=buffer(device,cb,D3D11_BIND_CONSTANT_BUFFER);
    D3D11_RENDER_TARGET_VIEW_DESC view{};view.Format=viewFormat;view.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2DARRAY;
    view.Texture2DArray.FirstArraySlice=3;view.Texture2DArray.ArraySize=1;
    check(device->CreateRenderTargetView(rs,&view,&result.target),"69 RS3 view");
    // The original VS uses SV_VertexID, Draw(3,0); no fullscreen VB/IB.
    return result;
}

static bool sameRs69DepthDescription(const D3D11_DEPTH_STENCIL_DESC& a,const D3D11_DEPTH_STENCIL_DESC& b) {
    auto face=[](const auto& x,const auto& y) {
        return x.StencilFailOp==y.StencilFailOp&&x.StencilDepthFailOp==y.StencilDepthFailOp&&
            x.StencilPassOp==y.StencilPassOp&&x.StencilFunc==y.StencilFunc;
    };
    return a.DepthEnable==b.DepthEnable&&a.DepthWriteMask==b.DepthWriteMask&&a.DepthFunc==b.DepthFunc&&
        a.StencilEnable==b.StencilEnable&&a.StencilReadMask==b.StencilReadMask&&a.StencilWriteMask==b.StencilWriteMask&&
        face(a.FrontFace,b.FrontFace)&&face(a.BackFace,b.BackFace);
}

static bool executeRs69Fallback(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical,
    NativeRs69Fallback& prepared,ID3D11DepthStencilView* geometryDsv) {
    if(!prepared.enabled)return false;
    // D61040 admits first fill only before both retained upper bits are set.
    // Same-size next Main does not reset them; the active SDF/reset writers
    // are outside this current empty-population producer.
    if((prepared.state40&0xC0000000u)==0xC0000000u) {
        std::cout<<"node69 sourceEmptyPopulation preserve retained state40=C0000000 no-draw\n";
        return false;
    }
    ComPtr<ID3D11BlendState> entryBlend;FLOAT entryFactor[4];UINT entryMask;
    context->OMGetBlendState(&entryBlend,entryFactor,&entryMask);
    const auto entryLogical=logical;
    ComPtr<ID3D11DepthStencilState> entryDepth;UINT entryReference;
    context->OMGetDepthStencilState(&entryDepth,&entryReference);
    ComPtr<ID3D11RasterizerState> entryRaster;context->RSGetState(&entryRaster);
    auto temporaryDepth=logical.description;temporaryDepth.DepthEnable=FALSE;
    // Retaining original COM state objects gives a stable cache identity for
    // the actual inherited descriptors; factors/reference are bound per draw.
    auto found=std::find_if(prepared.states.begin(),prepared.states.end(),[&](const auto& state) {
        return state.inheritedBlend.Get()==entryBlend.Get()&&state.inheritedRaster.Get()==entryRaster.Get()&&
            sameRs69DepthDescription(state.depthDescription,temporaryDepth);
    });
    if(found==prepared.states.end()) {
        NativeRs69Fallback::State state{};state.inheritedBlend=entryBlend;state.inheritedRaster=entryRaster;
        D3D11_BLEND_DESC blend{};blend.RenderTarget[0].RenderTargetWriteMask=15;
        if(entryBlend)entryBlend->GetDesc(&blend);
        blend.RenderTarget[0].RenderTargetWriteMask=BYTE(prepared.mask);
        check(device->CreateBlendState(&blend,&state.blend),"69 temporary RG mask; inherited blend");
        state.depthDescription=temporaryDepth;
        check(device->CreateDepthStencilState(&temporaryDepth,&state.depth),"69 temporary depth test off");
        D3D11_RASTERIZER_DESC raster{};raster.FillMode=D3D11_FILL_SOLID;raster.DepthClipEnable=TRUE;
        if(entryRaster)entryRaster->GetDesc(&raster);
        raster.CullMode=D3D11_CULL_NONE;raster.ScissorEnable=FALSE;
        check(device->CreateRasterizerState(&raster,&state.raster),"69 temporary no scissor/CullNONE");
        prepared.states.push_back(std::move(state));found=prepared.states.end()-1;
    }
    context->IASetInputLayout(nullptr);context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
    context->VSSetShader(prepared.vs.Get(),nullptr,0);context->PSSetShader(prepared.ps.Get(),nullptr,0);
    auto cb=prepared.constants.Get();context->PSSetConstantBuffers(0,1,&cb);
    context->OMSetBlendState(found->blend.Get(),entryFactor,entryMask);
    logical.bind(context,found->depth.Get(),temporaryDepth,entryLogical.reference);
    context->RSSetState(found->raster.Get());
    D3D11_VIEWPORT vp{0,0,float(prepared.width),float(prepared.height),0,1};context->RSSetViewports(1,&vp);
    auto target=prepared.target.Get();context->OMSetRenderTargets(1,&target,geometryDsv);
    context->Draw(3,0);context->OMSetRenderTargets(0,nullptr,nullptr);
    // These objects differ only in the source fields changed above. No
    // shader, CB, viewport or unrelated context state is restored.
    logical.bind(context,entryDepth.Get(),entryLogical.description,entryLogical.reference);
    context->RSSetState(entryRaster.Get());context->OMSetBlendState(entryBlend.Get(),entryFactor,entryMask);
    prepared.state40|=0xC0000000u;
    std::cout<<"node69 sourceEmptyPopulationFirstFallback mask="<<prepared.mask<<" literal="<<prepared.color[0]<<','
        <<prepared.color[1]<<','<<prepared.color[2]<<','<<prepared.color[3]
        <<" state40=0->C0000000 scope=bounded-no-SDF-volumes\n";
    return true;
}

// Compatibility for existing one-shot calls; normal reuse owns prepared once.
static bool drawRs69Fallback(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical,
    const fs::path& input,ID3D11Texture2D* rs,ID3D11DepthStencilView* geometryDsv,DXGI_FORMAT viewFormat,UINT width,UINT height) {
    auto prepared=prepareRs69Fallback(device,input,rs,viewFormat,width,height);
    return executeRs69Fallback(device,context,logical,prepared,geometryDsv);
}
