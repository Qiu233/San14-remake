// Include after source-hdr-native.h. Only the common camera writer group;
// other fields remain with their original preparation/selection owners.
#pragma once
#include "DeferredCameraFrame.h"

inline void updateNativeDeferredCameraGpu(ID3D11DeviceContext* context,
    const NativeDeferredCameraProfile& profile,const NativeDeferredCameraFields& current,
    NativeSourceHdrPasses& passes) {
    for(auto& pass:passes.passes) {
        const auto& layout=profile.constants.at(pass.identity);
        for(auto& constant:pass.constants)if(!constant.vertex&&constant.slot==0) {
            applyNativeDeferredCameraConstant(layout,current,constant.bytes);
            context->UpdateSubresource(constant.buffer.Get(),0,nullptr,constant.bytes.data(),0,0);
        }
    }
}
