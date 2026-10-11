#pragma once
#include <algorithm>
#include <array>
#include <bit>
#include <cmath>
#include <cstdint>
#include <vector>
#include <limits>
#include <stdexcept>

namespace San14::Render {
struct OceanGridSettings {
    std::uint32_t columns{}, rows{};
    float sizeX{}, sizeZ{}, cycle{};
    std::uint32_t declaredLodCount{};
    float maximumDistance{};
};
struct OceanSurfaceState {
    float waterHeight{}, cutoffDistance{}, directionX{}, directionZ{}, displacement{};
};
struct OceanIndexRange { std::uint32_t start{}, count{}; };
struct OceanVisibilityGrid {
    float waterY{}, cellX{}, cellZ{}, cutoff{}, maxDistance{}, directX{}, directZ{}, displacement{};
    std::uint32_t lodCount{}, capacity{};
    std::vector<float> lodDistances;
    std::vector<OceanIndexRange> ranges;
};
struct OceanBubbleUv { std::uint16_t x{}, z{}; };
static_assert(sizeof(OceanBubbleUv)==4);

// Stitching uses interior lattice coordinates; a one-vertex skirt surrounds it.
// Each fan deliberately retains its degenerate vertices and append order.
class OceanLodStripBuilder {
public:
    OceanLodStripBuilder(std::uint32_t columns,std::uint32_t rows,std::vector<std::uint32_t>& indices)
        :columns_(columns),rows_(rows),stride_(columns+3),indices_(indices){}
    void tile(unsigned centerLod,unsigned eastLod,unsigned northLod) {
        const unsigned step=1u<<centerLod,east=1u<<eastLod,north=1u<<northLod;
        const bool stitchEast=east!=step,stitchNorth=north!=step;
        regularRows(columns_-(stitchEast?step:0),rows_-(stitchNorth?step:0),step);
        if(stitchNorth) {
            const unsigned end=columns_-(stitchEast?std::max(step,north):0);
            for(unsigned x=0;x<end;x+=std::max(step,north))northFan(x,rows_-step,step,north);
        }
        if(stitchEast) {
            const unsigned end=rows_-(stitchNorth?std::max(step,east):0);
            for(unsigned z=0;z<end;z+=std::max(step,east))eastFan(columns_-step,z,step,east);
        }
        if(stitchEast&&stitchNorth)corner(step,east,north);
        skirt(step,east,north);
    }
    void extraQuads() {
        for(unsigned z=0;z<rows_+2;++z)for(unsigned x=0;x<columns_+2;++x) {
            if((x==0||x==columns_+1)&&(z==0||z==rows_+1))continue;
            quad(raw(x,z),raw(x,z+1),raw(x+1,z+1),raw(x+1,z));
        }
    }
private:
    unsigned raw(unsigned x,unsigned z)const { return z*stride_+x; }
    unsigned vertex(unsigned x,unsigned z)const { return raw(x+1,z+1); }
    void append(unsigned index) { indices_.push_back(index); }
    void quad(unsigned a,unsigned b,unsigned c,unsigned d) {
        append(a);append(b);append(c);append(d);
    }
    void fan(unsigned a,unsigned b,unsigned anchor) { quad(a,a,b,anchor); }
    void regularRows(unsigned endX,unsigned endZ,unsigned step) {
        for(unsigned z=0;z<endZ;z+=step) {
            if(z)append(vertex(0,z));
            for(unsigned x=0;x<=endX;x+=step) {
                append(vertex(x,z));append(vertex(x,z+step));
            }
            append(vertex(endX,z+step));
        }
    }
    void northFan(unsigned x,unsigned z,unsigned step,unsigned north,bool omitFirst=false) {
        if(north>step) {
            for(unsigned t=north-(omitFirst?step:0);t;t-=step)
                fan(vertex(x+t,z),vertex(x+t-step,z),vertex(x+(t>north/2?north:0),z+step));
            quad(vertex(x,z+step),vertex(x+north/2,z),vertex(x+north,z+step),vertex(x+north,z+step));
        } else {
            for(unsigned t=0;t<step;t+=north)
                fan(vertex(x+t,z+step),vertex(x+t+north,z+step),vertex(x+(t<step/2?0:step),z));
            quad(vertex(x+step,z),vertex(x+step/2,z+step),vertex(x,z),vertex(x,z));
        }
    }
    void eastFan(unsigned x,unsigned z,unsigned step,unsigned east,bool omitLast=false) {
        if(east>step) {
            for(unsigned t=0;t<east-(omitLast?step:0);t+=step)
                fan(vertex(x,z+t),vertex(x,z+t+step),vertex(x+step,z+(t<east/2?0:east)));
            quad(vertex(x+step,z+east),vertex(x,z+east/2),vertex(x+step,z),vertex(x+step,z));
        } else {
            for(unsigned t=step;t;t-=east)
                fan(vertex(x+step,z+t),vertex(x+step,z+t-east),vertex(x,z+(t>step/2?step:0)));
            quad(vertex(x,z),vertex(x+step,z+step/2),vertex(x,z+step),vertex(x,z+step));
        }
    }
    void corner(unsigned step,unsigned east,unsigned north) {
        const unsigned x=columns_-step,z=rows_-step;
        if(east>step)eastFan(x,rows_-east,step,east,true);
        if(north>step)northFan(columns_-north,z,step,north,true);
        const unsigned anchor=vertex(x,z);
        if(north<step) {
            for(unsigned t=0;t<step;t+=north)
                fan(vertex(x+t,z+step),vertex(x+t+north,z+step),anchor);
        }
        if(east<step) {
            for(unsigned t=step;t;t-=east) {
                const unsigned index=vertex(x+step,z+t);
                quad(index+(north<step?1:0),index,vertex(x+step,z+t-east),anchor);
            }
        }
        if(east<step||north<step) { append(anchor);append(anchor); }
    }
    void skirt(unsigned step,unsigned east,unsigned north) {
        append(1);
        for(unsigned x=1;x<=columns_+1;x+=step) { append(raw(x,0));append(raw(x,1)); }
        append(columns_*2+4);append(raw(1,rows_+1));
        for(unsigned x=1;x<=columns_+1;x+=north) { append(raw(x,rows_+1));append(raw(x,rows_+2)); }
        append(raw(columns_+1,rows_+2));append(raw(1,1));
        for(unsigned z=1;z<=rows_+1;z+=step) { append(raw(1,z));append(raw(0,z)); }
        append(raw(0,rows_+1));append(columns_*2+5);
        for(unsigned z=1;z<=rows_+1;z+=east) { append(raw(columns_+2,z));append(raw(columns_+1,z)); }
    }
    unsigned columns_,rows_,stride_;
    std::vector<std::uint32_t>& indices_;
};

inline std::uint16_t packOceanUvHalf(float value) {
    const auto bits=std::bit_cast<std::uint32_t>(value);
    const auto sign=std::uint16_t((bits>>16)&0x8000u);
    const int exponent=int((bits>>23)&255)-0x70;
    if(exponent>31)return std::uint16_t(sign|0x7C00);
    if(exponent>=0)return std::uint16_t(sign|std::uint16_t(exponent*0x400)|std::uint16_t((bits&0x7FFFFF)>>13));
    if(exponent>-19)return std::uint16_t(sign|std::uint16_t(((bits&0x7FFFFF)|0x800000)>>((13-exponent)&31)));
    return sign;
}
struct OceanGrid {
    OceanGridSettings settings;
    OceanVisibilityGrid visibility;
    std::vector<OceanBubbleUv> bubbleUvs;
    std::vector<std::uint32_t> indices;
    std::array<std::uint16_t,4> farIndices{0,2,1,3};

