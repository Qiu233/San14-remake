#pragma once
#include "../Passes/Environment/Ocean/OceanGrid.h"
#include <optional>

namespace San14::Render {
struct NamedMapRendererDeclaration {
    std::uint32_t viewSlot{}, viewObject{}, rendererObject{};
    std::uint32_t declaredColumns{}, declaredRows{}, commonDimensionOverride{};
};

struct RendererGridDimensions {
    std::uint32_t columns{}, rows{};
    bool operator==(const RendererGridDimensions&) const = default;
};

// This provider is a domain input, independent of the backend viewport. The
// normal constructor does not attach one; a nonempty producer remains separate.
struct RendererTargetDimensions {
    std::int32_t columns{}, rows{};
};

class NamedMapRenderer {
public:
    NamedMapRenderer(NamedMapRendererDeclaration declaration,
        OceanGridSettings gridSettings, const OceanSurfaceSettings& surface,
        std::optional<RendererTargetDimensions> targetDimensions)
        : declaration_(declaration), targetDimensions_(targetDimensions),
          oceanGrid_(constructOceanGrid(resolveGrid(gridSettings), surface)) {}
    NamedMapRenderer(const NamedMapRenderer&) = delete;
    NamedMapRenderer& operator=(const NamedMapRenderer&) = delete;

    std::uint32_t viewSlot() const { return declaration_.viewSlot; }
    OceanGrid& oceanGrid() { return oceanGrid_; }
    const OceanGrid& oceanGrid() const { return oceanGrid_; }

    // The scene publisher resolves the current CPU grid dimensions before it
    // captures a View subset. No Cloud child or GPU completion gates this call.
    void prepareAmbientDimensions() {
        const auto dimensions = resolveDimensions();
        if (ambientDimensions_ && *ambientDimensions_ == dimensions) return;
        if (dimensions.columns != oceanGrid_.settings.columns ||
            dimensions.rows != oceanGrid_.settings.rows)
            throw std::runtime_error("Named renderer grid replacement is not implemented");
        ambientDimensions_ = dimensions;
    }

private:
    RendererGridDimensions resolveDimensions() const {
        auto columns = targetDimensions_ ? std::uint32_t(targetDimensions_->columns)
            : declaration_.commonDimensionOverride;
        auto rows = targetDimensions_ ? std::uint32_t(targetDimensions_->rows)
            : declaration_.commonDimensionOverride;
        if (columns == 0) columns = declaration_.declaredColumns;
        if (rows == 0) rows = declaration_.declaredRows;
        return {columns, rows};
    }
    OceanGridSettings resolveGrid(OceanGridSettings settings) const {
        const auto dimensions = resolveDimensions();
        settings.columns = dimensions.columns;
        settings.rows = dimensions.rows;
        return settings;
    }

    const NamedMapRendererDeclaration declaration_;
    const std::optional<RendererTargetDimensions> targetDimensions_;
    OceanGrid oceanGrid_;
    std::optional<RendererGridDimensions> ambientDimensions_;
};
} // namespace San14::Render
