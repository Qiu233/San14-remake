#pragma once
#include "../../../Runtime/PreparedFileInput.h"
#include "../../NamedMapRenderer.h"
#include <filesystem>
#include <fstream>

namespace San14::Render::Interop {
inline NamedMapRendererDeclaration readNamedMapRendererDeclaration(
    const std::filesystem::path& path) {
    NativePreparedInputFile input(path, std::ios::binary);
    std::uint32_t magic{};
    NamedMapRendererDeclaration result;
    auto read = [&](auto& value) {
        input.read(reinterpret_cast<char*>(&value), sizeof(value));
    };
    read(magic);
    read(result.viewSlot);
    read(result.viewObject);
    read(result.rendererObject);
    read(result.declaredColumns);
    read(result.declaredRows);
    read(result.commonDimensionOverride);
    if (!input || input.peek() != std::char_traits<char>::eof() || magic != 0x3143524Eu)
        throw std::runtime_error("Named renderer declaration NRC1 format");
    if (result.viewSlot != 0)
        throw std::runtime_error("Named renderer slot needs its actual scene construction provider");
    if (result.viewObject == 0 || result.rendererObject == 0)
        throw std::runtime_error("Named renderer declaration requires its selected View and renderer");
    return result;
}
} // namespace San14::Render::Interop
