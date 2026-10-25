#pragma once
#include <stdint.h>
#include "CommandMapInput.h"
#include "RouteOverlayInput.h"
#include "CurrentEffectInput.h"
#include "CurrentEffectDirectInput.h"
#ifdef _WIN32
#define SAN14_RENDER_API extern "C" __declspec(dllexport)
#else
#define SAN14_RENDER_API extern "C"
#endif

// Borrowed Godot driver pointers. All calls run on Godot's render thread. Before
// create, an RD attachment use must have submitted output in RT layout.
// next uses that layout or the submitted Canvas sampling layout (outputUse=1).
// The bridge restores that same tracked layout, with no per-frame clear.
struct San14RenderInput {
    uint8_t camera[960];
    double controller[7]; // x,y,z,w,distance,yaw,pitch
    int32_t band, fraction;
    float deltaSeconds;
    uint32_t flags; //1 prepared camera,2 paused,4 eligible accessory pose update,8 hide force colors+grid
    const uint8_t* ownershipColors; // Optional source color0..60 array; copied synchronously.
    uint64_t ownershipRevision;
    uint32_t ownershipCount; //48400 with a pointer;0 means retain current ownership.
    const float* ownershipMapColors; //Once-loaded actual MapColor(raw0..59),60 float4 records.
    uint32_t ownershipMapColorCount; //60 with a pointer;0 retains current source palette.
    const uint8_t* armyWorld; //Actual actor packet; copied before returning to managed.
    uint32_t armyWorldBytes; //0 without a pointer retains the current actual population.
    const uint8_t* currentSeason; //Same-request prepared Main/lighting/atmosphere numeric values.
    uint32_t currentSeasonBytes;
    float currentRawSunPosition[4]; //Raw controller Sun, distinct from normalized Sky direction.
    float currentCloudGeometry40; //Actual once-loaded Camera614 value for this season.
    const uint8_t* cityGateWorld; //Actual61 primary/accessory and flag identity packet.
    uint32_t cityGateWorldBytes; //0 retains the current population.
    const uint8_t* areaCapitalWorld; //Independent282 port/prefecture source publication.
    uint32_t areaCapitalWorldBytes;
    const wchar_t* atmosphereResourceDirectory; // Immutable replacement candidate; next only, null retains resources.
};
struct San14RenderReceipt {
    uint8_t camera[960];
    uint64_t request;
};
struct San14AnimationState {
    uint64_t updates;
    uint32_t armyPlayers,cityGroups,areaGroups,fpsMode;
    float firstBodyPhase,firstFlagPhase;
};
struct San14ArmyUnitPosition {
    int32_t unit;
    float position[4]; //Source CUnit10/14/18/1C, not a model root.
};
SAN14_RENDER_API void* __cdecl san14_render_create(void* device,void* queue,void* output,
    // worldDetails: bit0 city accessories, bit1 actual supplied Army population.
    const wchar_t* terrainSourceDirectory,int width,int height,uint32_t worldDetails,
    const San14RenderInput* input,San14RenderReceipt* receipt);
SAN14_RENDER_API int __cdecl san14_render_next(void* owner,void* output,
    const San14RenderInput* input,uint32_t outputUse,San14RenderReceipt* receipt);
SAN14_RENDER_API int __cdecl san14_render_next_command_map_v1(void* owner,void* output,
    const San14RenderInput* input,const San14CommandMapInputV1* command,
    uint32_t outputUse,San14RenderReceipt* receipt);
SAN14_RENDER_API int __cdecl san14_render_next_command_map_v2(void* owner,void* output,
    const San14RenderInput* input,const San14CommandMapInputV2* command,
    uint32_t outputUse,San14RenderReceipt* receipt);
// Both optional map extensions belong to this same accepted camera request.
// Null command or route retains that installed owner. Empty route explicitly clears.
SAN14_RENDER_API int __cdecl san14_render_next_map_overlays_v1(void* owner,void* output,
    const San14RenderInput* input,const San14CommandMapInputV2* command,
    const San14RouteOverlayInputV1* route,uint32_t outputUse,San14RenderReceipt* receipt);
SAN14_RENDER_API int __cdecl san14_render_next_map_effects_v1(void* owner,void* output,
    const San14RenderInput* input,const San14CommandMapInputV2* command,
    const San14RouteOverlayInputV1* route,const San14CurrentEffectInputV1* effects,
    uint32_t outputUse,San14RenderReceipt* receipt);
// Synchronous calls on the same render thread as next; all package bytes are
// consumed before return. Allocation identities belong to this session.
SAN14_RENDER_API uint64_t __cdecl san14_effect_direct_create_v1(void* owner,const uint8_t* bytes,uint32_t byteCount);
SAN14_RENDER_API int __cdecl san14_effect_direct_submit_v1(void* owner,uint64_t allocation,const uint8_t* bytes,uint32_t byteCount);
SAN14_RENDER_API int __cdecl san14_effect_direct_live_v1(void* owner,uint64_t allocation,int32_t* live);
SAN14_RENDER_API int __cdecl san14_effect_direct_release_v1(void* owner,uint64_t allocation);
SAN14_RENDER_API int __cdecl san14_effect_direct_draw_v1(void* owner,uint64_t allocation,const San14CurrentEffectInputV1* material,int64_t submissionOrdinal);
SAN14_RENDER_API int __cdecl san14_render_animation_event(void* owner,
    const San14RenderInput* input,uint32_t fpsMode);
SAN14_RENDER_API void __cdecl san14_render_animation_state(void* owner,San14AnimationState* result);
// Pure CPU copy after create/next, on the same render thread. Returns row count
// or -1 on failure; no animation, grounding or GPU readback occurs here.
SAN14_RENDER_API int __cdecl san14_render_army_unit_positions(void* owner,
    San14ArmyUnitPosition* result,uint32_t capacity);
SAN14_RENDER_API void __cdecl san14_render_destroy(void* owner);
SAN14_RENDER_API const char* __cdecl san14_render_error();
