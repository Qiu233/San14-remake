#define NOMINMAX
#include "UiEffectApi.h"
#include "Interop.h"
#include <d3dcompiler.h>
#include <wrl/client.h>
#include <array>
#include <vector>
#include <map>
#include <memory>
#include <string>
#include <stdexcept>
#include <cstring>
#include <algorithm>
#include <functional>
#include "../Device/DepthState.h"
using Microsoft::WRL::ComPtr;
static void check(HRESULT value,const char* operation) {if(FAILED(value))throw std::runtime_error(operation);}
#include "../Passes/Effects/UiEffectRenderer.h"

namespace {
thread_local std::string uiError;
struct UiOwner {
    San14::Render::D3D11InteropOwner interop;
    current_effect::UiRenderer renderer;
    std::uint64_t requests{};
    UINT width,height,selector;
    UiOwner(ID3D12Device* device,ID3D12CommandQueue* queue,UINT width,UINT height,UINT selector)
        :interop(device,queue),renderer(interop.device(),width,height,selector),width(width),height(height),selector(selector) {}
    ~UiOwner() {try{interop.drain();}catch(HRESULT){} }
    void next(ID3D12Resource* output,const San14CurrentEffectInputV1& input,bool reverseDepth,
        San14::Render::OutputUse outputUse,UINT paneAlpha,San14UiEffectReceiptV1& receipt) {
        try {
            renderer.draw(interop.device(),interop.context(),input,reverseDepth,paneAlpha);
            copy(output,outputUse,input.sequence,receipt);
        }catch(...) {
            try{interop.drain();}catch(HRESULT){}
            throw;
        }
    }
    void copy(ID3D12Resource* output,San14::Render::OutputUse use,std::uint64_t sequence,San14UiEffectReceiptV1& receipt) {
        interop.prepareOutput(output,use);interop.copyFinal(renderer.texture());
        receipt={++requests,sequence,renderer.drawCount(),width,height,selector};
    }
};
void error() {
    try{throw;}catch(const std::exception& e){uiError=e.what();}
    catch(HRESULT code){uiError="UI Effect D3D HRESULT "+std::to_string(static_cast<unsigned>(code));}
    catch(...){uiError="UI Effect native failure";}
}
}
void* __cdecl san14_ui_effect_create_v1(void* device,void* queue,void* output,
        std::uint32_t width,std::uint32_t height,std::uint32_t selector,San14UiEffectReceiptV1* receipt) {
    try {
        if(!device||!queue||!output||!receipt)throw std::invalid_argument("UI Effect create input absent");
        auto owner=std::make_unique<UiOwner>(static_cast<ID3D12Device*>(device),static_cast<ID3D12CommandQueue*>(queue),width,height,selector);
        owner->renderer.clear(owner->interop.context());owner->renderer.present(owner->interop.context());
        owner->copy(static_cast<ID3D12Resource*>(output),San14::Render::OutputUse::RenderTarget,0,*receipt);
        uiError.clear();return owner.release();
    }catch(...){error();return nullptr;}
}
int __cdecl san14_ui_effect_next_v1(void* value,void* output,const San14CurrentEffectInputV1* input,
        std::uint32_t reverseDepth,San14UiEffectReceiptV1* receipt) {
    try {
        if(!value||!output||!input||!receipt||reverseDepth>1)throw std::invalid_argument("UI Effect next input absent");
        auto& owner=*static_cast<UiOwner*>(value);
        owner.next(static_cast<ID3D12Resource*>(output),*input,reverseDepth!=0,San14::Render::OutputUse::ShaderRead,255,*receipt);
        uiError.clear();return 0;
    }catch(...){error();return -1;}
}
int __cdecl san14_ui_effect_next_v3(void* value,void* output,const San14CurrentEffectInputV1* input,
        std::uint32_t reverseDepth,std::uint32_t outputUse,std::uint32_t paneAlpha,San14UiEffectReceiptV1* receipt) {
    try {
        if(!value||!output||!input||!receipt||reverseDepth>1||outputUse>1||paneAlpha==0||paneAlpha>255)
            throw std::invalid_argument("UI Effect next state/input absent");
        auto& owner=*static_cast<UiOwner*>(value);
        owner.next(static_cast<ID3D12Resource*>(output),*input,reverseDepth!=0,
            static_cast<San14::Render::OutputUse>(outputUse),paneAlpha,*receipt);
        uiError.clear();return 0;
    }catch(...){error();return -1;}
}
void __cdecl san14_ui_effect_destroy_v1(void* value) {delete static_cast<UiOwner*>(value);}
const char* __cdecl san14_ui_effect_error_v1() {return uiError.c_str();}
