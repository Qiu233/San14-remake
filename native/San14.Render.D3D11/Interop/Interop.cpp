#include "Interop.h"
#include <algorithm>

namespace San14::Render {
using Microsoft::WRL::ComPtr;
namespace {
void require(HRESULT result) { if (FAILED(result)) throw result; }
DXGI_FORMAT copyFamily(DXGI_FORMAT format) {
    switch (format) {
    case DXGI_FORMAT_B8G8R8A8_TYPELESS:
    case DXGI_FORMAT_B8G8R8A8_UNORM:
    case DXGI_FORMAT_B8G8R8A8_UNORM_SRGB: return DXGI_FORMAT_B8G8R8A8_TYPELESS;
    case DXGI_FORMAT_R8G8B8A8_TYPELESS:
    case DXGI_FORMAT_R8G8B8A8_UNORM:
    case DXGI_FORMAT_R8G8B8A8_UNORM_SRGB: return DXGI_FORMAT_R8G8B8A8_TYPELESS;
    default: return DXGI_FORMAT_UNKNOWN;
    }
}
}

D3D11InteropOwner::D3D11InteropOwner(ID3D12Device* borrowedDevice,
        ID3D12CommandQueue* borrowedQueue,std::shared_ptr<RenderQueueBridge> bridge)
    :twelve_(borrowedDevice),queue_(bridge?bridge->nativeQueue():borrowedQueue),queueBridge_(std::move(bridge)) {
    // ComPtr AddRefs borrowed driver pointers; never Attach Godot's reference.
    if (!twelve_ || !queue_) throw E_POINTER;
    if (queue_->GetDesc().Type != D3D12_COMMAND_LIST_TYPE_DIRECT) throw E_INVALIDARG;
    ComPtr<ID3D12Device> queueDevice;
    require(queue_->GetDevice(IID_PPV_ARGS(&queueDevice)));
    if (queueDevice.Get() != twelve_.Get()) throw E_INVALIDARG;
    D3D12_FEATURE_DATA_D3D12_OPTIONS12 options{};
    require(twelve_->CheckFeatureSupport(D3D12_FEATURE_D3D12_OPTIONS12, &options, sizeof(options)));
    enhanced_ = options.EnhancedBarriersSupported != FALSE;
    IUnknown* queues[] = {queue_.Get()};
    require(D3D11On12CreateDevice(twelve_.Get(), D3D11_CREATE_DEVICE_BGRA_SUPPORT,
        nullptr, 0, queues, 1, 0, &eleven_, &context_, nullptr));
    require(eleven_.As(&on12_));
    if(queueBridge_) {
        if(queueBridge_->device()!=twelve_.Get())throw E_INVALIDARG;
        fence_=queueBridge_->nativeFence();doubleBufferedOutput_=true;
    } else require(twelve_->CreateFence(0, D3D12_FENCE_FLAG_NONE, IID_PPV_ARGS(&fence_)));
    completedEvent_ = CreateEventW(nullptr, FALSE, FALSE, nullptr);
    if (!completedEvent_) throw HRESULT_FROM_WIN32(GetLastError());
}

D3D11InteropOwner::D3D11InteropOwner(std::shared_ptr<Native11Bridge> bridge)
    :twelve_(bridge->godotDevice()),native11Bridge_(std::move(bridge)),
        eleven_(native11Bridge_->device()),context_(native11Bridge_->context()),
        fence_(native11Bridge_->nativeFence()),doubleBufferedOutput_(true) {
    auto deviceRole=deviceOwner()->acquire();
    if(deviceOwner()->sharedScenes())sceneState_=deviceOwner()->createSceneState();
    completedEvent_=CreateEventW(nullptr,FALSE,FALSE,nullptr);
    if(!completedEvent_)throw HRESULT_FROM_WIN32(GetLastError());
}

D3D11InteropOwner::SceneStateScope::SceneStateScope(D3D11InteropOwner& owner):owner_(owner) {
    if(owner_.sceneState_)owner_.deviceOwner()->context1()->SwapDeviceContextState(owner_.sceneState_.Get(),&previous_);
}
D3D11InteropOwner::SceneStateScope::~SceneStateScope() noexcept {
    if(previous_) {
        ComPtr<ID3DDeviceContextState> current;
        owner_.deviceOwner()->context1()->SwapDeviceContextState(previous_.Get(),&current);
        owner_.sceneState_=std::move(current);
    }
}

D3D11InteropOwner::~D3D11InteropOwner() noexcept {
    auto device=deviceOwner();
    auto deviceRole=device?device->acquire():Native11Device::Role{};
    SceneStateScope sceneState(*this);
    context_->ClearState();
    // ClearState releases retained pipeline bindings. Release wrappers before
    // flushing their deferred deletion; retain output and handoff storage until
    // queued work has drained.
    for (auto& output : outputs_) output->wrapped.Reset();
    try { drain(); } catch (HRESULT) { /* Device/queue failure during teardown. */ }
    activeOutput_ = nullptr;
    outputs_.clear();
    CloseHandle(completedEvent_);
}

void D3D11InteropOwner::drain() {
    if(native11Bridge_) {native11Bridge_->drainNative();return;}
    context_->Flush();
    if(queueBridge_)queueBridge_->waitForConsumers();
    waitUntil(signalQueue());
}

void D3D11InteropOwner::flushRecordedCommands() {
    context_->Flush();
}

std::uint64_t D3D11InteropOwner::signalQueue() {
    if(native11Bridge_)return fenceValue_=native11Bridge_->signalNative();
    if(queueBridge_)return fenceValue_=queueBridge_->signalNative();
    const auto value = ++fenceValue_;
    require(queue_->Signal(fence_.Get(), value));
    return value;
}

void D3D11InteropOwner::waitUntil(std::uint64_t value) {
    if (!value) return;
    const auto completed = fence_->GetCompletedValue();
    if (completed == UINT64_MAX) {
        require(twelve_->GetDeviceRemovedReason());
        throw DXGI_ERROR_DEVICE_REMOVED;
    }
    if (completed < value) {
        require(fence_->SetEventOnCompletion(value, completedEvent_));
        const DWORD waited = WaitForSingleObject(completedEvent_, INFINITE);
        if (waited != WAIT_OBJECT_0) throw HRESULT_FROM_WIN32(GetLastError());
    }
    if (fence_->GetCompletedValue() == UINT64_MAX) throw DXGI_ERROR_DEVICE_REMOVED;
}

void D3D11InteropOwner::prepareOutput(ID3D12Resource* borrowedTexture,OutputUse use) {
    if (!borrowedTexture) throw E_POINTER;
    if(native11Bridge_) {
        if(use!=OutputUse::RenderTarget&&use!=OutputUse::ShaderRead)throw E_INVALIDARG;
        auto* texture=native11Bridge_->requireOutput(borrowedTexture);
        auto found=std::find_if(outputs_.begin(),outputs_.end(),[&](const auto& slot){return slot->resource.Get()==borrowedTexture;});
        if(found==outputs_.end()) {
            if(outputs_.size()==2)throw E_INVALIDARG;
            auto slot=std::make_unique<OutputBinding>();slot->resource=borrowedTexture;slot->wrapped=texture;
            texture->GetDesc(&slot->description);slot->use=use;
            outputs_.push_back(std::move(slot));activeOutput_=outputs_.back().get();
        } else {activeOutput_=found->get();activeOutput_->use=use;}
        // No D3D12 handoff allocators or 11on12 wrappers exist in this mode.
        // Keep the existing physical-output completion wait on this worker.
        waitUntil(activeOutput_->lastSubmissionFence);return;
    }
    if(queueBridge_)queueBridge_->requireOutput(borrowedTexture);
    if (activeOutput_ && activeOutput_->resource.Get() == borrowedTexture && activeOutput_->use == use) {
        if (doubleBufferedOutput_) waitUntil(activeOutput_->lastSubmissionFence);
        return;
    }
    auto found = std::find_if(outputs_.begin(), outputs_.end(), [borrowedTexture](const auto& output) {
        return output->resource.Get() == borrowedTexture;
    });
    if (doubleBufferedOutput_) {
        // A list may not be submitted again while the GPU reads its previous
        // execution. This per-output token also protects use/allocator changes.
        if (found != outputs_.end()) waitUntil((*found)->lastSubmissionFence);
        else if (outputs_.size() >= 2) throw E_INVALIDARG;
    } else {
        // Existing render-thread/UI consumers retain their synchronous mode.
        drain();
    }
    if (found != outputs_.end() && (*found)->use == use) {
        activeOutput_ = found->get();
        return;
    }
    ComPtr<ID3D12Device> textureDevice;
    require(borrowedTexture->GetDevice(IID_PPV_ARGS(&textureDevice)));
    const auto desc = borrowedTexture->GetDesc();
    if (textureDevice.Get() != twelve_.Get() || desc.Dimension != D3D12_RESOURCE_DIMENSION_TEXTURE2D ||
        desc.DepthOrArraySize != 1 || desc.MipLevels != 1 || desc.SampleDesc.Count != 1 ||
        !(desc.Flags & D3D12_RESOURCE_FLAG_ALLOW_RENDER_TARGET) || copyFamily(desc.Format) == DXGI_FORMAT_UNKNOWN)
        throw E_INVALIDARG;
    auto candidate = std::make_unique<OutputBinding>();
    candidate->resource = borrowedTexture;
    candidate->use = use;
    const auto state = enhanced_ ? D3D12_RESOURCE_STATE_COMMON :
        use==OutputUse::RenderTarget?D3D12_RESOURCE_STATE_RENDER_TARGET:D3D12_RESOURCE_STATE_PIXEL_SHADER_RESOURCE;
    D3D11_RESOURCE_FLAGS flags{D3D11_BIND_RENDER_TARGET};
    require(on12_->CreateWrappedResource(candidate->resource.Get(), &flags, state, state, IID_PPV_ARGS(&candidate->wrapped)));
    candidate->wrapped->GetDesc(&candidate->description);
    if (enhanced_) {
        for (unsigned i = 0; i < 2; ++i) {
            require(twelve_->CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE_DIRECT,
                IID_PPV_ARGS(&candidate->allocators[i])));
            require(twelve_->CreateCommandList(0, D3D12_COMMAND_LIST_TYPE_DIRECT,
                candidate->allocators[i].Get(), nullptr, IID_PPV_ARGS(&candidate->handoffs[i])));
            require(candidate->handoffs[i]->Close());
        }
        recordHandoff(*candidate, 0, true);
        recordHandoff(*candidate, 1, false);
    }
    // Publish only a completely prepared binding. A failed use transition
    // leaves the previous binding alive; native job failure stops publication.
    if (found == outputs_.end()) {
        outputs_.push_back(std::move(candidate));
        activeOutput_ = outputs_.back().get();
    } else {
        *found = std::move(candidate);
        activeOutput_ = found->get();
    }
}

