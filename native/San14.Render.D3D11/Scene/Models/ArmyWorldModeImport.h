#pragma once
#include "ArmyWorldPopulation.h"
#include "ArmyGraphicsActions.h"

// A once-imported mode resource library. It has no logical actors or current
// World population; the mode setter/physical pool creates those later.
struct ArmyWorldModeResources {
    struct Primitive {
        ArmyWorldTemplateLibrary::Primitive binding;
        uint32_t actualFinalTable{};
    };
    unsigned category{},capacity{},poolKind{},poolFlags{},commonExclude{};
    std::array<uint32_t,2> fps{};
    ArmyWorldTemplateLibrary::Actor actor;
    ArmyWorldPresentation constructor;
    NativeAccessoryPoseSource pose;
    NativeArmyDefaultAction defaultAction;
    NativeArmyGraphicsActions actions;
    std::array<float,6> localBounds{};
    std::vector<uint32_t> recordMasks,recordCapacities;
    std::vector<Primitive> primitives;
    std::map<uint32_t,uint64_t> tableOwners;
    NativeModelInputs models;
    std::vector<std::vector<char>> vertices;
    std::vector<std::array<uint32_t,3>> skinControls;
    std::vector<std::array<char,80>> pixels;
};

namespace army_world_mode_import_detail {
using native_accessory_pose_detail::Reader;
inline void schema(Reader& in,const std::array<char,8>& expected){
    std::array<char,8> magic{};in.read(magic.data(),8);
    if(magic!=expected)throw std::runtime_error("Army mode source schema");
}
inline void end(const Reader& in){
    if(in.at!=in.bytes.size())throw std::runtime_error("Army mode source trailing bytes");
}
inline NativeArmyDefaultAction player(Reader& in){
    NativeArmyDefaultAction row;
    row.category=in.get<uint32_t>();row.rawActionId=in.get<uint32_t>();
    row.ordinal=in.get<uint32_t>();row.clipFileId=in.get<uint32_t>();row.grouped=in.get<uint32_t>();
    row.controllerRate=in.get<float>();row.clipDuration=in.get<float>();row.loopDeclaration=in.get<float>();
    return row;
}
}

