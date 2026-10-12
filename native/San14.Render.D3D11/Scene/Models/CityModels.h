// One source-created current CUnitCity primary. Include after model-native.h
// and model-continuous-native.h. Device objects/imports are prepared once;
// current accepted Camera supplies original prefix/planes and complete CBs.
#pragma once

#include "CityModelAdmission.h"
struct NativeCityModelSource {
    NativeModelContinuous catalog;
    std::vector<unsigned> queues;
    NativeCityModelAdmission admission;
    unsigned ordinaryRecordCount{},ordinaryRecordIndex{},ordinaryRecordMask{};
};
struct NativeCityModels {
    NativeCityModelSource source;
    NativeModelInputs allModels;
};
struct NativeCityModelPlan {
    NativeModelContinuousPlan models;
    NativeCityModelMasks masks;
    std::vector<unsigned> ordinaryCounts;
};
struct NativeCityModelQueues {
    NativeModelInputs ordinary54,alpha55;
};

inline NativeCityModelSource prepareNativeCityModelSource(const fs::path& directory) {
    NativeCityModelSource r;r.catalog=prepareNativeModelContinuous(directory);
    std::ifstream queues(directory/"queues.txt");unsigned queue;
    while(queues>>queue){if(queue!=54&&queue!=55)throw std::runtime_error("City source queue is unclosed");r.queues.push_back(queue);}
    if(r.queues.size()!=r.catalog.primitives.size())throw std::runtime_error("City primitive/queue source is incomplete");
    auto& a=r.admission;std::ifstream profile(directory/"admission.txt");
    profile>>a.registered3330>>a.successful3340>>a.callerFirst>>a.callerSecond>>a.callerShadow
        >>a.world328>>a.world32C>>a.world330>>a.eligibility>>a.exclusion78>>a.caster10C>>a.opacityB0
        >>a.common240Present>>a.commonFlags8;
    if(!profile)throw std::runtime_error("City actual TGK21/World/Common admission source is absent");
    std::ifstream counter(directory/"ordinary-counter-profile.txt");
    counter>>r.ordinaryRecordCount>>r.ordinaryRecordIndex>>r.ordinaryRecordMask;
    if(!counter||r.ordinaryRecordCount>32||r.ordinaryRecordIndex>=r.ordinaryRecordCount
        ||(r.ordinaryRecordMask&r.admission.exclusion78)!=0)
        throw std::runtime_error("City actual ordinary kind1 record identity is incomplete");
    return r;
}
inline NativeCityModels prepareNativeCityModels(ID3D11Device* device,const fs::path& directory) {
    NativeCityModels r;r.source=prepareNativeCityModelSource(directory);
    r.allModels=prepareNativeModelContinuousGpu(device,r.source.catalog,directory);
    for(std::size_t i=0;i<r.source.catalog.primitives.size();++i)
        r.allModels.draws.at(i).identity="city:7/primary/draw-"+std::to_string(r.source.catalog.primitives[i].drawIndex);
    return r;
}
template<class AcceptedSourceCameraFrame>
inline NativeCityModelPlan selectNativeCityModels(const NativeCityModelSource& source,const AcceptedSourceCameraFrame& frame) {
    NativeCityModelPlan r;r.masks=nativeCityModelCurrentMasks(source.admission);
    if(r.masks.firstSlot)r.models=selectNativeModelContinuousFromCameraFrame(source.catalog,frame);
    r.ordinaryCounts.resize(source.ordinaryRecordCount);
    r.ordinaryCounts[source.ordinaryRecordIndex]=unsigned(r.models.draws.size());
    return r;
}
template<class AcceptedSourceCameraFrame>
inline NativeCityModelPlan selectNativeCityModels(const NativeCityModels& owner,const AcceptedSourceCameraFrame& frame) {
    return selectNativeCityModels(owner.source,frame);
}
inline NativeCityModelQueues updateSelectNativeCityModels(ID3D11DeviceContext* context,
    const NativeCityModels& owner,const NativeCityModelPlan& plan) {
    auto selected=updateSelectNativeModelContinuous(context,plan.models,owner.allModels);
    NativeCityModelQueues r;r.ordinary54.blend=selected.blend;r.alpha55.blend=selected.blend;
    for(std::size_t i=0;i<selected.draws.size();++i) {
        const auto queue=owner.source.queues.at(plan.models.draws[i].primitiveIndex);
        (queue==54?r.ordinary54:r.alpha55).draws.push_back(std::move(selected.draws[i]));
    }
    return r; // Upload -> draw54/draw55 now, before another Camera CB upload.
}

// Merge the two already source-sorted ordinary54 subsets. Field14's stable
// ascending pass followed by field3 is equivalent to (tableOwner,distanceKey).
// Exact ties retain the explicit owned publication order: Field then City.
// The input Field plan must describe exactly its current selected draw list;
// a late-filtered subset supplies its matching plan rather than losing keys.
inline NativeCityModelQueues mergeNativeCityModelsWithField54(
    const NativeModelContinuousPlan& fieldPlan,const NativeModelInputs& fieldModels,
    const NativeCityModelSource& citySource,const NativeCityModelPlan& cityPlan,
    const NativeCityModelQueues& cityModels) {
    if(fieldPlan.draws.size()!=fieldModels.draws.size())throw std::runtime_error("Field54 merge needs matching selected source keys");
    std::vector<const NativeModelContinuousDraw*> cityKeys;
    for(const auto& row:cityPlan.models.draws)
        if(citySource.queues.at(row.primitiveIndex)==54)cityKeys.push_back(&row);
    if(cityKeys.size()!=cityModels.ordinary54.draws.size())throw std::runtime_error("City54 merge needs matching selected source keys");
    NativeCityModelQueues r;r.alpha55=cityModels.alpha55;
    r.ordinary54.blend=fieldModels.blend?fieldModels.blend:cityModels.ordinary54.blend;
    r.ordinary54.draws.reserve(fieldPlan.draws.size()+cityKeys.size());
    std::size_t field=0,city=0;
    while(field<fieldPlan.draws.size()||city<cityKeys.size()) {
        bool chooseField=city==cityKeys.size();
        if(!chooseField&&field<fieldPlan.draws.size()) {
            const auto& a=fieldPlan.draws[field];const auto& b=*cityKeys[city];
            chooseField=a.tableOwner<b.tableOwner||(a.tableOwner==b.tableOwner&&a.distanceKey<=b.distanceKey);
        }
        r.ordinary54.draws.push_back(chooseField?fieldModels.draws[field++]:cityModels.ordinary54.draws[city++]);
    }
    return r;
}

// The same 19 original geometry owners are accessible through allModels for
// the existing shadow consumer. Read shadow-models.json/source admission and
// run actual OwnCamera/light/class gates; ordinary visibility is not cast.
// The merge above covers these finite Field/City source-owned subsets, not
// the original fork's unobserved thread interleaving or complete TGK21 roster.
// Current counter/target/stencil publication and logical exit states remain
// with the caller; fixed draw-time states use the source-exported profile.
