#pragma once
#include <cstdint>

// Optional next-request extension. The base1160-byte camera/world ABI is unchanged.
// Managed owns these arrays until this synchronous call has copied/prepared them.
struct San14RouteVertexV1 { float x,y,z,u,v; };
struct San14RouteSpanV1 {
    std::uint32_t first,count,topology,bank,role; //D3D topology5 strip/4 list; role0 ribbon/1 bend
};
struct San14RouteEndpointV1 {
    std::int32_t hex;
    float x,y,z;
    std::int32_t surface,bank,channel;
};
enum class San14RouteUpdateKind : std::uint32_t { Retain=0, Replace=1, Clear=2 };
struct San14RouteOverlayInputV1 {
    std::uint32_t size,version;
    std::uint64_t revision,resourceGeneration;
    const wchar_t* resourceDirectory; //Actual generation's route-program-input; null retains media.
    const San14RouteVertexV1* vertices;
    const San14RouteSpanV1* spans;
    const San14RouteEndpointV1* endpoints;
    std::uint32_t vertexCount,spanCount,endpointCount;
    San14RouteUpdateKind updateKind;
};
static_assert(sizeof(San14RouteVertexV1)==20);
static_assert(sizeof(San14RouteSpanV1)==20);
static_assert(sizeof(San14RouteEndpointV1)==28);
static_assert(sizeof(San14RouteOverlayInputV1)==72);
