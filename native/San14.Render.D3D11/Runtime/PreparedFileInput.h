// Prepared manifests use portable separators and relative mesh/texture paths.
// Extended Windows paths require native separators and lexical dot resolution
// before file APIs; this import boundary performs no filesystem scan or I/O.
#pragma once
#include <filesystem>
#include <fstream>
#include <utility>

inline std::filesystem::path nativePreparedFilePath(std::filesystem::path path) {
    return path.lexically_normal().make_preferred();
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
