#pragma once
#include "WeatherFogState.h"
namespace source_weather_fog {
using source_camera::get;
// Read only consumed fog fields. Bytes72..95 remain owned by the light resource.
inline FogState decodeFog(const uint8_t* record){
    return {get<V4>(record),FogMode(get<uint32_t>(record+0x10)),
        get<float>(record+0x14),get<float>(record+0x18),get<float>(record+0x1c),
        get<V3>(record+0x20),get<float>(record+0x2c),get<V3>(record+0x30),get<float>(record+0x3c),
        get<float>(record+0x40),get<float>(record+0x44)};
}
} // namespace source_weather_fog
