#pragma once
#include "../../Runtime/PreparedFileInput.h"
#include "ArmyWorldPopulation.h"
#include "ArmyMaterialResourceContext.h"
#include <iomanip>
#include <sstream>

inline NativeArmyMaterialResourceContext::ManagerInitialization readNativeArmyManagerInitialization(
        const std::filesystem::path& directory){
    using native_accessory_pose_detail::Reader;
    NativeArmyMaterialResourceContext::ManagerInitialization result;
    Reader input(directory/"manager-initialization.bin");
    if(input.get<uint32_t>()!=0x31494D41)throw std::runtime_error("Army managers require AMI1");
    result.generation=input.get<uint64_t>();const auto count=input.get<uint32_t>();
    if(count!=3||count>(input.bytes.size()-input.at)/8)
        throw std::runtime_error("Army manager initialization requires its three actual managers");
    for(uint32_t i=0;i<count;++i){
        NativeArmyMaterialResourceContext::ManagerRows manager;
        manager.manager=input.get<uint32_t>();const auto rows=input.get<uint32_t>();
        if(manager.manager!=i||rows!=52||rows>(input.bytes.size()-input.at)/8)
            throw std::runtime_error("Army manager row declaration is unsupported or truncated");
        for(uint32_t j=0;j<rows;++j){const auto row=input.get<uint32_t>(),name=input.get<uint32_t>();
            if(row!=j+1)throw std::runtime_error("Army manager source row is outside its declared initialization order");
            manager.rows.emplace_back(row,name);
        }
        result.managers.push_back(std::move(manager));
    }
    auto raw=[&]{
        const auto length=input.get<uint32_t>();
        if(length>input.bytes.size()-input.at)throw std::runtime_error("Army manager raw declaration is truncated");
        std::vector<uint8_t> bytes(length);input.read(bytes.data(),length);return bytes;
    };
    const auto materials=input.get<uint32_t>();
    if(materials>(input.bytes.size()-input.at)/44)throw std::runtime_error("Army manager Material count exceeds declaration bytes");
    for(uint32_t i=0;i<materials;++i){
        NativeArmyMaterialResourceContext::ManagerMaterialDeclaration material;
        material.name=input.get<uint32_t>();material.ordinal=input.get<uint32_t>();material.scope=input.get<uint64_t>();
        material.regular=input.get<uint32_t>();material.target=input.get<uint32_t>();material.alternate=input.get<uint32_t>();
        material.blendState=input.get<uint32_t>();material.stageTableResource=input.get<uint32_t>();
        const auto uri=raw();material.sourceUri.assign(uri.begin(),uri.end());material.filenameContext=raw();
        if(material.sourceUri.empty()||material.sourceUri.find('\0')!=std::string::npos)
            throw std::runtime_error("Army manager Material URI is invalid");
        if(!material.name||!result.materials.emplace(material.name,material).second)
            throw std::runtime_error("Army manager Material declaration repeats a named source");
    }
    const auto stages=input.get<uint32_t>();
    if(stages>(input.bytes.size()-input.at)/8)throw std::runtime_error("Army stage source count exceeds declaration bytes");
    for(uint32_t i=0;i<stages;++i){const auto resource=input.get<uint32_t>();auto blob=raw();
        if(!resource||!result.stageBlobs.emplace(resource,std::move(blob)).second)
            throw std::runtime_error("Army manager stage resource is null or repeated");
    }
    if(input.at!=input.bytes.size())throw std::runtime_error("Army manager initialization has extra bytes");
    return result;
}
struct NativeArmyMaterialSourceSlot {uint32_t material{},texture{},sampler{};};
inline std::vector<NativeArmyMaterialSourceSlot> readNativeArmyMaterialSourceSlots(const std::filesystem::path& directory){
    NativePreparedInputFile input(directory/"stage-source-slots.txt");
    if(!input)throw std::runtime_error("Army actual Material texture source slots absent");
    std::vector<NativeArmyMaterialSourceSlot> rows;NativeArmyMaterialSourceSlot row;
    while(input>>row.material){
        if(!(input>>row.texture>>row.sampler)||row.texture>=16||row.sampler>=16)
            throw std::runtime_error("Army Material texture source slot is incomplete or unsupported");
        rows.push_back(row);
    }
    if(!input.eof()||rows.empty())throw std::runtime_error("Army Material texture source slot declaration malformed");
    return rows;
}
inline std::vector<NativeArmyMaterialResourceContext::StageRegistration> readNativeArmyStageRegistrations(
        const std::filesystem::path& directory){
    NativePreparedInputFile input(directory/"stage-registrations.txt");
    if(!input)throw std::runtime_error("Army actual shader stage registrations absent");
    std::vector<NativeArmyMaterialResourceContext::StageRegistration> rows;
    NativeArmyMaterialResourceContext::StageRegistration row;
    while(input>>row.slot){
        if(!(input>>row.primary>>row.secondary))throw std::runtime_error("Army shader stage registration is incomplete");
        rows.push_back(row);
    }
    if(!input.eof()||rows.size()!=3)throw std::runtime_error("Army shader stage registration inventory differs");
    return rows;
}

