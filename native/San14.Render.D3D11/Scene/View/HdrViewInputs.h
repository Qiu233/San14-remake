#pragma once
#include <cstdint>
#include <optional>
#include <stdexcept>

namespace source_shadow_camera { struct Profile; }
namespace San14::Render {
enum class LocalLightQueryState { Unresolved, Absent, Present };
struct LocalLightQuery {
    LocalLightQueryState state{LocalLightQueryState::Unresolved};
    std::uint32_t value{};
    static LocalLightQuery absent() { return {LocalLightQueryState::Absent, 0}; }
    static LocalLightQuery present(std::uint32_t value) { return {LocalLightQueryState::Present, value}; }
    std::uint32_t requireValue() const {
        if (state == LocalLightQueryState::Unresolved) throw std::runtime_error("HDR local-light query is unresolved");
        return state == LocalLightQueryState::Absent ? 0u : value;
    }
};
struct PreparedViewLocalLightQueries {
    LocalLightQuery first, second;
    bool areEmpty() const {
        const auto firstValue = first.requireValue();
        const auto secondValue = second.requireValue();
        return firstValue == 0 && secondValue == 0;
    }
};
enum class HdrPreRendererState { Unresolved, Absent, Present };
struct PreparedHdrViewInputs {
    PreparedViewLocalLightQueries localLights;
    HdrPreRendererState preRenderer{HdrPreRendererState::Unresolved};
    const source_shadow_camera::Profile* compactShadowProvider{};
    std::optional<std::size_t> selectedShadowLight;
    bool shadowMatricesAvailable{};
};
} // namespace San14::Render
