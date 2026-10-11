// Ocean CPU spectrum and retained FFT producer. No GPU or process access.
#pragma once
#include "OceanGrid.h"
#include "OceanSpectrum.h"
#include "RenderRandom.h"
#include <optional>
#include <cstdint>
#include <cstring>
#include <utility>
#include <vector>
#include <array>
#include <fstream>
#include <limits>
#include <cmath>
#ifdef _MSC_VER
#pragma float_control(precise,on,push)
#pragma fp_contract(off)
#endif
#pragma once
#include <cstdint>
#include <cstring>
#include <utility>

// CE 140014590; requires /fp:strict (or an equivalent strict binary32 mode).
// Keep the original arithmetic grouping; this is not std::sin/std::cos.
inline void OceanSourceSinCos(float angle, float& sine, float& cosine) {
    if (angle == 0.0f) { sine = 0.0f; cosine = 1.0f; return; }
    bool positiveSine = 0.0f <= angle;
    if (!positiveSine) angle = -angle;
    while (6.2831855f <= angle) angle = angle + -6.2831855f;
    bool positiveCosine = angle < 3.1415927f;
    if (!positiveCosine) { angle = angle + -3.1415927f; positiveSine = !positiveSine; }
    if (1.5707964f <= angle) { angle = 3.1415927f - angle; positiveCosine = !positiveCosine; }
    float square, polynomial;
    if (angle < 0.6683185f) {
        square = angle * angle;
        if (positiveSine) polynomial = (6.0f - square) * 20.0f + square * square;
        else polynomial = (square - 6.0f) * 20.0f - square * square;
        sine = polynomial * angle * 0.008333334f;
        if (positiveCosine) cosine = ((square * square + 360.0f) * (30.0f - square) - 10080.0f) * 0.0013888889f;
        else cosine = ((square * square + 360.0f) * (square - 30.0f) + 10080.0f) * 0.0013888889f;
        return;
    }
    if (angle < 0.9083185f) {
        square = angle * angle;
        polynomial = square * square + 360.0f;
        if (positiveCosine) polynomial = polynomial * (30.0f - square) - 10080.0f;
        else polynomial = polynomial * (square - 30.0f) + 10080.0f;
        cosine = polynomial * 0.0013888889f;
        square = (1.5707964f - angle) * (1.5707964f - angle);
        polynomial = square * square + 360.0f;
        if (positiveSine) polynomial = polynomial * (30.0f - square) - 10080.0f;
        else polynomial = polynomial * (square - 30.0f) + 10080.0f;
        sine = polynomial * 0.0013888889f;
        return;
    }
    angle = 1.5707964f - angle;
    square = angle * angle;
    if (positiveCosine) polynomial = (6.0f - square) * 20.0f + square * square;
    else polynomial = (square - 6.0f) * 20.0f - square * square;
    cosine = polynomial * angle * 0.008333334f;
    if (positiveSine) sine = ((square * square + 360.0f) * (30.0f - square) - 10080.0f) * 0.0013888889f;
    else sine = ((square * square + 360.0f) * (square - 30.0f) + 10080.0f) * 0.0013888889f;
}

inline float OceanSourceSignFlip(float value) {
    uint32_t bits;
    std::memcpy(&bits, &value, sizeof bits);
    bits ^= 0x80000000u;
    std::memcpy(&value, &bits, sizeof value);
    return value;
}

// CE 11B7310; prepare once for each dimension. Arrays each have n/2 floats.
inline void OceanSourceTwiddle(uint32_t n, float* real, float* imag) {
    for (uint32_t i = 0; i < (n >> 1); ++i) {
        float sine, cosine;
        OceanSourceSinCos((float(i) * 6.2831855f) / float(n), sine, cosine);
        real[i] = cosine;
        imag[i] = OceanSourceSignFlip(sine);
    }
}

// CE 11BBDF0, original paired swap order; no normalization or float arithmetic.
inline void OceanSourceBitReverse(uint32_t n, float* real, float* imag, uint32_t stride) {
    const uint32_t half = n >> 1, quarter = n >> 2;
    uint32_t reversed = 0;
    const auto swapPair = [&](uint32_t a, uint32_t b) {
        std::swap(real[a * stride], real[b * stride]);
        std::swap(imag[a * stride], imag[b * stride]);
    };
    for (uint32_t i = 0; i < half; i += 2) {
        if (i < reversed) {
            swapPair(i, reversed);
            swapPair(i + half + 1, reversed + half + 1);
        }
        swapPair(i + half, reversed + 1);
        reversed ^= quarter;
        uint32_t bit = quarter;
        if (reversed < quarter) {
            do { bit >>= 1; reversed ^= bit; } while (reversed < bit);
        }
    }
}

