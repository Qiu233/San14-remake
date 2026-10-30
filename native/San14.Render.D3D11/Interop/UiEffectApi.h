#pragma once
#include "CurrentEffectInput.h"
#include <cstdint>

struct San14UiEffectReceiptV1 {
    std::uint64_t request,sequence;
    std::uint32_t drawCount,width,height,selector;
};
static_assert(sizeof(San14UiEffectReceiptV1)==32);
extern "C" {
__declspec(dllexport) void* __cdecl san14_ui_effect_create_v1(void* device,void* queue,void* output,
    std::uint32_t width,std::uint32_t height,std::uint32_t selector,San14UiEffectReceiptV1* receipt);
__declspec(dllexport) int __cdecl san14_ui_effect_next_v1(void* owner,void* output,
    const San14CurrentEffectInputV1* input,std::uint32_t reverseDepth,San14UiEffectReceiptV1* receipt);
__declspec(dllexport) int __cdecl san14_ui_effect_next_v3(void* owner,void* output,
    const San14CurrentEffectInputV1* input,std::uint32_t reverseDepth,std::uint32_t outputUse,
    std::uint32_t paneAlpha,San14UiEffectReceiptV1* receipt);
__declspec(dllexport) void __cdecl san14_ui_effect_destroy_v1(void* owner);
__declspec(dllexport) const char* __cdecl san14_ui_effect_error_v1();
}
