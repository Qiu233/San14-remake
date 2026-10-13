#pragma once
#include "ArmyWorldPopulation.h"
#include "ArmyDefaultActions.h"
#include <filesystem>
#include <fstream>

struct NativeArmyCategoryInputs {
    struct Primitive {ArmyWorldTemplateLibrary::Primitive binding;uint32_t table{};};
    unsigned category{},capacity{},commonExclude{};
    ArmyWorldTemplateLibrary::CategoryVisibility visibility;
    ArmyWorldPresentation constructor;
    std::array<float,6> localBounds{};
    NativeAccessoryPoseSource pose;
    NativeArmyDefaultAction defaultAction;
    std::array<uint32_t,2> fps{};
    std::vector<uint32_t> recordMasks,recordCapacities;
    std::vector<Primitive> primitives;
    std::map<uint32_t,uint64_t> tableOwners;
    std::map<uint64_t,std::pair<std::string,uint32_t>> tableIdentities;
};

inline NativeArmyCategoryInputs readNativeArmyCategoryInputs(const std::filesystem::path& directory){
    using native_accessory_pose_detail::Reader;
    auto end=[](const Reader& in){if(in.at!=in.bytes.size())throw std::runtime_error("Army category input trailing bytes");};
    auto magic=[](Reader& in,const std::array<char,8>& expected){std::array<char,8> value{};in.read(value.data(),8);
        if(value!=expected)throw std::runtime_error("Army category input schema");};
    NativeArmyCategoryInputs r;Reader in(directory/"mode-resource.bin");magic(in,{'A','R','M','Y','M','O','D','1'});
    r.category=in.get<uint32_t>();const auto templates=in.get<uint32_t>(),records=in.get<uint32_t>();
    r.commonExclude=in.get<uint32_t>();r.visibility.scalar=in.get<float>();r.visibility.exclusion=in.get<uint32_t>();
    r.visibility.mode=in.get<uint32_t>();r.visibility.forceDistance=in.get<float>();r.visibility.distanceBias=in.get<float>();
    in.read(&r.constructor.world,48);in.read(r.constructor.bounds.data(),80);
    if(r.category>9||!templates||records>32||(r.visibility.mode!=0&&r.visibility.mode!=3)||
        !std::isfinite(r.visibility.scalar)||!std::isfinite(r.visibility.forceDistance)||!std::isfinite(r.visibility.distanceBias))
        throw std::runtime_error("Army category visibility declaration");
    r.recordMasks.resize(records);r.recordCapacities.resize(records);
    for(unsigned record=0;record<records;++record){r.recordMasks[record]=in.get<uint32_t>();r.recordCapacities[record]=in.get<uint32_t>();}
    r.primitives.resize(templates);
    for(auto& row:r.primitives){auto& p=row.binding;
        p.draw=in.get<uint32_t>();p.sourceTemplate=in.get<uint32_t>();p.palette=in.get<uint32_t>();
        p.queue=in.get<uint32_t>();p.allowedRecords=in.get<uint32_t>();row.table=in.get<uint32_t>();in.get<uint64_t>();
        if(p.sourceTemplate>=templates||!row.table||(p.queue!=49&&p.queue!=50&&p.queue!=54&&p.queue!=55)||
            (records<32&&(p.allowedRecords>>records)!=0))throw std::runtime_error("Army category primitive declaration");
    }
    end(in);
    if(r.category==2||r.category==3){
        std::ifstream flags(directory/"flag-draws.txt");unsigned draw;int manager;
        if(!flags)throw std::runtime_error("Army category material bindings absent");
        while(flags>>draw>>manager){
            auto found=std::find_if(r.primitives.begin(),r.primitives.end(),[&](const auto& row){return row.binding.draw==draw;});
            if(found==r.primitives.end()||manager<0||manager>2||found->binding.materialDependent)
                throw std::runtime_error("Army category material binding invalid");
            found->binding.materialDependent=true;found->binding.materialManager=manager;
        }
        if(!flags.eof())throw std::runtime_error("Army category material binding malformed");
    }
    Reader defaults(directory/"army-default-actions.bin");magic(defaults,{'A','R','M','Y','A','C','T','1'});
    if(defaults.get<uint32_t>()!=1)throw std::runtime_error("Army category default player count");
    for(auto& fps:r.fps){fps=defaults.get<uint32_t>();if(!fps)throw std::runtime_error("Army category FPS is zero");}
    auto& action=r.defaultAction;action.category=defaults.get<uint32_t>();action.rawActionId=defaults.get<uint32_t>();
    action.ordinal=defaults.get<uint32_t>();action.clipFileId=defaults.get<uint32_t>();action.grouped=defaults.get<uint32_t>();
    action.controllerRate=defaults.get<float>();action.clipDuration=defaults.get<float>();action.loopDeclaration=defaults.get<float>();end(defaults);
    if(action.category!=r.category||action.rawActionId!=0||action.grouped!=(r.category<2?1u:0u)||!action.clipFileId||
        !std::isfinite(action.controllerRate)||action.controllerRate<=0||!std::isfinite(action.clipDuration)||action.clipDuration<=0||
        !std::isfinite(action.loopDeclaration)||action.loopDeclaration>=0)throw std::runtime_error("Army category default player declaration");
    r.pose=prepareNativeArmyPoseSource(directory/"poses"/("category-"+std::to_string(r.category)));
    if(r.pose.duration8!=action.clipDuration)throw std::runtime_error("Army category default pose/player mismatch");
    for(const auto& row:r.primitives)if(row.binding.palette>=r.pose.palettes.size())throw std::runtime_error("Army category palette missing");
    Reader bounds(directory/"local-bounds.bin");bounds.read(r.localBounds.data(),24);end(bounds);
    Reader pool(directory/"pool-banks.bin");magic(pool,{'A','R','M','Y','P','O','L','1'});
    if(pool.get<uint32_t>()!=1||pool.get<uint32_t>()!=r.category)throw std::runtime_error("Army category pool declaration");
    r.capacity=pool.get<uint32_t>();const auto kind=pool.get<uint32_t>(),flags=pool.get<uint32_t>();end(pool);
    if(!r.capacity||kind!=1||flags!=0)throw std::runtime_error("Army category pool kind/flags unsupported");
    std::ifstream tables(directory/"base-table-owners.txt");uint32_t table;uint64_t owner;
    if(!tables)throw std::runtime_error("Army category table owners absent");
    while(tables>>table>>owner)if(!table||!owner||!r.tableOwners.emplace(table,owner).second)
        throw std::runtime_error("Army category table owner duplicate");
    if(!tables.eof()||r.tableOwners.empty())throw std::runtime_error("Army category table owners incomplete");
    std::ifstream identities(directory/"table-identities.txt");std::string uri;
    if(!identities)throw std::runtime_error("Army category full table identities absent");
    while(identities>>owner>>uri>>table)if(!owner||!table||!r.tableIdentities.emplace(owner,std::pair{uri,table}).second)
        throw std::runtime_error("Army category full table identity duplicate");
    if(!identities.eof())throw std::runtime_error("Army category full table identity malformed");
    return r;
}

