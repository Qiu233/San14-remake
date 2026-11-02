#pragma once
#include "../Runtime/PreparedFileInput.h"
#include "TerrainProgramConstants.h"
#include <d3d11.h>
#include <d3dcompiler.h>
#include <d3d11shader.h>
#include <wrl/client.h>
#include <array>
#include <iterator>
#include <map>
#include <memory>
#include <string>
#include <vector>
#include <stdexcept>

namespace San14::Render {
// Only immutable Landscape programs and their input signature. Camera/season
// constants, instances, targets and textures remain with their actual session.
struct NativeTerrainPrograms final {
    template<class T> using Com=Microsoft::WRL::ComPtr<T>;
    Com<ID3D11VertexShader> vertex,coarseVertex;
    Com<ID3D11PixelShader> pixel,coarsePixel;
    Com<ID3D11InputLayout> layout;
    std::array<NativeTerrainProgramConstantLayout,4> constants;

    static std::shared_ptr<const NativeTerrainPrograms> load(ID3D11Device* device,
            const std::filesystem::path& directory,bool grouped) {
        auto result=std::make_shared<NativeTerrainPrograms>();
        auto vertex=read(directory/(grouped?"near-vs.dxbc":"terrain-vs.dxbc"));
        auto pixel=read(directory/(grouped?"near-ps.dxbc":"terrain-ps.dxbc"));
        require(device->CreateVertexShader(vertex.data(),vertex.size(),nullptr,&result->vertex));
        require(device->CreatePixelShader(pixel.data(),pixel.size(),nullptr,&result->pixel));
        Com<ID3D11ShaderReflection> reflection;
        require(D3DReflect(vertex.data(),vertex.size(),__uuidof(ID3D11ShaderReflection),
            reinterpret_cast<void**>(reflection.GetAddressOf())));
        D3D11_SHADER_DESC description{};require(reflection->GetDesc(&description));
        constexpr std::array<DXGI_FORMAT,5> formats{DXGI_FORMAT_R32G32B32_FLOAT,DXGI_FORMAT_R32G32B32_FLOAT,
            DXGI_FORMAT_R32G32B32_FLOAT,DXGI_FORMAT_R32G32B32A32_FLOAT,DXGI_FORMAT_R32G32B32A32_FLOAT};
        constexpr std::array<UINT,5> offsets{0,0,12,24,40};
        std::vector<D3D11_INPUT_ELEMENT_DESC> elements;
        for(UINT i=0;i<description.InputParameters;i++) {
            D3D11_SIGNATURE_PARAMETER_DESC p{};require(reflection->GetInputParameterDesc(i,&p));
            if(p.SystemValueType!=D3D_NAME_UNDEFINED)continue;
            const auto n=elements.size();
            if(n>=formats.size())throw std::runtime_error("Unexpected Landscape vertex signature");
            elements.push_back({p.SemanticName,p.SemanticIndex,formats[n],n?1u:0u,offsets[n],
                n?D3D11_INPUT_PER_INSTANCE_DATA:D3D11_INPUT_PER_VERTEX_DATA,n?1u:0u});
        }
        require(device->CreateInputLayout(elements.data(),UINT(elements.size()),vertex.data(),vertex.size(),&result->layout));
        if(grouped) {
            auto coarseVertex=read(directory/"coarse-vs.dxbc"),coarsePixel=read(directory/"coarse-ps.dxbc");
            require(device->CreateVertexShader(coarseVertex.data(),coarseVertex.size(),nullptr,&result->coarseVertex));
            require(device->CreatePixelShader(coarsePixel.data(),coarsePixel.size(),nullptr,&result->coarsePixel));
            result->constants={readNativeTerrainProgramConstantLayout({vertex.data(),vertex.size()},true),
                readNativeTerrainProgramConstantLayout({pixel.data(),pixel.size()},false),
                readNativeTerrainProgramConstantLayout({coarseVertex.data(),coarseVertex.size()},true),
                readNativeTerrainProgramConstantLayout({coarsePixel.data(),coarsePixel.size()},false)};
        }
        return result;
    }
private:
    static void require(HRESULT result){if(FAILED(result))throw result;}
    static std::vector<char> read(const std::filesystem::path& path) {
        NativePreparedInputFile input(path,std::ios::binary);
        if(!input)throw std::runtime_error("Prepared Landscape program is absent");
        std::vector<char> bytes{std::istreambuf_iterator<char>(input),std::istreambuf_iterator<char>()};
        if(input.bad()||bytes.empty())throw std::runtime_error("Prepared Landscape program is unreadable or empty");
        return bytes;
    }
};

// Explicit device-owned current entries. Replacement drops this owner's old
// entry; actual TerrainPass borrowers keep their immutable generation alive.
// The caller holds the device execution role for every operation on this bank.
struct NativeTerrainProgramDeclaration {
    std::wstring logicalKey,identity;
    std::filesystem::path directory;
};
class NativeTerrainProgramResidency final {
    struct Entry {std::wstring identity;std::shared_ptr<const NativeTerrainPrograms> programs;};
    std::map<std::wstring,Entry> current_;
    static std::wstring line(NativePreparedInputFile& input) {
        std::string value;
        if(!std::getline(input,value))throw std::runtime_error("Landscape program manifest is incomplete");
        if(!value.empty()&&value.back()=='\r')value.pop_back();
        std::wstring result;
        for(unsigned char c:value) {
            if(c<32||c>126)throw std::runtime_error("Landscape program manifest requires its ASCII source identity");
            result.push_back(wchar_t(c));
        }
        return result;
    }
public:
    void replace(ID3D11Device* device,const std::vector<NativeTerrainProgramDeclaration>& sources) {
        if(sources.empty())throw std::runtime_error("The declared Landscape program closure is empty");
        std::map<std::wstring,Entry> candidate;
        for(const auto& source:sources) {
            if(source.logicalKey.empty()||source.identity.empty())throw std::runtime_error("Landscape program source identity is absent");
            std::shared_ptr<const NativeTerrainPrograms> programs;
            if(auto found=current_.find(source.logicalKey);found!=current_.end()&&found->second.identity==source.identity)
                programs=found->second.programs;
            else {
                // Only a new dependency identity opens prepared source files.
                // Hits do not inspect timestamps, read bytes or recompute hashes.
                NativePreparedInputFile manifest(source.directory/"terrain-programs.txt");
                if(line(manifest)!=L"SAN14_TERRAIN_PROGRAMS 1"||line(manifest)!=source.logicalKey||line(manifest)!=source.identity)
                    throw std::runtime_error("Landscape program package differs from its declared source identity");
                programs=NativeTerrainPrograms::load(device,source.directory,true);
            }
            if(!candidate.emplace(source.logicalKey,Entry{source.identity,std::move(programs)}).second)
                throw std::runtime_error("The active Landscape program declaration is duplicated");
        }
        // Publish only the complete new closure. Old passes retain their own
        // shared_ptr; this owner neither keeps a history nor drops live borrows.
        current_.swap(candidate);
    }
    std::shared_ptr<const NativeTerrainPrograms> borrow(const std::wstring& logicalKey,const std::wstring& identity)const {
        const auto found=current_.find(logicalKey);
        if(found==current_.end()||found->second.identity!=identity)
            throw std::runtime_error("Landscape programs were not loaded for this actual source generation at startup");
        return found->second.programs;
    }
};
} // namespace San14::Render
