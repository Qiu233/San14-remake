#pragma once
#include <cstdint>

// Two bounded little-endian memory packages. Every offset is relative to the
// beginning of its own package. No resource path or original native pointer is
// serialized. Stage codes match G1S: 0 PS, 1 VS (other stages require a consumer).
struct San14CurrentEffectInputV1 {
    std::uint32_t size,version;
    std::uint64_t resourceGeneration;
    const std::uint8_t* resources;
    std::uint32_t resourceBytes;
    const std::uint8_t* frame;
    std::uint32_t frameBytes,updateKind; //0 retain;1 replace frame;2 clear frame
    std::uint64_t sequence; //actual host frame identity, never an algorithm selector
};
static_assert(sizeof(San14CurrentEffectInputV1)==56);

struct San14EffectResourceHeaderV1 {
    std::uint32_t magic,version; //CER1=31524543; v1 planar 2D, v2 explicit texture arrays
    std::uint32_t pipelineCount,pipelineOffset,meshCount,meshOffset;
    std::uint32_t textureCount,textureOffset,samplerCount,samplerOffset;
    std::uint32_t reserved0,reserved1;
};
struct San14EffectPipelineV1 {
    std::uint32_t id,renderingType,runtimeBank,stageOffset,stageCount;
    std::uint32_t elementOffset,elementCount,reserved;
};
struct San14EffectStageV1 {std::uint32_t stage,byteOffset,byteLength;};
struct San14EffectElementV1 {
    char semantic[32];
    std::uint32_t index,format,slot,offset,classification,stepRate;
};
struct San14EffectMeshV1 {
    std::uint32_t id,stride,indexFormat,vertexOffset,vertexBytes,indexOffset,indexBytes;
};
struct San14EffectTextureV1 {
    std::uint32_t id,width,height,mipCount,resourceFormat,viewFormat;
    // v1: reserved fields are zero; ArraySize is one and view dimension is 2D.
    // v2: reserved0 is ArraySize; reserved1 is DXBC view dimension (4=2D, 5=2DArray).
    // Subresources are in D3D order: mip + arraySlice * mipCount.
    std::uint32_t subresourceOffset,subresourceCount,reserved0,reserved1;
};
struct San14EffectSubresourceV1 {std::uint32_t byteOffset,byteLength,rowPitch,slicePitch;};
struct San14EffectSamplerV1 {std::uint32_t id,byteOffset,byteLength;}; //D3D11_SAMPLER_DESC

struct San14EffectFrameHeaderV1 {
    std::uint32_t magic,version; //CEF1=31464543, version1
    std::uint32_t streamOffset,streamBytes,drawCount,drawOffset;
    std::uint32_t constantCount,constantOffset,textureBindingCount,textureBindingOffset;
    std::uint32_t samplerBindingCount,samplerBindingOffset;
};
struct San14EffectConstantV1 {
    std::uint32_t stage,slot,byteOffset,byteLength; //complete selected RDEF buffer, 16B aligned length
};
// semantic0: this generation's uploaded resource id; 1: current MainD1 mirror;
// 2: current AutoHDRScaleExposure; 3: Scattering2FogAccel; 4: actual scene capture.
struct San14EffectBindingV1 {std::uint32_t stage,slot,semantic,resourceId;};
struct San14EffectDrawV1 {
    std::uint32_t pipelineId,sourceNode,view,timing,configuration,renderingType;
    std::uint32_t headerFlags,flags,alphaReference,dynamicOffset,dynamicStride,dynamicCount;
    std::uint32_t meshId,topology,indexCount,indexStart; //meshId FFFFFFFF means sprite
    std::int32_t baseVertex;
    std::uint32_t firstConstant,constantCount,firstTexture,textureCount,firstSampler,samplerCount;
    std::uint32_t blendOffset,blendBytes; //D3D11_BLEND_DESC recipe; inherited component masks retained
};
static_assert(sizeof(San14EffectResourceHeaderV1)==48);
static_assert(sizeof(San14EffectPipelineV1)==32);
static_assert(sizeof(San14EffectElementV1)==56);
static_assert(sizeof(San14EffectMeshV1)==28);
static_assert(sizeof(San14EffectTextureV1)==40);
static_assert(sizeof(San14EffectFrameHeaderV1)==48);
static_assert(sizeof(San14EffectDrawV1)==100);
