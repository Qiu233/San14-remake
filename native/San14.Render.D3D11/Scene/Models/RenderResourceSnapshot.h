#pragma once
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <iterator>
#include <memory>
#include <optional>
#include <stdexcept>
#include <string>
#include <tuple>
#include <vector>

struct NativeRenderResourceDescriptor {
    using Identity=std::tuple<uint64_t,std::string,uint32_t,uint32_t,uint32_t,uint32_t,uint32_t,uint32_t,uint32_t>;
    uint32_t name{},kind{},classId{},rootFile{},ordinal{},targetResource{},targetObject{};
    uint64_t sourceScope{};
    std::string sourceUri;
    std::optional<uint64_t> blendStateValue;
    Identity identity()const {
        return std::tuple{sourceScope,sourceUri,name,kind,classId,rootFile,ordinal,targetResource,targetObject};
    }
};

struct NativeRenderResourceSnapshot {
    uint64_t generation{};
    std::vector<std::shared_ptr<const NativeRenderResourceDescriptor>> descriptors;
};

// Session input reader. Parsing completes before the material namespace changes.
class NativeRenderResourceSnapshots {
public:
    explicit NativeRenderResourceSnapshots(const std::filesystem::path& directory) {
        std::ifstream locator(directory/"context-directory.txt");std::string component,extra;
        if(!(locator>>component)||(locator>>extra)||component=="."||component==".."||
                component.find_first_of("/\\:")!=std::string::npos)
            throw std::runtime_error("Army resource Context snapshot directory absent or invalid");
        root_=directory/component;
    }
    NativeRenderResourceSnapshot read(uint64_t generation)const {
        std::ifstream input(root_/std::to_string(generation)/"descriptors.bin",std::ios::binary);
        if(!input)throw std::runtime_error("Army resource Context descriptors absent");
        std::vector<char> bytes{std::istreambuf_iterator<char>(input),{}};
        if(input.bad())throw std::runtime_error("Army resource Context descriptors could not be read");
        size_t at=0;
        auto word=[&] {
            if(bytes.size()-at<4)throw std::runtime_error("Army resource Context truncated");
            uint32_t value;std::memcpy(&value,bytes.data()+at,4);at+=4;return value;
        };
        auto wide=[&] {
            if(bytes.size()-at<8)throw std::runtime_error("Army resource Context truncated scope");
            uint64_t value;std::memcpy(&value,bytes.data()+at,8);at+=8;return value;
        };
        const auto format=word();
        if(format!=0x31434352&&format!=0x32434352)
            throw std::runtime_error("Army resource Context requires RCC1 or RCC2");
        const auto count=word();
        if(count>(bytes.size()-at)/40)throw std::runtime_error("Army resource Context row count invalid");
        NativeRenderResourceSnapshot snapshot;snapshot.generation=generation;
        snapshot.descriptors.reserve(count);
        uint32_t prior=0;
        for(uint32_t i=0;i<count;++i) {
            auto row=std::make_shared<NativeRenderResourceDescriptor>();
            row->name=word();row->kind=word();row->classId=word();row->rootFile=word();
            row->ordinal=word();row->targetResource=word();row->targetObject=word();
            row->sourceScope=wide();
            const auto length=word();
            if(!row->name||(i&&row->name<=prior)||row->kind>1||length>bytes.size()-at||!length)
                throw std::runtime_error("Army resource Context descriptor invalid");
            row->sourceUri.assign(bytes.data()+at,length);at+=length;
            if(row->sourceUri.find('\0')!=std::string::npos)
                throw std::runtime_error("Army resource Context URI contains a null byte");
            if(format==0x32434352){
                const auto payload=word();
                if(payload){
                    if(payload!=8||row->kind!=0||row->classId!=0x78E5679D)
                        throw std::runtime_error("Army resource Context typed payload is unsupported");
                    row->blendStateValue=wide();
                }
            }
            prior=row->name;
            snapshot.descriptors.push_back(std::move(row));
        }
        if(at!=bytes.size())throw std::runtime_error("Army resource Context has extra bytes");
        return snapshot;
    }
private:
    std::filesystem::path root_;
};
