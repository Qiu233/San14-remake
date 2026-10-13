#pragma once
#include "../CityGateWorldPublication.h"
#include <array>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <sstream>
#include <tuple>

struct NativeAccessoryMaterialKey {
    unsigned manager{};
    San14::Render::CityGateFlagKey flag{};
    auto tuple() const {return std::tie(manager,flag.force,flag.color,flag.glyph);}
    bool operator<(const NativeAccessoryMaterialKey& other) const {return tuple()<other.tuple();}
};
struct NativeAccessoryMaterialInput {
    NativeAccessoryMaterialKey key;
    unsigned proxy{},texture{},sampler{};
};
inline std::filesystem::path nativeAccessoryMaterialDirectory(const NativeAccessoryMaterialKey& key) {
    std::ostringstream name;
    name<<key.flag.force<<'-'<<key.flag.color<<'-'<<std::uppercase<<std::hex
        <<std::setw(4)<<std::setfill('0')<<key.flag.glyph;
    return std::filesystem::path("requests")/name.str()/std::to_string(key.manager);
}
inline NativeAccessoryMaterialInput readNativeAccessoryMaterialInput(const std::filesystem::path& directory,
    const NativeAccessoryMaterialKey& expected) {
    std::ifstream input(directory/"material.bin",std::ios::binary);std::array<unsigned,8> words{};
    input.read(reinterpret_cast<char*>(words.data()),sizeof(words));
    if(!input||input.peek()!=std::char_traits<char>::eof()||words[0]!=0x314d4641u)
        throw std::runtime_error("Accessory material requires AFM1 declaration");
    NativeAccessoryMaterialInput result{{words[1],{int(words[2]),int(words[3]),words[4]}},words[5],words[6],words[7]};
    if(result.key.tuple()!=expected.tuple()||result.key.manager>1||result.key.flag.force<1||
        result.key.flag.force>51||result.key.flag.color<0||result.key.flag.color>60||
        result.key.flag.glyph>0xffff||result.texture>=16||result.sampler>=16)
        throw std::runtime_error("Accessory material declaration differs from current flag identity");
    return result;
}