void D3D11InteropOwner::recordHandoff(OutputBinding& output, unsigned index, bool toCommon) {
    require(output.allocators[index]->Reset());
    require(output.handoffs[index]->Reset(output.allocators[index].Get(), nullptr));
    D3D12_TEXTURE_BARRIER barrier{};
    const bool attachment=output.use==OutputUse::RenderTarget;
    const auto sync=attachment?D3D12_BARRIER_SYNC_RENDER_TARGET:D3D12_BARRIER_SYNC_ALL_SHADING;
    const auto access=attachment?D3D12_BARRIER_ACCESS_RENDER_TARGET:D3D12_BARRIER_ACCESS_SHADER_RESOURCE;
    const auto layout=attachment?D3D12_BARRIER_LAYOUT_RENDER_TARGET:D3D12_BARRIER_LAYOUT_SHADER_RESOURCE;
    barrier.SyncBefore = toCommon ? sync : D3D12_BARRIER_SYNC_ALL;
    barrier.SyncAfter = toCommon ? D3D12_BARRIER_SYNC_ALL : sync;
    barrier.AccessBefore = toCommon ? access : D3D12_BARRIER_ACCESS_COMMON;
    barrier.AccessAfter = toCommon ? D3D12_BARRIER_ACCESS_COMMON : access;
    barrier.LayoutBefore = toCommon ? layout : D3D12_BARRIER_LAYOUT_COMMON;
    barrier.LayoutAfter = toCommon ? D3D12_BARRIER_LAYOUT_COMMON : layout;
    barrier.pResource = output.resource.Get();
    barrier.Subresources.IndexOrFirstMipLevel = 0xFFFFFFFFu;
    D3D12_BARRIER_GROUP group{};
    group.Type = D3D12_BARRIER_TYPE_TEXTURE;
    group.NumBarriers = 1;
    group.pTextureBarriers = &barrier;
    output.handoffs[index]->Barrier(1, &group);
    require(output.handoffs[index]->Close());
}

