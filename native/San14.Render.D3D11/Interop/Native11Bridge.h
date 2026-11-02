#pragma once
#include <windows.h>
#include <d3d11_4.h>
#include <d3d12.h>
#include <dxgi1_4.h>
#include <wrl/client.h>
#include <array>
#include <cstdint>
#include <limits>
#include <iterator>
#include <mutex>
#include <stdexcept>
#include "Native11Device.h"

namespace San14::Render {
// The two exported resources belong to Godot's actual D3D12 device. Only
// these color outputs are shared; every Main pass still uses native D3D11.
class Native11Bridge final {
    template<class T> using ComPtr=Microsoft::WRL::ComPtr<T>;
public:
    struct SlotState {
        std::uint64_t resource12{},resource11{},lastReadyRequest{},lastReadyValue{},
            lastConsumedValue{},lastNativeWaitValue{},lastGodotWaitReadyValue{};
    };
    struct State {
        std::uint32_t structBytes{232},version{2},backendKind{2},flags{};
        std::uint64_t godotDevice12{},godotQueue{},device11{},context11{},adapterLuid{},
            nativeReadyFence11{},nativeReadyFence12{},godotConsumedFence11{},godotConsumedFence12{},
            lastNativeSignal{},lastGodotConsumed{},nativeCompleted{},godotCompleted{};
        SlotState slots[2];
    };
    static_assert(sizeof(SlotState)==56&&sizeof(State)==232);

    Native11Bridge(ID3D12Device* device,ID3D12CommandQueue* queue,UINT width,UINT height)
        :Native11Bridge(std::make_shared<Native11Device>(device,false),queue,width,height) {}
    Native11Bridge(std::shared_ptr<Native11Device> device,ID3D12CommandQueue* queue,UINT width,UINT height)
        :deviceLease_(std::move(device)),godotDevice_(deviceOwner()->godotDevice()),
        godotQueue_(queue),device11_(deviceOwner()->device()),device5_(deviceOwner()->device5()),
        context11_(deviceOwner()->context()),context4_(deviceOwner()->context4()),
        width_(width),height_(height),adapterLuid_(deviceOwner()->adapterLuid()) {
        if(!width||!height)throw E_INVALIDARG;
        deviceOwner()->requireGodotQueue(queue);
        createFence(nativeReady12_,nativeReady11_);
        createFence(godotConsumed12_,godotConsumed11_);
        for(auto& slot:outputs_)createOutput(slot);
        completedEvent_=CreateEventW(nullptr,FALSE,FALSE,nullptr);
        if(!completedEvent_)throw HRESULT_FROM_WIN32(GetLastError());
    }
    ~Native11Bridge() noexcept {if(completedEvent_)CloseHandle(completedEvent_);}
    Native11Bridge(const Native11Bridge&)=delete;
    Native11Bridge& operator=(const Native11Bridge&)=delete;

    const std::shared_ptr<Native11Device>& deviceOwner()const noexcept{return deviceLease_.owner();}
    ID3D12Device* godotDevice()const noexcept{return godotDevice_.Get();}
    ID3D11Device* device()const noexcept{return device11_.Get();}
    ID3D11DeviceContext* context()const noexcept{return context11_.Get();}
    ID3D12Fence* nativeFence()const noexcept{return nativeReady12_.Get();}
    ID3D12Resource* output(unsigned index)const{return outputs_.at(index).resource12.Get();}
    std::uint64_t adapterLuid()const noexcept{return adapterLuid_;}
    UINT width()const noexcept{return width_;}
    UINT height()const noexcept{return height_;}

