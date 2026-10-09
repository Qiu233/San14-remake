#include "Interop.h"

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
        ID3D12CommandQueue* borrowedQueue) : twelve_(borrowedDevice), queue_(borrowedQueue) {
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
    require(twelve_->CreateFence(0, D3D12_FENCE_FLAG_NONE, IID_PPV_ARGS(&fence_)));
    if (enhanced_) {
        for (unsigned i = 0; i < 2; ++i) {
            require(twelve_->CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE_DIRECT,
                IID_PPV_ARGS(&allocators_[i])));
            require(twelve_->CreateCommandList(0, D3D12_COMMAND_LIST_TYPE_DIRECT,
                allocators_[i].Get(), nullptr, IID_PPV_ARGS(&handoffs_[i])));
            require(handoffs_[i]->Close());
        }
    }
    completedEvent_ = CreateEventW(nullptr, FALSE, FALSE, nullptr);
    if (!completedEvent_) throw HRESULT_FROM_WIN32(GetLastError());
}

D3D11InteropOwner::~D3D11InteropOwner() noexcept {
    context_->ClearState();
    wrapped_.Reset();
    // ClearState releases retained pipeline bindings; flush and fence complete
    // queued native work and deferred wrapper deletion before COM retirement.
    try { drain(); } catch (HRESULT) { /* Device/queue failure during teardown. */ }
    CloseHandle(completedEvent_);
}

void D3D11InteropOwner::drain() {
    context_->Flush();
    waitQueue();
}

void D3D11InteropOwner::waitQueue() {
    const auto value = ++fenceValue_;
    require(queue_->Signal(fence_.Get(), value));
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
    if (borrowedTexture == output_.Get() && use==outputUse_) return;
    ComPtr<ID3D12Device> textureDevice;
    require(borrowedTexture->GetDevice(IID_PPV_ARGS(&textureDevice)));
    const auto desc = borrowedTexture->GetDesc();
    if (textureDevice.Get() != twelve_.Get() || desc.Dimension != D3D12_RESOURCE_DIMENSION_TEXTURE2D ||
        desc.DepthOrArraySize != 1 || desc.MipLevels != 1 || desc.SampleDesc.Count != 1 ||
        !(desc.Flags & D3D12_RESOURCE_FLAG_ALLOW_RENDER_TARGET) || copyFamily(desc.Format) == DXGI_FORMAT_UNKNOWN)
        throw E_INVALIDARG;
    drain(); // Output replacement and allocator Reset only after completion.
    wrapped_.Reset();
    output_ = borrowedTexture;
    outputUse_=use;
    const auto state = enhanced_ ? D3D12_RESOURCE_STATE_COMMON :
        use==OutputUse::RenderTarget?D3D12_RESOURCE_STATE_RENDER_TARGET:D3D12_RESOURCE_STATE_PIXEL_SHADER_RESOURCE;
    D3D11_RESOURCE_FLAGS flags{D3D11_BIND_RENDER_TARGET};
    require(on12_->CreateWrappedResource(output_.Get(), &flags, state, state, IID_PPV_ARGS(&wrapped_)));
    wrapped_->GetDesc(&outputDesc_);
    if (enhanced_) {
        recordHandoff(0, true);
        recordHandoff(1, false);
    }
}

void D3D11InteropOwner::recordHandoff(unsigned index, bool toCommon) {
    require(allocators_[index]->Reset());
    require(handoffs_[index]->Reset(allocators_[index].Get(), nullptr));
    D3D12_TEXTURE_BARRIER barrier{};
    const bool attachment=outputUse_==OutputUse::RenderTarget;
    const auto sync=attachment?D3D12_BARRIER_SYNC_RENDER_TARGET:D3D12_BARRIER_SYNC_ALL_SHADING;
    const auto access=attachment?D3D12_BARRIER_ACCESS_RENDER_TARGET:D3D12_BARRIER_ACCESS_SHADER_RESOURCE;
    const auto layout=attachment?D3D12_BARRIER_LAYOUT_RENDER_TARGET:D3D12_BARRIER_LAYOUT_SHADER_RESOURCE;
    barrier.SyncBefore = toCommon ? sync : D3D12_BARRIER_SYNC_ALL;
    barrier.SyncAfter = toCommon ? D3D12_BARRIER_SYNC_ALL : sync;
    barrier.AccessBefore = toCommon ? access : D3D12_BARRIER_ACCESS_COMMON;
    barrier.AccessAfter = toCommon ? D3D12_BARRIER_ACCESS_COMMON : access;
    barrier.LayoutBefore = toCommon ? layout : D3D12_BARRIER_LAYOUT_COMMON;
    barrier.LayoutAfter = toCommon ? D3D12_BARRIER_LAYOUT_COMMON : layout;
    barrier.pResource = output_.Get();
    barrier.Subresources.IndexOrFirstMipLevel = 0xFFFFFFFFu;
    D3D12_BARRIER_GROUP group{};
    group.Type = D3D12_BARRIER_TYPE_TEXTURE;
    group.NumBarriers = 1;
    group.pTextureBarriers = &barrier;
    handoffs_[index]->Barrier(1, &group);
    require(handoffs_[index]->Close());
}

void D3D11InteropOwner::copyFinal(ID3D11Texture2D* finalTexture) {
    if (!finalTexture || !wrapped_) throw E_POINTER;
    D3D11_TEXTURE2D_DESC source{};
    finalTexture->GetDesc(&source);
    if (source.Width != outputDesc_.Width || source.Height != outputDesc_.Height ||
        source.MipLevels != outputDesc_.MipLevels || source.ArraySize != outputDesc_.ArraySize ||
        source.SampleDesc.Count != outputDesc_.SampleDesc.Count || source.SampleDesc.Quality != outputDesc_.SampleDesc.Quality ||
        copyFamily(source.Format) != copyFamily(outputDesc_.Format)) throw E_INVALIDARG;
    // Submit prior native Main work before adding D3D12 handoff commands to
    // the shared queue (Microsoft Direct3D 11 on 12, Background sequencing).
    context_->Flush();
    if (enhanced_) {
        ID3D12CommandList* lists[] = {handoffs_[0].Get()};
        queue_->ExecuteCommandLists(1, lists);
    }
    ID3D11Resource* resources[] = {wrapped_.Get()};
    on12_->AcquireWrappedResources(resources, 1);
    context_->CopyResource(wrapped_.Get(), finalTexture);
    on12_->ReleaseWrappedResources(resources, 1);
    context_->Flush();
    if (enhanced_) {
        ID3D12CommandList* lists[] = {handoffs_[1].Get()};
        queue_->ExecuteCommandLists(1, lists);
    }
    // Same direct queue orders the RD primer, native draws/copy and restored
    // layout. This single wait also makes immutable handoff-list reuse legal.
    waitQueue();
}

} // namespace San14::Render
