#pragma once
#include "ArmyWorldPopulation.h"
#include "ArmyAnimation.h"
#include <algorithm>
#include <utility>
#include <span>
#ifdef SAN14_SHARED_POOL_PROOF
#include <iostream>
#endif

enum class GraphicsModelFamily {Army,AreaCapital,City,Gate};
struct GraphicsModelBindingKey {
    GraphicsModelFamily family{};
    ArmyWorldActorKey actor{};
    auto tuple() const {return std::tie(family,actor);}
    bool operator<(const GraphicsModelBindingKey& b) const {return tuple()<b.tuple();}
    bool operator==(const GraphicsModelBindingKey& b) const {return tuple()==b.tuple();}
};

// All logical families share the once-loaded physical banks. Source
// free-list insertion and acquisition both use the tail: LIFO.
// A released slot keeps its World, local pose, H clock and private GPU models.
template<class Population>
class ArmyWorldModelPool {
public:
    using Model=typename decltype(std::declval<Population&>().currentModels.draws)::value_type;
    struct Ref {unsigned category{},index{};};
    struct Slot {
        unsigned category{},index{};
        ArmyWorldPresentation presented;
        NativeAccessoryPoseOwner pose;
        NativeArmyPoseCache cache;
        NativeArmyAnimationClock clock;
        // A physical draw owns one model/CB. The caller rebinds a different
        // family's completed material on that model when the slot is reused.
        std::map<unsigned,Model> models;
    };
    std::map<GraphicsModelBindingKey,Ref> bindings;
    // Existing Army consumers observe only Army binding changes.
    uint64_t bindingRevision{};

    // These source arrays belong to the retained population/library and outlive
    // the pool. Only the small slot state is materialized on first acquisition.
    ArmyWorldModelPool(std::span<const unsigned> capacities,
            std::span<const ArmyWorldPresentation> constructors,
            std::span<const NativeAccessoryPoseSource> poseDefaults,
            const NativeArmyDefaultActions& defaults)
        {
        slots_.resize(capacities.size());free_.resize(capacities.size());
        resources_.resize(capacities.size());
        for(unsigned category=0;category<capacities.size();++category){
            resources_[category]=CategoryResources{&constructors[category],&poseDefaults[category],&defaults.rows[category]};
            slots_[category].resize(capacities[category]);
            free_[category].reserve(capacities[category]);
            for(unsigned index=0;index<capacities[category];++index)free_[category].push_back(index);
        }
    }

    // An attachment-only renderer installs the one actual resource category;
    // it does not manufacture unused Army actors or poses for other banks.
    ArmyWorldModelPool(unsigned category,unsigned capacity,const ArmyWorldPresentation& constructor,
        const NativeAccessoryPoseSource& pose,const NativeArmyDefaultAction& action) {
        slots_.resize(category+1);free_.resize(category+1);resources_.resize(category+1);
        resources_[category]=CategoryResources{&constructor,&pose,&action};
        slots_[category].resize(capacity);free_[category].reserve(capacity);
        for(unsigned index=0;index<capacity;++index)free_[category].push_back(index);
    }

    bool acquire(const ArmyWorldActorKey& key){return acquire({GraphicsModelFamily::Army,key});}

    bool acquire(const GraphicsModelBindingKey& key){
        if(bindings.contains(key))return false;
        const unsigned category=unsigned(key.actor.category);
        if(category>=resources_.size()||!resources_[category])
            throw std::runtime_error("Shared model category has no installed resources");
        auto& slots=slots_.at(category);auto& free=free_[category];
        if(free.empty())return false;
        const unsigned index=free.back();
        const auto& resource=*resources_[category];
        if(!slots[index])slots[index].emplace(Slot{category,index,*resource.constructor,
            createNativeAccessoryPoseOwner(*resource.pose),{},
            createNativeArmyDefaultAnimationClock(*resource.action),{}});
        free.pop_back();bindings.emplace(key,Ref{category,index});changed(key.family);
#ifdef SAN14_SHARED_POOL_PROOF
        logPhysical("bind",key,*slots[index]);
#endif
        return true;
    }

    // Detach a real manager view from the logical Army pointer without returning
    // any physical node. Retiring views use private render-owner keys; a new
    // logical Army view can then borrow its own independent physical nodes.
    void detachUnit(int32_t unit,int32_t viewOwner){
        auto it=bindings.lower_bound({GraphicsModelFamily::Army,{unit,0,0,0}});
        while(it!=bindings.end()&&it->first.family==GraphicsModelFamily::Army&&it->first.actor.unit==unit){
            auto node=bindings.extract(it++);node.key().actor.unit=viewOwner;
            if(!bindings.insert(std::move(node)).inserted)throw std::runtime_error("Army detached view binding collision");
            changed(GraphicsModelFamily::Army);
        }
    }

    void releaseUnit(int32_t unit){releaseUnit(GraphicsModelFamily::Army,unit);}
    void releaseUnit(const GraphicsModelBindingKey& key){releaseUnit(key.family,key.actor.unit);}

