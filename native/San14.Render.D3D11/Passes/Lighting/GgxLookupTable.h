#pragma once
// Integrate the specular lookup table once for this render session.
static ComPtr<ID3D11Texture2D> generateGgx(ID3D11Device* device,ID3D11DeviceContext* context,
    const fs::path& source) {
    auto vsCode=load(source/"vertex.dxbc"),psCode=load(source/"ggx-pixel.dxbc");
    ComPtr<ID3D11VertexShader> vs;ComPtr<ID3D11PixelShader> ps;
    check(device->CreateVertexShader(vsCode.data(),vsCode.size(),nullptr,&vs),"GGX source fullscreen VS");
    check(device->CreatePixelShader(psCode.data(),psCode.size(),nullptr,&ps),"original embedded GGX PS");
    // The lookup uses 256 integration samples per texel, stored as float2.
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
