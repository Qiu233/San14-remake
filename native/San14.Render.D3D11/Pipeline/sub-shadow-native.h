// Original node63: four HBAO stages, then Common140 MIN-alpha into existing AB.
// Include after shared fs/load/buffer/check, source-depth, ambient and refraction-color.
#pragma once

struct NativeSubShadowPass {
    ComPtr<ID3D11VertexShader> vertex;
    ComPtr<ID3D11PixelShader> pixel;
    std::array<ComPtr<ID3D11Buffer>,D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT> vertexConstants,pixelConstants;
    std::array<ComPtr<ID3D11SamplerState>,D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT> samplers;
    std::array<ComPtr<ID3D11ShaderResourceView>,32> inputs;
    ComPtr<ID3D11RenderTargetView> output;
    ComPtr<ID3D11Buffer> vertices;
    ComPtr<ID3D11InputLayout> layout;
    ComPtr<ID3D11DepthStencilState> depth;
    ComPtr<ID3D11RasterizerState> raster;
    ComPtr<ID3D11BlendState> blend;
    D3D11_DEPTH_STENCIL_DESC sourceDepth{};
    D3D11_VIEWPORT viewport{};
    UINT reference=0,stride=0,count=0,first=0;
    D3D11_PRIMITIVE_TOPOLOGY topology{};
    bool withDepth=false;
};
struct NativeSubShadow {
    std::map<std::string,NativeRefractionColorTarget> surfaces;
    std::vector<NativeSubShadowPass> passes;
    ComPtr<ID3D11Texture2D> normals,ab,noise;
    NativeDepthMirror mainDepth;
    ComPtr<ID3D11DepthStencilView> mainDsv;
    std::array<ComPtr<ID3D11DepthStencilState>,2> exitDepth;
    std::array<D3D11_DEPTH_STENCIL_DESC,2> exitDepthDescription{};
    ComPtr<ID3D11BlendState> exitBlend;
    ComPtr<ID3D11PixelShader> stockPixel;
    ComPtr<ID3D11Buffer> stockPixelConstants;
};

