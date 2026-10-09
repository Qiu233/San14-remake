// Specific original exposure102/103/104 -> tone108 -> FXAA109 lab slice.
// Include after terrain.cpp's load/check/buffer/pixelBytes/decode helpers.
// No product owner, arbitrary replay graph, replacement shader or image output.
#pragma once
#include <iomanip>
#include <limits>

struct PostNativeTarget {
    ComPtr<ID3D11Texture2D> texture;
    ComPtr<ID3D11RenderTargetView> target;
    ComPtr<ID3D11ShaderResourceView> view;
    DXGI_FORMAT sampledFormat{};
};
static PostNativeTarget postNativeTarget(ID3D11Device* device,const fs::path& directory,UINT width,UINT height) {
    UINT resource,shader,render,w,h;
    std::ifstream row(directory/"target.txt");
    if(!(row>>resource>>shader>>render>>w>>h)||w!=width||h!=height)
        throw std::runtime_error("Post source target extent differs from this fixed export");
    D3D11_TEXTURE2D_DESC td{};td.Width=width;td.Height=height;td.ArraySize=td.MipLevels=1;
    td.Format=DXGI_FORMAT(resource);td.SampleDesc.Count=1;td.Usage=D3D11_USAGE_DEFAULT;
    td.BindFlags=D3D11_BIND_RENDER_TARGET|D3D11_BIND_SHADER_RESOURCE;
    PostNativeTarget result;result.sampledFormat=DXGI_FORMAT(shader);
    check(device->CreateTexture2D(&td,nullptr,&result.texture),"post original target resource");
    D3D11_RENDER_TARGET_VIEW_DESC rd{};rd.Format=DXGI_FORMAT(render);rd.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D;
    check(device->CreateRenderTargetView(result.texture.Get(),&rd,&result.target),"post original target RTV");
    D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=DXGI_FORMAT(shader);sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;
    sd.Texture2D.MipLevels=1;
    check(device->CreateShaderResourceView(result.texture.Get(),&sd,&result.view),"post original target SRV");
    return result;
}
static std::vector<char> postNativeReadback(ID3D11Device* device,ID3D11DeviceContext* context,
    ID3D11Texture2D* actual,DXGI_FORMAT viewFormat) {
    D3D11_TEXTURE2D_DESC td{};actual->GetDesc(&td);
    UINT rowBytes=td.Width*pixelBytes(viewFormat);
    td.Usage=D3D11_USAGE_STAGING;td.BindFlags=td.MiscFlags=0;td.CPUAccessFlags=D3D11_CPU_ACCESS_READ;
    ComPtr<ID3D11Texture2D> staging;check(device->CreateTexture2D(&td,nullptr,&staging),"post readback staging");
    context->CopyResource(staging.Get(),actual);
    D3D11_MAPPED_SUBRESOURCE mapped{};check(context->Map(staging.Get(),0,D3D11_MAP_READ,0,&mapped),"post native readback");
    std::vector<char> bytes(size_t(rowBytes)*td.Height);
    for(UINT y=0;y<td.Height;++y)
        std::memcpy(bytes.data()+size_t(y)*rowBytes,static_cast<const char*>(mapped.pData)+size_t(y)*mapped.RowPitch,rowBytes);
    context->Unmap(staging.Get(),0);return bytes;
}
static void postNativeWriteBytes(const fs::path& path,const std::vector<char>& bytes) {
    std::ofstream file(path,std::ios::binary);file.write(bytes.data(),std::streamsize(bytes.size()));
    if(!file)throw std::runtime_error("Cannot write native post numerical readback");
}
static void postNativeStats(std::ostream& json,const std::vector<char>& data,DXGI_FORMAT format,UINT width,UINT height) {
    std::array<double,3> total{0,0,0};std::array<float,3> low{INFINITY,INFINITY,INFINITY},high{-INFINITY,-INFINITY,-INFINITY};
    UINT nonfinite=0,stride=pixelBytes(format);
    for(size_t i=0;i<size_t(width)*height;++i) {
        auto p=decode(data.data()+i*stride,format);
        for(int c=0;c<3;++c) {
            if(!std::isfinite(p[c]))++nonfinite;
            else {total[c]+=p[c];low[c]=std::min(low[c],p[c]);high[c]=std::max(high[c],p[c]);}
        }
    }
    json<<"{\"viewFormat\":"<<UINT(format)<<",\"nonfiniteComponents\":"<<nonfinite<<",\"meanStoredRGB\":[";
    for(int c=0;c<3;++c){if(c)json<<',';json<<total[c]/(double(width)*height);}
    json<<"],\"minStoredRGB\":[";
    for(int c=0;c<3;++c){if(c)json<<',';json<<(std::isfinite(low[c])?low[c]:0);}
    json<<"],\"maxStoredRGB\":[";
    for(int c=0;c<3;++c){if(c)json<<',';json<<(std::isfinite(high[c])?high[c]:0);}
    json<<"],\"storedProbePixels\":[";
    std::array<std::array<UINT,2>,5> points{{{0,0},{width/4,height/4},{width/2,height/2},{3*width/4,3*height/4},{width-1,height-1}}};
    for(size_t n=0;n<points.size();++n) {
        if(n)json<<',';auto p=points[n];auto value=decode(data.data()+(size_t(p[1])*width+p[0])*stride,format);
        json<<"{\"x\":"<<p[0]<<",\"y\":"<<p[1]<<",\"storedRGB\":[";
        for(int c=0;c<3;++c){if(c)json<<',';if(std::isfinite(value[c]))json<<value[c];else json<<"null";}
        json<<"]}";
    }
    json<<"]}";
}

