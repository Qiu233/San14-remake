// MainD1 owns primary DSV storage and separate persistent sampled storage.
// Original Pop opcode3 updates the latter before leaving that target scope.
struct NativeDepthMirror {
    ComPtr<ID3D11Texture2D> primary,sampled;
    ComPtr<ID3D11ShaderResourceView> depth,stencil;
};
static NativeDepthMirror createNativeDepthMirror(ID3D11Device* device,ID3D11Texture2D* primary,DXGI_FORMAT depthFormat) {
    NativeDepthMirror out;out.primary=primary;
    D3D11_TEXTURE2D_DESC td{};primary->GetDesc(&td);td.BindFlags=D3D11_BIND_SHADER_RESOURCE;
    check(device->CreateTexture2D(&td,nullptr,&out.sampled),"original MainD1 sampled storage");
    D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=depthFormat;sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sd.Texture2D.MipLevels=1;
    check(device->CreateShaderResourceView(out.sampled.Get(),&sd,&out.depth),"original MainD1 sampled depth view");
    sd.Format=DXGI_FORMAT_X32_TYPELESS_G8X24_UINT;
    check(device->CreateShaderResourceView(out.sampled.Get(),&sd,&out.stencil),"original MainD1 sampled stencil view");
    return out;
}
static void updateNativeDepthMirror(ID3D11DeviceContext* context,const NativeDepthMirror& depth,const char* sourcePoint) {
    context->CopySubresourceRegion(depth.sampled.Get(),0,0,0,0,depth.primary.Get(),0,nullptr);
    std::cout<<"source MainD1 mirror update "<<sourcePoint<<" primary19 -> sampled19 subresource0\n";
}
