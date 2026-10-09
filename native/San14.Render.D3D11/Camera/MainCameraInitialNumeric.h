#pragma once
// Project-owned startup numeric packet. This arena is not a complete source
// Camera: its unused gaps have no original semantics and contain no pointers.
#include <array>
#include <cstdint>
#include <cstring>
#include <span>
#include <stdexcept>
#include <vector>

namespace own_main_init {
inline constexpr std::array<uint32_t,37> tailOffsets{
    0x9EC,0x9F0,0x9F4,0x9F8,0x9FC,0xA00,0xA04,0xA08,0xA10,0xA18,0xA20,
    0xA28,0xA2C,0xA38,0xA3C,0xA40,0xA84,0xA88,
    0xB1C,0xDC0,0xDC4,0xDC8,0xDCC,0xDD0,0xDD4,0xDD8,0xDDC,0xDE0,0xDE4,0xDE8,0xDEC,
    0xDF4,0xDF8,0x141C,0x1438,0x143C,0x1510};
inline std::vector<char> decode(std::span<const char> packet) {
    constexpr size_t prefixOffset=8,prefixBytes=0x3C0-prefixOffset,headerBytes=20;
    constexpr size_t packetBytes=headerBytes+prefixBytes+tailOffsets.size()*8;
    if(packet.size()!=packetBytes||std::memcmp(packet.data(),"MAINNUM1",8)!=0)
        throw std::runtime_error("Initial Main numeric packet format");
    auto word=[&](size_t offset){uint32_t value;std::memcpy(&value,packet.data()+offset,4);return value;};
    if(word(8)!=prefixOffset||word(12)!=prefixBytes||word(16)!=tailOffsets.size())
        throw std::runtime_error("Initial Main numeric packet schema");
    std::vector<char> arena(0x18C0); // Only the explicitly supplied fields are meaningful.
    std::memcpy(arena.data()+prefixOffset,packet.data()+headerBytes,prefixBytes);
    size_t cursor=headerBytes+prefixBytes;
    for(uint32_t offset:tailOffsets){
        if(word(cursor)!=offset)throw std::runtime_error("Initial Main numeric field order");
        std::memcpy(arena.data()+offset,packet.data()+cursor+4,4);cursor+=8;
    }
    return arena;
}
} // namespace own_main_init
