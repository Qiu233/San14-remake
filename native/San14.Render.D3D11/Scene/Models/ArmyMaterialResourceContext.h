#pragma once
#include "RenderResourceSnapshot.h"
#include <algorithm>
#include <array>
#include <cstdint>
#include <cstring>
#include <map>
#include <memory>
#include <optional>
#include <stdexcept>
#include <string>
#include <tuple>
#include <utility>
#include <vector>

struct NativeAccessoryMaterial;

// Each accepted resource snapshot supplies the current named descriptors.
// Material instances retain captured Data when a later snapshot replaces names.
class NativeArmyMaterialResourceContext {
public:
    using Descriptor=NativeRenderResourceDescriptor;
    struct TextureGroupData {
        std::shared_ptr<const Descriptor> source;
    };
    struct TextureGroupBinding {
        std::shared_ptr<TextureGroupData> data;
        std::shared_ptr<NativeAccessoryMaterial> record;
    };
    struct BlendStateData {
        std::shared_ptr<const Descriptor> source;
        std::shared_ptr<const uint64_t> record;
    };
    struct StageParameters {
        std::array<uint8_t,204> bytes{};
    };
    struct StageRegistration {
        int32_t slot{},primary{},secondary{};
    };
    struct StageTableData {
        uint32_t resource{};
        std::array<uint8_t,200> payload{};
        std::shared_ptr<const StageParameters> parameters;
    };
    struct CapturedMaterialData {
        std::shared_ptr<const Descriptor> source;
        uint32_t regularName{},targetName{},alternateName{},blendStateName{},stageTableResource{};
        std::vector<uint8_t> filenameContext;
        std::shared_ptr<TextureGroupData> regular,target,alternate;
        std::shared_ptr<BlendStateData> blendState;
        std::shared_ptr<StageTableData> stageTable;
        std::optional<std::shared_ptr<const StageParameters>> stageParameters;
    };
    struct ManagerMaterialDeclaration {
        uint32_t name{},ordinal{};
        uint64_t scope{};
        std::string sourceUri;
        uint32_t regular{},target{},alternate{},blendState{},stageTableResource{};
        std::vector<uint8_t> filenameContext;
    };
    struct ManagerRows {
        uint32_t manager{};
        std::vector<std::pair<uint32_t,uint32_t>> rows;
    };
    struct ManagerInitialization {
        uint64_t generation{};
        std::vector<ManagerRows> managers;
        std::map<uint32_t,ManagerMaterialDeclaration> materials;
        std::map<uint32_t,std::vector<uint8_t>> stageBlobs;
    };
    using ManagerAssignments=std::map<std::pair<uint32_t,uint32_t>,std::shared_ptr<CapturedMaterialData>>;

    bool isCurrentGeneration(uint64_t generation)const {
        return generation_&&*generation_==generation;
    }
    void selectSnapshot(const NativeRenderResourceSnapshot& snapshot) {
        if(isCurrentGeneration(snapshot.generation))return;
        std::map<uint32_t,std::shared_ptr<const Descriptor>> candidate;
        std::map<uint32_t,std::shared_ptr<TextureGroupData>> candidateGroups;
        std::map<uint32_t,std::shared_ptr<CapturedMaterialData>> candidateMaterials;
        std::map<uint32_t,std::shared_ptr<BlendStateData>> candidateBlendStates;
        auto retained=retainedDescriptors_;
        for(const auto& row:snapshot.descriptors) {
            const auto key=row->identity();
            auto found=retained.find(key);
            auto reused=found==retained.end()?nullptr:found->second.lock();
            std::shared_ptr<const Descriptor> descriptor=reused?std::move(reused):row;
            auto group=textureGroups_.find(key);
            if(group!=textureGroups_.end())if(auto data=group->second.lock())candidateGroups.emplace(row->name,std::move(data));
            auto material=materials_.find(key);
            if(material!=materials_.end())if(auto data=material->second.lock())candidateMaterials.emplace(row->name,std::move(data));
            auto blend=blendStates_.find(key);
            if(blend!=blendStates_.end())if(auto data=blend->second.lock())candidateBlendStates.emplace(row->name,std::move(data));
            retained[key]=descriptor;candidate.emplace(row->name,std::move(descriptor));
        }
        for(auto it=retained.begin();it!=retained.end();)
            if(it->second.expired())it=retained.erase(it);else ++it;
        descriptors_.swap(candidate);currentTextureGroups_.swap(candidateGroups);
        currentMaterials_.swap(candidateMaterials);
        currentBlendStates_.swap(candidateBlendStates);
        retainedDescriptors_.swap(retained);generation_=snapshot.generation;
        for(auto it=textureGroups_.begin();it!=textureGroups_.end();)
            if(it->second.expired())it=textureGroups_.erase(it);else ++it;
        for(auto it=materials_.begin();it!=materials_.end();)
            if(it->second.expired())it=materials_.erase(it);else ++it;
        for(auto it=blendStates_.begin();it!=blendStates_.end();)
            if(it->second.expired())it=blendStates_.erase(it);else ++it;
    }