void D3D11InteropOwner::copyFinal(ID3D11Texture2D* finalTexture,std::uint64_t request) {
    if (!finalTexture || !activeOutput_) throw E_POINTER;
    auto& output = *activeOutput_;
    D3D11_TEXTURE2D_DESC source{};
    finalTexture->GetDesc(&source);
    if (source.Width != output.description.Width || source.Height != output.description.Height ||
        source.MipLevels != output.description.MipLevels || source.ArraySize != output.description.ArraySize ||
        source.SampleDesc.Count != output.description.SampleDesc.Count || source.SampleDesc.Quality != output.description.SampleDesc.Quality ||
        copyFamily(source.Format) != copyFamily(output.description.Format)) throw E_INVALIDARG;
    if(native11Bridge_) {
        // Shared simultaneous-access color has immutable COMMON layout.
        // Both real GPU fences protect whole-image read/write hazards.
        native11Bridge_->waitBeforeNativeWrite(output.resource.Get());
        context_->CopyResource(output.wrapped.Get(),finalTexture);
        output.lastSubmissionFence=signalQueue(); // Signal then native Flush.
        native11Bridge_->publishReady(output.resource.Get(),request,output.lastSubmissionFence);
        return;
    }
    // Submit prior Native Main work before D3D12 handoff commands on its
    // selected queue. The independent bridge then waits on Godot's actual
    // enqueued consumer Signal before this physical output may be written.
    context_->Flush();
    if(queueBridge_)queueBridge_->waitBeforeNativeWrite(output.resource.Get());
    if (enhanced_) {
        ID3D12CommandList* lists[] = {output.handoffs[0].Get()};
        queue_->ExecuteCommandLists(1, lists);
    }
    ID3D11Resource* resources[] = {output.wrapped.Get()};
    on12_->AcquireWrappedResources(resources, 1);
    context_->CopyResource(output.wrapped.Get(), finalTexture);
    on12_->ReleaseWrappedResources(resources, 1);
    context_->Flush();
    if (enhanced_) {
        ID3D12CommandList* lists[] = {output.handoffs[1].Get()};
        queue_->ExecuteCommandLists(1, lists);
    }
    // The selected Native queue orders writes and restored output layout.
    // Legacy Canvas shares that queue; independent Map sampling explicitly
    // waits for this already-enqueued ready Signal on Godot's queue. Keep the
    // token protecting the existing physical output/handoff allocator reuse.
    output.lastSubmissionFence = signalQueue();
    if(queueBridge_)queueBridge_->publishReady(output.resource.Get(),request,output.lastSubmissionFence);
    if (!doubleBufferedOutput_) waitUntil(output.lastSubmissionFence);
}

