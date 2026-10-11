// Finite original Main Ocean117F010/11B8320 CPU geometry publication.
// Held source profile2/perspective, ordinary pass0, empty retained ripple population.
#pragma once
#include "WaterCameraFrame.h"
#include "OceanGrid.h"
#include "../../../Camera/CameraState.h"
#include "../../../Camera/CameraMatrix.h"
#include <limits>

namespace source_water_geometry {
using source_water_camera::A;using source_water_camera::S;using source_water_camera::M;using source_water_camera::D;
using Matrix=source_matrix::Matrix;
using V4=source_camera::V4;
using Range=San14::Render::OceanIndexRange;
using Profile=San14::Render::OceanVisibilityGrid;
struct Instance {float x,z,ordinal;};
static_assert(sizeof(Instance)==12,"Original Ocean instance ABI");
struct Vertex {float x,y,z;uint16_t normalX,normalZ;};
static_assert(sizeof(Vertex)==16,"Original float3 + half2 far vertex");
struct Strip {uint32_t flat,indexCount,startIndex,baseVertex,instanceOffset,rangeOrdinal;};
struct Frame {
    std::vector<Instance> instances; // Includes one final source0/0/0 sentinel.
    std::vector<Strip> strips;
    std::array<Vertex,4> farVertices{};
    std::array<float,4> waveClip{},farClip{};
    std::array<V4,8> corners{};
    std::array<float,4> farBounds{};
    bool waterPlaneVisible=false;
};
// 1091250 intersects each of eight exact plane triples using3F6EB0.
// It does not inverse-project a substituted NDC corner convention.
inline std::array<V4,8> corners(const std::array<source_camera::Plane,6>& planes) {
    constexpr unsigned triples[8][3]{{4,0,3},{4,1,3},{5,1,3},{5,0,3},{4,0,2},{4,1,2},{5,1,2},{5,0,2}};
    std::array<V4,8> out{};
    for(unsigned i=0;i<8;i++) {
        auto a=planes[triples[i][0]].equation,b=planes[triples[i][1]].equation,c=planes[triples[i][2]].equation;
        Matrix inverse=source_matrix::inverse({a.X,b.X,c.X,0,a.Y,b.Y,c.Y,0,a.Z,b.Z,c.Z,0,0,0,0,1});
        float rhs[]{source_camera::flip(a.W),source_camera::flip(b.W),source_camera::flip(c.W)};
        float* target=&out[i].X;
        for(unsigned k=0;k<4;k++)target[k]=A(A(A(M(inverse[4+k],rhs[1]),M(inverse[k],rhs[0])),M(inverse[8+k],rhs[2])),inverse[12+k]);
    }
    return out;
}
// Original plane-distance arithmetic retains cornerW (not all sourceW are1).
inline bool intersectWater(float waterY,const std::array<V4,8>& c,std::array<float,4>& bounds) {
    std::array<float,8> distance{};std::vector<V4> intersections;
    float d=source_camera::flip(A(A(waterY,0),0));
    for(unsigned i=0;i<8;i++) {
        distance[i]=A(A(A(M(c[i].X,0),c[i].Y),M(c[i].Z,0)),M(d,c[i].W));
        if(distance[i]==0)intersections.push_back(c[i]);
    }
    constexpr unsigned edges[12][2]{{0,1},{1,5},{5,4},{4,0},{3,2},{2,6},{6,7},{7,3},{0,3},{1,2},{5,6},{4,7}};
    for(const auto& e:edges)if(M(distance[e[0]],distance[e[1]])<0) {
        auto a=c[e[0]],b=c[e[1]];float t=source_camera::flip(D(distance[e[0]],S(distance[e[1]],distance[e[0]]))),u=S(1,t);
        intersections.push_back({A(M(u,a.X),M(t,b.X)),A(M(u,a.Y),M(t,b.Y)),A(M(u,a.Z),M(t,b.Z)),a.W});
    }
    if(intersections.size()<3)return false;
    float minX=std::numeric_limits<float>::max(),minZ=minX,maxX=-minX,maxZ=maxX;
    for(auto v:intersections) {if(v.X<=minX)minX=v.X;if(v.Z<=minZ)minZ=v.Z;if(maxX<=v.X)maxX=v.X;if(maxZ<=v.Z)maxZ=v.Z;}
    bounds={minX,minZ,maxX,maxZ};return true;
}
inline uint32_t lod(const Profile& p,float depth) {
    uint32_t value=p.lodCount-1;while(value&&!(p.lodDistances[value]<depth))value--;return value;
}
inline Frame prepareCurrent(const Profile& p,const source_camera::CameraTransform& camera,const std::array<float,8>& completedWaveBounds) {
    const bool reverse=camera.optics.reversedDepth;
    if(camera.optics.projectionKind==source_camera::ProjectionKind::Orthographic)throw std::runtime_error("Ocean current source subset requires perspective");
    Frame out;auto combined=camera.worldToClip;auto fullPlanes=source_camera::makePlanes(combined,reverse);
    out.corners=corners(fullPlanes);out.waterPlaneVisible=intersectWater(p.waterY,out.corners,out.farBounds);
    if(!out.waterPlaneVisible)return out;
    float eyeX=camera.eye.X,eyeY=camera.eye.Y,eyeZ=camera.eye.Z;
    float dx=S(camera.target.X,eyeX),dy=S(camera.target.Y,eyeY),dz=S(camera.target.Z,eyeZ);
    // Original117F010 X/Y/Z normalization order.
    float inv=D(1,std::sqrt(A(A(M(dx,dx),M(dy,dy)),M(dz,dz))));dx=M(dx,inv);dy=M(dy,inv);dz=M(dz,inv);
    float halfX=M(p.cellX,.5f),halfZ=M(p.cellZ,.5f),horizontal=std::sqrt(S(1,M(dy,dy)));
    float cutoffRange=D(p.cutoff,std::max(horizontal,1.1920928955078125e-5f));
    float depth=A(A(M(M(dx,cutoffRange),dx),M(source_camera::flip(S(eyeY,p.waterY)),dy)),M(M(dz,cutoffRange),dz));
    float nearClip=camera.optics.nearPlane,farClip=camera.optics.farPlane,clip;
    if(reverse)clip=D(M(S(depth,farClip),nearClip),M(S(nearClip,farClip),depth));
    else clip=D(M(S(depth,nearClip),farClip),M(S(farClip,nearClip),depth));
    out.waveClip={0,0,reverse?1.f:-1.f,reverse?source_camera::flip(S(clip,M(clip,.2f))):A(M(S(1,clip),.2f),clip)};
    out.farClip=out.waveClip;
    if(nearClip<depth)out.farClip={0,0,reverse?-1.f:1.f,reverse?clip:source_camera::flip(clip)};

    // Source clone forced camera preparation retains current view, recomputes
    // perspective only when its explicit reduced far bound changes.
    float reducedFar=farClip;
    if(dx==0&&dz==0)reducedFar=M(depth,1.1f);
    else reducedFar=std::min(reducedFar,M(A(A(p.maxDistance,p.cellX),p.cellZ),1.1f));
    Matrix reduced=combined;
    if(reducedFar!=farClip) {
        auto projection=camera.projection;float na=reverse?reducedFar:nearClip,fa=reverse?nearClip:reducedFar;
        projection[10]=D(fa,S(na,fa));projection[14]=M(projection[10],na);
        reduced=source_camera::compose(camera.view,projection);
    }
    std::array<float,4> visibleBounds{};auto reducedPlanes=source_camera::makePlanes(reduced,reverse);
    if(!intersectWater(p.waterY,corners(reducedPlanes),visibleBounds))visibleBounds={0,0,0,0};
    float startX=S(M(std::floor(D(visibleBounds[0],p.cellX)),p.cellX),p.cellX);
    float startZ=S(M(std::floor(D(visibleBounds[1],p.cellZ)),p.cellZ),p.cellZ);
    int nx=static_cast<int>(std::ceil(D(S(A(p.cellX,visibleBounds[2]),startX),p.cellX)));
    int nz=static_cast<int>(std::ceil(D(S(A(p.cellZ,visibleBounds[3]),startZ),p.cellZ)));
    float stepX=p.cellX,stepZ=p.cellZ;
    if(S(camera.target.X,eyeX)<0){startX=A(M(float(nx-1),stepX),startX);stepX=source_camera::flip(stepX);}
    if(S(camera.target.Z,eyeZ)<0){startZ=A(M(float(nz-1),stepZ),startZ);stepZ=source_camera::flip(stepZ);}
    float planarInv=D(1,std::sqrt(A(A(M(dx,dx),0),M(dz,dz)))),planarX=M(planarInv,dx),planarZ=M(planarInv,dz);
    auto bounds=completedWaveBounds;
    for(unsigned axis=0;axis<3;axis++)if(bounds[axis+4]==bounds[axis])bounds[axis+4]=S(bounds[axis+4],.0011920928955078125f);
    for(int xi=0;xi<nx;xi++) {
        float x=A(startX,halfX),zBase=startZ;
        for(int zi=0;zi<nz;zi++) {
            float z=A(zBase,halfZ);
            source_camera::V3 hi{A(bounds[0],x),A(bounds[1],p.waterY),A(bounds[2],z)};
            source_camera::V3 lo{A(bounds[4],x),A(bounds[5],p.waterY),A(bounds[6],z)};
            float ex=M(M(p.directX,p.displacement),1000),ez=M(M(p.directZ,p.displacement),1000);
            if(p.directX<=0)lo.X=A(lo.X,ex);else hi.X=A(hi.X,ex);
            if(p.directZ<=0)lo.Z=A(lo.Z,ez);else hi.Z=A(hi.Z,ez);
            float beyond=S(A(A(M(S(x,eyeX),planarX),M(0,0)),M(S(z,eyeZ),planarZ)),p.cutoff);
            if(source_camera::intersects(fullPlanes,lo,hi)&&!(beyond>0&&std::sqrt(A(M(halfZ,halfZ),M(halfX,halfX)))<=beyond)) {
                auto distance=[&](float px,float pz){return A(A(M(S(p.waterY,eyeY),dy),M(S(px,eyeX),dx)),M(S(pz,eyeZ),dz));};
                uint32_t center=lod(p,distance(x,z)),plusX=lod(p,distance(A(x,p.cellX),z)),plusZ=lod(p,distance(x,A(z,p.cellZ)));
                uint32_t ordinal=(center*p.lodCount+plusX)*p.lodCount+plusZ;auto range=p.ranges[ordinal];
                uint32_t instance=uint32_t(out.instances.size());out.instances.push_back({x,z,float(instance)});
                out.strips.push_back({0,range.count,range.start,0,instance*12,ordinal});
                if(out.instances.size()+2>=p.capacity)break;
            }
            zBase=A(zBase,stepZ);
        }
        if(out.instances.size()+1>=p.capacity)break;startX=A(startX,stepX);
    }
    // Current ordinary branch's empty ripple owner adds no special index range.
    out.instances.push_back({0,0,0});out.strips.push_back({1,4,0,0,uint32_t((out.instances.size()-1)*12),0});
    out.farVertices={Vertex{out.farBounds[0],0,out.farBounds[1],0,0},Vertex{out.farBounds[2],0,out.farBounds[1],0,0},
        Vertex{out.farBounds[0],0,out.farBounds[3],0,0},Vertex{out.farBounds[2],0,out.farBounds[3],0,0}};
    return out;
}
// Original slot0 plane changes with the current117F010 clip threshold. Other
// five source reset planes remain untouched in the complete VS CBs.
inline void writeCurrentClips(source_water_camera::Packets& packets,const Frame& frame) {
    if(!frame.waterPlaneVisible)return;
    source_water_camera::copy(packets.constants[0],80,frame.waveClip.data(),16);
    source_water_camera::copy(packets.constants[2],80,frame.farClip.data(),16);
}
} // namespace source_water_geometry
