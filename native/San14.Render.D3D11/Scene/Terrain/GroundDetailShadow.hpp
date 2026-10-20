#pragma once
#include "GroundDetailSubmission.hpp"
#include "../../Passes/Shadows/ShadowCameraFrame.h"
#include <optional>

namespace ground_detail_source {
// The compact wrapper belongs to the View's selected shadow light. The first
// caster gate belongs instead to the first nonexcluded directional light in
// the procedural template's ordered collection (B95C40 context+18).
struct CompactCasterSelection {
    bool wrapperPresent{};
    std::optional<uint8_t> firstLightFlags;
};

inline source_camera::Matrix frustumCasterBox(const CurrentView& view) {
    // 1090730 publishes maximum then minimum. B95C40 reverses those endpoints
    // for D19B10 and supplies W=1 and no additional transform.
    const auto lo=view.minimum,hi=view.maximum;
    const float x=M(S(hi.X,lo.X),.5f),y=M(S(hi.Y,lo.Y),.5f),z=M(S(hi.Z,lo.Z),.5f);
    return {x,0,0,0, 0,y,0,0, 0,0,z,0, A(x,lo.X),A(y,lo.Y),A(z,lo.Z),1};
}

inline std::vector<source_shadow_camera::ProceduralCaster> prepareCompactCasters(
    const CurrentView& view,const OrderedPopulation& population,
    std::span<const TemplateWorld> templates,WorldDispatch dispatch,
    uint32_t successfulViews,const WorldCommon& common,const CompactCasterSelection& selected) {
    std::vector<source_shadow_camera::ProceduralCaster> result;
    if(!selected.wrapperPresent||!selected.firstLightFlags||
        !firstCasterFamily(*selected.firstLightFlags))return result;
    const auto box=frustumCasterBox(view);
    // aggregate already retains the complete source-ordered primitive directory
    // for every nonempty template, including empty LOD type22 commands.
    for(const auto& submission:population.submissions) {
        const auto index=submission.primitive.templateIndex;
        if(index>=templates.size())throw std::runtime_error("Procedural shadow template identity");
        if(!templateMasks(dispatch,templates[index],successfulViews,common).shadow)continue;
        if(result.empty()||result.back().templateIndex!=index)result.push_back({index,0,box});
        ++result.back().primitiveCount;
    }
    return result;
}
} // namespace ground_detail_source
