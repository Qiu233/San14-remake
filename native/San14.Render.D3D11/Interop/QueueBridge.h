#pragma once
#include <array>
#include <cstdint>
#include <mutex>
#include <stdexcept>
#include <d3d12.h>
#include <wrl/client.h>

namespace San14::Render {
// Two actual queues on one device. Every cross-queue wait names a Signal
// already enqueued by its producer; no future/CPU-simulated completion exists.
class RenderQueueBridge final {
public:
    struct SlotState {
        std::uint64_t resource{},lastReadyRequest{},lastReadyValue{},lastConsumedValue{},lastNativeWaitValue{},lastGodotWaitReadyValue{};
    };
    struct State {
        std::uint32_t retiring{},retired{};
        std::uint64_t device{},nativeQueue{},godotQueue{},nativeFence{},godotFence{},lastNativeSignal{},lastGodotConsumed{},nativeCompleted{},godotCompleted{};
        std::array<SlotState,2> slots;
    };
    RenderQueueBridge(ID3D12Device* device,ID3D12CommandQueue* godotQueue,
        ID3D12Resource* first,ID3D12Resource* second) :device_(device),godotQueue_(godotQueue) {
        if(!device_||!godotQueue_||!first||!second||first==second)throw E_INVALIDARG;
        if(godotQueue_->GetDesc().Type!=D3D12_COMMAND_LIST_TYPE_DIRECT)throw E_INVALIDARG;
        Microsoft::WRL::ComPtr<ID3D12Device> queueDevice;
        require(godotQueue_->GetDevice(IID_PPV_ARGS(&queueDevice)));
        if(queueDevice.Get()!=device_.Get())throw E_INVALIDARG;
        ID3D12Resource* resources[2]{first,second};
        for(std::size_t i=0;i<slots_.size();++i) {
            Microsoft::WRL::ComPtr<ID3D12Device> resourceDevice;
            require(resources[i]->GetDevice(IID_PPV_ARGS(&resourceDevice)));
            if(resourceDevice.Get()!=device_.Get())throw E_INVALIDARG;
            slots_[i].resource=resources[i];
        }
        D3D12_COMMAND_QUEUE_DESC description{};description.Type=D3D12_COMMAND_LIST_TYPE_DIRECT;
        description.NodeMask=godotQueue_->GetDesc().NodeMask;
        require(device_->CreateCommandQueue(&description,IID_PPV_ARGS(&nativeQueue_)));
        require(device_->CreateFence(0,D3D12_FENCE_FLAG_NONE,IID_PPV_ARGS(&nativeFence_)));
        require(device_->CreateFence(0,D3D12_FENCE_FLAG_NONE,IID_PPV_ARGS(&godotFence_)));
    }
    ID3D12Device* device()const noexcept{return device_.Get();}
    ID3D12CommandQueue* nativeQueue()const noexcept{return nativeQueue_.Get();}
    ID3D12Fence* nativeFence()const noexcept{return nativeFence_.Get();}

