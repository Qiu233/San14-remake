#pragma once
#include "../../Runtime/PreparedFileInput.h"
#include "HdrMaterialModifiers.h"
#include <filesystem>
#include <fstream>
#include <map>

namespace San14::Render {
struct HdrShaderModules {
    bool hasHatching{};
    HdrHatchingPhase hatching;
};

// One loaded scene's named ShaderObjects. The same object borrowed by several
// graph calls shares its plugin instance; a different object using identical
// G1S bytes does not. Programs and draw recipes never own this temporal state.
class HdrShaderModuleBank {
public:
    HdrShaderModules& borrow(const std::filesystem::path& file) {
        std::array<std::uint32_t,3> header{};
        NativePreparedInputFile input(file,std::ios::binary);
        input.read(reinterpret_cast<char*>(header.data()),sizeof(header));
        if(!input||input.peek()!=std::char_traits<char>::eof()||header[0]!=0x314D5348u||
            header[1]==0||header[2]>1)
            throw std::runtime_error("HDR named shader module declaration format");
        const bool hasHatching=header[2]!=0;
        auto [found,inserted]=owners_.try_emplace(header[1]);
        if(inserted)found->second.hasHatching=hasHatching;
        else if(found->second.hasHatching!=hasHatching)
            throw std::runtime_error("One named HDR shader object has inconsistent plugin declarations");
        return found->second;
    }
private:
    std::map<std::uint32_t,HdrShaderModules> owners_;
};
} // namespace San14::Render
