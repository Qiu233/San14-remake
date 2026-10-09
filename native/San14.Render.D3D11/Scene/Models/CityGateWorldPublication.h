#pragma once
#include <cstdint>
#include <cstring>
#include <vector>
#include <stdexcept>

namespace San14::Render {
// Copied at the managed/native boundary; no borrowed managed pointer survives
// the synchronous call. Source parser and device consumers run in Main.
struct CityGateFlagKey { std::int32_t force,color;std::uint32_t glyph; };
struct CityGateWorldPublication {
    std::uint64_t revision{};
    std::vector<char> primary,accessories;
    std::vector<CityGateFlagKey> flags;
};
inline CityGateWorldPublication copyCityGateWorldPublication(const std::uint8_t* data,std::size_t size) {
    if(!data||size<24)throw std::runtime_error("Current City/Gate publication is truncated");
    auto word=[&](std::size_t at){std::uint32_t v;std::memcpy(&v,data+at,4);return v;};
    if(word(0)!=0x31574743u)throw std::runtime_error("Current City/Gate requires CGW1");
    CityGateWorldPublication out;std::memcpy(&out.revision,data+4,8);
    const std::size_t p=word(12),a=word(16),f=word(20);
    if(!out.revision||!p||!a||24+p+a+f*sizeof(CityGateFlagKey)!=size)
        throw std::runtime_error("Current City/Gate source packet lengths differ");
    out.primary.assign(reinterpret_cast<const char*>(data+24),reinterpret_cast<const char*>(data+24+p));
    out.accessories.assign(reinterpret_cast<const char*>(data+24+p),reinterpret_cast<const char*>(data+24+p+a));
    out.flags.resize(f);if(f)std::memcpy(out.flags.data(),data+24+p+a,f*sizeof(CityGateFlagKey));
    return out;
}
}
