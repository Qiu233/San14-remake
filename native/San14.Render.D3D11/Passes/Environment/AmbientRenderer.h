// Original Sky WORK -> MakeSkyAmbient for the native D3D11 lab. Device inputs
// and cube resources persist; node5 owns its numerical cadence/selection.
// Main/season matrices and weather stay fixed in this experiment. Optional
// Star/Cloud animation consumes the shared original node3 update.
#pragma once
#include "AmbientStarFrame.h"

struct AmbientNativeProgram {
    ComPtr<ID3D11VertexShader> vs;
    ComPtr<ID3D11PixelShader> ps;
    ComPtr<ID3D11GeometryShader> gs;
    ComPtr<ID3D11InputLayout> layout;
};
static AmbientNativeProgram ambientProgram(ID3D11Device* device,const fs::path& source,
    const char* name,bool star=false) {
    auto vc=load(source/(std::string(name)+"-vs.dxbc"));
    auto pc=load(source/(std::string(name)+"-ps.dxbc"));
    AmbientNativeProgram result;
    check(device->CreateVertexShader(vc.data(),vc.size(),nullptr,&result.vs),"ambient original VS");
    check(device->CreatePixelShader(pc.data(),pc.size(),nullptr,&result.ps),"ambient original PS");
    if(star) {
        auto gc=load(source/(std::string(name)+"-gs.dxbc"));
        check(device->CreateGeometryShader(gc.data(),gc.size(),nullptr,&result.gs),"ambient original stars GS");
    }
    ComPtr<ID3D11ShaderReflection> reflection;
    check(D3DReflect(vc.data(),vc.size(),__uuidof(ID3D11ShaderReflection),
        reinterpret_cast<void**>(reflection.GetAddressOf())),"ambient VS reflection");
    D3D11_SHADER_DESC desc{};check(reflection->GetDesc(&desc),"ambient VS description");
    std::vector<D3D11_INPUT_ELEMENT_DESC> inputs;
    for(UINT i=0;i<desc.InputParameters;++i) {
        D3D11_SIGNATURE_PARAMETER_DESC p{};check(reflection->GetInputParameterDesc(i,&p),"ambient VS input");
        if(p.SystemValueType!=D3D_NAME_UNDEFINED)continue;
        DXGI_FORMAT format;UINT offset;
        if(std::strcmp(p.SemanticName,"POSITION")==0) {
            format=star?DXGI_FORMAT_R32G32B32_FLOAT:DXGI_FORMAT_R32G32_FLOAT;offset=0;
        } else if(star&&std::strcmp(p.SemanticName,"PSIZE")==0) {format=DXGI_FORMAT_R32_FLOAT;offset=12;}
        else if(star&&std::strcmp(p.SemanticName,"COLOR")==0) {format=DXGI_FORMAT_R8G8B8A8_UNORM;offset=16;}
        else throw std::runtime_error("Unclosed ambient original vertex input: "+std::string(p.SemanticName));
        inputs.push_back({p.SemanticName,p.SemanticIndex,format,0,offset,D3D11_INPUT_PER_VERTEX_DATA,0});
    }
    if(!inputs.empty())check(device->CreateInputLayout(inputs.data(),UINT(inputs.size()),vc.data(),vc.size(),
        &result.layout),"ambient original input layout");
    return result;
}
static ComPtr<ID3D11SamplerState> ambientSampler(ID3D11Device* device,const fs::path& path) {
    auto bytes=load(path);D3D11_SAMPLER_DESC desc{};
    if(bytes.size()!=sizeof(desc))throw std::runtime_error("Original ambient sampler is not52B");
    std::memcpy(&desc,bytes.data(),sizeof(desc));ComPtr<ID3D11SamplerState> result;
    check(device->CreateSamplerState(&desc,&result),"ambient exact sampler");return result;
}
struct AmbientNativeTextureBinding {
    std::string program;
    UINT slot;
    ComPtr<ID3D11Resource> resource;
    ComPtr<ID3D11ShaderResourceView> view;
    ComPtr<ID3D11SamplerState> sampler;
};
static std::vector<AmbientNativeTextureBinding> ambientTextures(ID3D11Device* device,const fs::path& source,
    const char* tableName="ambient-textures.tsv",UINT expectedCount=8) {
    std::ifstream rows(source/tableName);
    if(!rows)throw std::runtime_error(std::string(tableName)+" missing");
    std::vector<AmbientNativeTextureBinding> result;
    std::string program,prefix;UINT slot,dimension,width,height,depth,mips,resourceFormat,viewFormat;
    while(rows>>program>>slot>>dimension>>width>>height>>depth>>mips>>resourceFormat>>viewFormat>>prefix) {
        if(dimension!=4&&dimension!=8)throw std::runtime_error("Ambient actual texture is not2D/3D");
        std::vector<std::vector<char>> data(mips);std::vector<D3D11_SUBRESOURCE_DATA> initial(mips);
        for(UINT mip=0;mip<mips;++mip) {
            UINT w=std::max(1u,width>>mip),h=std::max(1u,height>>mip),z=std::max(1u,depth>>mip);
            data[mip]=load(source/(prefix+"-mip"+std::to_string(mip)+".bin"));
            UINT pitch,slice;
            if(viewFormat==71||viewFormat==72||viewFormat==98||viewFormat==99) {
                UINT block=viewFormat==71||viewFormat==72?8u:16u;
                pitch=((w+3)/4)*block;slice=pitch*((h+3)/4);
            } else {pitch=w*pixelBytes(DXGI_FORMAT(viewFormat));slice=pitch*h;}
            if(data[mip].size()!=size_t(slice)*z)throw std::runtime_error("Ambient original mip layout differs: "+prefix);
            initial[mip]={data[mip].data(),pitch,slice};
        }
        AmbientNativeTextureBinding binding;binding.program=program;binding.slot=slot;
        D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=DXGI_FORMAT(viewFormat);
        if(dimension==8) {
            D3D11_TEXTURE3D_DESC td{};td.Width=width;td.Height=height;td.Depth=depth;td.MipLevels=mips;
            td.Format=DXGI_FORMAT(resourceFormat);td.Usage=D3D11_USAGE_IMMUTABLE;td.BindFlags=D3D11_BIND_SHADER_RESOURCE;
            ComPtr<ID3D11Texture3D> texture;
            check(device->CreateTexture3D(&td,initial.data(),&texture),"ambient original3D source texture");
            check(texture.As(&binding.resource),"ambient3D source resource");
            sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE3D;sd.Texture3D.MipLevels=mips;
        } else {
            if(depth!=1)throw std::runtime_error("Ambient2D source unexpectedly has volume depth");
            D3D11_TEXTURE2D_DESC td{};td.Width=width;td.Height=height;td.ArraySize=1;td.MipLevels=mips;
            td.Format=DXGI_FORMAT(resourceFormat);td.SampleDesc.Count=1;td.Usage=D3D11_USAGE_IMMUTABLE;td.BindFlags=D3D11_BIND_SHADER_RESOURCE;
            ComPtr<ID3D11Texture2D> texture;
            check(device->CreateTexture2D(&td,initial.data(),&texture),"ambient original2D source texture");
            check(texture.As(&binding.resource),"ambient2D source resource");
            sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sd.Texture2D.MipLevels=mips;
        }
        check(device->CreateShaderResourceView(binding.resource.Get(),&sd,&binding.view),"ambient original source SRV");
        binding.sampler=ambientSampler(device,source/(prefix+"-sampler.bin"));result.push_back(std::move(binding));
    }
    if(result.size()!=expectedCount)throw std::runtime_error("Source Sky/Cloud texture table count differs");
    return result;
}
struct AmbientNativeCube {
    ComPtr<ID3D11Texture2D> texture;
    ComPtr<ID3D11ShaderResourceView> view;
    std::vector<ComPtr<ID3D11ShaderResourceView>> singleMips;
    std::vector<ComPtr<ID3D11RenderTargetView>> surfaces;
};
static AmbientNativeCube ambientCube(ID3D11Device* device,UINT width,UINT levels,DXGI_FORMAT format) {
    AmbientNativeCube cube;D3D11_TEXTURE2D_DESC td{};
    td.Width=td.Height=width;td.MipLevels=levels;td.ArraySize=6;td.Format=format;td.SampleDesc.Count=1;
    td.Usage=D3D11_USAGE_DEFAULT;td.BindFlags=D3D11_BIND_SHADER_RESOURCE|D3D11_BIND_RENDER_TARGET;
    td.MiscFlags=D3D11_RESOURCE_MISC_TEXTURECUBE;
    check(device->CreateTexture2D(&td,nullptr,&cube.texture),"ambient128 cube resource");
    D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=format;sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURECUBE;sd.TextureCube.MipLevels=levels;
    check(device->CreateShaderResourceView(cube.texture.Get(),&sd,&cube.view),"ambient full cube SRV");
    for(UINT mip=0;mip<levels;++mip) {
        sd.TextureCube.MostDetailedMip=mip;sd.TextureCube.MipLevels=1;
        ComPtr<ID3D11ShaderResourceView> v;check(device->CreateShaderResourceView(cube.texture.Get(),&sd,&v),"ambient one-mip cube SRV");
        cube.singleMips.push_back(std::move(v));
    }
    for(UINT face=0;face<6;++face)for(UINT mip=0;mip<levels;++mip) {
        D3D11_RENDER_TARGET_VIEW_DESC rd{};rd.Format=format;rd.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2DARRAY;
        rd.Texture2DArray.MipSlice=mip;rd.Texture2DArray.FirstArraySlice=face;rd.Texture2DArray.ArraySize=1;
        ComPtr<ID3D11RenderTargetView> v;check(device->CreateRenderTargetView(cube.texture.Get(),&rd,&v),"ambient face/mip RTV");
        cube.surfaces.push_back(std::move(v));
    }
    return cube;
}
static ComPtr<ID3D11BlendState> ambientBlend(ID3D11Device* device,bool enable,D3D11_BLEND destination) {
    D3D11_BLEND_DESC desc{};auto& a=desc.RenderTarget[0];a.BlendEnable=enable;
    a.SrcBlend=a.SrcBlendAlpha=D3D11_BLEND_ONE;a.DestBlend=a.DestBlendAlpha=destination;
    a.BlendOp=a.BlendOpAlpha=D3D11_BLEND_OP_ADD;a.RenderTargetWriteMask=15;
    ComPtr<ID3D11BlendState> result;check(device->CreateBlendState(&desc,&result),"ambient source composition");return result;
}

