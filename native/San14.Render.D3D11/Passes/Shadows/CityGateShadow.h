// Current Field1111 + City/Gate61 bridge. Include after the existing model,
// city population, joint-population-shadow and shadow-camera-gpu headers.
// Import/device preparation stays once; this helper owns no GPU objects.
#pragma once

struct NativeCityGateShadowLight {
    unsigned mask{},f1C{},f1F{},f20{},wrapper0{};
};
struct NativeCityGateShadowAdmission {
    unsigned commonExclusion404{},selectedRecord1BD0{};
    std::vector<NativeCityGateShadowLight> lights;
};
struct NativeCityGateShadowSource {
    unsigned fieldOwners{},fieldPrimitives{},fieldTemplates{},primaryBase{},primaryTemplates{};
    std::array<NativeCityGateShadowAdmission,2> admission;
};

inline NativeCityGateShadowSource loadNativeCityGateShadowSource(const fs::path& directory,
        const NativeModelContinuous& field,const NativeCityPopulationSource& cities) {
    if(field.instances.empty()||cities.worlds.size()!=61||cities.catalog.instances.size()!=61)
        throw std::runtime_error("Current joint shadow requires complete Field and City/Gate61 sources");
    NativeCityGateShadowSource source;
    source.fieldOwners=unsigned(field.instances.size());source.fieldPrimitives=unsigned(field.primitives.size());
    source.fieldTemplates=field.templateCount;source.primaryTemplates=unsigned(cities.templates.size());
    for(const auto& instance:field.instances)source.primaryBase=std::max(source.primaryBase,instance.index+1);
    std::ifstream rows(directory/"admission.bin",std::ios::binary);
    if(primaryRead<unsigned>(rows)!=0x31414743u||primaryRead<unsigned>(rows)!=source.admission.size())
        throw std::runtime_error("Current City/Gate Shadow CGA1 import absent");
    for(auto& family:source.admission) {
        family.commonExclusion404=primaryRead<unsigned>(rows);family.selectedRecord1BD0=primaryRead<unsigned>(rows);
        family.lights.resize(primaryRead<unsigned>(rows));
        for(auto& light:family.lights){light.mask=primaryRead<unsigned>(rows);light.f1C=primaryRead<unsigned>(rows);
            light.f1F=primaryRead<unsigned>(rows);light.f20=primaryRead<unsigned>(rows);light.wrapper0=primaryRead<unsigned>(rows);}
    }
    return source;
}

inline unsigned nativeCityGateFirstCasterMask(const NativeCityGateShadowAdmission& source,
        const NativeCityModelAdmission& world) {
    const auto masks=nativeCityModelCurrentMasks(world);
    if(!masks.shadowSlot||!source.selectedRecord1BD0)return 0;
    for(const auto& light:source.lights)if((light.mask&(world.exclusion78|source.commonExclusion404))==0) {
        // Original C39F88..FC1, including the actual signed10C gate above.
        const bool bit0=(unsigned(world.caster10C)&1)!=0;
        return (light.f1C&1)&&((light.f1C&2)?!bit0:bit0)?15u:0u;
    }
    return 0;
}

inline void updateNativeCityGateJointShadowCatalog(NativeJointPopulationShadow& joint,
        const NativeCityGateShadowSource& source,const NativeModelContinuous& field,
        const NativeCityPopulationSource& cities) {
    if(field.instances.size()!=source.fieldOwners||field.primitives.size()!=source.fieldPrimitives||
       field.templateCount!=source.fieldTemplates||cities.catalog.instances.size()!=61||
       cities.templates.size()!=source.primaryTemplates)
        throw std::runtime_error("Current joint source identity changed; import its new complete bank");
    joint.catalog=field;
    joint.catalog.templateCount=source.fieldTemplates+source.primaryTemplates;
    joint.catalog.instances.reserve(source.fieldOwners+61);
    joint.catalog.primitives.reserve(source.fieldPrimitives+cities.catalog.primitives.size());
    for(unsigned i=0;i<cities.catalog.instances.size();++i) {
        auto instance=cities.catalog.instances[i];
        if(instance.index!=i)throw std::runtime_error("Current City/Gate packed owner identity differs");
        instance.index+=source.primaryBase;instance.firstPrimitive+=source.fieldPrimitives;
        joint.catalog.instances.push_back(instance);
    }
    for(const auto& current:cities.catalog.primitives) {
        auto primitive=current;primitive.instanceIndex+=source.primaryBase;primitive.templateIndex+=source.fieldTemplates;
        joint.catalog.primitives.push_back(primitive);
    }
}

inline void publishNativeCityGateJointShadowProfile(const NativeCityGateShadowSource& source,unsigned family,
        const NativeCityPopulationSource& cities,source_shadow_camera::Profile& profile) {
    if(family>=source.admission.size()||profile.worlds.size()<source.fieldOwners)
        throw std::runtime_error("Current joint source family/Field profile absent");
    profile.worlds.resize(source.fieldOwners+61);
    for(unsigned i=0;i<61;++i) {
        const auto& instance=cities.catalog.instances[i];const auto& world=cities.worlds[i];
        const auto masks=nativeCityModelCurrentMasks(world);
        source_shadow_camera::World current;
        current.index=source.primaryBase+i;
        current.allowedMask=cities.present[i]?nativeCityGateFirstCasterMask(source.admission[family],world):0;
        current.exclusion=world.exclusion78;current.flags=world.caster10C;current.opacity=world.opacityB0;
        current.primitiveCount=instance.primitiveCount;
        current.ordinaryFirstMask=cities.present[i]&&masks.firstSlot?masks.first&world.successful3340:0;
        profile.worlds[source.fieldOwners+i]=current;
    }
}

// Call after applying a CPU1 World publication, before any OwnCamera request.
// FieldDisplay may still update its own first1111 masks for the current camera.
// The currentSeason producer may update the existing numeric profile fields.
inline void publishNativeCityGateJointShadow(NativeJointPopulationShadow& joint,
        const NativeCityGateShadowSource& source,const NativeModelContinuous& field,
        const NativeCityPopulationSource& cities,
        std::array<source_shadow_camera::Profile,2>& camera,
        std::array<source_shadow_gpu::Profile,2>& gpu) {
    updateNativeCityGateJointShadowCatalog(joint,source,field,cities);
    for(unsigned family=0;family<camera.size();++family) {
        publishNativeCityGateJointShadowProfile(source,family,cities,camera[family]);
        source_shadow_gpu::bindCatalog(gpu[family],joint.catalog);
    }
}

// Geometry comes from the complete currentModels packed order, never an
// ordinary54/55 visibility plan. Each shadow CB remains owned by the existing
// NativeShadowCameraGpu; ordinary mutable CBs are not overwritten here.
inline void connectNativeCityGateJointShadowGeometry(NativeJointPopulationShadow& joint,
        const NativeModelInputs& field,const NativeCityPopulation& cities) {
    connectNativeJointPopulationShadowGeometry(joint,field,cities.currentModels);
}