// CE 11BAF70. Equivalent scalar traversal of its four-at-a-time loops;
// butterflies are visited in the same stage/block/within-block order.
// twiddle selection is performed by caller: n == Nz => 158/160, else 168/170.
inline void OceanSourceFFT(uint32_t n, uint32_t log2n, uint32_t stride,
                           float* real, float* imag,
                           const float* twiddleReal, const float* twiddleImag) {
    OceanSourceBitReverse(n, real, imag, stride);
    uint32_t block = 2, twiddleStep = n >> 1;
    for (uint32_t stage = 0; stage < log2n; ++stage) {
        const uint32_t half = block >> 1;
        for (uint32_t base = 0; base < n; base += block) {
            for (uint32_t i = 0; i < half; ++i) {
                const uint32_t a = (base + i) * stride;
                const uint32_t b = (base + i + half) * stride;
                const float ar = real[a], ai = imag[a];
                const float br = real[b], bi = imag[b];
                const float wr = twiddleReal[i * twiddleStep], wi = twiddleImag[i * twiddleStep];
                const float tr = wr * br - wi * bi;
                const float ti = wr * bi + wi * br;
                real[a] = tr + ar;
                imag[a] = ti + ai;
                real[b] = ar - tr;
                imag[b] = ai - ti;
            }
        }
        block *= 2;
        twiddleStep >>= 1;
    }
}


