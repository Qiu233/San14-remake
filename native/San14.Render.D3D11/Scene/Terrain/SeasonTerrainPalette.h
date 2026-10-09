#pragma once
// CE C460 changes LandscapePalette rows10/4. The actual first-name lookup
// indexes below belong to one loaded Landscape, not a synthetic texture bank.
#include "../../Passes/Environment/SeasonResources.h"
#include <d3dcompiler.h>
#include <d3d11shader.h>

namespace source_current_season_terrain {
struct PaletteBank {std::array<std::vector<uint32_t>,4> seasons;};
inline PaletteBank readPaletteBank(const std::filesystem::path& directory) {
    std::ifstream input(directory/"season-palette.bin",std::ios::binary);
    if(!input)throw std::runtime_error("Actual seasonal Landscape palette absent");
    auto word=[&](){uint32_t v{};input.read(reinterpret_cast<char*>(&v),4);
        if(!input)throw std::runtime_error("Actual seasonal Landscape palette truncated");return v;};
    uint32_t seasons=word(),count=word();
    if(seasons!=4||count>255)throw std::runtime_error("Actual seasonal Landscape palette extent");
    PaletteBank out;
    for(uint32_t i=0;i<4;++i){uint32_t season=word();if(season>=4)throw std::runtime_error("Actual seasonal palette index");
        auto& rows=out.seasons[season];rows.resize(count);for(auto& row:rows)row=word();}
    if(input.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Actual seasonal palette trailing bytes");
    return out;
}
struct Binding {UINT offset{},extent{};};
// Reflection belongs to program loading, once. The changed index column is
// present only in the actual near PS, never copied by coarse CB offsets.
inline Binding prepareBinding(std::span<const char> actualNearPixelProgram) {
    Microsoft::WRL::ComPtr<ID3D11ShaderReflection> reflection;
    source_current_season_resources::requireHr(D3DReflect(actualNearPixelProgram.data(),actualNearPixelProgram.size(),
        __uuidof(ID3D11ShaderReflection),reinterpret_cast<void**>(reflection.GetAddressOf())),"Season terrain PS RDEF absent");
    auto globals=reflection->GetConstantBufferByName("$Globals");D3D11_SHADER_BUFFER_DESC buffer{};
    source_current_season_resources::requireHr(globals->GetDesc(&buffer),"Season terrain Globals absent");
    auto variable=globals->GetVariableByName("vDtlTexIdx");D3D11_SHADER_VARIABLE_DESC v{};D3D11_SHADER_TYPE_DESC t{};
    source_current_season_resources::requireHr(variable->GetDesc(&v),"Season terrain index column absent");
    source_current_season_resources::requireHr(variable->GetType()->GetDesc(&t),"Season terrain index column type absent");
    if(buffer.Size!=16432||t.Class!=D3D_SVC_SCALAR||t.Type!=D3D_SVT_INT||t.Rows!=1||t.Columns!=1||
        t.Elements!=255||v.Size!=254*16+4||v.StartOffset+v.Size>buffer.Size)
        throw std::runtime_error("Actual seasonal near terrain palette RDEF differs");
    return {v.StartOffset,buffer.Size};
}
inline void patchCurrent(const PaletteBank& bank,const Binding& binding,uint32_t season,std::span<char> currentNearPixelBytes) {
    if(season>=4||currentNearPixelBytes.size()!=binding.extent)throw std::runtime_error("Current seasonal terrain palette extent");
    const auto& rows=bank.seasons[season];
    for(unsigned index:{10u,4u})if(index<rows.size())
        std::memcpy(currentNearPixelBytes.data()+binding.offset+index*16,&rows[index],4);
}
// Patch before the existing updateNativeTerrainCameraGpu so that one ordinary
// upload includes both current camera and seasonal palette. Coarse has only
// ScaleU; C460 does not modify it, or any texture/control/geometry/history.
template<class TerrainGpuOwner>
inline void patchNativeCurrent(const PaletteBank& bank,const Binding& binding,uint32_t season,TerrainGpuOwner& terrain) {
    patchCurrent(bank,binding,season,terrain.constants[1].bytes);
}
} // namespace source_current_season_terrain
