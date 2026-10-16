#pragma once
#include "OcclusionConstants.h"
#include <map>

namespace San14::Render {
struct OcclusionProgramSource {
    unsigned stage{};
    std::vector<char> code;
    std::vector<OcclusionConstantLayout> constants;
};
// Unique imported programs are retained once. A pass selects references into
// this bank; group counts do not duplicate shader objects or filesystem trees.
class OcclusionGpuPrograms {
    struct Constants {
        OcclusionConstantLayout layout;
        ComPtr<ID3D11Buffer> buffer;
    };
    struct Program {
        ComPtr<ID3D11VertexShader> vertex;
        ComPtr<ID3D11PixelShader> pixel;
        std::vector<Constants> constants;
    };
public:
    void bind(ID3D11Device* device,ID3D11DeviceContext* context,unsigned id,
        const OcclusionProgramSource& source,const OcclusionConstantValues& values,unsigned width,unsigned height) {
        auto& program=prepare(device,id,source);
        std::array<ID3D11Buffer*,D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT> bindings{};
        for(const auto& constants:program.constants) {
            const auto bytes=packOcclusionConstants(constants.layout,values,width,height);
            context->UpdateSubresource(constants.buffer.Get(),0,nullptr,bytes.data(),0,0);
            bindings.at(constants.layout.slot)=constants.buffer.Get();
        }
        if(source.stage==1) {
            context->VSSetShader(program.vertex.Get(),nullptr,0);
            context->VSSetConstantBuffers(0,UINT(bindings.size()),bindings.data());
        }else if(source.stage==0) {
            context->PSSetShader(program.pixel.Get(),nullptr,0);
            context->PSSetConstantBuffers(0,UINT(bindings.size()),bindings.data());
        }else throw std::runtime_error("Unsupported occlusion graphics stage");
    }
private:
    Program& prepare(ID3D11Device* device,unsigned id,const OcclusionProgramSource& source) {
        if(auto found=programs_.find(id);found!=programs_.end())return found->second;
        if(source.code.empty())throw std::runtime_error("Occlusion selected program is empty");
        Program result;
        if(source.stage==1)check(device->CreateVertexShader(source.code.data(),source.code.size(),nullptr,&result.vertex),"Occlusion selected VS");
        else if(source.stage==0)check(device->CreatePixelShader(source.code.data(),source.code.size(),nullptr,&result.pixel),"Occlusion selected PS");
        else throw std::runtime_error("Unsupported occlusion graphics stage");
        for(const auto& layout:source.constants) {
            if(!layout.bytes||(layout.bytes&15)||layout.slot>=D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT)
                throw std::runtime_error("Occlusion resource CB layout");
            Constants constants;constants.layout=layout;
            D3D11_BUFFER_DESC bd{};bd.ByteWidth=layout.bytes;bd.BindFlags=D3D11_BIND_CONSTANT_BUFFER;bd.Usage=D3D11_USAGE_DEFAULT;
            check(device->CreateBuffer(&bd,nullptr,&constants.buffer),"Occlusion current CB");
            result.constants.push_back(std::move(constants));
        }
        return programs_.emplace(id,std::move(result)).first->second;
    }
    std::map<unsigned,Program> programs_;
};
} // namespace San14::Render
