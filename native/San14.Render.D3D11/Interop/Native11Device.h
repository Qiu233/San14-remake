#pragma once
#include <windows.h>
#include <d3d11_4.h>
#include <d3d12.h>
#include <dxgi1_4.h>
#include <wrl/client.h>
#include <cstdint>
#include <iterator>
#include <memory>
#include <mutex>
#include <stdexcept>
#include "../Device/TerrainPrograms.h"
#include "../Device/PrimaryModelGpuMedia.h"
#include "../Device/RouteGpuReferences.h"
#include "../Device/DirectEffectPrograms.h"

namespace San14::Render {
class NativeRouteGpuResidency;
// Explicit process owner. No static registry, worker or invented GPU-ready
// token. Bridges borrow this owner and keep their own outputs and timelines.
class Native11Device final {
    template<class T>using ComPtr=Microsoft::WRL::ComPtr<T>;
public:
    using Role=std::unique_lock<std::recursive_mutex>;
    explicit Native11Device(ID3D12Device* godotDevice,bool sharedScenes=true)
        :godotDevice_(godotDevice),sharedScenes_(sharedScenes) {
        if(!godotDevice||godotDevice->GetNodeCount()!=1)throw E_INVALIDARG;
        check(godotDevice->QueryInterface(IID_PPV_ARGS(&godotIdentity_)));
        D3D12_FEATURE_DATA_D3D12_OPTIONS12 options{};
        check(godotDevice->CheckFeatureSupport(D3D12_FEATURE_D3D12_OPTIONS12,&options,sizeof(options)));
        if(!options.EnhancedBarriersSupported)throw E_NOINTERFACE;
        D3D12_FEATURE_DATA_D3D12_OPTIONS3 casting{};
        check(godotDevice->CheckFeatureSupport(D3D12_FEATURE_D3D12_OPTIONS3,&casting,sizeof(casting)));
        if(!casting.CastingFullyTypedFormatSupported)throw E_NOINTERFACE;
        const auto luid=godotDevice->GetAdapterLuid();
        adapterLuid_=(std::uint64_t(std::uint32_t(luid.HighPart))<<32)|luid.LowPart;
        ComPtr<IDXGIFactory4> factory;check(CreateDXGIFactory1(IID_PPV_ARGS(&factory)));
        ComPtr<IDXGIAdapter1> adapter;check(factory->EnumAdapterByLuid(luid,IID_PPV_ARGS(&adapter)));
        constexpr D3D_FEATURE_LEVEL levels[]{D3D_FEATURE_LEVEL_11_1,D3D_FEATURE_LEVEL_11_0};
        check(D3D11CreateDevice(adapter.Get(),D3D_DRIVER_TYPE_UNKNOWN,nullptr,
            D3D11_CREATE_DEVICE_BGRA_SUPPORT,levels,UINT(std::size(levels)),D3D11_SDK_VERSION,
            &device_,&featureLevel_,&context_));
        check(device_.As(&device5_));check(device_.As(&device1_));
        check(context_.As(&context4_));check(context_.As(&context1_));
        ComPtr<IDXGIDevice> dxgi;check(device_.As(&dxgi));ComPtr<IDXGIAdapter> actual;
        check(dxgi->GetAdapter(&actual));DXGI_ADAPTER_DESC description{};check(actual->GetDesc(&description));
        if(description.AdapterLuid.LowPart!=luid.LowPart||description.AdapterLuid.HighPart!=luid.HighPart)
            throw E_INVALIDARG;
        directEffectPrograms_=std::make_shared<NativeDirectEffectPrograms>(device_.Get());
        // Shared scenes require real pipeline-state isolation, not only a lock.
        if(sharedScenes_) {auto role=acquire();auto state=createSceneState();}
    }
    ~Native11Device() noexcept {
        auto role=acquire();
        context_->ClearState();context_->Flush();
    }
    Native11Device(const Native11Device&)=delete;
    Native11Device& operator=(const Native11Device&)=delete;