struct NativeArmyMaterialKey {
    unsigned category{},manager{};ArmyWorldMaterialKey flag;
    auto tuple() const {return std::tie(category,manager,flag.force,flag.color49,flag.glyph);}
    bool operator<(const NativeArmyMaterialKey& other)const{return tuple()<other.tuple();}
};
inline std::filesystem::path nativeArmyMaterialDirectory(const NativeArmyMaterialKey& key){
    std::ostringstream text;text<<key.flag.force<<'-'<<key.flag.color49<<'-'<<std::uppercase<<std::hex
        <<std::setw(4)<<std::setfill('0')<<key.flag.glyph;
    return std::filesystem::path("requests")/std::to_string(key.category)/text.str()/std::to_string(key.manager);
}
struct NativeArmyMaterialInput {
    NativeArmyMaterialKey key;
    unsigned selectedMaterial{},gateMaterial{},worldPreserve{},regular{},target{},alternate{};
    std::array<unsigned,4> tables{};
    unsigned stageTableResource{};
    std::vector<uint8_t> filenameContext;
    std::string regularSourceUri;
    uint32_t regularSourceObject{},regularSourceOrdinal{};
    std::string materialSourceUri;
    uint32_t materialSourceObject{},materialSourceOrdinal{},materialSourceRow{};
    unsigned proxy{},texture{},sampler{};
};
inline NativeArmyMaterialInput readNativeArmyMaterialInput(const std::filesystem::path& directory,
        const NativeArmyMaterialKey& expected){
    using native_accessory_pose_detail::Reader;
    Reader in(directory/"material-state.bin");
    if(in.get<unsigned>()!=0x33534D41)throw std::runtime_error("Army material requires AMS3");
    NativeArmyMaterialInput r;r.key.category=in.get<unsigned>();r.key.manager=in.get<unsigned>();
    r.key.flag.force=in.get<int>();r.key.flag.color49=in.get<int>();r.key.flag.glyph=in.get<unsigned>();
    r.selectedMaterial=in.get<unsigned>();r.gateMaterial=in.get<unsigned>();r.worldPreserve=in.get<unsigned>();
    r.regular=in.get<unsigned>();r.target=in.get<unsigned>();r.alternate=in.get<unsigned>();
    for(auto& table:r.tables)table=in.get<unsigned>();
    r.stageTableResource=in.get<unsigned>();
    if(in.at!=in.bytes.size()||r.key.tuple()!=expected.tuple()||r.key.category<2||r.key.category>3||r.key.manager>2||
        r.key.flag.force<0||r.key.flag.force>=52||r.key.flag.color49< -1||r.key.flag.color49>255||r.key.flag.glyph>65535||
        !r.selectedMaterial||!r.gateMaterial)
        throw std::runtime_error("Army material declaration differs from current full identity or supported owner");
    Reader material(directory/"material.bin");
    if(material.get<unsigned>()!=0x314D4641||material.get<unsigned>()!=r.key.manager||
        material.get<int>()!=r.key.flag.force||material.get<int>()!=r.key.flag.color49||material.get<unsigned>()!=r.key.flag.glyph)
        throw std::runtime_error("Army image job identity differs from material");
    r.proxy=material.get<unsigned>();r.texture=material.get<unsigned>();r.sampler=material.get<unsigned>();
    if(material.at!=material.bytes.size()||r.texture>=16||r.sampler>=16)throw std::runtime_error("Army material image binding invalid");
    r.filenameContext=std::move(Reader(directory/"filename-context.bin").bytes);
    NativePreparedInputFile source(directory/"texture-group-source.txt");std::string extra;
    if(!(source>>r.regularSourceUri>>r.regularSourceObject>>r.regularSourceOrdinal)||(source>>extra))
        throw std::runtime_error("Army regular texture-group source identity absent or malformed");
    NativePreparedInputFile materialSource(directory/"material-source.txt");
    if(!(materialSource>>r.materialSourceUri>>r.materialSourceObject>>r.materialSourceOrdinal>>r.materialSourceRow)||
            (materialSource>>extra)||r.materialSourceObject!=r.selectedMaterial||!r.materialSourceRow)
        throw std::runtime_error("Army selected Material source assignment absent or malformed");
    return r;
}
struct NativeArmyMaterialTemplateInput {
    unsigned draw{},table{},role{},queue{},allowedRecords{};std::filesystem::path directory;std::string tableUri;
};
inline std::vector<NativeArmyMaterialTemplateInput> readNativeArmyMaterialTemplates(const std::filesystem::path& directory){
    NativePreparedInputFile list(directory/"templates.txt");std::vector<NativeArmyMaterialTemplateInput> rows;
    NativeArmyMaterialTemplateInput row;std::string relative;int queue;
    if(!list)throw std::runtime_error("Army material candidate templates absent");
    while(list>>row.draw>>row.table>>row.role>>relative>>queue>>row.allowedRecords>>row.tableUri){
        if(!row.table||row.role>4)throw std::runtime_error("Army material candidate identity invalid");
        row.queue=unsigned(queue);row.directory=directory/relative;rows.push_back(row);
    }
    if(!list.eof())throw std::runtime_error("Army material candidate metadata malformed");
    return rows;
}
