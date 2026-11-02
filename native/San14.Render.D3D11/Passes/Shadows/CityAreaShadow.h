// Extend the existing two source families to the current City61+Area282 view.
// Include after area-primary-native.h and city-gate-shadow-native.h.
#pragma once
// This updates only current admission and World masks. The actual source
// publication owns the joint catalog and sparse GPU index binding.
inline void publishNativeCityAreaJointShadowAdmission(
        const NativeCityGateShadowSource& source,const NativeModelContinuous& field,
        const NativeCityAreaPrimary& primary,std::array<source_shadow_camera::Profile,2>& camera) {
    if(field.templateCount!=source.fieldTemplates||primary.source.templates.size()!=source.primaryTemplates)
        throw std::runtime_error("Current Field/primary Shadow template bank differs");
    unsigned primaryBase=0;for(const auto& row:field.instances)primaryBase=std::max(primaryBase,row.index+1);
    const auto fieldOwners=field.instances.size();
    for(unsigned family=0;family<camera.size();++family) {
        auto& profile=camera[family];profile.worlds.resize(fieldOwners+primary.source.worlds.size());
        for(unsigned i=0;i<primary.source.worlds.size();++i) {
            const auto& world=primary.source.worlds[i];const auto masks=nativeCityModelCurrentMasks(world,primary.source.caller);
            source_shadow_camera::World row;
            row.index=primaryBase+i;row.exclusion=world.exclusion78;row.flags=world.caster10C;
            row.opacity=world.opacityB0;row.primitiveCount=primary.source.catalog.instances[i].primitiveCount;
            row.allowedMask=primary.source.present[i]?nativeCityGateFirstCasterMask(source.admission[family],world,primary.source.caller):0;
            row.ordinaryFirstMask=primary.source.present[i]&&masks.firstSlot?masks.first&primary.source.caller.successfulViews:0;
            profile.worlds[fieldOwners+i]=row;
        }
    }
}
// Initial setup and each real primary publication still replace all current
// catalog payloads, then rebind both families before geometry can be drawn.
inline void publishNativeCityAreaJointShadow(NativeJointPopulationShadow& joint,
        const NativeCityGateShadowSource& source,const NativeModelContinuous& field,
        const NativeCityAreaPrimary& primary,std::array<source_shadow_camera::Profile,2>& camera,
        std::array<source_shadow_gpu::Profile,2>& gpu) {
    if(field.templateCount!=source.fieldTemplates||primary.source.templates.size()!=source.primaryTemplates)
        throw std::runtime_error("Current Field/primary Shadow template bank differs");
    unsigned primaryBase=0;for(const auto& row:field.instances)primaryBase=std::max(primaryBase,row.index+1);
    const auto fieldOwners=field.instances.size(),fieldPrimitives=field.primitives.size();
    joint.catalog=field; // preserves sparse source index, packed order and ordinary CB bytes
    joint.catalog.templateCount=field.templateCount+primary.source.catalog.templateCount;
    for(auto row:primary.source.catalog.instances) {
        row.index+=primaryBase;row.firstPrimitive+=unsigned(fieldPrimitives);
        joint.catalog.instances.push_back(row);
    }
    for(auto row:primary.source.catalog.primitives) {
        row.instanceIndex+=primaryBase;row.templateIndex+=field.templateCount;
        joint.catalog.primitives.push_back(row);
    }
    publishNativeCityAreaJointShadowAdmission(source,field,primary,camera);
    for(auto& profile:gpu)source_shadow_gpu::bindCatalog(profile,joint.catalog);
}
inline void connectNativeCityAreaJointShadowGeometry(NativeJointPopulationShadow& joint,
        const NativeModelInputs& completeField,const NativeCityAreaPrimary& primary) {
    connectNativeJointPopulationShadowGeometry(joint,completeField,primary.currentModels);
}
