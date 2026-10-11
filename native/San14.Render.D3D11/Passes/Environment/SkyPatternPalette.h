#pragma once
#include "SkyPatternTargets.h"
#include "SkyAtmosphereResources.h"
#include "../../Camera/CameraState.h"

namespace San14::Render {
// This is a synchronous borrow of the retained environment. Wrapper presence
// is independent of whether the most recent Pattern operation drew pixels.
struct SkyPatternPalette {
    const NativeSkyAtmosphereResources& atmosphere;
    const NativeSkyPatternTarget* selected;
    const source_camera::Matrix& worldToPattern;
};
} // namespace San14::Render
