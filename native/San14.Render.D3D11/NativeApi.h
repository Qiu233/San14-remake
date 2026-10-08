#pragma once
#include <stdint.h>
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
SAN14_RENDER_API int __cdecl san14_render_animation_event(void* owner,
    const San14RenderInput* input,uint32_t fpsMode);
SAN14_RENDER_API void __cdecl san14_render_animation_state(void* owner,San14AnimationState* result);
// Pure CPU copy after create/next, on the same render thread. Returns row count
// or -1 on failure; no animation, grounding or GPU readback occurs here.
SAN14_RENDER_API int __cdecl san14_render_army_unit_positions(void* owner,
    San14ArmyUnitPosition* result,uint32_t capacity);
SAN14_RENDER_API void __cdecl san14_render_destroy(void* owner);
SAN14_RENDER_API const char* __cdecl san14_render_error();
