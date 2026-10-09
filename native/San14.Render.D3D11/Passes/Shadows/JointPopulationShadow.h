// One Field + full61 primary Shadow population. Include after existing
// model/population/shadow headers. This is independent of the one-city adapter.
#pragma once
struct NativeJointPopulationShadow {
    NativeModelContinuous catalog;
    NativeModelInputs allModels;
};
inline void connectNativeJointPopulationShadowGeometry(NativeJointPopulationShadow& joint,
    const NativeModelInputs& completeField,const NativeModelInputs& completePrimaries) {
    joint.allModels.blend=completeField.blend;
    joint.allModels.draws=completeField.draws;
    joint.allModels.draws.insert(joint.allModels.draws.end(),completePrimaries.draws.begin(),completePrimaries.draws.end());
    if(joint.allModels.draws.size()!=joint.catalog.primitives.size())
        throw std::runtime_error("Complete Field+primary current geometry must match the joint shadow catalog");
}
inline NativeJointPopulationShadow prepareNativeJointPopulationShadow(const fs::path& input,
    const NativeModelInputs& completeField,const NativeModelInputs& completePrimaries) {
    NativeJointPopulationShadow joint;
    joint.catalog=prepareNativeModelContinuous(input/"catalog");
    connectNativeJointPopulationShadowGeometry(joint,completeField,completePrimaries);
    return joint;
}
