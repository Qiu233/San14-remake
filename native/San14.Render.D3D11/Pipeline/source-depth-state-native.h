// Source CPU state survives disabled hardware fields. D3D11 GetDesc may
// canonicalize ignored depth/stencil fields, so it is not the source state.
struct NativeSourceDepthState {
    D3D11_DEPTH_STENCIL_DESC description;
    UINT reference;
    void bind(ID3D11DeviceContext* context,ID3D11DepthStencilState* hardware,
        const D3D11_DEPTH_STENCIL_DESC& requested,UINT requestedReference) {
        description=requested;reference=requestedReference;
        context->OMSetDepthStencilState(hardware,requestedReference);
    }
};
