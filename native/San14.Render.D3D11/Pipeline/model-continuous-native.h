// All supported source FieldScene static Model inputs. Include after model-native.h.
// Device objects are prepared once; each accepted camera selects and rewrites
// complete 160B/80B source CB payloads. No original files are read per request.
#pragma once
#include <algorithm>
#include <array>
#include <cmath>
#include <cstdint>
#include <cstring>
#include <fstream>
#include <numeric>
#include <stdexcept>
#include <string>
#include <vector>
#include <emmintrin.h>

struct NativeModelContinuousInstance {
    std::uint32_t index{},mode{},firstPrimitive{},primitiveCount{},hasUnsupported{};
    float forceDistance{},distanceBias{};
    std::array<float,20> obb{};
};
struct NativeModelContinuousPrimitive {
    std::uint32_t instanceIndex{},drawIndex{},templateIndex{};
    std::uint64_t tableOwner{};
    std::array<char,160> vertex{};
    std::array<char,80> pixel{};
};
struct NativeModelContinuous {
    std::uint32_t templateCount{},commonMode{};
    std::vector<unsigned> sortFields;
    std::vector<NativeModelContinuousInstance> instances;
    std::vector<NativeModelContinuousPrimitive> primitives;
};
struct NativeModelContinuousDraw {
    std::uint32_t primitiveIndex{};
    std::uint64_t distanceKey{},tableOwner{};
    std::array<char,160> vertex{};
    std::array<char,80> pixel{};
};
struct NativeModelContinuousPlan {
    std::vector<std::uint32_t> visibleInstances,visibleUnsupportedInstances;
    std::vector<NativeModelContinuousDraw> draws;
};

inline NativeModelContinuous prepareNativeModelContinuous(const fs::path& source) {
    NativeModelContinuous result;std::uint32_t instanceCount,primitiveCount;
    std::ifstream info(source/"catalog.txt");
    info>>instanceCount>>primitiveCount>>result.templateCount>>result.commonMode;
    if(!info)throw std::runtime_error("Static Model continuous catalog metadata absent");
    std::ifstream order(source/"sort-fields.txt");unsigned field;
    while(order>>field)result.sortFields.push_back(field);
    if(result.sortFields!=std::vector<unsigned>{14,3})throw std::runtime_error("Unsupported static Model opaque sort fields");
    std::ifstream instances(source/"instances.bin",std::ios::binary),primitives(source/"primitives.bin",std::ios::binary);
    if(!instances||!primitives)throw std::runtime_error("Static Model complete instance/primitive source absent");
    result.instances.resize(instanceCount);result.primitives.resize(primitiveCount);
    for(auto& row:result.instances) {
        instances.read(reinterpret_cast<char*>(&row.index),4);instances.read(reinterpret_cast<char*>(&row.mode),4);
        instances.read(reinterpret_cast<char*>(&row.firstPrimitive),4);instances.read(reinterpret_cast<char*>(&row.primitiveCount),4);
        instances.read(reinterpret_cast<char*>(&row.hasUnsupported),4);instances.read(reinterpret_cast<char*>(&row.forceDistance),4);
        instances.read(reinterpret_cast<char*>(&row.distanceBias),4);instances.read(reinterpret_cast<char*>(row.obb.data()),80);
        if(!instances||row.firstPrimitive>primitiveCount||row.primitiveCount>primitiveCount-row.firstPrimitive
            ||(row.mode!=0&&row.mode!=3))throw std::runtime_error("Static Model OBB/primitive range requires a separate source profile");
    }
    for(auto& row:result.primitives) {
        primitives.read(reinterpret_cast<char*>(&row.instanceIndex),4);primitives.read(reinterpret_cast<char*>(&row.drawIndex),4);
        primitives.read(reinterpret_cast<char*>(&row.templateIndex),4);primitives.read(reinterpret_cast<char*>(&row.tableOwner),8);
        primitives.read(row.vertex.data(),row.vertex.size());primitives.read(row.pixel.data(),row.pixel.size());
        if(!primitives||row.templateIndex>=result.templateCount)throw std::runtime_error("Static Model primitive template index/payload incomplete");
    }
    return result;
}
inline float modelContinuousAdd(float a,float b){return _mm_cvtss_f32(_mm_add_ss(_mm_set_ss(a),_mm_set_ss(b)));}
inline float modelContinuousSub(float a,float b){return _mm_cvtss_f32(_mm_sub_ss(_mm_set_ss(a),_mm_set_ss(b)));}
inline float modelContinuousMul(float a,float b){return _mm_cvtss_f32(_mm_mul_ss(_mm_set_ss(a),_mm_set_ss(b)));}
inline float modelContinuousNeg(float value){std::uint32_t bits;std::memcpy(&bits,&value,4);bits^=0x80000000u;std::memcpy(&value,&bits,4);return value;}

