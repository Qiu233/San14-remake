#pragma once
#include <cstdint>

// Independent first-focus scalar, belonging to the same accepted Main request.
// Absence retains its installed value; hex48400 explicitly clears the target.
struct San14HexFocusInputV1 {
    std::uint32_t size,version,hex,reserved;
};
static_assert(sizeof(San14HexFocusInputV1)==16);
