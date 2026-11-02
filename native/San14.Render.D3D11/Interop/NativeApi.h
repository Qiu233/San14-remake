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

// Legacy calls borrow Godot driver pointers on its render thread. Normal
// Map jobs use their serialized worker; short bridge APIs enqueue separately. Before
// create, an RD attachment use must have submitted output in RT layout.
// next uses that layout or the submitted Canvas sampling layout (outputUse=1).
// The bridge restores that same tracked layout, with no per-frame clear.
// Native11 shared outputs have immutable COMMON layout and use the same real
// initial-submission/consumer/ready gates without an On12 wrapped resource.
struct San14RenderInput {
    uint8_t camera[960];
    double controller[7]; // x,y,z,w,distance,yaw,pitch
    int32_t band, fraction;
    float deltaSeconds;
    uint32_t flags; //1 prepared camera,2 paused,4 eligible accessory pose update,8 hide force colors+grid
    // Bits20..22:0 automatic zoom mode;1..5 captured Main override0..4.
    // Independent animation events carry this without a full season packet.
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
    const wchar_t* patternResourceDirectory; // Exact normal-pattern-receiver-input directory; next only, null retains resources.
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
// One scene may be prepared ahead of output admission. The receipt identifies
// real completed Main recording, not Canvas submission or GPU completion.
// Copy must consume that exact request before another scene/animation update.
// Optional extensions have the same retain/clear semantics as synchronous next.
SAN14_RENDER_API int __cdecl san14_render_prepare_scene_v1(void* owner,
    const San14RenderInput* input,const San14CommandMapInputV2* command,
    const San14RouteOverlayInputV1* route,const San14CurrentEffectInputV1* effects,
    San14RenderReceipt* receipt);
SAN14_RENDER_API int __cdecl san14_render_copy_scene_v1(void* owner,void* output,
    uint64_t request,uint32_t outputUse);
// Synchronous calls on the same render thread as next; all package bytes are
// consumed before return. Allocation identities belong to this session.
SAN14_RENDER_API int __cdecl san14_effect_retire_owner_v1(void* owner,uint64_t sourceOwner);
SAN14_RENDER_API uint64_t __cdecl san14_effect_direct_create_v1(void* owner,const uint8_t* bytes,uint32_t byteCount);
SAN14_RENDER_API int __cdecl san14_effect_direct_submit_v1(void* owner,uint64_t allocation,const uint8_t* bytes,uint32_t byteCount);
SAN14_RENDER_API int __cdecl san14_effect_direct_live_v1(void* owner,uint64_t allocation,int32_t* live);
SAN14_RENDER_API int __cdecl san14_effect_direct_release_v1(void* owner,uint64_t allocation);
SAN14_RENDER_API int __cdecl san14_effect_direct_draw_v1(void* owner,uint64_t allocation,const San14CurrentEffectInputV1* material,int64_t submissionOrdinal);
SAN14_RENDER_API int __cdecl san14_effect_direct_lighting_v1(void* owner,uint64_t allocation,const uint8_t* bytes,uint32_t byteCount,int64_t submissionOrdinal);
SAN14_RENDER_API int __cdecl san14_effect_lighting_cluster_v1(void* owner,const uint8_t* bytes,uint32_t byteCount);
SAN14_RENDER_API int __cdecl san14_render_animation_event(void* owner,
    const San14RenderInput* input,uint32_t fpsMode);
SAN14_RENDER_API void __cdecl san14_render_animation_state(void* owner,San14AnimationState* result);
// Pure CPU copy after create/next, on the same render thread. Returns row count
// or -1 on failure; no animation, grounding or GPU readback occurs here.
SAN14_RENDER_API int __cdecl san14_render_army_unit_positions(void* owner,
    San14ArmyUnitPosition* result,uint32_t capacity);
SAN14_RENDER_API void __cdecl san14_render_destroy(void* owner);
SAN14_RENDER_API const char* __cdecl san14_render_error();

// Explicit independent-queue Map bridge. Old synchronous/UI-effect create
// remains on its original borrowed queue. Input/receipt ABI is unchanged.
struct San14RenderQueueBridgeSlotV1 {
    uint64_t resource,lastReadyRequest,lastReadyValue,lastConsumedValue,lastNativeWaitValue,lastGodotWaitReadyValue;
};
struct San14RenderQueueBridgeStateV1 {
    uint32_t structBytes,version,retiring,retired;
    uint64_t device,nativeQueue,godotQueue,nativeFence,godotFence;
    uint64_t lastNativeSignal,lastGodotConsumed,nativeCompleted,godotCompleted;
    San14RenderQueueBridgeSlotV1 slots[2];
};
SAN14_RENDER_API void* __cdecl san14_render_queue_bridge_create_v1(void* device,void* godotQueue,void* output0,void* output1);
// Native D3D11 on the same physical adapter, not another D3D12 command queue.
// Godot's device creates the two RT|simultaneous-access shared color images.
struct San14Native11BridgeCreateV1 {
    uint32_t structBytes,version,width,height,dxgiFormat,resourceFlags,backendKind,reserved;
    uint64_t output0Resource12,output1Resource12,nativeDevice11,adapterLuid;
};
struct San14Native11BridgeSlotV2 {
    uint64_t resource12,resource11,lastReadyRequest,lastReadyValue,
        lastConsumedValue,lastNativeWaitValue,lastGodotWaitReadyValue;
};
struct San14Native11BridgeStateV2 {
    // flags:1 retiring,2 retired,4 actual initial submission,8 owner claimed.
    uint32_t structBytes,version,backendKind,flags;
    uint64_t godotDevice12,godotQueue,device11,context11,adapterLuid,
        nativeReadyFence11,nativeReadyFence12,godotConsumedFence11,godotConsumedFence12,
        lastNativeSignal,lastGodotConsumed,nativeCompleted,godotCompleted;
    San14Native11BridgeSlotV2 slots[2];
};
// Explicit process-lifetime device owner. Destroy returns nonzero while any
// actual bridge child remains; that failure leaves the device handle usable.
struct San14Native11DeviceInfoV1 {
    uint32_t structBytes,version;
    uint64_t godotDevice12,device11,context11,adapterLuid;
};
SAN14_RENDER_API void* __cdecl san14_render_native11_device_create_v1(void* godotDevice12,
    San14Native11DeviceInfoV1* result,uint32_t bytes);
SAN14_RENDER_API int __cdecl san14_render_native11_device_destroy_v1(void* deviceOwner);
struct San14TerrainProgramSourceV1 {
    const wchar_t* logicalKey;
    const wchar_t* identity;
    const wchar_t* directory;
};
SAN14_RENDER_API int __cdecl san14_render_native11_terrain_programs_load_v1(void* deviceOwner,
    const San14TerrainProgramSourceV1* sources,uint32_t count);
SAN14_RENDER_API int __cdecl san14_render_native11_route_media_load_v1(void* deviceOwner,
    const San14TerrainProgramSourceV1* sources,uint32_t count);

struct San14PrimaryModelGpuSourceV1 {
    const wchar_t* logicalKey;
    const wchar_t* identity;
    const wchar_t* directory;
};
SAN14_RENDER_API int __cdecl san14_render_native11_primary_models_load_v1(void* deviceOwner,
    const San14PrimaryModelGpuSourceV1* sources,uint32_t count);

SAN14_RENDER_API void* __cdecl san14_render_native11_bridge_create_v2(void* deviceOwner,void* godotQueue,
    uint32_t width,uint32_t height,San14Native11BridgeCreateV1* result,uint32_t bytes);
SAN14_RENDER_API void* __cdecl san14_render_native11_bridge_create_v1(void* godotDevice12,void* godotQueue,
    uint32_t width,uint32_t height,San14Native11BridgeCreateV1* result,uint32_t bytes);
SAN14_RENDER_API int __cdecl san14_render_queue_bridge_state_v2(void* bridge,San14Native11BridgeStateV2* result,uint32_t bytes);
// These short bridge calls enqueue only; no contextGate, Native job or GPU
// CPU wait. consumed is called after an actual submitted Godot frame.
SAN14_RENDER_API int __cdecl san14_render_queue_bridge_signal_consumed_v1(void* bridge,uint64_t* enqueuedValue);
SAN14_RENDER_API int __cdecl san14_render_queue_bridge_wait_ready_v1(void* bridge,void* output,uint64_t request,uint64_t* readyValue);
// Close-only real queue tail; no Canvas receipt or unsubmitted RD flush.
SAN14_RENDER_API int __cdecl san14_render_queue_bridge_seal_godot_tail_v1(void* bridge,uint64_t* enqueuedValue);
SAN14_RENDER_API int __cdecl san14_render_queue_bridge_state_v1(void* bridge,San14RenderQueueBridgeStateV1* result,uint32_t bytes);
SAN14_RENDER_API void* __cdecl san14_render_create_map_worker_v1(void* bridge,void* output,
    const wchar_t* terrainSourceDirectory,int width,int height,uint32_t worldDetails,
    const San14RenderInput* input,San14RenderReceipt* receipt);
// Typed source identities select already-loaded device resources; no opening
// path is used as a cache key and no file is read on this borrow.
SAN14_RENDER_API void* __cdecl san14_render_create_map_worker_v2(void* bridge,void* output,
    const wchar_t* terrainSourceDirectory,int width,int height,uint32_t worldDetails,
    const wchar_t* terrainProgramKey,const wchar_t* terrainProgramIdentity,
    const San14RenderInput* input,San14RenderReceipt* receipt);

// Retirement worker only: stop/join jobs, obtain an actual final Godot
// submission Signal, run original Owner drain, then retire bridge, destroy
// Owner and bridge, and finally free output RIDs. No future Signal is awaited.
SAN14_RENDER_API int __cdecl san14_render_queue_bridge_retire_v1(void* bridge);
SAN14_RENDER_API int __cdecl san14_render_queue_bridge_destroy_v1(void* bridge);
