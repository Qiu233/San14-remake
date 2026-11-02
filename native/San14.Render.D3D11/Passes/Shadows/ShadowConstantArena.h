#pragma once
#include <algorithm>
#include <array>
#include <cstring>
#include <limits>
#include <stdexcept>
#include <utility>
#include <vector>
#include <d3d11_1.h>
#include <wrl/client.h>

// The Shadow owner prepares each complete current row once, including the
// current Tree2 wind tail and cascade alpha bias. No geometry or draw state
// is retained here. The original ordered consumer selects these same rows.
struct NativeShadowConstantPacket {
    std::array<char,272> vertex{};
    std::array<char,48> pixel{};
    UINT vertexBytes{},pixelBytes{};
};
struct NativeShadowConstantWindow {
    ID3D11Buffer* buffer{};
    UINT firstConstant{},constantCount{};
};
struct NativeShadowConstantWindows {
    NativeShadowConstantWindow vertex,pixel;
};

// DEFAULT snapshots use the same D3D11.1 range mechanism as the ordinary
// Base arena. Shadow owns distinct packets, buffers and submission lifetime.
class NativeShadowConstantArena {
public:
    template<class PrepareRow>
    const std::vector<NativeShadowConstantWindows>* upload(ID3D11DeviceContext* context,
        std::size_t rowCount,PrepareRow&& prepareRow) {
        initialize(context);
        if(!context1_)return nullptr;
        packets_.resize(rowCount);windows_.assign(rowCount,{});
        std::size_t vertexBytes=0,pixelBytes=0;
        for(std::size_t i=0;i<rowCount;++i) {
            auto& packet=packets_[i];packet=prepareRow(i);
            auto& windows=windows_[i];
            if(!packet.vertexBytes||packet.vertexBytes>packet.vertex.size()||packet.pixelBytes>packet.pixel.size())
                throw std::runtime_error("Shadow constant packet exceeds its complete imported payload");
            const auto vertexWindow=align(packet.vertexBytes);
            windows.vertex.firstConstant=UINT(vertexBytes/16);windows.vertex.constantCount=UINT(vertexWindow/16);
            vertexBytes+=vertexWindow;
            if(packet.pixelBytes) {
                const auto pixelWindow=align(packet.pixelBytes);
                windows.pixel.firstConstant=UINT(pixelBytes/16);windows.pixel.constantCount=UINT(pixelWindow/16);
                pixelBytes+=pixelWindow;
            }
            if(vertexBytes>maximumCapacity||pixelBytes>maximumCapacity)
                throw std::runtime_error("Shadow constant arena exceeds Direct3D buffer size");
        }
        if(!rowCount)return &windows_;
        vertex_.prepare(device_.Get(),vertexBytes);
        if(pixelBytes)pixel_.prepare(device_.Get(),pixelBytes);
        for(std::size_t i=0;i<rowCount;++i) {
            const auto& packet=packets_[i];auto& windows=windows_[i];
            windows.vertex.buffer=vertex_.buffer.Get();
            std::memcpy(vertex_.bytes.data()+std::size_t(windows.vertex.firstConstant)*16,packet.vertex.data(),packet.vertexBytes);
            if(packet.pixelBytes) {
                windows.pixel.buffer=pixel_.buffer.Get();
                std::memcpy(pixel_.bytes.data()+std::size_t(windows.pixel.firstConstant)*16,packet.pixel.data(),packet.pixelBytes);
            }
        }
        context->UpdateSubresource(vertex_.buffer.Get(),0,nullptr,vertex_.bytes.data(),0,0);
        if(pixelBytes)context->UpdateSubresource(pixel_.buffer.Get(),0,nullptr,pixel_.bytes.data(),0,0);
        return &windows_;
    }
    ID3D11DeviceContext1* context()const{return context1_.Get();}
private:
    static constexpr std::size_t alignment=256;
    static constexpr std::size_t maximumCapacity=std::numeric_limits<UINT>::max()&~std::size_t(alignment-1);
    static std::size_t align(std::size_t bytes){return (bytes+alignment-1)&~(alignment-1);}
    struct Storage {
        Microsoft::WRL::ComPtr<ID3D11Buffer> buffer;
        std::vector<Microsoft::WRL::ComPtr<ID3D11Buffer>> retired;
        std::vector<char> bytes;
        std::size_t capacity{};
        void prepare(ID3D11Device* device,std::size_t needed) {
            if(needed>capacity) {
                const auto next=std::max(needed,std::min(maximumCapacity,std::max(std::size_t(65536),capacity*2)));
                D3D11_BUFFER_DESC description{};description.ByteWidth=UINT(next);
                description.Usage=D3D11_USAGE_DEFAULT;description.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
                Microsoft::WRL::ComPtr<ID3D11Buffer> candidate;
                check(device->CreateBuffer(&description,nullptr,&candidate),"Shadow complete frame constant arena");
                if(buffer)retired.emplace_back(std::move(buffer));
                buffer=std::move(candidate);capacity=next;
            }
            // UpdateSubresource reads the entire allocated ByteWidth. Zero the
            // full source, preserving old small-CB out-of-buffer zero reads.
            bytes.assign(capacity,0);
        }
    };
    void initialize(ID3D11DeviceContext* context) {
        if(initialized_)return;
        initialized_=true;
        Microsoft::WRL::ComPtr<ID3D11DeviceContext1> ranged;
        if(FAILED(context->QueryInterface(IID_PPV_ARGS(&ranged))))return;
        Microsoft::WRL::ComPtr<ID3D11Device> device;context->GetDevice(&device);
        D3D11_FEATURE_DATA_D3D11_OPTIONS options{};
        if(FAILED(device->CheckFeatureSupport(D3D11_FEATURE_D3D11_OPTIONS,&options,sizeof(options)))||!options.ConstantBufferOffsetting)return;
        context1_=std::move(ranged);device_=std::move(device);
    }
    bool initialized_{};
    Microsoft::WRL::ComPtr<ID3D11DeviceContext1> context1_;
    Microsoft::WRL::ComPtr<ID3D11Device> device_;
    Storage vertex_,pixel_;
    std::vector<NativeShadowConstantPacket> packets_;
    std::vector<NativeShadowConstantWindows> windows_;
};