inline std::array<float,3> modelContinuousSupport(const std::array<float,20>& obb,const float* plane,bool culling) {
    std::array<float,3> point{obb[0],obb[1],obb[2]};
    for(unsigned axis=0;axis<3;++axis) {
        const auto* basis=obb.data()+4+axis*4;
        float nx=culling?modelContinuousNeg(plane[0]):plane[0];
        float ny=culling?modelContinuousNeg(plane[1]):plane[1];
        float nz=culling?modelContinuousNeg(plane[2]):plane[2];
        float dot=modelContinuousAdd(modelContinuousMul(basis[1],ny),modelContinuousMul(basis[0],nx));
        dot=modelContinuousAdd(dot,modelContinuousMul(basis[2],nz));
        const float extent=modelContinuousMul(obb[16+axis],dot<=0.f?-1.f:1.f);
        for(unsigned coordinate=0;coordinate<3;++coordinate) {
            float product=modelContinuousMul(extent,basis[coordinate]);
            point[coordinate]=culling?modelContinuousAdd(product,point[coordinate]):modelContinuousAdd(point[coordinate],product);
        }
    }
    return point;
}
inline bool modelContinuousVisible(const NativeModelContinuousInstance& instance,const std::array<std::array<float,4>,6>& planes) {
    if(instance.mode==3)return true;
    constexpr unsigned order[]{0,1,4,2,3,5};
    for(unsigned index:order) {
        const auto& p=planes[index];auto support=modelContinuousSupport(instance.obb,p.data(),true);
        float dot=modelContinuousAdd(modelContinuousMul(support[1],p[1]),modelContinuousMul(support[0],p[0]));
        dot=modelContinuousAdd(dot,modelContinuousMul(support[2],p[2]));dot=modelContinuousAdd(dot,p[3]);
        if(!(dot<=0.f))return false;
    }
    return true;
}
inline std::uint64_t modelContinuousDistanceKey(const NativeModelContinuousInstance& instance,const float* plane,float span,float scale) {
    float distance=instance.forceDistance;
    if(distance<0.f) {
        auto support=modelContinuousSupport(instance.obb,plane,false);
        distance=modelContinuousAdd(modelContinuousMul(support[1],plane[1]),modelContinuousMul(support[0],plane[0]));
        distance=modelContinuousAdd(distance,modelContinuousMul(support[2],plane[2]));distance=modelContinuousAdd(distance,plane[3]);
        distance=modelContinuousNeg(distance);
    }
    float upper=modelContinuousSub(span,1.f);upper=upper>0.f?upper:0.f;
    float bounded=modelContinuousAdd(distance,instance.distanceBias);if(!(upper>=bounded))bounded=upper;
    float value=modelContinuousAdd(span,bounded);if(0.f>=value)return 0;
    value=modelContinuousMul(value,scale);std::uint64_t add=0;
    constexpr float two63=9223372036854775808.f;
    if(!(value<two63)){value=modelContinuousSub(value,two63);if(!(value>=two63))add=0x8000000000000000ULL;}
    return std::uint64_t(_mm_cvttss_si64(_mm_set_ss(value)))+add;
}

// Same accepted original 3C0 prefix and normalized six planes used by terrain.
// The source reverse-depth PreparedView plane+40 is planes[4]. No independent
// camera reconstruction, source reads, object creation or fixed-camera lookup.
inline NativeModelContinuousPlan selectNativeModelContinuous(const NativeModelContinuous& catalog,
    const std::array<char,0x3C0>& cameraPrefix,const std::array<std::array<float,4>,6>& planes) {
    NativeModelContinuousPlan plan;float nearValue,farValue;
    std::memcpy(&nearValue,cameraPrefix.data()+0x140,4);std::memcpy(&farValue,cameraPrefix.data()+0x144,4);
    float span=modelContinuousAdd(modelContinuousSub(farValue,nearValue),1.f);
    float doubled=modelContinuousAdd(span,span);
    float scale=float(18446744073709551616.0/double(doubled));
    const unsigned viewIndices[]{0,4,8,12,1,5,9,13,2,6,10,14};
    std::array<char,48> worldToView{};
    for(unsigned i=0;i<12;++i)std::memcpy(worldToView.data()+i*4,cameraPrefix.data()+0x80+viewIndices[i]*4,4);
    for(const auto& instance:catalog.instances) {
        if(!modelContinuousVisible(instance,planes))continue;
        plan.visibleInstances.push_back(instance.index);
        if(instance.hasUnsupported)plan.visibleUnsupportedInstances.push_back(instance.index);
        const auto key=modelContinuousDistanceKey(instance,planes[4].data(),span,scale);
        for(std::uint32_t index=instance.firstPrimitive;index<instance.firstPrimitive+instance.primitiveCount;++index) {
            const auto& primitive=catalog.primitives[index];NativeModelContinuousDraw draw;
            draw.primitiveIndex=index;draw.distanceKey=key;draw.tableOwner=primitive.tableOwner;
            draw.vertex=primitive.vertex;draw.pixel=primitive.pixel;
            std::memcpy(draw.vertex.data(),cameraPrefix.data()+0x100,64);
            std::memcpy(draw.pixel.data()+16,worldToView.data(),48);plan.draws.push_back(draw);
        }
    }
    // Original D4B7E0: each enabled field is an ascending stable 8-byte radix;
    // the last field pass is most significant. Exact ties retain publication.
    std::vector<NativeModelContinuousDraw> scratch(plan.draws.size());
    for(unsigned field:catalog.sortFields)for(unsigned shift=0;shift<64;shift+=8) {
        std::array<std::size_t,256> counts{};
        for(const auto& draw:plan.draws){auto key=field==14?draw.distanceKey:draw.tableOwner;++counts[(key>>shift)&255];}
        std::size_t sum=0;for(auto& count:counts){auto n=count;count=sum;sum+=n;}
        for(const auto& draw:plan.draws){auto key=field==14?draw.distanceKey:draw.tableOwner;scratch[counts[(key>>shift)&255]++]=draw;}
        plan.draws.swap(scratch);
    }
    return plan;
}

