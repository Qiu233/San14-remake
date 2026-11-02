#pragma once
#include <windows.h>
#include <d3d11.h>
#include <d3d12.h>
#include <d3d11on12.h>
#include <wrl/client.h>
#include <cstdint>
#include <memory>
#include <vector>
#include "QueueBridge.h"
#include "Native11Bridge.h"

namespace San14::Render {
enum class OutputUse { RenderTarget, ShaderRead };

// Retained version of the verified 2026-10-07 bridge:
// labs/render-d3d11-e2e/integration-review/d3d11on12-probe/ProbeInterop.cpp
// and its README.md. Legacy owners use Godot's main direct queue; the
// explicit Map bridge supplies its independently owned Native DIRECT queue.
// First output use is a submitted RD attachment. After Canvas submits sampling,
// the caller supplies ShaderRead; copyFinal restores that same tracked layout. Retire this
// owner (and renderer resources using its device) before freeing the output RID.
// Native11 outputs instead use shared simultaneous-access color, whose physical
// layout remains COMMON; logical output use and both real GPU fences still apply.
class D3D11InteropOwner final {
public:
    D3D11InteropOwner(ID3D12Device* borrowedDevice, ID3D12CommandQueue* borrowedQueue,
        std::shared_ptr<RenderQueueBridge> bridge={});
    explicit D3D11InteropOwner(std::shared_ptr<Native11Bridge> bridge);
    ~D3D11InteropOwner() noexcept;
    D3D11InteropOwner(const D3D11InteropOwner&) = delete;
    D3D11InteropOwner& operator=(const D3D11InteropOwner&) = delete;

    ID3D11Device* device() const noexcept { return eleven_.Get(); }
    ID3D11DeviceContext* context() const noexcept { return context_.Get(); }
    std::shared_ptr<Native11Device> deviceOwner()const noexcept {
        return native11Bridge_?native11Bridge_->deviceOwner():nullptr;
    }
    class SceneStateScope final {
    public:
        explicit SceneStateScope(D3D11InteropOwner& owner);
        ~SceneStateScope() noexcept;
        SceneStateScope(const SceneStateScope&)=delete;
        SceneStateScope& operator=(const SceneStateScope&)=delete;
    private:
        D3D11InteropOwner& owner_;
        Microsoft::WRL::ComPtr<ID3DDeviceContextState> previous_;
    };
    void prepareOutput(ID3D12Resource* borrowedTexture,OutputUse use=OutputUse::RenderTarget);
    // Exact GPU copy, no format/channel conversion or readback. Original final
    // BGRA TYPELESS90/sRGB91 needs BGRA output (90/87/91). The RGBA prototype
    // (27/28/29) is also accepted with a matching RGBA source, not a BGRA source.
    // Synchronous by default. Explicit double-buffered output mode retains
    // each submission fence and waits before that physical output is reused.
    void copyFinal(ID3D11Texture2D* finalTexture,std::uint64_t request=0);
    void drain();
    // Submit already-recorded private scene work without a CPU completion wait.
    void flushRecordedCommands();
    // Explicit opt-in for the normal Map's two retained physical outputs.
    // Submission is queue-ordered; CPU completion moves to output reuse/drain.
    void enableDoubleBufferedOutput();
    struct OutputSlotState {
        std::uint64_t resource{}, lastSubmissionFence{};
        std::uint32_t use{}, reserved{};
    };
    struct OutputQueueState {
        std::uint64_t device{}, queue{}, issuedFence{}, completedFence{};
        std::uint32_t doubleBuffered{}, outputCount{};
        OutputSlotState outputs[2];
    };
    OutputQueueState outputQueueState() const;
    bool isNative11() const noexcept {return native11Bridge_!=nullptr;}

private:
    struct OutputBinding {
        Microsoft::WRL::ComPtr<ID3D12Resource> resource;
        Microsoft::WRL::ComPtr<ID3D11Texture2D> wrapped;
        Microsoft::WRL::ComPtr<ID3D12CommandAllocator> allocators[2];
        Microsoft::WRL::ComPtr<ID3D12GraphicsCommandList7> handoffs[2];
        D3D11_TEXTURE2D_DESC description{};
        OutputUse use = OutputUse::RenderTarget;
        std::uint64_t lastSubmissionFence{};
    };
    // Output RIDs outlive this native owner. Each physical output retains its
    // own wrapper and immutable handoff lists until owner retirement.
    std::vector<std::unique_ptr<OutputBinding>> outputs_;
    OutputBinding* activeOutput_ = nullptr;
    void recordHandoff(OutputBinding& output, unsigned index, bool toCommon);
    std::uint64_t signalQueue();
    void waitUntil(std::uint64_t value);
    Microsoft::WRL::ComPtr<ID3D12Device> twelve_;
    Microsoft::WRL::ComPtr<ID3D12CommandQueue> queue_;
    std::shared_ptr<RenderQueueBridge> queueBridge_;
    std::shared_ptr<Native11Bridge> native11Bridge_;
    Microsoft::WRL::ComPtr<ID3D11Device> eleven_;
    Microsoft::WRL::ComPtr<ID3D11DeviceContext> context_;
    Microsoft::WRL::ComPtr<ID3DDeviceContextState> sceneState_;
    Microsoft::WRL::ComPtr<ID3D11On12Device> on12_;
    Microsoft::WRL::ComPtr<ID3D12Fence> fence_;
    HANDLE completedEvent_ = nullptr;
    std::uint64_t fenceValue_ = 0;
    bool enhanced_ = false;
    bool doubleBufferedOutput_ = false;
};

} // namespace San14::Render