    // The caller owns the same serialized Native immediate-context role as
    // the existing Main worker. No Godot short bridge operation enters it.
    ID3D11Texture2D* requireOutput(ID3D12Resource* resource)const {
        for(const auto& slot:outputs_)if(slot.resource12.Get()==resource)return slot.resource11.Get();
        throw E_INVALIDARG;
    }
    void claimMapOwner() {
        std::lock_guard lock(gate_);
        if(claimed_||retiring_||retired_||!initialAdmitted_)
            throw std::runtime_error("Native11 owner needs its actual initial Godot submission and one owner");
        claimed_=true;
    }
    void waitBeforeNativeWrite(ID3D12Resource* resource) {
        auto deviceRole=deviceOwner()->acquire();
        std::uint64_t value{};
        {std::lock_guard lock(gate_);requireLive();value=find(resource).state.lastConsumedValue;}
        if(value)check(context4_->Wait(godotConsumed11_.Get(),value));
        {std::lock_guard lock(gate_);find(resource).state.lastNativeWaitValue=value;}
    }
    // Signal belongs to the native immediate command stream. Flush must follow
    // it before a value is published to a Godot GPU Wait; it is not completion.
    std::uint64_t signalNative() {
        auto deviceRole=deviceOwner()->acquire();
        std::uint64_t value{};
        {std::lock_guard lock(gate_);if(retired_)return lastNativeSignal_;
            value=next(nativeCounter_);}
        check(context4_->Signal(nativeReady11_.Get(),value));context11_->Flush();
        {std::lock_guard lock(gate_);lastNativeSignal_=value;}
        return value;
    }
    void publishReady(ID3D12Resource* resource,std::uint64_t request,std::uint64_t value) {
        std::lock_guard lock(gate_);requireLive();
        if(!value||value>lastNativeSignal_)throw E_INVALIDARG;
        auto& slot=find(resource);slot.state.lastReadyRequest=request;slot.state.lastReadyValue=value;
    }
    std::uint64_t waitReady(ID3D12Resource* resource,std::uint64_t request) {
        std::lock_guard lock(gate_);requireLive();auto& slot=find(resource);
        const auto value=slot.state.lastReadyValue;
        if(!value||slot.state.lastReadyRequest!=request||value>lastNativeSignal_)
            throw std::runtime_error("Native11 ready Wait requires the exact already-submitted copy");
        check(godotQueue_->Wait(nativeReady12_.Get(),value));
        slot.state.lastGodotWaitReadyValue=value;return value;
    }
    // Called only after an actual submitted Godot frame, including repeated
    // sampling. This conservative tail covers all uses of both physical slots.
    std::uint64_t signalConsumed() {
        std::lock_guard lock(gate_);requireLive();
        const auto value=signalGodotLocked();initialAdmitted_=true;return value;
    }
    std::uint64_t sealGodotTail() {
        std::lock_guard lock(gate_);
        if(retired_)return lastGodotConsumed_;
        if(retiring_)return sealedValue_;
        const auto value=signalGodotLocked();sealedValue_=value;retiring_=true;return value;
    }
    // Existing resource replacement/owner teardown calls this on the Native
    // worker. The consumer value is already enqueued; no future draw is needed.
    void drainNative() {
        auto deviceRole=deviceOwner()->acquire();
        std::uint64_t consumer{};
        {std::lock_guard lock(gate_);if(retired_)return;consumer=lastGodotConsumed_;}
        if(consumer)check(context4_->Wait(godotConsumed11_.Get(),consumer));
        waitNative(signalNative());
    }
    void retire() {
        auto deviceRole=deviceOwner()->acquire();
        {std::lock_guard lock(gate_);if(retired_)return;
            if(!retiring_||!sealedValue_)throw std::runtime_error("Native11 retirement needs its queued Godot tail seal");
            if(retireStarted_)throw std::runtime_error("Native11 retirement is already running");
            retireStarted_=true;}
        drainNative();
        {std::lock_guard lock(gate_);retired_=true;}
    }
    bool canDestroy()const {
        std::lock_guard lock(gate_);
        if(retired_)return true;
        // Import/framebuffer construction can fail before any bridge queue
        // operation. RD's patched COM/deferred owner retains recorded resources.
        if(claimed_||lastNativeSignal_||lastGodotConsumed_)return false;
        for(const auto& slot:outputs_)if(slot.state.lastGodotWaitReadyValue)return false;
        return true;
    }
    State state()const {
        std::lock_guard lock(gate_);State result;
        result.flags=(retiring_?1u:0u)|(retired_?2u:0u)|(initialAdmitted_?4u:0u)|(claimed_?8u:0u);
        result.godotDevice12=address(godotDevice_.Get());result.godotQueue=address(godotQueue_.Get());
        result.device11=address(device11_.Get());result.context11=address(context11_.Get());
        result.adapterLuid=adapterLuid_;
        result.nativeReadyFence11=address(nativeReady11_.Get());result.nativeReadyFence12=address(nativeReady12_.Get());
        result.godotConsumedFence11=address(godotConsumed11_.Get());result.godotConsumedFence12=address(godotConsumed12_.Get());
        result.lastNativeSignal=lastNativeSignal_;result.lastGodotConsumed=lastGodotConsumed_;
        result.nativeCompleted=nativeReady12_->GetCompletedValue();result.godotCompleted=godotConsumed12_->GetCompletedValue();
        for(unsigned i=0;i<2;++i)result.slots[i]=outputs_[i].state;
        return result;
    }
private:
    struct Output {ComPtr<ID3D12Resource> resource12;ComPtr<ID3D11Texture2D> resource11;SlotState state;};
    static void check(HRESULT value){if(FAILED(value))throw value;}
    template<class T>static std::uint64_t address(T* value){return reinterpret_cast<std::uint64_t>(value);}
    static std::uint64_t next(std::uint64_t& value) {
        if(value==(std::numeric_limits<std::uint64_t>::max)())throw E_BOUNDS;return ++value;
    }
    void requireLive()const {
        if(retiring_||retired_)throw std::runtime_error("Native11 output bridge is sealed or retired");
    }
    Output& find(ID3D12Resource* resource) {
        for(auto& slot:outputs_)if(slot.resource12.Get()==resource)return slot;
        throw E_INVALIDARG;
    }
    std::uint64_t signalGodotLocked() {
        const auto value=next(godotCounter_);check(godotQueue_->Signal(godotConsumed12_.Get(),value));
        lastGodotConsumed_=value;
        for(auto& slot:outputs_)slot.state.lastConsumedValue=value;
        return value;
    }
    void createFence(ComPtr<ID3D12Fence>& twelve,ComPtr<ID3D11Fence>& eleven) {
        check(godotDevice_->CreateFence(0,D3D12_FENCE_FLAG_SHARED,IID_PPV_ARGS(&twelve)));
        HANDLE handle=nullptr;check(godotDevice_->CreateSharedHandle(twelve.Get(),nullptr,GENERIC_ALL,nullptr,&handle));
        const auto hr=device5_->OpenSharedFence(handle,IID_PPV_ARGS(&eleven));CloseHandle(handle);check(hr);
    }
    void createOutput(Output& slot) {
        D3D12_HEAP_PROPERTIES heap{};heap.Type=D3D12_HEAP_TYPE_DEFAULT;heap.CreationNodeMask=heap.VisibleNodeMask=1;
        D3D12_RESOURCE_DESC desc{};desc.Dimension=D3D12_RESOURCE_DIMENSION_TEXTURE2D;
        desc.Width=width_;desc.Height=height_;desc.DepthOrArraySize=desc.MipLevels=1;
        desc.Format=DXGI_FORMAT_B8G8R8A8_UNORM;desc.SampleDesc.Count=1;desc.Layout=D3D12_TEXTURE_LAYOUT_UNKNOWN;
        desc.Flags=D3D12_RESOURCE_FLAG_ALLOW_RENDER_TARGET|D3D12_RESOURCE_FLAG_ALLOW_SIMULTANEOUS_ACCESS;
        check(godotDevice_->CreateCommittedResource(&heap,D3D12_HEAP_FLAG_SHARED,&desc,
            D3D12_RESOURCE_STATE_COMMON,nullptr,IID_PPV_ARGS(&slot.resource12)));
        HANDLE handle=nullptr;check(godotDevice_->CreateSharedHandle(slot.resource12.Get(),nullptr,GENERIC_ALL,nullptr,&handle));
        const auto hr=device5_->OpenSharedResource1(handle,IID_PPV_ARGS(&slot.resource11));CloseHandle(handle);check(hr);
        const auto actual=slot.resource12->GetDesc();D3D11_TEXTURE2D_DESC opened{};slot.resource11->GetDesc(&opened);
        if(actual.Flags!=desc.Flags||actual.Format!=desc.Format||actual.Width!=width_||actual.Height!=height_||
            opened.Width!=width_||opened.Height!=height_||opened.Format!=desc.Format||opened.MipLevels!=1||
            opened.ArraySize!=1||opened.SampleDesc.Count!=1||opened.SampleDesc.Quality!=0)throw E_INVALIDARG;
        slot.state.resource12=address(slot.resource12.Get());slot.state.resource11=address(slot.resource11.Get());
    }
    void waitNative(std::uint64_t value) {
        if(!value)return;auto completed=nativeReady12_->GetCompletedValue();
        if(completed==(std::numeric_limits<std::uint64_t>::max)())throw DXGI_ERROR_DEVICE_REMOVED;
        if(completed<value) {
            check(nativeReady12_->SetEventOnCompletion(value,completedEvent_));
            const auto result=WaitForSingleObject(completedEvent_,INFINITE);
            if(result!=WAIT_OBJECT_0)throw HRESULT_FROM_WIN32(GetLastError());
        }
        if(nativeReady12_->GetCompletedValue()==(std::numeric_limits<std::uint64_t>::max)())throw DXGI_ERROR_DEVICE_REMOVED;
    }
    // First declared, last destroyed: child count covers all bridge resources.
    Native11Device::BridgeLease deviceLease_;
    mutable std::mutex gate_;
    ComPtr<ID3D12Device> godotDevice_;ComPtr<ID3D12CommandQueue> godotQueue_;
    ComPtr<ID3D11Device> device11_;ComPtr<ID3D11Device5> device5_;
    ComPtr<ID3D11DeviceContext> context11_;ComPtr<ID3D11DeviceContext4> context4_;
    ComPtr<ID3D12Fence> nativeReady12_,godotConsumed12_;
    ComPtr<ID3D11Fence> nativeReady11_,godotConsumed11_;
    std::array<Output,2> outputs_;UINT width_{},height_{};std::uint64_t adapterLuid_{};
    std::uint64_t nativeCounter_{},godotCounter_{},lastNativeSignal_{},lastGodotConsumed_{},sealedValue_{};
    bool claimed_{},initialAdmitted_{},retiring_{},retireStarted_{},retired_{};HANDLE completedEvent_{};
};
} // namespace San14::Render
