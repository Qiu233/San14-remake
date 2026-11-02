#pragma once
#include "../Runtime/PreparedFileInput.h"
#include <array>
#include <cstdint>
#include <memory>
#include <stdexcept>
#include <string>
#include <vector>

namespace San14::Render {
struct NativePrimaryForwardNamedWrite {
    unsigned field{},destination{},source{},bytes{};
};
struct NativePrimaryForwardTemplateMetadata {
    bool eligible{};
    std::string directory;
};

// Resource-only Forward layout. No retained numeric packet, current light,
// camera, World, GPU constants or render target is stored in this metadata.
struct NativePrimaryForwardMetadata final {
    std::vector<NativePrimaryForwardTemplateMetadata> templates;
    std::vector<NativePrimaryForwardNamedWrite> lightingWrites,patternWrites;
    std::vector<std::array<unsigned,2>> vertexWrites,pixelWrites;
    unsigned lightGroup{};

    static std::vector<std::array<unsigned,2>> loadDefinedRanges(const std::filesystem::path& path,unsigned capacity) {
        NativePreparedInputFile input(path);std::vector<std::array<unsigned,2>> writes;unsigned offset{},bytes{};
        while(input>>offset>>bytes) {
            if(offset>capacity||bytes>capacity-offset)throw std::runtime_error("Forward source CB write outside allocation");
            writes.push_back({offset,bytes});
        }
        if(writes.empty())throw std::runtime_error("Forward CB source-defined writes absent");
        return writes;
    }

    static NativePrimaryForwardMetadata loadTemplates(const std::filesystem::path& directory) {
        NativePrimaryForwardMetadata result;unsigned count{};
        NativePreparedInputFile inputCount(directory/"template-count.txt");inputCount>>count;
        if(!inputCount)throw std::runtime_error("Forward state0 template count absent");
        result.templates.resize(count);
        NativePreparedInputFile rows(directory/"templates.txt");unsigned index{},eligible{};std::string relative;
        while(rows>>index>>eligible>>relative) {
            auto& item=result.templates.at(index);item.eligible=eligible!=0;item.directory=relative;
        }
        if(!rows.eof())throw std::runtime_error("Forward lighting source is incomplete");
        return result;
    }

    // Legacy CPU-only inputs keep their original contract: GPU-defined ranges
    // are not required until a GPU/static closure is actually being prepared.
    static NativePrimaryForwardMetadata loadSourceLayout(const std::filesystem::path& directory) {
        auto result=loadTemplates(directory);
        // Binary named-write ABI: HDR fields 0..11, then Forward Eye field12.
        constexpr std::array<unsigned,13> lightingWidths{64,64,16,16,4,16,16,64,4,16,16,8,16};
        NativePrimaryForwardNamedWrite write;
        NativePreparedInputFile lighting(directory/"forward-lighting-writes.txt");
        while(lighting>>write.field>>write.destination>>write.source>>write.bytes) {
            const auto width=write.field<lightingWidths.size()?lightingWidths[write.field]:0u;
            if(!width||!write.bytes||std::uint64_t(write.source)+write.bytes>width||
                std::uint64_t(write.destination)+write.bytes>1360)
                throw std::runtime_error("Forward named lighting layout range");
            result.lightingWrites.push_back(write);
        }
        NativePreparedInputFile pattern(directory/"forward-pattern-writes.txt");
        while(pattern>>write.field>>write.destination>>write.source>>write.bytes) {
            constexpr std::array<unsigned,3> widths{64,4,32};
            if(write.field>=widths.size()||!write.bytes||std::uint64_t(write.source)+write.bytes>widths[write.field]||
                std::uint64_t(write.destination)+write.bytes>(write.field==0?480u:1360u))
                throw std::runtime_error("Forward named Pattern layout range");
            result.patternWrites.push_back(write);
        }
        NativePreparedInputFile group(directory/"forward-light-group.txt");group>>result.lightGroup;
        if(!lighting.eof()||!pattern.eof()||!group||result.lightingWrites.empty()||result.patternWrites.empty())
            throw std::runtime_error("Forward lighting source is incomplete");
        return result;
    }

    static NativePrimaryForwardMetadata load(const std::filesystem::path& directory) {
        auto result=loadSourceLayout(directory);
        result.vertexWrites=loadDefinedRanges(directory/"vs-defined-ranges.txt",480);
        result.pixelWrites=loadDefinedRanges(directory/"ps-defined-ranges.txt",1360);
        return result;
    }
    static NativePrimaryForwardMetadata loadGpuLayout(const std::filesystem::path& directory) {
        auto result=loadTemplates(directory);
        result.vertexWrites=loadDefinedRanges(directory/"vs-defined-ranges.txt",480);
        result.pixelWrites=loadDefinedRanges(directory/"ps-defined-ranges.txt",1360);
        return result;
    }
};
} // namespace San14::Render