    Role acquire() {return Role(execution_);}
    ID3D12Device* godotDevice()const noexcept{return godotDevice_.Get();}
    ID3D11Device* device()const noexcept{return device_.Get();}
    ID3D11Device5* device5()const noexcept{return device5_.Get();}
    ID3D11DeviceContext* context()const noexcept{return context_.Get();}
    ID3D11DeviceContext1* context1()const noexcept{return context1_.Get();}
    ID3D11DeviceContext4* context4()const noexcept{return context4_.Get();}
    std::uint64_t adapterLuid()const noexcept{return adapterLuid_;}
    bool sharedScenes()const noexcept{return sharedScenes_;}
    std::shared_ptr<const NativeDirectEffectPrograms> borrowDirectEffectPrograms() {
        auto role=acquire();
        {std::lock_guard lock(lifetime_);if(closing_)throw std::runtime_error("Native11 device owner is closing");}
        return directEffectPrograms_;
    }
    void loadTerrainPrograms(const std::vector<NativeTerrainProgramDeclaration>& sources) {
        auto role=acquire();
        {std::lock_guard lock(lifetime_);if(closing_)throw std::runtime_error("Native11 device owner is closing");}
        terrainPrograms_.replace(device_.Get(),sources);
    }
    std::shared_ptr<const NativeTerrainPrograms> borrowTerrainPrograms(
            const std::wstring& logicalKey,const std::wstring& identity) {
        auto role=acquire();
        return terrainPrograms_.borrow(logicalKey,identity);
    }
    void loadPrimaryModels(const std::vector<NativePrimaryModelGpuDeclaration>& sources) {
        auto role=acquire();
        {std::lock_guard lock(lifetime_);if(closing_)throw std::runtime_error("Native11 device owner is closing");}
        primaryModels_.replace(device_.Get(),sources);
    }
    std::shared_ptr<const NativePrimaryModelGpuMedia> borrowPrimaryModels(
            const std::wstring& logicalKey,const std::wstring& identity) {
        auto role=acquire();
        {std::lock_guard lock(lifetime_);if(closing_)throw std::runtime_error("Native11 device owner is closing");}
        return primaryModels_.borrow(logicalKey,identity);
    }

    void loadRouteMedia(const std::vector<NativeRouteGpuDeclaration>& sources);
    std::shared_ptr<const NativeRouteGpuMedia> borrowRouteMedia(const std::wstring& key,const std::wstring& identity);

    void requireGodotQueue(ID3D12CommandQueue* queue)const {
        if(!queue||queue->GetDesc().Type!=D3D12_COMMAND_LIST_TYPE_DIRECT)throw E_INVALIDARG;
        ComPtr<ID3D12Device> actual;check(queue->GetDevice(IID_PPV_ARGS(&actual)));
        ComPtr<IUnknown> identity;check(actual.As(&identity));
        if(identity.Get()!=godotIdentity_.Get())
            throw std::runtime_error("Native11 process owner belongs to another Godot device generation");
    }
    ComPtr<ID3DDeviceContextState> createSceneState() {
        ComPtr<ID3DDeviceContextState> state;D3D_FEATURE_LEVEL chosen{};
        check(device1_->CreateDeviceContextState(0,&featureLevel_,1,D3D11_SDK_VERSION,
            __uuidof(ID3D11Device1),&chosen,&state));
        if(chosen!=featureLevel_||!state)throw E_UNEXPECTED;
        return state;
    }
    // Called by explicit device_destroy while its handle remains valid. A
    // failed child check never consumes the handle and can be retried later.
    bool beginClose() {
        std::lock_guard lock(lifetime_);
        if(bridges_)return false;
        closing_=true;return true;
    }
    class BridgeLease final {
    public:
        explicit BridgeLease(std::shared_ptr<Native11Device> owner):owner_(std::move(owner)) {
            if(!owner_)throw E_POINTER;
            std::lock_guard lock(owner_->lifetime_);
            if(owner_->closing_)throw std::runtime_error("Native11 device owner is closing");
            ++owner_->bridges_;
        }
        ~BridgeLease() noexcept {
            std::lock_guard lock(owner_->lifetime_);--owner_->bridges_;
        }
        BridgeLease(const BridgeLease&)=delete;
        BridgeLease& operator=(const BridgeLease&)=delete;
        const std::shared_ptr<Native11Device>& owner()const noexcept{return owner_;}
    private:
        std::shared_ptr<Native11Device> owner_;
    };
private:
    static void check(HRESULT hr){if(FAILED(hr))throw hr;}
    // Recursion is only for nested pieces of the same complete transaction:
    // Owner -> interop -> bridge Wait/Signal/drain and member destruction.
    std::recursive_mutex execution_;
    std::mutex lifetime_;std::uint64_t bridges_{};bool closing_{};
    ComPtr<ID3D12Device> godotDevice_;ComPtr<IUnknown> godotIdentity_;
    ComPtr<ID3D11Device> device_;ComPtr<ID3D11Device1> device1_;ComPtr<ID3D11Device5> device5_;
    ComPtr<ID3D11DeviceContext> context_;ComPtr<ID3D11DeviceContext1> context1_;ComPtr<ID3D11DeviceContext4> context4_;
    D3D_FEATURE_LEVEL featureLevel_{};std::uint64_t adapterLuid_{};bool sharedScenes_{};
    NativeTerrainProgramResidency terrainPrograms_;
    NativePrimaryModelGpuResidency primaryModels_;
    std::shared_ptr<NativeRouteGpuResidency> routeMedia_;
    std::shared_ptr<const NativeDirectEffectPrograms> directEffectPrograms_;
};
} // namespace San14::Render
