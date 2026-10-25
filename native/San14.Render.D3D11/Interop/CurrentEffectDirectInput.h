#pragma once
#include <cstdint>

struct San14EffectDirectHeaderV1 {
    std::uint32_t magic,version; //CED1
    std::uint32_t bufferCount,bufferOffset,programCount,programOffset;
    std::uint32_t requiredKernels,genericType,vertexStride,indirectOffset,vertexBufferId,argumentBufferId;
};
struct San14EffectDirectBufferV1 {
    std::uint32_t id,byteLength,usage,format,structureStride,initialOffset,initialBytes,reserved;
};
struct San14EffectDirectProgramV1 {
    std::uint32_t kernel,sourceOffset,sourceBytes,entryOffset,entryBytes,profileOffset,profileBytes;
};
struct San14EffectDirectCommandHeaderV1 {
    std::uint32_t magic,version,uploadCount,uploadOffset,dispatchCount,dispatchOffset,reserved0,reserved1;
};
struct San14EffectDirectUploadV1 {std::uint32_t bufferId,bufferOffset,byteOffset,byteLength;};
struct San14EffectDirectDispatchV1 {
    std::uint32_t kernel,groupsX,groupsY,groupsZ,constantCount,constantOffset;
    std::uint32_t resourceCount,resourceOffset,unorderedCount,unorderedOffset,reserved0,reserved1;
};
struct San14EffectDirectConstantV1 {std::uint32_t slot,byteOffset,byteLength;};
struct San14EffectDirectBindingV1 {std::uint32_t slot,bufferId;};
static_assert(sizeof(San14EffectDirectHeaderV1)==48);
static_assert(sizeof(San14EffectDirectBufferV1)==32);
static_assert(sizeof(San14EffectDirectProgramV1)==28);
static_assert(sizeof(San14EffectDirectCommandHeaderV1)==32);
static_assert(sizeof(San14EffectDirectDispatchV1)==48);
