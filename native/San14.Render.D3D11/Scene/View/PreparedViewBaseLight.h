#pragma once
#include "MapViewSettings.h"
#include "../../Passes/Environment/SeasonFrame.h"
#include <array>
#include <cstddef>
#include <cstdint>
#include <optional>
#include <stdexcept>

namespace San14::Render {
struct PreparedViewBaseLightSelection {
    std::size_t recordIndex;
    std::uint32_t object;
    std::array<float, 3> direction;
    std::uint32_t fogMode;
};

// A generation captures only the base-light values consumed by Pattern. The
// normal World records retain their source groups; later mask setters require
// a separate current-record producer and are not inferred from these headers.
class PreparedViewBaseLight {
public:
    enum class State { Unresolved, Absent, Present };

    static PreparedViewBaseLight select(const source_current_season::Frame* world,
        const MapViewCommonSettings& common) {
        if (!world) return PreparedViewBaseLight(State::Unresolved);
        if (!common.present || common.outerLightGroupOverride == 0)
            return PreparedViewBaseLight(State::Absent);
        if (common.outerLightGroupOverride > 32)
            throw std::runtime_error("Prepared View base-light selector exceeds supported groups");
        const auto selectedGroup = std::uint32_t{1} << (common.outerLightGroupOverride - 1);
        for (std::size_t index = 0; index < world->lights.size(); ++index) {
            const auto& light = world->lights[index];
            if ((light.mask & ~selectedGroup) != 0) continue;
            PreparedViewBaseLight result(State::Present);
            result.selection_ = PreparedViewBaseLightSelection{index, light.object,
                light.viewBase.direction, light.viewBase.fogMode};
            return result;
        }
        return PreparedViewBaseLight(State::Absent);
    }

    State state() const { return state_; }
    const std::optional<PreparedViewBaseLightSelection>& selection() const {
        if (state_ == State::Unresolved)
            throw std::runtime_error("Prepared View base-light producer is unresolved");
        return selection_;
    }

private:
    explicit PreparedViewBaseLight(State state) : state_(state) {}
    State state_;
    std::optional<PreparedViewBaseLightSelection> selection_;
};
} // namespace San14::Render
