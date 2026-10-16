#pragma once
#include <array>
#include <vector>
#include <cstring>
#include <cstdint>

namespace San14::Render {
enum class OcclusionConstantSemantic : unsigned {
    Resolution,Depth,View,Radius,Ray,Occlusion,Blur,SwitchReference,RadiusRange
};
struct OcclusionConstantField {
    OcclusionConstantSemantic semantic{};
    unsigned offset{},bytes{};
};
struct OcclusionConstantLayout {
    unsigned slot{},bytes{};
    std::vector<OcclusionConstantField> fields;
};
struct OcclusionConstantValues {
    std::array<float,4> depth{},view{},blur{};
    std::vector<std::array<float,4>> radius,ray,occlusion,radiusRange;
    std::vector<unsigned> references;
};
// Layouts are the actual selected program's RDEF. Arrays are padded only to
// that resource-declared capacity; scalar references retain the HLSL stride.
inline std::vector<char> packOcclusionConstants(const OcclusionConstantLayout& layout,
    const OcclusionConstantValues& values,unsigned width,unsigned height) {
    if(!width||!height||!layout.bytes||(layout.bytes&15)||layout.slot>=D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT)
        throw std::runtime_error("Occlusion constant buffer layout or dimensions");
    std::vector<char> result(layout.bytes,0);
    const std::array<float,4> resolution{float(width),float(height),1.f/float(width),1.f/float(height)};
    for(const auto& field:layout.fields) {
        if(field.offset>result.size()||field.bytes>result.size()-field.offset)
            throw std::runtime_error("Occlusion named constant is outside its RDEF buffer");
        auto vector=[&](const std::array<float,4>& value) {
            if(field.bytes!=sizeof(value))throw std::runtime_error("Occlusion vector RDEF size");
            std::memcpy(result.data()+field.offset,value.data(),sizeof(value));
        };
        auto array=[&](const std::vector<std::array<float,4>>& value) {
            if(value.size()>field.bytes/16||field.bytes%16)
                throw std::runtime_error("Current occlusion groups exceed selected vector array capacity");
            for(size_t i=0;i<value.size();++i)std::memcpy(result.data()+field.offset+i*16,value[i].data(),16);
        };
        switch(field.semantic) {
            case OcclusionConstantSemantic::Resolution:vector(resolution);break;
            case OcclusionConstantSemantic::Depth:vector(values.depth);break;
            case OcclusionConstantSemantic::View:vector(values.view);break;
            case OcclusionConstantSemantic::Radius:array(values.radius);break;
            case OcclusionConstantSemantic::Ray:array(values.ray);break;
            case OcclusionConstantSemantic::Occlusion:array(values.occlusion);break;
            case OcclusionConstantSemantic::Blur:vector(values.blur);break;
            case OcclusionConstantSemantic::RadiusRange:array(values.radiusRange);break;
            case OcclusionConstantSemantic::SwitchReference:
                if(values.references.empty()||field.bytes<(values.references.size()-1)*16+sizeof(unsigned))
                    throw std::runtime_error("Current occlusion groups exceed selected scalar array capacity");
                for(size_t i=0;i<values.references.size();++i)
                    std::memcpy(result.data()+field.offset+i*16,&values.references[i],sizeof(unsigned));
                break;
            default:throw std::runtime_error("Unsupported occlusion constant semantic");
        }
    }
    return result;
}
} // namespace San14::Render
