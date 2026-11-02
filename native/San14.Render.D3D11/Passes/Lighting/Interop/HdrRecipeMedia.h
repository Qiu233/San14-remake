#pragma once
#include "../../../Runtime/PreparedFileInput.h"
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <map>
#include <sstream>
#include <stdexcept>
#include <string>
#include <vector>

namespace San14::Render::Interop {
// One import boundary, owned by readRecipe. Parsed HDR recipes retain their
// ordinary value/COM owners; no path registry or per-frame archive lookup exists.
class HdrRecipeMedia {
    std::filesystem::path directory_;
    bool packed_{};
    std::map<std::string,std::vector<char>> files_;

    static std::vector<char> readFile(const std::filesystem::path& path) {
        NativePreparedInputFile input(path,std::ios::binary|std::ios::ate);
        if(!input)throw std::runtime_error("HDR recipe media file is absent");
        const auto end=input.tellg();
        if(end<0)throw std::runtime_error("HDR recipe media file extent");
        std::vector<char> bytes(static_cast<std::size_t>(end));input.seekg(0);
        if(!bytes.empty())input.read(bytes.data(),static_cast<std::streamsize>(bytes.size()));
        if(!input)throw std::runtime_error("HDR recipe media file is truncated");
        return bytes;
    }
public:
    explicit HdrRecipeMedia(const std::filesystem::path& directory):directory_(directory) {
        const auto path=directory_/"media.bin";
        if(!std::filesystem::exists(nativePreparedFilePath(path)))return;
        packed_=true;
        const auto bytes=readFile(path);std::size_t at=0;
        auto word=[&]() {
            if(bytes.size()-at<4)throw std::runtime_error("HDR recipe media header is truncated");
            std::uint32_t value;std::memcpy(&value,bytes.data()+at,4);at+=4;return value;
        };
        if(word()!=0x314D5248u)throw std::runtime_error("HDR recipe media version");
        const auto count=word();
        if(count>(bytes.size()-at)/8)throw std::runtime_error("HDR recipe media entry count");
        for(std::uint32_t i=0;i<count;++i) {
            const auto length=word();
            if(!length||length>bytes.size()-at)throw std::runtime_error("HDR recipe media name extent");
            std::string name(bytes.data()+at,length);at+=length;
            if(name=="."||name==".."||name.find_first_of("/\\\0",0,3)!=std::string::npos)
                throw std::runtime_error("HDR recipe media entry name");
            const auto size=word();
            if(size>bytes.size()-at)throw std::runtime_error("HDR recipe media entry extent");
            std::vector<char> data(bytes.begin()+at,bytes.begin()+at+size);at+=size;
            if(!files_.emplace(std::move(name),std::move(data)).second)
                throw std::runtime_error("HDR recipe media duplicate entry");
        }
        if(at!=bytes.size())throw std::runtime_error("HDR recipe media trailing bytes");
    }
    bool exists(const std::string& name) const {
        return packed_?files_.contains(name):std::filesystem::exists(nativePreparedFilePath(directory_/name));
    }
    std::vector<char> read(const std::string& name) const {
        if(!packed_)return readFile(directory_/name);
        const auto found=files_.find(name);
        if(found==files_.end())throw std::runtime_error("HDR recipe media required entry is absent: "+name);
        return found->second;
    }
    std::istringstream open(const std::string& name) const {
        const auto bytes=read(name);
        return std::istringstream(std::string(bytes.begin(),bytes.end()),std::ios::in|std::ios::binary);
    }
};
} // namespace San14::Render::Interop