void D3D11InteropOwner::enableDoubleBufferedOutput() {
    if (outputs_.size() > 2) throw E_INVALIDARG;
    doubleBufferedOutput_ = true;
}

D3D11InteropOwner::OutputQueueState D3D11InteropOwner::outputQueueState() const {
    if(native11Bridge_)throw std::runtime_error("Native11 backend has no Native D3D12 queue; use bridge state_v2");
    if (outputs_.size() > 2) throw E_INVALIDARG;
    const auto completed = fence_->GetCompletedValue();
    if (completed == UINT64_MAX) {
        require(twelve_->GetDeviceRemovedReason());
        throw DXGI_ERROR_DEVICE_REMOVED;
    }
    OutputQueueState state{};
    state.device=reinterpret_cast<std::uint64_t>(twelve_.Get());
    state.queue=reinterpret_cast<std::uint64_t>(queue_.Get());
    state.issuedFence=fenceValue_;state.completedFence=completed;
    state.doubleBuffered=doubleBufferedOutput_?1u:0u;
    state.outputCount=static_cast<std::uint32_t>(outputs_.size());
    for (std::size_t i=0;i<outputs_.size();++i) {
        const auto& output=*outputs_[i];
        state.outputs[i]={reinterpret_cast<std::uint64_t>(output.resource.Get()),output.lastSubmissionFence,
            static_cast<std::uint32_t>(output.use),0};
    }
    return state;
}

} // namespace San14::Render
