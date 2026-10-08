#pragma once
#include "CityGateWorldPublication.h"
#include <string>
namespace San14::Render {
struct AreaCapitalWorldPublication {
    CityGateWorldPublication models;
    std::vector<std::string> identities;
};
inline AreaCapitalWorldPublication copyAreaCapitalWorldPublication(const std::uint8_t* data,std::size_t size) {
    if(!data||size<28)throw std::runtime_error("Current Area Capital publication is truncated");
    auto word=[&](std::size_t at){std::uint32_t v;std::memcpy(&v,data+at,4);return v;};
    if(word(0)!=0x31574341u)throw std::runtime_error("Current Area Capital requires ACW1");
    AreaCapitalWorldPublication out;auto& models=out.models;std::memcpy(&models.revision,data+4,8);
    const std::size_t p=word(12),a=word(16),f=word(20),n=word(24);std::size_t at=28+p+a+f*sizeof(CityGateFlagKey);
    if(!models.revision||!p||!a||n!=282||at>size)throw std::runtime_error("Current Area Capital source packet shape differs");
    models.primary.assign(reinterpret_cast<const char*>(data+28),reinterpret_cast<const char*>(data+28+p));
    models.accessories.assign(reinterpret_cast<const char*>(data+28+p),reinterpret_cast<const char*>(data+28+p+a));
    models.flags.resize(f);if(f)std::memcpy(models.flags.data(),data+28+p+a,f*sizeof(CityGateFlagKey));
    out.identities.reserve(n);
    for(std::size_t i=0;i<n;++i) {
        if(at+4>size)throw std::runtime_error("Current Area identity length is truncated");
        const std::size_t bytes=word(at);at+=4;
        if(!bytes||at+bytes>size)throw std::runtime_error("Current Area identity is truncated");
        out.identities.emplace_back(reinterpret_cast<const char*>(data+at),bytes);at+=bytes;
    }
    if(at!=size)throw std::runtime_error("Current Area publication has trailing bytes");return out;
}
}
