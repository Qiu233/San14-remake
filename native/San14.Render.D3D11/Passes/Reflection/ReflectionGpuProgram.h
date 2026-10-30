#pragma once
#include <d3d11.h>
#include <d3dcompiler.h>
#include <d3d11shader.h>
#include <array>
#include <algorithm>
#include <cstdint>
#include <map>
#include <string>
#include <vector>
#include <cstring>
#include <stdexcept>

namespace San14::Render::Reflection {
using Float4=std::array<float,4>;
using Matrix=std::array<float,16>;

// Original DXBC is imported from the normal resource conversion package.
// Reflection metadata is cached at resource creation, never parsed per frame.
class OriginalProgram {
public:
    void prepare(ID3D11Device* device,const fs::path& directory,bool compute) {
        compute_=compute;
        const auto code=load(directory/(compute?"cs.dxbc":"ps.dxbc"));
        if(compute)check(device->CreateComputeShader(code.data(),code.size(),nullptr,&cs_),"Reflection original CS");
        else check(device->CreatePixelShader(code.data(),code.size(),nullptr,&ps_),"Reflection original PS");
        ComPtr<ID3D11ShaderReflection> reflected;
        check(D3DReflect(code.data(),code.size(),__uuidof(ID3D11ShaderReflection),reinterpret_cast<void**>(reflected.GetAddressOf())),"Reflection source RDEF");
        D3D11_SHADER_DESC shader{};check(reflected->GetDesc(&shader),"Reflection source shader declaration");
        if(compute)reflected->GetThreadGroupSize(&group_[0],&group_[1],&group_[2]);
        if(shader.ConstantBuffers) {
            auto globals=reflected->GetConstantBufferByName("$Globals");D3D11_SHADER_BUFFER_DESC cb{};
            check(globals->GetDesc(&cb),"Reflection original Globals");
            if(!cb.Size||(cb.Size&15))throw std::runtime_error("Reflection Globals byte alignment");
            bytes_.resize(cb.Size);
            for(UINT index=0;index<cb.Variables;++index) {
                D3D11_SHADER_VARIABLE_DESC v{};check(globals->GetVariableByIndex(index)->GetDesc(&v),"Reflection source variable");
                if(v.StartOffset+v.Size>cb.Size||!fields_.emplace(v.Name,std::pair<UINT,UINT>{v.StartOffset,v.Size}).second)
                    throw std::runtime_error("Reflection source Globals field range");
            }
            D3D11_BUFFER_DESC desc{};desc.ByteWidth=cb.Size;desc.Usage=D3D11_USAGE_DEFAULT;desc.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
            check(device->CreateBuffer(&desc,nullptr,&constants_),"Reflection original Globals buffer");
        }
    }
    bool has(const char* name)const{return fields_.find(name)!=fields_.end();}
    void vector(const char* name,const Float4& value) {write(name,value.data(),sizeof(value));}
    void matrix(const char* name,const Matrix& value) {write(name,value.data(),sizeof(value));}
    void write(const char* name,const void* value,UINT bytes) {
        auto field=fields_.find(name);if(field==fields_.end()||field->second.second!=bytes)
            throw std::runtime_error(std::string("Reflection source parameter missing or wrong size: ")+name);
        std::memcpy(bytes_.data()+field->second.first,value,bytes);
    }
    void bind(ID3D11DeviceContext* context)const {
        if(constants_)context->UpdateSubresource(constants_.Get(),0,nullptr,bytes_.data(),0,0);
        auto cb=constants_.Get();
        if(compute_){context->CSSetShader(cs_.Get(),nullptr,0);context->CSSetConstantBuffers(0,1,&cb);}
        else {context->PSSetShader(ps_.Get(),nullptr,0);context->PSSetConstantBuffers(0,1,&cb);}
    }
    void dispatchGroups(ID3D11DeviceContext* context,UINT x,UINT y,UINT z=1)const {
        if(!compute_||!x||!y||!z)throw std::runtime_error("Reflection actual dispatch extent");
        bind(context);context->Dispatch(x,y,z);
    }
    void dispatch(ID3D11DeviceContext* context,UINT width,UINT height,UINT depth=1)const {
        if(!compute_||!group_[0]||!group_[1]||!group_[2])throw std::runtime_error("Reflection compute group declaration");
        bind(context);context->Dispatch((width+group_[0]-1)/group_[0],(height+group_[1]-1)/group_[1],(depth+group_[2]-1)/group_[2]);
    }
private:
    bool compute_{};
    std::array<UINT,3> group_{};
    ComPtr<ID3D11ComputeShader> cs_;ComPtr<ID3D11PixelShader> ps_;ComPtr<ID3D11Buffer> constants_;
    std::vector<char> bytes_;std::map<std::string,std::pair<UINT,UINT>> fields_;
};
inline void unbindOutputs(ID3D11DeviceContext* context) {
    std::array<ID3D11UnorderedAccessView*,8> empty{};
    context->OMSetRenderTargetsAndUnorderedAccessViews(0,nullptr,nullptr,0,8,empty.data(),nullptr);
}
inline void unbindCompute(ID3D11DeviceContext* context) {
    std::array<ID3D11ShaderResourceView*,16> srvs{};std::array<ID3D11UnorderedAccessView*,8> uavs{};
    context->CSSetShaderResources(0,UINT(srvs.size()),srvs.data());
    context->CSSetUnorderedAccessViews(0,UINT(uavs.size()),uavs.data(),nullptr);
}
inline void unbindPixel(ID3D11DeviceContext* context) {
    std::array<ID3D11ShaderResourceView*,16> srvs{};context->PSSetShaderResources(0,UINT(srvs.size()),srvs.data());
}
} // namespace San14::Render::Reflection
