#pragma once
#include "OceanGrid.h"
#include "OceanWaveSettings.h"
#include <array>
#include <vector>

namespace San14::Render {
struct OceanSpectrumCell {
    float h0Real{},h0Imag{},mirrorReal{},mirrorImag{},kx{},kz{},kLength{},omega{};
};
static_assert(sizeof(OceanSpectrumCell)==32);
struct OceanSpectrumChanges {bool waveVectors{},initialSpectrum{},dispersion{};};
// These named float CRT boundaries preserve formula grouping. Their bit parity
// with the original dispatched CRT is an explicit outstanding numerical limit.
inline float oceanSpectrumPower(float base,float exponent){return std::pow(base,exponent);}
inline float oceanSpectrumExponential(float value){return std::exp(value);}
inline float oceanSpectrumRoot(float value){return std::sqrt(value);}
inline float oceanAmplitudeScale(const OceanGridSettings& grid,const OceanWaveSettings& wave) {
    const float meanSize=(grid.sizeZ+grid.sizeX)*0.5f;
    if(meanSize==0)return 0;
    const float ratio=wave.amplitude/(meanSize*meanSize);return ratio*ratio;
}
inline float oceanInitialSpectrumMultiplier(float kx,float kz,const OceanWaveSettings& wave,float amplitudeScale) {
    const float kSquared=(kx*kx+0.0f)+kz*kz;
    const float windSquared=(0.0f+wave.windX*wave.windX)+wave.windZ*wave.windZ;
    if(windSquared==0||kSquared==0)return 0.0f;
    const float dot=(kx*wave.windX+0.0f)+kz*wave.windZ;
    float alignmentSquared=(dot*dot)/(kSquared*windSquared);
    if(alignmentSquared>=1.0f)alignmentSquared=1.0f;
    constexpr float directionalExponent=2.0f;
    const float direction=oceanSpectrumPower(alignmentSquared,directionalExponent*0.5f);
    const float longWave=oceanSpectrumExponential(-1.0f/(kSquared*((windSquared*windSquared)/(wave.gravity*wave.gravity))));
    const float shortWave=oceanSpectrumExponential(-(kSquared*wave.smallWaveSuppressionLength*wave.smallWaveSuppressionLength));
    return oceanSpectrumRoot(direction*(longWave/(kSquared*kSquared))*amplitudeScale*shortWave)*0.70710677f;
}
inline OceanSpectrumChanges oceanSpectrumChanges(const OceanGridSettings& oldGrid,const OceanWaveSettings& oldWave,
    const OceanGridSettings& grid,const OceanWaveSettings& wave) {
    const bool vectors=oldGrid.columns!=grid.columns||oldGrid.rows!=grid.rows||oldGrid.sizeX!=grid.sizeX||oldGrid.sizeZ!=grid.sizeZ;
    const bool gravity=oldWave.gravity!=wave.gravity;
    return {vectors,vectors||gravity||oldWave.windX!=wave.windX||oldWave.windZ!=wave.windZ||
        oldWave.amplitude!=wave.amplitude||oldWave.smallWaveSuppressionLength!=wave.smallWaveSuppressionLength,
        vectors||gravity||oldGrid.cycle!=grid.cycle};
}
inline void constructOceanSpectrum(std::vector<OceanSpectrumCell>& cells,const std::array<std::vector<float>,2>& gaussian,
    const OceanGridSettings& grid,const OceanWaveSettings& wave,OceanSpectrumChanges changes) {
    validateOceanGridSettings(grid);validateOceanWaveSettings(wave);
    const std::size_t count=std::size_t(grid.columns)*grid.rows;
    if(cells.size()!=count||gaussian[0].size()!=count||gaussian[1].size()!=count)
        throw std::runtime_error("Ocean spectrum layout requires matching retained Gaussian storage");
    const float amplitudeScale=oceanAmplitudeScale(grid,wave),cycleFactor=grid.cycle/6.2831855f;
    for(std::uint32_t z=0;z<grid.rows;++z)for(std::uint32_t x=0;x<grid.columns;++x) {
        const std::size_t i=std::size_t(z)*grid.columns+x;auto& cell=cells[i];
        if(changes.waveVectors) {
            cell.kx=(float(x)-float(grid.columns)*0.5f)*(6.2831855f/grid.sizeX);
            cell.kz=(float(z)-float(grid.rows)*0.5f)*(6.2831855f/grid.sizeZ);
            cell.kLength=oceanSpectrumRoot((cell.kx*cell.kx+0.0f)+cell.kz*cell.kz);
        }
        if(changes.initialSpectrum) {
            const float multiplier=oceanInitialSpectrumMultiplier(cell.kx,cell.kz,wave,amplitudeScale);
            cell.h0Real=multiplier*gaussian[0][i];cell.h0Imag=multiplier*gaussian[1][i];
        }
        if(changes.dispersion)cell.omega=std::floor(oceanSpectrumRoot(cell.kLength*wave.gravity)*cycleFactor)/cycleFactor;
        if(!std::isfinite(cell.kx)||!std::isfinite(cell.kz)||!std::isfinite(cell.kLength)||
           !std::isfinite(cell.h0Real)||!std::isfinite(cell.h0Imag)||!std::isfinite(cell.omega))
            throw std::runtime_error("Ocean spectrum exceeds the supported finite arithmetic domain");
    }
    // Complete h0 before any mirror read. The time-spectrum stage conjugates.
    if(changes.initialSpectrum)for(std::uint32_t z=0;z<grid.rows;++z)for(std::uint32_t x=0;x<grid.columns;++x) {
        const std::size_t i=std::size_t(z)*grid.columns+x;
        const std::size_t mirror=std::size_t(z==0?0:grid.rows-z)*grid.columns+(x==0?0:grid.columns-x);
        cells[i].mirrorReal=cells[mirror].h0Real;cells[i].mirrorImag=cells[mirror].h0Imag;
    }
}
} // namespace San14::Render