// Parse the sparse siege/naval packets independently of the contiguous 0..3
// loaders. Source identifiers, sizes and cross-file links are checked at load.
inline ArmyWorldModeResources readArmyWorldModeSource(const std::filesystem::path& directory){
    using namespace army_world_mode_import_detail;
    ArmyWorldModeResources r;
    Reader in(directory/"mode-resource.bin");schema(in,{'A','R','M','Y','M','O','D','1'});
    r.category=in.get<uint32_t>();const auto templates=in.get<uint32_t>(),records=in.get<uint32_t>();
    r.commonExclude=in.get<uint32_t>();r.actor.scalar=in.get<float>();r.actor.exclusion=in.get<uint32_t>();
    r.actor.mode=in.get<uint32_t>();r.actor.forceDistance=in.get<float>();r.actor.distanceBias=in.get<float>();
    in.read(&r.constructor.world,48);in.read(r.constructor.bounds.data(),80);
    if(r.category<4||r.category>9||templates==0||records==0||records>32||
            (r.actor.mode!=0&&r.actor.mode!=3)||!std::isfinite(r.actor.scalar)||
            !std::isfinite(r.actor.forceDistance)||!std::isfinite(r.actor.distanceBias))
        throw std::runtime_error("Army mode category/culler source fields");
    r.recordMasks.resize(records);r.recordCapacities.resize(records);
    for(unsigned record=0;record<records;++record){r.recordMasks[record]=in.get<uint32_t>();r.recordCapacities[record]=in.get<uint32_t>();}
    r.primitives.resize(templates);
    for(auto& row:r.primitives){auto& p=row.binding;
        p.draw=in.get<uint32_t>();p.sourceTemplate=in.get<uint32_t>();p.palette=in.get<uint32_t>();
        p.queue=in.get<uint32_t>();p.allowedRecords=in.get<uint32_t>();row.actualFinalTable=in.get<uint32_t>();
        in.get<uint64_t>(); // Independent package-local owner is never a global owner.
        if(p.sourceTemplate>=templates||!row.actualFinalTable||
                (p.queue!=49&&p.queue!=50&&p.queue!=54&&p.queue!=55)||
                (records<32&&(p.allowedRecords>>records)!=0))
            throw std::runtime_error("Army mode primitive source range");
    }
    end(in);
    Reader defaults(directory/"army-default-actions.bin");schema(defaults,{'A','R','M','Y','A','C','T','1'});
    if(defaults.get<uint32_t>()!=1)throw std::runtime_error("Army mode sparse default row count");
    for(auto& fps:r.fps){fps=defaults.get<uint32_t>();if(!fps)throw std::runtime_error("Army mode source FPS is zero");}
    r.defaultAction=player(defaults);end(defaults);
    if(r.defaultAction.category!=r.category||r.defaultAction.rawActionId!=0||r.defaultAction.grouped!=0||
            !r.defaultAction.clipFileId||!std::isfinite(r.defaultAction.controllerRate)||r.defaultAction.controllerRate<=0||
            !std::isfinite(r.defaultAction.clipDuration)||r.defaultAction.clipDuration<=0||
            !std::isfinite(r.defaultAction.loopDeclaration)||r.defaultAction.loopDeclaration>=0)
        throw std::runtime_error("Army mode actual independent default player");
    r.pose=prepareNativeArmyPoseSource(directory/"poses"/("category-"+std::to_string(r.category)));
    if(r.pose.duration8!=r.defaultAction.clipDuration)throw std::runtime_error("Army mode pose/default clip differ");
    for(const auto& row:r.primitives)if(row.binding.palette>=r.pose.palettes.size())
        throw std::runtime_error("Army mode primitive palette absent");
    r.actions=loadNativeArmyGraphicsActions(directory,std::span<const NativeAccessoryPoseSource>(&r.pose,1),
        std::span<const NativeArmyDefaultAction>(&r.defaultAction,1),r.category);
    const auto bounds=load(directory/"local-bounds.bin");
    if(bounds.size()!=24)throw std::runtime_error("Army source getter bounds size");
    std::memcpy(r.localBounds.data(),bounds.data(),24);
    Reader pool(directory/"pool-banks.bin");schema(pool,{'A','R','M','Y','P','O','L','1'});
    if(pool.get<uint32_t>()!=1||pool.get<uint32_t>()!=r.category)throw std::runtime_error("Army mode sparse pool category");
    r.capacity=pool.get<uint32_t>();r.poolKind=pool.get<uint32_t>();r.poolFlags=pool.get<uint32_t>();end(pool);
    if(!r.capacity||r.poolKind!=1||r.poolFlags!=0)throw std::runtime_error("Army mode requires actual loaded model bank kind/flags");
    Reader constructor(directory/"constructor-worlds.bin");schema(constructor,{'A','R','M','Y','C','S','T','1'});
    if(constructor.get<uint32_t>()!=1||constructor.get<uint32_t>()!=r.category)throw std::runtime_error("Army mode sparse constructor category");
    ArmyWorldPresentation actual;constructor.read(&actual.world,48);constructor.read(actual.bounds.data(),80);end(constructor);
    if(std::memcmp(&actual.world,&r.constructor.world,48)||std::memcmp(actual.bounds.data(),r.constructor.bounds.data(),80))
        throw std::runtime_error("Army mode constructor packets differ");
    std::ifstream tables(directory/"base-table-owners.txt");uint32_t table;uint64_t owner;
    if(!tables)throw std::runtime_error("Army actual base table owners absent");
    while(tables>>table>>owner)if(!table||!owner||!r.tableOwners.emplace(table,owner).second)
        throw std::runtime_error("Army actual base table owner identity duplicate");
    if(!tables.eof()||r.tableOwners.empty())throw std::runtime_error("Army actual base table owners incomplete");
    std::ifstream models(directory/"models.txt");std::string relative;
    if(!models)throw std::runtime_error("Army mode models list absent");
    while(models>>relative){
        auto vertex=load(directory/relative/"vs-b0.bin"),pixel=load(directory/relative/"ps-b0.bin");
        if((vertex.size()!=160&&vertex.size()!=6256)||pixel.size()!=80)throw std::runtime_error("Army mode actual VS/PS source size");
        r.vertices.push_back(std::move(vertex));std::array<char,80> bytes;std::memcpy(bytes.data(),pixel.data(),80);r.pixels.push_back(bytes);
        auto controls=load(directory/relative/"skin-controls.bin");
        if(controls.size()!=12)throw std::runtime_error("Army mode source matrix control size");
        std::array<uint32_t,3> words;std::memcpy(words.data(),controls.data(),12);r.skinControls.push_back(words);
    }
    if(r.vertices.size()!=templates)throw std::runtime_error("Army mode model/source template count differs");
    return r;
}