    // Called only after a real Godot submission, including initial clears and
    // the final detached Canvas submission. Queue enqueue never waits for GPU.
    std::uint64_t signalConsumed() {
        std::lock_guard lock(gate_);requireOpen();
        const auto value=next(godotIssued_);
        require(godotQueue_->Signal(godotFence_.Get(),value));godotIssued_=value;
        for(auto& slot:slots_)slot.state.lastConsumedValue=value;
        return value;
    }
    void claimMapOwner() {
        std::lock_guard lock(gate_);requireOpen();
        if(!godotIssued_)throw std::runtime_error("Independent Map requires the actual initial Godot submission Signal");
        if(ownerClaimed_)throw std::runtime_error("Independent Map bridge already belongs to its Native owner");
        ownerClaimed_=true;
    }
    // Closing-only queue tail after admission stops and jobs join. This is
    // an actual GPU Signal for already enqueued work, not a Canvas receipt
    // or a flush of Godot's still-unsubmitted command lists.
    std::uint64_t sealGodotTail() {
        std::lock_guard lock(gate_);requireOpen();
        const auto value=next(godotIssued_);
        require(godotQueue_->Signal(godotFence_.Get(),value));godotIssued_=value;
        for(auto& slot:slots_)slot.state.lastConsumedValue=value;
        retiring_=true;return value;
    }
    void requireOutput(ID3D12Resource* resource)const {
        std::lock_guard lock(gate_);requireOpen();(void)find(resource);
    }
    // Native writes wait only for the last Signal already queued by Godot.
    // Output borrowers/admission remain the caller's existing protocol.
    void waitBeforeNativeWrite(ID3D12Resource* resource) {
        std::lock_guard lock(gate_);requireOpen();auto& slot=find(resource);
        if(!godotIssued_)throw std::runtime_error("Output has no actual initial Godot consumer submission");
        require(nativeQueue_->Wait(godotFence_.Get(),godotIssued_));
        slot.state.lastNativeWaitValue=godotIssued_;
    }
    // Adoption/drain has the same real Godot dependency as the former shared
    // queue tail. Also legal during final destruction: no future values exist.
    void waitForConsumers() {
        std::lock_guard lock(gate_);
        if(godotIssued_)require(nativeQueue_->Wait(godotFence_.Get(),godotIssued_));
    }
    // Shared by the original allocator/output completion fence and ready
    // publications, preserving one monotonic Native timeline.
    std::uint64_t signalNative() {
        std::lock_guard lock(gate_);return signalNativeLocked();
    }
    void publishReady(ID3D12Resource* resource,std::uint64_t request,std::uint64_t value) {
        std::lock_guard lock(gate_);requireOpen();auto& slot=find(resource);
        if(!value||value>nativeIssued_)throw std::runtime_error("Output ready value was not enqueued on the Native queue");
        slot.state.lastReadyRequest=request;slot.state.lastReadyValue=value;
    }
    std::uint64_t waitReady(ID3D12Resource* resource,std::uint64_t request) {
        std::lock_guard lock(gate_);requireOpen();auto& slot=find(resource);
        if(!slot.state.lastReadyValue||slot.state.lastReadyRequest!=request)
            throw std::runtime_error("Godot sampling requires this physical output's copied request");
        require(godotQueue_->Wait(nativeFence_.Get(),slot.state.lastReadyValue));
        slot.state.lastGodotWaitReadyValue=slot.state.lastReadyValue;
        return slot.state.lastReadyValue;
    }
    // Retirement worker only, after Native jobs joined, actual final Godot
    // Signal was enqueued, and original Owner drain flushed remaining work.
    // No bridge mutex is held across the CPU completion wait.
    void retire() {
        std::uint64_t value{};
        {
            std::lock_guard lock(gate_);
            if(retired_)return;
            retiring_=true;
            if(!retirementValue_) {
                if(godotIssued_)require(nativeQueue_->Wait(godotFence_.Get(),godotIssued_));
                retirementValue_=signalNativeLocked();
            }
            value=retirementValue_;
        }
        try {
            const auto completed=completedValue(nativeFence_.Get());
            if(completed<value) {
                HANDLE event=CreateEventW(nullptr,FALSE,FALSE,nullptr);
                if(!event)throw HRESULT_FROM_WIN32(GetLastError());
                const auto armed=nativeFence_->SetEventOnCompletion(value,event);
                if(FAILED(armed)){CloseHandle(event);require(armed);}
                const auto result=WaitForSingleObject(event,INFINITE);const auto error=GetLastError();CloseHandle(event);
                if(result!=WAIT_OBJECT_0)throw HRESULT_FROM_WIN32(error);
                (void)completedValue(nativeFence_.Get());
            }
            std::lock_guard lock(gate_);retired_=true;
        }catch(...) {
            std::lock_guard lock(gate_);
            if(FAILED(device_->GetDeviceRemovedReason()))retired_=true;
            throw;
        }
    }
    bool canDestroy()const {
        std::lock_guard lock(gate_);
        return retired_||FAILED(device_->GetDeviceRemovedReason());
    }
    State state()const {
        std::lock_guard lock(gate_);State result{};
        result.retiring=retiring_?1u:0u;result.retired=retired_?1u:0u;
        result.device=reinterpret_cast<std::uint64_t>(device_.Get());
        result.nativeQueue=reinterpret_cast<std::uint64_t>(nativeQueue_.Get());
        result.godotQueue=reinterpret_cast<std::uint64_t>(godotQueue_.Get());
        result.nativeFence=reinterpret_cast<std::uint64_t>(nativeFence_.Get());
        result.godotFence=reinterpret_cast<std::uint64_t>(godotFence_.Get());
        result.lastNativeSignal=nativeIssued_;result.lastGodotConsumed=godotIssued_;
        result.nativeCompleted=completedValue(nativeFence_.Get());result.godotCompleted=completedValue(godotFence_.Get());
        for(std::size_t i=0;i<slots_.size();++i){result.slots[i]=slots_[i].state;result.slots[i].resource=reinterpret_cast<std::uint64_t>(slots_[i].resource.Get());}
        return result;
    }
private:
    struct Slot {Microsoft::WRL::ComPtr<ID3D12Resource> resource;SlotState state;};
    static void require(HRESULT result){if(FAILED(result))throw result;}
    static std::uint64_t next(std::uint64_t value) {
        if(value>=UINT64_MAX-1)throw std::overflow_error("GPU queue timeline exhausted");
        return value+1;
    }
    void requireOpen()const {if(retiring_)throw std::runtime_error("Queue bridge is retiring");}
    Slot& find(ID3D12Resource* resource) {
        for(auto& slot:slots_)if(slot.resource.Get()==resource)return slot;
        throw E_INVALIDARG;
    }
    const Slot& find(ID3D12Resource* resource)const {
        for(const auto& slot:slots_)if(slot.resource.Get()==resource)return slot;
        throw E_INVALIDARG;
    }
    std::uint64_t signalNativeLocked() {
        const auto value=next(nativeIssued_);require(nativeQueue_->Signal(nativeFence_.Get(),value));
        nativeIssued_=value;return value;
    }
    std::uint64_t completedValue(ID3D12Fence* fence)const {
        const auto value=fence->GetCompletedValue();
        if(value==UINT64_MAX){require(device_->GetDeviceRemovedReason());throw DXGI_ERROR_DEVICE_REMOVED;}
        return value;
    }
    mutable std::mutex gate_;
    Microsoft::WRL::ComPtr<ID3D12Device> device_;
    Microsoft::WRL::ComPtr<ID3D12CommandQueue> nativeQueue_,godotQueue_;
    Microsoft::WRL::ComPtr<ID3D12Fence> nativeFence_,godotFence_;
    std::array<Slot,2> slots_;
    std::uint64_t nativeIssued_{},godotIssued_{},retirementValue_{};
    bool retiring_{},retired_{},ownerClaimed_{};
};
} // namespace San14::Render
