// One Field + full61 primary Shadow population. Include after existing
// model/population/shadow headers. This is independent of the one-city adapter.
#pragma once
struct NativeJointPopulationShadow {
    NativeModelContinuous catalog;
    // Borrow the complete packed geometry from session owners. Reconnect
    // after every actual primary publication, before any synchronous draw.
    NativeModelDrawReferences allModels;
};
inline void connectNativeJointPopulationShadowGeometry(NativeJointPopulationShadow& joint,
    const NativeModelInputs& completeField,const NativeModelInputs& completePrimaries) {
    joint.allModels.blend=completeField.blend;
    joint.allModels.draws.clear();
    joint.allModels.draws.reserve(completeField.draws.size()+completePrimaries.draws.size());
    for(const auto& draw:completeField.draws)joint.allModels.draws.push_back(&draw);
    for(const auto& draw:completePrimaries.draws)joint.allModels.draws.push_back(&draw);
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
