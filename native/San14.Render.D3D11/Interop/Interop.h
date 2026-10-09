#pragma once
#include <windows.h>
#include <d3d11.h>
#include <d3d12.h>
#include <d3d11on12.h>
#include <wrl/client.h>
#include <cstdint>

namespace San14::Render {
enum class OutputUse { RenderTarget, ShaderRead };

// Retained version of the verified 2026-10-07 bridge:
// labs/render-d3d11-e2e/integration-review/d3d11on12-probe/ProbeInterop.cpp
// and its README.md. Call on Godot's render thread with its main direct queue.
// First output use is a submitted RD attachment. After Canvas submits sampling,
// the caller supplies ShaderRead; copyFinal restores that same tracked layout. Retire this
// owner (and renderer resources using its device) before freeing the output RID.
class D3D11InteropOwner final {
public:
    D3D11InteropOwner(ID3D12Device* borrowedDevice, ID3D12CommandQueue* borrowedQueue);
    ~D3D11InteropOwner() noexcept;
    D3D11InteropOwner(const D3D11InteropOwner&) = delete;
    D3D11InteropOwner& operator=(const D3D11InteropOwner&) = delete;

    ID3D11Device* device() const noexcept { return eleven_.Get(); }
    ID3D11DeviceContext* context() const noexcept { return context_.Get(); }
    void prepareOutput(ID3D12Resource* borrowedTexture,OutputUse use=OutputUse::RenderTarget);
    // Exact GPU copy, no format/channel conversion or readback. Original final
    // BGRA TYPELESS90/sRGB91 needs BGRA output (90/87/91). The RGBA prototype
    // (27/28/29) is also accepted with a matching RGBA source, not a BGRA source.
    // One completion wait covers both handoffs/copy and permits list reuse.
    void copyFinal(ID3D11Texture2D* finalTexture);
    void drain();

private:
    void recordHandoff(unsigned index, bool toCommon);
    void waitQueue();
    Microsoft::WRL::ComPtr<ID3D12Device> twelve_;
    Microsoft::WRL::ComPtr<ID3D12CommandQueue> queue_;
    Microsoft::WRL::ComPtr<ID3D11Device> eleven_;
    Microsoft::WRL::ComPtr<ID3D11DeviceContext> context_;
    Microsoft::WRL::ComPtr<ID3D11On12Device> on12_;
    Microsoft::WRL::ComPtr<ID3D12Resource> output_;
    Microsoft::WRL::ComPtr<ID3D11Texture2D> wrapped_;
    Microsoft::WRL::ComPtr<ID3D12Fence> fence_;
    Microsoft::WRL::ComPtr<ID3D12CommandAllocator> allocators_[2];
    Microsoft::WRL::ComPtr<ID3D12GraphicsCommandList7> handoffs_[2];
    D3D11_TEXTURE2D_DESC outputDesc_{};
    HANDLE completedEvent_ = nullptr;
    std::uint64_t fenceValue_ = 0;
    bool enhanced_ = false;
    OutputUse outputUse_ = OutputUse::RenderTarget;
};

} // namespace San14::Render
