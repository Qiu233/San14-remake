#pragma once
#include "../Runtime/PreparedFileInput.h"
#include <d3d11.h>
#include <wrl/client.h>
#include <cstddef>
#include <cstdint>
#include <iterator>
#include <memory>
#include <stdexcept>
#include <string>
#include <vector>

namespace San14::Render {
// One locale's actual ordinary Primary shadow programs. Mutable constants,
// depth, targets, raster and all current camera/World data stay in the view.
struct NativePrimaryShadowGpuMedia final {
    struct Template {
        Microsoft::WRL::ComPtr<ID3D11VertexShader> vertex;
        Microsoft::WRL::ComPtr<ID3D11InputLayout> layout;
    };
    std::vector<Template> templates;

    static std::shared_ptr<const NativePrimaryShadowGpuMedia> load(ID3D11Device* device,
            const std::filesystem::path& directory,std::size_t count) {
        auto result=std::make_shared<NativePrimaryShadowGpuMedia>();
        result->templates.resize(count);
        for(std::size_t index=0;index<count;++index) {
            auto path=directory/("template-"+std::to_string(index));
            auto vertex=read(path/"vs.dxbc");auto& output=result->templates[index];
            require(device->CreateVertexShader(vertex.data(),vertex.size(),nullptr,&output.vertex),"Primary shadow VS");
            struct Element{std::string name;UINT index{},format{},slot{},offset{};};
            std::vector<Element> elements;Element element;
            NativePreparedInputFile rows(path/"layout.txt");
            if(!rows)throw std::runtime_error("Primary shadow source layout is absent");
            while(rows>>element.name>>element.index>>element.format>>element.slot>>element.offset)
                elements.push_back(element);
            std::vector<D3D11_INPUT_ELEMENT_DESC> layout;
            for(const auto& entry:elements)
                layout.push_back({entry.name.c_str(),entry.index,DXGI_FORMAT(entry.format),entry.slot,entry.offset,
                    D3D11_INPUT_PER_VERTEX_DATA,0});
            require(device->CreateInputLayout(layout.data(),UINT(layout.size()),vertex.data(),vertex.size(),&output.layout),
                "Primary shadow source input layout");
        }
        return result;
    }
private:
    static void require(HRESULT result,const char* what) {
        if(FAILED(result))throw std::runtime_error(std::string(what)+" HRESULT="+std::to_string(std::uint32_t(result)));
    }
    static std::vector<char> read(const std::filesystem::path& path) {
        NativePreparedInputFile input(path,std::ios::binary);
        if(!input)throw std::runtime_error("Primary shadow prepared program is absent: "+path.string());
        std::vector<char> result{std::istreambuf_iterator<char>(input),std::istreambuf_iterator<char>()};
        if(input.bad()||result.empty())throw std::runtime_error("Primary shadow prepared program is empty or unreadable");
        return result;
    }
};
} // namespace San14::Render
