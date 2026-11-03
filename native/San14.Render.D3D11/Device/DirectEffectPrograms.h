#pragma once
#include <array>
#include <iterator>
#include <string_view>
#include <stdexcept>
#include <d3d11.h>
#include <wrl/client.h>
#include "DirectEffectPrograms.generated.h"

namespace San14::Render {
// This immutable bank belongs to the application Native11 device. Scenes and
// particle allocations borrow it; no scene exit rebuilds or retires its media.
class NativeDirectEffectPrograms final {
    template<class T>using ComPtr=Microsoft::WRL::ComPtr<T>;
    std::array<ComPtr<ID3D11ComputeShader>,std::size(current_effect::built_direct::Programs)> shaders;
public:
    explicit NativeDirectEffectPrograms(ID3D11Device* device) {
        for(std::size_t i=0;i<shaders.size();++i) {
            const auto& program=current_effect::built_direct::Programs[i];
            if(FAILED(device->CreateComputeShader(program.code,program.codeBytes,nullptr,&shaders[i])))
                throw std::runtime_error("Direct Effect built compute shader");
        }
    }
    ComPtr<ID3D11ComputeShader> find(std::string_view source,std::string_view entry,std::string_view profile) const {
        if(profile!="cs_5_0")return {};
        for(std::size_t i=0;i<shaders.size();++i) {
            const auto& program=current_effect::built_direct::Programs[i];
            if(entry==program.entry&&source==std::string_view(reinterpret_cast<const char*>(program.source),program.sourceBytes))return shaders[i];
        }
        return {};
    }
};
}
