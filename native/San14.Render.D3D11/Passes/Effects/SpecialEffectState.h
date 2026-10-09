// D33's source state effects remain observable when its special tail is empty.
// No guessed special shader/geometry is submitted in this finite experiment.
struct NativeSpecialEffects {
    bool tailOwnerAvailable{true};
    UINT queueFlags,hostFlags,alphaTest;
    std::map<UINT,UINT> references,rows;
    UINT depthCompare,alternateDepthCompare,stencilCompare,readMask,writeMask,qOr;
    D3D11_DEPTH_STENCILOP_DESC operations;
};
static NativeSpecialEffects createNativeSpecialEffectsWithoutParticleOwner(UINT actualHostFlags) {
    NativeSpecialEffects out{};
    out.hostFlags=actualHostFlags;
    out.qOr=0x40; // D33 publishes this before its owner/population gates.
    out.tailOwnerAvailable=false;
    return out;
}
static NativeSpecialEffects loadNativeSpecialEffects(const fs::path& source) {
    NativeSpecialEffects out{};std::ifstream flags(source/"context.txt");flags>>out.queueFlags>>out.hostFlags>>out.alphaTest;
    if(!flags)throw std::runtime_error("Source D33 context input absent");
    std::map<std::string,UINT> values;std::ifstream state(source/"stencil-sequence.txt");std::string line,key;UINT value;
    while(std::getline(state,line)) {
        if(line.empty()||line[0]=='#')continue;std::istringstream row(line);row>>key>>value;
        if(key=="SetStencilOpsD3D") {
            UINT failDepth,pass;row>>failDepth>>pass;
            out.operations.StencilFailOp=D3D11_STENCIL_OP(value);out.operations.StencilDepthFailOp=D3D11_STENCIL_OP(failDepth);out.operations.StencilPassOp=D3D11_STENCIL_OP(pass);
        } else values.emplace(key,value);
    }
    out.depthCompare=values.at("SetDepthCompareD3DForHost20000001");out.stencilCompare=values.at("SetStencilCompareD3D");
    out.alternateDepthCompare=values.at("SetDepthCompareD3DAlternate");
    out.operations.StencilFunc=D3D11_COMPARISON_FUNC(out.stencilCompare);
    out.readMask=values.at("SetStencilReadMask");out.writeMask=values.at("SetStencilWriteMask");out.qOr=values.at("PersistContextQOr");
    out.references.emplace(141,values.at("SetStencilRefNode141"));out.references.emplace(147,values.at("SetStencilRefNode147"));
    std::ifstream rows(source/"source-eligibility.txt");UINT node,type,bank,mode,count;
    while(std::getline(rows,line)){if(line.empty()||line[0]=='#')continue;std::istringstream row(line);row>>node>>type>>bank>>mode>>count;
        if(!row)throw std::runtime_error("Source specialized queue row absent");out.rows.emplace(node,count);}
    return out;
}
static void applyNativeSpecialEffects(ID3D11Device* device,ID3D11DeviceContext* context,
    NativeSourceDepthState& logical,NativeSpecialEffects& source,UINT node) {
    source.queueFlags|=source.qOr; // Source before owner and population tests.
    if(!source.tailOwnerAvailable)return;
    if(source.rows.at(node)!=0)throw std::runtime_error("Original specialized tail requires its shader/stream consumer");
    const auto entry=logical;const UINT savedAlpha=source.alphaTest;
    source.alphaTest=1;auto enter=entry.description;enter.DepthEnable=TRUE;
    enter.DepthFunc=D3D11_COMPARISON_FUNC((source.hostFlags&0x20000000u)?source.depthCompare:source.alternateDepthCompare);enter.StencilEnable=TRUE;
    enter.FrontFace=enter.BackFace=source.operations;enter.StencilReadMask=BYTE(source.readMask);enter.StencilWriteMask=BYTE(source.writeMask);
    ComPtr<ID3D11DepthStencilState> state;check(device->CreateDepthStencilState(&enter,&state),"D33 original zero-tail entry state");
    logical.bind(context,state.Get(),enter,source.references.at(node));
    // Restore compare/ref/read and write, explicitly disable stencil. The
    // original caller does not restore operations; Keep/Keep/Replace stays.
    auto exit=enter;exit.FrontFace.StencilFunc=entry.description.FrontFace.StencilFunc;
    exit.BackFace.StencilFunc=entry.description.BackFace.StencilFunc;
    exit.StencilReadMask=entry.description.StencilReadMask;exit.StencilWriteMask=entry.description.StencilWriteMask;
    exit.StencilEnable=FALSE;source.alphaTest=savedAlpha;
    exit.DepthEnable=entry.description.DepthEnable;exit.DepthFunc=entry.description.DepthFunc;
    ComPtr<ID3D11DepthStencilState> restored;check(device->CreateDepthStencilState(&exit,&restored),"D33 original zero-tail source exit");
    logical.bind(context,restored.Get(),exit,entry.reference);
    std::cout<<"source specialized Effect"<<node<<" tail=0 draws=0 Q="<<std::hex<<source.queueFlags<<std::dec
        <<" restoredTest="<<exit.DepthEnable<<" compare="<<exit.DepthFunc<<" stencilOff; Keep/Keep/Replace persists\n";
}