// Thin adapter for the already accepted source_camera::Frame shape. It copies
// its current prefix/plane equations; it does not construct another Camera.
template<class AcceptedSourceCameraFrame>
inline NativeModelContinuousPlan selectNativeModelContinuousFromCameraFrame(const NativeModelContinuous& catalog,
    const AcceptedSourceCameraFrame& frame) {
    std::array<char,0x3C0> prefix{};std::memcpy(prefix.data(),frame.prefix.data(),prefix.size());
    std::array<std::array<float,4>,6> planes{};
    for(unsigned i=0;i<6;++i){const auto& p=frame.planes[i].equation;planes[i]={p.X,p.Y,p.Z,p.W};}
    return selectNativeModelContinuous(catalog,prefix,planes);
}

inline ComPtr<ID3D11Buffer> modelContinuousConstantBuffer(ID3D11Device* device,const char* bytes,UINT size) {
    D3D11_BUFFER_DESC desc{};desc.ByteWidth=size;desc.Usage=D3D11_USAGE_DEFAULT;desc.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
    D3D11_SUBRESOURCE_DATA initial{};initial.pSysMem=bytes;ComPtr<ID3D11Buffer> result;
    check(device->CreateBuffer(&desc,&initial,&result),"Complete mutable source Model CB");return result;
}
inline NativeModelInputs prepareNativeModelContinuousGpu(ID3D11Device* device,const NativeModelContinuous& catalog,const fs::path& source) {
    auto templates=loadNativeModels(device,source);NativeModelInputs result;result.blend=templates.blend;
    if(templates.draws.size()!=catalog.templateCount)throw std::runtime_error("Static Model GPU template catalog differs");
    result.draws.reserve(catalog.primitives.size());
    for(const auto& primitive:catalog.primitives) {
        auto draw=templates.draws.at(primitive.templateIndex);
        draw.identity="instance-"+std::to_string(primitive.instanceIndex)+"/draw-"+std::to_string(primitive.drawIndex);
        draw.vsConstants=modelContinuousConstantBuffer(device,primitive.vertex.data(),UINT(primitive.vertex.size()));
        draw.psConstants=modelContinuousConstantBuffer(device,primitive.pixel.data(),UINT(primitive.pixel.size()));
        result.draws.push_back(std::move(draw)); // Static mesh/texture/shader/state COM owners shared.
    }
    return result;
}
inline NativeModelInputs updateSelectNativeModelContinuous(ID3D11DeviceContext* context,const NativeModelContinuousPlan& plan,
    const NativeModelInputs& allModels) {
    if(!plan.visibleUnsupportedInstances.empty())throw std::runtime_error("Current view contains explicitly unclosed static Model profiles");
    NativeModelInputs result;result.blend=allModels.blend;result.draws.reserve(plan.draws.size());
    for(const auto& selected:plan.draws) {
        const auto& draw=allModels.draws.at(selected.primitiveIndex);
        context->UpdateSubresource(draw.vsConstants.Get(),0,nullptr,selected.vertex.data(),0,0);
        context->UpdateSubresource(draw.psConstants.Get(),0,nullptr,selected.pixel.data(),0,0);
        result.draws.push_back(draw);
    }
    return result; // Submit immediately with the existing drawNativeModels.
}
