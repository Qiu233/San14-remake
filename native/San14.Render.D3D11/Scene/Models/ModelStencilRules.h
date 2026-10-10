#pragma once
#include "../View/MapViewPopulation.h"
#include <span>

namespace San14::Render {
enum class ModelStencilTarget { ClassOne, Other };
enum class StencilComparison : unsigned { Unspecified=0, Always=8 };
enum class StencilOperation : unsigned { Unspecified=0, Keep=1, Replace=3 };
struct ModelStencilBinding {
    unsigned instanceIndex{}, drawIndex{}, excludedGroups{}, recordMembership{};
};
static_assert(sizeof(ModelStencilBinding)==16);
struct ModelStencilDirective {
    bool submit{}, enabled{};
    unsigned reference{}, readMask{}, writeMask{};
    StencilComparison compare{};
    StencilOperation fail{}, depthFail{}, pass{};
};
inline ModelStencilDirective selectModelStencil(std::span<const MapViewRecord> records,
    const ModelStencilSettings& settings,const MapViewFrame& view,
    const ModelStencilBinding& binding,std::size_t recordIndex,ModelStencilTarget target) {
    if(recordIndex>=records.size())throw std::runtime_error("Current ordinary54 record index");
    ModelStencilDirective result;
    const auto& record=records[recordIndex];
    if(!(binding.recordMembership&(1u<<recordIndex))||view.ordinaryCounts[recordIndex]==0||
        (binding.excludedGroups&record.groupMask)!=0)return result;
    const auto sign=(record.groupMask&0x80000000u)?ModelGroupSign::Negative:ModelGroupSign::Nonnegative;
    bool selected=sign==settings.selectedSign;
    unsigned reference=record.groupMask;
    if(settings.selectorGroup) {
        if(!settings.referencePrefix&&record.groupMask==settings.selectorGroup)selected=false;
        if(settings.referencePrefix)reference|=target==ModelStencilTarget::ClassOne?0x60u:0x80u;
    } else if(settings.referencePrefix)reference|=target==ModelStencilTarget::ClassOne?0x60u:0x80u;
    else if(settings.secondaryReferencePrefix&&target==ModelStencilTarget::ClassOne)reference|=0xD0u;
    result.submit=selected;result.enabled=settings.enabled;
    result.reference=reference&255;result.readMask=result.writeMask=255;
    result.compare=StencilComparison::Always;result.fail=result.depthFail=StencilOperation::Keep;result.pass=StencilOperation::Replace;
    return result;
}
} // namespace San14::Render
