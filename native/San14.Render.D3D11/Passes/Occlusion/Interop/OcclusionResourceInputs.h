#pragma once
#include "../OcclusionResourceBank.h"
#include <span>

namespace San14::Render::Interop {
class OcclusionResourceReader {
public:
    OcclusionResourceReader(const fs::path& path,unsigned signature):bytes_(load(path)) {
        if(word()!=signature)throw std::runtime_error("Occlusion resource signature");
    }
    unsigned word() {
        unsigned value;take(&value,sizeof(value));return value;
    }
    unsigned count(size_t minimumRowBytes) {
        const auto value=word();
        if(minimumRowBytes&&value>remaining()/minimumRowBytes)throw std::runtime_error("Occlusion resource row extent");
        return value;
    }
    std::vector<char> blob() {
        const auto size=word();if(size>remaining())throw std::runtime_error("Occlusion program byte extent");
        std::vector<char> result(size);take(result.data(),size);return result;
    }
    template<class T> T record() {T value;take(&value,sizeof(value));return value;}
    void finish() const {if(remaining())throw std::runtime_error("Occlusion resource trailing bytes");}
private:
    size_t remaining() const {return bytes_.size()-offset_;}
    void take(void* destination,size_t size) {
        if(size>remaining())throw std::runtime_error("Occlusion resource is truncated");
        if(size)std::memcpy(destination,bytes_.data()+offset_,size);offset_+=size;
    }
    std::vector<char> bytes_;
    size_t offset_{};
};

inline OcclusionResourceBank readOcclusionResources(const fs::path& source) {
    OcclusionResourceBank result;
    // OCP1: unique DXBC stage programs and named RDEF layouts.
    OcclusionResourceReader programs(source/"programs.bin",0x3150434Fu);
    const auto programCount=programs.count(16);
    for(unsigned i=0;i<programCount;++i) {
        const auto id=programs.word();OcclusionProgramSource program;
        program.stage=programs.word();program.code=programs.blob();
        const auto count=programs.count(12);
        for(unsigned b=0;b<count;++b) {
            OcclusionConstantLayout layout;layout.slot=programs.word();layout.bytes=programs.word();
            const auto fields=programs.count(12);
            if(layout.slot>=D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT||!layout.bytes||(layout.bytes&15))
                throw std::runtime_error("Occlusion imported constant buffer layout");
            for(unsigned f=0;f<fields;++f) {
                OcclusionConstantField field;field.semantic=OcclusionConstantSemantic(programs.word());
                field.offset=programs.word();field.bytes=programs.word();
                if(field.offset>layout.bytes||field.bytes>layout.bytes-field.offset)
                    throw std::runtime_error("Occlusion imported constant field layout");
                layout.fields.push_back(field);
            }
            program.constants.push_back(std::move(layout));
        }
        if(!result.programs.emplace(id,std::move(program)).second)throw std::runtime_error("Occlusion duplicate program identity");
    }
    programs.finish();

    // OCV1: actual variant keys and all declared pass references. The current
    // renderer selects only its primary graphics stages; absent references
    // remain absent rather than becoming a fallback shader.
    OcclusionResourceReader variants(source/"variants.bin",0x3156434Fu);
    const auto variantCount=variants.word(),passCount=variants.word();
    for(unsigned i=0;i<variantCount;++i) {
        OcclusionVariantSource variant;for(auto& word:variant.words)word=variants.word();
        for(unsigned p=0;p<passCount;++p) {
            OcclusionPassSource pass;pass.vertex=variants.word();pass.pixel=variants.word();
            const auto textures=variants.count(8);
            for(unsigned t=0;t<textures;++t) {
                OcclusionTextureBinding binding;binding.slot=variants.word();binding.input=OcclusionTextureInput(variants.word());
                if(binding.slot>=32)
                    throw std::runtime_error("Occlusion imported texture declaration");
                pass.textures.push_back(binding);
            }
            variant.passes.push_back(std::move(pass));
        }
        result.variantIndices.emplace(variant.words,result.variants.size());
        result.blurCapabilities|=std::uint64_t(1)<<(variant.words[4]&63);
        result.variants.push_back(std::move(variant));
    }
    variants.finish();

    // OCS1: native sampler descriptors produced by the existing key decoder.
    OcclusionResourceReader samplers(source/"samplers.bin",0x3153434Fu);
    const auto samplerCount=samplers.count(sizeof(D3D11_SAMPLER_DESC));
    for(unsigned i=0;i<samplerCount;++i)result.samplers.push_back(samplers.record<D3D11_SAMPLER_DESC>());
    samplers.finish();
    // OCT1: ordered capability candidates for each scratch output.
    OcclusionResourceReader targets(source/"targets.bin",0x3154434Fu);
    const auto targetCount=targets.count(4);
    if(targetCount!=result.candidates.size())throw std::runtime_error("Occlusion scratch recipe count");
    for(auto& output:result.candidates) {
        const auto count=targets.count(12);
        for(unsigned i=0;i<count;++i)output.push_back({targets.word(),targets.word(),targets.word()});
    }
    targets.finish();
    // OCN1: sample-only deterministic noise; there is no unused RTV format.
    OcclusionResourceReader noise(source/"noise.bin",0x314E434Fu);
    result.noise.resource=noise.word();result.noise.sampled=noise.word();
    result.noise.width=noise.word();result.noise.height=noise.word();result.noise.rowPitch=result.noise.width*4;
    result.noise.bytes=noise.blob();noise.finish();
    // OCK1: one group-independent project stock bank and source blend recipe.
    OcclusionResourceReader stock(source/"stock.bin",0x314B434Fu);
    result.stock.vertex=stock.blob();result.stock.default3=stock.blob();
    result.stock.common138=stock.blob();result.stock.common140=stock.blob();
    result.stock.sampler=result.samplers.at(stock.word());
    auto& blend=result.compositionBlend;
    blend.BlendEnable=stock.word();blend.SrcBlend=D3D11_BLEND(stock.word());blend.DestBlend=D3D11_BLEND(stock.word());
    blend.BlendOp=D3D11_BLEND_OP(stock.word());blend.SrcBlendAlpha=D3D11_BLEND(stock.word());
    blend.DestBlendAlpha=D3D11_BLEND(stock.word());blend.BlendOpAlpha=D3D11_BLEND_OP(stock.word());
    const auto mask=stock.word();if(mask>15)throw std::runtime_error("Occlusion composition color mask");
    blend.RenderTargetWriteMask=BYTE(mask);stock.finish();
    return result;
}
} // namespace San14::Render::Interop
