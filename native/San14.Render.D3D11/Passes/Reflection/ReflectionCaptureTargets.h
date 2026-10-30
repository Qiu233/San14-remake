#pragma once
#include "ReflectionGpuProgram.h"
#include <limits>
#include <cstdint>
#include <cmath>

namespace San14::Render::Reflection {
struct ArrayTexture {
    ComPtr<ID3D11Texture2D> texture;
    ComPtr<ID3D11ShaderResourceView> srv;
    std::vector<ComPtr<ID3D11RenderTargetView>> targets;
    std::vector<ComPtr<ID3D11ShaderResourceView>> previousMips;
    ComPtr<ID3D11UnorderedAccessView> uav;
    UINT width{},height{},layers{},mips{};
};
inline ArrayTexture makeArray(ID3D11Device* device,UINT width,UINT height,UINT layers,UINT mips,DXGI_FORMAT format,bool writable) {
    if(!width||!height||!layers||!mips)throw std::runtime_error("Reflection actual array extent");
    ArrayTexture out;out.width=width;out.height=height;out.layers=layers;out.mips=mips;
    D3D11_TEXTURE2D_DESC td{};td.Width=width;td.Height=height;td.ArraySize=layers;td.MipLevels=mips;td.Format=format;
    td.SampleDesc.Count=1;td.Usage=D3D11_USAGE_DEFAULT;td.BindFlags=D3D11_BIND_SHADER_RESOURCE|D3D11_BIND_RENDER_TARGET|(writable?D3D11_BIND_UNORDERED_ACCESS:0);
    check(device->CreateTexture2D(&td,nullptr,&out.texture),"Reflection actual array storage");
    D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=format;sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2DARRAY;
    sd.Texture2DArray.ArraySize=layers;sd.Texture2DArray.MipLevels=mips;
    check(device->CreateShaderResourceView(out.texture.Get(),&sd,&out.srv),"Reflection actual array SRV");
    for(UINT mip=0;mip<mips;++mip) {
        // Native D3D11 can borrow precisely one array-wide previous mip. No
        // copies, readback or automatic mip generation are required.
        sd.Texture2DArray.MostDetailedMip=mip;sd.Texture2DArray.MipLevels=1;
        ComPtr<ID3D11ShaderResourceView> view;check(device->CreateShaderResourceView(out.texture.Get(),&sd,&view),"Reflection array mip SRV");out.previousMips.push_back(std::move(view));
        for(UINT layer=0;layer<layers;++layer) {
            D3D11_RENDER_TARGET_VIEW_DESC rd{};rd.Format=format;rd.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2DARRAY;
            rd.Texture2DArray.MipSlice=mip;rd.Texture2DArray.FirstArraySlice=layer;rd.Texture2DArray.ArraySize=1;
            ComPtr<ID3D11RenderTargetView> target;check(device->CreateRenderTargetView(out.texture.Get(),&rd,&target),"Reflection actual array target");out.targets.push_back(std::move(target));
        }
    }
    if(writable) {
        D3D11_UNORDERED_ACCESS_VIEW_DESC ud{};ud.Format=format;ud.ViewDimension=D3D11_UAV_DIMENSION_TEXTURE2DARRAY;ud.Texture2DArray.ArraySize=layers;
        check(device->CreateUnorderedAccessView(out.texture.Get(),&ud,&out.uav),"Reflection actual array UAV");
    }
    return out;
}
struct CoefficientBuffer {
    ComPtr<ID3D11Buffer> buffer;
    ComPtr<ID3D11UnorderedAccessView> uav;
};
inline CoefficientBuffer makeCoefficients(ID3D11Device* device,UINT probes,bool sky) {
    if(!probes||probes>std::numeric_limits<UINT>::max()/(sky?4u:108u))throw std::runtime_error("Reflection coefficient extent");
    CoefficientBuffer out;D3D11_BUFFER_DESC bd{};bd.ByteWidth=probes*(sky?4:108);bd.Usage=D3D11_USAGE_DEFAULT;bd.BindFlags=D3D11_BIND_UNORDERED_ACCESS;
    if(!sky){bd.MiscFlags=D3D11_RESOURCE_MISC_BUFFER_STRUCTURED;bd.StructureByteStride=108;}
    check(device->CreateBuffer(&bd,nullptr,&out.buffer),"Reflection actual coefficient storage");
    D3D11_UNORDERED_ACCESS_VIEW_DESC ud{};ud.ViewDimension=D3D11_UAV_DIMENSION_BUFFER;ud.Format=sky?DXGI_FORMAT_R32_FLOAT:DXGI_FORMAT_UNKNOWN;ud.Buffer.NumElements=probes;
    check(device->CreateUnorderedAccessView(out.buffer.Get(),&ud,&out.uav),"Reflection actual coefficient UAV");return out;
}
// Owns the source's four256x256 RGBA16F Cubes and D32 depth Cube.
class CaptureTargets {
public:
    explicit CaptureTargets(ID3D11Device* device) {
        for(UINT color=0;color<4;++color) {
            D3D11_TEXTURE2D_DESC td{};td.Width=td.Height=256;td.ArraySize=6;td.MipLevels=1;
            td.Format=DXGI_FORMAT_R16G16B16A16_FLOAT;td.SampleDesc.Count=1;td.Usage=D3D11_USAGE_DEFAULT;
            td.BindFlags=D3D11_BIND_RENDER_TARGET|D3D11_BIND_SHADER_RESOURCE;td.MiscFlags=D3D11_RESOURCE_MISC_TEXTURECUBE;
            check(device->CreateTexture2D(&td,nullptr,&colors_[color]),"Reflection capture original Cube");
            D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=td.Format;sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURECUBE;sd.TextureCube.MipLevels=1;
            check(device->CreateShaderResourceView(colors_[color].Get(),&sd,&cubes_[color]),"Reflection capture Cube SRV");
            for(UINT face=0;face<6;++face) {
                D3D11_RENDER_TARGET_VIEW_DESC rd{};rd.Format=td.Format;rd.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2DARRAY;rd.Texture2DArray.FirstArraySlice=face;rd.Texture2DArray.ArraySize=1;
                check(device->CreateRenderTargetView(colors_[color].Get(),&rd,&faces_[face][color]),"Reflection four-MRT Cube face");
            }
        }
        D3D11_TEXTURE2D_DESC td{};td.Width=td.Height=256;td.ArraySize=6;td.MipLevels=1;td.Format=DXGI_FORMAT_R32_TYPELESS;
        td.SampleDesc.Count=1;td.Usage=D3D11_USAGE_DEFAULT;td.BindFlags=D3D11_BIND_DEPTH_STENCIL|D3D11_BIND_SHADER_RESOURCE;td.MiscFlags=D3D11_RESOURCE_MISC_TEXTURECUBE;
        check(device->CreateTexture2D(&td,nullptr,&depth_ ),"Reflection D32 Cube storage");
        for(UINT face=0;face<6;++face) {
            D3D11_DEPTH_STENCIL_VIEW_DESC dd{};dd.Format=DXGI_FORMAT_D32_FLOAT;dd.ViewDimension=D3D11_DSV_DIMENSION_TEXTURE2DARRAY;dd.Texture2DArray.FirstArraySlice=face;dd.Texture2DArray.ArraySize=1;
            check(device->CreateDepthStencilView(depth_.Get(),&dd,&depthFaces_[face]),"Reflection D32 Cube face");
        }
    }
    std::array<ID3D11RenderTargetView*,4> begin(ID3D11DeviceContext* context,UINT face)const {
        if(face>=6)throw std::out_of_range("Reflection capture face");
        unbindCompute(context);unbindPixel(context);unbindOutputs(context);
        const float zero[4]{},red[4]{1,0,0,1};std::array<ID3D11RenderTargetView*,4> colors{};
        for(UINT i=0;i<4;++i){colors[i]=faces_[face][i].Get();context->ClearRenderTargetView(colors[i],i==3?red:zero);}
        context->ClearDepthStencilView(depthFaces_[face].Get(),D3D11_CLEAR_DEPTH,1,0);
        context->OMSetRenderTargets(4,colors.data(),depthFaces_[face].Get());
        D3D11_VIEWPORT viewport{0,0,256,256,0,1};context->RSSetViewports(1,&viewport);return colors;
    }
    ID3D11ShaderResourceView* cube(UINT color)const{return cubes_.at(color).Get();}
private:
    std::array<ComPtr<ID3D11Texture2D>,4> colors_;
    std::array<ComPtr<ID3D11ShaderResourceView>,4> cubes_;
    std::array<std::array<ComPtr<ID3D11RenderTargetView>,4>,6> faces_;
    ComPtr<ID3D11Texture2D> depth_;
    std::array<ComPtr<ID3D11DepthStencilView>,6> depthFaces_;
};
struct FieldStorageShape {UINT x{},y{},z{},tile{},atlas{},shadow{};float pitch{},range{};Float4 origin{};};
class FieldStorage {
public:
    FieldStorage(ID3D11Device* device,FieldStorageShape shape):shape(shape) {
        if(!shape.tile||!shape.atlas||shape.atlas/shape.tile==0)throw std::runtime_error("Reflection field tile dimensions");
        const auto count64=std::uint64_t(shape.x)*shape.y*shape.z;
        if(!count64||count64>std::numeric_limits<UINT>::max())throw std::runtime_error("Reflection field grid extent");
        probes=UINT(count64);columns=shape.atlas/shape.tile;rows=columns;
        layers=UINT(std::ceil(float(probes)/float(std::uint64_t(columns)*rows)));mips=1;
        for(UINT tile=shape.tile;tile>4;tile>>=1)++mips;
        radiance=makeArray(device,shape.atlas,shape.atlas,layers,mips,DXGI_FORMAT_R11G11B10_FLOAT,true);
        capture=makeArray(device,shape.atlas,shape.atlas,layers*3,1,DXGI_FORMAT_R11G11B10_FLOAT,true);
        scratch=makeArray(device,shape.atlas,shape.atlas,layers,1,DXGI_FORMAT_R11G11B10_FLOAT,false);
        propagationExtent=columns*32;
        propagation=makeArray(device,propagationExtent,propagationExtent,1,1,DXGI_FORMAT_R11G11B10_FLOAT,false);
        sh=makeCoefficients(device,probes,false);working=makeCoefficients(device,probes,false);propagated=makeCoefficients(device,probes,false);sky=makeCoefficients(device,probes,true);
        if(!shape.shadow)throw std::runtime_error("Reflection actual field shadow extent");
        D3D11_TEXTURE2D_DESC td{};td.Width=td.Height=shape.shadow;td.ArraySize=td.MipLevels=1;td.SampleDesc.Count=1;td.Format=DXGI_FORMAT_R32_TYPELESS;
        td.Usage=D3D11_USAGE_DEFAULT;td.BindFlags=D3D11_BIND_DEPTH_STENCIL|D3D11_BIND_SHADER_RESOURCE;
        check(device->CreateTexture2D(&td,nullptr,&shadowTexture),"Reflection actual field shadow map");
        D3D11_DEPTH_STENCIL_VIEW_DESC dd{};dd.Format=DXGI_FORMAT_D32_FLOAT;dd.ViewDimension=D3D11_DSV_DIMENSION_TEXTURE2D;
        check(device->CreateDepthStencilView(shadowTexture.Get(),&dd,&shadowDepth),"Reflection actual field shadow DSV");
        D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=DXGI_FORMAT_R32_FLOAT;sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sd.Texture2D.MipLevels=1;
        check(device->CreateShaderResourceView(shadowTexture.Get(),&sd,&shadowView),"Reflection actual field shadow SRV");
    }
    FieldStorageShape shape;UINT probes{},columns{},rows{},layers{},mips{},propagationExtent{};
    ArrayTexture radiance,capture,scratch,propagation;
    CoefficientBuffer sh,working,propagated,sky;
    ComPtr<ID3D11Texture2D> shadowTexture;ComPtr<ID3D11DepthStencilView> shadowDepth;ComPtr<ID3D11ShaderResourceView> shadowView;
    // Cursor advances only after the actual conversion command submissions.
    int nextCapture{};
};
} // namespace San14::Render::Reflection
