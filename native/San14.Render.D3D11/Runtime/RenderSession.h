#pragma once
#include <coroutine>
#include <exception>
#include <utility>
#include <string>
#include <vector>
#include <filesystem>
#include <optional>
#include <stdexcept>
#include <functional>
#include <memory>
#include <d3d11.h>
#include "../Camera/CameraState.h"
#include "../Interop/RouteOverlayInput.h"
#include "../Device/RouteGpuReferences.h"
#include "../Interop/CurrentEffectInput.h"
#include "../Scene/Terrain/TerritoryOwnership.h"
#include "../Scene/Terrain/CommandMap.h"
#include "../Scene/Terrain/HexFocus.h"
#include "../Scene/Models/ArmyWorldPublication.h"
#include "../Scene/Models/ArmyUnitHeight.h"
#include "RenderConfiguration.h"
#include "MainRenderPhaseState.h"
#include "../Scene/Models/CityGateWorldPublication.h"
#include "../Scene/Models/AreaCapitalWorldPublication.h"
#include "../Passes/Environment/SeasonFrame.h"

namespace San14::Render {
struct NativeTerrainPrograms;
class NativeDirectEffectPrograms;
struct NativePrimaryModelGpuMedia;
struct NativePrimaryPassGpuMedia;
enum class FrameInputFlag : unsigned {
    PublishedCamera=1,
    Paused=2,
    InitialAnimationUpdate=4,
    HideTerritory=8,
    //A loaded Startup scene has Army resources, but no live publication owner.
    StartupArmyOwnerAbsent=0x80000
};
struct RenderInput {
    source_camera::Prefix camera{};
    source_camera::State controller{};
    int band{},fraction{};
    float deltaSeconds{};
    unsigned flags{}; // Stable interop mask; renderer uses FrameInputFlag.
    const std::uint8_t* ownershipColors{};
    std::uint64_t ownershipRevision{};
    std::uint32_t ownershipCount{};
    const float* ownershipMapColors{};
    std::uint32_t ownershipMapColorCount{};
    const std::uint8_t* armyWorld{};
    std::uint32_t armyWorldBytes{};
    const std::uint8_t* currentSeason{};
    std::uint32_t currentSeasonBytes{};
    source_camera::V4 currentRawSunPosition{};
    float currentCloudGeometry40{};
    const std::uint8_t* cityGateWorld{};
    std::uint32_t cityGateWorldBytes{};
    const std::uint8_t* areaCapitalWorld{};
    std::uint32_t areaCapitalWorldBytes{};
    const wchar_t* atmosphereResourceDirectory{};
    const wchar_t* patternResourceDirectory{};
    // Stable interop flags20..22:0 auto;1..5 actual Main override0..4.
    // The independent animation queue carries its own captured override.
    std::optional<std::uint32_t> mainModeOverride() const {
        const auto code=(flags>>20)&7u;
        if(code>5u)throw std::runtime_error("Native Main override flag domain");
        return code?std::optional<std::uint32_t>{code-1u}:std::nullopt;
    }
    bool contains(FrameInputFlag flag) const noexcept {
        return (flags & static_cast<unsigned>(flag))!=0;
    }
};
struct RenderOutput {
    ID3D11Texture2D* texture{}; // borrowed until the next render or session close
    source_camera::Prefix camera{};
    std::uint64_t request{};
};
struct AnimationState {
    uint64_t updates{};
    uint32_t armyPlayers{},cityGroups{},areaGroups{},fpsMode{};
    float firstBodyPhase{},firstFlagPhase{};
};
struct RenderHost {
    std::shared_ptr<const NativeDirectEffectPrograms> directEffectPrograms;
    std::uint32_t mainFlags{MainRenderPhaseState::FreshHostFlags};
    MainRenderPhaseState mainPhase;
    ID3D11Device* device{};
    ID3D11DeviceContext* context{};
    std::shared_ptr<const NativeTerrainPrograms> terrainPrograms; // borrowed startup generation
    std::shared_ptr<const NativePrimaryModelGpuMedia> primaryGpuMedia;
    std::shared_ptr<const NativePrimaryPassGpuMedia> primaryPassGpu;
    bool requirePrimaryGpuMedia{};
    std::vector<NativeRouteGpuBorrowed> routeGpuMedia;
    bool requireRouteGpuMedia{};
    RenderInput input;
    TerritoryOwnershipPublication territoryOwnership;
    source_command_map::Publication commandMap;
    source_hex_focus::Publication hexFocus;
    std::optional<ArmyWorldPublication> armyWorld;
    std::optional<source_current_season::Frame> currentSeason;
    std::optional<CityGateWorldPublication> cityGateWorld;
    std::optional<AreaCapitalWorldPublication> areaCapitalWorld;
    // Actual paced manager callbacks are separate from Main/Canvas requests.
    std::function<void(unsigned)> animationUpdate;
    // Continuous Map presentation may submit the completed geometry prefix
    // while Main's remaining passes are still being recorded.
    std::function<void()> submitScenePrefix;
    std::function<void(const std::filesystem::path&,const std::filesystem::path&)> replaceSeasonResources;
    std::function<void(const San14RouteOverlayInputV1&)> replaceRoutes;
    std::function<void(const San14CurrentEffectInputV1&)> replaceCurrentEffects;
    std::function<void(std::uint64_t)> retireEffectOwner;
    std::function<std::uint64_t(const std::uint8_t*,std::uint32_t)> createDirectEffect;
    std::function<void(std::uint64_t,const std::uint8_t*,std::uint32_t)> submitDirectEffect;
    std::function<int(std::uint64_t)> directEffectLive;
    std::function<void(std::uint64_t)> releaseDirectEffect;
    std::function<void(std::uint64_t,const San14CurrentEffectInputV1&,std::int64_t)> setDirectEffectDraw;
    std::function<void(std::uint64_t,const std::uint8_t*,std::uint32_t,std::int64_t)> setDirectEffectLighting;
    std::function<void(const std::uint8_t*,std::uint32_t)> configureEffectLighting;
    AnimationState animationState;
    std::function<std::vector<ArmyLabelUnitPosition>()> armyUnitPositions;
};
// Callbacks capture retained session owners. Detach them before a closed or
// failed session leaves a host with references into its destroyed frame.
class RenderCallbackScope {
public:
    explicit RenderCallbackScope(RenderHost& host) noexcept : host(host) {}
    ~RenderCallbackScope() {
        host.animationUpdate={};
        host.submitScenePrefix={};
        host.armyUnitPositions={};
        host.replaceSeasonResources={};
        host.replaceRoutes={};
        host.replaceCurrentEffects={};host.retireEffectOwner={};
        host.createDirectEffect={};host.submitDirectEffect={};host.directEffectLive={};host.releaseDirectEffect={};
        host.setDirectEffectDraw={};host.setDirectEffectLighting={};host.configureEffectLighting={};
    }
    RenderCallbackScope(const RenderCallbackScope&)=delete;
    RenderCallbackScope& operator=(const RenderCallbackScope&)=delete;
private:
    RenderHost& host;
};
// A coroutine keeps GPU owners and closures alive across caller
// requests. It does not own a thread, poll files or advance an independent loop.
class RenderSession {
public:
    struct promise_type {
        RenderOutput output;
        std::exception_ptr failure;
        int status{};
        RenderSession get_return_object(){return RenderSession{std::coroutine_handle<promise_type>::from_promise(*this)};}
        std::suspend_always initial_suspend()noexcept{return {};}
        std::suspend_always final_suspend()noexcept{return {};}
        std::suspend_always yield_value(RenderOutput current)noexcept{output=std::move(current);return {};}
        void return_value(int result)noexcept{status=result;}
        void unhandled_exception()noexcept{failure=std::current_exception();}
    };
    RenderSession()=default;
    explicit RenderSession(std::coroutine_handle<promise_type> h):handle(h){}
    RenderSession(RenderSession&& other)noexcept:handle(std::exchange(other.handle,{})){}
    RenderSession& operator=(RenderSession&& other)noexcept {
        if(handle)handle.destroy();handle=std::exchange(other.handle,{});return *this;
    }
    RenderSession(const RenderSession&)=delete;
    ~RenderSession(){if(handle)handle.destroy();}
    const RenderOutput& render() {
        if(!handle)throw std::runtime_error("Native Main has no session");
        auto& state=handle.promise();
        if(state.failure)std::rethrow_exception(state.failure);
        if(handle.done())throw std::runtime_error("Native Main stopped with status "+std::to_string(state.status));
        handle.resume();
        if(state.failure)std::rethrow_exception(state.failure);
        if(handle.done())throw std::runtime_error("Native Main stopped with status "+std::to_string(state.status));
        return state.output;
    }
private:
    std::coroutine_handle<promise_type> handle{};
};
RenderSession createRenderSession(RenderHost& host, RenderConfiguration configuration);
}
