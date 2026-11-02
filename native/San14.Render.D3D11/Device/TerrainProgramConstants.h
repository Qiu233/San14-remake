#pragma once
#include <d3d11.h>
#include <d3dcompiler.h>
#include <d3d11shader.h>
#include <wrl/client.h>
#include <algorithm>
#include <array>
#include <cstring>
#include <span>
#include <stdexcept>
#include <vector>

namespace San14::Render {
// Immutable source RDEF only. Per-scene CB bytes and GPU buffers are separate.
struct NativeTerrainProgramConstantLayout {
    bool vertex=false;
    UINT bufferBytes=0,slot=0;
    int worldToProjection=-1,worldToView=-1,eye=-1,detailTextureIndex=-1;
    UINT detailTextureCapacity=0;
};
inline void requireTerrainProgramLayout(HRESULT hr,const char*) {if(FAILED(hr))throw hr;}
inline NativeTerrainProgramConstantLayout readNativeTerrainProgramConstantLayout(
        std::span<const char> dxbc,bool vertex) {
    NativeTerrainProgramConstantLayout result;result.vertex=vertex;
    Microsoft::WRL::ComPtr<ID3D11ShaderReflection> reflection;
    requireTerrainProgramLayout(D3DReflect(dxbc.data(),dxbc.size(),__uuidof(ID3D11ShaderReflection),
        reinterpret_cast<void**>(reflection.GetAddressOf())),"Terrain source camera RDEF");
    D3D11_SHADER_INPUT_BIND_DESC binding{};
    requireTerrainProgramLayout(reflection->GetResourceBindingDescByName("$Globals",&binding),"Terrain source Globals binding");
    if(binding.Type!=D3D_SIT_CBUFFER||binding.BindPoint!=0||binding.BindCount!=1)
        throw std::runtime_error("Terrain source Globals cb0 binding differs");
    result.slot=binding.BindPoint;
    auto globals=reflection->GetConstantBufferByName("$Globals");D3D11_SHADER_BUFFER_DESC bufferDesc{};
    requireTerrainProgramLayout(globals->GetDesc(&bufferDesc),"Terrain source Globals layout");
    if(bufferDesc.Type!=D3D_CT_CBUFFER||!bufferDesc.Size||bufferDesc.Size%16||bufferDesc.Size>D3D11_REQ_CONSTANT_BUFFER_ELEMENT_COUNT*16u)
        throw std::runtime_error("Terrain source complete CB size differs");
    result.bufferBytes=bufferDesc.Size;
    auto lane=[&](ID3D11ShaderReflectionVariable* variable,UINT size,D3D_SHADER_VARIABLE_CLASS kind,
        D3D_SHADER_VARIABLE_TYPE type,UINT rows,UINT columns,UINT elements) {
        D3D11_SHADER_VARIABLE_DESC v{};D3D11_SHADER_TYPE_DESC t{};
        requireTerrainProgramLayout(variable->GetDesc(&v),"Terrain source camera lane");requireTerrainProgramLayout(variable->GetType()->GetDesc(&t),"Terrain source camera lane type");
        if(v.Size!=size||v.StartOffset>bufferDesc.Size||size>bufferDesc.Size-v.StartOffset||
            t.Class!=kind||t.Type!=type||t.Rows!=rows||t.Columns!=columns||t.Elements!=elements||t.Members)
            throw std::runtime_error("Terrain source camera lane layout differs");
        return int(v.StartOffset);
    };
    std::vector<std::pair<UINT,UINT>> ranges;
    for(UINT i=0;i<bufferDesc.Variables;++i) {
        auto variable=globals->GetVariableByIndex(i);D3D11_SHADER_VARIABLE_DESC description{};
        requireTerrainProgramLayout(variable->GetDesc(&description),"Terrain source constant field");
        if(!description.Name||!description.Size||description.StartOffset%4||description.StartOffset>bufferDesc.Size||
            description.Size>bufferDesc.Size-description.StartOffset)
            throw std::runtime_error("Terrain source constant field range differs");
        ranges.emplace_back(description.StartOffset,description.StartOffset+description.Size);
        auto capture=[&](int& offset,UINT size,D3D_SHADER_VARIABLE_CLASS kind,D3D_SHADER_VARIABLE_TYPE type,
            UINT rows,UINT columns,UINT elements) {
            if(offset>=0)throw std::runtime_error("Terrain source constant field duplicated");
            offset=lane(variable,size,kind,type,rows,columns,elements);
        };
        if(vertex&&std::strcmp(description.Name,"mW2P")==0)
            capture(result.worldToProjection,64,D3D_SVC_MATRIX_ROWS,D3D_SVT_FLOAT,4,4,0);
        else if(!vertex&&std::strcmp(description.Name,"mWV")==0)
            capture(result.worldToView,48,D3D_SVC_VECTOR,D3D_SVT_FLOAT,1,4,3);
        else if(!vertex&&std::strcmp(description.Name,"vEye")==0)
            capture(result.eye,16,D3D_SVC_VECTOR,D3D_SVT_FLOAT,1,4,0);
        else if(!vertex&&std::strcmp(description.Name,"vDtlTexIdx")==0) {
            D3D11_SHADER_TYPE_DESC type{};requireTerrainProgramLayout(variable->GetType()->GetDesc(&type),"Terrain source palette type");
            if(!type.Elements||type.Elements>255)throw std::runtime_error("Terrain source palette capacity differs");
            capture(result.detailTextureIndex,(type.Elements-1)*16+4,D3D_SVC_SCALAR,D3D_SVT_INT,1,1,type.Elements);
            result.detailTextureCapacity=type.Elements;
        }
    }
    std::sort(ranges.begin(),ranges.end());
    for(size_t i=1;i<ranges.size();++i)if(ranges[i-1].second>ranges[i].first)
        throw std::runtime_error("Terrain source constant fields overlap");
    if(vertex?result.worldToProjection<0:result.worldToView<0)
        throw std::runtime_error("Terrain source camera matrix absent");
    return result;

}
} // namespace San14::Render
