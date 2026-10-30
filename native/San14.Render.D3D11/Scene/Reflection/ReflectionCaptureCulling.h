#pragma once
#include "ReflectionSceneFrame.h"
#include "../../Camera/CameraTangent.h"
#include "../../Passes/Shadows/ShadowCameraFrame.h"
#include <limits>
namespace San14::Render::Reflection {
// The same source convex kernel used by compact shadows, with capture's actual
// top-down camera, ordered work/frustum intersection and six accumulated depths.
class CaptureCulling {
    using V=source_camera::V4;
    using B=source_camera::Matrix;
    V center_,lo_,hi_;
    std::uint32_t candidates_{},visible_{};
    float far_{};
    std::array<float,6> near_,farDepth_,lastDistances_{};
public:
    CaptureCulling(const FieldDefinition& field,V center,const source_camera::CameraTransform& current):center_(center) {
        using namespace source_shadow_camera;
        auto camera=current;camera.eye=center;camera.eye.Y=A(A(field.viewRange,field.viewRange),center.Y);
        camera.target=center;camera.unrolledView=source_camera::lookTo(camera.eye,camera.target,camera.up);camera.view=camera.unrolledView;
        if(camera.optics.roll!=0){auto t=source_camera::sinCos(source_camera::flip(camera.optics.roll));
            for(int i=0;i<4;i++){float x=camera.unrolledView[i*4],y=camera.unrolledView[i*4+1];
                camera.view[i*4]=S(M(x,t[1]),M(y,t[0]));camera.view[i*4+1]=A(M(x,t[0]),M(y,t[1]));}}
        camera.optics.farPlane=A(M(field.viewRange,3.5f),100.f);camera.optics.aspectRatio=1;
        const float tangent=source_camera::tangentSse(std::bit_cast<float>(0x3FC90FDBu)*.5f);
        Profile p;p.tangent=tangent;
        const auto work=workMatrix(p,camera);
        auto forward=normalize(sub(camera.target,camera.eye));forward.W=S(camera.target.W,camera.eye.W);
        const auto view=lightView(camera.eye,forward,camera.up);
        B basis{view[0],view[4],view[8],0,view[1],view[5],view[9],0,forward.X,forward.Y,forward.Z,forward.W,
            camera.eye.X,camera.eye.Y,camera.eye.Z,camera.eye.W};
        float limit=std::bit_cast<float>(0x7722B3DBu);lo_={limit,limit,limit,1};hi_={-limit,-limit,-limit,1};
        for(const auto& v:intersect(frustum(basis,camera.optics.nearPlane,camera.optics.farPlane,tangent,tangent),workBox(work))){
            lo_={min(lo_.X,v.X),min(lo_.Y,v.Y),min(lo_.Z,v.Z),1};hi_={max(hi_.X,v.X),max(hi_.Y,v.Y),max(hi_.Z,v.Z),1};}
        const float radius=std::sqrt(M(field.viewRange,field.viewRange));
        lo_={max(-radius,S(lo_.X,center.X)),max(-radius,S(lo_.Y,center.Y)),max(-radius,S(lo_.Z,center.Z)),1};
        hi_={min(radius,S(hi_.X,center.X)),min(radius,S(hi_.Y,center.Y)),min(radius,S(hi_.Z,center.Z)),1};
        auto side=[&](float extent,unsigned bit){if(extent>0){candidates_|=bit;far_=max(far_,extent);}};
        side(-lo_.X,2);side(-lo_.Y,8);side(-lo_.Z,16);side(hi_.X,1);side(hi_.Y,4);side(hi_.Z,32);far_=min(radius,far_);
        near_.fill(std::numeric_limits<float>::max());farDepth_.fill(std::bit_cast<float>(0x00800000u));
        float half=M(field.viewRange,.5f);B seed{half,0,0,0,0,half,0,0,0,0,half,0,center.X,center.Y,center.Z,1};
        accumulate(seed); // Real D69760 seed; no Model tuple is published.
    }
    std::uint32_t test(const std::array<float,20>& bounds){return accumulate(source_shadow_camera::worldBox(bounds));}
    float currentDistance(unsigned face)const {return lastDistances_.at(face);}
    void finish(CaptureWork& work)const {
        using namespace source_shadow_camera;
        static constexpr std::array<V,6> directions{{{1,0,0,0},{-1,0,0,0},{0,1,0,0},{0,-1,0,0},{0,0,-1,0},{0,0,1,0}}};
        static constexpr std::array<V,6> ups{{{0,1,0,0},{0,1,0,0},{0,0,1,0},{0,0,-1,0},{0,1,0,0},{0,1,0,0}}};
        for(unsigned face=0;face<6;face++){auto& f=work.faces[face];f.index=face;f.visible=(visible_&(1u<<face))!=0;if(!f.visible)continue;
            f.nearDistance=max(1.f,near_[face]);f.farDistance=min(far_,farDepth_[face]);const float n=f.nearDistance,z=f.farDistance,twice=M(n,2.f);
            B projection{D(M(n,-2.f),twice),0,0,0,0,D(A(n,n),twice),0,0,0,0,D(z,S(z,n)),1,0,0,D(M(n,z),S(n,z)),0};
            f.worldToProjection=source_camera::compose(lightView(center_,directions[face],ups[face]),projection);
        }
    }
private:
    std::uint32_t accumulate(const B& box){
        using namespace source_shadow_camera;
        float x=A(A(std::fabs(box[4]),std::fabs(box[0])),std::fabs(box[8]));
        float y=A(A(std::fabs(box[5]),std::fabs(box[1])),std::fabs(box[9]));
        float z=A(A(std::fabs(box[6]),std::fabs(box[2])),std::fabs(box[10]));
        V lo{S(S(box[12],x),center_.X),S(S(box[13],y),center_.Y),S(S(box[14],z),center_.Z),1};
        V hi{S(A(x,box[12]),center_.X),S(A(y,box[13]),center_.Y),S(A(z,box[14]),center_.Z),1};
        if(((candidates_&2)&&hi.X<=lo_.X)||((candidates_&8)&&hi.Y<=lo_.Y)||((candidates_&16)&&hi.Z<=lo_.Z)||
            ((candidates_&1)&&hi_.X<=lo.X)||((candidates_&4)&&hi_.Y<=lo.Y)||((candidates_&32)&&hi_.Z<=lo.Z))return 0;
        unsigned mask=0;auto face=[&](unsigned i,float a,float b,float c,float d,float n,float f){
            if(!(candidates_&(1u<<i))||!(f>0&&a<f&&-b<f&&c<f&&-d<f))return;
            if(M(a,b)>0)n=max(n,min(std::fabs(a),std::fabs(b)));if(M(c,d)>0)n=max(n,min(std::fabs(c),std::fabs(d)));
            float distance=1.f<=n?n:1.f;lastDistances_[i]=distance;near_[i]=min(near_[i],distance);farDepth_[i]=max(farDepth_[i],f);mask|=1u<<i;};
        face(0,lo.Z,hi.Z,lo.Y,hi.Y,lo.X,hi.X);face(1,lo.Z,hi.Z,lo.Y,hi.Y,-hi.X,-lo.X);
        face(2,lo.X,hi.X,lo.Z,hi.Z,lo.Y,hi.Y);face(3,lo.X,hi.X,lo.Z,hi.Z,-hi.Y,-lo.Y);
        face(5,lo.X,hi.X,lo.Y,hi.Y,lo.Z,hi.Z);face(4,lo.X,hi.X,lo.Y,hi.Y,-hi.Z,-lo.Z);
        visible_|=mask;return mask;
    }
};
} // namespace San14::Render::Reflection
