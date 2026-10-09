// Original ordinary/post-Ocean RS3 shadow receiver. Prepare source/device data
// once; execute reads the current D1 mirror and logical source inheritance.
struct NativeSourceShadowReceiver {
    struct Sampler { UINT slot;ComPtr<ID3D11SamplerState> state; };
    struct DepthPair {
        D3D11_COMPARISON_FUNC comparison;BYTE stencilWriteMask;
        D3D11_DEPTH_STENCIL_DESC drawDescription,exitDescription;
        ComPtr<ID3D11DepthStencilState> draw,exit;
    };
    ComPtr<ID3D11VertexShader> vs;
    ComPtr<ID3D11PixelShader> ps;
    ComPtr<ID3D11Buffer> constants;
    ComPtr<ID3D11ShaderResourceView> atlas,directionalDiffuse;
    ComPtr<ID3D11RenderTargetView> target;
    ComPtr<ID3D11RasterizerState> raster;
    ComPtr<ID3D11BlendState> alphaBlend,fullBlend;
    std::vector<Sampler> samplers;
    std::vector<DepthPair> depths;
    UINT compare=0,reference=0,readMask=0,width=0,height=0;
    std::string sourceName;
};

static NativeSourceShadowReceiver prepareSourceShadowReceiver(ID3D11Device* device,const fs::path& source,
    ID3D11ShaderResourceView* shadowAtlas,ID3D11ShaderResourceView* directionalDiffuse,
    ID3D11Texture2D* rs,DXGI_FORMAT rsFormat,UINT width,UINT height) {
    NativeSourceShadowReceiver result;
    result.width=width;result.height=height;result.sourceName=source.filename().string();
    result.atlas=shadowAtlas;result.directionalDiffuse=directionalDiffuse;
    auto v=load(source/"vs.dxbc"),p=load(source/"ps.dxbc");
    check(device->CreateVertexShader(v.data(),v.size(),nullptr,&result.vs),"source shadow receiver VS");
    check(device->CreatePixelShader(p.data(),p.size(),nullptr,&result.ps),"source shadow receiver PS");
    result.constants=buffer(device,load(source/"ps-b0.bin"),D3D11_BIND_CONSTANT_BUFFER);
    UINT slot;std::string filename;std::ifstream rows(source/"samplers.txt");
    if(!rows)throw std::runtime_error("Source shadow receiver samplers.txt absent");
    while(rows>>slot>>filename) {
        auto raw=load(source/filename);
        if(raw.size()!=sizeof(D3D11_SAMPLER_DESC))throw std::runtime_error("Source shadow sampler requires52 bytes");
        D3D11_SAMPLER_DESC d;std::memcpy(&d,raw.data(),sizeof(d));
        NativeSourceShadowReceiver::Sampler sampler{slot,{}};
        check(device->CreateSamplerState(&d,&sampler.state),"source shadow receiver sampler");
        result.samplers.push_back(std::move(sampler));
    }
    D3D11_RENDER_TARGET_VIEW_DESC rd{};rd.Format=rsFormat;rd.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2DARRAY;
    rd.Texture2DArray.FirstArraySlice=3;rd.Texture2DArray.ArraySize=1;
    check(device->CreateRenderTargetView(rs,&rd,&result.target),"shadow receiver RS3");
    std::ifstream rasterSource(source/"raster.txt");rasterSource>>result.compare>>result.reference>>result.readMask;
    if(!rasterSource)throw std::runtime_error("Source receiver stencil state absent");
    D3D11_RASTERIZER_DESC raster{};raster.FillMode=D3D11_FILL_SOLID;raster.CullMode=D3D11_CULL_NONE;raster.DepthClipEnable=TRUE;
    check(device->CreateRasterizerState(&raster,&result.raster),"source receiver raster");
    D3D11_BLEND_DESC blend{};blend.RenderTarget[0].RenderTargetWriteMask=D3D11_COLOR_WRITE_ENABLE_ALPHA;
    check(device->CreateBlendState(&blend,&result.alphaBlend),"source receiver alpha writes");
    blend.RenderTarget[0].RenderTargetWriteMask=15;
    check(device->CreateBlendState(&blend,&result.fullBlend),"source receiver exit full mask");
    // The original VS creates fullscreen geometry from SV_VertexID:
    // Draw(3,0), null input layout, TRIANGLELIST; no VB/IB to allocate.
    return result;
}

