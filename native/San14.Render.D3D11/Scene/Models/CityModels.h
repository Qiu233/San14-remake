// One source-created current CUnitCity primary. Include after model-native.h
// and model-continuous-native.h. Device objects/imports are prepared once;
// current accepted Camera supplies original prefix/planes and complete CBs.
#pragma once
#include "../../Runtime/PreparedFileInput.h"

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
    NativePreparedInputFile queues(directory/"queues.txt");unsigned queue;
    while(queues>>queue){if(queue!=54&&queue!=55)throw std::runtime_error("City source queue is unclosed");r.queues.push_back(queue);}
    if(r.queues.size()!=r.catalog.primitives.size())throw std::runtime_error("City primitive/queue source is incomplete");
    auto& a=r.admission;NativePreparedInputFile profile(directory/"admission.txt");
    profile>>a.registered3330>>a.successful3340>>a.callerFirst>>a.callerSecond>>a.callerShadow
        >>a.world328>>a.world32C>>a.world330>>a.eligibility>>a.exclusion78>>a.caster10C>>a.opacityB0
        >>a.common240Present>>a.commonFlags8;
    if(!profile)throw std::runtime_error("City actual TGK21/World/Common admission source is absent");
    NativePreparedInputFile counter(directory/"ordinary-counter-profile.txt");
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
    if(fieldPlan.draws.size()!=fieldModels.draws.size())throw std::runtime_error("Field54/55 merge needs matching selected source keys");
    struct Row {const NativeModelContinuousDraw* key;const NativeModelDraw* draw;};
    std::vector<Row> fieldOpaque,cityOpaqueRows,cityAlphaRows,alpha;
    for(size_t i=0;i<fieldPlan.draws.size();++i) {
        const auto& key=fieldPlan.draws[i];
        if(key.queue!=54&&key.queue!=55)throw std::runtime_error("Field normal queue is outside54/55");
        (key.queue==54?fieldOpaque:alpha).push_back({&key,&fieldModels.draws[i]});
    }
    const bool fieldHasAlpha=!alpha.empty();
    size_t cityOpaque=0,cityAlpha=0;
    for(const auto& key:cityPlan.models.draws) {
        const auto queue=citySource.queues.at(key.primitiveIndex);
        if(queue==54)cityOpaqueRows.push_back({&key,&cityModels.ordinary54.draws.at(cityOpaque++)});
        else if(queue==55)cityAlphaRows.push_back({&key,&cityModels.alpha55.draws.at(cityAlpha++)});
        else throw std::runtime_error("City normal queue is outside54/55");
    }
    if(cityOpaque!=cityModels.ordinary54.draws.size()||cityAlpha!=cityModels.alpha55.draws.size())
        throw std::runtime_error("City54/55 merge needs matching selected source keys");
    // Actual ascending stable descriptor passes. Publication ties retain Field
    // before City, as in the former ordinary54 merge. Alpha uses its own list.
    auto order=[](std::vector<Row>& rows,const std::vector<unsigned>& fields) {
        for(auto field:fields) {
            if(field!=14&&field!=3)throw std::runtime_error("Normal model source sort field is unclosed");
            std::stable_sort(rows.begin(),rows.end(),[field](const auto& a,const auto& b) {
                return (field==14?a.key->distanceKey:a.key->tableOwner)<(field==14?b.key->distanceKey:b.key->tableOwner);
            });
        }
    };
    std::vector<Row> opaque;opaque.reserve(fieldOpaque.size()+cityOpaqueRows.size());
    auto compare=[](const Row& a,const Row& b) {
        return a.key->tableOwner<b.key->tableOwner||(a.key->tableOwner==b.key->tableOwner&&a.key->distanceKey<b.key->distanceKey);
    };
    // Both opaque subsets are already source-sorted; preserve the original
    // linear merge instead of sorting every visible rigid model again.
    size_t f=0,c=0;
    while(f<fieldOpaque.size()||c<cityOpaqueRows.size()) {
        const bool useField=c==cityOpaqueRows.size()||(f<fieldOpaque.size()&&!compare(cityOpaqueRows[c],fieldOpaque[f]));
        opaque.push_back(useField?fieldOpaque[f++]:cityOpaqueRows[c++]);
    }
    if(fieldHasAlpha) {
        // City's existing model plan is opaque-sorted. Recover its actual
        // publication order before the distinct Alpha descriptor so equal
        // distance keys are not accidentally broken by a material table.
        std::sort(cityAlphaRows.begin(),cityAlphaRows.end(),[](const auto& a,const auto& b){return a.key->primitiveIndex<b.key->primitiveIndex;});
    }
    alpha.insert(alpha.end(),cityAlphaRows.begin(),cityAlphaRows.end());
    if(fieldHasAlpha) {
        if(fieldPlan.alphaSortFields.empty())throw std::runtime_error("Field55 actual Alpha descriptor absent");
        order(alpha,fieldPlan.alphaSortFields);
    }
    NativeCityModelQueues r;
    r.ordinary54.blend=fieldModels.blend?fieldModels.blend:cityModels.ordinary54.blend;
    r.alpha55.blend=fieldModels.blend?fieldModels.blend:cityModels.alpha55.blend;
    for(const auto& row:opaque)r.ordinary54.draws.push_back(*row.draw);
    for(const auto& row:alpha)r.alpha55.draws.push_back(*row.draw);
    return r;
}

// The same 19 original geometry owners are accessible through allModels for
// the existing shadow consumer. Read shadow-models.json/source admission and
// run actual OwnCamera/light/class gates; ordinary visibility is not cast.
// The merge above covers these finite Field/City source-owned subsets, not
// the original fork's unobserved thread interleaving or complete TGK21 roster.
// Current counter/target/stencil publication and logical exit states remain
// with the caller; fixed draw-time states use the source-exported profile.
