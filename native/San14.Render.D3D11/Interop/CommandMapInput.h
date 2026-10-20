#pragma once
#include <stdint.h>

// Extension to a particular accepted next request. The original1160B input
// ABI stays unchanged. All pointed data is copied before the call returns.
struct San14CommandMapInputV1 {
    uint32_t size,version;
    uint64_t revision,dataRevision,providerGeneration;
    uint32_t present,hoverHex,fpsMode,count;
    const uint8_t* membership;
    const uint16_t* rawAreas;
};
static_assert(sizeof(San14CommandMapInputV1)==64);