    std::shared_ptr<const Descriptor> lookup(uint32_t name)const {
        if(!generation_)throw std::runtime_error("Army resource Context snapshot has not been selected");
        if(!name)return {};
        auto found=descriptors_.find(name);
        return found==descriptors_.end()?nullptr:found->second;
    }
    std::shared_ptr<TextureGroupData> lookupTextureGroup(uint32_t name) {
        const auto source=lookup(name);if(!source)return {};
        if(source->kind!=0)
            throw std::runtime_error("Army texture-group reference requires its actual resource resolver");
        if(source->classId!=0x3059B9C3)
            throw std::runtime_error("Army texture-group descriptor requires another typed factory");
        auto& data=currentTextureGroups_[name];
        if(!data)data=std::make_shared<TextureGroupData>(TextureGroupData{source});
        textureGroups_[source->identity()]=data;
        return data;
    }
    std::shared_ptr<BlendStateData> lookupBlendStateDependency(uint32_t name) {
        const auto source=lookup(name);if(!source)return {};
        if(source->kind!=0||source->classId!=0x78E5679D)
            throw std::runtime_error("Army blend dependency requires its actual winner's typed factory");
        if(!source->blendStateValue)
            throw std::runtime_error("Army blend-state declaration has no typed CPU input");
        auto& data=currentBlendStates_[name];
        if(!data){
            const uint64_t value=*source->blendStateValue;
            auto& retained=blendRecords_[value];auto record=retained.lock();
            if(!record)record=std::make_shared<const uint64_t>(value);
            auto candidate=std::make_shared<BlendStateData>(BlendStateData{source,record});
            blendStates_[source->identity()]=candidate;retained=record;data=std::move(candidate);
        }
        return data;
    }
    static std::shared_ptr<StageTableData> loadStageTable(uint32_t resource,const std::vector<uint8_t>& blob) {
        if(blob.size()<8)throw std::runtime_error("Army stage resource GSTK header is truncated");
        uint32_t magic,version;std::memcpy(&magic,blob.data(),4);std::memcpy(&version,blob.data()+4,4);
        if(magic!=0x4B545347||version!=0)throw std::runtime_error("Army stage resource requires GSTK version zero");
        auto data=std::make_shared<StageTableData>();data->resource=resource;
        std::copy_n(blob.data()+8,std::min<size_t>(200,blob.size()-8),data->payload.data());return data;
    }
    static StageParameters prepareStageParameters(const StageTableData& source) {
        StageParameters result;std::copy(source.payload.begin(),source.payload.end(),result.bytes.begin());
        uint32_t hash=0,weight=1;
        for(uint8_t byte:source.payload){if(!byte)break;weight*=31;
            hash+=uint32_t(int32_t(int8_t(byte)))*weight;
        }
        std::memcpy(result.bytes.data()+200,&hash,4);
        int16_t entries;std::memcpy(&entries,source.payload.data(),2);
        const auto narrowedBits=uint16_t(12*int32_t(entries)+8);
        int16_t narrowed;std::memcpy(&narrowed,&narrowedBits,2);
        const auto units=uint16_t((int32_t(narrowed)+7)>>3);
        std::memcpy(result.bytes.data()+6,&units,2);return result;
    }
    static std::map<uint32_t,uint32_t> prepareStageTextureBindings(const StageParameters& parameters,
            const std::vector<StageRegistration>& registrations) {
        auto word=[&](size_t offset){uint16_t value;std::memcpy(&value,parameters.bytes.data()+offset,2);return value;};
        const auto count=word(0),primaryCount=word(2);const auto special=int16_t(word(4));
        if(!count||8u+12u*count>200||primaryCount>count||special<0||unsigned(special)>=count)
            throw std::runtime_error("Army captured stage table has an unsupported texture registration extent");
        auto decode=[](int32_t encoded){
            if(encoded<0||encoded>32767)throw std::runtime_error("Army stage registration requires its supported signed encoding");
            const auto index=(encoded&7)+8*(((encoded>>3)&15)+10*(encoded>>7));
            if(index>=88)throw std::runtime_error("Army stage registration exceeds its source namespace");return index;
        };
        std::map<uint32_t,uint32_t> bindings{{0,word(8)},{1,word(8+12*unsigned(special))}};
        for(unsigned i=primaryCount;i<count;++i){
            const auto sourceSlot=word(8+12*i),primary=word(12+12*i),secondary=word(14+12*i);
            const StageRegistration* first=nullptr;const StageRegistration* second=nullptr;
            for(const auto& registration:registrations){
                if(registration.primary&&decode(registration.primary)==decode(primary)){
                    if(first)throw std::runtime_error("Army primary shader texture registration is ambiguous");first=&registration;
                }
                if(registration.secondary&&registration.secondary!=registration.primary&&decode(registration.secondary)==decode(secondary)){
                    if(second)throw std::runtime_error("Army secondary shader texture registration is ambiguous");second=&registration;
                }
            }
            if(!first||(!second&&secondary&&secondary!=primary))
                throw std::runtime_error("Army captured stage entry lacks its actual shader registration");
            if(first->slot<0||first->slot>=16||(second&&(second->slot<0||second->slot>=16)))
                throw std::runtime_error("Army shader texture registration has an unsupported destination slot");
            bindings[unsigned(first->slot)]=sourceSlot;if(second)bindings[unsigned(second->slot)]=sourceSlot;
        }
        if(bindings.size()!=4||!bindings.contains(0)||!bindings.contains(1)||!bindings.contains(2)||!bindings.contains(3))
            throw std::runtime_error("Army stage bindings require the actual four-slot pixel interface");
        return bindings;
    }
    std::shared_ptr<CapturedMaterialData> captureMaterial(uint32_t name,const std::string& uri,uint32_t ordinal,
            uint32_t regular,uint32_t target,uint32_t alternate,uint32_t blendState,uint32_t stageResource,
            const std::vector<uint8_t>& filename) {
        const auto source=lookup(name);
        if(!source||source->kind!=0||source->classId!=0xA8F14404)
            throw std::runtime_error("Army selected named Material requires its actual typed factory");
        if(source->sourceUri!=uri||source->ordinal!=ordinal)
            throw std::runtime_error("Army selected Material media belongs to another resource descriptor");
        auto& data=currentMaterials_[name];
        if(data){
            if(std::tie(data->regularName,data->targetName,data->alternateName,data->blendStateName,data->stageTableResource)!=
                    std::tie(regular,target,alternate,blendState,stageResource)||data->filenameContext!=filename)
                throw std::runtime_error("Army captured Material declarations changed without an actual assignment");
            return data;
        }
        auto candidate=std::make_shared<CapturedMaterialData>();candidate->source=source;
        candidate->regularName=regular;candidate->targetName=target;
        candidate->alternateName=alternate;candidate->blendStateName=blendState;candidate->stageTableResource=stageResource;
        candidate->filenameContext=filename;
        candidate->regular=lookupTextureGroup(regular);candidate->target=lookupTextureGroup(target);
        candidate->alternate=lookupTextureGroup(alternate);candidate->blendState=lookupBlendStateDependency(blendState);
        if(stageResource){const auto stage=stageTables_.find(stageResource);
            if(stage==stageTables_.end())throw std::runtime_error("Army captured stage resource has no actual file-pool source");
            candidate->stageTable=stage->second;
        }
        // Only successful factory capture is retained. Later image or template
        // preparation failure does not repeat these lookups against a new scope.
        materials_[source->identity()]=candidate;data=std::move(candidate);return data;
    }
    ManagerAssignments initializeManagerRows(const ManagerInitialization& input, const NativeRenderResourceSnapshot& snapshot) {
        if(input.generation!=snapshot.generation)
            throw std::runtime_error("Army manager initialization belongs to another resource snapshot");
        selectSnapshot(snapshot);
        for(const auto& [resource,blob]:input.stageBlobs){
            auto candidate=loadStageTable(resource,blob);const auto previous=stageTables_.find(resource);
            if(previous!=stageTables_.end()){
                if(previous->second->payload!=candidate->payload)
                    throw std::runtime_error("Army stage resource changed without replacing its actual file-pool owner");
            }else stageTables_.emplace(resource,std::move(candidate));
        }
        ManagerAssignments assignments;
        for(const auto& manager:input.managers)for(const auto& [row,name]:manager.rows){
            const auto source=name?lookup(name):nullptr;
            std::shared_ptr<CapturedMaterialData> data;
            const auto declaration=input.materials.find(name);
            if(source){
                if(declaration==input.materials.end())
                    throw std::runtime_error("Army manager Material descriptor has no initialization declaration");
                const auto& fields=declaration->second;
                if(source->sourceScope!=fields.scope||source->sourceUri!=fields.sourceUri||source->ordinal!=fields.ordinal)
                    throw std::runtime_error("Army manager Material initialization belongs to another descriptor scope");
                data=captureMaterial(name,fields.sourceUri,fields.ordinal,
                    fields.regular,fields.target,fields.alternate,fields.blendState,fields.stageTableResource,fields.filenameContext);
            }else if(name)
                throw std::runtime_error("Army nonzero manager Material lookup failed before initialization completed");
            if(!assignments.emplace(std::pair{manager.manager,row},std::move(data)).second)
                throw std::runtime_error("Army manager initialization repeats a source row");
        }
        return assignments;
    }
    void completeManagerStageParameters(const ManagerAssignments& assignments) {
        for(const auto& [row,data]:assignments)if(data&&!data->stageParameters){
            std::shared_ptr<const StageParameters> parameters;
            if(data->stageTable){
                if(!data->stageTable->parameters){
                    auto candidate=std::make_shared<StageParameters>(prepareStageParameters(*data->stageTable));
                    auto& retained=stageParameters_[candidate->bytes];auto existing=retained.lock();
                    parameters=existing?std::move(existing):std::move(candidate);retained=parameters;
                    data->stageTable->parameters=parameters;
                }else parameters=data->stageTable->parameters;
            }else if(!data->filenameContext.empty()&&data->filenameContext.front()!=0)
                throw std::runtime_error("Army filename stage resource requires its actual file producer");
            data->stageParameters=std::move(parameters);
        }
    }
    TextureGroupBinding bindRegularGroup(const std::shared_ptr<TextureGroupData>& data,
            const std::string& uri,uint32_t object,uint32_t ordinal,
            std::shared_ptr<NativeAccessoryMaterial> record) {
        if(data->source->sourceUri!=uri||data->source->name!=object||data->source->ordinal!=ordinal)
            throw std::runtime_error("Army regular image producer belongs to another resource descriptor");
        // The produced full-key record has its own lifetime. It does not replace
        // another material's record through the shared source Data association.
        return {data,std::move(record)};
    }
private:
    using Identity=Descriptor::Identity;
    std::optional<uint64_t> generation_;
    std::map<uint32_t,std::shared_ptr<const Descriptor>> descriptors_;
    std::map<uint32_t,std::shared_ptr<TextureGroupData>> currentTextureGroups_;
    std::map<uint32_t,std::shared_ptr<CapturedMaterialData>> currentMaterials_;
    std::map<uint32_t,std::shared_ptr<BlendStateData>> currentBlendStates_;
    std::map<Identity,std::weak_ptr<const Descriptor>> retainedDescriptors_;
    std::map<Identity,std::weak_ptr<TextureGroupData>> textureGroups_;
    std::map<Identity,std::weak_ptr<CapturedMaterialData>> materials_;
    std::map<Identity,std::weak_ptr<BlendStateData>> blendStates_;
    std::map<uint64_t,std::weak_ptr<const uint64_t>> blendRecords_;
    std::map<uint32_t,std::shared_ptr<StageTableData>> stageTables_;
    std::map<std::array<uint8_t,204>,std::weak_ptr<const StageParameters>> stageParameters_;
};