using NativeOceanFftCell=San14::Render::OceanSpectrumCell;
struct NativeOceanFftVertex {float x,y,z;uint16_t nx,nz;};
static_assert(sizeof(NativeOceanFftCell)==32&&sizeof(NativeOceanFftVertex)==16,"Original Ocean FFT storage");
struct NativeOceanFft {
    uint32_t nx=0,nz=0,workers=0,logNx=0,logNz=0,activeSide=0;
    float sizeX=0,sizeZ=0,cycle=0,choppy=0,phase=0;
    San14::Render::OceanGridSettings spectrumGrid;
    San14::Render::OceanWaveSettings waveSettings;
    std::optional<San14::Render::RenderRandomObject> random;
    std::vector<NativeOceanFftCell> cells;
    std::array<std::vector<float>,2> gaussian; // Drawn once per Ocean object, retained across current wave changes.
    std::array<std::vector<float>,10> spectra;
    std::array<std::vector<float>,2> twiddleX,twiddleZ;
    std::array<std::vector<NativeOceanFftVertex>,2> vertices;
    std::array<std::array<float,8>,2> bounds{}; // Max XYZ0, Min XYZ0, original E0/F0 order.
};
static uint16_t nativeOceanFftHalf(float value) {
    uint32_t bits;std::memcpy(&bits,&value,4);
    const uint16_t sign=uint16_t(bits>>16)&0x8000;
    const int exponent=int((bits>>23)&255)-0x70;
    // 9BD050 truncates; it does not use IEEE round-to-nearest conversion.
    if(exponent>31)return sign|0x7C00;
    if(exponent>=0)return sign|uint16_t(exponent*0x400)|uint16_t((bits&0x7FFFFF)>>13);
    if(exponent>-19)return sign|uint16_t(((bits&0x7FFFFF)|0x800000)>>((13-exponent)&31));
    return sign;
}
// Ordinary Ocean construction uses two configured workers and begins with
// side0 at phase0. Historical queued timing is not a resource declaration.
static NativeOceanFft prepareNativeOceanFft(const San14::Render::OceanGridSettings& grid,
    const San14::Render::OceanWaveSettings& wave,San14::Render::RenderRandomContext& randomContext,uint32_t startupSeed) {
    San14::Render::validateOceanGridSettings(grid);San14::Render::validateOceanWaveSettings(wave);
    NativeOceanFft fft;fft.workers=2;
    fft.nx=grid.columns;fft.nz=grid.rows;
    fft.sizeX=grid.sizeX;fft.sizeZ=grid.sizeZ;fft.cycle=grid.cycle;fft.choppy=wave.choppyFactor;
    fft.spectrumGrid=grid;fft.waveSettings=wave;
    fft.cells.resize(size_t(fft.nx)*fft.nz);
    for(auto& lane:fft.gaussian)lane.resize(fft.cells.size());
    fft.random.emplace(startupSeed);
    randomContext.fillGaussian(*fft.random,fft.gaussian[0],fft.gaussian[1]);
    San14::Render::constructOceanSpectrum(fft.cells,fft.gaussian,grid,wave,{true,true,true});
    for(auto& field:fft.spectra)field.resize(fft.cells.size());
    for(auto& side:fft.vertices)side.resize(size_t(fft.nx+3)*(fft.nz+3));
    for(uint32_t n=fft.nx;n>1;n>>=1)++fft.logNx;
    for(uint32_t n=fft.nz;n>1;n>>=1)++fft.logNz;
    for(auto& table:fft.twiddleX)table.resize(fft.nx/2);
    for(auto& table:fft.twiddleZ)table.resize(fft.nz/2);
    OceanSourceTwiddle(fft.nx,fft.twiddleX[0].data(),fft.twiddleX[1].data());
    OceanSourceTwiddle(fft.nz,fft.twiddleZ[0].data(),fft.twiddleZ[1].data());
    return fft;
}
static void applyNativeOceanWaveSettings(NativeOceanFft& fft,const San14::Render::OceanWaveSettings& wave) {
    San14::Render::validateOceanWaveSettings(wave);
    const auto changes=San14::Render::oceanSpectrumChanges(fft.spectrumGrid,fft.waveSettings,fft.spectrumGrid,wave);
    if(changes.initialSpectrum||changes.dispersion)
        San14::Render::constructOceanSpectrum(fft.cells,fft.gaussian,fft.spectrumGrid,wave,changes);
    fft.waveSettings=wave;fft.choppy=wave.choppyFactor;
}
// Time spectrum consumes constructed h0, modular mirrors and current dispersion.
static void nativeOceanFftTimeSpectrum(NativeOceanFft& fft,float phase) {
    fft.phase=phase;
    for(size_t i=0;i<fft.cells.size();++i) {
        const auto& c=fft.cells[i];float sine,cosine;
        OceanSourceSinCos(c.omega*phase,sine,cosine);
        const float real=(c.h0Real+c.mirrorReal)*cosine-(c.h0Imag+c.mirrorImag)*sine;
        const float imag=(c.h0Imag-c.mirrorImag)*cosine+(c.h0Real-c.mirrorReal)*sine;
        fft.spectra[0][i]=real;fft.spectra[1][i]=imag;
        fft.spectra[2][i]=-(imag*c.kx);fft.spectra[3][i]=real*c.kx;
        fft.spectra[4][i]=-(imag*c.kz);fft.spectra[5][i]=real*c.kz;
        if(c.kLength==0)for(uint32_t lane=6;lane<10;++lane)fft.spectra[lane][i]=0;
        else {
            fft.spectra[6][i]=(c.kx/c.kLength)*imag;fft.spectra[7][i]=-((c.kx/c.kLength)*real);
            fft.spectra[8][i]=(c.kz/c.kLength)*imag;fft.spectra[9][i]=-((c.kz/c.kLength)*real);
        }
    }
}
static void nativeOceanFftRows(NativeOceanFft& fft) {
    const auto& tables=fft.nx==fft.nz?fft.twiddleZ:fft.twiddleX;
    for(uint32_t row=0;row<fft.nz;++row)for(uint32_t pair=0;pair<5;++pair)
        OceanSourceFFT(fft.nx,fft.logNx,1,fft.spectra[pair*2].data()+size_t(row)*fft.nx,
            fft.spectra[pair*2+1].data()+size_t(row)*fft.nx,tables[0].data(),tables[1].data());
}
static void nativeOceanFftColumns(NativeOceanFft& fft) {
    for(uint32_t column=0;column<fft.nx;++column)for(uint32_t pair=0;pair<5;++pair)
        OceanSourceFFT(fft.nz,fft.logNz,fft.nx,fft.spectra[pair*2].data()+column,
            fft.spectra[pair*2+1].data()+column,fft.twiddleZ[0].data(),fft.twiddleZ[1].data());
}
static void nativeOceanFftVertices(NativeOceanFft& fft) {
    // 11BA8A0 signs centered spectra and applies the source choppy multiplier.
    for(uint32_t z=0;z<fft.nz;++z)for(uint32_t x=0;x<fft.nx;++x) {
        const size_t i=size_t(z)*fft.nx+x;const bool positive=((z+x)&1)==0;
        if(!positive)for(uint32_t lane:{0u,2u,4u})fft.spectra[lane][i]=OceanSourceSignFlip(fft.spectra[lane][i]);
        const float dx=fft.choppy*fft.spectra[6][i],dz=fft.choppy*fft.spectra[8][i];
        fft.spectra[6][i]=positive?dx:-dx;fft.spectra[8][i]=positive?dz:-dz;
    }
    auto& vertices=fft.vertices[fft.activeSide];auto& bounds=fft.bounds[fft.activeSide];
    const float maximum=std::numeric_limits<float>::max();bounds={-maximum,-maximum,-maximum,0,maximum,maximum,maximum,0};
    const uint32_t stride=fft.nx+3;
    const float halfX=fft.sizeX*.5f,halfZ=fft.sizeZ*.5f;
    auto extend=[&](const NativeOceanFftVertex& v){
        if(bounds[0]<=v.x)bounds[0]=v.x;if(bounds[1]<=v.y)bounds[1]=v.y;if(bounds[2]<=v.z)bounds[2]=v.z;
        if(v.x<=bounds[4])bounds[4]=v.x;if(v.y<=bounds[5])bounds[5]=v.y;if(v.z<=bounds[6])bounds[6]=v.z;
    };
    for(uint32_t z=0;z<fft.nz;++z) {
        const float baseZ=float(z)*(fft.sizeZ/float(fft.nz))-halfZ;
        const size_t first=size_t(z)*fft.nx,row=size_t(z+1)*stride;
        for(uint32_t x=0;x<fft.nx;++x) {
            const size_t i=first+x;auto& v=vertices[row+x+1];
            v={((float(x)*(fft.sizeX/float(fft.nx))-halfX)+fft.spectra[6][i]),fft.spectra[0][i],
                baseZ+fft.spectra[8][i],nativeOceanFftHalf(fft.spectra[2][i]),nativeOceanFftHalf(fft.spectra[4][i])};
            extend(v);
        }
        vertices[row]=vertices[row+1];vertices[row].y-=10.0f;
        vertices[row+fft.nx+1]=vertices[row+1];vertices[row+fft.nx+1].x+=fft.sizeX;
        if(bounds[0]<=vertices[row+fft.nx+1].x)bounds[0]=vertices[row+fft.nx+1].x;
        vertices[row+fft.nx+2]=vertices[row+fft.nx+1];vertices[row+fft.nx+2].x+=0.0f;vertices[row+fft.nx+2].y-=10.0f;
    }
    // 11B9A50 periodic last row; only source interior-column Z enters maxZ.
    const size_t periodic=size_t(fft.nz+1)*stride;
    for(uint32_t x=0;x<stride;++x) {
        vertices[periodic+x]=vertices[stride+x];vertices[periodic+x].z+=fft.sizeZ;
        if(x>0&&x<=fft.nx+1&&bounds[2]<=vertices[periodic+x].z)bounds[2]=vertices[periodic+x].z;
    }
    vertices[periodic+stride-1].x+=0.0f;
    // 11B9550 / 11BA010: top/bottom skirts duplicate corner interior
    // columns, lowering exactly ten; corners are not lowered twice.
    const size_t bottom=size_t(fft.nz+2)*stride;
    for(uint32_t x=0;x<stride;++x) {
        const uint32_t from=x==0?1:x==stride-1?stride-2:x;
        vertices[x]=vertices[stride+from];vertices[x].y-=10.0f;
        vertices[bottom+x]=vertices[periodic+from];vertices[bottom+x].y-=10.0f;
        if(x>=stride-2){vertices[x].x+=0.0f;vertices[bottom+x].x+=0.0f;}
        vertices[bottom+x].z+=0.0f;
    }
}
static void realizeNativeOceanFft(NativeOceanFft& fft,float sourcePhase) {
    nativeOceanFftTimeSpectrum(fft,sourcePhase);
    nativeOceanFftRows(fft);nativeOceanFftColumns(fft);nativeOceanFftVertices(fft);
}

