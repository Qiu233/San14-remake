#pragma once
#include "ArmyPose.h"
#include "ArmyDefaultActions.h"
#include "Accessories/AccessoryGlyphRenderer.h"
#include "ArmyRigidWorldConstants.h"
#include <map>

// Include after model-native.h/model-continuous-native.h. Army retains its
// real skin6256/rigid160 CBs, current root, palette and independent light records.
struct NativeArmyActor {
    uint32_t index{},category{},exclusion{},mode{};float slotScalar{},forceDistance{},distanceBias{};
    std::array<float,20> obb{};NativeAccessoryPoseRoot world{};
};
struct NativeArmyPrimitive {uint32_t actor{},draw{},sourceTemplate{},palette{},queue{},allowedRecords{};uint64_t tableOwner{};};
struct NativeArmySource {
    unsigned templateCount{},commonExclude{};
    std::vector<uint32_t> recordMasks,capacities;std::vector<NativeArmyActor> actors;std::vector<NativeArmyPrimitive> primitives;
    std::vector<NativeAccessoryPoseSource> poses;NativeArmyDefaultActions defaultActions;
    std::vector<std::vector<char>> vertices;
    std::vector<std::array<uint32_t,3>> skinControls;
    std::vector<std::array<char,80>> pixels;std::vector<std::string> identities;
};
inline NativeArmySource prepareNativeArmySource(const fs::path& directory){
    using native_accessory_pose_detail::Reader;
    Reader in(directory/"population.bin");std::array<char,8> magic{};in.read(magic.data(),8);
    if(magic!=std::array<char,8>{'A','R','M','Y','P','O','P','1'})throw std::runtime_error("Army population source schema");
    NativeArmySource r;r.defaultActions=prepareNativeArmyDefaultActions(directory);
    auto actors=in.get<uint32_t>(),primitives=in.get<uint32_t>();r.templateCount=in.get<uint32_t>();auto records=in.get<uint32_t>();r.commonExclude=in.get<uint32_t>();
    if(records>32)throw std::runtime_error("Army independent record count");r.recordMasks.resize(records);r.capacities.resize(records);
    for(unsigned i=0;i<records;++i){r.recordMasks[i]=in.get<uint32_t>();r.capacities[i]=in.get<uint32_t>();}
    r.actors.resize(actors);r.poses.resize(r.defaultActions.rows.size());std::vector<bool> loaded(r.poses.size());
    for(auto& a:r.actors){a.index=in.get<uint32_t>();a.category=in.get<uint32_t>();a.slotScalar=in.get<float>();a.exclusion=in.get<uint32_t>();a.mode=in.get<uint32_t>();
        a.forceDistance=in.get<float>();a.distanceBias=in.get<float>();in.read(a.obb.data(),80);in.read(&a.world,48);
        if(a.index>=actors||a.category>=r.poses.size()||(a.mode!=0&&a.mode!=3)||!std::isfinite(a.slotScalar))throw std::runtime_error("Army source current actor range");
        if(!loaded[a.category]){r.poses[a.category]=prepareNativeArmyPoseSource(directory/"poses"/("category-"+std::to_string(a.category)));
            if(r.poses[a.category].duration8!=r.defaultActions.rows[a.category].clipDuration)throw std::runtime_error("Army imported default clip/pose duration differs");loaded[a.category]=true;}}
    r.primitives.resize(primitives);for(auto& p:r.primitives){p.actor=in.get<uint32_t>();p.draw=in.get<uint32_t>();p.sourceTemplate=in.get<uint32_t>();p.palette=in.get<uint32_t>();
        p.queue=in.get<uint32_t>();p.allowedRecords=in.get<uint32_t>();p.tableOwner=in.get<uint64_t>();
        if(p.actor>=actors||p.sourceTemplate>=r.templateCount||(p.queue!=49&&p.queue!=50&&p.queue!=54&&p.queue!=55)||p.palette>=r.poses[r.actors[p.actor].category].palettes.size())throw std::runtime_error("Army source primitive range/queue/palette");}
    if(in.at!=in.bytes.size())throw std::runtime_error("Army population source trailing bytes");
    std::ifstream names(directory/"identities.txt");std::string name;while(names>>name)r.identities.push_back(name);
    if(r.identities.size()!=r.actors.size())throw std::runtime_error("Army actual owner identities absent");
    std::ifstream models(directory/"models.txt");std::string relative;while(models>>relative){auto bytes=load(directory/relative/"ps-b0.bin");if(bytes.size()!=80)throw std::runtime_error("Army original PS80 profile");
        std::array<char,80> b{};std::memcpy(b.data(),bytes.data(),80);r.pixels.push_back(b);
        auto vertex=load(directory/relative/"vs-b0.bin");
        if(vertex.size()!=160&&vertex.size()!=6256)throw std::runtime_error("Army original VS constant profile");
        r.vertices.push_back(std::move(vertex));
        auto controls=load(directory/relative/"skin-controls.bin");
        if(controls.size()!=12)throw std::runtime_error("Army source matrix control size");
        std::array<uint32_t,3> words;std::memcpy(words.data(),controls.data(),12);r.skinControls.push_back(words);}
    if(r.pixels.size()!=r.templateCount)throw std::runtime_error("Army current template count differs");return r;
}
struct NativeArmyDraw {
    unsigned primitive{},record{};uint64_t distanceKey{},tableOwner{};
    std::vector<char> vertex;std::array<char,80> pixel{};
};
struct NativeArmyPlan {std::vector<unsigned> visibleActors,ordinaryCounts;std::vector<NativeArmyDraw> draws;};
struct NativeArmyPopulation {
    NativeArmySource source;NativeModelInputs templates,currentModels;
    std::vector<NativeAccessoryPoseOwner> poses;
    std::vector<NativeArmyPoseCache> poseCaches;
    std::vector<accessory_glyph::Primary> generatedPrimaries;
};
inline NativeArmyPopulation prepareNativeArmyPopulation(ID3D11Device* device,ID3D11DeviceContext* context,const fs::path& directory,bool populateImportedActors=true){
    NativeArmyPopulation r;r.source=prepareNativeArmySource(directory);r.templates=loadNativeModels(device,directory);
    if(r.templates.draws.size()!=r.source.templateCount)throw std::runtime_error("Army native complete template count");
    // Complete the exact source TextPane jobs once. Only completed resources
    // enter this owned group registry, whose real IDs drive CPU8/20/30 lookup.
    std::map<uint32_t,unsigned> primaries;std::map<uint32_t,bool> completedGroups;
    std::vector<std::array<uint32_t,4>> materialRows;std::ifstream list(directory/"models.txt");std::string relative;
    accessory_glyph::StockProducer producer(device,directory/"glyph");
    unsigned index=0;while(list>>relative){auto path=directory/relative;
        uint32_t regular,target,alternate,preserve;std::ifstream material(path/"material-ready-source.txt");if(material>>regular>>target>>alternate>>preserve){materialRows.push_back({regular,target,alternate,preserve});completedGroups[regular]=false;}
        unsigned texture,sampler,proxy;std::string job;std::ifstream runtime(path/"runtime-textures.txt");
        while(runtime>>texture>>sampler>>proxy>>job){if(texture>=16||sampler>=16)throw std::runtime_error("Army source runtime register");
            if(!primaries.count(proxy)){auto jobDir=fs::weakly_canonical(path/job);auto raw=accessory_glyph::read(jobDir/"glyph-job.bin");uint32_t code;std::memcpy(&code,raw.data()+12,4);
                accessory_glyph::Atlas atlas;if(code)atlas=accessory_glyph::Atlas::create(device,directory/"glyph",code);
                auto primary=producer.execute(device,context,directory/"glyph",fs::relative(jobDir,directory/"glyph"),code?&atlas:nullptr);
                if(primary.proxy!=proxy||!primary.producerCompleted)throw std::runtime_error("Army original glyph producer incomplete");
                primaries[proxy]=unsigned(r.generatedPrimaries.size());r.generatedPrimaries.push_back(std::move(primary));}
            const auto& primary=r.generatedPrimaries[primaries[proxy]];r.templates.draws[index].textures[texture]=primary.srv;r.templates.draws[index].samplers[sampler]=primary.sourceSampler;
        }if(material)completedGroups[regular]=true;++index;
    }
    for(const auto& row:materialRows){if(!completedGroups[row[0]])throw std::runtime_error("Army complete regular group not ready");
        if(completedGroups.count(row[1])||completedGroups.count(row[2]))throw std::runtime_error("Army actual CPU20/30 registered group branch differs; select its actual fields");}
    if(!populateImportedActors)return r;
    r.poses.reserve(r.source.actors.size());for(const auto& a:r.source.actors)r.poses.push_back(createNativeAccessoryPoseOwner(r.source.poses[a.category]));
    r.poseCaches.resize(r.poses.size());
    std::array<char,80> pixel{};r.currentModels.blend=r.templates.blend;
    for(const auto& p:r.source.primitives){auto d=r.templates.draws[p.sourceTemplate];d.identity=r.source.identities[p.actor]+"/draw"+std::to_string(p.draw);
        d.retainedVs=std::make_shared<std::vector<char>>(r.source.vertices[p.sourceTemplate]);
        d.vsConstants=modelContinuousConstantBuffer(device,d.retainedVs->data(),unsigned(d.retainedVs->size()));
        d.psConstants=modelContinuousConstantBuffer(device,pixel.data(),80);r.currentModels.draws.push_back(std::move(d));}
    return r;
}
// Source owner supplies current actor World/root on normal logical changes.
// Same model/category inventory reuses every imported/device source object.
inline void publishNativeArmyWorld(NativeArmyPopulation& owner,unsigned actor,const NativeAccessoryPoseRoot& world,const std::array<float,20>& actualBounds){
    owner.source.actors.at(actor).world=world;owner.source.actors.at(actor).obb=actualBounds;
}
template<class AcceptedCamera>
inline NativeArmyPlan selectNativeArmyPopulation(NativeArmyPopulation& owner,const AcceptedCamera& camera,const std::vector<float>& actualReturnedPhases,
    const std::vector<bool>* actualSamples=nullptr,const std::vector<const NativeAccessoryPoseSource*>* actualActions=nullptr,
    const std::vector<bool>* actualMainMembership=nullptr){
    auto& s=owner.source;if(actualReturnedPhases.size()!=s.actors.size())throw std::runtime_error("Army current phase per actor is absent");
    NativeArmyPlan r;r.ordinaryCounts.resize(s.recordMasks.size());std::vector<bool> visible(s.actors.size());
    std::vector<uint64_t> keys(s.actors.size());std::array<char,0x3C0> prefix{};std::memcpy(prefix.data(),camera.prefix.data(),prefix.size());
    std::array<std::array<float,4>,6> planes{};for(unsigned i=0;i<6;++i){const auto& p=camera.planes[i].equation;planes[i]={p.X,p.Y,p.Z,p.W};}
    float nearValue,farValue;std::memcpy(&nearValue,prefix.data()+0x140,4);std::memcpy(&farValue,prefix.data()+0x144,4);
    float span=modelContinuousAdd(modelContinuousSub(farValue,nearValue),1.f),scale=float(18446744073709551616.0/double(modelContinuousAdd(span,span)));
    const unsigned viewIndices[]{0,4,8,12,1,5,9,13,2,6,10,14};std::array<char,48> view{};
    for(unsigned i=0;i<12;++i)std::memcpy(view.data()+i*4,prefix.data()+0x80+viewIndices[i]*4,4);
    for(const auto& a:s.actors){if(actualMainMembership&&!(*actualMainMembership)[a.index])continue;
        auto& pose=owner.poses[a.index];const auto& source=actualActions?*(*actualActions)[a.index]:s.poses[a.category];
        prepareNativeArmyCurrentPose(source,pose,owner.poseCaches[a.index],
        actualReturnedPhases[a.index],a.world,!actualSamples||(*actualSamples)[a.index],a.category!=0&&a.category!=1);
        NativeModelContinuousInstance bounds;bounds.index=a.index;bounds.mode=a.mode;bounds.obb=a.obb;bounds.forceDistance=a.forceDistance;bounds.distanceBias=a.distanceBias;
        visible[a.index]=a.slotScalar>0&&modelContinuousVisible(bounds,planes);if(visible[a.index]){r.visibleActors.push_back(a.index);keys[a.index]=modelContinuousDistanceKey(bounds,planes[4].data(),span,scale);}}
    for(unsigned record=0;record<s.recordMasks.size();++record)for(unsigned i=0;i<s.primitives.size();++i){const auto& p=s.primitives[i];const auto& a=s.actors[p.actor];
        if(!visible[p.actor]||(s.recordMasks[record]&(a.exclusion|s.commonExclude))!=0)continue;
        ++r.ordinaryCounts[record];NativeArmyDraw d;d.primitive=i;d.record=record;d.distanceKey=keys[p.actor];d.tableOwner=p.tableOwner;d.pixel=s.pixels[p.sourceTemplate];
        auto& held=*owner.currentModels.draws[i].retainedVs;d.vertex=held;
        const auto& palette=owner.poses[p.actor].frame.palettes[p.palette];
        if(d.vertex.size()==160){
            const auto packed=packArmyRigid160(std::span<const char,160>(held.data(),160),
                std::span<const char,64>(prefix.data()+0x100,64),palette);
            std::copy(packed.begin(),packed.end(),d.vertex.begin());
        }else{
            std::memcpy(d.vertex.data(),prefix.data()+0x100,64);
            std::memcpy(d.vertex.data()+64,palette.rows.data(),palette.rows.size()*16);
            const auto& controls=s.skinControls[p.sourceTemplate];
            for(unsigned control=0;control<3;++control)
                std::memcpy(d.vertex.data()+6208+control*16,&controls[control],4);
        }
        held=d.vertex;
        std::memcpy(d.pixel.data()+16,view.data(),48);r.draws.push_back(std::move(d));}
    for(unsigned i=0;i<r.ordinaryCounts.size();++i)if(r.ordinaryCounts[i]&&r.ordinaryCounts[i]>=s.capacities[i])throw std::runtime_error("Army actual ordinary counter reaches source JGE capacity");
    // Each source-selected49/50/54/55 bank retains its independent record and
    // publication order. Current B90 lacks LightMap, so it enters54 after the
    // original49/50 gate skipped it; its independent count is unchanged.
    std::stable_sort(r.draws.begin(),r.draws.end(),[&](const auto& a,const auto& b){const auto& pa=s.primitives[a.primitive];const auto& pb=s.primitives[b.primitive];
        if(pa.queue!=pb.queue)return pa.queue<pb.queue;if(a.record!=b.record)return a.record<b.record;
        if(a.tableOwner!=b.tableOwner)return a.tableOwner<b.tableOwner;return a.distanceKey<b.distanceKey;});
    return r;
}
struct NativeArmyQueues {NativeModelInputs ordinary49,alpha50,ordinary54,alpha55;};
inline NativeArmyQueues updateSelectNativeArmyPopulation(ID3D11DeviceContext* context,const NativeArmyPopulation& owner,const NativeArmyPlan& plan){
    NativeArmyQueues r;r.ordinary49.blend=r.alpha50.blend=r.ordinary54.blend=r.alpha55.blend=owner.currentModels.blend;
    for(const auto& row:plan.draws){auto d=owner.currentModels.draws[row.primitive];context->UpdateSubresource(d.vsConstants.Get(),0,nullptr,row.vertex.data(),0,0);
        context->UpdateSubresource(d.psConstants.Get(),0,nullptr,row.pixel.data(),0,0);const auto& p=owner.source.primitives[row.primitive];
        auto* bank=p.queue==49?&r.ordinary49:p.queue==50?&r.alpha50:p.queue==54?&r.ordinary54:&r.alpha55;bank->draws.push_back(std::move(d));}return r;
}
