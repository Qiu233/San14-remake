// D33's source state effects remain observable when its special tail is empty.
// The retained phase owner publishes traversal changes independently of the tail.
struct NativeSpecialEffects {
    bool tailOwnerAvailable{true};
    UINT hostFlags,alphaTest;
    std::map<UINT,UINT> references,rows;
    UINT depthCompare,alternateDepthCompare,stencilCompare,readMask,writeMask;
    D3D11_DEPTH_STENCILOP_DESC operations;
};
static NativeSpecialEffects createNativeSpecialEffectsWithoutParticleOwner(UINT actualHostFlags) {
    NativeSpecialEffects out{};
    out.hostFlags=actualHostFlags;
    out.tailOwnerAvailable=false;
    return out;
}
static void applyNativeSpecialEffects(ID3D11Device* device,ID3D11DeviceContext* context,
    NativeSourceDepthState& logical,NativeSpecialEffects& source,San14::Render::MainRenderPhaseState& phase,UINT node) {
    phase.publishSpecialTail(); // Before owner and population tests.
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
    std::cout<<"source specialized Effect"<<node<<" tail=0 draws=0 Q="<<std::hex<<phase.deferredQueueFlags()<<std::dec
        <<" restoredTest="<<exit.DepthEnable<<" compare="<<exit.DepthFunc<<" stencilOff; Keep/Keep/Replace persists\n";
}
