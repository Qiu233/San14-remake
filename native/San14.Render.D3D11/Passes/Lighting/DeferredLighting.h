// One original GGX initializer and one selected F103 lighting pass.
// All sampled pixels are produced on this D3D11 device.
static ComPtr<ID3D11Texture2D> generateGgx(ID3D11Device* device,ID3D11DeviceContext* context,
    const fs::path& source) {
    auto vsCode=load(source/"vertex.dxbc"),psCode=load(source/"ggx-pixel.dxbc");
    ComPtr<ID3D11VertexShader> vs;ComPtr<ID3D11PixelShader> ps;
    check(device->CreateVertexShader(vsCode.data(),vsCode.size(),nullptr,&vs),"GGX source fullscreen VS");
    check(device->CreatePixelShader(psCode.data(),psCode.size(),nullptr,&ps),"original embedded GGX PS");
    // Actual BFB* initializer: 256x256, 256 integration samples, source float2.
    std::vector<char> cb(32);float size=256,scale=2;UINT samples=256;
    std::memcpy(cb.data(),&size,4);std::memcpy(cb.data()+4,&size,4);std::memcpy(cb.data()+8,&samples,4);std::memcpy(cb.data()+12,&scale,4);
    auto constants=buffer(device,cb,D3D11_BIND_CONSTANT_BUFFER);
    D3D11_TEXTURE2D_DESC td{};td.Width=td.Height=256;td.ArraySize=td.MipLevels=1;td.SampleDesc.Count=1;td.Format=DXGI_FORMAT_R16G16_FLOAT;td.Usage=D3D11_USAGE_DEFAULT;td.BindFlags=D3D11_BIND_RENDER_TARGET|D3D11_BIND_SHADER_RESOURCE;
    ComPtr<ID3D11Texture2D> texture;check(device->CreateTexture2D(&td,nullptr,&texture),"GGX 256x256 RG16F");
    ComPtr<ID3D11RenderTargetView> target;check(device->CreateRenderTargetView(texture.Get(),nullptr,&target),"GGX RTV");
    D3D11_RASTERIZER_DESC rs{};rs.FillMode=D3D11_FILL_SOLID;rs.CullMode=D3D11_CULL_NONE;rs.DepthClipEnable=TRUE;
    ComPtr<ID3D11RasterizerState> raster;check(device->CreateRasterizerState(&rs,&raster),"GGX raster");
    D3D11_DEPTH_STENCIL_DESC ds{};ds.DepthWriteMask=D3D11_DEPTH_WRITE_MASK_ZERO;ds.DepthFunc=D3D11_COMPARISON_ALWAYS;
    ComPtr<ID3D11DepthStencilState> depth;check(device->CreateDepthStencilState(&ds,&depth),"GGX no depth");
    D3D11_BLEND_DESC bd{};bd.RenderTarget[0].RenderTargetWriteMask=D3D11_COLOR_WRITE_ENABLE_RED|D3D11_COLOR_WRITE_ENABLE_GREEN;
    ComPtr<ID3D11BlendState> blend;check(device->CreateBlendState(&bd,&blend),"GGX RG writes");
    context->IASetInputLayout(nullptr);context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);context->VSSetShader(vs.Get(),nullptr,0);context->PSSetShader(ps.Get(),nullptr,0);
    auto rawCb=constants.Get();context->PSSetConstantBuffers(0,1,&rawCb);context->RSSetState(raster.Get());context->OMSetDepthStencilState(depth.Get(),0);context->OMSetBlendState(blend.Get(),nullptr,~0u);
    D3D11_VIEWPORT vp{0,0,256,256,0,1};context->RSSetViewports(1,&vp);auto rawTarget=target.Get();context->OMSetRenderTargets(1,&rawTarget,nullptr);context->Draw(3,0);context->OMSetRenderTargets(0,nullptr,nullptr);
    return texture;
}