// Prepare once for the supplied source camera/constants, state baseline and
// fixed MainD1/NS/AB owners. Reprepare when those source inputs are replaced.
// No context queries or source-state reconstruction from D3D GetDesc.
static NativeSubShadow prepareNativeSubShadow63(ID3D11Device* device,const fs::path& source,
    const NativeSourceDepthState& sourceEntry,const D3D11_RASTERIZER_DESC& sourceRaster,
    const D3D11_BLEND_DESC& sourceBlend,const NativeDepthMirror& mainDepth,
    ID3D11Texture2D* normals,DXGI_FORMAT normalFormat,ID3D11Texture2D* ab,ID3D11DepthStencilView* mainDsv) {
    NativeSubShadow result;result.mainDepth=mainDepth;result.normals=normals;result.ab=ab;result.mainDsv=mainDsv;
    auto view=[&](ID3D11Texture2D* texture,DXGI_FORMAT format) {
        D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=format;sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sd.Texture2D.MipLevels=1;
        ComPtr<ID3D11ShaderResourceView> v;check(device->CreateShaderResourceView(texture,&sd,&v),"63 original input view");return v;
    };
    std::map<std::string,ComPtr<ID3D11ShaderResourceView>> inputs;
    inputs["MainD1"]=mainDepth.depth;inputs["MainD1Stencil"]=mainDepth.stencil;inputs["NS"]=view(normals,normalFormat);
    UINT nr,ns,nt,nw,nh;std::ifstream noiseRow(source/"noise4x4-target.txt");noiseRow>>nr>>ns>>nt>>nw>>nh;
    if(!noiseRow)throw std::runtime_error("63 source noise descriptor absent");
    auto noiseBytes=load(source/"noise4x4-bgra8.bin");
    if(noiseBytes.size()!=size_t(nw)*nh*4)throw std::runtime_error("63 original noise byte count differs");
    D3D11_SUBRESOURCE_DATA noiseData{};noiseData.pSysMem=noiseBytes.data();noiseData.SysMemPitch=nw*4;
    D3D11_TEXTURE2D_DESC nd{};nd.Width=nw;nd.Height=nh;nd.ArraySize=nd.MipLevels=nd.SampleDesc.Count=1;
    nd.Format=DXGI_FORMAT(nr);nd.BindFlags=D3D11_BIND_SHADER_RESOURCE;nd.Usage=D3D11_USAGE_IMMUTABLE;
    check(device->CreateTexture2D(&nd,&noiseData,&result.noise),"63 original4x4 noise");inputs["Noise4x4"]=view(result.noise.Get(),DXGI_FORMAT(ns));
    std::ifstream manifest(source/"normal-manifest.txt");std::string name;
    if(!manifest)throw std::runtime_error("63 normal source pass manifest absent");
    D3D11_BLEND_DESC lastBlend=sourceBlend;
    while(manifest>>name) {
        const auto directory=source/name;NativeSubShadowPass pass;
        std::string targetRole;std::ifstream roleRow(directory/"target-role.txt");roleRow>>targetRole;
        if(!roleRow)throw std::runtime_error("63 source target role absent");
        const bool compose=targetRole=="ExistingAB0";
        UINT resource,sampled,rendered,width,height;std::ifstream target(directory/"target.txt");target>>resource>>sampled>>rendered>>width>>height;
        if(!target)throw std::runtime_error("63 source target absent");
        ID3D11Texture2D* outputTexture=ab;
        if(!compose) {
            UINT support,quality;check(device->CheckFormatSupport(DXGI_FORMAT(rendered),&support),"63 selected target capability");
            check(device->CheckMultisampleQualityLevels(DXGI_FORMAT(rendered),1,&quality),"63 source one-sample capability");
            if(!(support&D3D11_FORMAT_SUPPORT_RENDER_TARGET)||!quality)throw std::runtime_error("63 reselect original allocation candidate for this device");
            auto surface=createRefractionColorTarget(device,width,height,1,DXGI_FORMAT(resource),DXGI_FORMAT(sampled));
            outputTexture=surface.texture.Get();inputs[targetRole]=surface.view;result.surfaces.emplace(targetRole,std::move(surface));
        }
        D3D11_RENDER_TARGET_VIEW_DESC rd{};rd.Format=DXGI_FORMAT(rendered);rd.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D;
        check(device->CreateRenderTargetView(outputTexture,&rd,&pass.output),"63 source MRT0");
        auto vc=load(directory/"vs.dxbc"),pc=load(directory/"ps.dxbc");
        check(device->CreateVertexShader(vc.data(),vc.size(),nullptr,&pass.vertex),"63 original VS");
        check(device->CreatePixelShader(pc.data(),pc.size(),nullptr,&pass.pixel),"63 original PS");
        std::string stage,file;UINT slot;std::ifstream cbs(directory/"cbuffers.txt");
        if(!cbs)throw std::runtime_error("63 original CB list absent");
        while(cbs>>stage>>slot>>file){auto cb=buffer(device,load(directory/file),D3D11_BIND_CONSTANT_BUFFER);
            if(stage=="VS")pass.vertexConstants.at(slot)=cb;else if(stage=="PS")pass.pixelConstants.at(slot)=cb;
            else throw std::runtime_error("63 unsupported source CB stage");}
        std::string role,key;std::ifstream textures(directory/"textures.txt"),samplers(directory/"samplers.txt");
        if(!textures||!samplers)throw std::runtime_error("63 original resource list absent");
        while(textures>>slot>>role>>key)pass.inputs.at(slot)=inputs.at(role);
        while(samplers>>slot>>file)pass.samplers.at(slot)=ambientSampler(device,directory/file);
        UINT test,write,depthCompare,enabled,compare,reference,readMask,writeMask,cull;
        std::ifstream depth(directory/"depth.txt"),stencil(directory/"stencil.txt"),raster(directory/"raster-cull.txt");
        depth>>test>>write>>depthCompare;stencil>>enabled>>compare>>reference>>readMask>>writeMask;raster>>cull;
        if(!depth||!stencil||!raster)throw std::runtime_error("63 original depth/stencil/raster fields absent");
        auto ds=sourceEntry.description;ds.DepthEnable=test;ds.DepthWriteMask=write?D3D11_DEPTH_WRITE_MASK_ALL:D3D11_DEPTH_WRITE_MASK_ZERO;
        // compare0 and emitted writeMask255 are inherited fields, not setters.
        // Keep the actual sourceEntry DepthFunc and StencilWriteMask.
        if(depthCompare)ds.DepthFunc=D3D11_COMPARISON_FUNC(depthCompare);
        ds.StencilEnable=enabled;ds.StencilReadMask=BYTE(readMask);ds.FrontFace.StencilFunc=D3D11_COMPARISON_FUNC(compare);
        ds.FrontFace.StencilFailOp=ds.FrontFace.StencilDepthFailOp=ds.FrontFace.StencilPassOp=D3D11_STENCIL_OP_KEEP;ds.BackFace=ds.FrontFace;
        pass.sourceDepth=ds;pass.reference=reference;
        check(device->CreateDepthStencilState(&ds,&pass.depth),"63 original depth/stencil");
        auto rs=sourceRaster;rs.CullMode=D3D11_CULL_MODE(cull);
        check(device->CreateRasterizerState(&rs,&pass.raster),"63 original Cull writer");
        auto bd=sourceBlend;auto& b=bd.RenderTarget[0];b.RenderTargetWriteMask=15;
        UINT blendEnabled;std::ifstream blendEnabledRow(directory/"blend-enabled.txt");blendEnabledRow>>blendEnabled;
        if(!blendEnabledRow)throw std::runtime_error("63 original blend enable absent");b.BlendEnable=blendEnabled;
        if(compose){UINT src,dst,op,srcA,dstA,opA,mask;std::ifstream blend(directory/"blend.txt");
            blend>>enabled>>src>>dst>>op>>srcA>>dstA>>opA>>mask;
            if(!blend)throw std::runtime_error("63 original composition blend absent");
            b.BlendEnable=enabled;b.SrcBlend=D3D11_BLEND(src);b.DestBlend=D3D11_BLEND(dst);b.BlendOp=D3D11_BLEND_OP(op);
            b.SrcBlendAlpha=D3D11_BLEND(srcA);b.DestBlendAlpha=D3D11_BLEND(dstA);b.BlendOpAlpha=D3D11_BLEND_OP(opA);b.RenderTargetWriteMask=BYTE(mask);}
        check(device->CreateBlendState(&bd,&pass.blend),"63 original blend/mask");lastBlend=bd;
        pass.viewport=readRefractionViewport(directory);UINT topology;std::ifstream geometry(directory/"geometry.txt");
        if(compose){geometry>>pass.stride>>pass.count>>pass.first>>topology;
            pass.vertices=buffer(device,load(directory/"vertices.bin"),D3D11_BIND_VERTEX_BUFFER);
            struct Element{std::string name;UINT index,format,slot,offset;};std::vector<Element> elements;Element e;
            std::ifstream layoutRows(directory/"layout.txt");while(layoutRows>>e.name>>e.index>>e.format>>e.slot>>e.offset)elements.push_back(e);
            std::vector<D3D11_INPUT_ELEMENT_DESC> declarations;
            for(auto& x:elements)declarations.push_back({x.name.c_str(),x.index,DXGI_FORMAT(x.format),x.slot,x.offset,D3D11_INPUT_PER_VERTEX_DATA,0});
            check(device->CreateInputLayout(declarations.data(),UINT(declarations.size()),vc.data(),vc.size(),&pass.layout),"63 original stock layout");
        }else geometry>>pass.count>>topology;
        if(!geometry)throw std::runtime_error("63 original geometry recipe absent");pass.topology=D3D11_PRIMITIVE_TOPOLOGY(topology);
        UINT withDsv;std::ifstream targetDepth(directory/"target-depth.txt");targetDepth>>withDsv;
        if(!targetDepth)throw std::runtime_error("63 source depth attachment absent");pass.withDepth=withDsv!=0;
        result.passes.push_back(std::move(pass));
    }
    if(result.passes.size()!=6)throw std::runtime_error("63 normal source requires four HBAO and two composition draws");
    for(UINT test=0;test!=2;++test){auto ds=result.passes.back().sourceDepth;ds.DepthEnable=test;
        result.exitDepthDescription[test]=ds;check(device->CreateDepthStencilState(&ds,&result.exitDepth[test]),"63 original exit DepthTest");}
    lastBlend.RenderTarget[0].RenderTargetWriteMask=15;
    check(device->CreateBlendState(&lastBlend,&result.exitBlend),"63 original exit RGBA mask");
    // EA3305 returns StockScreen to mode3 after the Common140 scope.
    auto stock=load(source/"compose-08"/"ps.dxbc");
    check(device->CreatePixelShader(stock.data(),stock.size(),nullptr,&result.stockPixel),"63 original exit stockmode3 PS");
    result.stockPixelConstants=buffer(device,load(source/"compose-08"/"ps-b0.bin"),D3D11_BIND_CONSTANT_BUFFER);
    return result;
}

