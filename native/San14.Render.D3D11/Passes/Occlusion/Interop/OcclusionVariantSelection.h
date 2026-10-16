#pragma once
#include "../OcclusionResourceBank.h"
#include "../ContactOcclusionCurrent.h"

namespace San14::Render::Interop {
// These word positions belong to the imported G1 variant record. Domain
// selection remains named; unknown/reserved words retain the real template.
inline std::array<unsigned,16> prepareOcclusionVariantKey(const OcclusionResourceBank& resources,
    const contact_occlusion::VariantSelection& selected) {
    if(resources.variants.empty())throw std::runtime_error("Contact occlusion variant bank is empty");
    auto key=resources.variants.front().words;
    key[0]=selected.usesViewDepth;key[1]=selected.normalMode;key[2]=0;key[3]=selected.quality;
    key[4]=(resources.blurCapabilities&(std::uint64_t(1)<<(selected.blurCapabilityValue&63)))?selected.requestedBlurKernel:0;
    key[5]=selected.linearBlur;key[8]=!selected.usesViewDepth;key[9]=selected.suppressHalo;
    // Each two-bit family selects per-group Radius/RadiusRange, Ray and
    // Occlusion arrays. The other fixed selectors are the actual Main caller's
    // normal-source interpretation, primary switch mode and reference enable.
    constexpr unsigned PerGroupParameterFamilies=0x3F,PrimaryNormalSource=4,PrimarySwitchMode=7;
    key[10]=PerGroupParameterFamilies;key[11]=PrimaryNormalSource;key[12]=selected.groupCount;
    key[13]=PrimarySwitchMode;key[14]=1;key[15]=selected.splitRange;
    return key;
}
} // namespace San14::Render::Interop
