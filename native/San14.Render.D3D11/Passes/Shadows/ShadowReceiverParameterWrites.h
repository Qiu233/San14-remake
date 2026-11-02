#pragma once
#include "ShadowReceiverMaterial.h"

namespace San14::Render {
// D76780 publishes requested shadow20 to Generic D4. The bool projection is
// independent of both the matched shader row and the Compact cascade count.
inline void installReceiverShadowMode(ShadowReceiverShadowValues& values,std::uint32_t requested) {
    // Fresh D4 is zero. Its unchanged setter does not invent an unwritten lane.
    if(requested==0&&!values.shadowEnabled)return;
    if(requested==0){values.shadowEnabled=std::array<std::uint32_t,3>{0,0,0};return;}
    const auto code=(requested&4u)||static_cast<std::int32_t>(requested)>7?3u:requested-1u;
    values.shadowEnabled=std::array<std::uint32_t,3>{1u,code&1u,(code>>1)&1u};
}

// Compact v90 single-atlas scSM2, after the actual scSM dimensions writer.
inline ReceiverVector prepareReceiverSecondaryAtlasScale(ReceiverVector actualAtlasScale) {
    return {source_camera::M(actualAtlasScale.X,-1.5f),source_camera::M(actualAtlasScale.Y,-1.5f),
        source_camera::M(actualAtlasScale.X,2),source_camera::M(actualAtlasScale.Y,2)};
}
}