// Per-frame GPU work only. Caller updates this persistent MainD1 sampled mirror
// at source59. Four actual draws overwrite HBAO outputs; composition keeps AB RGB.
static void executeNativeSubShadow63(ID3D11DeviceContext* context,NativeSourceDepthState& logical,
    const NativeSubShadow& prepared,const D3D11_VIEWPORT& returnViewport) {
    const UINT entryTest=logical.description.DepthEnable?1:0;
    context->OMSetRenderTargets(0,nullptr,nullptr);
    context->GSSetShader(nullptr,nullptr,0);context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);
    for(const auto& pass:prepared.passes) {
        context->VSSetShader(pass.vertex.Get(),nullptr,0);context->PSSetShader(pass.pixel.Get(),nullptr,0);
        std::array<ID3D11Buffer*,D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT> vsCb{},psCb{};
        for(size_t i=0;i<vsCb.size();++i){vsCb[i]=pass.vertexConstants[i].Get();psCb[i]=pass.pixelConstants[i].Get();}
        context->VSSetConstantBuffers(0,UINT(vsCb.size()),vsCb.data());context->PSSetConstantBuffers(0,UINT(psCb.size()),psCb.data());
        std::array<ID3D11ShaderResourceView*,32> pixel{};std::array<ID3D11SamplerState*,D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT> samplers{};
        for(size_t i=0;i<pixel.size();++i)pixel[i]=pass.inputs[i].Get();
        for(size_t i=0;i<samplers.size();++i)samplers[i]=pass.samplers[i].Get();
        context->PSSetShaderResources(0,UINT(pixel.size()),pixel.data());context->PSSetSamplers(0,UINT(samplers.size()),samplers.data());
        logical.bind(context,pass.depth.Get(),pass.sourceDepth,pass.reference);
        context->RSSetState(pass.raster.Get());context->OMSetBlendState(pass.blend.Get(),nullptr,~0u);
        context->RSSetViewports(1,&pass.viewport);context->IASetInputLayout(pass.layout.Get());
        context->IASetPrimitiveTopology(pass.topology);auto vb=pass.vertices.Get();UINT offset=0;
        context->IASetVertexBuffers(0,1,&vb,&pass.stride,&offset);context->IASetIndexBuffer(nullptr,DXGI_FORMAT_UNKNOWN,0);
        auto output=pass.output.Get();context->OMSetRenderTargets(1,&output,pass.withDepth?prepared.mainDsv.Get():nullptr);
        context->Draw(pass.count,pass.first);
        // Release sampled inputs before a later pass writes those resources.
        pixel.fill(nullptr);context->PSSetShaderResources(0,UINT(pixel.size()),pixel.data());
        if(&pass!=&prepared.passes.back())context->OMSetRenderTargets(0,nullptr,nullptr);
    }
    // Source63 restores testing, RGBA mask and StockScreen mode3 only. Write0,
    // CullNONE, Equal08/read0F/allKeep and slot0 MIN remain. Source64 restores
    // the outer target scope; there is no whole-context restore here.
    logical.bind(context,prepared.exitDepth[entryTest].Get(),prepared.exitDepthDescription[entryTest],prepared.passes.back().reference);
    context->OMSetBlendState(prepared.exitBlend.Get(),nullptr,~0u);context->PSSetShader(prepared.stockPixel.Get(),nullptr,0);
    auto stockCb=prepared.stockPixelConstants.Get();context->PSSetConstantBuffers(0,1,&stockCb);
    context->RSSetViewports(1,&returnViewport);
}

