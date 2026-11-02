#pragma once
#include "ModelTextureMedia.h"
#include "PrimaryPassGpuMedia.h"
#include <windows.h>
#include <array>
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <iterator>
#include <map>
#include <memory>
#include <sstream>
#include <string>
#include <utility>
#include <vector>

namespace San14::Render {
// Camera-free import metadata. The application roster carries the original
// three-line draw prefix; depth/stencil and constants are deliberately absent.
struct NativePrimaryModelGpuDrawSource {
    std::uint32_t shaderFile{},vertexProgram{},pixelProgram{};
    std::string relativeDirectory;
    std::filesystem::path mediaDirectory,indexRelative;
    UINT indexCount{},indexStart{},topology{},indexBits{};
    INT baseVertex{};
    std::array<UINT,3> raster{}; // Fill, cull, front counter-clockwise.
};
inline void readNativePrimaryModelGpuGeometry(std::istream& input,NativePrimaryModelGpuDrawSource& source) {
    std::string index;
    input>>source.indexCount>>source.indexStart>>source.baseVertex>>source.topology>>source.indexBits>>index
        >>source.raster[0]>>source.raster[1]>>source.raster[2];
    if(!input)throw std::runtime_error("Primary immutable draw metadata is incomplete");
    source.indexRelative=std::filesystem::u8path(index);
}

// This type cannot carry per-view depth, constant buffers, retainedVs, wind,
// World state, targets or a context. Every COM object here is immutable media.
struct NativePrimaryModelGpuDraw final {
    template<class T>using Com=Microsoft::WRL::ComPtr<T>;
    std::string relativeDirectory;
    Com<ID3D11VertexShader> vertex;
    Com<ID3D11PixelShader> pixel;
    Com<ID3D11InputLayout> layout;
    std::vector<Com<ID3D11Buffer>> vertexBuffers;
    std::vector<UINT> strides;
    Com<ID3D11Buffer> indices;
    std::array<Com<ID3D11ShaderResourceView>,16> textures;
    std::array<Com<ID3D11SamplerState>,16> samplers;
    Com<ID3D11RasterizerState> raster;
    UINT indexCount{},indexStart{},topology{},indexBits{};
    INT baseVertex{};
};
struct NativePrimaryModelGpuDeclaration {
    std::wstring logicalKey,identity;
    std::filesystem::path directory;
};
struct NativePrimaryModelGpuReference {
    std::wstring logicalKey,identity,locale;
};

struct NativePrimaryModelGpuMedia final {
    template<class T>using Com=Microsoft::WRL::ComPtr<T>;
    std::map<std::wstring,std::shared_ptr<const NativePrimaryPassGpuMedia>> passes;
    std::vector<NativePrimaryModelGpuDraw> draws;
    Com<ID3D11BlendState> blend;