#include "AmbientBudget.h"
struct NativeAmbientFaceConstants {ComPtr<ID3D11Buffer> vs,ps,gs;};
struct NativeAmbientFilterSamples {ComPtr<ID3D11Buffer> directions,weights;ComPtr<ID3D11ShaderResourceView> directionView,weightView;};
struct NativeAmbientDrawCounts {UINT stars=0,sky=0,clouds=0,copy=0,filter=0,blend=0;};
struct NativeAmbient {
    UINT width=0,levels=0,format=0,samples=0,selected=0,points=0,skyEnabled=0;
    AmbientNativeProgram sky,clouds,stars,copy,filter,blendProgram;
    AmbientNativeCube work;
    std::array<AmbientNativeCube,4> outputs;
    std::array<bool,4> completed{};
    std::array<float,6> faceDepthClear{};
    // The original WORK has two cubes (+78/+D0), depth2D(+20), color2D(+18).
    AmbientNativeCube lightSnapshot;
    ComPtr<ID3D11Texture2D> workDepth,workColor;
    ComPtr<ID3D11DepthStencilView> workDsv;
    std::vector<ComPtr<ID3D11ShaderResourceView>> workColorMips;
    std::vector<AmbientNativeTextureBinding> textures;
    ComPtr<ID3D11SamplerState> cubeSampler;
    std::vector<ComPtr<ID3D11SamplerState>> blendMipSamplers;
    ComPtr<ID3D11Buffer> quad,starPoints,blendPixelConstants;
    std::array<NativeAmbientFaceConstants,6> starConstants,skyConstants,cloudConstants;
    std::array<ComPtr<ID3D11Buffer>,6> blendVertexConstants;
    std::vector<ComPtr<ID3D11Buffer>> copyConstants,filterConstants;
    std::vector<NativeAmbientFilterSamples> filterSamples;
    ComPtr<ID3D11BlendState> skyBlend,cloudBlend,starBlend,replace;
    ComPtr<ID3D11RasterizerState> raster;
    ComPtr<ID3D11DepthStencilState> depth;
    NativeAmbientDrawCounts draws;
    NativeAmbientStarContinuous starAnimation;
    bool starAnimationEnabled=false;
    NativeAmbientBudget budget;
    UINT state168=0;
    float elapsed20=0,priorClock18=0;
    ID3D11ShaderResourceView* selectedView() const{return outputs.at(selected).view.Get();}
};
static NativeAmbient prepareNativeAmbient(ID3D11Device* device,const fs::path& source) {
    NativeAmbient owner;std::ifstream settings(source/"ambient-native.txt");
    settings>>owner.width>>owner.levels>>owner.format>>owner.samples>>owner.selected>>owner.points>>owner.skyEnabled;
    if(!settings||owner.width!=128||owner.levels!=7||owner.format!=26||owner.samples!=1024||owner.selected!=0||owner.points!=121318||owner.skyEnabled!=1)
        throw std::runtime_error("Current native ambient packet requires its original128/L7/N1024 profile");
    std::ifstream faceDepth(source/"work-face-depth.txt");UINT faceIndex,faceFlags;float clearDepth;
    for(UINT n=0;n<6;++n) {
        faceDepth>>faceIndex>>faceFlags>>clearDepth;
        if(!faceDepth||faceIndex!=n)throw std::runtime_error("Original WORK face depth inputs absent");
        owner.faceDepthClear[n]=clearDepth;
    }
    owner.sky=ambientProgram(device,source,"sky-current");owner.clouds=ambientProgram(device,source,"clouds");
    owner.stars=ambientProgram(device,source,"stars",true);owner.copy=ambientProgram(device,source,"copy");
    owner.filter=ambientProgram(device,source,"filter");owner.blendProgram=ambientProgram(device,source,"blend");
    owner.work=ambientCube(device,owner.width,owner.levels,DXGI_FORMAT(owner.format));
    owner.lightSnapshot=ambientCube(device,owner.width,owner.levels,DXGI_FORMAT(owner.format));
    for(auto& output:owner.outputs)output=ambientCube(device,owner.width,owner.levels,DXGI_FORMAT(owner.format));
    D3D11_TEXTURE2D_DESC depthDesc{};depthDesc.Width=depthDesc.Height=owner.width;depthDesc.MipLevels=depthDesc.ArraySize=1;
    depthDesc.Format=DXGI_FORMAT_R32_TYPELESS;depthDesc.SampleDesc.Count=1;depthDesc.BindFlags=D3D11_BIND_DEPTH_STENCIL|D3D11_BIND_SHADER_RESOURCE;
    check(device->CreateTexture2D(&depthDesc,nullptr,&owner.workDepth),"WORK20 original native4E depth2D");
    D3D11_DEPTH_STENCIL_VIEW_DESC dsv{};dsv.Format=DXGI_FORMAT_D32_FLOAT;dsv.ViewDimension=D3D11_DSV_DIMENSION_TEXTURE2D;
    check(device->CreateDepthStencilView(owner.workDepth.Get(),&dsv,&owner.workDsv),"WORK20 original DSV40");
    D3D11_TEXTURE2D_DESC colorDesc=depthDesc;colorDesc.MipLevels=owner.levels;colorDesc.Format=DXGI_FORMAT_R16G16B16A16_TYPELESS;
    colorDesc.BindFlags=D3D11_BIND_RENDER_TARGET|D3D11_BIND_SHADER_RESOURCE;
    check(device->CreateTexture2D(&colorDesc,nullptr,&owner.workColor),"WORK18 original native3 color2D");
    for(UINT mip=0;mip<owner.levels;++mip){D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=DXGI_FORMAT_R16G16B16A16_FLOAT;sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;
        sd.Texture2D.MostDetailedMip=mip;sd.Texture2D.MipLevels=1;ComPtr<ID3D11ShaderResourceView> view;
        check(device->CreateShaderResourceView(owner.workColor.Get(),&sd,&view),"WORK28 original single-mip color view");owner.workColorMips.push_back(std::move(view));}
    owner.textures=ambientTextures(device,source);owner.cubeSampler=ambientSampler(device,source/"cube-sampler-desc.bin");
    auto samplerBytes=load(source/"cube-sampler-desc.bin");D3D11_SAMPLER_DESC cubeDescription{};std::memcpy(&cubeDescription,samplerBytes.data(),sizeof(cubeDescription));
    for(UINT mip=0;mip<owner.levels;++mip){cubeDescription.MinLOD=float(mip);ComPtr<ID3D11SamplerState> sampler;
        check(device->CreateSamplerState(&cubeDescription,&sampler),"Ambient blend original per-mip MinLOD");owner.blendMipSamplers.push_back(std::move(sampler));}
    owner.quad=buffer(device,load(source/"quad-position-xy.bin"),D3D11_BIND_VERTEX_BUFFER);
    owner.starPoints=buffer(device,load(source/"star-vertices.bin"),D3D11_BIND_VERTEX_BUFFER);
    owner.skyBlend=ambientBlend(device,true,D3D11_BLEND_SRC_ALPHA);owner.cloudBlend=ambientBlend(device,true,D3D11_BLEND_INV_SRC_ALPHA);
    owner.starBlend=ambientBlend(device,true,D3D11_BLEND_ONE);owner.replace=ambientBlend(device,false,D3D11_BLEND_ZERO);
    D3D11_RASTERIZER_DESC rs{};rs.FillMode=D3D11_FILL_SOLID;rs.CullMode=D3D11_CULL_NONE;rs.DepthClipEnable=TRUE;
    check(device->CreateRasterizerState(&rs,&owner.raster),"Ambient retained cull-off raster");
    D3D11_DEPTH_STENCIL_DESC ds{};ds.DepthWriteMask=D3D11_DEPTH_WRITE_MASK_ZERO;ds.DepthFunc=D3D11_COMPARISON_ALWAYS;
    check(device->CreateDepthStencilState(&ds,&owner.depth),"Ambient retained no-depth");
    auto cb=[&](const std::string& file){return buffer(device,load(source/file),D3D11_BIND_CONSTANT_BUFFER);};
    auto mutableCb=[&](const std::string& file){
        auto bytes=load(source/file);D3D11_BUFFER_DESC d{};d.ByteWidth=UINT(bytes.size());
        d.Usage=D3D11_USAGE_DEFAULT;d.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
        D3D11_SUBRESOURCE_DATA initial{};initial.pSysMem=bytes.data();ComPtr<ID3D11Buffer> result;
        check(device->CreateBuffer(&d,&initial,&result),"Ambient retained animated VS constants");return result;
    };
    for(UINT face=0;face<6;++face){const auto f=std::to_string(face);
        owner.starConstants[face]={mutableCb("stars-face"+f+"-vs-b0.bin"),cb("stars-face"+f+"-ps-b0.bin"),cb("stars-face"+f+"-gs-b0.bin")};
        owner.skyConstants[face]={cb("sky-current-face"+f+"-vs-b0.bin"),cb("sky-current-face"+f+"-ps-b0.bin"),{}};
        owner.cloudConstants[face]={cb("clouds-face"+f+"-vs-b0.bin"),cb("clouds-face"+f+"-ps-b0.bin"),{}};
        owner.blendVertexConstants[face]=cb("blend-face"+f+"-vs-b0.bin");
    }
    D3D11_BUFFER_DESC bd{};bd.ByteWidth=16;bd.Usage=D3D11_USAGE_DEFAULT;bd.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
    auto blendBytes=load(source/"blend-t0-ps-b0.bin");D3D11_SUBRESOURCE_DATA initial{};initial.pSysMem=blendBytes.data();
    check(device->CreateBuffer(&bd,&initial,&owner.blendPixelConstants),"Ambient retained interpolation constants");
    owner.copyConstants.resize(6*owner.levels);owner.filterConstants.resize(6*owner.levels);owner.filterSamples.resize(owner.levels);
    for(UINT face=0;face<6;++face)for(UINT mip=0;mip<owner.levels;++mip){UINT surface=face*owner.levels+mip;
        owner.copyConstants[surface]=cb("copy-surface"+std::to_string(surface)+"-b0.bin");
        if(mip)owner.filterConstants[surface]=cb("filter-surface"+std::to_string(surface)+"-b0.bin");}
    for(UINT mip=1;mip<owner.levels;++mip){auto& sample=owner.filterSamples[mip];
        auto directionBytes=load(source/("filter-mip"+std::to_string(mip)+"-t17-half4.bin")),weightBytes=load(source/("filter-mip"+std::to_string(mip)+"-t18-float.bin"));
        if(directionBytes.size()!=size_t(owner.samples)*8||weightBytes.size()!=size_t(owner.samples)*4)throw std::runtime_error("Ambient filter typed-buffer size differs from N1024");
        sample.directions=buffer(device,directionBytes,D3D11_BIND_SHADER_RESOURCE);sample.weights=buffer(device,weightBytes,D3D11_BIND_SHADER_RESOURCE);
        D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.ViewDimension=D3D11_SRV_DIMENSION_BUFFER;sd.Buffer.NumElements=owner.samples;sd.Format=DXGI_FORMAT_R16G16B16A16_FLOAT;
        check(device->CreateShaderResourceView(sample.directions.Get(),&sd,&sample.directionView),"Ambient retained half4 direction/LOD");sd.Format=DXGI_FORMAT_R32_FLOAT;
        check(device->CreateShaderResourceView(sample.weights.Get(),&sd,&sample.weightView),"Ambient retained float weights");
    }
    return owner;
}
static void selectNativeAmbient(ID3D11DeviceContext* context,NativeAmbient& owner,AmbientNativeProgram& program,ID3D11BlendState* blend) {
    std::array<ID3D11ShaderResourceView*,32> nullViews{};std::array<ID3D11SamplerState*,16> nullSamplers{};
    context->OMSetRenderTargets(0,nullptr,nullptr);context->PSSetShaderResources(0,32,nullViews.data());context->PSSetSamplers(0,16,nullSamplers.data());
    context->IASetInputLayout(program.layout.Get());context->VSSetShader(program.vs.Get(),nullptr,0);context->PSSetShader(program.ps.Get(),nullptr,0);
    context->GSSetShader(program.gs.Get(),nullptr,0);context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);
    context->OMSetBlendState(blend,nullptr,~0u);context->RSSetState(owner.raster.Get());context->OMSetDepthStencilState(owner.depth.Get(),0);
}
static void targetNativeAmbient(ID3D11DeviceContext* context,NativeAmbient& owner,AmbientNativeCube& cube,UINT face,UINT mip,bool workDepth=false) {
    UINT extent=std::max(1u,owner.width>>mip);D3D11_VIEWPORT viewport{0,0,float(extent),float(extent),0,1};context->RSSetViewports(1,&viewport);
    auto target=cube.surfaces[face*owner.levels+mip].Get();context->OMSetRenderTargets(1,&target,workDepth?owner.workDsv.Get():nullptr);
}
static void constantsNativeAmbient(ID3D11DeviceContext* context,const NativeAmbientFaceConstants& constants) {
    auto v=constants.vs.Get(),p=constants.ps.Get(),g=constants.gs.Get();context->VSSetConstantBuffers(0,1,&v);context->PSSetConstantBuffers(0,1,&p);
    if(g)context->GSSetConstantBuffers(0,1,&g);
}
static void texturesNativeAmbient(ID3D11DeviceContext* context,NativeAmbient& owner,const char* program) {
    for(auto& texture:owner.textures)if(texture.program==program){auto v=texture.view.Get();auto s=texture.sampler.Get();context->PSSetShaderResources(texture.slot,1,&v);context->PSSetSamplers(texture.slot,1,&s);}
}
static void cleanupNativeAmbient(ID3D11DeviceContext* context) {
    context->OMSetRenderTargets(0,nullptr,nullptr);std::array<ID3D11ShaderResourceView*,32> empty{};context->PSSetShaderResources(0,32,empty.data());context->GSSetShader(nullptr,nullptr,0);
}
static UINT executeNativeAmbientWork(ID3D11DeviceContext* context,NativeAmbient& owner,const NativeAmbientWorkPlan& plan,
    float unscaledFrameDelta=0) {
    auto& output=owner.outputs.at(plan.generation);
    for(UINT face=plan.firstBaseFace;face<plan.firstBaseFace+plan.baseFaces;++face){
        // E40540 first push: RGBA black plus independent WORK20 depth.
        // Face-camera158 owns reverse depth; Main3E4 is not its operand.
        const float clear[4]{};context->ClearRenderTargetView(owner.work.surfaces[face*owner.levels].Get(),clear);
        context->ClearDepthStencilView(owner.workDsv.Get(),D3D11_CLEAR_DEPTH,owner.faceDepthClear[face],0);
        if(face==3)continue;
        if(owner.starAnimationEnabled)updateNativeAmbientStarFace(context,owner.starAnimation,
            owner.starConstants[face],face,unscaledFrameDelta);
        selectNativeAmbient(context,owner,owner.stars,owner.starBlend.Get());targetNativeAmbient(context,owner,owner.work,face,0,true);
        constantsNativeAmbient(context,owner.starConstants[face]);auto vb=owner.starPoints.Get();UINT stride=20,offset=0;
        context->IASetVertexBuffers(0,1,&vb,&stride,&offset);context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_POINTLIST);context->Draw(owner.points,0);++owner.draws.stars;
        selectNativeAmbient(context,owner,owner.sky,owner.skyBlend.Get());targetNativeAmbient(context,owner,owner.work,face,0,true);texturesNativeAmbient(context,owner,"sky-current");
        constantsNativeAmbient(context,owner.skyConstants[face]);vb=owner.quad.Get();stride=8;context->IASetVertexBuffers(0,1,&vb,&stride,&offset);
        context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP);context->Draw(4,0);++owner.draws.sky;
        selectNativeAmbient(context,owner,owner.clouds,owner.cloudBlend.Get());targetNativeAmbient(context,owner,owner.work,face,0,true);texturesNativeAmbient(context,owner,"clouds");
        constantsNativeAmbient(context,owner.cloudConstants[face]);vb=nullptr;stride=offset=0;context->IASetVertexBuffers(0,1,&vb,&stride,&offset);
        context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP);context->Draw(4,0);++owner.draws.clouds;
    }
    auto cubeDraw=[&](AmbientNativeProgram& program,AmbientNativeCube& destination,UINT face,UINT mip,ID3D11ShaderResourceView* input,ID3D11Buffer* cb){
        selectNativeAmbient(context,owner,program,owner.replace.Get());targetNativeAmbient(context,owner,destination,face,mip);
        context->VSSetConstantBuffers(0,1,&cb);context->PSSetConstantBuffers(0,1,&cb);auto sampler=owner.cubeSampler.Get();
        context->PSSetShaderResources(2,1,&input);context->PSSetSamplers(2,1,&sampler);ID3D11Buffer* vb=nullptr;UINT stride=0,offset=0;
        context->IASetVertexBuffers(0,1,&vb,&stride,&offset);context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP);
    };
    if(plan.copy)for(UINT face=0;face<6;++face)for(UINT mip=0;mip<owner.levels;++mip){UINT surface=face*owner.levels+mip;
        cubeDraw(owner.copy,mip==0?output:owner.work,face,mip,mip==0?owner.work.view.Get():owner.work.singleMips[mip-1].Get(),owner.copyConstants[surface].Get());
        context->Draw(3,0);++owner.draws.copy;}
    for(UINT flat=plan.firstFilter;flat<plan.firstFilter+plan.filterFaces;++flat){UINT face=flat%6,mip=flat/6;
        cubeDraw(owner.filter,output,face,mip,owner.work.view.Get(),owner.filterConstants[face*owner.levels+mip].Get());
        auto& sample=owner.filterSamples[mip];ID3D11ShaderResourceView* views[]{sample.directionView.Get(),sample.weightView.Get()};context->PSSetShaderResources(17,2,views);
        context->Draw(3,0);++owner.draws.filter;}
    if(plan.completed)owner.completed.at(plan.completedIndex)=true;cleanupNativeAmbient(context);return plan.units;
}
static UINT executeNativeAmbientBlend(ID3D11DeviceContext* context,NativeAmbient& owner,UINT nextGeneration,float fraction) {
    UINT first=(nextGeneration+1)%3,second=(nextGeneration+2)%3;const float info[4]{fraction,0,0,0};context->UpdateSubresource(owner.blendPixelConstants.Get(),0,nullptr,info,0,0);
    for(UINT face=0;face<6;++face)for(UINT mip=0;mip<owner.levels;++mip){
        selectNativeAmbient(context,owner,owner.blendProgram,owner.replace.Get());targetNativeAmbient(context,owner,owner.outputs[3],face,mip);
        auto vs=owner.blendVertexConstants[face].Get(),ps=owner.blendPixelConstants.Get();context->VSSetConstantBuffers(0,1,&vs);context->PSSetConstantBuffers(0,1,&ps);
        auto a=owner.outputs[first].view.Get(),b=owner.outputs[second].view.Get();auto s=owner.blendMipSamplers[mip].Get();
        context->PSSetShaderResources(2,1,&a);context->PSSetShaderResources(4,1,&b);context->PSSetSamplers(2,1,&s);context->PSSetSamplers(4,1,&s);
        ID3D11Buffer* vb=nullptr;UINT stride=0,offset=0;context->IASetVertexBuffers(0,1,&vb,&stride,&offset);
        context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP);context->Draw(4,0);++owner.draws.blend;
    }
    owner.completed[3]=owner.completed[first]&&owner.completed[second];cleanupNativeAmbient(context);return 6;
}
static ID3D11ShaderResourceView* executeNativeAmbientBatch(ID3D11DeviceContext* context,NativeAmbient& owner,UINT destination=0) {
    // Explicit finite backend batch, not a native node5 clock/initialization receipt.
    auto plan=planNativeAmbientWork(destination,0,1,43);executeNativeAmbientWork(context,owner,plan);owner.selected=destination;
    return owner.selectedView();
}
static ID3D11ShaderResourceView* executeNativeAmbientNode5(ID3D11DeviceContext* context,NativeAmbient& owner,
    float currentCommonClock,bool allowsWorkInitialization,float unscaledFrameDelta=0) {
    auto request=prepareNativeAmbientBudget(owner.budget,currentCommonClock);
    float elapsed=owner.elapsed20;
    if(!allowsWorkInitialization)owner.state168|=0x40000000u;
    else {
        // Prepare has actually created all four typed top-level WORK inputs.
        // E4690E/E46918 publishes bit31 before1197140 clears only the two
        // internal cubes: D0 then78, mip-major then face. External cubes,
        // color2D18 and depth2D20 are deliberately not initialized by this.
        if((owner.state168&0x80000000u)==0){
            owner.state168=(owner.state168&~0x40000000u)|0x80000000u;
            const float zero[4]{};
            for(auto* cube:{&owner.lightSnapshot,&owner.work})for(UINT mip=0;mip<owner.levels;++mip)for(UINT face=0;face<6;++face)
                context->ClearRenderTargetView(cube->surfaces[face*owner.levels+mip].Get(),zero);
        }
        auto plan=planNativeAmbientWork(owner.state168,elapsed,1.f,request.units);
        executeNativeAmbientWork(context,owner,plan,unscaledFrameDelta);
        if(plan.blend)executeNativeAmbientBlend(context,owner,(plan.exitState168&3u),plan.blendFraction);
        owner.state168=plan.exitState168;elapsed=plan.elapsed;
    }
    owner.selected=(owner.state168>>8)&3u;
    owner.elapsed20=nativeAmbientBudgetAdd(elapsed,std::max(nativeAmbientBudgetSub(currentCommonClock,owner.priorClock18),0.f));
    owner.priorClock18=currentCommonClock;completeNativeAmbientBudget(owner.budget,request);
    // Source selection is not a claim that a fresh selected slot has pixels.
    // Caller can inspect completed[selected]; no substitute cube is selected.
    return owner.selectedView();
}
static void captureNativeAmbientCube(ID3D11Device* device,ID3D11DeviceContext* context,NativeAmbient& owner,const fs::path& output,
    UINT cubeIndex=~0u) {
    fs::create_directories(output);const UINT captured=cubeIndex==~0u?owner.selected:cubeIndex;auto& target=owner.outputs.at(captured);
    D3D11_TEXTURE2D_DESC td{};target.texture->GetDesc(&td);td.Usage=D3D11_USAGE_STAGING;td.BindFlags=0;td.CPUAccessFlags=D3D11_CPU_ACCESS_READ;td.MiscFlags=0;
    ComPtr<ID3D11Texture2D> readback;check(device->CreateTexture2D(&td,nullptr,&readback),"Ambient explicit cube staging");context->CopyResource(readback.Get(),target.texture.Get());
    std::ofstream raw(output/"ambient-produced-cube-r11g11b10.bin",std::ios::binary),rows(output/"ambient-produced-subresources.tsv");
    UINT64 totalBytes=0;double sum=0;UINT64 finite=0,nonfinite=0;float minimum=INFINITY,maximum=-INFINITY;
    for(UINT face=0;face<6;++face)for(UINT mip=0;mip<owner.levels;++mip){UINT extent=std::max(1u,owner.width>>mip),sub=D3D11CalcSubresource(mip,face,owner.levels);D3D11_MAPPED_SUBRESOURCE mapped{};
        check(context->Map(readback.Get(),sub,D3D11_MAP_READ,0,&mapped),"Ambient explicit cube readback");rows<<face<<'\t'<<mip<<'\t'<<extent<<'\t'<<totalBytes<<'\t'<<extent*extent*4<<'\n';
        for(UINT y=0;y<extent;++y){const char* p=static_cast<const char*>(mapped.pData)+y*mapped.RowPitch;raw.write(p,extent*4);
            for(UINT x=0;x<extent;++x){auto values=decode(p+x*4,DXGI_FORMAT(owner.format));for(UINT lane=0;lane<3;++lane){float value=values[lane];
                if(!std::isfinite(value))++nonfinite;else{++finite;sum+=value;minimum=std::min(minimum,value);maximum=std::max(maximum,value);}}}}
        totalBytes+=extent*extent*4;context->Unmap(readback.Get(),sub);
    }
    const auto& d=owner.draws;std::ofstream receipt(output/"ambient-native-result.txt");
    receipt<<"source-selected-cube-index "<<owner.selected<<"\ncaptured-cube-index "<<captured<<"\noriginal-cube-object 23F44EB5\nwidth "<<owner.width<<"\nlevels "<<owner.levels<<"\nformat "<<owner.format
        <<"\nfilter-samples "<<owner.samples<<"\nbase-clears 6\nstar-draws "<<d.stars<<"\nsky-draws "<<d.sky<<"\ncloud-draws "<<d.clouds<<"\ncopy-draws "<<d.copy
        <<"\nfilter-draws "<<d.filter<<"\npacked-bytes "<<totalBytes<<"\nnonfinite "<<nonfinite<<"\nminimum "<<minimum<<"\nmaximum "<<maximum<<"\nmean "<<(finite?sum/finite:0)
        <<"\noriginal-cpu168-work-return-proved 0\nnormal-Host-ready-proved 0\n";
    std::cout<<"Ambient retained cube128/L7, Stars="<<d.stars<<" Sky="<<d.sky<<" Clouds="<<d.clouds<<" copy="<<d.copy<<" filter="<<d.filter<<" finite="<<finite<<" nonfinite="<<nonfinite<<'\n';
}
static ComPtr<ID3D11ShaderResourceView> produceAmbientCube(ID3D11Device* device,ID3D11DeviceContext* context,const fs::path& source,const fs::path& output) {
    auto owner=prepareNativeAmbient(device,source);executeNativeAmbientBatch(context,owner);captureNativeAmbientCube(device,context,owner,output);
    ComPtr<ID3D11ShaderResourceView> result=owner.outputs[owner.selected].view;return result;
}
