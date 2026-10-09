// Original Effect2 consumer. CPU writers have already emitted the one sorted
// shared stream; this native experiment only submits its original GPU ABI.
struct NativeEffectPass {
    ComPtr<ID3D11VertexShader> vertex;
    ComPtr<ID3D11PixelShader> pixel;
    ComPtr<ID3D11InputLayout> layout;
    struct Constant { bool vertex; UINT slot; ComPtr<ID3D11Buffer> buffer; };
    std::vector<Constant> constants;
};
struct NativeEffectDraw {
    UINT pass,offset,count,start;INT base;
    std::string texture;
};
struct NativeEffectInputs {
    std::map<UINT,NativeEffectPass> passes;
    std::vector<NativeEffectDraw> draws;
    ComPtr<ID3D11Buffer> dynamic,meshVertices,meshIndices;
    NativeEffectProviders providers;
    ComPtr<ID3D11Texture2D> exposure;
    ComPtr<ID3D11ShaderResourceView> exposureView;
    ComPtr<ID3D11SamplerState> depthSampler;
    D3D11_BLEND_DESC blend;
};
static NativeEffectInputs loadNativeEffectInputs(ID3D11Device* device,const fs::path& source) {
    NativeEffectInputs out;
    const auto stream=load(source/"dynamic-stream.bin");
    out.dynamic=buffer(device,stream,D3D11_BIND_VERTEX_BUFFER);
    out.meshVertices=buffer(device,load(source/"g1em-mesh2-vb.bin"),D3D11_BIND_VERTEX_BUFFER);
    out.meshIndices=buffer(device,load(source/"g1em-mesh2-ib.bin"),D3D11_BIND_INDEX_BUFFER);
    out.providers=loadNativeEffectProviders(device,source.parent_path().parent_path()/"effect-native-provider-input");
    auto blendBytes=load(source/"blend-desc.bin");
    if(blendBytes.size()!=sizeof(out.blend))throw std::runtime_error("Original Effect blend descriptor size");
    std::memcpy(&out.blend,blendBytes.data(),sizeof(out.blend));
    auto samplerBytes=load(source/"target-E13DDB4F-sampler-desc.bin");D3D11_SAMPLER_DESC sampler;
    if(samplerBytes.size()!=sizeof(sampler))throw std::runtime_error("Original Effect MainD1 sampler size");
    std::memcpy(&sampler,samplerBytes.data(),sizeof(sampler));
    check(device->CreateSamplerState(&sampler,&out.depthSampler),"Effect original MainD1 sampler");
    auto exposure=load(source/"auto-exposure-one.bin");
    D3D11_TEXTURE2D_DESC td{};td.Width=td.Height=td.ArraySize=td.MipLevels=1;
    td.Format=DXGI_FORMAT_R32_TYPELESS;td.SampleDesc.Count=1;td.Usage=D3D11_USAGE_IMMUTABLE;td.BindFlags=D3D11_BIND_SHADER_RESOURCE;
    D3D11_SUBRESOURCE_DATA data{exposure.data(),4,4};
    check(device->CreateTexture2D(&td,&data,&out.exposure),"Effect original t25 manager texture");
    D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=DXGI_FORMAT_R32_FLOAT;sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sd.Texture2D.MipLevels=1;
    check(device->CreateShaderResourceView(out.exposure.Get(),&sd,&out.exposureView),"Effect original t25 manager view");
    for(UINT index:{7u,22u,125u}) {
        NativeEffectPass pass;const auto directory=source/("pass-"+std::to_string(index));
        auto vs=load(directory/"vs.dxbc"),ps=load(directory/"ps.dxbc");
        check(device->CreateVertexShader(vs.data(),vs.size(),nullptr,&pass.vertex),"Effect original VS");
        check(device->CreatePixelShader(ps.data(),ps.size(),nullptr,&pass.pixel),"Effect original PS");
        struct Element { std::string semantic;UINT index,format,slot,offset,classification,step; };
        std::vector<Element> fields;std::ifstream layout(directory/"layout.txt");Element field;
        while(layout>>field.semantic>>field.index>>field.format>>field.slot>>field.offset>>field.classification>>field.step)fields.push_back(field);
        if(fields.empty())throw std::runtime_error("Original Effect input layout absent");
        std::vector<D3D11_INPUT_ELEMENT_DESC> elements;
        for(const auto& e:fields)elements.push_back({e.semantic.c_str(),e.index,DXGI_FORMAT(e.format),e.slot,e.offset,D3D11_INPUT_CLASSIFICATION(e.classification),e.step});
        check(device->CreateInputLayout(elements.data(),UINT(elements.size()),vs.data(),vs.size(),&pass.layout),"Effect original physical input layout");
        std::ifstream constants(directory/"cbuffers.txt");std::string stage,file;UINT slot;
        while(constants>>stage>>slot>>file)pass.constants.push_back({stage=="vs",slot,buffer(device,load(directory/file),D3D11_BIND_CONSTANT_BUFFER)});
        if(pass.constants.empty())throw std::runtime_error("Original Effect constants absent");
        out.passes.emplace(index,std::move(pass));
    }
    std::ifstream rows(source/"draws.tsv");std::string line,family,command;UINT ordinal,slot,stride;
    if(!rows)throw std::runtime_error("Original Effect queue absent");
    while(std::getline(rows,line)) {
        if(line.empty()||line[0]=='#')continue;
        std::istringstream row(line);NativeEffectDraw draw;
        row>>ordinal>>draw.pass>>family>>slot>>stride>>draw.offset>>command>>draw.count>>draw.start>>draw.base>>draw.texture;
        if(!row)throw std::runtime_error("Incomplete original Effect draw row");
        const bool mesh=draw.pass==125;
        if((mesh&&(command!="DrawIndexed"||slot!=1||stride!=64))||
           (!mesh&&(command!="Draw"||slot!=0||stride!=32))||
           draw.offset+(mesh?stride:stride*draw.count)>stream.size())throw std::runtime_error("Original Effect stream bounds/ABI");
        (void)out.passes.at(draw.pass);(void)out.providers.textures.at(draw.texture);
        out.draws.push_back(std::move(draw));
    }
    std::cout<<"source Effect consumer original pipelines="<<out.passes.size()<<" sorted draws="<<out.draws.size()<<" streamBytes="<<stream.size()<<'\n';
    return out;
}
static void drawNativeEffects188(ID3D11Device* device,ID3D11DeviceContext* context,
    NativeSourceDepthState& logical,const NativeEffectInputs& input,
    ID3D11ShaderResourceView* depth,ID3D11ShaderResourceView* accl) {
    // AA16 does not change Test/Compare/Stencil or component masks. D32 and
    // graph scopes supply them. Source AA0AA0 restores only its saved fields.
    const auto entry=logical;
    ComPtr<ID3D11BlendState> entryBlend;FLOAT entryFactor[4];UINT entryMask;
    context->OMGetBlendState(&entryBlend,entryFactor,&entryMask);
    ComPtr<ID3D11RasterizerState> entryRaster;context->RSGetState(&entryRaster);
    D3D11_RASTERIZER_DESC rs{};rs.FillMode=D3D11_FILL_SOLID;rs.DepthClipEnable=TRUE;
    if(entryRaster)entryRaster->GetDesc(&rs);rs.CullMode=D3D11_CULL_NONE;
    ComPtr<ID3D11RasterizerState> raster;check(device->CreateRasterizerState(&rs,&raster),"Effect AA16 CullNONE");
    auto ds=logical.description;ds.DepthWriteMask=D3D11_DEPTH_WRITE_MASK_ZERO;
    ComPtr<ID3D11DepthStencilState> state;check(device->CreateDepthStencilState(&ds,&state),"Effect AA16 inherited logical depth/stencil");
    D3D11_BLEND_DESC bd=input.blend;
    // Keep each inherited enable/mask except AA16's RT0 enable mutation.
    // Recipe factors come from original Main Common3BC, not inactive GetDesc.
    if(entryBlend) {
        D3D11_BLEND_DESC inherited;entryBlend->GetDesc(&inherited);
        for(UINT i=0;i<8;++i){bd.RenderTarget[i].RenderTargetWriteMask=inherited.RenderTarget[i].RenderTargetWriteMask;
            if(i)bd.RenderTarget[i].BlendEnable=inherited.RenderTarget[i].BlendEnable;}
    }
    ComPtr<ID3D11BlendState> blend;check(device->CreateBlendState(&bd,&blend),"Effect original Main AA16 recipe");
    context->OMSetBlendState(blend.Get(),entryFactor,entryMask);logical.bind(context,state.Get(),ds,logical.reference);
    context->RSSetState(raster.Get());context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP);
    context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);context->GSSetShader(nullptr,nullptr,0);
    std::array<ID3D11ShaderResourceView*,32> views{};views[25]=input.exposureView.Get();views[27]=accl;
    UINT activePass=0,sprites=0,meshes=0;
    for(const auto& draw:input.draws) {
        const auto& texture=input.providers.textures.at(draw.texture);
        if(activePass!=draw.pass) {
            const auto& pass=input.passes.at(draw.pass);
            context->VSSetShader(pass.vertex.Get(),nullptr,0);context->PSSetShader(pass.pixel.Get(),nullptr,0);
            context->IASetInputLayout(pass.layout.Get());
            for(const auto& cb:pass.constants){auto raw=cb.buffer.Get();if(cb.vertex)context->VSSetConstantBuffers(cb.slot,1,&raw);else context->PSSetConstantBuffers(cb.slot,1,&raw);}
            views[3]=draw.pass==7?nullptr:depth;activePass=draw.pass;
            auto raw=input.depthSampler.Get();context->PSSetSamplers(3,1,&raw);
        }
        views[0]=texture.view.Get();context->PSSetShaderResources(0,UINT(views.size()),views.data());
        auto sampler=texture.sampler.Get();context->PSSetSamplers(0,1,&sampler);
        if(draw.pass==125) {
            ID3D11Buffer* vertices[]{input.meshVertices.Get(),input.dynamic.Get()};UINT strides[]{24,64},offsets[]{0,draw.offset};
            context->IASetVertexBuffers(0,2,vertices,strides,offsets);
            context->IASetIndexBuffer(input.meshIndices.Get(),DXGI_FORMAT_R16_UINT,0);
            context->DrawIndexed(draw.count,draw.start,draw.base);++meshes;
        } else {
            auto vertices=input.dynamic.Get();UINT stride=32,offset=0;
            context->IASetVertexBuffers(0,1,&vertices,&stride,&offset);
            context->Draw(draw.count,draw.offset/stride);++sprites;
        }
    }
    views.fill(nullptr);context->PSSetShaderResources(0,UINT(views.size()),views.data());
    auto exit=logical.description;exit.DepthWriteMask=entry.description.DepthWriteMask;
    ComPtr<ID3D11DepthStencilState> exitState;check(device->CreateDepthStencilState(&exit,&exitState),"Effect AA0AA0 restore saved write");
    logical.bind(context,exitState.Get(),exit,logical.reference);
    context->OMSetBlendState(entryBlend.Get(),entryFactor,entryMask);context->RSSetState(entryRaster.Get());
    ID3D11Buffer* noVertices=nullptr;UINT zero=0;
    context->IASetVertexBuffers(0,1,&noVertices,&zero,&zero);
    std::cout<<"source Effects188 draws="<<input.draws.size()<<" sprite="<<sprites<<" mesh="<<meshes<<" original joint order; AA0AA0 saved fields restored\n";
}
