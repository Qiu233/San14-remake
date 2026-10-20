#pragma once
#include "OceanClock.h"
#include "OceanOpticsSettings.h"
#include "OceanTextureMaterialSettings.h"
#include "OceanBubbleMaterialSettings.h"
#include "OceanDrawMaterialState.h"
#include "WaterCameraFrame.h"

namespace San14::Render {
inline OceanBubbleMaterialSettings acceptOceanBubbleMaterialSettings(OceanBubbleMaterialSettings settings) {
    // The ordinary rich group owns index zero, independently of its child.
    for (auto& length : settings.scaleLengths) length=oceanMaterialScale(length);
    return settings;
}
inline std::array<float,4> oceanBubbleScale(float x,float z,float gridX,float gridZ) {
    return {1.0f/x,1.0f/z,gridX/x,gridZ/z};
}
inline std::array<float,4> oceanBubbleBand(std::array<float,4> parameter) {
    const float limit=parameter[0]+4.712389f;
    if (limit<parameter[1]) parameter[1]=limit;
    return parameter;
}

// Composition reads retained runtime and current typed owners. It neither
// advances time nor reads numeric templates from a previous draw.
inline std::array<OceanDrawMaterialState,2> composeOceanDrawMaterials(
    const OceanClock& clock,const OceanSurfaceSettings& surface,const OceanOpticsSettings& optics,
    const OceanTextureMaterialState& texture,const OceanBubbleMaterialSettings& bubble,
    const OceanBubbleRuntime& runtime,const OceanHorizontalDisplacementBinding& horizontal,
    const source_water_camera::CurrentCamera& camera,
    const std::array<float,4>& waveClip,const std::array<float,4>& farClip,
    float gridX,float gridZ,float cutoffDistance,std::uint32_t normalDepth,bool refractionMaterialBound, bool generatorEnabled) {
    OceanDrawMaterialState nearMaterial;
    nearMaterial.eye=camera.eye; nearMaterial.worldToProjection=camera.worldToProjection;
    nearMaterial.worldView=camera.worldView; nearMaterial.cameraDirectionXZ=camera.cameraDirectionXZ;
    // Wave attenuation and its base height share the direction constant.
    // Use the grid's accepted cutoff, as geometry visibility does.
    nearMaterial.cameraDirectionXZ[2]=1.0f/cutoffDistance;
    nearMaterial.cameraDirectionXZ[3]=surface.waterHeight;
    nearMaterial.refractionWorldToTexture=camera.refraction.worldToTexture;
    nearMaterial.depthToZ=camera.refraction.depthToZ;
    nearMaterial.refractionCameraPosition=camera.eye;
    nearMaterial.refractionCameraDirection=camera.refractionDirection;
    // The explicit draw clip mask enables plane zero and disables planes 1..5.
    nearMaterial.clipPlanes[0]=waveClip;
    const auto phases=deriveOceanAnimationPhases(clock,surface,normalDepth);
    nearMaterial.normalUv=oceanNormalUv(texture);
    nearMaterial.normalNoise={1.0f/texture.uv.noiseScaleX,1.0f/texture.uv.noiseScaleZ,phases.normal};
    nearMaterial.heightUv=oceanHeightUv(texture);
    nearMaterial.waterLevel=surface.waterHeight;
    const auto direction=oceanTextureWaveDirection(surface);
    const auto spread=oceanTextureWaveSpread(texture.wave.scatter);
    nearMaterial.waveDirection={direction[0],direction[1],spread[0],spread[1]};
    const float waveNumber=6.2831855f/texture.wave.length;
    nearMaterial.waveParameter={waveNumber,texture.wave.speed*waveNumber,phases.waveTime,0};
    nearMaterial.waveParameter2={texture.wave.kurtosis,1,0,0};
    nearMaterial.waveFigure={texture.wave.amplitude,texture.wave.length,1,texture.wave.width};
    nearMaterial.waveNoise={horizontal.lengths[0],horizontal.lengths[1],surface.displacement,0};
    nearMaterial.shoalingScale=texture.wave.shoalingScale;
    if (refractionMaterialBound) {
        nearMaterial.refractionIndex=optics.refractionIndex;
        nearMaterial.refractionTransparency=optics.refractionTransparency;
        nearMaterial.diffuseTransparency=optics.diffuseTransparency;
        nearMaterial.refractionParameter=oceanRefractionParameters(optics);
    }
    nearMaterial.albedo={optics.albedo[0],optics.albedo[1],optics.albedo[2],optics.roughness};
    nearMaterial.reflectance={optics.reflectance[0],optics.reflectance[1],optics.reflectance[2],optics.roughness};
    auto layerRows=[&](unsigned index,float mask,float phase) {
        const auto& layer=bubble.layers[index];
        return std::array<OceanDrawMaterialState::V4,3>{layer.color,layer.reflectance,
            OceanDrawMaterialState::V4{layer.alphaThreshold,layer.normalThreshold,mask,phase}};
    };
    nearMaterial.surfaceBubbleColor=layerRows(0,bubble.maskScales[0],phases.bubbles[0]);
    nearMaterial.innerBubbleColor=layerRows(1,bubble.maskScales[1],phases.bubbles[1]);
    nearMaterial.shoreBubbleColor=layerRows(2,0,0);
    nearMaterial.surfaceBubbleParameter=oceanBubbleBand(bubble.surfaceBand);
    nearMaterial.innerBubbleParameter=oceanBubbleBand(bubble.innerBand);
    nearMaterial.shoreBubbleParameter=bubble.shoreParameter;
    nearMaterial.bubbleShoalingAdjustment=bubble.shoalingAdjustment;
    nearMaterial.scattering=bubble.scattering;
    nearMaterial.spawnPoints=runtime.points();
    nearMaterial.bubbleDepth={bubble.depth,bubble.layers[0].alphaThreshold,
        bubble.layers[1].alphaThreshold,bubble.layers[2].alphaThreshold};
    const auto& ranges=runtime.frameRanges();
    const double frameTick=std::floor(clock.accumulatedTime/(1.0/60.0));
    for (unsigned layer=0;layer<3;++layer) {
        auto frame=static_cast<std::uint32_t>(double(bubble.animationSpeeds[layer])*frameTick);
        if (ranges.counts[layer]) frame%=ranges.counts[layer];
        nearMaterial.bubbleFrames[layer]=float(ranges.starts[layer]+frame);
    }
    if (generatorEnabled) {
        float phase=0;
        if(surface.bubbleMode<2)phase=float(surface.bubbleMode);
        else if(bubble.generatorPeriod>0) {
            phase=float(std::fmod(clock.accumulatedTime,double(bubble.generatorPeriod))/double(bubble.generatorPeriod));
            if(static_cast<long long>(clock.accumulatedTime/double(bubble.generatorPeriod))&1)phase=1.0f-phase;
        }
        nearMaterial.bubbleFrames[3]=phase;
    }
    nearMaterial.surfaceBubbleScale=oceanBubbleScale(bubble.scaleLengths[0],bubble.scaleLengths[1],gridX,gridZ);
    nearMaterial.innerBubbleScale=oceanBubbleScale(bubble.scaleLengths[2],bubble.scaleLengths[3],gridX,gridZ);
    nearMaterial.shoreBubbleScale=oceanBubbleScale(bubble.scaleLengths[4],bubble.scaleLengths[5],gridX,gridZ);
    auto farMaterial=nearMaterial;
    farMaterial.clipPlanes[0]=farClip;
    farMaterial.waveFigure[0]=0;
    farMaterial.shoalingScale=1;
    farMaterial.bubbleColorScale={};
    const std::array<OceanDrawMaterialState::V4,3> farColor{{{0,0,0,0},{0,0,0,0},{0,1,0,0}}};
    farMaterial.surfaceBubbleColor=farColor; farMaterial.innerBubbleColor=farColor; farMaterial.shoreBubbleColor=farColor;
    farMaterial.bubbleDepth={}; farMaterial.bubbleShoalingAdjustment={1,0,0,0};
    return {nearMaterial,farMaterial};
}
} // namespace San14::Render