static void executeSourceShadowReceiver(ID3D11Device* device,ID3D11DeviceContext* context,
    NativeSourceDepthState& logical,NativeSourceShadowReceiver& prepared,
    ID3D11ShaderResourceView* currentDepthMirror,ID3D11DepthStencilView* geometryDsv) {
    // EBCD70 sets every other DS field. Comparison and the unused stencil
    // write mask alone survive from the caller's logical source description.
    auto found=std::find_if(prepared.depths.begin(),prepared.depths.end(),[&](const auto& d) {
        return d.comparison==logical.description.DepthFunc&&d.stencilWriteMask==logical.description.StencilWriteMask;
    });
    if(found==prepared.depths.end()) {
        NativeSourceShadowReceiver::DepthPair pair{};
        pair.comparison=logical.description.DepthFunc;pair.stencilWriteMask=logical.description.StencilWriteMask;
        auto ds=logical.description;
        ds.DepthEnable=FALSE;ds.DepthWriteMask=D3D11_DEPTH_WRITE_MASK_ZERO;ds.StencilEnable=TRUE;
        ds.StencilReadMask=BYTE(prepared.readMask);
        ds.FrontFace.StencilFunc=D3D11_COMPARISON_FUNC(prepared.compare);
        ds.FrontFace.StencilPassOp=ds.FrontFace.StencilFailOp=ds.FrontFace.StencilDepthFailOp=D3D11_STENCIL_OP_KEEP;
        ds.BackFace=ds.FrontFace;pair.drawDescription=ds;
        check(device->CreateDepthStencilState(&ds,&pair.draw),"source receiver stencil");
        ds.StencilEnable=FALSE;pair.exitDescription=ds;
        check(device->CreateDepthStencilState(&ds,&pair.exit),"source receiver exit stencil off");
        prepared.depths.push_back(std::move(pair));found=prepared.depths.end()-1;
    }
    ID3D11ShaderResourceView* views[]{prepared.atlas.Get(),prepared.directionalDiffuse.Get(),currentDepthMirror};
    context->PSSetShaderResources(0,3,views);
    for(auto& sampler:prepared.samplers){auto s=sampler.state.Get();context->PSSetSamplers(sampler.slot,1,&s);}
    context->IASetInputLayout(nullptr);context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
    context->VSSetShader(prepared.vs.Get(),nullptr,0);context->PSSetShader(prepared.ps.Get(),nullptr,0);context->GSSetShader(nullptr,nullptr,0);
    auto cb=prepared.constants.Get();context->PSSetConstantBuffers(0,1,&cb);
    context->RSSetState(prepared.raster.Get());
    logical.bind(context,found->draw.Get(),found->drawDescription,prepared.reference);
    context->OMSetBlendState(prepared.alphaBlend.Get(),nullptr,~0u);
    D3D11_VIEWPORT viewport{0,0,float(prepared.width),float(prepared.height),0,1};context->RSSetViewports(1,&viewport);
    auto target=prepared.target.Get();context->OMSetRenderTargets(1,&target,geometryDsv);
    context->Draw(3,0);context->OMSetRenderTargets(0,nullptr,nullptr);
    ID3D11ShaderResourceView* nulls[3]{};context->PSSetShaderResources(0,3,nulls);
    // Source exit changes stencil-enable and color mask only. Test/write stay
    // disabled, blend stays off and CullNONE stays in the caller.
    logical.bind(context,found->exit.Get(),found->exitDescription,prepared.reference);
    context->OMSetBlendState(prepared.fullBlend.Get(),nullptr,~0u);
    std::cout<<prepared.sourceName<<" original shadow receiver draw=3 stencil="<<prepared.reference<<'/'<<prepared.readMask
        <<" native-produced EA, selected original sLit, current native D1; alpha write only\n";
}

// Compatibility for existing one-shot experiments. The reusable owner prepares
// 68 and119 separately and passes its retained original FE8 view above.
static void drawSourceShadowReceiver(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical,
    const fs::path& source,const fs::path& diffuseSource,const NativeShadowMap& shadow,
    ID3D11Texture2D* rs,ID3D11ShaderResourceView* depthView,ID3D11DepthStencilView* geometryDsv,
    DXGI_FORMAT rsFormat,UINT width,UINT height) {
    auto diffuse=loadTexture(device,diffuseSource/"directional-diffuse.tex");
    ComPtr<ID3D11ShaderResourceView> lit;
    check(device->CreateShaderResourceView(diffuse.Get(),nullptr,&lit),"shadow receiver original FE8 diffuse");
    auto prepared=prepareSourceShadowReceiver(device,source,shadow.view.Get(),lit.Get(),rs,rsFormat,width,height);
    executeSourceShadowReceiver(device,context,logical,prepared,depthView,geometryDsv);
}
