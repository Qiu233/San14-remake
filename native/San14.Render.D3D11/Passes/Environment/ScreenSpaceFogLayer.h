#pragma once
#include <cstdint>

namespace source_current_ssfog {
// Signed modes share the same layer family; the sign only changes the
// height origin in the current typed layer producer.
inline int layerType(uint32_t mode) {
    uint32_t family=mode&0x7fffffffu;
    return family>=1&&family<=3?int(family)-1:-1;
}
} // namespace source_current_ssfog