    // Also used by explicit standalone devices, keeping one model import path.
    // The shared application path never calls this loader from a scene opening.
    static std::shared_ptr<NativePrimaryModelGpuMedia> load(ID3D11Device* device,
            const std::vector<NativePrimaryModelGpuDrawSource>& sources) {
        auto result=std::make_shared<NativePrimaryModelGpuMedia>();
        std::map<std::filesystem::path,Com<ID3D11Buffer>> vertices,indices;
        std::map<std::uint64_t,Com<ID3D11VertexShader>> vertexShaders;
        std::map<std::uint64_t,Com<ID3D11PixelShader>> pixelShaders;
        std::map<std::string,Com<ID3D11InputLayout>> layouts;
        std::map<std::pair<std::filesystem::path,std::array<UINT,2>>,Com<ID3D11ShaderResourceView>> textures;
        std::map<std::array<UINT,13>,Com<ID3D11SamplerState>> samplers;
        std::map<std::array<UINT,3>,Com<ID3D11RasterizerState>> rasters;
        result->draws.reserve(sources.size());
        for(const auto& source:sources) {
            NativePrimaryModelGpuDraw draw;
            draw.relativeDirectory=source.relativeDirectory;
            draw.indexCount=source.indexCount;draw.indexStart=source.indexStart;draw.baseVertex=source.baseVertex;
            draw.topology=source.topology;draw.indexBits=source.indexBits;
            const auto vertexKey=(std::uint64_t(source.shaderFile)<<32)|source.vertexProgram;
            const auto pixelKey=(std::uint64_t(source.shaderFile)<<32)|source.pixelProgram;
            auto vertex=read(source.mediaDirectory/"vs.dxbc");
            auto& vs=vertexShaders[vertexKey];
            if(!vs)require(device->CreateVertexShader(vertex.data(),vertex.size(),nullptr,&vs),"Primary original shared VS");
            draw.vertex=vs;
            auto& ps=pixelShaders[pixelKey];
            if(!ps) {
                auto pixel=read(source.mediaDirectory/"ps.dxbc");
                require(device->CreatePixelShader(pixel.data(),pixel.size(),nullptr,&ps),"Primary original shared PS");
            }
            draw.pixel=ps;
            struct Element{std::string name;UINT index{},format{},slot{},offset{};};
            std::vector<Element> elements;Element element;
            NativePreparedInputFile layout(source.mediaDirectory/"layout.txt");
            if(!layout)throw std::runtime_error("Primary source layout is absent");
            std::string layoutKey=std::to_string(vertexKey);
            while(layout>>element.name>>element.index>>element.format>>element.slot>>element.offset) {
                elements.push_back(element);
                layoutKey+="/"+element.name+":"+std::to_string(element.index)+":"+std::to_string(element.format)+":"+
                    std::to_string(element.slot)+":"+std::to_string(element.offset);
            }
            auto& inputLayout=layouts[layoutKey];
            if(!inputLayout) {
                std::vector<D3D11_INPUT_ELEMENT_DESC> description;
                for(const auto& entry:elements)description.push_back({entry.name.c_str(),entry.index,DXGI_FORMAT(entry.format),
                    entry.slot,entry.offset,D3D11_INPUT_PER_VERTEX_DATA,0});
                require(device->CreateInputLayout(description.data(),UINT(description.size()),vertex.data(),vertex.size(),&inputLayout),"Primary original shared layout");
            }
            draw.layout=inputLayout;
            UINT slot{},stride{};std::string file;
            NativePreparedInputFile streams(source.mediaDirectory/"streams.txt");
            if(!streams)throw std::runtime_error("Primary source streams are absent");
            while(streams>>slot>>stride>>file) {
                if(slot!=draw.vertexBuffers.size())throw std::runtime_error("Primary original stream order differs");
                auto path=nativePreparedFilePath(source.mediaDirectory/std::filesystem::u8path(file));
                auto& buffer=vertices[path];if(!buffer)buffer=immutableBuffer(device,read(path),D3D11_BIND_VERTEX_BUFFER);
                draw.vertexBuffers.push_back(buffer);draw.strides.push_back(stride);
            }
            auto indexPath=nativePreparedFilePath(source.mediaDirectory/source.indexRelative);
            auto& indexBuffer=indices[indexPath];
            if(!indexBuffer)indexBuffer=immutableBuffer(device,read(indexPath),D3D11_BIND_INDEX_BUFFER);
            draw.indices=indexBuffer;
            auto& raster=rasters[source.raster];
            if(!raster) {
                D3D11_RASTERIZER_DESC description{};description.FillMode=D3D11_FILL_MODE(source.raster[0]);
                description.CullMode=D3D11_CULL_MODE(source.raster[1]);description.FrontCounterClockwise=source.raster[2];
                description.DepthClipEnable=TRUE;
                require(device->CreateRasterizerState(&description,&raster),"Primary source shared raster");
            }
            draw.raster=raster;
            UINT resource{},view{};NativePreparedInputFile textureInputs(source.mediaDirectory/"textures-native.txt");
            if(!textureInputs)throw std::runtime_error("Primary source texture bindings are absent");
            while(textureInputs>>slot>>resource>>view>>file) {
                if(slot>=draw.textures.size())throw std::runtime_error("Primary texture slot");
                auto path=nativePreparedFilePath(source.mediaDirectory/std::filesystem::u8path(file));
                const auto key=std::make_pair(path,std::array<UINT,2>{resource,view});
                auto& texture=textures[key];
                if(!texture)texture=loadNativeModelTexture(device,path,DXGI_FORMAT(resource),DXGI_FORMAT(view));
                draw.textures[slot]=texture;
            }
            NativePreparedInputFile samplerInputs(source.mediaDirectory/"samplers-native.txt");
            if(!samplerInputs)throw std::runtime_error("Primary source sampler bindings are absent");
            while(samplerInputs>>slot>>file) {
                if(slot>=draw.samplers.size())throw std::runtime_error("Primary sampler slot");
                auto bytes=read(source.mediaDirectory/std::filesystem::u8path(file));
                if(bytes.size()!=sizeof(D3D11_SAMPLER_DESC))throw std::runtime_error("Primary sampler descriptor");
                static_assert(sizeof(D3D11_SAMPLER_DESC)==sizeof(std::array<UINT,13>));
                std::array<UINT,13> key{};std::memcpy(key.data(),bytes.data(),bytes.size());
                auto& sampler=samplers[key];
                if(!sampler) {
                    D3D11_SAMPLER_DESC description{};std::memcpy(&description,bytes.data(),bytes.size());
                    require(device->CreateSamplerState(&description,&sampler),"Primary original shared sampler");
                }
                draw.samplers[slot]=sampler;
            }
            result->draws.push_back(std::move(draw));
        }
        D3D11_BLEND_DESC blend{};blend.IndependentBlendEnable=TRUE;
        for(UINT i=0;i<5;++i){blend.RenderTarget[i].BlendEnable=FALSE;blend.RenderTarget[i].RenderTargetWriteMask=15;}
        require(device->CreateBlendState(&blend,&result->blend),"Primary original five MRT blend");
        return result;
    }

