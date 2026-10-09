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
#include <d3d11.h>
#include "Pipeline/camera-continuous-native.h"
#include "TerritoryOwnership.h"
#include "ArmyWorldPublication.h"
#include "ArmyUnitHeight.h"
#include "RenderConfiguration.h"
#include "CityGateWorldPublication.h"
#include "AreaCapitalWorldPublication.h"
#include "Pipeline/current-season-client-input/current-season-native.h"

namespace San14::Render {
enum class FrameInputFlag : unsigned {
    PublishedCamera=1,
    Paused=2,
    InitialAnimationUpdate=4,
    HideTerritory=8
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
    ID3D11Device* device{};
    ID3D11DeviceContext* context{};
    RenderInput input;
    TerritoryOwnershipPublication territoryOwnership;
    std::optional<ArmyWorldPublication> armyWorld;
    std::optional<source_current_season::Frame> currentSeason;
    std::optional<CityGateWorldPublication> cityGateWorld;
    std::optional<AreaCapitalWorldPublication> areaCapitalWorld;
    // Actual paced manager callbacks are separate from Main/Canvas requests.
    std::function<void(unsigned)> animationUpdate;
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
        host.armyUnitPositions={};
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