    std::size_t vertexCount()const { return std::size_t(settings.columns+3)*(settings.rows+3); }
    bool usesWideIndices()const { return vertexCount()>=65536; }
};
// The retained FFT and strip renderer support power-of-two dimensions with
// six complete LOD levels, and an unsigned 16-bit index stream.
inline void validateOceanGridSettings(const OceanGridSettings& settings) {
    auto supportedDimension=[](std::uint32_t value) {
        return value>=32 && value<=2048 && std::has_single_bit(value);
    };
    if(!supportedDimension(settings.columns)||!supportedDimension(settings.rows))
        throw std::runtime_error("Ocean grid requires power-of-two dimensions of at least 32");
    if(std::size_t(settings.columns+3)*(settings.rows+3)>=65536)
        throw std::runtime_error("Ocean grid requires unsupported 32-bit index rendering");
    if(!std::isfinite(settings.sizeX)||!std::isfinite(settings.sizeZ)||
       !std::isfinite(settings.cycle)||!std::isfinite(settings.maximumDistance)||
       settings.sizeX<=0||settings.sizeZ<=0||settings.cycle<=0||settings.maximumDistance<=0)
        throw std::runtime_error("Ocean grid size, cycle and maximum distance must be positive and finite");
    const float radius=std::ceil(settings.maximumDistance/std::min(settings.sizeX,settings.sizeZ));
    if(!std::isfinite(radius)||radius>8192)
        throw std::runtime_error("Ocean grid instance capacity exceeds the supported buffer extent");
}
inline OceanGrid constructOceanGrid(const OceanGridSettings& settings,const OceanSurfaceState& surface) {
    validateOceanGridSettings(settings);
    if(!std::isfinite(surface.waterHeight)||!std::isfinite(surface.cutoffDistance)||
       !std::isfinite(surface.directionX)||!std::isfinite(surface.directionZ)||
       !std::isfinite(surface.displacement)||surface.cutoffDistance<0)
        throw std::runtime_error("Ocean current surface inputs must be finite");
    OceanGrid grid;grid.settings=settings;
    auto& visible=grid.visibility;
    visible.waterY=surface.waterHeight;visible.cellX=settings.sizeX;visible.cellZ=settings.sizeZ;
    visible.cutoff=surface.cutoffDistance;visible.maxDistance=settings.maximumDistance;
    visible.directX=surface.directionX;visible.directZ=surface.directionZ;visible.displacement=surface.displacement;
    visible.lodCount=6;
    const auto radius=std::uint32_t(std::ceil(settings.maximumDistance/std::min(settings.sizeX,settings.sizeZ)));
    visible.capacity=std::max(30u,5*radius*radius);
    visible.lodDistances.resize(visible.lodCount);
    for(unsigned level=1;level<visible.lodCount;++level)
        visible.lodDistances[level]=float(level)*(settings.maximumDistance/float(visible.lodCount))+
            std::max(settings.sizeX,settings.sizeZ)*0.5f;
    OceanLodStripBuilder builder(settings.columns,settings.rows,grid.indices);
    for(unsigned level=0;level<visible.lodCount;++level)
        for(unsigned east=0;east<visible.lodCount;++east)
            for(unsigned north=0;north<visible.lodCount;++north) {
                const auto start=std::uint32_t(grid.indices.size());
                builder.tile(level,east,north);
                visible.ranges.push_back({start,std::uint32_t(grid.indices.size())-start});
            }
    const auto start=std::uint32_t(grid.indices.size());builder.extraQuads();
    visible.ranges.push_back({start,std::uint32_t(grid.indices.size())-start});
    grid.bubbleUvs.reserve(grid.vertexCount());
    for(unsigned z=0;z<settings.rows+3;++z)for(unsigned x=0;x<settings.columns+3;++x)
        grid.bubbleUvs.push_back({packOceanUvHalf(std::clamp((float(x)-1)/float(settings.columns),0.f,1.f)),
            packOceanUvHalf(std::clamp((float(z)-1)/float(settings.rows),0.f,1.f))});
    return grid;
}
} // namespace San14::Render