static ComPtr<ID3D11Texture2D> drawLighting(ID3D11Device* device,ID3D11DeviceContext* context,
    const fs::path& source,const fs::path& input,const fs::path& postSource,
    const fs::path& ambientSource,const std::array<ComPtr<ID3D11Texture2D>,5>& gbuffer,
    const std::array<DXGI_FORMAT,6>& shaderFormats,ID3D11Texture2D* geometryDepth,
    ID3D11DepthStencilView* geometryDsv,ID3D11Texture2D* ggx,
    ID3D11ShaderResourceView* ambient,UINT width,UINT height,
    DXGI_FORMAT& outputShaderFormat,DXGI_FORMAT& outputTargetFormat,
    bool floatDiagnostic=false,bool disableAmbientDiagnostic=false) {
    context->ClearState();
    auto vsBytes=load(source/"vertex.dxbc"),psBytes=load(source/"pixel.dxbc");
    ComPtr<ID3D11VertexShader> vs;ComPtr<ID3D11PixelShader> ps;
    check(device->CreateVertexShader(vsBytes.data(),vsBytes.size(),nullptr,&vs),"F103 original VS");
    check(device->CreatePixelShader(psBytes.data(),psBytes.size(),nullptr,&ps),"F103 original PS383");
    context->VSSetShader(vs.Get(),nullptr,0);context->PSSetShader(ps.Get(),nullptr,0);
    context->IASetInputLayout(nullptr);context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
    std::vector<ComPtr<ID3D11Buffer>> constants;std::string stage,name;UINT slot;
    std::ifstream buffers(source/"cbuffers.txt");
    while(buffers>>stage>>slot>>name){auto cb=buffer(device,load(source/name),D3D11_BIND_CONSTANT_BUFFER);auto raw=cb.Get();if(stage=="PS")context->PSSetConstantBuffers(slot,1,&raw);else context->VSSetConstantBuffers(slot,1,&raw);constants.push_back(cb);}
    D3D11_TEXTURE2D_DESC depthDesc{};geometryDepth->GetDesc(&depthDesc);depthDesc.BindFlags=D3D11_BIND_SHADER_RESOURCE;
    ComPtr<ID3D11Texture2D> depthCopy;check(device->CreateTexture2D(&depthDesc,nullptr,&depthCopy),"F103 readable depth copy");context->CopyResource(depthCopy.Get(),geometryDepth);
    std::vector<ComPtr<ID3D11ShaderResourceView>> ownedViews;
    auto view2D=[&](ID3D11Texture2D* texture,DXGI_FORMAT format){D3D11_TEXTURE2D_DESC d{};texture->GetDesc(&d);D3D11_SHADER_RESOURCE_VIEW_DESC sv{};sv.Format=format;
        if(d.ArraySize>1){sv.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2DARRAY;sv.Texture2DArray.ArraySize=d.ArraySize;sv.Texture2DArray.MipLevels=d.MipLevels;}
        else{sv.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sv.Texture2D.MipLevels=d.MipLevels;}
        ComPtr<ID3D11ShaderResourceView> view;check(device->CreateShaderResourceView(texture,&sv,&view),"F103 exact source SRV");ownedViews.push_back(view);return view.Get();};
    auto table=buffer(device,load(source/"material-table.bin"),D3D11_BIND_SHADER_RESOURCE);
    D3D11_SHADER_RESOURCE_VIEW_DESC tableDesc{};tableDesc.Format=DXGI_FORMAT_R32_UINT;tableDesc.ViewDimension=D3D11_SRV_DIMENSION_BUFFER;tableDesc.Buffer.NumElements=2;
    ComPtr<ID3D11ShaderResourceView> tableView;check(device->CreateShaderResourceView(table.Get(),&tableDesc,&tableView),"original two-word material table");
    std::ifstream images(source/"textures.txt");std::string role;
    while(images>>slot>>role){ID3D11ShaderResourceView* selected=nullptr;
        if(role=="GBufferAB")selected=view2D(gbuffer[0].Get(),shaderFormats[0]);
        else if(role=="GBufferNS")selected=view2D(gbuffer[1].Get(),shaderFormats[1]);
        else if(role=="GBufferRS")selected=view2D(gbuffer[2].Get(),shaderFormats[2]);
        else if(role=="GBufferRR")selected=view2D(gbuffer[3].Get(),shaderFormats[3]);
        else if(role=="GBufferDepth")selected=view2D(depthCopy.Get(),shaderFormats[5]);
        else if(role=="GGX")selected=view2D(ggx,DXGI_FORMAT_R16G16_FLOAT);
        else if(role=="MaterialTable")selected=tableView.Get();
        else if(role=="AmbientSpecular"){if(!ambient)throw std::runtime_error("F103 requires the produced native source Sky cube");selected=disableAmbientDiagnostic?nullptr:ambient;}
        // Real NS Z/W=0 and source material0 word0 fix eta1/r7.w=false.
        // PS383's refraction-only t0/t4/t5 do not affect this opaque slice.
        else if(role=="RefractionDepth"||role=="TranslucentSpecular"||role=="BlurredDepth")selected=nullptr;
        else if(role=="Hatching")selected=nullptr; // Explicit source-null lab lookup.
        else throw std::runtime_error("Unimplemented F103 input: "+role);
        context->PSSetShaderResources(slot,1,&selected);std::cout<<"F103 input t"<<slot<<' '<<role<<" source="<<(selected?"native-produced":"bounded-null")<<'\n';
    }
    std::vector<ComPtr<ID3D11SamplerState>> samplers;
    UINT filter,u,v,w,aniso,comparison;float bias,minLod,maxLod;std::ifstream samplerRows(source/"samplers.txt");
    while(samplerRows>>slot>>filter>>u>>v>>w>>bias>>aniso>>comparison>>minLod>>maxLod){D3D11_SAMPLER_DESC d{};d.Filter=D3D11_FILTER(filter);d.AddressU=D3D11_TEXTURE_ADDRESS_MODE(u);d.AddressV=D3D11_TEXTURE_ADDRESS_MODE(v);d.AddressW=D3D11_TEXTURE_ADDRESS_MODE(w);d.MipLODBias=bias;d.MaxAnisotropy=aniso;d.ComparisonFunc=D3D11_COMPARISON_FUNC(comparison);d.MinLOD=minLod;d.MaxLOD=maxLod;ComPtr<ID3D11SamplerState> sampler;check(device->CreateSamplerState(&d,&sampler),"F103 original sampler");auto raw=sampler.Get();context->PSSetSamplers(slot,1,&raw);samplers.push_back(sampler);}
    auto installRawSampler=[&](UINT registerIndex,const fs::path& file){auto bytes=load(file);if(bytes.size()!=sizeof(D3D11_SAMPLER_DESC))throw std::runtime_error("Source sampler must be 52B");D3D11_SAMPLER_DESC d;std::memcpy(&d,bytes.data(),sizeof(d));ComPtr<ID3D11SamplerState> sampler;check(device->CreateSamplerState(&d,&sampler),"F103 source sampler descriptor");auto raw=sampler.Get();context->PSSetSamplers(registerIndex,1,&raw);samplers.push_back(sampler);};
    installRawSampler(3,ambientSource/"cube-sampler-desc.bin");installRawSampler(2,input/"rs68"/"null-backend-sampler.bin");
    UINT resourceFormat,shaderFormat,targetFormat,sourceWidth,sourceHeight;
    std::ifstream targetFile(postSource/"lighting-target.txt");targetFile>>resourceFormat>>shaderFormat>>targetFormat>>sourceWidth>>sourceHeight;
    if(!targetFile)throw std::runtime_error("Source F103 output declaration is missing");
    if(floatDiagnostic){resourceFormat=shaderFormat=targetFormat=DXGI_FORMAT_R32G32B32A32_FLOAT;std::cout<<"DIAGNOSTIC: original PS383 output stored in RGBA32F instead of source byte target\n";}
    outputShaderFormat=DXGI_FORMAT(shaderFormat);outputTargetFormat=DXGI_FORMAT(targetFormat);
    if(disableAmbientDiagnostic)std::cout<<"DIAGNOSTIC ABLATION: only source ambient cube t3 replaced by null\n";
    D3D11_TEXTURE2D_DESC td{};td.Width=width;td.Height=height;td.ArraySize=td.MipLevels=1;td.SampleDesc.Count=1;td.Format=DXGI_FORMAT(resourceFormat);td.Usage=D3D11_USAGE_DEFAULT;td.BindFlags=D3D11_BIND_RENDER_TARGET|D3D11_BIND_SHADER_RESOURCE;
    ComPtr<ID3D11Texture2D> color;check(device->CreateTexture2D(&td,nullptr,&color),"node73 source Scene target");
    D3D11_RENDER_TARGET_VIEW_DESC rtv{};rtv.Format=DXGI_FORMAT(targetFormat);rtv.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D;
    ComPtr<ID3D11RenderTargetView> target;check(device->CreateRenderTargetView(color.Get(),&rtv,&target),"source F103 RTV");float clear[4]{};context->ClearRenderTargetView(target.Get(),clear);
    D3D11_BLEND_DESC bd{};bd.RenderTarget[0].RenderTargetWriteMask=D3D11_COLOR_WRITE_ENABLE_ALL;ComPtr<ID3D11BlendState> blend;check(device->CreateBlendState(&bd,&blend),"F103 blend off");
    D3D11_RASTERIZER_DESC rs{};rs.FillMode=D3D11_FILL_SOLID;rs.CullMode=D3D11_CULL_NONE;rs.DepthClipEnable=TRUE;ComPtr<ID3D11RasterizerState> raster;check(device->CreateRasterizerState(&rs,&raster),"F103 source cullNone");
    D3D11_DEPTH_STENCIL_DESC ds{};ds.DepthWriteMask=D3D11_DEPTH_WRITE_MASK_ZERO;ds.DepthFunc=D3D11_COMPARISON_ALWAYS;ds.StencilEnable=TRUE;ds.StencilReadMask=7;ds.StencilWriteMask=0;ds.FrontFace.StencilFunc=D3D11_COMPARISON_EQUAL;ds.FrontFace.StencilFailOp=ds.FrontFace.StencilDepthFailOp=ds.FrontFace.StencilPassOp=D3D11_STENCIL_OP_KEEP;ds.BackFace=ds.FrontFace;
    ComPtr<ID3D11DepthStencilState> depth;check(device->CreateDepthStencilState(&ds,&depth),"F103 current controlled stencil0");
    context->RSSetState(raster.Get());context->OMSetBlendState(blend.Get(),nullptr,~0u);context->OMSetDepthStencilState(depth.Get(),0);D3D11_VIEWPORT vp{0,0,float(width),float(height),0,1};context->RSSetViewports(1,&vp);auto rt=target.Get();context->OMSetRenderTargets(1,&rt,geometryDsv);context->Draw(3,0);context->ClearState();
    std::cout<<"F103 originalPS383 draw=3 target="<<resourceFormat<<'/'<<shaderFormat<<'/'<<targetFormat<<" boundedOpaqueMaterial=0/eta1\n";return color;
}
