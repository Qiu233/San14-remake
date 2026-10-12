// Cloud device inputs come from the prepared renderer and target declarations.
// The session retains animation phase, ping surfaces and camera history.
#pragma once
#include <map>
#include "CloudWeatherInputs.h"
#include "CloudProgramInputs.h"
struct NativeMainCloudSurface {
    ComPtr<ID3D11Texture2D> texture;
    ComPtr<ID3D11ShaderResourceView> view;
    ComPtr<ID3D11RenderTargetView> target;
    DXGI_FORMAT sampled=DXGI_FORMAT_UNKNOWN;
};
struct NativeMainCloudSampler {
    UINT slot=0;
    std::array<ComPtr<ID3D11SamplerState>,2> sides;
};
struct NativeMainCloudPass {
    std::string name;
    UINT combination=0,vsIndex=0,psIndex=0,width=0,height=0,rtCount=0,withDsv=0;
    ComPtr<ID3D11VertexShader> vs;
    ComPtr<ID3D11PixelShader> ps;
    ComPtr<ID3D11Buffer> vcb,pcb;
    std::vector<char> vbytes,pbytes;
    std::vector<std::pair<UINT,std::string>> inputs,outputs;
    std::vector<NativeMainCloudSampler> samplers;
    ComPtr<ID3D11DepthStencilState> depth;
    ComPtr<ID3D11RasterizerState> raster;
    ComPtr<ID3D11BlendState> blend;
    D3D11_DEPTH_STENCIL_DESC depthDescription{};
    D3D11_RASTERIZER_DESC rasterDescription{};
    D3D11_BLEND_DESC blendDescription{};
};
struct NativeMainCloudAxis {
    UINT sourcePhase=0;
    std::optional<source_current_cloud_weather::AnimationChannel> weatherChannel;
    int cbOffset=-1;
    float wind=0,period=0,position=0,scale=0,phase=0;
};
struct NativeMainCloud {
    // Names retain physical source ping0/ping1 identities. Dynamic current /
    // previous roles are resolved from C4 while all three passes execute.
    std::map<std::string,NativeMainCloudSurface> surfaces;
    std::vector<NativeMainCloudPass> passes;
    std::vector<AmbientNativeTextureBinding> staticTextures;
    ComPtr<ID3D11RenderTargetView> sceneTarget;
    std::vector<NativeMainCloudAxis> axes;
    std::array<float,3> baseWind{},basePeriod{},pendingVector{};
    std::array<char,64> currentW2P{},previousW2P{};
    UINT topology=0,first=0,count=0,currentSide=0,lastWrittenSide=0,bayerCounter=0;
    UINT bayerVsOffset=0,bayerPsOffset=0,previousMatrixOffset=0,previousVectorOffset=0;
    float lastUpdateClock=0,cloudy=0;
    bool historiesNew=true,previousCloudPositive=false;
    const NativeMainCloudSurface& lastRgb() const {return surfaces.at(lastWrittenSide?"previous-rgb":"current-rgb");}
    const NativeMainCloudSurface& lastAlpha() const {return surfaces.at(lastWrittenSide?"previous-alpha":"current-alpha");}
};
static float nativeMainCloudRemainder(float value,float period) {
    // Same finite float-input/double reduction as1191B40's native fmod and
    // the existing source CPU producer; retain the sign of exact zero.
    double x=std::fabs(double(value)),y=period;
    if(x==y)return std::copysign(0.f,value);
    if(x<y)return value;
    uint64_t xb,yb;std::memcpy(&xb,&x,8);std::memcpy(&yb,&y,8);
    int a=int((xb>>52)&2047),b=int((yb>>52)&2047),blocks=a>b?(a-b)/24:0;
    double scaled=std::ldexp(y,blocks*24);
    for(int n=0;n<blocks;++n){x=x-double(int(x/scaled))*scaled;scaled=std::ldexp(scaled,-24);}
    x=x-double(int(x/scaled))*scaled;
    if(value<0)x=-x;
    return float(x);
}
static void nativeMainCloudFloat(std::vector<char>& bytes,UINT offset,float value) {std::memcpy(bytes.data()+offset,&value,4);}
static ComPtr<ID3D11Buffer> nativeMainCloudBuffer(ID3D11Device* device,const std::vector<char>& bytes) {
    D3D11_BUFFER_DESC d{};d.ByteWidth=UINT(bytes.size());d.Usage=D3D11_USAGE_DEFAULT;d.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
    D3D11_SUBRESOURCE_DATA initial{};initial.pSysMem=bytes.data();ComPtr<ID3D11Buffer> result;
    check(device->CreateBuffer(&d,&initial,&result),"Main Cloud retained constant buffer");return result;
}
static NativeMainCloud prepareNativeMainCloud(ID3D11Device* device,const fs::path& source,
    const fs::path& continuousSource,ID3D11Texture2D* hdr) {
    NativeMainCloud result;
    std::ifstream targets(source/"targets.txt");std::string name,key;UINT width,height,resource,sampled,rendered;
    if(!targets)throw std::runtime_error("Main Cloud source targets absent");
    while(targets>>name>>key>>width>>height>>resource>>sampled>>rendered) {
        NativeMainCloudSurface surface;surface.sampled=DXGI_FORMAT(sampled);
        D3D11_TEXTURE2D_DESC td{};td.Width=width;td.Height=height;td.MipLevels=td.ArraySize=1;
        td.Format=DXGI_FORMAT(resource);td.SampleDesc.Count=1;td.BindFlags=D3D11_BIND_SHADER_RESOURCE|D3D11_BIND_RENDER_TARGET;
        check(device->CreateTexture2D(&td,nullptr,&surface.texture),"Main Cloud source target");
        D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=DXGI_FORMAT(sampled);sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sd.Texture2D.MipLevels=1;
        check(device->CreateShaderResourceView(surface.texture.Get(),&sd,&surface.view),"Main Cloud source SRV");
        D3D11_RENDER_TARGET_VIEW_DESC rd{};rd.Format=DXGI_FORMAT(rendered);rd.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D;
        check(device->CreateRenderTargetView(surface.texture.Get(),&rd,&surface.target),"Main Cloud source RTV");
        result.surfaces.emplace(name,std::move(surface));
    }
    result.staticTextures=ambientTextures(device,source,"pass1-textures.tsv",std::nullopt);
    D3D11_TEXTURE2D_DESC hdrDesc{};hdr->GetDesc(&hdrDesc);
    D3D11_RENDER_TARGET_VIEW_DESC sceneDesc{};sceneDesc.Format=hdrDesc.Format;sceneDesc.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D;
    check(device->CreateRenderTargetView(hdr,&sceneDesc,&result.sceneTarget),"Main Cloud retained Scene RTV");
    std::string geometryKind;std::ifstream geometry(source/"geometry.txt");geometry>>geometryKind>>result.topology>>result.first>>result.count;
    if(!geometry||geometryKind!="SV_VertexID")throw std::runtime_error("Main Cloud source geometry absent");
    std::ifstream passRows(source/"passes.txt");NativeMainCloudPass p;
    const auto programs=source_cloud_programs::read(source/"programs.txt");
    std::map<UINT,ComPtr<ID3D11VertexShader>> vertexPrograms;
    std::map<UINT,ComPtr<ID3D11PixelShader>> pixelPrograms;
    if(!passRows)throw std::runtime_error("Main Cloud source pass list absent");
    // Program selection and render target preparation have separate owners.
    while(passRows>>p.name>>p.width>>p.height>>p.rtCount>>p.withDsv) {
        const auto& program=programs.at(p.name);
        p.combination=program.combination;p.vsIndex=program.vertex;p.psIndex=program.pixel;
        auto foundVs=vertexPrograms.find(p.vsIndex);
        if(foundVs==vertexPrograms.end()) {
            auto vb=load(source/(p.name+"-vs.dxbc"));
            check(device->CreateVertexShader(vb.data(),vb.size(),nullptr,&p.vs),"Main Cloud original VS");
            vertexPrograms.emplace(p.vsIndex,p.vs);
        } else p.vs=foundVs->second;
        auto foundPs=pixelPrograms.find(p.psIndex);
        if(foundPs==pixelPrograms.end()) {
            auto pb=load(source/(p.name+"-ps.dxbc"));
            check(device->CreatePixelShader(pb.data(),pb.size(),nullptr,&p.ps),"Main Cloud original PS");
            pixelPrograms.emplace(p.psIndex,p.ps);
        } else p.ps=foundPs->second;
        p.vbytes=load(source/(p.name+"-vs-b0.bin"));
        if(p.vbytes.size()!=program.vertexConstantBytes)throw std::runtime_error("Cloud VS constants differ from selected RDEF");
        p.vcb=nativeMainCloudBuffer(device,p.vbytes);
        if(program.pixelConstantBytes) {
            p.pbytes=load(source/(p.name+"-ps-b0.bin"));
            if(p.pbytes.size()!=program.pixelConstantBytes)throw std::runtime_error("Cloud PS constants differ from selected RDEF");
            p.pcb=nativeMainCloudBuffer(device,p.pbytes);
        }
        std::ifstream textureRows(source/"textures.txt");std::string rowPass,role,file;UINT slot;
        while(textureRows>>rowPass>>slot>>role)if(rowPass==p.name)p.inputs.emplace_back(slot,role);
        std::ifstream rtvRows(source/"rtvs.txt");while(rtvRows>>rowPass>>slot>>role)if(rowPass==p.name)p.outputs.emplace_back(slot,role);
        std::ifstream samplerRows(source/"samplers.txt");std::string oppositeFile;
        while(samplerRows>>rowPass>>slot>>file>>oppositeFile)if(rowPass==p.name) {
            NativeMainCloudSampler sampler;sampler.slot=slot;
            sampler.sides[0]=ambientSampler(device,source/file);
            sampler.sides[1]=file==oppositeFile?sampler.sides[0]:ambientSampler(device,source/oppositeFile);
            p.samplers.push_back(std::move(sampler));
        }
        result.passes.push_back(std::move(p));p=NativeMainCloudPass{};
    }
    std::ifstream animation(continuousSource/"animation.txt");NativeMainCloudAxis axis;
    if(!animation)throw std::runtime_error("Main Cloud retained animation source absent");
    while(animation>>axis.sourcePhase>>axis.cbOffset>>axis.wind>>axis.period>>axis.position>>axis.scale){
        axis.weatherChannel=source_current_cloud_weather::decodeAnimationChannel(axis.sourcePhase);
        result.axes.push_back(axis);axis=NativeMainCloudAxis{};}
    std::ifstream pending(continuousSource/"pending-vector.txt");
    for(UINT i=0;i<3;++i)pending>>result.baseWind[i]>>result.basePeriod[i];
    if(!pending)throw std::runtime_error("Main Cloud displacement source absent");
    std::ifstream layout(continuousSource/"cb-layout.txt");std::string stage,field;UINT offset;
    if(!layout)throw std::runtime_error("Main Cloud CB layout source absent");
    while(layout>>stage>>field>>offset) {
        if(field=="previous_w2p")result.previousMatrixOffset=offset;
        else if(field=="previous_vector")result.previousVectorOffset=offset;
        else if(stage=="pass1-vs")result.bayerVsOffset=offset;
        else if(stage=="pass2-ps")result.bayerPsOffset=offset;
    }
    auto matrix=load(continuousSource/"current-w2p.bin");
    if(matrix.size()!=64)throw std::runtime_error("Main Cloud actual W2P is not64B");
    std::memcpy(result.currentW2P.data(),matrix.data(),64);
    for(auto& pass:result.passes)if(pass.name=="pass1")std::memcpy(result.previousW2P.data(),pass.pbytes.data()+result.previousMatrixOffset,64);
    auto cloud=load(continuousSource/"cloudy.bin");
    if(cloud.size()!=4)throw std::runtime_error("Main Cloud current Cloudy source is not4B");
    std::memcpy(&result.cloudy,cloud.data(),4);
    return result;
}
static NativeMainCloudSurface& nativeMainCloudRole(NativeMainCloud& cloud,const std::string& role) {
    if(cloud.currentSide==0)return cloud.surfaces.at(role);
    if(role=="current-rgb")return cloud.surfaces.at("previous-rgb");
    if(role=="previous-rgb")return cloud.surfaces.at("current-rgb");
    if(role=="current-alpha")return cloud.surfaces.at("previous-alpha");
    if(role=="previous-alpha")return cloud.surfaces.at("current-alpha");
    return cloud.surfaces.at(role);
}
static float advanceNativeMainCloud(NativeMainCloud& cloud,float currentCommonClock) {
    float delta=currentCommonClock-cloud.lastUpdateClock;cloud.lastUpdateClock=currentCommonClock;
    for(auto& a:cloud.axes)a.phase=nativeMainCloudRemainder(a.phase-delta*a.wind,a.period);
    for(UINT i=0;i<3;++i)cloud.pendingVector[i]=nativeMainCloudRemainder(cloud.pendingVector[i]+delta*cloud.baseWind[i],cloud.basePeriod[i]);
    return delta; // Original node3 delta also feeds the Star geometry+34 writer.
}
static UINT consumeNativeMainCloud(NativeMainCloud& cloud,NativeMainCloudPass& pass) {
    UINT usedBayer=cloud.bayerCounter;
    std::memcpy(pass.vbytes.data()+cloud.bayerVsOffset,&usedBayer,4);
    std::memcpy(pass.pbytes.data()+cloud.previousMatrixOffset,cloud.previousW2P.data(),64);
    for(auto& a:cloud.axes)if(a.cbOffset>=0)nativeMainCloudFloat(pass.pbytes,UINT(a.cbOffset),(a.phase+a.position)*a.scale);
    for(auto& next:cloud.passes)if(next.name=="pass2") {
        std::memcpy(next.pbytes.data()+cloud.bayerPsOffset,&usedBayer,4);
        for(UINT i=0;i<3;++i)nativeMainCloudFloat(next.pbytes,cloud.previousVectorOffset+i*4,cloud.pendingVector[i]);
        nativeMainCloudFloat(next.pbytes,cloud.previousVectorOffset+12,0);
    }
    cloud.pendingVector.fill(0);cloud.bayerCounter=usedBayer==15?0:usedBayer+1;
    return usedBayer;
}
static bool prepareNativeMainCloudHistory(ID3D11DeviceContext* context,NativeMainCloud& cloud,UINT actualQ7F8) {
    // History preparation observes Cloudy before positive cloud drawing.
    // Zero frames still reach this resource preparation.
    const bool positive=cloud.cloudy>0;
    if(cloud.historiesNew||(!cloud.previousCloudPositive&&positive)||(actualQ7F8&8)!=0) {
        const float zero[4]{};
        for(const char* role:{"current-rgb","previous-rgb","current-alpha","previous-alpha"})context->ClearRenderTargetView(cloud.surfaces.at(role).target.Get(),zero);
        cloud.historiesNew=false;
    }
    cloud.previousCloudPositive=positive;
    return positive;
}
static void executeNativeMainCloud(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical,
    NativeMainCloud& cloud,ID3D11DepthStencilView* mainDsv,float currentCommonClock,bool drawEnabled) {
    // Node3 already advanced owned CC/phase/pending at the Main entry.
    // This consumer never advances time itself. Only positive cloud drawing is
    // gated; the unconditional ping-side exit also follows a skipped draw.
    UINT usedBayer=cloud.bayerCounter;
    if(drawEnabled)for(auto& pass:cloud.passes) {
        if(pass.name=="pass1") {
            //1191FE0 emits old DWORD counter, consumes pending, then advances.
            usedBayer=consumeNativeMainCloud(cloud,pass);
            context->UpdateSubresource(pass.vcb.Get(),0,nullptr,pass.vbytes.data(),0,0);
            context->UpdateSubresource(pass.pcb.Get(),0,nullptr,pass.pbytes.data(),0,0);
        } else if(pass.name=="pass2")context->UpdateSubresource(pass.pcb.Get(),0,nullptr,pass.pbytes.data(),0,0);
        ComPtr<ID3D11RasterizerState> entryRaster;context->RSGetState(&entryRaster);
        ComPtr<ID3D11DepthStencilState> entryDepth;UINT entryRef;context->OMGetDepthStencilState(&entryDepth,&entryRef);
        const auto entryDepthDescription=logical.description;
        ComPtr<ID3D11BlendState> entryBlend;float entryFactor[4];UINT entrySampleMask;context->OMGetBlendState(&entryBlend,entryFactor,&entrySampleMask);
        D3D11_VIEWPORT entryViewport{};UINT viewportCount=1;context->RSGetViewports(&viewportCount,&entryViewport);
        std::array<ID3D11ShaderResourceView*,32> inputs{};context->OMSetRenderTargets(0,nullptr,nullptr);context->PSSetShaderResources(0,UINT(inputs.size()),inputs.data());
        for(auto& input:pass.inputs)inputs.at(input.first)=nativeMainCloudRole(cloud,input.second).view.Get();
        for(auto& texture:cloud.staticTextures)if(texture.program==pass.name)inputs.at(texture.slot)=texture.view.Get();
        context->PSSetShaderResources(0,UINT(inputs.size()),inputs.data());
        // The optional ambient-altitude role must not inherit a prior draw's
        // sampler when the selected three-image Plane does not provide it.
        ID3D11SamplerState* absentSampler=nullptr;context->PSSetSamplers(4,1,&absentSampler);
        // Samplers follow the same physical ping side as their texture views.
        // Static inputs share one state across both sides.
        for(auto& sampler:pass.samplers){auto raw=sampler.sides[cloud.currentSide].Get();context->PSSetSamplers(sampler.slot,1,&raw);}
        auto vcb=pass.vcb.Get(),pcb=pass.pcb.Get();context->VSSetConstantBuffers(0,1,&vcb);context->PSSetConstantBuffers(0,1,&pcb);
        std::array<ID3D11RenderTargetView*,8> output{};
        for(auto& target:pass.outputs)output.at(target.first)=target.second=="current-scene"?cloud.sceneTarget.Get():nativeMainCloudRole(cloud,target.second).target.Get();
        D3D11_DEPTH_STENCIL_DESC ds=entryDepthDescription;ds.DepthEnable=pass.name!="pass1";ds.DepthWriteMask=D3D11_DEPTH_WRITE_MASK_ZERO;
        D3D11_RASTERIZER_DESC rs{};rs.FillMode=D3D11_FILL_SOLID;rs.DepthClipEnable=TRUE;if(entryRaster)entryRaster->GetDesc(&rs);rs.CullMode=D3D11_CULL_NONE;rs.ScissorEnable=FALSE;
        D3D11_BLEND_DESC bd{};if(entryBlend)entryBlend->GetDesc(&bd);else bd.RenderTarget[0].RenderTargetWriteMask=15;
        bd.IndependentBlendEnable=TRUE;
        for(UINT i=0;i<pass.rtCount;++i){auto& b=bd.RenderTarget[i];b.RenderTargetWriteMask=bd.RenderTarget[0].RenderTargetWriteMask;b.BlendEnable=pass.name=="pass4";
            if(b.BlendEnable){b.SrcBlend=b.SrcBlendAlpha=D3D11_BLEND_ONE;b.DestBlend=b.DestBlendAlpha=D3D11_BLEND_INV_SRC_ALPHA;b.BlendOp=b.BlendOpAlpha=D3D11_BLEND_OP_ADD;}}
        // Reuse device states on the normal unchanged entry. Descriptor changes
        // preserve actual source inheritance without reparsing device inputs.
        if(!pass.depth||std::memcmp(&ds,&pass.depthDescription,sizeof(ds))!=0){pass.depth.Reset();check(device->CreateDepthStencilState(&ds,&pass.depth),"Main Cloud retained temporary depth");pass.depthDescription=ds;}
        if(!pass.raster||std::memcmp(&rs,&pass.rasterDescription,sizeof(rs))!=0){pass.raster.Reset();check(device->CreateRasterizerState(&rs,&pass.raster),"Main Cloud retained temporary raster");pass.rasterDescription=rs;}
        if(!pass.blend||std::memcmp(&bd,&pass.blendDescription,sizeof(bd))!=0){pass.blend.Reset();check(device->CreateBlendState(&bd,&pass.blend),"Main Cloud retained temporary blend");pass.blendDescription=bd;}
        D3D11_VIEWPORT viewport{0,0,float(pass.width),float(pass.height),0,1};context->RSSetViewports(1,&viewport);context->RSSetState(pass.raster.Get());
        logical.bind(context,pass.depth.Get(),ds,entryRef);context->OMSetBlendState(pass.blend.Get(),entryFactor,entrySampleMask);
        context->IASetInputLayout(nullptr);context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY(cloud.topology));
        context->VSSetShader(pass.vs.Get(),nullptr,0);context->PSSetShader(pass.ps.Get(),nullptr,0);context->GSSetShader(nullptr,nullptr,0);context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);
        context->OMSetRenderTargets(pass.rtCount,output.data(),pass.withDsv?mainDsv:nullptr);context->Draw(cloud.count,cloud.first);
        if(pass.name=="pass1")cloud.previousW2P=cloud.currentW2P; //119269C..26DC after the draw, for the next request.
        context->OMSetRenderTargets(0,nullptr,nullptr);inputs.fill(nullptr);context->PSSetShaderResources(0,UINT(inputs.size()),inputs.data());
        context->RSSetViewports(1,&entryViewport);context->RSSetState(entryRaster.Get());logical.bind(context,entryDepth.Get(),entryDepthDescription,entryRef);
        context->OMSetBlendState(entryBlend.Get(),entryFactor,entrySampleMask);
        std::cout<<"source MainCloud124 "<<pass.name<<" combo="<<pass.combination<<" VS="<<pass.vsIndex<<" PS="<<pass.psIndex<<" draw="<<cloud.count
            <<" extent="<<pass.width<<'x'<<pass.height<<" MRT="<<pass.rtCount<<" MainDSV="<<pass.withDsv<<" inheritedStencil="<<entryRef
            <<" historySide="<<cloud.currentSide<<" bayer="<<usedBayer<<" clock="<<currentCommonClock<<"\n";
    }
    cloud.lastWrittenSide=cloud.currentSide;
    cloud.currentSide=(cloud.currentSide-1)&1; // EB5C66/6A/6D normal node exit.
    auto currentScene=cloud.sceneTarget.Get();context->OMSetRenderTargets(1,&currentScene,mainDsv);
}
