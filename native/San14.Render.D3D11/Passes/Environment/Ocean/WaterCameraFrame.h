// CPU source inputs for same-band Main pan. No device/context/resource loading
// during publication. Far geometry remains an explicit independent requirement.
#pragma once
#include "../../../Camera/CameraTransform.h"
#include <array>
#include <cmath>

namespace source_water_camera {
struct RefractionDerived {
    std::array<float,3> position{},direction{},viewY{};
    std::array<float,2> depthToZ{};
    std::array<float,12> worldToTexture{};
};
struct CurrentCamera {
    RefractionDerived refraction;
    std::array<float,4> eye{}, refractionDirection{}, cameraDirectionXZ{};
    std::array<float,16> worldToProjection{};
    std::array<float,12> worldView{};
};
// Source scalar boundaries preserve binary32 operations and prohibit FMA joins.
__declspec(noinline) inline float A(float a,float b){return a+b;}
__declspec(noinline) inline float S(float a,float b){return a-b;}
__declspec(noinline) inline float M(float a,float b){return a*b;}
__declspec(noinline) inline float D(float a,float b){return a/b;}
inline std::array<float,3> deltaDirection(const source_camera::CameraTransform& camera,bool oceanOrder) {
    const std::array<float,3> eye{camera.eye.X,camera.eye.Y,camera.eye.Z},target{camera.target.X,camera.target.Y,camera.target.Z};
    std::array<float,3> delta{};
    for(unsigned i=0;i<3;i++)delta[i]=S(target[i],eye[i]);
    float a=M(delta[0],delta[0]),b=M(delta[1],delta[1]);
    float lengthSquared=A(oceanOrder?A(b,a):A(a,b),M(delta[2],delta[2]));float inv=D(1,std::sqrt(lengthSquared));
    for(auto& v:delta)v=M(v,inv);return delta;
}
inline RefractionDerived deriveRefraction(const source_camera::CameraTransform& camera,float minimumDepth=0,float maximumDepth=1) {
    RefractionDerived out;out.position={camera.eye.X,camera.eye.Y,camera.eye.Z};
    for(unsigned i=0;i<3;i++) {out.viewY[i]=camera.view[1+4*i];}
    out.direction=deltaDirection(camera,false); // OriginalDeferredRefractionMath9B4FF0.
    float nearClip=camera.optics.nearPlane,farClip=camera.optics.farPlane;bool reverse=camera.optics.reversedDepth;
    float u=reverse?nearClip:farClip,v=reverse?farClip:nearClip,q=D(1,S(maximumDepth,minimumDepth));
    out.depthToZ[1]=M(D(S(v,u),M(v,u)),q);
    float first=M(minimumDepth,q);first=M(first,S(u,v));first=D(first,u);first=A(first,1);out.depthToZ[0]=M(first,D(1,v));
    // Source9A8410 uses original half literals, independent of target dimensions.
    for(unsigned i=0;i<4;i++) {
        float w=camera.worldToClip[3+4*i],x=camera.worldToClip[4*i],y=camera.worldToClip[1+4*i];
        out.worldToTexture[i]=A(M(w,.5f),M(x,.5f));out.worldToTexture[4+i]=A(M(w,.5f),M(y,-.5f));out.worldToTexture[8+i]=w;
    }
    return out;
}
// The same camera mathematics now publishes named values, independent of a
// selected shader's buffer sizes and destination offsets.
inline CurrentCamera prepareCurrentCamera(const source_camera::CameraTransform& camera) {
    auto derived=deriveRefraction(camera);auto oceanDirection=deltaDirection(camera,true);
    std::array<float,12> view{};
    for(unsigned row=0;row<3;row++)for(unsigned col=0;col<4;col++)view[row*4+col]=camera.view[col*4+row];
    float x=S(camera.target.X,camera.eye.X),z=S(camera.target.Z,camera.eye.Z);
    float inv=D(1,std::sqrt(A(A(M(x,x),0),M(z,z))));std::array<float,2> directionXZ{M(x,inv),M(inv,z)};
    std::array<float,4> direction4{oceanDirection[0],oceanDirection[1],oceanDirection[2],S(camera.target.W,camera.eye.W)};
    return {derived,{camera.eye.X,camera.eye.Y,camera.eye.Z,camera.eye.W},direction4,
        {directionXZ[0],directionXZ[1],0,0},camera.worldToClip,view};
}
} // namespace source_water_camera
