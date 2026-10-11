// Prepared Sky and Cloud inputs feed the persistent WORK cube resources.
// Current matrices and weather are updated independently of device creation;
// optional Star and Cloud animation consume the shared Main clock update.
#pragma once
#include "AmbientStarFrame.h"
#include "AmbientResourceInputs.h"
#include "SkyMaterialInputs.h"
#include "SkyTextureIdentity.h"
#include <optional>

struct AmbientNativeProgram {
    ComPtr<ID3D11VertexShader> vs;
    ComPtr<ID3D11PixelShader> ps;
    ComPtr<ID3D11GeometryShader> gs;
    ComPtr<ID3D11InputLayout> layout;
};
static AmbientNativeProgram ambientProgram(ID3D11Device* device,const fs::path& source,
    const char* name) {
    auto vc=load(source/(std::string(name)+"-vs.dxbc"));
    auto pc=load(source/(std::string(name)+"-ps.dxbc"));
    AmbientNativeProgram result;
    check(device->CreateVertexShader(vc.data(),vc.size(),nullptr,&result.vs),"ambient original VS");
    check(device->CreatePixelShader(pc.data(),pc.size(),nullptr,&result.ps),"ambient original PS");
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
            format=DXGI_FORMAT_R32G32_FLOAT;offset=0;
        }
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
    San14::Render::SkyTextureIdentity identity;
    ComPtr<ID3D11Resource> resource;
    ComPtr<ID3D11ShaderResourceView> view;
    ComPtr<ID3D11SamplerState> sampler;
};
static std::vector<AmbientNativeTextureBinding> ambientTextures(ID3D11Device* device,const fs::path& source,
    const char* tableName,std::optional<UINT> expectedCount,bool allowEmpty=false) {
    std::ifstream rows(source/tableName);
    if(!rows)throw std::runtime_error(std::string(tableName)+" missing");
    std::vector<AmbientNativeTextureBinding> result;
    std::string line;
    while(std::getline(rows,line)) {
        const auto imported=San14::Render::Interop::readSkyTextureBindingRow(line);
        if(!imported)continue;
        const auto& row=*imported;
        const auto& program=row.program;
        const auto& prefix=row.prefix;
        const UINT slot=row.slot,dimension=row.dimension,width=row.width,height=row.height,
            depth=row.depth,mips=row.levels,resourceFormat=row.resourceFormat,viewFormat=row.viewFormat;
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
        AmbientNativeTextureBinding binding;binding.program=program;binding.slot=slot;binding.identity=row.identity;
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
    if(expectedCount&&result.size()!=*expectedCount&&!(allowEmpty&&result.empty()))throw std::runtime_error("Source Sky/Cloud texture table count differs");
    return result;
}
struct AmbientNativeCube {
    San14::Render::AmbientTargetDescription description;
    ComPtr<ID3D11Texture2D> texture;
    ComPtr<ID3D11ShaderResourceView> view;
    std::vector<ComPtr<ID3D11ShaderResourceView>> singleMips;
    std::vector<ComPtr<ID3D11RenderTargetView>> surfaces;
};
static AmbientNativeCube ambientCube(ID3D11Device* device,const San14::Render::AmbientTargetDescription& description) {
    const UINT width=description.width,levels=description.levels;
    AmbientNativeCube cube;cube.description=description;D3D11_TEXTURE2D_DESC td{};
    td.Width=td.Height=width;td.MipLevels=levels;td.ArraySize=6;td.Format=DXGI_FORMAT(description.resourceFormat);td.SampleDesc.Count=description.samples;
    td.Usage=D3D11_USAGE_DEFAULT;td.BindFlags=D3D11_BIND_SHADER_RESOURCE|D3D11_BIND_RENDER_TARGET;
    td.MiscFlags=D3D11_RESOURCE_MISC_TEXTURECUBE;
    check(device->CreateTexture2D(&td,nullptr,&cube.texture),"ambient declared cube resource");
    D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=DXGI_FORMAT(description.shaderFormat);sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURECUBE;sd.TextureCube.MipLevels=levels;
    check(device->CreateShaderResourceView(cube.texture.Get(),&sd,&cube.view),"ambient full cube SRV");
    for(UINT mip=0;mip<levels;++mip) {
        sd.TextureCube.MostDetailedMip=mip;sd.TextureCube.MipLevels=1;
        ComPtr<ID3D11ShaderResourceView> v;check(device->CreateShaderResourceView(cube.texture.Get(),&sd,&v),"ambient one-mip cube SRV");
        cube.singleMips.push_back(std::move(v));
    }
    for(UINT face=0;face<6;++face)for(UINT mip=0;mip<levels;++mip) {
        D3D11_RENDER_TARGET_VIEW_DESC rd{};rd.Format=DXGI_FORMAT(description.targetFormat);rd.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2DARRAY;
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

#include "AmbientGeneration.h"
#include "MainStarRenderer.h"
struct NativeAmbientFaceConstants {ComPtr<ID3D11Buffer> vs,ps,gs;};
struct NativeAmbientFilterSamples {ComPtr<ID3D11Buffer> directions,weights;ComPtr<ID3D11ShaderResourceView> directionView,weightView;float weightSum{};};
struct NativeAmbientMaterialProgram {
    AmbientNativeProgram shaders;
    San14::Render::Interop::AmbientProgramLayout layout;
    NativeAmbientFaceConstants constants;
    std::vector<char> vertexBytes,pixelBytes;
};
struct NativeAmbientSkyFace {
    San14::Render::SkyMaterialState state;
    NativeAmbientFaceConstants constants;
    std::vector<char> vertexBytes,pixelBytes;
};
static ComPtr<ID3D11Buffer> createNativeAmbientConstants(ID3D11Device* device,const std::vector<char>& bytes) {
    D3D11_BUFFER_DESC description{};description.ByteWidth=UINT(bytes.size());
    description.Usage=D3D11_USAGE_DEFAULT;description.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
    D3D11_SUBRESOURCE_DATA initial{};initial.pSysMem=bytes.data();ComPtr<ID3D11Buffer> result;
    check(device->CreateBuffer(&description,&initial,&result),"Ambient selected-layout constants");return result;
}
static NativeAmbientMaterialProgram prepareNativeAmbientMaterialProgram(ID3D11Device* device,const fs::path& source,
    const char* name,const San14::Render::Interop::AmbientProgramLayout& layout) {
    NativeAmbientMaterialProgram result;result.shaders=ambientProgram(device,source,name);result.layout=layout;
    result.vertexBytes.resize(layout.vertex.byteSize);result.pixelBytes.resize(layout.pixel.byteSize);
    result.constants.vs=createNativeAmbientConstants(device,result.vertexBytes);
    result.constants.ps=createNativeAmbientConstants(device,result.pixelBytes);return result;
}
static void publishNativeAmbientMaterial(ID3D11DeviceContext* context,NativeAmbientMaterialProgram& program,
    const San14::Render::AmbientMaterialState& material,
    const std::array<ID3D11ShaderResourceView*,4>& resources,const std::array<ID3D11SamplerState*,2>& samplers) {
    program.layout.vertex.pack(material,program.vertexBytes);program.layout.pixel.pack(material,program.pixelBytes);
    context->UpdateSubresource(program.constants.vs.Get(),0,nullptr,program.vertexBytes.data(),0,0);
    context->UpdateSubresource(program.constants.ps.Get(),0,nullptr,program.pixelBytes.data(),0,0);
    auto vertex=program.constants.vs.Get(),pixel=program.constants.ps.Get();
    context->VSSetConstantBuffers(program.layout.vertex.slot,1,&vertex);
    context->PSSetConstantBuffers(program.layout.pixel.slot,1,&pixel);
    for(const auto& binding:program.layout.resources) {
        auto view=resources[binding.role];
        if(binding.stage)context->VSSetShaderResources(binding.slot,1,&view);else context->PSSetShaderResources(binding.slot,1,&view);
    }
    for(const auto& binding:program.layout.samplers) {
        auto sampler=samplers[binding.role];
        if(binding.stage)context->VSSetSamplers(binding.slot,1,&sampler);else context->PSSetSamplers(binding.slot,1,&sampler);
    }
}
struct NativeAmbientDrawCounts {UINT stars=0,sky=0,clouds=0,copy=0,filter=0,blend=0;};
struct NativeAmbient {
    UINT width=0,levels=0,samples=0,skyEnabled=0;
    San14::Render::AmbientResourceDescription description;
    AmbientNativeProgram sky,clouds;
    std::array<NativeAmbientMaterialProgram,3> materialPrograms;
    San14::Render::Interop::SkyMaterialLayout skyLayout;
    San14::Render::Interop::SkySamplingPresence skySampling;
    std::array<NativeAmbientSkyFace,6> skyFaces;
    std::array<source_camera::Matrix,6> blendCameras;
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
    std::array<std::vector<ComPtr<ID3D11SamplerState>>,4> blendMipSamplers;
    ComPtr<ID3D11Buffer> quad;
    std::array<NativeAmbientFaceConstants,6> cloudConstants;
    std::vector<NativeAmbientFilterSamples> filterSamples;
    ComPtr<ID3D11BlendState> skyBlend,cloudBlend,starBlend,replace;
    ComPtr<ID3D11RasterizerState> raster;
    ComPtr<ID3D11DepthStencilState> depth;
    NativeAmbientDrawCounts draws;
    San14::Render::MainStarPass* starOwner{};
    const source_weather_fog::Power* starPower{};
    NativeAmbientStarCameras starCameras;
    AmbientGeneration generation;
    ID3D11ShaderResourceView* selectedView() const{return outputs.at(generation.selectedOutput()).view.Get();}
};
static NativeAmbient prepareNativeAmbient(ID3D11Device* device,const fs::path& source,
    San14::Render::MainStarPass& stars,const source_weather_fog::Power& power) {
    NativeAmbient owner;
    owner.description=San14::Render::Interop::readAmbientResourceDescription(source/"targets.bin");
    const auto& targets=owner.description.targets;
    owner.width=targets[4].width;owner.levels=targets[4].levels;
    owner.samples=owner.description.filterSamples;owner.skyEnabled=owner.description.skyEnabled?1:0;
    owner.starOwner=&stars;owner.starPower=&power;
    owner.starCameras=readNativeAmbientStarCameras(source/"star-cameras.bin");
    std::ifstream faceDepth(source/"work-face-depth.txt");UINT faceIndex,faceFlags;float clearDepth;
    for(UINT face=0;face<6;++face) {
        faceDepth>>faceIndex>>faceFlags>>clearDepth;
        if(!faceDepth||faceIndex!=face)throw std::runtime_error("WORK face depth inputs absent");
        owner.faceDepthClear[face]=clearDepth;
    }
    const auto skySource=source/"sky";
    owner.sky=ambientProgram(device,skySource,"main-sky");owner.clouds=ambientProgram(device,source,"clouds");
    owner.skyLayout=San14::Render::Interop::readSkyMaterialLayout(skySource/"material-layout.bin");
    owner.skySampling=San14::Render::Interop::readSkySamplingPresence(skySource/"sampling-presence.bin");
    for(unsigned face=0;face<6;++face) {
        auto& sky=owner.skyFaces[face];
        sky.state=San14::Render::Interop::readSkyInitialState(skySource/("face"+std::to_string(face)+".bin"));
        sky.vertexBytes.resize(owner.skyLayout.vertex.byteSize);sky.pixelBytes.resize(owner.skyLayout.pixel.byteSize);
        owner.skyLayout.vertex.pack(sky.state,sky.vertexBytes);owner.skyLayout.pixel.pack(sky.state,sky.pixelBytes);
        sky.constants.vs=createNativeAmbientConstants(device,sky.vertexBytes);
        sky.constants.ps=createNativeAmbientConstants(device,sky.pixelBytes);
    }
    const auto layouts=San14::Render::Interop::readAmbientMaterialLayout(source/"material-layout.bin");
    const char* names[]{"copy","filter","blend"};
    for(unsigned operation=0;operation<3;++operation)
        owner.materialPrograms[operation]=prepareNativeAmbientMaterialProgram(device,source,names[operation],layouts[operation]);
    owner.blendCameras=San14::Render::Interop::readAmbientBlendCameras(source/"blend-cameras.bin");
    owner.work=ambientCube(device,targets[4]);owner.lightSnapshot=ambientCube(device,targets[5]);
    for(unsigned output=0;output<4;++output)owner.outputs[output]=ambientCube(device,targets[output]);
    D3D11_TEXTURE2D_DESC depth{};depth.Width=targets[6].width;depth.Height=targets[6].height;
    depth.MipLevels=targets[6].levels;depth.ArraySize=1;depth.Format=DXGI_FORMAT(targets[6].resourceFormat);
    depth.SampleDesc.Count=targets[6].samples;depth.BindFlags=D3D11_BIND_DEPTH_STENCIL|D3D11_BIND_SHADER_RESOURCE;
    check(device->CreateTexture2D(&depth,nullptr,&owner.workDepth),"Ambient declared WORK depth");
    D3D11_DEPTH_STENCIL_VIEW_DESC dsv{};dsv.Format=DXGI_FORMAT(targets[6].depthFormat);dsv.ViewDimension=D3D11_DSV_DIMENSION_TEXTURE2D;
    check(device->CreateDepthStencilView(owner.workDepth.Get(),&dsv,&owner.workDsv),"Ambient declared WORK depth view");
    auto color=depth;color.Width=targets[7].width;color.Height=targets[7].height;color.MipLevels=targets[7].levels;
    color.Format=DXGI_FORMAT(targets[7].resourceFormat);color.SampleDesc.Count=targets[7].samples;
    color.BindFlags=D3D11_BIND_RENDER_TARGET|D3D11_BIND_SHADER_RESOURCE;
    check(device->CreateTexture2D(&color,nullptr,&owner.workColor),"Ambient declared WORK color");
    for(UINT mip=0;mip<targets[7].levels;++mip) {
        D3D11_SHADER_RESOURCE_VIEW_DESC view{};view.Format=DXGI_FORMAT(targets[7].shaderFormat);
        view.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;view.Texture2D.MostDetailedMip=mip;view.Texture2D.MipLevels=1;
        ComPtr<ID3D11ShaderResourceView> resource;
        check(device->CreateShaderResourceView(owner.workColor.Get(),&view,&resource),"Ambient WORK color mip view");
        owner.workColorMips.push_back(std::move(resource));
    }
    owner.textures=ambientTextures(device,skySource,"main-sky-textures.tsv",std::nullopt);
    auto clouds=ambientTextures(device,source,"cloud-textures.tsv",7);
    for(auto& texture:clouds)owner.textures.push_back(std::move(texture));
    owner.cubeSampler=ambientSampler(device,source/"cube4-sampler-desc.bin");
    for(UINT output=0;output<4;++output) {
        const auto bytes=load(source/("cube"+std::to_string(output)+"-sampler-desc.bin"));
        if(bytes.size()!=sizeof(D3D11_SAMPLER_DESC))throw std::runtime_error("Ambient output sampler extent");
        D3D11_SAMPLER_DESC description{};std::memcpy(&description,bytes.data(),sizeof(description));
        for(UINT mip=0;mip<owner.levels;++mip) {
            description.MinLOD=float(mip);ComPtr<ID3D11SamplerState> sampler;
            check(device->CreateSamplerState(&description,&sampler),"Ambient output-specific mip sampler");
            owner.blendMipSamplers[output].push_back(std::move(sampler));
        }
    }
    owner.quad=buffer(device,load(skySource/"quad-position-xy.bin"),D3D11_BIND_VERTEX_BUFFER);
    owner.skyBlend=ambientBlend(device,true,D3D11_BLEND_SRC_ALPHA);owner.cloudBlend=ambientBlend(device,true,D3D11_BLEND_INV_SRC_ALPHA);
    owner.starBlend=ambientBlend(device,true,D3D11_BLEND_ONE);owner.replace=ambientBlend(device,false,D3D11_BLEND_ZERO);
    D3D11_RASTERIZER_DESC raster{};raster.FillMode=D3D11_FILL_SOLID;raster.CullMode=D3D11_CULL_NONE;raster.DepthClipEnable=TRUE;
    check(device->CreateRasterizerState(&raster,&owner.raster),"Ambient cull-off raster");
    D3D11_DEPTH_STENCIL_DESC state{};state.DepthWriteMask=D3D11_DEPTH_WRITE_MASK_ZERO;state.DepthFunc=D3D11_COMPARISON_ALWAYS;
    check(device->CreateDepthStencilState(&state,&owner.depth),"Ambient no-depth state");
    for(UINT face=0;face<6;++face) {
        const auto prefix="clouds-face"+std::to_string(face);
        owner.cloudConstants[face]={buffer(device,load(source/(prefix+"-vs-b0.bin")),D3D11_BIND_CONSTANT_BUFFER),
            buffer(device,load(source/(prefix+"-ps-b0.bin")),D3D11_BIND_CONSTANT_BUFFER),{}};
    }
    owner.filterSamples.resize(owner.levels);
    for(UINT mip=1;mip<owner.levels;++mip) {
        auto& sample=owner.filterSamples[mip];const auto prefix="filter-mip"+std::to_string(mip);
        const auto directions=load(source/(prefix+"-t17-half4.bin")),weights=load(source/(prefix+"-t18-float.bin"));
        if(directions.size()!=size_t(owner.samples)*8||weights.size()!=size_t(owner.samples)*4)
            throw std::runtime_error("Ambient selected sample buffers do not match the requested count");
        const auto weightSum=load(source/(prefix+"-weight-sum.bin"));
        if(weightSum.size()!=4)throw std::runtime_error("Ambient sample weight sum extent");
        std::memcpy(&sample.weightSum,weightSum.data(),4);
        sample.directions=buffer(device,directions,D3D11_BIND_SHADER_RESOURCE);sample.weights=buffer(device,weights,D3D11_BIND_SHADER_RESOURCE);
        D3D11_SHADER_RESOURCE_VIEW_DESC view{};view.ViewDimension=D3D11_SRV_DIMENSION_BUFFER;view.Buffer.NumElements=owner.samples;
        view.Format=DXGI_FORMAT_R16G16B16A16_FLOAT;
        check(device->CreateShaderResourceView(sample.directions.Get(),&view,&sample.directionView),"Ambient actual half4 direction/LOD");
        view.Format=DXGI_FORMAT_R32_FLOAT;
        check(device->CreateShaderResourceView(sample.weights.Get(),&view,&sample.weightView),"Ambient actual float weights");
    }
    return owner;
}
static void publishNativeAmbientSkyFace(ID3D11DeviceContext* context,NativeAmbient& owner,UINT face,
    const San14::Render::SkyMaterialState& material) {
    auto& sky=owner.skyFaces[face];sky.state=material;
    owner.skyLayout.vertex.pack(material,sky.vertexBytes);owner.skyLayout.pixel.pack(material,sky.pixelBytes);
    context->UpdateSubresource(sky.constants.vs.Get(),0,nullptr,sky.vertexBytes.data(),0,0);
    context->UpdateSubresource(sky.constants.ps.Get(),0,nullptr,sky.pixelBytes.data(),0,0);
}
static void selectNativeAmbientState(ID3D11DeviceContext* context,NativeAmbient& owner,ID3D11BlendState* blend) {
    std::array<ID3D11ShaderResourceView*,128> nullViews{};std::array<ID3D11SamplerState*,16> nullSamplers{};
    context->OMSetRenderTargets(0,nullptr,nullptr);
    context->PSSetShaderResources(0,UINT(nullViews.size()),nullViews.data());context->VSSetShaderResources(0,UINT(nullViews.size()),nullViews.data());
    context->PSSetSamplers(0,UINT(nullSamplers.size()),nullSamplers.data());context->VSSetSamplers(0,UINT(nullSamplers.size()),nullSamplers.data());
    context->OMSetBlendState(blend,nullptr,~0u);context->RSSetState(owner.raster.Get());context->OMSetDepthStencilState(owner.depth.Get(),0);
}
static void selectNativeAmbient(ID3D11DeviceContext* context,NativeAmbient& owner,AmbientNativeProgram& program,ID3D11BlendState* blend) {
    selectNativeAmbientState(context,owner,blend);
    context->IASetInputLayout(program.layout.Get());context->VSSetShader(program.vs.Get(),nullptr,0);context->PSSetShader(program.ps.Get(),nullptr,0);
    context->GSSetShader(program.gs.Get(),nullptr,0);context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);
}
static void targetNativeAmbient(ID3D11DeviceContext* context,NativeAmbient& owner,AmbientNativeCube& cube,UINT face,UINT mip,bool workDepth=false) {
    UINT extent=std::max(1u,cube.description.width>>mip);D3D11_VIEWPORT viewport{0,0,float(extent),float(extent),0,1};context->RSSetViewports(1,&viewport);
    auto target=cube.surfaces[face*cube.description.levels+mip].Get();context->OMSetRenderTargets(1,&target,workDepth?owner.workDsv.Get():nullptr);
}
static void constantsNativeAmbient(ID3D11DeviceContext* context,const NativeAmbientFaceConstants& constants) {
    auto v=constants.vs.Get(),p=constants.ps.Get(),g=constants.gs.Get();context->VSSetConstantBuffers(0,1,&v);context->PSSetConstantBuffers(0,1,&p);
    if(g)context->GSSetConstantBuffers(0,1,&g);
}
static void texturesNativeAmbient(ID3D11DeviceContext* context,NativeAmbient& owner,const char* program) {
    for(auto& texture:owner.textures)if(texture.program==program){auto v=texture.view.Get();auto s=texture.sampler.Get();context->PSSetShaderResources(texture.slot,1,&v);context->PSSetSamplers(texture.slot,1,&s);}
}
static void cleanupNativeAmbient(ID3D11DeviceContext* context) {
    context->OMSetRenderTargets(0,nullptr,nullptr);std::array<ID3D11ShaderResourceView*,128> empty{};
    context->PSSetShaderResources(0,UINT(empty.size()),empty.data());context->VSSetShaderResources(0,UINT(empty.size()),empty.data());
    std::array<ID3D11SamplerState*,16> samplers{};
    context->PSSetSamplers(0,UINT(samplers.size()),samplers.data());context->VSSetSamplers(0,UINT(samplers.size()),samplers.data());
    context->GSSetShader(nullptr,nullptr,0);
}
static UINT executeNativeAmbientWork(ID3D11DeviceContext* context,NativeAmbient& owner,const NativeAmbientWorkPlan& plan) {
    auto& output=owner.outputs.at(plan.generation);
    for(UINT face=plan.firstBaseFace;face<plan.firstBaseFace+plan.baseFaces;++face){
        // E40540 first push: RGBA black plus independent WORK20 depth.
        // Face-camera158 owns reverse depth; Main3E4 is not its operand.
        const float clear[4]{};context->ClearRenderTargetView(owner.work.surfaces[face*owner.levels].Get(),clear);
        context->ClearDepthStencilView(owner.workDsv.Get(),D3D11_CLEAR_DEPTH,owner.faceDepthClear[face],0);
        if(face==3)continue;
        if(owner.starOwner->ownerPresent()) {
            selectNativeAmbientState(context,owner,owner.starBlend.Get());targetNativeAmbient(context,owner,owner.work,face,0,true);
            if(owner.starOwner->executeWorkFace(context,owner.starCameras.faces[face],owner.width,owner.width,*owner.starPower))
                ++owner.draws.stars;
        }
        ID3D11Buffer* vb=nullptr;UINT stride=0,offset=0;
        if(owner.skyEnabled) {
            selectNativeAmbient(context,owner,owner.sky,owner.skyBlend.Get());targetNativeAmbient(context,owner,owner.work,face,0,true);texturesNativeAmbient(context,owner,"main-sky");
            auto vertex=owner.skyFaces[face].constants.vs.Get(),pixel=owner.skyFaces[face].constants.ps.Get();
            context->VSSetConstantBuffers(owner.skyLayout.vertex.slot,1,&vertex);context->PSSetConstantBuffers(owner.skyLayout.pixel.slot,1,&pixel);
            vb=owner.quad.Get();stride=8;offset=0;context->IASetVertexBuffers(0,1,&vb,&stride,&offset);
            context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP);context->Draw(4,0);++owner.draws.sky;
        }
        selectNativeAmbient(context,owner,owner.clouds,owner.cloudBlend.Get());targetNativeAmbient(context,owner,owner.work,face,0,true);texturesNativeAmbient(context,owner,"clouds");
        constantsNativeAmbient(context,owner.cloudConstants[face]);vb=nullptr;stride=offset=0;context->IASetVertexBuffers(0,1,&vb,&stride,&offset);
        context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP);context->Draw(4,0);++owner.draws.clouds;
    }
    auto cubeDraw=[&](unsigned operation,AmbientNativeCube& destination,UINT face,UINT mip,
        ID3D11ShaderResourceView* input,const source_camera::V4& info,
        ID3D11ShaderResourceView* directions=nullptr,ID3D11ShaderResourceView* weights=nullptr){
        auto& program=owner.materialPrograms[operation];
        selectNativeAmbient(context,owner,program.shaders,owner.replace.Get());targetNativeAmbient(context,owner,destination,face,mip);
        San14::Render::AmbientMaterialState material;material.mipInfo=info;
        publishNativeAmbientMaterial(context,program,material,{input,nullptr,directions,weights},{owner.cubeSampler.Get(),nullptr});
        ID3D11Buffer* vb=nullptr;UINT stride=0,offset=0;
        context->IASetVertexBuffers(0,1,&vb,&stride,&offset);context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP);
    };
    if(plan.copy)for(UINT face=0;face<6;++face)for(UINT mip=0;mip<owner.levels;++mip){
        const float extent=float(mip?std::max(1u,owner.width>>mip):owner.width);
        cubeDraw(0,mip==0?output:owner.work,face,mip,
            mip==0?owner.work.view.Get():owner.work.singleMips[mip-1].Get(),{extent,0,0,float(face)});
        context->Draw(3,0);++owner.draws.copy;}
    for(UINT flat=plan.firstFilter;flat<plan.firstFilter+plan.filterFaces;++flat){UINT face=flat%6,mip=flat/6;
        auto& sample=owner.filterSamples[mip];
        cubeDraw(1,output,face,mip,owner.work.view.Get(),{float(owner.width),sample.weightSum,0,float(face)},
            sample.directionView.Get(),sample.weightView.Get());
        context->Draw(3,0);++owner.draws.filter;}
    if(plan.completed)owner.completed.at(plan.completedIndex)=true;cleanupNativeAmbient(context);return plan.units;
}
static UINT executeNativeAmbientBlend(ID3D11DeviceContext* context,NativeAmbient& owner,UINT nextGeneration,float fraction) {
    UINT first=(nextGeneration+1)%3,second=(nextGeneration+2)%3;auto& program=owner.materialPrograms[2];
    for(UINT face=0;face<6;++face)for(UINT mip=0;mip<owner.levels;++mip){
        selectNativeAmbient(context,owner,program.shaders,owner.replace.Get());targetNativeAmbient(context,owner,owner.outputs[3],face,mip);
        San14::Render::AmbientMaterialState material;material.clipToWorld=owner.blendCameras[face];material.interpolation={fraction,0,0,0};
        publishNativeAmbientMaterial(context,program,material,{owner.outputs[first].view.Get(),owner.outputs[second].view.Get(),nullptr,nullptr},
            {owner.blendMipSamplers[first][mip].Get(),owner.blendMipSamplers[second][mip].Get()});
        ID3D11Buffer* vb=nullptr;UINT stride=0,offset=0;context->IASetVertexBuffers(0,1,&vb,&stride,&offset);
        context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP);context->Draw(4,0);++owner.draws.blend;
    }
    owner.completed[3]=owner.completed[first]&&owner.completed[second];cleanupNativeAmbient(context);return 6;
}
static ID3D11ShaderResourceView* executeNativeAmbientBatch(ID3D11DeviceContext* context,NativeAmbient& owner,UINT destination=0) {
    // Explicit finite backend batch, not a native node5 clock/initialization receipt.
    AmbientGenerationProgress initial;initial.generation=destination;
    auto plan=planNativeAmbientWork(initial,0,1,ambient_schedule::workUnits);
    executeNativeAmbientWork(context,owner,plan);return owner.outputs.at(destination).view.Get();
}
static ID3D11ShaderResourceView* executeNativeAmbientNode5(ID3D11DeviceContext* context,NativeAmbient& owner,
    float currentCommonClock,bool allowsWorkInitialization) {
    const auto update=owner.generation.prepare(currentCommonClock,allowsWorkInitialization);
    if(update.enabled) {
        // Prepare has actually created all four typed top-level WORK inputs.
        // Initialization is recorded before clearing the two internal cubes:
        // snapshot then working cube, mip-major then face. External outputs,
        // the color target and depth target are not initialized by this step.
        if(owner.generation.needsInitialization()){
            owner.generation.recordInitialization();
            const float zero[4]{};
            for(auto* cube:{&owner.lightSnapshot,&owner.work})for(UINT mip=0;mip<owner.levels;++mip)for(UINT face=0;face<6;++face)
                context->ClearRenderTargetView(cube->surfaces[face*owner.levels+mip].Get(),zero);
        }
        const auto& plan=update.work;
        executeNativeAmbientWork(context,owner,plan);
        if(plan.blend)executeNativeAmbientBlend(context,owner,plan.next.generation,plan.blendFraction);
    }
    owner.generation.complete(update);
    // Source selection is not a claim that a fresh selected slot has pixels.
    // Completed output contents are tracked separately; no substitute is selected.
    return owner.selectedView();
}
