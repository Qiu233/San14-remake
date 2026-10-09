#pragma once
// Same ordinary source Main -> D38940 clone -> Sky/Cloud, and ordinary Main
// ->998EE0 Fog. Camera publication never advances shared time or Cloud history.
#include "../../Camera/CameraState.h"
#include "../../Camera/CameraMatrix.h"
#include "WeatherFogFrame.h"

namespace source_atmosphere_camera {
using source_camera::A;using source_camera::S;using source_camera::M;using source_camera::D;
using source_camera::V3;using source_camera::V4;using source_camera::Matrix;using source_camera::Prefix;
using source_camera::get;using source_camera::put;
inline float F(uint32_t bits){float value;std::memcpy(&value,&bits,4);return value;}
struct Profile {
    Prefix preparedMain{};Matrix skyTransform{},localToGlobal{};
    V4 sunProjection{},moonPosition{},altitude{};
    float skyYOffset=0,sunSize=0,moonSize=0,skyInfoZ=0,skyInfoW=0,halfFovTangent=0;
    int width=0,height=0;
};
struct Packet {
    Profile profile;
    std::array<uint8_t,144> skyVertex{};std::array<uint8_t,336> skyPixel{};
    std::array<uint8_t,176> cloudVertex{};std::array<uint8_t,560> cloudPixel{};
    std::array<std::array<uint8_t,1152>,3> fogPixel{};
};
struct Frame {
    Prefix preparedMain{},skyCamera{};
    std::array<uint8_t,144> skyVertex{};std::array<uint8_t,336> skyPixel{};
    std::array<uint8_t,176> cloudVertex{};std::array<uint8_t,560> cloudPixel{};
    std::array<uint8_t,32> cloudHistoryVertex{};
    std::array<uint8_t,16> fogVertex{};
    std::array<std::array<uint8_t,1152>,3> fogPixel{};
};
template<size_t N> inline std::array<uint8_t,N> readBytes(const std::filesystem::path& path){
    std::ifstream s(path,std::ios::binary);std::array<uint8_t,N> result{};
    s.read(reinterpret_cast<char*>(result.data()),N);
    if(!s||s.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Atmosphere camera source packet size: "+path.string());
    return result;
}
inline Packet readPacket(const std::filesystem::path& source){
    Packet r;auto& p=r.profile;std::ifstream s(source/"profile.bin",std::ios::binary);
    p.preparedMain=source_camera::read<Prefix>(s);p.skyTransform=source_camera::read<Matrix>(s);p.localToGlobal=source_camera::read<Matrix>(s);
    p.sunProjection=source_camera::read<V4>(s);p.moonPosition=source_camera::read<V4>(s);p.altitude=source_camera::read<V4>(s);
    p.skyYOffset=source_camera::read<float>(s);p.sunSize=source_camera::read<float>(s);p.moonSize=source_camera::read<float>(s);
    p.skyInfoZ=source_camera::read<float>(s);p.skyInfoW=source_camera::read<float>(s);p.halfFovTangent=source_camera::read<float>(s);
    p.width=source_camera::read<int>(s);p.height=source_camera::read<int>(s);
    if(s.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Atmosphere camera profile tail");
    r.skyVertex=readBytes<144>(source/"main-sky-vs-b0.bin");r.skyPixel=readBytes<336>(source/"main-sky-ps-b0.bin");
    r.cloudVertex=readBytes<176>(source/"cloud/pass1-vs-b0.bin");r.cloudPixel=readBytes<560>(source/"cloud/pass1-ps-b0.bin");
    int i=0;for(const char* name:{"fog-mask02","fog-mask08","fog-background"})r.fogPixel[i++]=readBytes<1152>(source/"fog"/(std::string(name)+"-ps-b0.bin"));
    return r;
}
inline source_weather_fog::Shared publishWeather(Packet& packet,const source_weather_fog::Frame& weather,V3 actualGlobal80Rgb){
    source_weather_fog::publish(packet.profile.preparedMain,weather);
    auto record=source_weather_fog::package(weather,get<float>(packet.profile.preparedMain.data()+0x1ec));
    auto atmosphere=source_weather_fog::shared(record,actualGlobal80Rgb);
    put(packet.skyPixel.data(),atmosphere.fog);put(packet.skyPixel.data()+16,atmosphere.scat);
    put(packet.cloudPixel.data()+32,atmosphere.fog);put(packet.cloudPixel.data()+48,atmosphere.scat);return atmosphere;
}
inline void publishDirectStage(Packet& packet,const Prefix& currentMain){
    for(unsigned offset:{0x180u,0x188u,0x1c4u,0x1d0u,0x210u})
        std::memcpy(packet.profile.preparedMain.data()+offset,currentMain.data()+offset,4);
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
inline Frame prepareCurrent(const Packet& packet,const Prefix& ordinaryCamera){
    const auto& p=packet.profile;Frame f;
    // The season owner holds current direct-stage/weather inputs. Motion does
    // not restore raw constructor MainAltitude210=8 over published current210.
    f.preparedMain=p.preparedMain;std::memcpy(f.preparedMain.data()+0x10,ordinaryCamera.data()+0x10,0x150);
    f.skyCamera=f.preparedMain;
    V4 eye=get<V4>(f.skyCamera.data()+0x10),target=get<V4>(f.skyCamera.data()+0x20),up=get<V4>(f.skyCamera.data()+0x30);
    eye.Y=A(M(get<float>(f.preparedMain.data()+0x210),100000),eye.Y);put(f.skyCamera.data()+0x10,eye);
    Matrix raw=source_camera::lookTo(eye,target,up),view=adjustedView(raw,get<float>(f.skyCamera.data()+0x150));
    Matrix projected=get<Matrix>(f.skyCamera.data()+0xc0),w2p=source_camera::compose(view,projected);
    put(f.skyCamera.data()+0x40,raw);put(f.skyCamera.data()+0x80,view);put(f.skyCamera.data()+0x100,w2p);
    float fov=get<float>(f.skyCamera.data()+0x148),aspect=get<float>(f.skyCamera.data()+0x14c);
    bool reverse=(get<uint32_t>(f.skyCamera.data()+0x158)&2)!=0;

    //118DC30/3F66E0/98B630. Cancellation is performed in original order.
    Matrix rigid{view[0],view[4],view[8],0,view[1],view[5],view[9],0,view[2],view[6],view[10],0,
        source_camera::flip(A(A(M(view[13],view[1]),M(view[12],view[0])),M(view[14],view[2]))),
        source_camera::flip(A(A(M(view[13],view[5]),M(view[12],view[4])),M(view[14],view[6]))),
        source_camera::flip(A(A(M(view[13],view[9]),M(view[12],view[8])),M(view[14],view[10]))),1};
    V4 negative{M(eye.X,-1),M(eye.Y,-1),M(eye.Z,-1),M(eye.W,-1)};
    for(int row=0;row<4;++row){int a=row*4;float w=rigid[a+3];rigid[a]=A(M(w,negative.X),rigid[a]);rigid[a+1]=A(M(w,negative.Y),rigid[a+1]);rigid[a+2]=A(M(w,negative.Z),rigid[a+2]);}
    Matrix c2l=source_camera::compose(source_camera::compose(source_matrix::inverse(projected),rigid),p.skyTransform);
    f.skyVertex=packet.skyVertex;f.skyPixel=packet.skyPixel;put(f.skyVertex.data(),c2l);put(f.skyVertex.data()+64,0.f);put(f.skyVertex.data()+80,p.localToGlobal);
    // B992A0/D7CE6C publishes current Main210*1000 to SkyYOffset1438.
    float skyYOffset=M(get<float>(f.preparedMain.data()+0x210),1000);
    float x=M(eye.X,.01f),y=A(A(M(eye.Y,.01f),p.altitude.Z),skyYOffset),z=M(eye.Z,.01f),w=eye.W;
    auto& m=p.skyTransform;
    V4 viewPosition{A(A(A(M(y,m[4]),M(x,m[0])),M(z,m[8])),M(w,m[12])),
        A(A(A(M(x,m[1]),M(y,m[5])),M(z,m[9])),M(w,m[13])),
        A(A(A(M(x,m[2]),M(y,m[6])),M(z,m[10])),M(w,m[14])),
        A(A(A(M(x,m[3]),M(y,m[7])),M(z,m[11])),M(w,m[15]))};
    put(f.skyPixel.data()+80,viewPosition);
    float sunLiteral=F(0x3c187400),moonLiteral=F(0x3c142917);
    float pixel=D(fov,float(p.height*2)),sunRadius=M(M(p.sunSize,sunLiteral),.5f),moonRadius=M(M(p.moonSize,moonLiteral),.5f);
    put(f.skyPixel.data()+96,V4{pixel>sunRadius?pixel:sunRadius,pixel>moonRadius?pixel:moonRadius,p.skyInfoZ,p.skyInfoW});
    put(f.skyPixel.data()+256,screenPosition(p.sunProjection,w2p,fov,aspect,p.width,p.height,p.sunSize,sunLiteral));
    V4 moon{M(p.moonPosition.X,-1),M(p.moonPosition.Y,-1),M(p.moonPosition.Z,-1),M(p.moonPosition.W,-1)};
    put(f.skyPixel.data()+272,screenPosition(moon,w2p,fov,aspect,p.width,p.height,p.moonSize,moonLiteral));

    //1191FE0 direction camera is a second original camera, near1/far100.
    // Its vectors are near-minus-far; using the ordinary Main rays is wrong.
    f.cloudVertex=packet.cloudVertex;f.cloudPixel=packet.cloudPixel;
    put(f.cloudVertex.data()+16,source_matrix::inverse(w2p));
    V4 direction{S(target.X,eye.X),S(target.Y,eye.Y),S(target.Z,eye.Z),1};
    float norm=std::sqrt(A(A(M(direction.X,direction.X),M(direction.Y,direction.Y)),M(direction.Z,direction.Z))),inv=D(1,norm);
    direction={M(direction.X,inv),M(direction.Y,inv),M(direction.Z,inv),1};
    Matrix rayView=adjustedView(source_camera::lookTo({0,0,0,1},direction,up),get<float>(f.skyCamera.data()+0x150));
    Matrix rayProjection=projection(p.halfFovTangent,aspect,1,100,reverse);
    Matrix rays=source_matrix::inverse(source_camera::compose(rayView,rayProjection));
    float farDepth=reverse?0.f:1.f,nearDepth=reverse?1.f:0.f;
    for(int corner=0;corner<4;++corner){float cx=float((corner>>1)*2-1),cy=float(1-(corner&1)*2);
        V4 a=rayTransform(cx,cy,farDepth,rays),b=rayTransform(cx,cy,nearDepth,rays);float ai=D(1,a.W),bi=D(1,b.W);
        put(f.cloudVertex.data()+80+corner*16,V4{S(M(b.X,bi),M(a.X,ai)),S(M(b.Y,bi),M(a.Y,ai)),S(M(b.Z,bi),M(a.Z,ai)),S(M(b.W,bi),M(a.W,ai))});}
    put(f.cloudVertex.data()+144,farDepth);put(f.cloudVertex.data()+148,nearDepth);
    put(f.cloudPixel.data()+16,eye);put(f.cloudPixel.data()+300,fov);
    put(f.cloudHistoryVertex.data()+16,farDepth);put(f.cloudHistoryVertex.data()+20,nearDepth);

    //998EE0 uses ordinary Main full80, not the raised Sky/Cloud clone.
    float mainNear=get<float>(f.preparedMain.data()+0x140),mainFar=get<float>(f.preparedMain.data()+0x144);
    float a=reverse?mainFar:mainNear,b=reverse?mainNear:mainFar;
    V4 vP2V{D(b,S(a,b)),D(M(b,a),S(b,a)),M(p.halfFovTangent,aspect),p.halfFovTangent};
    put(f.fogVertex.data(),vP2V);Matrix inverseView=source_matrix::inverse(get<Matrix>(f.preparedMain.data()+0x80));
    f.fogPixel=packet.fogPixel;for(auto& bytes:f.fogPixel){put(bytes.data()+1072,vP2V);put(bytes.data()+1088,inverseView);}
    return f;
}
} // namespace source_atmosphere_camera

#ifndef SOURCE_ATMOSPHERE_CAMERA_CPU_ONLY
struct NativeAtmosphereCamera {
    source_atmosphere_camera::Packet packet;
    source_atmosphere_camera::Frame current;
};
static NativeAtmosphereCamera prepareNativeAtmosphereCamera(ID3D11Device* device,const fs::path& source,
    NativeMainSky& sky,NativeMapFog& fog){
    NativeAtmosphereCamera r;r.packet=source_atmosphere_camera::readPacket(source);
    auto make=[&](const auto& bytes){std::vector<char> copy(bytes.size());std::memcpy(copy.data(),bytes.data(),bytes.size());return nativeMainCloudBuffer(device,copy);};
    // Only the camera-updated buffers change usage. Programs, typed textures,
    // samplers, geometry, views and existing Cloud history remain owned.
    sky.vertexConstants=make(r.packet.skyVertex);sky.pixelConstants=make(r.packet.skyPixel);
    // Fog VS is the actual16-byte vP2V, not the1152-byte pixel package.
    auto initial=source_atmosphere_camera::prepareCurrent(r.packet,r.packet.profile.preparedMain);
    for(auto& pass:fog.passes)for(auto& constant:pass.constants)if(constant.vertex)constant.buffer=make(initial.fogVertex);
    r.current=std::move(initial);return r;
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
static void publishNativeAtmosphereCurrentDirectPrefix(NativeAtmosphereCamera& r,const source_camera::Prefix& currentMain){
    source_atmosphere_camera::publishDirectStage(r.packet,currentMain);
}
static void updateNativeAtmosphereCamera(ID3D11DeviceContext* context,NativeAtmosphereCamera& r,
    const source_camera::Prefix& ordinaryCamera,NativeMainSky& sky,NativeMainCloud& cloud,NativeMapFog& fog){
    r.current=source_atmosphere_camera::prepareCurrent(r.packet,ordinaryCamera);const auto& f=r.current;
    context->UpdateSubresource(sky.vertexConstants.Get(),0,nullptr,f.skyVertex.data(),0,0);
    context->UpdateSubresource(sky.pixelConstants.Get(),0,nullptr,f.skyPixel.data(),0,0);
    for(auto& pass:cloud.passes){
        if(pass.name=="pass1"){
            std::memcpy(pass.vbytes.data()+16,f.cloudVertex.data()+16,136);
            std::memcpy(pass.pbytes.data()+16,f.cloudPixel.data()+16,16);std::memcpy(pass.pbytes.data()+300,f.cloudPixel.data()+300,4);
            //pass1's normal execute repacks Bayer/phase/previous and uploads.
        }else{std::memcpy(pass.vbytes.data()+16,f.cloudHistoryVertex.data()+16,8);context->UpdateSubresource(pass.vcb.Get(),0,nullptr,pass.vbytes.data(),0,0);}
    }
    std::memcpy(cloud.currentW2P.data(),f.skyCamera.data()+0x100,64);
    //previousW2P remains the previous submitted camera. The existing pass1
    //Draw must finish before its normal store current -> previous occurs.
    for(auto& pass:fog.passes)for(auto& constant:pass.constants){
        if(constant.vertex){std::memcpy(constant.bytes.data(),f.fogVertex.data(),16);context->UpdateSubresource(constant.buffer.Get(),0,nullptr,constant.bytes.data(),0,0);}
        else{std::memcpy(constant.bytes.data()+1072,f.fogVertex.data(),16);std::memcpy(constant.bytes.data()+1088,f.fogPixel[0].data()+1088,64);}
        //Fog execute still advances its source24 phases and uploads the PCB.
    }
}
#endif