static ComPtr<ID3D11Texture2D> producePostOutput(ID3D11Device* device,ID3D11DeviceContext* context,
    const fs::path& source,const fs::path& output,ID3D11ShaderResourceView* scene,UINT width,UINT height) {
    if(!scene)throw std::runtime_error("Post slice needs actual native node73 SceneColor SRV");
    D3D11_SHADER_RESOURCE_VIEW_DESC sceneView{};scene->GetDesc(&sceneView);
    ComPtr<ID3D11Resource> sceneResource;scene->GetResource(&sceneResource);
    ComPtr<ID3D11Texture2D> sceneTexture;check(sceneResource.As(&sceneTexture),"post SceneColor texture");
    D3D11_TEXTURE2D_DESC sceneDesc{};sceneTexture->GetDesc(&sceneDesc);
    UINT sourceResource,sourceShader,sourceRender,sourceWidth,sourceHeight;
    std::ifstream sceneRow(source/"lighting-target.txt");
    if(!(sceneRow>>sourceResource>>sourceShader>>sourceRender>>sourceWidth>>sourceHeight)||
        width!=sourceWidth||height!=sourceHeight||sceneDesc.Width!=width||sceneDesc.Height!=height||
        sceneDesc.Format!=DXGI_FORMAT(sourceResource)||sceneView.Format!=DXGI_FORMAT(sourceShader)||
        sceneView.ViewDimension!=D3D11_SRV_DIMENSION_TEXTURE2D)
        throw std::runtime_error("Post actual SceneColor differs from native source node73 resource/SRV");
    fs::create_directories(output);
    context->ClearState(); // This fixed slice explicitly binds every field it consumes below.

    UINT byteCount,viewFormat,elements,binds,usage,cpuAccess,misc,stride;
    std::ifstream histRow(source/"histogram.txt");
    if(!(histRow>>byteCount>>viewFormat>>elements>>binds>>usage>>cpuAccess>>misc>>stride))
        throw std::runtime_error("Original histogram descriptor missing");
    D3D11_BUFFER_DESC hd{};hd.ByteWidth=byteCount;hd.Usage=D3D11_USAGE(usage);hd.BindFlags=binds;
    hd.CPUAccessFlags=cpuAccess;hd.MiscFlags=misc;hd.StructureByteStride=stride;
    ComPtr<ID3D11Buffer> histogram;check(device->CreateBuffer(&hd,nullptr,&histogram),"post original Histogram8");
    D3D11_UNORDERED_ACCESS_VIEW_DESC hu{};hu.Format=DXGI_FORMAT(viewFormat);hu.ViewDimension=D3D11_UAV_DIMENSION_BUFFER;hu.Buffer.NumElements=elements;
    ComPtr<ID3D11UnorderedAccessView> histogramUav;check(device->CreateUnorderedAccessView(histogram.Get(),&hu,&histogramUav),"post Histogram8 UAV");
    D3D11_SHADER_RESOURCE_VIEW_DESC hs{};hs.Format=DXGI_FORMAT(viewFormat);hs.ViewDimension=D3D11_SRV_DIMENSION_BUFFER;hs.Buffer.NumElements=elements;
    ComPtr<ID3D11ShaderResourceView> histogramView;check(device->CreateShaderResourceView(histogram.Get(),&hs,&histogramView),"post Histogram8 SRV");

    UINT er,es,eu,ew,eh;
    std::ifstream exposureRow(source/"exposure.txt");
    if(!(exposureRow>>er>>es>>eu>>ew>>eh)||ew!=2||eh!=1)
        throw std::runtime_error("Original Exposure61 needs its2x1 descriptor");
    D3D11_TEXTURE2D_DESC ed{};ed.Width=ew;ed.Height=eh;ed.MipLevels=ed.ArraySize=1;ed.SampleDesc.Count=1;
    ed.Format=DXGI_FORMAT(er);ed.Usage=D3D11_USAGE_DEFAULT;
    ed.BindFlags=D3D11_BIND_SHADER_RESOURCE|D3D11_BIND_RENDER_TARGET|D3D11_BIND_UNORDERED_ACCESS;
    // Root-authorized controlled experiment variable. Original fresh creator
    // passes nullptr initialData; these two ones are not an original-history claim.
    const std::array<float,2> previousExposure{1.f,1.f};
    D3D11_SUBRESOURCE_DATA prior{};prior.pSysMem=previousExposure.data();prior.SysMemPitch=prior.SysMemSlicePitch=8;
    ComPtr<ID3D11Texture2D> exposure;check(device->CreateTexture2D(&ed,&prior,&exposure),"post controlled previousExposure[1,1]");
    D3D11_UNORDERED_ACCESS_VIEW_DESC ev{};ev.Format=DXGI_FORMAT(eu);ev.ViewDimension=D3D11_UAV_DIMENSION_TEXTURE2D;
    ComPtr<ID3D11UnorderedAccessView> exposureUav;check(device->CreateUnorderedAccessView(exposure.Get(),&ev,&exposureUav),"post Exposure61 UAV");
    D3D11_SHADER_RESOURCE_VIEW_DESC ex{};ex.Format=DXGI_FORMAT(es);ex.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;ex.Texture2D.MipLevels=1;
    ComPtr<ID3D11ShaderResourceView> exposureView;check(device->CreateShaderResourceView(exposure.Get(),&ex,&exposureView),"post Exposure61 SRV");
    auto cb24Bytes=load(source/"103-exposure/cb24.bin");
    if(cb24Bytes.size()!=64||cb24Bytes!=load(source/"104-exposure/cb24.bin"))
        throw std::runtime_error("Post Exposure CB24 needs same complete source64B");
    auto cb24=buffer(device,cb24Bytes,D3D11_BIND_CONSTANT_BUFFER);auto raw24=cb24.Get();
    context->CSSetConstantBuffers(2,1,&raw24);
    ID3D11UnorderedAccessView* noUav=nullptr;ID3D11ShaderResourceView* noView=nullptr;
    for(UINT pass:std::array<UINT,3>{102,103,104}) {
        fs::path directory=source/(std::to_string(pass)+"-exposure");
        auto code=load(directory/"compute.dxbc");ComPtr<ID3D11ComputeShader> shader;
        check(device->CreateComputeShader(code.data(),code.size(),nullptr,&shader),"post original exposure CS");
        context->CSSetUnorderedAccessViews(0,1,&noUav,nullptr);context->CSSetShaderResources(0,1,&noView);
        auto uav=pass==104?exposureUav.Get():histogramUav.Get();
        auto srv=pass==104?histogramView.Get():pass==103?scene:nullptr;
        context->CSSetShaderResources(0,1,&srv);context->CSSetUnorderedAccessViews(0,1,&uav,nullptr);
        context->CSSetShader(shader.Get(),nullptr,0);
        UINT x,y,z;std::ifstream dispatch(directory/"dispatch.txt");
        if(!(dispatch>>x>>y>>z)||!x||!y||!z)throw std::runtime_error("Post original compute dispatch missing");
        context->Dispatch(x,y,z);
        std::cout<<"POST original pass"<<pass<<" dispatch="<<x<<','<<y<<','<<z<<'\n';
    }
    context->CSSetUnorderedAccessViews(0,1,&noUav,nullptr);context->CSSetShaderResources(0,1,&noView);
    context->CSSetShader(nullptr,nullptr,0);
    auto exposureBytes=postNativeReadback(device,context,exposure.Get(),DXGI_FORMAT(es));
    postNativeWriteBytes(output/"post-exposure104-r32f.bin",exposureBytes);
    std::array<float,2> actualExposure;std::memcpy(actualExposure.data(),exposureBytes.data(),8);

    // Exact original selected HDR system2 volume, not an altered LUT or pixels.
    std::ifstream lutFile(source/"108-tone/hdr-lut.tex",std::ios::binary);
    if(!lutFile)throw std::runtime_error("Post original HDR volume absent");
    UINT lw=u32(lutFile),lh=u32(lutFile),ld=u32(lutFile),lm=u32(lutFile),lf=u32(lutFile),lr,ls;
    std::ifstream lutFormats(source/"108-tone/hdr-resource.txt");
    if(!(lutFormats>>lr>>ls)||ls!=lf)throw std::runtime_error("Post original HDR native resource/view formats missing");
    std::vector<std::vector<char>> lutStorage(lm);std::vector<D3D11_SUBRESOURCE_DATA> lutInitial(lm);
    for(UINT mip=0;mip<lm;++mip) {
        auto& data=lutInitial[mip];data.SysMemPitch=u32(lutFile);data.SysMemSlicePitch=u32(lutFile);
        lutStorage[mip].resize(u32(lutFile));lutFile.read(lutStorage[mip].data(),std::streamsize(lutStorage[mip].size()));
        if(!lutFile)throw std::runtime_error("Truncated post original HDR subresource");data.pSysMem=lutStorage[mip].data();
    }
    D3D11_TEXTURE3D_DESC ldsc{};ldsc.Width=lw;ldsc.Height=lh;ldsc.Depth=ld;ldsc.MipLevels=lm;
    ldsc.Format=DXGI_FORMAT(lr);ldsc.Usage=D3D11_USAGE_IMMUTABLE;ldsc.BindFlags=D3D11_BIND_SHADER_RESOURCE;
    ComPtr<ID3D11Texture3D> lut;check(device->CreateTexture3D(&ldsc,lutInitial.data(),&lut),"post actual original HDR3D");
    D3D11_SHADER_RESOURCE_VIEW_DESC lsd{};lsd.Format=DXGI_FORMAT(ls);lsd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE3D;lsd.Texture3D.MipLevels=lm;
    ComPtr<ID3D11ShaderResourceView> lutView;check(device->CreateShaderResourceView(lut.Get(),&lsd,&lutView),"post original HDR3D SRV");

    auto tone=postNativeTarget(device,source/"108-tone",width,height);
    auto final=postNativeTarget(device,source/"109-fxaa",width,height);
    auto cb22Bytes=load(source/"108-tone/cb22.bin");
    if(cb22Bytes.size()!=208||cb22Bytes!=load(source/"109-fxaa/cb22.bin"))
        throw std::runtime_error("Post Composite CB22 needs same complete source208B");
    float lensGate,ldrGate;std::memcpy(&lensGate,cb22Bytes.data()+40,4);std::memcpy(&ldrGate,cb22Bytes.data()+20,4);
    if(lensGate>0||ldrGate>0)throw std::runtime_error("This post slice has no active Lens/LDR producer");
    auto cb22=buffer(device,cb22Bytes,D3D11_BIND_CONSTANT_BUFFER);auto raw22=cb22.Get();
    context->VSSetConstantBuffers(2,1,&raw22);context->PSSetConstantBuffers(2,1,&raw22);
    std::array<ComPtr<ID3D11SamplerState>,2> samplerOwners;
    for(UINT n=0;n<2;++n) {
        auto data=load(source/"108-tone"/("sampler"+std::to_string(n+7)+".bin"));
        if(data.size()!=sizeof(D3D11_SAMPLER_DESC))throw std::runtime_error("Post source sampler must be52B");
        D3D11_SAMPLER_DESC desc{};std::memcpy(&desc,data.data(),sizeof(desc));
        check(device->CreateSamplerState(&desc,&samplerOwners[n]),"post exact original manager sampler");
    }
    ID3D11SamplerState* samplerViews[]{samplerOwners[0].Get(),samplerOwners[1].Get()};context->PSSetSamplers(7,2,samplerViews);
    D3D11_RASTERIZER_DESC rs{};rs.FillMode=D3D11_FILL_SOLID;rs.CullMode=D3D11_CULL_NONE;rs.DepthClipEnable=TRUE;rs.ScissorEnable=TRUE;
    ComPtr<ID3D11RasterizerState> raster;check(device->CreateRasterizerState(&rs,&raster),"post source fullscreen raster");
    D3D11_DEPTH_STENCIL_DESC ds{};ds.DepthWriteMask=D3D11_DEPTH_WRITE_MASK_ZERO;ds.DepthFunc=D3D11_COMPARISON_ALWAYS;
    ComPtr<ID3D11DepthStencilState> depth;check(device->CreateDepthStencilState(&ds,&depth),"post source disabled depth/stencil");
    D3D11_BLEND_DESC bs{};auto& b=bs.RenderTarget[0];b.SrcBlend=b.SrcBlendAlpha=D3D11_BLEND_ONE;
    b.DestBlend=b.DestBlendAlpha=D3D11_BLEND_ZERO;b.BlendOp=b.BlendOpAlpha=D3D11_BLEND_OP_ADD;
    b.RenderTargetWriteMask=D3D11_COLOR_WRITE_ENABLE_ALL;
    ComPtr<ID3D11BlendState> blend;check(device->CreateBlendState(&bs,&blend),"post source blendOff/RGBA");
    context->RSSetState(raster.Get());context->OMSetDepthStencilState(depth.Get(),0);context->OMSetBlendState(blend.Get(),nullptr,~0u);
    D3D11_VIEWPORT viewport{0,0,float(width),float(height),0,1};D3D11_RECT scissor{0,0,LONG(width),LONG(height)};
    context->RSSetViewports(1,&viewport);context->RSSetScissorRects(1,&scissor);
    context->IASetInputLayout(nullptr);context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
    std::array<ID3D11ShaderResourceView*,5> inputs{scene,nullptr,exposureView.Get(),lutView.Get(),nullptr};
    for(UINT pass:std::array<UINT,2>{108,109}) {
        fs::path directory=source/(pass==108?"108-tone":"109-fxaa");
        auto vc=load(directory/"vertex.dxbc"),pc=load(directory/"pixel.dxbc");
        ComPtr<ID3D11VertexShader> vs;ComPtr<ID3D11PixelShader> ps;
        check(device->CreateVertexShader(vc.data(),vc.size(),nullptr,&vs),"post original explicit VS");
        check(device->CreatePixelShader(pc.data(),pc.size(),nullptr,&ps),"post original pixel shader");
        context->OMSetRenderTargets(0,nullptr,nullptr);
        std::array<ID3D11ShaderResourceView*,5> empty{};context->PSSetShaderResources(0,UINT(empty.size()),empty.data());
        if(pass==109)inputs={tone.view.Get(),nullptr,nullptr,nullptr,nullptr};
        context->PSSetShaderResources(0,UINT(inputs.size()),inputs.data());
        auto target=pass==108?tone.target.Get():final.target.Get();context->OMSetRenderTargets(1,&target,nullptr);
        context->VSSetShader(vs.Get(),nullptr,0);context->PSSetShader(ps.Get(),nullptr,0);context->Draw(3,0);
        std::cout<<"POST original pass"<<pass<<" draw=3 targetView="<<(pass==108?26:91)<<'\n';
    }
    context->OMSetRenderTargets(0,nullptr,nullptr);std::array<ID3D11ShaderResourceView*,5> none{};
    context->PSSetShaderResources(0,UINT(none.size()),none.data());
    auto toneBytes=postNativeReadback(device,context,tone.texture.Get(),tone.sampledFormat);
    auto finalBytes=postNativeReadback(device,context,final.texture.Get(),final.sampledFormat);
    postNativeWriteBytes(output/"post-tone108-r11g11b10.bin",toneBytes);
    postNativeWriteBytes(output/"post-final109-bgra8.bin",finalBytes);
    std::ofstream result(output/"post-native-result.json");result<<std::setprecision(std::numeric_limits<float>::max_digits10);
    result<<"{\n\"scope\":\"fixed native original post slice; no complete original frame or original initial exposure claim\",\n"
        <<"\"controlledPreviousExposureTexels\":[1,1],\"originalInitialDataNull\":true,\n"
        <<"\"sceneResourceFormat\":"<<UINT(sceneDesc.Format)<<",\"sceneShaderViewFormat\":"<<UINT(sceneView.Format)<<",\n"
        <<"\"width\":"<<width<<",\"height\":"<<height<<",\"actualExposure104\":[";
    for(int i=0;i<2;++i){if(i)result<<',';if(std::isfinite(actualExposure[i]))result<<actualExposure[i];else result<<"null";}
    result<<"],\n\"tone108\":";postNativeStats(result,toneBytes,tone.sampledFormat,width,height);
    result<<",\n\"final109\":";postNativeStats(result,finalBytes,final.sampledFormat,width,height);result<<"\n}\n";
    std::cout<<"POST CS104 actual exposure="<<actualExposure[0]<<','<<actualExposure[1]<<" controlledPrior=1,1\n";
    context->ClearState();
    return final.texture;
}
