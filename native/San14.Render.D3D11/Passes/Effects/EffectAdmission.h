#pragma once
#include "../../Runtime/PreparedFileInput.h"
#include "EffectCameraFrame.h"
#include "../../Scene/Models/FieldDisplayFrame.h"
#include <map>

namespace source_field_effect {
struct World {uint32_t sourceIndex{},objectId{},group{},type{},priorD8{},flags68{};};
struct CandidateIdentity {uint32_t ordinal{},sourceIndex{},objectId{},leaf{},particle{};};
struct Profile {std::vector<World> worlds;std::vector<CandidateIdentity> candidates;};
inline Profile loadProfile(const std::filesystem::path& path){
    NativePreparedInputFile input(path,std::ios::binary);using source_camera::read;
    if(!input||read<std::array<char,8>>(input)!=std::array<char,8>{'F','X','R','E','G','0','0','1'})
        throw std::runtime_error("Field Effect identity binding source");
    unsigned worlds=read<unsigned>(input),candidates=read<unsigned>(input);Profile p;p.worlds.resize(worlds);p.candidates.resize(candidates);
    for(auto& world:p.worlds)world=read<World>(input);
    for(auto& candidate:p.candidates)candidate=read<CandidateIdentity>(input);
    if(input.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Field Effect identity binding trailing bytes");return p;
}
struct Snapshot {uint32_t sceneTypeMask{};std::vector<uint32_t> worldD8;};
struct WorldSceneView {std::vector<uint32_t> masks;};
class Registry {
    std::vector<source_far_main::RegistryEntry> entries;
    std::vector<uint32_t> ownedD8,candidateWorld;
public:
    // World identity joins are import-time inputs. Candidate.ordinal is checked
    // along with its explicit World/leaf/particle tuple; it never selects a World.
    Registry(const source_far_main::Profile& farProfile,const source_effect_camera::Profile& effects,const Profile& imported){
        std::map<uint32_t,source_far_main::RegistryEntry> registered;
        for(const auto& entry:farProfile.registry)if(!registered.emplace(entry.sourceIndex,entry).second)
            throw std::runtime_error("Field source registry repeats a source identity");
        std::map<uint32_t,unsigned> worlds;entries.reserve(imported.worlds.size());
        for(const auto& world:imported.worlds){auto entry=registered.find(world.sourceIndex);
            if(entry==registered.end()||entry->second.type!=0x100||entry->second.objectId!=world.objectId||
                entry->second.group!=world.group||world.type!=0x100||world.group>=7||
                !worlds.emplace(world.sourceIndex,unsigned(entries.size())).second)
                throw std::runtime_error("Held Effect World and actual Field source registry identity differ");
            entries.push_back(entry->second);ownedD8.push_back(world.priorD8);
        }
        if(imported.candidates.size()!=effects.candidates.size())throw std::runtime_error("Field Effect candidate identity count differs");
        candidateWorld.reserve(effects.candidates.size());
        for(size_t i=0;i<effects.candidates.size();++i){const auto& actual=effects.candidates[i];const auto& identity=imported.candidates[i];
            auto world=worlds.find(actual.world);
            if(world==worlds.end()||identity.ordinal!=actual.ordinal||identity.sourceIndex!=actual.world||identity.leaf!=actual.leaf||
                identity.particle!=actual.particleOrdinal||identity.objectId!=entries[world->second].objectId)
                throw std::runtime_error("Actual Effect candidate tuple differs from its bound World identity");
            candidateWorld.push_back(world->second);
        }
    }
    // Captured once after the actual Group writes. All World scene views use
    // this same pre-v30 D8 snapshot even after the owner's normal entry reset.
    Snapshot capture(const source_far_main::Current& frame){
        Snapshot out;out.sceneTypeMask=frame.sceneTypeMask;out.worldD8.resize(entries.size());
        for(size_t i=0;i<entries.size();++i){ownedD8[i]=source_far_main::captureCurrentEffectMask(entries[i],frame,ownedD8[i]);out.worldD8[i]=ownedD8[i];}
        return out;
    }
    // E1AA00 reads v408/pre-resetD8 and immediately stores FFFFFFFF before
    // testing its intersection with param7. Scene-type rejection never calls
    // that World callback. Eligibility and the caller's second mask stay actual
    // view inputs, separate from registration and candidate family/category.
    bool admitsMask(const Snapshot& snapshot,unsigned candidate,uint32_t effectiveCallerSecondMask)const {
        unsigned world=candidateWorld.at(candidate);
        return source_mode2_octree::admitsEntry(entries[world].type,snapshot.sceneTypeMask)&&
            (snapshot.worldD8.at(world)&effectiveCallerSecondMask)!=0;
    }
    // The imported frozen cohort has no captured Common3338/outer-second/
    // successful3340 admission. Preserve its existing producer scope while
    // adding only the reached Field SceneType and Group whole-D8 gates. This
    // is not a full original caller mask or v408 readiness assertion.
    bool preserveImportedAdmission(const Snapshot& snapshot,unsigned candidate)const {
        unsigned world=candidateWorld.at(candidate);
        return source_mode2_octree::admitsEntry(entries[world].type,snapshot.sceneTypeMask)&&snapshot.worldD8.at(world)!=0;
    }
    bool admits(const Snapshot& snapshot,unsigned candidate,uint32_t effectiveCallerSecondMask,bool actualEligibilityV408)const {
        return actualEligibilityV408&&admitsMask(snapshot,candidate,effectiveCallerSecondMask);
    }
    // D68E40 ORs the outer second caller with Common3338. D67290 intersects
    // that effective mask, the captured pre-reset D8 and successful3340 per View.
    // Feed every new WorldSceneView this same captured request snapshot.
    template<class ActualEligibility>
    WorldSceneView selectWorldSceneView(const Snapshot& snapshot,uint32_t outerSecondCallerMask,uint32_t common3338,
            uint32_t successful3340,ActualEligibility actualEligibility)const {
        WorldSceneView out;out.masks.resize(entries.size());uint32_t caller=outerSecondCallerMask|common3338;
        for(unsigned world=0;world<entries.size();++world)if(source_mode2_octree::admitsEntry(entries[world].type,snapshot.sceneTypeMask)&&actualEligibility(world,entries[world]))
            out.masks[world]=snapshot.worldD8.at(world)&caller&successful3340&0xFFFFu;
        return out;
    }
    bool admits(const WorldSceneView& view,unsigned candidate)const{return view.masks.at(candidateWorld.at(candidate))!=0;}
    const source_far_main::RegistryEntry& worldEntry(unsigned world)const{return entries.at(world);}
    // Invoke only for an actually reached E1AA00 entry, including its rejected
    // v408/mask branch. A type-closed or otherwise unreached callback is not a
    // restore event. This does not mutate any retained WorldSceneView snapshot.
    void consumeNormalWorldEntry(unsigned world){ownedD8.at(world)=0xFFFFFFFFu;}
    template<class ActualCallbackReached>
    void consumeNormalWorldEntries(const Snapshot& snapshot,ActualCallbackReached actualReached){
        for(unsigned world=0;world<entries.size();++world)if(source_mode2_octree::admitsEntry(entries[world].type,snapshot.sceneTypeMask)&&actualReached(world,entries[world]))
            consumeNormalWorldEntry(world);
    }
    size_t worldCount()const{return entries.size();}
};
} // namespace source_field_effect
