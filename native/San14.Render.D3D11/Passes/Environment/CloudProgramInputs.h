#pragma once
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <map>
#include <stdexcept>
#include <string>

namespace source_cloud_programs {
struct Program {
    uint32_t combination{},vertex{},pixel{},vertexConstantBytes{},pixelConstantBytes{};
};
inline std::map<std::string,Program> read(const std::filesystem::path& path) {
    std::ifstream input(path);std::map<std::string,Program> result;
    if(!input)throw std::runtime_error("Cloud program media absent");
    std::string name;Program p;
    while(input>>name) {
        if(!(input>>p.combination>>p.vertex>>p.pixel>>p.vertexConstantBytes>>p.pixelConstantBytes)||
            (name!="pass1"&&name!="pass2"&&name!="pass4")||
            p.vertexConstantBytes==0||p.vertexConstantBytes%16||p.pixelConstantBytes%16||
            !result.emplace(name,p).second)
            throw std::runtime_error("Cloud program media format is unsupported");
    }
    if(!input.eof()||result.size()!=3)throw std::runtime_error("Cloud program media requires Main passes1/2/4");
    return result;
}
}