// The caller retains this TableID registry across all resource imports. Join
// actual full tables first, then allocate a new owner for a genuinely new table.
inline void bindArmyWorldModeTables(ArmyWorldModeResources& r,std::map<uint32_t,uint64_t>& actualTableOwners){
    for(const auto& [table,owner]:r.tableOwners){auto [entry,created]=actualTableOwners.emplace(table,owner);
        if(!created&&entry->second!=owner)throw std::runtime_error("Army actual base full table owner changed");}
    uint64_t next=0;for(const auto& [table,owner]:actualTableOwners)next=std::max(next,owner);
    for(auto& row:r.primitives){auto found=actualTableOwners.find(row.actualFinalTable);
        if(found==actualTableOwners.end())found=actualTableOwners.emplace(row.actualFinalTable,++next).first;
        row.binding.tableOwner=found->second;
    }
    r.tableOwners=actualTableOwners;
}
inline ArmyWorldModeResources importArmyWorldModeResources(ID3D11Device* device,
        const std::filesystem::path& directory,const NativeArmySource& base,
        std::map<uint32_t,uint64_t>& actualTableOwners){
    auto r=readArmyWorldModeSource(directory);
    if(r.commonExclude!=base.commonExclude||r.recordMasks!=base.recordMasks||
            r.recordCapacities!=base.capacities||r.fps!=base.defaultActions.fps)
        throw std::runtime_error("Army mode current source View/FPS differs from retained bank");
    bindArmyWorldModeTables(r,actualTableOwners);
    r.models=loadNativeModels(device,directory);
    if(r.models.draws.size()!=r.primitives.size())throw std::runtime_error("Army mode complete native model count");
    return r;
}

// Call before constructing ArmyWorldModelPool: that pool keeps spans to these
// category arrays. Normal World publication remains the sole actor producer.
inline void appendArmyWorldModeResources(ArmyWorldModeResources&& mode,NativeArmyPopulation& owner,
        ArmyWorldTemplateLibrary& library,NativeArmyGraphicsActions& actions,std::vector<unsigned>& capacities){
    const unsigned category=mode.category,first=unsigned(owner.templates.draws.size()),size=category+1;
    if(category<library.actors.size()&&library.actors[category])throw std::runtime_error("Army mode category already imported");
    owner.source.poses.resize(size);owner.source.defaultActions.rows.resize(size);
    library.actors.resize(size);library.primitives.resize(size);library.constructors.resize(size);library.defaultActionOrdinals.resize(size);capacities.resize(size);
    owner.source.poses[category]=std::move(mode.pose);owner.source.defaultActions.rows[category]=mode.defaultAction;
    library.actors[category]=mode.actor;library.constructors[category]=mode.constructor;library.defaultActionOrdinals[category]=mode.defaultAction.ordinal;
    library.localBounds[category]=mode.localBounds;
    capacities[category]=mode.capacity;
    for(auto& primitive:mode.primitives){primitive.binding.sourceTemplate+=first;library.primitives[category].push_back(primitive.binding);}
    for(auto& draw:mode.models.draws)owner.templates.draws.push_back(std::move(draw));
    for(auto& vertex:mode.vertices)owner.source.vertices.push_back(std::move(vertex));
    for(auto& controls:mode.skinControls)owner.source.skinControls.push_back(controls);
    for(auto& pixel:mode.pixels)owner.source.pixels.push_back(pixel);
    owner.source.templateCount=unsigned(owner.templates.draws.size());
    for(auto& [key,action]:mode.actions.rows)if(!actions.rows.emplace(key,std::move(action)).second)
        throw std::runtime_error("Army mode action already imported");
    library.tableOwners=std::move(mode.tableOwners);
}
