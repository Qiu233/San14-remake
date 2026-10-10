#pragma once
#include "../ModelStencilRules.h"
#include <filesystem>
#include <fstream>

namespace San14::Render::Interop {
// The host target class is decoded at its packed input boundary.
inline ModelStencilTarget decodeModelStencilTarget(std::uint32_t hostFlags) {
    return (hostFlags&3)==1?ModelStencilTarget::ClassOne:ModelStencilTarget::Other;
}
inline std::vector<ModelStencilBinding> readModelStencilCatalog(const std::filesystem::path& path) {
    std::ifstream input(path,std::ios::binary|std::ios::ate);
    auto bytes=static_cast<std::streamoff>(input.tellg());
    if(!input||bytes<0||bytes%16)
        throw std::runtime_error("Ordinary54 source primitive descriptor extent");
    std::vector<ModelStencilBinding> result(static_cast<std::size_t>(bytes)/16);
    input.seekg(0);input.read(reinterpret_cast<char*>(result.data()),bytes);
    if(!input)throw std::runtime_error("Ordinary54 source primitive descriptors truncated");
    return result;
}
} // namespace San14::Render::Interop
