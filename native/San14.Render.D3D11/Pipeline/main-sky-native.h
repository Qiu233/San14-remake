// Source node124 Main Sky2. These device inputs belong to one fixed SourceMain
// generation. Both Star gates are closed; the selected Sky draw is Draw4 once.
#pragma once
struct NativeMainSky {
    UINT width=0,height=0,combination=0,vsIndex=0,psIndex=0;
    AmbientNativeProgram program;
    std::vector<AmbientNativeTextureBinding> textures;
    std::array<ID3D11ShaderResourceView*,6> views{};
    std::array<ID3D11SamplerState*,6> samplers{};
    ComPtr<ID3D11Buffer> vertices,vertexConstants,pixelConstants;
    ComPtr<ID3D11RenderTargetView> target;
    ComPtr<ID3D11DepthStencilView> dsv;
    ComPtr<ID3D11BlendState> blend;
    ComPtr<ID3D11DepthStencilState> depth;
    ComPtr<ID3D11RasterizerState> raster;
    D3D11_DEPTH_STENCIL_DESC depthDescription{};
    D3D11_RASTERIZER_DESC rasterDescription{};
};
static NativeMainSky prepareNativeMainSky(ID3D11Device* device,const fs::path& source,
    ID3D11Texture2D* hdr,ID3D11DepthStencilView* dsv,UINT width,UINT height) {
    NativeMainSky sky;UINT starsSkipped,blendEnabled;
    std::ifstream packet(source/"main-sky.txt");
    packet>>sky.width>>sky.height>>sky.combination>>sky.vsIndex>>sky.psIndex>>starsSkipped>>blendEnabled;
    if(!packet||sky.width!=width||sky.height!=height)throw std::runtime_error("Current Main Sky source extent absent or mismatched");
    if(!starsSkipped)throw std::runtime_error("This Main source needs its original Star packets before Sky2");
    sky.program=ambientProgram(device,source,"main-sky");
    sky.textures=ambientTextures(device,source,"main-sky-textures.tsv",4);
    sky.vertices=buffer(device,load(source/"quad-position-xy.bin"),D3D11_BIND_VERTEX_BUFFER);
    sky.vertexConstants=buffer(device,load(source/"main-sky-vs-b0.bin"),D3D11_BIND_CONSTANT_BUFFER);
    sky.pixelConstants=buffer(device,load(source/"main-sky-ps-b0.bin"),D3D11_BIND_CONSTANT_BUFFER);
    for(auto& texture:sky.textures){sky.views.at(texture.slot)=texture.view.Get();sky.samplers.at(texture.slot)=texture.sampler.Get();}
    D3D11_BLEND_DESC bd{};bd.RenderTarget[0].BlendEnable=blendEnabled;bd.RenderTarget[0].RenderTargetWriteMask=15;
    check(device->CreateBlendState(&bd,&sky.blend),"Main Sky source blend");
    D3D11_TEXTURE2D_DESC targetDesc{};hdr->GetDesc(&targetDesc);
    D3D11_RENDER_TARGET_VIEW_DESC rd{};rd.Format=targetDesc.Format;rd.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D;
    check(device->CreateRenderTargetView(hdr,&rd,&sky.target),"Main Sky retained HDR RTV");sky.dsv=dsv;
    return sky;
}
static void executeNativeMainSky(ID3D11Device* device,ID3D11DeviceContext* context,
    NativeSourceDepthState& logical,NativeMainSky& sky) {
    context->OMSetRenderTargets(0,nullptr,nullptr);context->PSSetShaderResources(0,UINT(sky.views.size()),sky.views.data());
    context->PSSetSamplers(0,UINT(sky.samplers.size()),sky.samplers.data());
    // Preserve ignored depth/stencil fields from source logical storage,
    // never reconstruct them with hardware GetDesc while they are disabled.
    D3D11_DEPTH_STENCIL_DESC ds=logical.description;
    ds.DepthEnable=FALSE;ds.DepthWriteMask=D3D11_DEPTH_WRITE_MASK_ZERO;ds.StencilEnable=TRUE;ds.StencilReadMask=255;
    ds.FrontFace.StencilFunc=D3D11_COMPARISON_EQUAL;
    ds.FrontFace.StencilFailOp=ds.FrontFace.StencilDepthFailOp=ds.FrontFace.StencilPassOp=D3D11_STENCIL_OP_KEEP;ds.BackFace=ds.FrontFace;
    if(!sky.depth||std::memcmp(&ds,&sky.depthDescription,sizeof(ds))!=0) {
        sky.depth.Reset();check(device->CreateDepthStencilState(&ds,&sky.depth),"Main Sky stencil0/FF");sky.depthDescription=ds;
    }
    ComPtr<ID3D11RasterizerState> entryRaster;context->RSGetState(&entryRaster);
    D3D11_RASTERIZER_DESC rs{};rs.FillMode=D3D11_FILL_SOLID;rs.DepthClipEnable=TRUE;if(entryRaster)entryRaster->GetDesc(&rs);rs.CullMode=D3D11_CULL_NONE;
    if(!sky.raster||std::memcmp(&rs,&sky.rasterDescription,sizeof(rs))!=0) {
        sky.raster.Reset();check(device->CreateRasterizerState(&rs,&sky.raster),"Main Sky CullNONE");sky.rasterDescription=rs;
    }
    context->IASetInputLayout(sky.program.layout.Get());context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP);
    auto vb=sky.vertices.Get();UINT stride=8,offset=0;context->IASetVertexBuffers(0,1,&vb,&stride,&offset);
    context->VSSetShader(sky.program.vs.Get(),nullptr,0);context->PSSetShader(sky.program.ps.Get(),nullptr,0);
    context->GSSetShader(nullptr,nullptr,0);context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);
    auto vcb=sky.vertexConstants.Get(),pcb=sky.pixelConstants.Get();context->VSSetConstantBuffers(0,1,&vcb);context->PSSetConstantBuffers(0,1,&pcb);
    D3D11_VIEWPORT viewport{0,0,float(sky.width),float(sky.height),0,1};context->RSSetViewports(1,&viewport);context->RSSetState(sky.raster.Get());
    logical.bind(context,sky.depth.Get(),ds,0);context->OMSetBlendState(sky.blend.Get(),nullptr,~0u);
    auto rt=sky.target.Get();context->OMSetRenderTargets(1,&rt,sky.dsv.Get());context->Draw(4,0);context->OMSetRenderTargets(0,nullptr,nullptr);
    std::array<ID3D11ShaderResourceView*,6> empty{};context->PSSetShaderResources(0,UINT(empty.size()),empty.data());
    // Source Star gates are closed. These overrides remain into Main Cloud.
    std::cout<<"source MainSky124 combo="<<sky.combination<<" VS="<<sky.vsIndex<<" PS="<<sky.psIndex<<" draw=4 stencil=0/FF no-clear stars=source-skip\n";
}
static void drawNativeMainSky(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical,
    const fs::path& source,ID3D11Texture2D* hdr,ID3D11DepthStencilView* dsv,UINT width,UINT height) {
    auto sky=prepareNativeMainSky(device,source,hdr,dsv,width,height);
    executeNativeMainSky(device,context,logical,sky);
}