struct NativeArmyCategoryDeclaration {unsigned category{};std::filesystem::path directory;bool borrowedGeometry{};};
inline std::vector<NativeArmyCategoryDeclaration> readNativeArmyCategoryDeclarations(const std::filesystem::path& root){
    native_accessory_pose_detail::Reader in(root/"resource-bank.bin");
    if(in.get<uint32_t>()!=0x31425241||in.get<uint32_t>()!=10||in.at!=in.bytes.size())
        throw std::runtime_error("Army resource bank declaration");
    std::ifstream list(root/"categories.txt");std::vector<NativeArmyCategoryDeclaration> rows;
    unsigned category,borrow;std::string relative;
    while(list>>category>>relative>>borrow){
        if(category!=rows.size()||borrow!=(category==2?1u:0u))throw std::runtime_error("Army resource category ordering/borrow");
        rows.push_back({category,root/relative,borrow!=0});
    }
    if(!list.eof()||rows.size()!=10)throw std::runtime_error("Army resource categories incomplete");
    return rows;
}

struct NativeArmyResourceProfiles {
    struct Category {NativeArmyCategoryDeclaration declaration;unsigned templateCount{};};
    std::vector<Category> categories;
    ArmyWorldTemplateLibrary library;
    std::vector<NativeAccessoryPoseSource> poses;
    NativeArmyDefaultActions defaults;
    std::vector<unsigned> capacities;
    std::vector<uint32_t> recordMasks,recordCapacities;
    unsigned commonExclude{};
};
// Assemble the complete value-owned category library before any pool borrows
// its arrays. This stage has no actors, physical draws or readiness receipts.
inline NativeArmyResourceProfiles readNativeArmyResourceProfiles(const std::filesystem::path& root){
    NativeArmyResourceProfiles result;auto& library=result.library;
    result.poses.resize(10);result.defaults.rows.resize(10);result.capacities.resize(10);
    library.visibility.resize(10);library.primitives.resize(10);library.constructors.resize(10);
    library.defaultActionOrdinals.resize(10);
    unsigned first=0;
    for(const auto& declaration:readNativeArmyCategoryDeclarations(root)){
        auto input=readNativeArmyCategoryInputs(declaration.directory);const auto category=declaration.category;
        if(input.category!=category)throw std::runtime_error("Army category directory identity differs");
        if(category==0){result.recordMasks=input.recordMasks;result.recordCapacities=input.recordCapacities;
            result.commonExclude=input.commonExclude;result.defaults.fps=input.fps;
        }else if(input.recordMasks!=result.recordMasks||input.recordCapacities!=result.recordCapacities||
            input.commonExclude!=result.commonExclude||input.fps!=result.defaults.fps)
            throw std::runtime_error("Army category View/FPS declarations differ");
        library.templateRoutes.resize(first+input.primitives.size());
        for(auto& row:input.primitives){
            const auto& identity=input.tableIdentities.at(input.tableOwners.at(row.table));
            auto entry=library.tableOwners.find(identity);
            if(entry==library.tableOwners.end())entry=library.tableOwners.emplace(identity,library.tableOwners.size()+1).first;
            row.binding.tableOwner=entry->second;row.binding.sourceTemplate+=first;
            library.templateRoutes.at(row.binding.sourceTemplate)={row.binding.queue,row.binding.allowedRecords,row.binding.tableOwner};
            library.primitives[category].push_back(row.binding);
        }
        library.visibility[category]=input.visibility;library.constructors[category]=input.constructor;
        library.defaultActionOrdinals[category]=input.defaultAction.ordinal;library.localBounds[category]=input.localBounds;
        result.capacities[category]=input.capacity;result.poses[category]=std::move(input.pose);
        result.defaults.rows[category]=input.defaultAction;
        result.categories.push_back({declaration,unsigned(input.primitives.size())});first+=unsigned(input.primitives.size());
    }
    return result;
}
