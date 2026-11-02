// Prepared manifests use portable separators and relative mesh/texture paths.
// Windows file APIs need native separators, lexical dot resolution and the
// extended prefix for absolute paths, including roots read from UTF-8 manifests.
// This import boundary performs no filesystem scan or I/O.
#pragma once
#include <filesystem>
#include <fstream>
#include <string>
#include <utility>

inline std::filesystem::path nativePreparedFilePath(std::filesystem::path path) {
    path=path.lexically_normal().make_preferred();
#ifdef _WIN32
    const auto& value=path.native();
    if(path.is_absolute()&&!value.starts_with(L"\\\\?\\")&&!value.starts_with(L"\\\\.\\")) {
        if(value.starts_with(L"\\\\"))
            return std::filesystem::path(L"\\\\?\\UNC\\"+value.substr(2));
        return std::filesystem::path(L"\\\\?\\"+value);
    }
#endif
    return path;
}

// A prepared resource stream preserves standard ifstream state and streaming
// semantics. Only the actual file-open boundary normalizes manifest paths.
class NativePreparedInputFile : public std::ifstream {
public:
    NativePreparedInputFile() = default;
    explicit NativePreparedInputFile(const std::filesystem::path& path,
        std::ios_base::openmode mode = std::ios_base::in)
        : std::ifstream(nativePreparedFilePath(path), mode) {}
    NativePreparedInputFile(NativePreparedInputFile&& other)
        : std::ifstream(std::move(other)) {}
    NativePreparedInputFile& operator=(NativePreparedInputFile&& other) {
        std::ifstream::operator=(std::move(other));
        return *this;
    }
    void open(const std::filesystem::path& path,
        std::ios_base::openmode mode = std::ios_base::in) {
        std::ifstream::open(nativePreparedFilePath(path), mode);
    }
};
