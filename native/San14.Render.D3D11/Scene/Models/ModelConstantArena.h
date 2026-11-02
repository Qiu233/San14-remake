#pragma once
#include <algorithm>
#include <limits>
#include <utility>
#include "ModelRenderer.h"
#include "ModelFrame.h"

// Session-owned DEFAULT constant buffers. Each current draw retains its own
// complete, zero-padded shader window; original shaders still consume b0.
// UpdateSubresource snapshots the complete CPU source before it returns.
class NativeModelConstantArena {
public:
    NativeModelConstantSubmission upload(ID3D11DeviceContext* context,
        const NativeModelContinuousPlan& plan,const NativeModelInputs& resources) {
        initialize(context);
        entries_.clear();
        if(!context1_)return {}; // Original small-CB consumer remains available.
        if(!plan.visibleUnsupportedInstances.empty())
            throw std::runtime_error("Current view contains explicitly unclosed static Model profiles");
        entries_.resize(plan.draws.size());
        std::size_t vertexBytes=0,pixelBytes=0;
        for(std::size_t i=0;i<plan.draws.size();++i) {
            const auto& selected=plan.draws[i];auto& entry=entries_[i];
            entry.resources=&resources.draws.at(selected.primitiveIndex);
            const auto vertexSize=selected.extendedVertex.empty()?selected.vertex.size():selected.extendedVertex.size();
            const auto vertexWindow=windowBytes(vertexSize),pixelWindow=windowBytes(selected.pixel.size());
            entry.vertex.firstConstant=UINT(vertexBytes/16);entry.vertex.constantCount=UINT(vertexWindow/16);
            entry.pixel.firstConstant=UINT(pixelBytes/16);entry.pixel.constantCount=UINT(pixelWindow/16);
            vertexBytes+=vertexWindow;pixelBytes+=pixelWindow;
            if(vertexBytes>maximumCapacity||pixelBytes>maximumCapacity)
                throw std::runtime_error("Current model constant arena exceeds Direct3D buffer size");
        }
        if(entries_.empty())return {context1_.Get(),&entries_};
        vertex_.prepare(device_.Get(),vertexBytes);pixel_.prepare(device_.Get(),pixelBytes);
        for(std::size_t i=0;i<plan.draws.size();++i) {
            const auto& selected=plan.draws[i];auto& entry=entries_[i];
            entry.vertex.buffer=vertex_.buffer.Get();entry.pixel.buffer=pixel_.buffer.Get();
            const auto* vertex=selected.extendedVertex.empty()?selected.vertex.data():selected.extendedVertex.data();
            const auto vertexSize=selected.extendedVertex.empty()?selected.vertex.size():selected.extendedVertex.size();
            std::memcpy(vertex_.bytes.data()+std::size_t(entry.vertex.firstConstant)*16,vertex,vertexSize);
            std::memcpy(pixel_.bytes.data()+std::size_t(entry.pixel.firstConstant)*16,selected.pixel.data(),selected.pixel.size());
        }
        // A constant buffer cannot receive a partial UpdateSubresource. CPU
        // storage therefore covers the full allocated ByteWidth, including
        // all padding and unused capacity, not merely this frame's used bytes.
        context->UpdateSubresource(vertex_.buffer.Get(),0,nullptr,vertex_.bytes.data(),0,0);
        context->UpdateSubresource(pixel_.buffer.Get(),0,nullptr,pixel_.bytes.data(),0,0);
        return {context1_.Get(),&entries_};
    }
private:
    static constexpr std::size_t alignment=256;
    static constexpr std::size_t maximumCapacity=std::numeric_limits<UINT>::max()&~std::size_t(alignment-1);
    static std::size_t windowBytes(std::size_t bytes) {
        if(!bytes||bytes>D3D11_REQ_CONSTANT_BUFFER_ELEMENT_COUNT*16u)
            throw std::runtime_error("Model shader constant window exceeds the original 64 KiB shader limit");
        return (bytes+alignment-1)&~(alignment-1);
    }
    struct Storage {
        Microsoft::WRL::ComPtr<ID3D11Buffer> buffer;
        // Growth is geometric and infrequent. Retaining replaced allocations
        // until session retirement keeps borrowed submission pointers valid;
        // summed old capacity is bounded by the current allocation's scale.
        std::vector<Microsoft::WRL::ComPtr<ID3D11Buffer>> retired;
        std::vector<char> bytes;
        std::size_t capacity{};
        void prepare(ID3D11Device* device,std::size_t needed) {
            if(needed>capacity) {
                const auto grown=std::min(maximumCapacity,std::max(std::size_t(65536),capacity*2));
                const auto next=std::max(needed,grown);
                D3D11_BUFFER_DESC desc{};desc.ByteWidth=UINT(next);
                desc.Usage=D3D11_USAGE_DEFAULT;desc.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
                Microsoft::WRL::ComPtr<ID3D11Buffer> candidate;
                check(device->CreateBuffer(&desc,nullptr,&candidate),"Model frame constant arena");
                if(buffer)retired.emplace_back(std::move(buffer));
                buffer=std::move(candidate);capacity=next;
            }
            // Preserve original out-of-buffer zero reads inside the enlarged
            // aligned windows, even when selected entries or sizes change.
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
    std::vector<NativeModelDrawReference> entries_;
};
