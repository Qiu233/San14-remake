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

// V1 remains unchanged. V2 names display ownership explicitly instead of
// overloading Present. Fixed candidates have membership but no raw Area data.
enum class San14CommandMapDisplayKind : uint32_t {
    None=0, AreaAdministration=1, FixedCandidateGray=2,
    CampaignProvinceFocus=3,CampaignDistrictFocus=4
};
struct San14CommandMapInputV2 {
    uint32_t size,version;
    uint64_t revision,dataRevision,providerGeneration;
    San14CommandMapDisplayKind displayKind;
    uint32_t hoverHex,fpsMode,count;
    const uint8_t* membership;
    const uint16_t* rawAreas;
};
static_assert(sizeof(San14CommandMapInputV2)==64);
