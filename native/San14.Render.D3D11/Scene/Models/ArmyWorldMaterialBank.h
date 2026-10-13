#pragma once
#include "ArmyWorldMaterialImport.h"
#include "Accessories/AccessoryMaterials.h"
#include "ArmyMaterialResourceContext.h"

// Full-key material requests own completed image groups independently of the
// physical model pool. A requested job is not a registered material template.
class NativeArmyMaterials {
public:
    NativeArmyMaterials(ID3D11Device* device,const std::filesystem::path& root,NativeArmyPopulation& owner,
            ArmyWorldTemplateLibrary& library,NativeArmyMaterialResourceContext& resources,
            const NativeArmyMaterialResourceContext::ManagerInitialization& initialization,
            const NativeRenderResourceSnapshot& initialSnapshot):root_(root),resources_(resources),
                assignments_(resources.initializeManagerRows(initialization,initialSnapshot)){
        std::ifstream defaults(root_/"defaults.txt");unsigned category,draw,material;std::string relative;
        if(!defaults)throw std::runtime_error("Army default material declarations absent");
        while(defaults>>category>>draw>>material>>relative){
            const auto& rows=library.primitives.at(category);
            auto found=std::find_if(rows.begin(),rows.end(),[&](const auto& p){return p.draw==draw;});
            if(found==rows.end()||found->materialDependent||!material)throw std::runtime_error("Army default material binding differs");
            auto bindings=prepareNativeAccessoryStaticMaterial(device,root_/relative);
            auto& model=owner.templates.draws.at(found->sourceTemplate);
            model.textures=std::move(bindings.textures);model.samplers=std::move(bindings.samplers);
        }
        if(!defaults.eof())throw std::runtime_error("Army default material metadata malformed");
    }
    void prepareFreshWorldMaterials(ID3D11Device* device,ID3D11DeviceContext* context,NativeArmyPopulation& owner,
            ArmyWorldTemplateLibrary& library,const ArmyWorldPublication& world){
        resources_.completeManagerStageParameters(assignments_);
        std::set<NativeArmyMaterialKey> keys;
        for(const auto& actor:world.actors)for(const auto& primitive:library.primitives.at(actor.key.category))
            if(primitive.materialDependent)keys.insert({unsigned(actor.key.category),unsigned(primitive.materialManager),actor.material});
        for(const auto& key:keys)if(!completed_.contains(key))prepareKey(device,context,owner,library,key);
    }
private:
    void prepareKey(ID3D11Device* device,ID3D11DeviceContext* context,NativeArmyPopulation& owner,
            ArmyWorldTemplateLibrary& library,const NativeArmyMaterialKey& key){
        const auto relative=nativeArmyMaterialDirectory(key),directory=root_/relative;
        const auto input=readNativeArmyMaterialInput(directory,key);
        const auto candidates=readNativeArmyMaterialTemplates(directory);
        const auto assignment=std::pair{key.manager,input.materialSourceRow};
        const auto captured=assignments_.find(assignment);
        if(captured==assignments_.end()||!captured->second)
            throw std::runtime_error("Army requested source row has no initialized Material Data");
        const auto& material=*captured->second;
        if(material.source->sourceUri!=input.materialSourceUri||material.source->name!=input.selectedMaterial||
                material.source->ordinal!=input.materialSourceOrdinal||material.regularName!=input.regular||
                material.targetName!=input.target||material.alternateName!=input.alternate||
                material.stageTableResource!=input.stageTableResource||
                material.filenameContext!=input.filenameContext)
            throw std::runtime_error("Army manager row changed Material source without an actual assignment setter");
        const auto& regularData=material.regular;
        if(material.target||material.alternate)
            throw std::runtime_error("Army Material target/alternate descriptor requires its actual image-group producer");
        if(!material.stageParameters)
            throw std::runtime_error("Army Material stage parameters have not completed their actual producer");
        const auto& stageParameters=*material.stageParameters;
        if(!stageParameters)
            throw std::runtime_error("Army Material image binding requires its actual captured stage parameter record");
        auto produced=groups_.find(key);
        if(produced==groups_.end()&&regularData){
            auto group=std::make_shared<NativeAccessoryMaterial>(prepareNativeAccessoryStaticMaterial(device,directory));
            auto bytes=accessory_glyph::read(directory/"glyph-job.bin");std::array<unsigned,8> header{};
            if(bytes.size()!=48&&bytes.size()!=144)throw std::runtime_error("Army glyph job extent");
            std::memcpy(header.data(),bytes.data(),32);
            if(header[0]!=key.manager||header[1]!=unsigned(key.flag.force)||header[2]!=input.proxy||header[3]!=key.flag.glyph)
                throw std::runtime_error("Army image producer differs from current material identity");
            if(!producer_)producer_.emplace(device,root_);
            accessory_glyph::Atlas* atlas=nullptr;
            if(header[7]){auto found=atlases_.find(key.flag.glyph);
                if(found==atlases_.end())found=atlases_.emplace(key.flag.glyph,accessory_glyph::Atlas::create(device,root_,key.flag.glyph)).first;
                atlas=&found->second;
            }
            {NativeAccessoryProducerScope drawingState(context);
                group->glyph=producer_->execute(device,context,root_,relative,atlas);}
            if(!group->glyph.producerCompleted||!group->glyph.srv||!group->glyph.sourceSampler)
                throw std::runtime_error("Army image producer did not produce its actual resource group");
            group->textures.at(input.texture)=group->glyph.srv;group->samplers.at(input.sampler)=group->glyph.sourceSampler;
            auto binding=resources_.bindRegularGroup(regularData,input.regularSourceUri,input.regularSourceObject,
                input.regularSourceOrdinal,std::move(group));
            // Only the actual regular Data-present ready block installs mode
            // zero and its current texture record. Missing Data skips that block.
            produced=groups_.emplace(key,MaterialImages{binding.record,binding.record,std::move(binding)}).first;
        }
        if(produced==groups_.end())produced=groups_.emplace(key,MaterialImages{}).first;
        const auto& images=produced->second;
        if(!images.current)throw std::runtime_error("Army Material image binding has no current typed texture-group producer");
        struct SourceImage {ComPtr<ID3D11ShaderResourceView> view;ComPtr<ID3D11SamplerState> sampler;};
        std::map<uint32_t,SourceImage> sourceSlots;
        for(const auto& row:readNativeArmyMaterialSourceSlots(directory)){
            SourceImage source{images.current->textures.at(row.texture),images.current->samplers.at(row.sampler)};
            if(!source.view||!source.sampler||!sourceSlots.emplace(row.material,std::move(source)).second)
                throw std::runtime_error("Army Material source slot has no actual image pair or is duplicated");
        }
        const bool retainPrior=input.worldPreserve&&images.current==images.regular;
        // This caller creates fresh World records. Reusing a physical slot
        // retains its buffers and clock; it does not select a copied-record branch.
        const unsigned normalRole=regularData?(input.tables[2]?3u:input.tables[0]?1u:0u):0u;
        const unsigned role=retainPrior?0u:normalRole;
        if(!retainPrior&&!role)throw std::runtime_error("Army Material selected a null full table");
        std::vector<std::pair<unsigned,NativeArmyMaterialTemplateInput>> selected;
        for(const auto& primitive:library.primitives.at(key.category))if(primitive.materialManager==int(key.manager)){
            auto found=std::find_if(candidates.begin(),candidates.end(),[&](const auto& row){return row.draw==primitive.draw&&row.role==role;});
            if(found==candidates.end())throw std::runtime_error("Army selected material table has no candidate media");
            if((role&&found->table!=input.tables[role-1])||
                (found->queue!=49&&found->queue!=50&&found->queue!=54&&found->queue!=55))
                throw std::runtime_error("Army selected material candidate is unsupported or inconsistent");
            selected.emplace_back(primitive.sourceTemplate,*found);
        }
        if(selected.empty())throw std::runtime_error("Army current material manager has no actual draw binding");
        for(const auto& [base,candidate]:selected){
            auto model=loadNativeModelDraw(device,candidate.directory,candidate.directory.string(),&owner.templates.draws.at(base));
            const auto bindings=resources_.prepareStageTextureBindings(*stageParameters,readNativeArmyStageRegistrations(candidate.directory));
            for(const auto& [slot,materialSlot]:bindings){
                const auto source=sourceSlots.find(materialSlot);
                if(source==sourceSlots.end())throw std::runtime_error("Army captured stage entry requires another actual Material texture slot");
                model.textures.at(slot)=source->second.view;model.samplers.at(slot)=source->second.sampler;
            }
            auto vertex=load(candidate.directory/"vs-b0.bin"),pixel=load(candidate.directory/"ps-b0.bin"),skin=load(candidate.directory/"skin-controls.bin");
            if((vertex.size()!=160&&vertex.size()!=6256)||pixel.size()!=80||skin.size()!=12)
                throw std::runtime_error("Army selected material constant interface unsupported");
            std::array<char,80> pixelValues{};std::memcpy(pixelValues.data(),pixel.data(),80);
            std::array<unsigned,3> controls{};std::memcpy(controls.data(),skin.data(),12);
            auto table=library.tableOwners.find({candidate.tableUri,candidate.table});
            if(table==library.tableOwners.end())table=library.tableOwners.emplace(std::pair{candidate.tableUri,candidate.table},library.tableOwners.size()+1).first;
            const auto index=unsigned(owner.templates.draws.size());owner.templates.draws.push_back(std::move(model));
            owner.source.vertices.push_back(std::move(vertex));owner.source.pixels.push_back(pixelValues);owner.source.skinControls.push_back(controls);
            library.templateRoutes.push_back({candidate.queue,candidate.allowedRecords,table->second});
            library.registerCompleted(base,key.flag,index);
        }
        owner.source.templateCount=unsigned(owner.templates.draws.size());completed_.insert(key);
    }
    std::filesystem::path root_;
    NativeArmyMaterialResourceContext& resources_;
    std::optional<accessory_glyph::StockProducer> producer_;
    std::map<unsigned,accessory_glyph::Atlas> atlases_;
    struct MaterialImages {
        std::shared_ptr<NativeAccessoryMaterial> regular,current;
        NativeArmyMaterialResourceContext::TextureGroupBinding binding;
    };
    std::map<NativeArmyMaterialKey,MaterialImages> groups_;
    NativeArmyMaterialResourceContext::ManagerAssignments assignments_;
    std::set<NativeArmyMaterialKey> completed_;
};
