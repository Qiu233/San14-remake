#pragma once
#include "../../Runtime/PreparedFileInput.h"
// Include after the existing primary-forward CPU/GPU headers. The actual
// current primary catalog/plan still owns population, LOCAL and material.
inline std::filesystem::path nativePrimaryForwardBankBase(const std::filesystem::path& input) {
    NativePreparedInputFile source(input/"base-input.txt");std::string relative;source>>relative;
    if(!source)throw std::runtime_error("Forward bank shared source directory absent");return input/relative;
}
inline NativePrimaryForwardSource prepareNativePrimaryForwardCurrentBankSource(const std::filesystem::path& input,
        std::shared_ptr<const San14::Render::NativePrimaryForwardMetadata> metadata={},bool requireResident=false) {
    if(requireResident&&!metadata)throw std::runtime_error("Primary Forward CPU metadata must be loaded at startup");
    if(metadata) {
        NativePrimaryForwardSource result;result.metadata=std::move(metadata);
        result.templates.resize(result.metadata->templates.size());
        const auto base=nativePrimaryForwardBankBase(input);
        std::array<char,480> vertex{};std::array<char,1360> pixel{};
        NativePreparedInputFile vertexInput(base/"vs-b0.bin",std::ios::binary),pixelInput(base/"ps-b0.bin",std::ios::binary);
        vertexInput.read(vertex.data(),vertex.size());pixelInput.read(pixel.data(),pixel.size());
        if(!vertexInput||!pixelInput)throw std::runtime_error("Forward actual retained numeric packet is incomplete");
        for(std::size_t index=0;index<result.templates.size();++index) {
            auto& current=result.templates[index];current.eligible=result.metadata->templates[index].eligible;
            if(current.eligible){current.vertex=vertex;current.pixel=pixel;}
        }
        return result;
    }
    auto result=prepareNativePrimaryForwardSource(nativePrimaryForwardBankBase(input));
    unsigned count{};NativePreparedInputFile n(input/"template-count.txt");n>>count;
    if(!n||count<result.templates.size())throw std::runtime_error("Forward bank template extent differs");
    const auto previous=result.templates.size();result.templates.resize(count);
    NativePreparedInputFile rows(input/"templates.txt");unsigned index,eligible;std::string directory;
    while(rows>>index>>eligible>>directory){auto& t=result.templates.at(index);t.eligible=eligible!=0;
        if(!t.eligible||index<previous)continue;
        NativePreparedInputFile v(input/directory/"vs-b0.bin",std::ios::binary),p(input/directory/"ps-b0.bin",std::ios::binary);
        v.read(t.vertex.data(),t.vertex.size());p.read(t.pixel.data(),t.pixel.size());
        if(!v||!p)throw std::runtime_error("Forward bank actual source CB absent");}
    if(!rows.eof())throw std::runtime_error("Forward bank template metadata incomplete");return result;
}
#ifndef SOURCE_PRIMARY_FORWARD_BANK_CPU_ONLY
inline NativePrimaryForwardGpu prepareNativePrimaryForwardCurrentBankGpu(ID3D11Device* device,
        ID3D11DeviceContext* context,const fs::path& input,
        std::shared_ptr<const San14::Render::NativePrimaryForwardMedia> sharedMedia={},bool requireResident=false) {
    const auto base=nativePrimaryForwardBankBase(input);auto result=prepareNativePrimaryForwardGpu(device,context,base,sharedMedia,requireResident);
    if(sharedMedia)return result;
    const auto previous=result.layouts.size();unsigned count{};NativePreparedInputFile n(input/"template-count.txt");n>>count;
    if(!n||count<previous)throw std::runtime_error("Forward bank GPU template extent differs");result.layouts.resize(count);
    auto vertex=load(base/"vs.dxbc");NativePreparedInputFile rows(input/"templates.txt");unsigned index,eligible;std::string directory;
    while(rows>>index>>eligible>>directory){if(!eligible||index<previous)continue;
        struct Element{std::string name;unsigned index,format,slot,offset;};Element row;std::vector<Element> elements;
        NativePreparedInputFile layout(input/directory/"layout.txt");while(layout>>row.name>>row.index>>row.format>>row.slot>>row.offset)elements.push_back(row);
        if(elements.empty())throw std::runtime_error("Forward bank selected source layout absent");
        std::vector<D3D11_INPUT_ELEMENT_DESC> desc;for(const auto& e:elements)desc.push_back({e.name.c_str(),e.index,DXGI_FORMAT(e.format),e.slot,e.offset,D3D11_INPUT_PER_VERTEX_DATA,0});
        check(device->CreateInputLayout(desc.data(),UINT(desc.size()),vertex.data(),vertex.size(),&result.layouts.at(index)),"Forward bank original additional layout");}
    if(!rows.eof())throw std::runtime_error("Forward bank GPU metadata incomplete");return result;
}
#endif
// Current publication borrows its real primary record bindings and typed lights.