// Compatibility for the existing one-shot caller. Continuous Main retains one
// prepare result and executes each frame with explicit source descriptors.
static NativeSubShadow produceNativeSubShadow63(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical,
    const fs::path& source,const NativeDepthMirror& mainDepth,
    ID3D11Texture2D* normals,DXGI_FORMAT normalFormat,ID3D11Texture2D* ab,ID3D11DepthStencilView* mainDsv) {
    ComPtr<ID3D11RasterizerState> raster;context->RSGetState(&raster);
    D3D11_RASTERIZER_DESC rs{};rs.FillMode=D3D11_FILL_SOLID;rs.CullMode=D3D11_CULL_BACK;rs.DepthClipEnable=TRUE;
    if(raster)raster->GetDesc(&rs); // Active raster only; no depth/blend GetDesc inference.
    D3D11_BLEND_DESC bd{};for(auto& b:bd.RenderTarget){b.SrcBlend=b.SrcBlendAlpha=D3D11_BLEND_ONE;
        b.DestBlend=b.DestBlendAlpha=D3D11_BLEND_ZERO;b.BlendOp=b.BlendOpAlpha=D3D11_BLEND_OP_ADD;b.RenderTargetWriteMask=15;}
    D3D11_VIEWPORT viewport{};UINT count=1;context->RSGetViewports(&count,&viewport);
    auto prepared=prepareNativeSubShadow63(device,source,logical,rs,bd,mainDepth,normals,normalFormat,ab,mainDsv);
    executeNativeSubShadow63(context,logical,prepared,viewport);return prepared;
}
