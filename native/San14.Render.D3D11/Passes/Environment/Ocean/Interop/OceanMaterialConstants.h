#pragma once
#include "../OceanClock.h"
#include "../OceanOpticsSettings.h"
#include "../OceanTextureMaterialSettings.h"
#include <cstring>
#include <vector>

namespace San14::Render::Interop {
// Layout of the selected Ocean VS384/PS864 material. Clock business logic is
// independent of these byte lanes and never reads retained constant buffers.
inline void writeOceanAnimationPhases(std::array<std::vector<char>,2>& vertexBytes,
    std::array<std::vector<char>,2>& pixelBytes,const OceanAnimationPhases& phases,bool normalMaterialBound=true) {
    auto store=[](std::vector<char>& bytes,std::size_t offset,float value) {
        std::memcpy(bytes.data()+offset,&value,sizeof(value));
    };
    for(unsigned flat=0;flat<2;++flat) {
        if(normalMaterialBound) {
            store(vertexBytes[flat],200,phases.normal);
            store(pixelBytes[flat],104,phases.normal);
        }
        store(vertexBytes[flat],264,phases.waveTime);
        if(flat) {store(pixelBytes[flat],296,0.0f);store(pixelBytes[flat],360,0.0f);}
        store(pixelBytes[flat],300,flat?0.0f:phases.bubbles[0]);
        store(pixelBytes[flat],364,flat?0.0f:phases.bubbles[1]);
        store(pixelBytes[flat],424,0.0f);store(pixelBytes[flat],428,0.0f);
    }
}
inline void writeOceanSurfaceConstants(std::array<std::vector<char>,2>& vertexBytes,
    const OceanSurfaceSettings& settings) {
    const auto direction=oceanTextureWaveDirection(settings);
    for(auto& bytes:vertexBytes) {
        auto store=[&](std::size_t offset,float value){std::memcpy(bytes.data()+offset,&value,sizeof(value));};
        store(224,settings.waterHeight);
        store(240,direction[0]);store(244,direction[1]);
        store(312,settings.displacement);store(316,0.0f);
    }
}
inline void writeOceanMaterialUvConstants(std::array<std::vector<char>,2>& vertexBytes,
    std::array<std::vector<char>,2>& pixelBytes,const OceanTextureMaterialState& state,
    OceanMaterialTextureBindings bindings) {
    const auto normal=oceanNormalUv(state),height=oceanHeightUv(state);
    auto store=[](std::vector<char>& bytes,std::size_t offset,float value) {
        std::memcpy(bytes.data()+offset,&value,sizeof(value));
    };
    for(unsigned flat=0;flat<2;++flat) {
        if(bindings.normal) {
            for(unsigned i=0;i<normal.size();++i)store(vertexBytes[flat],176+i*4,normal[i]);
            // Noise UV is part of the normal callback, while its phase remains
            // the independently owned Ocean clock publication.
            store(vertexBytes[flat],192,1.0f/state.uv.noiseScaleX);
            store(vertexBytes[flat],196,1.0f/state.uv.noiseScaleZ);
            store(pixelBytes[flat],96,1.0f/state.uv.noiseScaleX);
            store(pixelBytes[flat],100,1.0f/state.uv.noiseScaleZ);
        }
        if(bindings.height)
            for(unsigned i=0;i<height.size();++i)store(vertexBytes[flat],208+i*4,height[i]);
    }
}
inline void writeOceanTextureWaveConstants(std::array<std::vector<char>,2>& vertexBytes,
    const OceanTextureMaterialState& state) {
    const auto& wave=state.wave;
    const auto spread=oceanTextureWaveSpread(wave.scatter);
    const float waveNumber=6.2831855f/wave.length;
    for(unsigned flat=0;flat<2;++flat) {
        auto store=[&](std::size_t offset,float value){std::memcpy(vertexBytes[flat].data()+offset,&value,sizeof(value));};
        store(248,spread[0]);store(252,spread[1]);
        store(256,waveNumber);store(260,wave.kurtosis*waveNumber);store(268,0.0f);
        store(272,wave.scale);store(276,1.0f);store(280,0.0f);store(284,0.0f);
        store(288,flat?0.0f:wave.amplitude);store(292,wave.length);
        store(296,1.0f);store(300,wave.width);
        // The supported selected horizontal-displacement source has no owner.
        // Its constructor UV dimensions remain zero, independently of normal
        // noise dimensions. A non-null wave texture needs its own binding path.
        store(304,0.0f);store(308,0.0f);
        store(368,flat?1.0f:wave.shoalingScale);
    }
}
inline void writeOceanOpticsConstants(std::array<std::vector<char>,2>& pixelBytes,
    const OceanOpticsSettings& settings,bool refractionMaterialBound) {
    for(auto& bytes:pixelBytes) {
        auto store=[&](std::size_t offset,float value){std::memcpy(bytes.data()+offset,&value,sizeof(value));};
        for(unsigned component=0;component<3;++component) {
            store(832+component*4,settings.albedo[component]);
            store(848+component*4,settings.reflectance[component]);
        }
        store(844,settings.roughness);store(860,settings.roughness);
        // D71230 requires both actual refraction-copy owners; child GPU views
        // do not determine whether the selected material callback exists.
        if(refractionMaterialBound) {
            store(160,settings.refractionIndex);store(164,settings.refractionTransparency);
            store(168,settings.diffuseTransparency);
            const auto parameters=oceanRefractionParameters(settings);
            for(unsigned component=0;component<4;++component)store(176+component*4,parameters[component]);
        }
    }
}
} // namespace San14::Render::Interop
