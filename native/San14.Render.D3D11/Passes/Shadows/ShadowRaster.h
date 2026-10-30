// CE D43190 / 10575E0 / 95EB60: queue-forced caster state, not
// the ordinary primitive raster. Source packets are imported once.
#pragma once
#include "../../Runtime/PreparedFileInput.h"
struct NativeShadowRasterSource {
    unsigned selector{}, common3F3{}, passCount{};
    float scale{};
    std::array<float,4> bias{};
    std::array<D3D11_RASTERIZER_DESC,4> passes{};
};
inline NativeShadowRasterSource loadNativeShadowRasterSource(const fs::path& directory) {
    NativeShadowRasterSource out;
    NativePreparedInputFile inputs(directory/"inputs.txt");
    inputs>>out.selector>>out.common3F3>>out.scale;
    for(auto& value:out.bias)inputs>>value;
    if(!inputs||out.selector>1||out.common3F3!=0)
        throw std::runtime_error("Current caster needs its source bias producer");
    NativePreparedInputFile rows(directory/"raster.txt");
    unsigned pass,fill,cull,front,clip,scissor,multi,aa;
    int expectedBias;float expectedSlope,clamp;
    while(rows>>pass>>expectedBias>>expectedSlope>>clamp>>fill>>cull>>front>>clip>>scissor>>multi>>aa) {
        auto& raster=out.passes.at(pass);
        raster.FillMode=D3D11_FILL_MODE(fill);raster.CullMode=D3D11_CULL_MODE(cull);
        raster.FrontCounterClockwise=front;raster.DepthClipEnable=clip;
        raster.ScissorEnable=scissor;raster.MultisampleEnable=multi;raster.AntialiasedLineEnable=aa;
        // sbias.x=5A4*.5, 9B4BF0 doubles it; native format4E uses 2^24,
        // float32 multiplication then truncation, even though EA's DSV is D32.
        const float sbiasX=out.scale*.5f;
        raster.SlopeScaledDepthBias=sbiasX+sbiasX;
        raster.DepthBias=int(out.bias.at(out.selector==0?0:pass)*16777216.f);
        raster.DepthBiasClamp=clamp;
        if(raster.DepthBias!=expectedBias||raster.SlopeScaledDepthBias!=expectedSlope)
            throw std::runtime_error("Caster bias differs from imported source conversion");
        ++out.passCount;
    }
    if(!out.passCount)throw std::runtime_error("Source caster raster packet absent");
    return out;
}
struct NativeShadowRasterGpu {
    std::array<NativeShadowRasterSource,2> source;
    std::array<std::array<ComPtr<ID3D11RasterizerState>,4>,2> passes;
    ComPtr<ID3D11RasterizerState> exit;
    D3D11_RASTERIZER_DESC exitDescription{};
};
// D43190 applies the selected seasonal Camera first, then the Main caller's
// overrides. The current cascade count can differ from the startup family.
inline void setNativeShadowRasterBias(NativeShadowRasterSource& source,unsigned selector,
    const std::array<float,4>& bias,unsigned count) {
    if(selector>1||!count||count>source.passes.size()||source.common3F3!=0)
        throw std::runtime_error("Current shadow raster requires its supported bias source branch");
    for(unsigned pass=source.passCount;pass<count;++pass)source.passes[pass]=source.passes[0];
    source.selector=selector;source.bias=bias;source.passCount=count;
    for(unsigned pass=0;pass<count;++pass)
        source.passes[pass].DepthBias=int(bias[selector==0?0:pass]*16777216.f);
}
inline void updateNativeShadowRasterGpu(ID3D11DeviceContext* context,NativeShadowRasterGpu& gpu,
    unsigned family,const NativeShadowRasterSource& source) {
    auto& prior=gpu.source.at(family);
    ComPtr<ID3D11Device> device;
    for(unsigned pass=0;pass<source.passCount;++pass) {
        auto& state=gpu.passes.at(family).at(pass);
        if(!state||prior.passes[pass].DepthBias!=source.passes[pass].DepthBias) {
            if(!device)context->GetDevice(&device);
            ComPtr<ID3D11RasterizerState> next;
            check(device->CreateRasterizerState(&source.passes[pass],&next),"current seasonal caster bias");
            state=std::move(next);
        }
    }
    prior=source;
}
inline NativeShadowRasterGpu prepareNativeShadowRasterGpu(ID3D11Device* device,
        std::array<NativeShadowRasterSource,2> source) {
    NativeShadowRasterGpu out;out.source=std::move(source);
    for(unsigned mode=0;mode<out.source.size();++mode)
        for(unsigned pass=0;pass<out.source[mode].passCount;++pass)
            check(device->CreateRasterizerState(&out.source[mode].passes[pass],&out.passes[mode][pass]),
                "original forced caster raster and per-pass bias");
    // Current source entry has Frontend1DB=0. v70 restores that disabled
    // bias, clears scissor, retains queue Cull/front/clip; EA6470 restores fill.
    out.exitDescription=out.source[0].passes[0];
    out.exitDescription.DepthBias=0;out.exitDescription.SlopeScaledDepthBias=0;
    out.exitDescription.ScissorEnable=FALSE;
    check(device->CreateRasterizerState(&out.exitDescription,&out.exit),"source caster exit raster");
    return out;
}
inline void retainNativeShadowRasterExit(ID3D11DeviceContext* context,NativeShadowRasterGpu& gpu) {
    ComPtr<ID3D11RasterizerState> entry;context->RSGetState(&entry);
    D3D11_RASTERIZER_DESC desc{};entry->GetDesc(&desc);
    if(desc.FillMode!=gpu.exitDescription.FillMode||
       desc.MultisampleEnable!=gpu.exitDescription.MultisampleEnable||
       desc.AntialiasedLineEnable!=gpu.exitDescription.AntialiasedLineEnable) {
        gpu.exitDescription.FillMode=desc.FillMode;
        gpu.exitDescription.MultisampleEnable=desc.MultisampleEnable;
        gpu.exitDescription.AntialiasedLineEnable=desc.AntialiasedLineEnable;
        ComPtr<ID3D11Device> device;context->GetDevice(&device);gpu.exit.Reset();
        check(device->CreateRasterizerState(&gpu.exitDescription,&gpu.exit),"current source caster exit fill");
    }
}
