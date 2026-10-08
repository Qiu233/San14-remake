#pragma once
// Include after source-hdr-native.h; no source I/O or new GPU objects.
#include "current-season-native.h"
#include <algorithm>

inline void updateNativeCurrentSeasonHdr(ID3D11DeviceContext* context,
    const source_current_season::Frame& current,NativeSourceHdrPasses& passes){
    for(auto& pass:passes.passes){
        auto source=std::find_if(current.hdr.begin(),current.hdr.end(),
            [&](const auto& value){return value.identity==pass.identity;});
        if(source==current.hdr.end())continue;
        auto constant=std::find_if(pass.constants.begin(),pass.constants.end(),
            [&](const auto& value){return !value.vertex&&value.slot==source->slot;});
        if(constant==pass.constants.end())throw std::runtime_error("Current HDR actual PS slot absent");
        source_current_season::publishHdr(*source,constant->bytes);
        context->UpdateSubresource(constant->buffer.Get(),0,nullptr,constant->bytes.data(),0,0);
    }
}
