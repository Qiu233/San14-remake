#pragma once
#include "MainStarMaterialState.h"
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <stdexcept>

struct NativeAmbientStarCameras {
    std::array<San14::Render::StarDrawCamera,6> faces;
};

// This initial input contains only the real six WORK cameras. Materials,
// programs, point geometry and retained draw setters belong to the shared Star.
inline NativeAmbientStarCameras readNativeAmbientStarCameras(const std::filesystem::path& path) {
    std::ifstream input(path,std::ios::binary);
    std::uint32_t magic{};input.read(reinterpret_cast<char*>(&magic),4);
    if(!input||magic!=0x31435357u)throw std::runtime_error("WORK Star camera WSC1 format");
    NativeAmbientStarCameras result;
    for(auto& face:result.faces) {
        input.read(reinterpret_cast<char*>(&face.eyeHeight),4);
        input.read(reinterpret_cast<char*>(&face.viewSpan),4);
        input.read(reinterpret_cast<char*>(face.worldToClip.data()),sizeof(face.worldToClip));
    }
    if(!input||input.peek()!=std::char_traits<char>::eof())
        throw std::runtime_error("WORK Star camera WSC1 extent");
    return result;
}