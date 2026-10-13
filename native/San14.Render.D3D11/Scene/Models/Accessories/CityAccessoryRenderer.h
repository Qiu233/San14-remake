#pragma once
#include "AccessoryPopulation.h"
#include "Category2Resources.h"
#include "AccessoryMaterials.h"

// Current World attachments borrow one immutable category2 resource bank.
// Physical models, private buffers, poses and clocks belong to the shared pool.
struct NativeCityAccessoryGpu {
    source_accessory_population::Profile population;
    std::shared_ptr<const NativeCategory2Resources> resources;
    std::shared_ptr<NativeAccessoryMaterials> materials;
    std::map<int,San14::Render::CityGateFlagKey> currentFlags;
};
inline NativeCityAccessoryGpu prepareNativeCityAccessoryGpu(ID3D11Device* device,
    const fs::path& input,source_accessory_population::Profile current) {
    NativeCityAccessoryGpu result;result.population=std::move(current);
    result.resources=prepareNativeCategory2Resources(device,input);
    result.materials=std::make_shared<NativeAccessoryMaterials>(device,input/"material-source");
    return result;
}
inline const NativeAccessoryMaterial& nativeAccessoryMaterialForDraw(const NativeCityAccessoryGpu& owner,
    unsigned draw,int force) {
    const auto manager=owner.resources->bindings.at(draw).materialManager;
    return manager<0?owner.materials->pole():owner.materials->material(unsigned(manager),owner.currentFlags.at(force));
}
inline ComPtr<ID3D11Buffer> createNativeAccessoryCurrentCb(ID3D11Device* device,UINT size) {
    D3D11_BUFFER_DESC desc{};desc.ByteWidth=size;desc.Usage=D3D11_USAGE_DEFAULT;desc.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
    ComPtr<ID3D11Buffer> result;check(device->CreateBuffer(&desc,nullptr,&result),"Accessory current full CB");return result;
}
