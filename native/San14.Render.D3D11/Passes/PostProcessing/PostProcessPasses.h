// One-time objects/parsed arguments for the finite original normal post recipe.
// Include after the native lab's load/check helpers. Resources and execution
// belong to the caller; this does not allocate frame surfaces or submit work.
#pragma once
#include "../../Runtime/PreparedFileInput.h"
#include <map>
#include <sstream>

struct NativeNormalPostConstantBinding {
    std::string stage;
    UINT slot{};
    ComPtr<ID3D11Buffer> buffer; // DEFAULT; caller may UpdateSubresource later.
};
struct NativeNormalPostSrvBinding {
    std::string stage;
    UINT slot{};
    std::string name;
    UINT mip{};
};
struct NativeNormalPostUavBinding {
    UINT slot{};
    std::string name;
};
struct NativeNormalPostTargetBinding {
    UINT slot{};
    std::string name;
    UINT mip{};
};
struct NativeNormalPostSamplerBinding {
    UINT slot{};
    ComPtr<ID3D11SamplerState> sampler; // Existing graphics recipe binds PS.
};
struct NativeNormalPostPass {
    std::string name;
    bool compute{};
    ComPtr<ID3D11ComputeShader> cs;
    ComPtr<ID3D11VertexShader> vs;
    ComPtr<ID3D11PixelShader> ps;
    std::vector<NativeNormalPostConstantBinding> constants;
    std::vector<NativeNormalPostSrvBinding> srvs;
    std::vector<NativeNormalPostUavBinding> uavs;
    std::vector<NativeNormalPostTargetBinding> targets;
    UINT width{},height{}; // Graphics target0 extent; unused for compute.
    D3D11_VIEWPORT viewport{};
    D3D11_RECT scissor{};
    ComPtr<ID3D11RasterizerState> raster;
    ComPtr<ID3D11BlendState> blend;
    UINT depthTest{},depthWrite{};
    std::vector<NativeNormalPostSamplerBinding> samplers;
    UINT dispatchX{},dispatchY{},dispatchZ{};
    UINT vertexCount{},instanceCount{},startVertex{},startInstance{};
    bool indirect{};
    UINT index{},offset{},stagingSide{}; // Source indirect buffer index/byte offset.
    D3D11_PRIMITIVE_TOPOLOGY topology{D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST};
    bool clear{}; // Execute after target bind on every reached call, not at load.
    UINT clearMask{},clearPacked{}; // Current source scatter recipe:15/RGBA0.
};

