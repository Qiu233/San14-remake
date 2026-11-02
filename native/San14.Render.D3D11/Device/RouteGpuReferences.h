#pragma once
#include <filesystem>
#include <memory>
#include <string>
#include <vector>

namespace San14::Render {
struct NativeRouteGpuMedia;
struct NativeRouteGpuDeclaration {
    std::wstring logicalKey,identity;
    std::filesystem::path directory;
};
struct NativeRouteGpuBorrowed {
    std::filesystem::path directory;
    std::shared_ptr<const NativeRouteGpuMedia> media;
};
std::vector<NativeRouteGpuDeclaration> readNativeRouteGpuReferences(const std::filesystem::path& path);
} // namespace San14::Render