// Source1184320 schedule is completed-job publication BEFORE time advances.
// CPU jobs run synchronously here; publication order and the two sides remain.
struct NativeOceanFftSchedule {
    NativeOceanFft fft;
    float publishedPhase=0;
    uint32_t publishedSide=0;
    std::array<float,8> publishedBounds{};
    bool completedJob=false;
};
static NativeOceanFftSchedule prepareNativeOceanFftSchedule(const San14::Render::OceanGridSettings& grid,
    const San14::Render::OceanWaveSettings& wave,San14::Render::RenderRandomContext& randomContext,uint32_t startupSeed) {
    NativeOceanFftSchedule state;state.fft=prepareNativeOceanFft(grid,wave,randomContext,startupSeed);
    // Complete the fresh phase0 job before publishing any GPU geometry.
    // This coherent constructor does not replay original startup thread races.
    realizeNativeOceanFft(state.fft,state.fft.phase);state.completedJob=true;
    return state;
}
static const std::vector<NativeOceanFftVertex>& publishNativeOceanFftCompleted(NativeOceanFftSchedule& state) {
    // Wait19C is already satisfied by serial five-stage completion. 1183A40
    // flipsD8 then publishes C8/D0[D8 xor1], preserving the completed side.
    state.publishedSide=state.fft.activeSide;state.publishedPhase=state.fft.phase;
    state.publishedBounds=state.fft.bounds[state.publishedSide];state.fft.activeSide^=1;
    state.completedJob=false;
    return state.fft.vertices[state.publishedSide];
}
static void queueNativeOceanFft(NativeOceanFftSchedule& state,double currentSourceGridTime) {
    // Consume the same grid20 advanced by the CB owner; no second clock owner.
    const float nextPhase=float(std::fmod(currentSourceGridTime,double(state.fft.cycle)));
    realizeNativeOceanFft(state.fft,nextPhase);state.completedJob=true;
}
#ifdef _MSC_VER
#pragma float_control(pop)
#endif