static std::vector<NativeNormalPostPass> loadNativeNormalPostPasses(
    ID3D11Device* device,const fs::path& source,UINT width,UINT height) {
    // Same file identity shares objects. No per-request hashes or fingerprints.
    auto identity=[](const fs::path& file){return fs::absolute(file).lexically_normal().string();};
    std::map<std::string,ComPtr<ID3D11Buffer>> constants;
    std::map<std::string,ComPtr<ID3D11ComputeShader>> computeShaders;
    std::map<std::string,ComPtr<ID3D11VertexShader>> vertexShaders;
    std::map<std::string,ComPtr<ID3D11PixelShader>> pixelShaders;
    std::map<std::string,ComPtr<ID3D11SamplerState>> samplers;
    std::map<std::string,std::pair<UINT,UINT>> targetExtents{
        {"SceneColor",{width,height}},{"Tone108",{width,height}},{"Final109",{width,height}}};

    // Only the already-validated target identities used by this finite recipe.
    UINT bloomIndex,bloomWidth,bloomHeight,bloomMips,bloomResource,bloomSrv,bloomRtv;
    NativePreparedInputFile bloom(source/"bloom.txt");
    if(!(bloom>>bloomIndex>>bloomWidth>>bloomHeight>>bloomMips>>bloomResource>>bloomSrv>>bloomRtv)||bloomIndex!=85)
        throw std::runtime_error("Normal post physical Bloom85 descriptor absent");
    targetExtents.emplace("Bloom85",std::make_pair(bloomWidth,bloomHeight));
    NativePreparedInputFile ordered(source/"ordered-required-color-passes.txt");
    if(!ordered)throw std::runtime_error("Normal post ordered source passes absent");
    std::vector<NativeNormalPostPass> passes;
    std::string entry;
    while(std::getline(ordered,entry)) {
        if(!entry.empty()&&entry.back()=='\r')entry.pop_back();
        if(entry.empty())continue;
        NativeNormalPostPass pass;pass.name=entry;
        const fs::path directory=source/entry;
        pass.compute=fs::exists(nativePreparedFilePath(directory/"compute.dxbc"));
        const bool dof=entry.find("-dof")!=std::string::npos;
        std::string stage,file;UINT slot;
        NativePreparedInputFile cbRows(directory/"cbuffers.txt");
        while(cbRows>>stage>>slot>>file) {
            if(stage!="VS"&&stage!="PS"&&stage!="CS")
                throw std::runtime_error("Normal post constant buffer stage invalid");
            const fs::path path=directory/file;
            const auto key=identity(path);
            auto found=constants.find(key);
            if(found==constants.end()) {
                auto bytes=load(path);
                if(bytes.empty()||bytes.size()%16!=0||bytes.size()>D3D11_REQ_CONSTANT_BUFFER_ELEMENT_COUNT*16u)
                    throw std::runtime_error("Normal post source CB byte size invalid");
                D3D11_BUFFER_DESC desc{};desc.ByteWidth=UINT(bytes.size());
                desc.Usage=D3D11_USAGE_DEFAULT;desc.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
                D3D11_SUBRESOURCE_DATA initial{};initial.pSysMem=bytes.data();
                ComPtr<ID3D11Buffer> object;
                check(device->CreateBuffer(&desc,&initial,&object),"normal post source DEFAULT CB");
                found=constants.emplace(key,object).first;
            }
            pass.constants.push_back({stage,slot,found->second});
        }
        if(fs::exists(nativePreparedFilePath(directory/"views.txt"))) {
            NativePreparedInputFile rows(directory/"views.txt");std::string name;int mip,last;
            while(rows>>stage>>slot>>name>>mip>>last) {
                if(stage!="VS"&&stage!="PS"&&stage!="CS")
                    throw std::runtime_error("Normal post SRV shader stage invalid");
                // The existing resource helper resolves one-mip views and
                // normalizes source -1 to0; the source last field is not used.
                pass.srvs.push_back({stage,slot,name,UINT(std::max(0,mip))});
            }
        } else {
            NativePreparedInputFile rows(directory/"textures.txt");std::string name;
            while(rows>>slot>>name)pass.srvs.push_back({pass.compute?"CS":"PS",slot,name,0});
        }
        if(pass.compute) {
            NativePreparedInputFile rows(directory/"uavs.txt");std::string name;
            while(rows>>slot>>name)pass.uavs.push_back({slot,name});
            const fs::path path=directory/"compute.dxbc";
            const auto key=identity(path);auto found=computeShaders.find(key);
            if(found==computeShaders.end()) {
                auto code=load(path);ComPtr<ID3D11ComputeShader> object;
                check(device->CreateComputeShader(code.data(),code.size(),nullptr,&object),"normal post original compute shader");
                found=computeShaders.emplace(key,object).first;
            }
            pass.cs=found->second;
            NativePreparedInputFile row(directory/"dispatch.txt");
            if(!(row>>pass.dispatchX>>pass.dispatchY>>pass.dispatchZ)||!pass.dispatchX||!pass.dispatchY||!pass.dispatchZ)
                throw std::runtime_error("Normal post source dispatch absent");
        } else {
            const fs::path vp=directory/"vertex.dxbc",pp=directory/"pixel.dxbc";
            auto vs=vertexShaders.find(identity(vp));
            if(vs==vertexShaders.end()) {
                auto code=load(vp);ComPtr<ID3D11VertexShader> object;
                check(device->CreateVertexShader(code.data(),code.size(),nullptr,&object),"normal post original VS");
                vs=vertexShaders.emplace(identity(vp),object).first;
            }
            auto ps=pixelShaders.find(identity(pp));
            if(ps==pixelShaders.end()) {
                auto code=load(pp);ComPtr<ID3D11PixelShader> object;
                check(device->CreatePixelShader(code.data(),code.size(),nullptr,&object),"normal post original PS");
                ps=pixelShaders.emplace(identity(pp),object).first;
            }
            pass.vs=vs->second;pass.ps=ps->second;
            UINT targetCount=0;pass.width=width;pass.height=height;
            if(dof) {
                NativePreparedInputFile rows(directory/"targets.txt");std::string name;
                UINT mip,resource,sampled,rendered,w,h;
                while(rows>>slot>>name>>mip>>resource>>sampled>>rendered>>w>>h) {
                    if(slot>=D3D11_SIMULTANEOUS_RENDER_TARGET_COUNT)
                        throw std::runtime_error("Normal post MRT slot outside D3D range");
                    pass.targets.push_back({slot,name,mip});targetCount=std::max(targetCount,slot+1);
                    if(slot==0){pass.width=w;pass.height=h;}
                }
            } else {
                std::string name;UINT mip=0;NativePreparedInputFile row(directory/"target-view.txt");
                if(!(row>>name>>mip))name=entry=="108-tone"?"Tone108":entry=="109-fxaa"?"Final109":"";
                if(name.empty())throw std::runtime_error("Normal post explicit target view absent");
                auto extent=targetExtents.at(name);
                pass.targets.push_back({0,name,mip});targetCount=1;
                pass.width=std::max(1u,extent.first>>mip);pass.height=std::max(1u,extent.second>>mip);
            }
            if(!targetCount)throw std::runtime_error("Normal post source graphics pass has no target");
            NativePreparedInputFile clear(directory/"clear.txt");
            if(clear>>pass.clearMask>>pass.clearPacked) {
                if(pass.clearMask!=15||pass.clearPacked!=0)
                    throw std::runtime_error("Normal post source clear differs from current scatter recipe");
                pass.clear=true;
            }
            D3D11_BLEND_DESC blend{};blend.IndependentBlendEnable=targetCount>1;
            NativePreparedInputFile blendRows(directory/"blend.txt");
            UINT enabled,srcColor,dstColor,colorOp,srcAlpha,dstAlpha,alphaOp,mask,n=0;
            while(blendRows>>enabled>>srcColor>>dstColor>>colorOp>>srcAlpha>>dstAlpha>>alphaOp>>mask) {
                if(n>=targetCount)throw std::runtime_error("Normal post blend target count differs");
                auto& row=blend.RenderTarget[n++];row.BlendEnable=enabled!=0;
                row.SrcBlend=D3D11_BLEND(srcColor);row.DestBlend=D3D11_BLEND(dstColor);row.BlendOp=D3D11_BLEND_OP(colorOp);
                row.SrcBlendAlpha=D3D11_BLEND(srcAlpha);row.DestBlendAlpha=D3D11_BLEND(dstAlpha);row.BlendOpAlpha=D3D11_BLEND_OP(alphaOp);
                row.RenderTargetWriteMask=BYTE(mask);
            }
            if(n==0) {
                auto& row=blend.RenderTarget[0];row.SrcBlend=row.SrcBlendAlpha=D3D11_BLEND_ONE;
                row.DestBlend=row.DestBlendAlpha=D3D11_BLEND_ZERO;row.BlendOp=row.BlendOpAlpha=D3D11_BLEND_OP_ADD;
                row.RenderTargetWriteMask=15;
            }
            check(device->CreateBlendState(&blend,&pass.blend),"normal post source effective blend");
            UINT cull,blendFlag,colorMask;NativePreparedInputFile rasterRow(directory/"raster.txt");
            if(!(rasterRow>>cull>>pass.depthTest>>pass.depthWrite>>blendFlag>>colorMask))
                throw std::runtime_error("Normal post raster recipe absent");
            UINT scissorEnabled=1;
            pass.scissor={0,0,LONG(pass.width),LONG(pass.height)};
            NativePreparedInputFile scissorRow(directory/"scissor.txt");
            if(scissorRow)scissorRow>>scissorEnabled>>pass.scissor.left>>pass.scissor.top>>pass.scissor.right>>pass.scissor.bottom;
            D3D11_RASTERIZER_DESC raster{};raster.FillMode=D3D11_FILL_SOLID;raster.CullMode=D3D11_CULL_MODE(cull);
            raster.DepthClipEnable=TRUE;raster.ScissorEnable=scissorEnabled!=0;
            check(device->CreateRasterizerState(&raster,&pass.raster),"normal post source fullscreen raster");
            pass.viewport={0,0,float(pass.width),float(pass.height),0,1};
            NativePreparedInputFile viewportRow(directory/"viewport.txt");
            if(viewportRow)viewportRow>>pass.viewport.TopLeftX>>pass.viewport.TopLeftY>>pass.viewport.Width>>pass.viewport.Height>>pass.viewport.MinDepth>>pass.viewport.MaxDepth;
            NativePreparedInputFile samplerRows(directory/"samplers.txt");std::string line;
            while(std::getline(samplerRows,line)) {
                std::istringstream row(line);if(!(row>>slot))continue;
                const fs::path path=directory/("sampler"+std::to_string(slot)+".bin");
                const auto key=identity(path);auto found=samplers.find(key);
                if(found==samplers.end()) {
                    auto bytes=load(path);
                    if(bytes.size()!=sizeof(D3D11_SAMPLER_DESC))throw std::runtime_error("Normal post original sampler needs52B");
                    D3D11_SAMPLER_DESC desc{};std::memcpy(&desc,bytes.data(),sizeof(desc));
                    ComPtr<ID3D11SamplerState> object;
                    check(device->CreateSamplerState(&desc,&object),"normal post actual source sampler");
                    found=samplers.emplace(key,object).first;
                }
                pass.samplers.push_back({slot,found->second});
            }
            pass.indirect=fs::exists(nativePreparedFilePath(directory/"indirect.txt"));
            if(pass.indirect) {
                UINT topology;std::string method;
                NativePreparedInputFile row(directory/"indirect.txt"),methodRow(directory/"indirect-method.txt");
                if(!(row>>pass.index>>pass.offset>>pass.stagingSide)||!(methodRow>>method>>topology)||
                    method!="DrawInstancedIndirect"||pass.stagingSide>=2)
                    throw std::runtime_error("Normal post original indirect recipe missing");
                pass.topology=D3D11_PRIMITIVE_TOPOLOGY(topology);
            } else {
                NativePreparedInputFile row(directory/"draw.txt");
                if(!(row>>pass.vertexCount>>pass.instanceCount>>pass.startVertex>>pass.startInstance))
                    throw std::runtime_error("Normal post source Draw command absent");
            }
        }
        passes.push_back(std::move(pass));
    }
    return passes;
}
