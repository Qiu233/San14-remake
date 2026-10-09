#pragma once
// Current camera membership and real Hi/Mid bank slices. The World renderer's
// scanning / primitive queue publication remains its own producer.
#include "GroundDetailPacketReader.hpp"
#include "../../Camera/CameraState.h"
#include "../../Camera/CameraMatrix.h"
#include <cfloat>

namespace ground_detail_source {
using source_camera::A;using source_camera::S;using source_camera::M;using source_camera::D;
using source_camera::V3;
struct CurrentView {
    V3 eye,minimum,maximum;
    std::array<source_camera::Plane,6> planes;
    uint32_t cullingType,scanningType;
};
inline CurrentView prepare(const source_camera::Frame& camera,uint32_t cullingType,uint32_t scanningType) {
    CurrentView v{{camera.eye.X,camera.eye.Y,camera.eye.Z},{FLT_MAX,FLT_MAX,FLT_MAX},{-FLT_MAX,-FLT_MAX,-FLT_MAX},camera.planes,cullingType,scanningType};
    constexpr int triples[8][3]={{4,0,3},{4,1,3},{5,1,3},{5,0,3},{4,0,2},{4,1,2},{5,1,2},{5,0,2}};
    for(auto& t:triples){auto a=v.planes[t[0]].equation,b=v.planes[t[1]].equation,c=v.planes[t[2]].equation;
        source_matrix::Matrix normals={a.X,b.X,c.X,0,a.Y,b.Y,c.Y,0,a.Z,b.Z,c.Z,0,0,0,0,1};
        auto inverse=source_matrix::inverse(normals);V3 p{A(A(A(M(inverse[4],-b.W),M(inverse[0],-a.W)),M(inverse[8],-c.W)),inverse[12]),
            A(A(A(M(inverse[5],-b.W),M(inverse[1],-a.W)),M(inverse[9],-c.W)),inverse[13]),
            A(A(A(M(inverse[6],-b.W),M(inverse[2],-a.W)),M(inverse[10],-c.W)),inverse[14])};
        v.minimum={std::min(v.minimum.X,p.X),std::min(v.minimum.Y,p.Y),std::min(v.minimum.Z,p.Z)};
        v.maximum={std::max(v.maximum.X,p.X),std::max(v.maximum.Y,p.Y),std::max(v.maximum.Z,p.Z)};
    }return v;
}
struct Slice { const Bank* source;uint32_t lod,start,count; };
inline std::vector<Slice> select(const Packet& p,const CurrentView& v) {
    if(v.cullingType!=1||v.scanningType!=0)throw std::runtime_error("Procedural finite current view requires source culling1/scanning0.");
    std::vector<Slice> result;const auto& d=p.declaration;float width=float(d[0]/d[6]);
    auto index=[&](float x,unsigned n){return std::clamp(int(x),0,int(n));};
    int ax0=index(D(S(S(v.eye.X,float(d[11])),float(d[2])),width),d[6]),ax1=index(D(S(A(v.eye.X,float(d[11])),float(d[2])),width),d[6]);
    int az0=index(D(S(S(v.eye.Z,float(d[11])),float(d[3])),width),d[7]),az1=index(D(S(A(v.eye.Z,float(d[11])),float(d[3])),width),d[7]);
    int x0=std::max(index(D(S(S(v.eye.X,float(d[10])),float(d[2])),width),d[6]),int(D(S(v.minimum.X,float(d[2])),width)));
    int z0=std::max(index(D(S(S(v.eye.Z,float(d[10])),float(d[3])),width),d[7]),int(D(S(v.minimum.Z,float(d[3])),width)));
    int x1=std::min(index(D(S(A(v.eye.X,float(d[10])),float(d[2])),width),d[6]),int(std::ceil(D(S(v.maximum.X,float(d[2])),width))));
    int z1=std::min(index(D(S(A(v.eye.Z,float(d[10])),float(d[3])),width),d[7]),int(std::ceil(D(S(v.maximum.Z,float(d[3])),width))));
    for(const auto& block:p.blocks){const auto& bs=block.bankIndices;int x=p.banks[bs[0]].x,z=p.banks[bs[0]].z;
        bool absolute=x>=ax0&&x<ax1&&z>=az0&&z<az1;
        if(!absolute&&(x<x0||x>=x1||z<z0||z>=z1))continue;
        float minY=FLT_MAX,maxY=FLT_MIN;
        for(auto bankIndex:bs){const Bank* b=&p.banks[bankIndex];const auto& bound=p.templates[b->templateIndex].sourceBounds;float range=S(bound[4],bound[1]);
            float fraction=bound[4]>=0&&bound[1]>=0?1:bound[4]<0&&bound[1]<0?0:D(bound[4],range);
            for(size_t i=0;i<b->instances.size();i++){const auto& r=b->instances[i].row;
                float scale=std::sqrt(A(A(M(r[1],r[1]),M(r[5],r[5])),M(r[9],r[9])));
                bool four=i<(b->instances.size()/4)*4;
                float high=A(four?M(M(fraction,range),scale):M(M(scale,range),fraction),r[7]);
                float low=S(r[7],four?M(M(S(1,fraction),range),scale):M(M(scale,range),S(1,fraction)));
                if(maxY<high)maxY=high;if(low<minY)minY=low;
            }
        }
        if(std::fabs(maxY)<=1.1920928955078125e-5f||minY==FLT_MAX)continue;
        V3 lo{A(M(float(x),width),float(d[2])),minY,A(M(float(z),width),float(d[3]))},hi{A(lo.X,width),maxY,A(lo.Z,width)};
        bool absoluteHit=absolute&&hi.X>=S(v.eye.X,float(d[11]))&&lo.X<=A(v.eye.X,float(d[11]))&&hi.Z>=S(v.eye.Z,float(d[11]))&&
            lo.Z<=A(v.eye.Z,float(d[11]))&&hi.Y>=S(v.eye.Y,float(d[12]))&&lo.Y<=A(v.eye.Y,float(d[12]));
        if(!absoluteHit&&!source_camera::intersects(v.planes,lo,hi))continue;
        float dx=S(A(M(A(float(x),.5f),width),float(d[2])),v.eye.X),dz=S(A(M(A(float(z),.5f),width),float(d[3])),v.eye.Z);
        float distance=std::sqrt(A(A(M(dx,dx),0),M(dz,dz)));
        for(auto bankIndex:bs){const Bank* b=&p.banks[bankIndex];const auto& e=p.elements[b->element];const auto& t=p.templates[b->templateIndex];uint32_t n=uint32_t(b->instances.size()),h=n,m=0;
            bool mid=!t.lodDraws[1].empty()&&(e.flags&8)!=0;
            if(mid&&distance>=e.lodDistances[1]){h=0;m=n;}
            else if(mid&&distance>=e.lodDistances[0]){float fraction=D(S(distance,e.lodDistances[0]),S(e.lodDistances[1],e.lodDistances[0]));
                m=uint32_t(std::floor(M(float(n),fraction)));h=uint32_t(std::ceil(M(S(1,fraction),float(n))));m-=h+m-n;}
            if(h)result.push_back({b,0,0,h});if(m)result.push_back({b,1,h,m});
        }
    }return result;
}
}