    static std::shared_ptr<const NativePrimaryModelGpuMedia> load(ID3D11Device* device,
            const NativePrimaryModelGpuDeclaration& source) {
        NativePreparedInputFile input(source.directory/"primary-gpu-media.txt");
        if(line(input)!="SAN14_PRIMARY_GPU_MEDIA 2"||wide(line(input))!=source.logicalKey||wide(line(input))!=source.identity)
            throw std::runtime_error("Primary GPU package differs from its declared source identity");
        std::size_t count{};input>>count;
        if(!input)throw std::runtime_error("Primary GPU roster count is absent");
        std::vector<NativePrimaryModelGpuDrawSource> draws;draws.reserve(count);
        for(std::size_t i=0;i<count;++i) {
            std::size_t index{};NativePrimaryModelGpuDrawSource draw;
            input>>index>>draw.shaderFile>>draw.vertexProgram>>draw.pixelProgram>>draw.relativeDirectory;
            if(!input||index!=i)throw std::runtime_error("Primary GPU roster order is incomplete");
            draw.mediaDirectory=source.directory/std::filesystem::u8path(draw.relativeDirectory);
            readNativePrimaryModelGpuGeometry(input,draw);
            draws.push_back(std::move(draw));
        }
        auto result=load(device,draws);
        std::size_t localeCount{};input>>localeCount;
        if(!input||!localeCount)throw std::runtime_error("Primary pass locale roster is absent");
        for(std::size_t i=0;i<localeCount;++i) {
            std::string locale,forward,shadow;std::size_t templates{};
            input>>locale>>forward>>shadow>>templates;
            if(!input)throw std::runtime_error("Primary pass static directory declaration is incomplete");
            auto media=NativePrimaryPassGpuMedia::load(device,source.directory/std::filesystem::u8path(forward),
                source.directory/std::filesystem::u8path(shadow),templates);
            if(!result->passes.emplace(wide(locale),std::move(media)).second)
                throw std::runtime_error("Primary pass locale is duplicated");
        }
        return result;
    }
    static NativePrimaryModelGpuReference readReference(const std::filesystem::path& path) {
        NativePreparedInputFile input(path);
        if(line(input)!="SAN14_PRIMARY_GPU_REFERENCE 2")
            throw std::runtime_error("Primary GPU opening reference has an unsupported format");
        NativePrimaryModelGpuReference result{wide(line(input)),wide(line(input)),wide(line(input))};
        if(result.logicalKey.empty()||result.identity.empty()||result.locale.empty())
            throw std::runtime_error("Primary GPU opening source identity is absent");
        return result;
    }
private:
    static void require(HRESULT result,const char* what) {
        if(FAILED(result))throw std::runtime_error(std::string(what)+" HRESULT="+std::to_string(std::uint32_t(result)));
    }
    static Microsoft::WRL::ComPtr<ID3D11Buffer> immutableBuffer(ID3D11Device* device,
            const std::vector<char>& bytes,UINT bind) {
        if(bytes.empty()||bytes.size()>UINT_MAX)
            throw std::runtime_error("Primary immutable geometry buffer size is invalid");
        D3D11_BUFFER_DESC description{};description.ByteWidth=UINT(bytes.size());
        description.Usage=D3D11_USAGE_IMMUTABLE;description.BindFlags=bind;
        D3D11_SUBRESOURCE_DATA data{};data.pSysMem=bytes.data();
        Microsoft::WRL::ComPtr<ID3D11Buffer> buffer;
        require(device->CreateBuffer(&description,&data,&buffer),"Primary original immutable geometry");
        return buffer;
    }
    static std::vector<char> read(const std::filesystem::path& path) {
        NativePreparedInputFile input(path,std::ios::binary);
        if(!input)throw std::runtime_error("Primary prepared media is absent: "+path.string());
        std::vector<char> bytes{std::istreambuf_iterator<char>(input),std::istreambuf_iterator<char>()};
        if(input.bad()||bytes.empty())throw std::runtime_error("Primary prepared media is unreadable or empty");
        return bytes;
    }
    static std::string line(NativePreparedInputFile& input) {
        std::string value;
        if(!std::getline(input,value))throw std::runtime_error("Primary GPU manifest is incomplete");
        if(!value.empty()&&value.back()=='\r')value.pop_back();
        return value;
    }
    static std::wstring wide(const std::string& value) {
        if(value.empty())return {};
        int size=MultiByteToWideChar(CP_UTF8,MB_ERR_INVALID_CHARS,value.data(),int(value.size()),nullptr,0);
        if(!size)throw std::runtime_error("Primary GPU manifest identity is not UTF-8");
        std::wstring result(size,L'\0');
        if(MultiByteToWideChar(CP_UTF8,MB_ERR_INVALID_CHARS,value.data(),int(value.size()),result.data(),size)!=size)
            throw std::runtime_error("Primary GPU manifest identity conversion failed");
        return result;
    }
};
inline NativePrimaryModelGpuReference readNativePrimaryModelGpuReference(const std::filesystem::path& path) {
    return NativePrimaryModelGpuMedia::readReference(path);
}

// The explicit application device owns only current logical scene entries.
// Replacement publishes atomically; actual scene borrowers retain old banks.
// The caller holds the Native11Device execution role, as for Terrain residency.
class NativePrimaryModelGpuResidency final {
    struct Entry{std::wstring identity;std::shared_ptr<const NativePrimaryModelGpuMedia> media;};
    std::map<std::wstring,Entry> current_;
public:
    void replace(ID3D11Device* device,const std::vector<NativePrimaryModelGpuDeclaration>& sources) {
        if(sources.empty())throw std::runtime_error("The declared Primary GPU closure is empty");
        std::map<std::wstring,Entry> candidate;
        for(const auto& source:sources) {
            if(source.logicalKey.empty()||source.identity.empty())throw std::runtime_error("Primary GPU source identity is absent");
            std::shared_ptr<const NativePrimaryModelGpuMedia> media;
            if(auto found=current_.find(source.logicalKey);found!=current_.end()&&found->second.identity==source.identity)
                media=found->second.media;
            else media=NativePrimaryModelGpuMedia::load(device,source);
            if(!candidate.emplace(source.logicalKey,Entry{source.identity,std::move(media)}).second)
                throw std::runtime_error("The active Primary GPU declaration is duplicated");
        }
        current_.swap(candidate);
    }
    std::shared_ptr<const NativePrimaryModelGpuMedia> borrow(const std::wstring& key,const std::wstring& identity)const {
        const auto found=current_.find(key);
        if(found==current_.end()||found->second.identity!=identity)
            throw std::runtime_error("Primary GPU media was not loaded for this actual source generation at startup");
        return found->second.media;
    }
};
} // namespace San14::Render
