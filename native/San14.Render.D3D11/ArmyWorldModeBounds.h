#pragma once
#include "Pipeline/city-accessory-pose-native.h"
#include <span>

// DAD880's single unparented WorldPQ bound. Input is the actual ModelCPU
// getter's min/max, after its two preparation roundings, not constructor OBB.
inline std::array<float,20> buildArmyWorldModeBounds(std::span<const float,6> actualGetterMinMax,
        const NativeAccessoryPoseRoot& world) {
    using namespace native_accessory_pose_detail;
    const float loX=mul(actualGetterMinMax[0],world.scale.x),hiX=mul(actualGetterMinMax[3],world.scale.x);
    const float loY=mul(actualGetterMinMax[1],world.scale.y),hiY=mul(actualGetterMinMax[4],world.scale.y);
    const float loZ=mul(actualGetterMinMax[2],world.scale.z),hiZ=mul(actualGetterMinMax[5],world.scale.z);
    const float middleX=mul(add(hiX,loX),.5f),middleY=mul(add(loY,hiY),.5f),middleZ=mul(add(loZ,hiZ),.5f);
    const float minimum=fromBits(0x37480000);
    const float extentX=std::max(mul(sub(hiX,loX),.5f),minimum);
    const float extentY=std::max(mul(sub(hiY,loY),.5f),minimum);
    const float extentZ=std::max(mul(sub(hiZ,loZ),.5f),minimum);
    // matrix's rotation entries are the same A12C80 leaf; scale1 adds no
    // numerical change. Raw Q stays unnormalized and basis stays unscaled.
    const auto rotation=matrix({{1,1,1,0},world.rotation,{0,0,0,1}}).words;
    float x=add(mul(middleY,rotation[4]),mul(middleX,rotation[0]));
    x=add(x,mul(middleZ,rotation[8]));x=add(x,mul(0.f,rotation[12]));
    float y=add(mul(middleX,rotation[1]),mul(middleY,rotation[5]));
    y=add(y,mul(middleZ,rotation[9]));y=add(y,mul(0.f,rotation[13]));
    float z=add(mul(middleX,rotation[2]),mul(middleY,rotation[6]));
    z=add(z,mul(middleZ,rotation[10]));z=add(z,mul(0.f,rotation[14]));
    return {add(x,world.translation.x),add(y,world.translation.y),add(z,world.translation.z),0.f,
        rotation[0],rotation[1],rotation[2],0.f,
        rotation[4],rotation[5],rotation[6],0.f,
        rotation[8],rotation[9],rotation[10],0.f,
        extentX,extentY,extentZ,0.f};
}
