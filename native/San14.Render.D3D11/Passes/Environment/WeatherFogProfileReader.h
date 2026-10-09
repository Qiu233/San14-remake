#pragma once
#include "WeatherFogState.h"
namespace source_weather_fog {
inline Profile readProfile(const std::filesystem::path& source){
    Profile p;std::ifstream s(source/"profile.bin",std::ios::binary);
    const auto fields=source_camera::read<std::array<float,46>>(s);
    p.conditions={fields[0],fields[1],fields[36]};
    p.response={fields[2],fields[3],fields[4],fields[5],fields[6],fields[7],fields[8],fields[9],
        fields[10],fields[11],fields[12],fields[13],fields[14],fields[15],fields[16]};
    p.fog={fields[17],fields[18],fields[19],fields[20],fields[21]};
    p.scattering={{fields[22],fields[23],fields[24]},{fields[25],fields[26],fields[27]},
        {fields[28],fields[29],fields[30]},{fields[31],fields[32],fields[33]}};
    p.lighting={fields[34],fields[35],fields[37],fields[38],
        {fields[39],fields[40],fields[41]},{fields[42],fields[43],fields[44]}};
    // Ordinal45 is not consumed by any weather producer or consumer.
    p.power.reciprocal=source_camera::read<std::array<double,257>>(s);p.power.logarithms=source_camera::read<std::array<double,257>>(s);
    p.power.exponentials=source_camera::read<std::array<double,64>>(s);p.power.constants=source_camera::read<std::array<double,11>>(s);
    if(s.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Weather Fog profile tail");
    std::ifstream table(source/"ambient2d-half4.bin",std::ios::binary);p.ambient.resize(64*256*4);
    table.read(reinterpret_cast<char*>(p.ambient.data()),p.ambient.size()*sizeof(uint16_t));
    if(!table||table.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Weather Ambient2D raw shape");return p;
}
} // namespace source_weather_fog
