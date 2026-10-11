#pragma once
// Current lights are layout-independent. Each installed pixel program owns its
// imported named-field layout; camera and refraction writers remain separate.
#include "../Environment/SeasonFrame.h"

inline void updateNativeCurrentSeasonHdr(ID3D11DeviceContext* context,
    const source_current_season::Frame& current, NativeSourceHdrPasses& passes) {
    if (!current.hdrLighting) throw std::runtime_error("Current HDR lighting publication is absent");
    for (auto& pass : passes.passes) {
        if (!pass.populationGate || !pass.lightingLayout)
            throw std::runtime_error("Current HDR pass has no imported light association or layout");
        const auto& gate = *pass.populationGate;
        const auto* outer = source_current_season::firstMatching(current, gate.outerMask);
        if (!outer) throw std::runtime_error("Current HDR matching outer light is absent");
        const auto* inner = source_current_season::firstMatching(current, gate.innerBit);
        if (!inner) inner = outer;
        const auto values = San14::Render::prepareHdrLighting(outer->lighting, inner->lighting, *current.hdrLighting);
        const auto& layout = *pass.lightingLayout;
        auto& constant = pass.constants[pass.lightingConstantIndex];
        San14::Render::writeHdrLighting(layout, values, constant.bytes);
        context->UpdateSubresource(constant.buffer.Get(), 0, nullptr, constant.bytes.data(), 0, 0);
    }
}
