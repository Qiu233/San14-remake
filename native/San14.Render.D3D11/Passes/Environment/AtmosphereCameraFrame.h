#pragma once
// Sky and cloud use a raised camera; fog uses the ordinary camera.
// Publishing their constants does not advance cloud history or shared time.
#include "../../Camera/CameraState.h"
#include "../../Camera/CameraMatrix.h"
#include "WeatherFogFrame.h"
#include "SkyMaterialInputs.h"

namespace source_atmosphere_camera {
using source_camera::A;using source_camera::S;using source_camera::M;using source_camera::D;
using source_camera::V3;using source_camera::V4;using source_camera::Matrix;using source_camera::Prefix;
using source_camera::get;using source_camera::put;
using source_camera::CameraTransform;
inline float F(uint32_t bits){float value;std::memcpy(&value,&bits,4);return value;}
struct DirectEnvironment {
    float altitude{},fogBlend{};
};
// Decoding is the compatibility boundary for prepared media and seasonal inputs.
inline DirectEnvironment decodeDirectEnvironment(const Prefix& input) {
    return {get<float>(input.data()+0x210),get<float>(input.data()+0x1ec)};
}
struct CelestialEnvironment {
    Matrix skyTransform{},localToGlobal{};
    V4 sunProjection{},moonPosition{},altitude{};
    float sunSize=0,moonSize=0,sunIntensity=0,moonIntensity=0,halfFovTangent=0;
};
struct Profile {
    CameraTransform initialCamera{};
    int width=0,height=0;
    float cloudSunDirectionW{};
};
struct Packet {
    Profile profile;
    DirectEnvironment direct;
    CelestialEnvironment environment;
    San14::Render::SkyMaterialState sky;
    San14::Render::SkyDrawPolicy skyPolicy;
    bool milkyTexturePresent{};
    std::array<uint8_t,176> cloudVertex{};std::array<uint8_t,560> cloudPixel{};
};
struct Frame {
    CameraTransform skyCamera{};
    San14::Render::SkyMaterialState sky;
    San14::Render::SkyDrawPolicy skyPolicy;
    std::array<uint8_t,176> cloudVertex{};std::array<uint8_t,560> cloudPixel{};
    std::array<uint8_t,32> cloudHistoryVertex{};
    V4 fogProjection{},fogEye{};
    Matrix fogInverseView{};
};
template<size_t N> inline std::array<uint8_t,N> readBytes(const std::filesystem::path& path){
    std::ifstream s(path,std::ios::binary);std::array<uint8_t,N> result{};
    s.read(reinterpret_cast<char*>(result.data()),N);
    if(!s||s.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Atmosphere camera source packet size: "+path.string());
    return result;
}
inline Packet readPacket(const std::filesystem::path& source,const std::filesystem::path& skySource){
    Packet r;auto& p=r.profile;std::ifstream s(source/"profile.bin",std::ios::binary);
    const auto preparedCamera=source_camera::read<Prefix>(s);
    p.initialCamera=source_camera::decodeCameraTransform(preparedCamera);r.direct=decodeDirectEnvironment(preparedCamera);
    auto& environment=r.environment;environment.skyTransform=source_camera::read<Matrix>(s);environment.localToGlobal=source_camera::read<Matrix>(s);
    environment.sunProjection=source_camera::read<V4>(s);environment.moonPosition=source_camera::read<V4>(s);environment.altitude=source_camera::read<V4>(s);
    source_camera::read<float>(s); // Retired prepared offset; every frame derives it from altitude.
    environment.sunSize=source_camera::read<float>(s);environment.moonSize=source_camera::read<float>(s);
    environment.sunIntensity=source_camera::read<float>(s);environment.moonIntensity=source_camera::read<float>(s);environment.halfFovTangent=source_camera::read<float>(s);
    p.width=source_camera::read<int>(s);p.height=source_camera::read<int>(s);
    if(s.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Atmosphere camera profile tail");
    r.sky=San14::Render::Interop::readSkyInitialState(skySource/"initial-state.bin");
    r.milkyTexturePresent=San14::Render::Interop::readSkySamplingPresence(skySource/"sampling-presence.bin").milky;
    r.skyPolicy=San14::Render::skyDrawPolicy(get<std::uint32_t>(preparedCamera.data()+0x3b0));
    r.cloudVertex=readBytes<176>(source/"cloud/pass1-vs-b0.bin");r.cloudPixel=readBytes<560>(source/"cloud/pass1-ps-b0.bin");
    p.cloudSunDirectionW=get<float>(r.cloudPixel.data()+476);
    return r;
}
inline source_weather_fog::Shared publishWeather(Packet& packet,const source_weather_fog::Frame& weather,V3 actualGlobal80Rgb){
    packet.direct.altitude=weather.currentAltitude;
    auto record=source_weather_fog::package(weather,packet.direct.fogBlend);
    auto atmosphere=source_weather_fog::shared(record,actualGlobal80Rgb);
    packet.sky.fog=atmosphere.fog;packet.sky.scattering=atmosphere.scat;
    put(packet.cloudPixel.data()+32,atmosphere.fog);put(packet.cloudPixel.data()+48,atmosphere.scat);return atmosphere;
}
inline void publishDirectStage(Packet& packet,float altitude) {
    // Fog blend retains the imported atmosphere setting. Only current altitude
    // was consumed from the old direct-stage publication.
    packet.direct.altitude=altitude;
}
inline Matrix adjustedView(Matrix raw,float offset){
    if(offset==0)return raw;auto t=source_camera::sinCos(source_camera::flip(offset));Matrix view=raw;
    for(int row=0;row<4;++row){float x=raw[row*4],y=raw[row*4+1];view[row*4]=S(M(x,t[1]),M(y,t[0]));view[row*4+1]=A(M(x,t[0]),M(y,t[1]));}
    return view;
}
inline Matrix projection(float tangent,float aspect,float sourceNear,float sourceFar,bool reverse){
    float nearArgument=reverse?sourceFar:sourceNear,farArgument=reverse?sourceNear:sourceFar;
    float yScale=D(1,M(tangent,1)),depthScale=D(farArgument,S(nearArgument,farArgument)),depthOffset=M(depthScale,nearArgument);
    return {D(yScale,aspect),0,0,0,0,yScale,0,0,0,0,depthScale,-1,0,0,depthOffset,0};
}
inline V4 screenPosition(V4 v,const Matrix& m,float fov,float aspect,int width,int height,float size,float literal){
    float x=A(A(A(M(v.Y,m[4]),M(v.X,m[0])),M(v.Z,m[8])),M(v.W,m[12]));
    float y=A(A(A(M(v.Y,m[5]),M(v.X,m[1])),M(v.Z,m[9])),M(v.W,m[13]));
    float w=A(A(A(M(v.Y,m[7]),M(v.X,m[3])),M(v.Z,m[11])),M(v.W,m[15]));
    float inverse=D(1,w),sx=M(x,inverse),sy=M(y,inverse);
    if(!(w>0))sx=sy=-1000;
    float minimum=D(2,float(std::max(width,height))),radius=M(M(size,literal),.5f),ratio=D(radius,fov);
    float maximum=minimum>ratio?minimum:ratio;return {sx,sy,aspect,A(maximum,maximum)};
}
inline V4 rayTransform(float x,float y,float z,const Matrix& m){
    V4 r;float* v=&r.X;for(int col=0;col<4;++col)v[col]=A(A(A(M(x,m[col]),M(y,m[4+col])),M(z,m[8+col])),m[12+col]);return r;
}
inline Frame prepareCurrent(const Packet& packet,const CameraTransform& ordinaryCamera){
    const auto& p=packet.environment;Frame f;
    f.skyCamera=ordinaryCamera;
    auto& camera=f.skyCamera;
    camera.eye.Y=A(M(packet.direct.altitude,100000),camera.eye.Y);
    camera.unrolledView=source_camera::lookTo(camera.eye,camera.target,camera.up);
    camera.view=adjustedView(camera.unrolledView,camera.optics.roll);
    camera.worldToClip=source_camera::compose(camera.view,camera.projection);
    const auto eye=camera.eye,target=camera.target,up=camera.up;
    const auto& view=camera.view;const auto& projected=camera.projection;const auto& w2p=camera.worldToClip;
    const float fov=camera.optics.viewSpan,aspect=camera.optics.aspectRatio;
    const bool reverse=camera.optics.reversedDepth;

    // Cancel camera translation before projecting the sky into its local space.
    Matrix rigid{view[0],view[4],view[8],0,view[1],view[5],view[9],0,view[2],view[6],view[10],0,
        source_camera::flip(A(A(M(view[13],view[1]),M(view[12],view[0])),M(view[14],view[2]))),
        source_camera::flip(A(A(M(view[13],view[5]),M(view[12],view[4])),M(view[14],view[6]))),
        source_camera::flip(A(A(M(view[13],view[9]),M(view[12],view[8])),M(view[14],view[10]))),1};
    V4 negative{M(eye.X,-1),M(eye.Y,-1),M(eye.Z,-1),M(eye.W,-1)};
    for(int row=0;row<4;++row){int a=row*4;float w=rigid[a+3];rigid[a]=A(M(w,negative.X),rigid[a]);rigid[a+1]=A(M(w,negative.Y),rigid[a+1]);rigid[a+2]=A(M(w,negative.Z),rigid[a+2]);}
    Matrix c2l=source_camera::compose(source_camera::compose(source_matrix::inverse(projected),rigid),p.skyTransform);
    f.sky=packet.sky;f.skyPolicy=packet.skyPolicy;
    f.sky.cameraToLocal=c2l;f.sky.projectionFarZ=0;f.sky.localToGlobal=p.localToGlobal;
    // The physical sky offset is derived from the accepted altitude.
    float skyYOffset=M(packet.direct.altitude,1000);
    float x=M(eye.X,.01f),y=A(A(M(eye.Y,.01f),p.altitude.Z),skyYOffset),z=M(eye.Z,.01f),w=eye.W;
    auto& m=p.skyTransform;
    V4 viewPosition{A(A(A(M(y,m[4]),M(x,m[0])),M(z,m[8])),M(w,m[12])),
        A(A(A(M(x,m[1]),M(y,m[5])),M(z,m[9])),M(w,m[13])),
        A(A(A(M(x,m[2]),M(y,m[6])),M(z,m[10])),M(w,m[14])),
        A(A(A(M(x,m[3]),M(y,m[7])),M(z,m[11])),M(w,m[15]))};
    f.sky.viewPosition=viewPosition;
    float sunLiteral=F(0x3c187400),moonLiteral=F(0x3c142917);
    float pixel=D(fov,float(packet.profile.height*2)),sunRadius=M(M(p.sunSize,sunLiteral),.5f),moonRadius=M(M(p.moonSize,moonLiteral),.5f);
    f.sky.skyInfo0={pixel>sunRadius?pixel:sunRadius,pixel>moonRadius?pixel:moonRadius,p.sunIntensity,p.moonIntensity};
    f.sky.sunScreenPosition=screenPosition(p.sunProjection,w2p,fov,aspect,packet.profile.width,packet.profile.height,p.sunSize,sunLiteral);
    V4 moon{M(p.moonPosition.X,-1),M(p.moonPosition.Y,-1),M(p.moonPosition.Z,-1),M(p.moonPosition.W,-1)};
    f.sky.moonScreenPosition=screenPosition(moon,w2p,fov,aspect,packet.profile.width,packet.profile.height,p.moonSize,moonLiteral);

    // Cloud direction rays use a separate origin camera with near 1 / far 100.
    // Its vectors are near minus far.
    f.cloudVertex=packet.cloudVertex;f.cloudPixel=packet.cloudPixel;
    put(f.cloudVertex.data()+16,source_matrix::inverse(w2p));
    V4 direction{S(target.X,eye.X),S(target.Y,eye.Y),S(target.Z,eye.Z),1};
    float norm=std::sqrt(A(A(M(direction.X,direction.X),M(direction.Y,direction.Y)),M(direction.Z,direction.Z))),inv=D(1,norm);
    direction={M(direction.X,inv),M(direction.Y,inv),M(direction.Z,inv),1};
    CameraTransform rayCamera;
    rayCamera.eye={0,0,0,1};rayCamera.target=direction;rayCamera.up=up;
    rayCamera.optics=camera.optics;rayCamera.optics.projectionKind=source_camera::ProjectionKind::Perspective;
    rayCamera.optics.nearPlane=1;rayCamera.optics.farPlane=100;
    rayCamera.unrolledView=source_camera::lookTo(rayCamera.eye,rayCamera.target,rayCamera.up);
    rayCamera.view=adjustedView(rayCamera.unrolledView,rayCamera.optics.roll);
    rayCamera.projection=projection(p.halfFovTangent,rayCamera.optics.aspectRatio,
        rayCamera.optics.nearPlane,rayCamera.optics.farPlane,rayCamera.optics.reversedDepth);
    rayCamera.worldToClip=source_camera::compose(rayCamera.view,rayCamera.projection);
    Matrix rays=source_matrix::inverse(rayCamera.worldToClip);
    float farDepth=reverse?0.f:1.f,nearDepth=reverse?1.f:0.f;
    for(int corner=0;corner<4;++corner){float cx=float((corner>>1)*2-1),cy=float(1-(corner&1)*2);
        V4 a=rayTransform(cx,cy,farDepth,rays),b=rayTransform(cx,cy,nearDepth,rays);float ai=D(1,a.W),bi=D(1,b.W);
        put(f.cloudVertex.data()+80+corner*16,V4{S(M(b.X,bi),M(a.X,ai)),S(M(b.Y,bi),M(a.Y,ai)),S(M(b.Z,bi),M(a.Z,ai)),S(M(b.W,bi),M(a.W,ai))});}
    put(f.cloudVertex.data()+144,farDepth);put(f.cloudVertex.data()+148,nearDepth);
    put(f.cloudPixel.data()+16,eye);put(f.cloudPixel.data()+300,fov);
    put(f.cloudHistoryVertex.data()+16,farDepth);put(f.cloudHistoryVertex.data()+20,nearDepth);

    // Fog reconstruction uses the ordinary camera rather than the raised sky camera.
    float mainNear=ordinaryCamera.optics.nearPlane,mainFar=ordinaryCamera.optics.farPlane;
    float a=reverse?mainFar:mainNear,b=reverse?mainNear:mainFar;
    V4 vP2V{D(b,S(a,b)),D(M(b,a),S(b,a)),M(p.halfFovTangent,aspect),p.halfFovTangent};
    f.fogProjection=vP2V;
    f.fogInverseView=source_matrix::inverse(ordinaryCamera.view);
    f.fogEye=ordinaryCamera.eye;
    return f;
}
} // namespace source_atmosphere_camera

#ifndef SOURCE_ATMOSPHERE_CAMERA_CPU_ONLY
struct NativeAtmosphereCamera {
    source_atmosphere_camera::Packet packet;
    source_atmosphere_camera::Frame current;
};
static NativeAtmosphereCamera prepareNativeAtmosphereCamera(const fs::path& source,const fs::path& skySource){
    NativeAtmosphereCamera r;r.packet=source_atmosphere_camera::readPacket(source,skySource);
    r.current=source_atmosphere_camera::prepareCurrent(r.packet,r.packet.profile.initialCamera);
    return r;
}
static void publishNativeAtmosphereWeather(NativeAtmosphereCamera& r,NativeMainCloud& cloud,
    const source_weather_fog::Frame& weather,source_camera::V3 actualGlobal80Rgb){
    auto atmosphere=source_atmosphere_camera::publishWeather(r.packet,weather,actualGlobal80Rgb);
    //The retained Main Cloud owner uploads this same package in its existing
    //pass1 execution. Fog begins32, Scat48 and previousW2P112 stays untouched.
    for(auto& pass:cloud.passes)if(pass.name=="pass1"){
        std::memcpy(pass.pbytes.data()+32,&atmosphere.fog,16);std::memcpy(pass.pbytes.data()+48,atmosphere.scat.data(),64);
    }
}
static void publishNativeAtmosphereCurrentDirectEnvironment(NativeAtmosphereCamera& r,float altitude){
    source_atmosphere_camera::publishDirectStage(r.packet,altitude);
}
static void updateNativeAtmosphereCamera(ID3D11DeviceContext* context,NativeAtmosphereCamera& r,
    const source_camera::CameraTransform& ordinaryCamera,NativeMainSky& sky,NativeMainCloud& cloud,bool publishSkyCloud){
    r.current=source_atmosphere_camera::prepareCurrent(r.packet,ordinaryCamera);const auto& f=r.current;
    if(!publishSkyCloud)return;
    publishNativeMainSkyMaterial(context,sky,f.sky);
    for(auto& pass:cloud.passes){
        if(pass.name=="pass1"){
            std::memcpy(pass.vbytes.data()+16,f.cloudVertex.data()+16,136);
            std::memcpy(pass.pbytes.data()+16,f.cloudPixel.data()+16,16);std::memcpy(pass.pbytes.data()+300,f.cloudPixel.data()+300,4);
            //pass1's normal execute repacks Bayer/phase/previous and uploads.
        }else{std::memcpy(pass.vbytes.data()+16,f.cloudHistoryVertex.data()+16,8);context->UpdateSubresource(pass.vcb.Get(),0,nullptr,pass.vbytes.data(),0,0);}
    }
    std::memcpy(cloud.currentW2P.data(),f.skyCamera.worldToClip.data(),64);
    //previousW2P remains the previous submitted camera. The existing pass1
    //Draw must finish before its normal store current -> previous occurs.
    // Fog consumes these semantic camera values after selecting its current
    // program layout, immediately before its existing phase upload and draw.
}
#endif