    // A mode setter clears this descriptor immediately, even while the logical
    // unit remains alive. Release its actual members in head/ordinal order.
    void releaseDescriptor(GraphicsModelFamily family,int32_t unit,int32_t descriptor){
        for(auto it=bindings.begin();it!=bindings.end();){
            const auto& key=it->first;
            if(key.family!=family||key.actor.unit!=unit||key.actor.descriptor!=descriptor){++it;continue;}
#ifdef SAN14_SHARED_POOL_PROOF
            logPhysical("release",key,*slots_[it->second.category][it->second.index]);
#endif
            free_[it->second.category].push_back(it->second.index);
            it=bindings.erase(it);changed(family);
        }
    }

    void releaseUnit(GraphicsModelFamily family,int32_t unit){
        for(auto it=bindings.begin();it!=bindings.end();){
            if(it->first.family!=family||it->first.actor.unit!=unit){++it;continue;}
#ifdef SAN14_SHARED_POOL_PROOF
            logPhysical("release",it->first,*slots_[it->second.category][it->second.index]);
#endif
            free_[it->second.category].push_back(it->second.index);
            it=bindings.erase(it);changed(family);
        }
    }

    void releaseAbsent(const std::map<int32_t,std::array<float,3>>& unitCenters){
        for(auto it=bindings.begin();it!=bindings.end();){
            if(it->first.family!=GraphicsModelFamily::Army||unitCenters.contains(it->first.actor.unit)){++it;continue;}
#ifdef SAN14_SHARED_POOL_PROOF
            logPhysical("release",it->first,*slots_[it->second.category][it->second.index]);
#endif
            free_[it->second.category].push_back(it->second.index);
            it=bindings.erase(it);changed(GraphicsModelFamily::Army);
        }
    }

    Slot* find(const ArmyWorldActorKey& key){return find({GraphicsModelFamily::Army,key});}

    // Descriptor membership survives a new category request. The source
    // reconciler compares list counts, not the node's previous resource kind.
    std::optional<ArmyWorldActorKey> member(GraphicsModelFamily family,int32_t unit,int32_t descriptor,int32_t ordinal) const {
        auto first=bindings.lower_bound({family,{unit,0,0,0}});
        for(auto it=first;it!=bindings.end()&&it->first.family==family&&it->first.actor.unit==unit;++it)
            if(it->first.actor.descriptor==descriptor&&it->first.actor.instance==ordinal)return it->first.actor;
        return std::nullopt;
    }

    Slot* find(const GraphicsModelBindingKey& key){
        const auto found=bindings.find(key);
        if(found==bindings.end())return nullptr;
        return &*slots_[found->second.category][found->second.index];
    }

    size_t boundCount(GraphicsModelFamily family) const {
        return std::count_if(bindings.begin(),bindings.end(),[&](const auto& row){return row.first.family==family;});
    }

    uint64_t familyBindingRevision(GraphicsModelFamily family) const {
        return familyRevisions_[unsigned(family)];
    }

    void snapshot(Population& population,ArmyWorldPopulationState& state){
        for(const auto& [key,index]:state.actorIndices)if(auto* slot=find(key)){
            const auto& actor=population.source.actors.at(index);
            slot->presented={actor.world,actor.obb};
            auto& cache=population.poseCaches.at(index);
            if(cache.pendingPoolSnapshot){
                // Reuse slot vector capacities; a camera cache hit makes no copy.
                // Keep the source pending until the existing assignment succeeds.
                slot->pose=population.poses.at(index);
                cache.pendingPoolSnapshot=false;slot->cache=cache;
            }
            // H clock belongs to the actual slot throughout the binding.
        }
    }

private:
#ifdef SAN14_SHARED_POOL_PROOF
    static void logPhysical(const char* operation,const GraphicsModelBindingKey& key,const Slot& slot){
        if(key.actor.category!=2)return;
        std::cout<<"SharedPool "<<operation<<" family="<<unsigned(key.family)<<" unit="<<key.actor.unit
            <<" ordinal="<<key.actor.instance<<" slot="<<slot.index<<" action="<<slot.clock.selectedRawAction24
            <<" hframe="<<slot.clock.hFrame24<<" root="<<slot.presented.world.translation.x<<','
            <<slot.presented.world.translation.y<<','<<slot.presented.world.translation.z<<" cb=";
        for(unsigned draw=0;draw<3;++draw){const auto found=slot.models.find(draw);
            if(draw)std::cout<<',';
            if(found==slot.models.end())std::cout<<"0:0";
            else std::cout<<uintptr_t(found->second.vsConstants.Get())<<':'<<uintptr_t(found->second.psConstants.Get());
        }
        std::cout<<'\n';
    }
#endif
    void changed(GraphicsModelFamily family){
        ++familyRevisions_[unsigned(family)];
        if(family==GraphicsModelFamily::Army)++bindingRevision;
    }
    std::array<uint64_t,4> familyRevisions_{};
    std::vector<std::vector<std::optional<Slot>>> slots_;
    std::vector<std::vector<unsigned>> free_;
    struct CategoryResources {
        const ArmyWorldPresentation* constructor;
        const NativeAccessoryPoseSource* pose;
        const NativeArmyDefaultAction* action;
    };
    std::vector<std::optional<CategoryResources>> resources_;
};
