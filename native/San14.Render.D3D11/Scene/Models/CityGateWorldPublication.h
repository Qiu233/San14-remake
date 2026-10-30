#pragma once
#include <cstdint>
#include <cstring>
#include <vector>
#include <stdexcept>

namespace San14::Render {
// Copied at the managed/native boundary; no borrowed managed pointer survives
// the synchronous call. Source parser and device consumers run in Main.
struct CityGateFlagKey {
    std::int32_t force,color;std::uint32_t glyph;
    bool operator==(const CityGateFlagKey&) const = default;
};
struct CityGateWorldPublication {
    std::uint64_t revision{};
    std::vector<char> primary,accessories,facilities;
    std::vector<CityGateFlagKey> flags;
};
// CGW2 keeps the existing City/Gate segments and appends a complete facility
// CPU1 roster. A zero-row CPU1 clears it; no segment retains the legacy owner.
// Header: magic, revision64, primaryBytes, accessoryBytes, flagCount, facilityBytes.
inline CityGateWorldPublication copyCityGateWorldPublication(const std::uint8_t* data,std::size_t size) {
    if(!data||size<24)throw std::runtime_error("Current City/Gate publication is truncated");
    auto word=[&](std::size_t at){std::uint32_t v;std::memcpy(&v,data+at,4);return v;};
    const auto schema=word(0);
    if(schema!=0x31574743u&&schema!=0x32574743u)throw std::runtime_error("Current City/Gate requires CGW1 or CGW2");
    const std::size_t header=schema==0x32574743u?28:24;
    if(size<header)throw std::runtime_error("Current City/Gate publication header is truncated");
    CityGateWorldPublication out;std::memcpy(&out.revision,data+4,8);
    const std::size_t p=word(12),a=word(16),f=word(20),facilities=header==28?word(24):0;
    if(!out.revision||!p||!a||header+p+a+f*sizeof(CityGateFlagKey)+facilities!=size)
        throw std::runtime_error("Current City/Gate source packet lengths differ");
    out.primary.assign(reinterpret_cast<const char*>(data+header),reinterpret_cast<const char*>(data+header+p));
    out.accessories.assign(reinterpret_cast<const char*>(data+header+p),reinterpret_cast<const char*>(data+header+p+a));
    out.flags.resize(f);if(f)std::memcpy(out.flags.data(),data+header+p+a,f*sizeof(CityGateFlagKey));
    const auto facilityStart=header+p+a+f*sizeof(CityGateFlagKey);
    out.facilities.assign(reinterpret_cast<const char*>(data+facilityStart),reinterpret_cast<const char*>(data+size));
    return out;
}
}
