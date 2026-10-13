#pragma once
#include "Category2ResourceInputs.h"
#include <memory>

struct NativeCategory2Resources : NativeCategory2Inputs {
    NativeModelInputs models;
};
inline std::shared_ptr<const NativeCategory2Resources> prepareNativeCategory2Resources(ID3D11Device* device,const fs::path& input) {
    auto result=std::make_shared<NativeCategory2Resources>();
    static_cast<NativeCategory2Inputs&>(*result)=readNativeCategory2Inputs(input);
    result->models=loadNativeModels(device,input/"geometry");
    return result;
}
