#pragma once
// Actual D3A850 sixface +11775C0/Sky2/Star/Cloud numeric continuation.
// No cube allocation, phase advance, budget reset or history publication.
#include "SeasonAtmosphereConstants.h"

namespace source_current_ambient {
using namespace source_camera;
using source_atmosphere_camera::F;
struct Profile {uint32_t width=128;std::array<float,4> starClockScales{};
 std::array<std::array<uint8_t,144>,6> skyVs{};std::array<std::array<uint8_t,336>,6> skyPs{};
 std::array<std::array<uint8_t,160>,6> cloudVs{};std::array<std::array<uint8_t,512>,6> cloudPs{};
 std::array<std::array<uint8_t,352>,6> starVs{};std::array<std::array<uint8_t,16>,6> starGs{},starPs{};};
inline Profile readProfile(const std::filesystem::path& source){Profile p;const auto initial=source/"spring";
 for(unsigned f=0;f<6;++f){std::string n=std::to_string(f);
 p.skyVs[f]=source_atmosphere_camera::readBytes<144>(initial/("sky-face"+n+"-vs.bin"));p.skyPs[f]=source_atmosphere_camera::readBytes<336>(initial/("sky-face"+n+"-ps.bin"));
 p.cloudVs[f]=source_atmosphere_camera::readBytes<160>(initial/("cloud-face"+n+"-vs.bin"));p.cloudPs[f]=source_atmosphere_camera::readBytes<512>(initial/("cloud-face"+n+"-ps.bin"));
 p.starVs[f]=source_atmosphere_camera::readBytes<352>(initial/("star-face"+n+"-vs.bin"));p.starGs[f]=source_atmosphere_camera::readBytes<16>(initial/("star-face"+n+"-gs.bin"));p.starPs[f]=source_atmosphere_camera::readBytes<16>(initial/("star-face"+n+"-ps.bin"));}
 std::ifstream s(source/"star-clock-scales.bin",std::ios::binary);if(read<uint32_t>(s)!=4)throw std::runtime_error("Current WORK Star source seasons");for(int i=0;i<4;++i){uint32_t season=read<uint32_t>(s);if(season>=4)throw std::runtime_error("Current WORK Star season range");p.starClockScales[season]=read<float>(s);}return p;
}
inline Matrix leftY(Matrix m,float angle){auto t=sinCos(angle);Matrix r=m;
 for(int col=0;col<3;++col)r[col]=S(M(m[col],t[1]),M(m[8+col],t[0]));
 r[8]=A(M(m[0],t[0]),M(m[8],t[1]));r[9]=A(M(m[9],t[1]),M(m[1],t[0]));r[10]=A(M(m[10],t[1]),M(m[2],t[0]));return r;}
inline Matrix rightY(Matrix m,float angle){auto t=sinCos(angle);for(int row=0;row<4;++row){int at=row*4;float x=m[at],z=m[at+2];m[at]=A(M(z,t[0]),M(x,t[1]));m[at+2]=S(M(z,t[1]),M(x,t[0]));}return m;}
inline Matrix combine3x3(const Matrix& a,const Matrix& b){Matrix out{};for(int row=0;row<3;++row)for(int col=0;col<3;++col)
 out[row*4+col]=A(A(M(b[row*4+1],a[4+col]),M(b[row*4],a[col])),M(b[row*4+2],a[8+col]));out[15]=1;return out;}
inline Matrix controllerF0(const source_current_season::Frame& current){const auto* p=reinterpret_cast<const uint8_t*>(current.atmosphere.data());
 float season=M(S(get<float>(current.main.data()+0x200),F(0x406ccccd)),F(0x3f060a92)),dt=get<float>(current.main.data()+0x204);dt=dt>0?dt:0;
 float longitude=M(get<float>(current.main.data()+0x208),F(0x3c8efa35)),hour=S(A(M(S(A(dt,dt),1),F(0x40490fdb)),season),longitude);
 auto q=sinCos(F(0x3fc90fdb));Matrix celestial{1,0,0,0,0,q[1],q[0],0,0,flip(q[0]),q[1],0,0,0,0,1};
 celestial=rightY(leftY(celestial,F(0x40490fdb)),hour);return source_matrix::inverse(combine3x3(celestial,get<Matrix>(p)));
}
inline float rational(float square){float n=S(F(0xbc5b3fe1),M(square,F(0x3b81ce6b)));n=S(M(n,square),F(0x3d678bdd));n=A(M(n,square),F(0x3e3c94dc));n=M(n,square);return D(n,S(F(0x3f8d6fa5),M(square,F(0x3f561f0d))));}
// Original101E910 and3F7280, closed finite MoonFace domain. No libm acos.
inline float acosSource(float value){uint32_t b;std::memcpy(&b,&value,4);float a=F(b&0x7fffffff);bool negative=(b>>31)!=0;if(a==1)return negative?F(0x40490fdb):0;if(a<F(0x32800000))return F(0x3fc90fdb);
 bool large=a>=.5f;float square=large?M(S(1,a),.5f):M(a,a),root=large?std::sqrt(square):0,arg=large?root:a,ratio=rational(square);uint64_t lowBits=0x3c91a62633145c07;double low;std::memcpy(&low,&lowBits,8);const double halfPi=1.5707963267948966;
 if(large){if(negative)return float(source_weather_fog::DS(3.1415926535897931,source_weather_fog::DM(source_weather_fog::DA(source_weather_fog::DS(double(M(ratio,arg)),low),root),2)));
 uint32_t bits;std::memcpy(&bits,&root,4);float trunc=F(bits&0xffff0000),correction=D(S(square,M(trunc,trunc)),A(trunc,root));return A(A(M(correction,2),M(M(arg,2),ratio)),M(trunc,2));}
 return float(source_weather_fog::DS(halfPi,source_weather_fog::DS(double(value),source_weather_fog::DS(low,double(M(ratio,value))))));}
inline std::array<float,2> moonSinCos(float angle){float q=M(angle,F(0x3e22f983));q=angle>=0?A(q,.5f):S(q,.5f);int reductions=int(q);float a=S(angle,M(float(reductions),F(0x40c90fdb))),sign=1;
 if(a>F(0x3fc90fdb)){a=S(F(0x40490fdb),a);sign=-1;}else if(a<F(0xbfc90fdb)){a=S(F(0xc0490fdb),a);sign=-1;}
 float z=M(a,a),sp=S(F(0x3638b88e),M(z,F(0x32cd365b))),term=M(z,F(0x348bdd11));sp=S(M(sp,z),F(0x39500bf1));sp=A(M(sp,z),F(0x3c088886));sp=S(M(sp,z),F(0x3e2aaaab));sp=A(M(sp,z),1);
 float cp=S(F(0x37cfb4c2),term);cp=S(M(cp,z),F(0x3ab609aa));cp=A(M(cp,z),F(0x3d2aaaa3));cp=S(M(cp,z),.5f);cp=A(M(cp,z),1);return {M(sp,a),M(cp,sign)};}
inline Matrix moonFace(V4 d){float ax=S(M(d.Z,-1),M(d.Y,0)),ay=S(M(d.X,0),M(d.Z,0)),az=S(M(d.Y,0),M(d.X,-1)),dot=A(S(M(d.X,0),M(d.Y,1)),M(d.Z,0));float length=std::sqrt(A(A(M(ax,ax),M(ay,ay)),M(az,az)));
 if(length!=0){ax=D(ax,length);ay=D(ay,length);az=D(az,length);}else ax=ay=az=0;auto sc=moonSinCos(flip(acosSource(dot)));float s=sc[0],c=sc[1],t=S(1,c),dx=A(M(M(t,ax),ax),c),dy=A(M(M(t,ay),ay),c),dz=A(M(M(t,az),az),c),yz=M(M(ay,t),az),zx=M(M(az,t),ax),xy=M(M(ax,t),ay),sx=M(s,ax),sy=M(s,ay),sz=M(s,az);
 return {dx,A(sz,xy),S(zx,sy),0,S(xy,sz),dy,A(sx,yz),0,A(sy,zx),S(yz,sx),dz,0,0,0,0,1};}
struct Face {Prefix camera{};V4 eye{};Matrix view{},projection{},w2p{};std::array<uint8_t,144> skyVs{};std::array<uint8_t,336> skyPs{};std::array<uint8_t,160> cloudVs{};std::array<uint8_t,512> cloudPs{};std::array<uint8_t,352> starVs{};std::array<uint8_t,16> starGs{},starPs{};};
struct Frame {std::array<Face,6> faces;Matrix controllerInverse{};float starClockScale{};bool skyEnabled{};};
inline Frame prepareCurrent(const Profile& profile,const source_current_season::Frame& current,const source_current_season_atmosphere::CloudLightInput& cloudLight){
 Frame out;out.controllerInverse=controllerF0(current);out.starClockScale=profile.starClockScales.at(current.season);out.skyEnabled=(get<uint32_t>(current.main.data()+0x3b0)&0x80000000u)!=0;
 const auto* p=reinterpret_cast<const uint8_t*>(current.atmosphere.data());Matrix sky=get<Matrix>(p),localToGlobal=get<Matrix>(p+64);V4 projectedSun=get<V4>(p+128),moon=get<V4>(p+144),altitude=get<V4>(p+160);float offset=get<float>(p+176),sunSize=get<float>(p+180),moonSize=get<float>(p+184),sz=get<float>(p+188),mw=get<float>(p+192);
 V4 eye=get<V4>(current.main.data()+0x10),target=get<V4>(current.main.data()+0x20);float dx=S(target.X,eye.X),dy=S(target.Y,eye.Y),dz=S(target.Z,eye.Z),length=std::sqrt(A(A(M(dy,dy),M(dx,dx)),M(dz,dz))),raised=M(get<float>(current.main.data()+0x210),100000);V4 ordinaryEye=eye;eye.Y=A(eye.Y,raised);
 float clipNear=get<float>(current.main.data()+0x140),clipFar=get<float>(current.main.data()+0x144);bool reverse=(get<uint32_t>(current.main.data()+0x158)&2)!=0;
 float z=D(reverse?clipNear:clipFar,reverse?S(clipFar,clipNear):S(clipNear,clipFar));Matrix projection{-1,0,0,0,0,1,0,0,0,0,z,-1,0,0,M(z,reverse?clipFar:clipNear),0};
 auto& global=source_current_season_atmosphere::selected(current,0x80);auto shared=source_current_season_atmosphere::sharedMode7Profile2(source_current_season_atmosphere::packageFog(global,get<float>(current.main.data()+0x1ec)),source_current_season_atmosphere::lightRgb(global));
 auto moonMatrix=moonFace(get<V4>(p+280));source_atmosphere_camera::Packet lightPacket;put(lightPacket.cloudPixel.data()+476,get<float>(profile.cloudPs[0].data()+412));source_current_season_atmosphere::publishCloudLights(lightPacket,current,cloudLight);
 static constexpr std::array<V4,6> directions{{{1,0,0,1},{-1,0,0,1},{0,1,0,1},{0,-1,0,1},{0,0,1,1},{0,0,-1,1}}};static constexpr std::array<V4,6> ups{{{0,1,0,0},{0,1,0,0},{0,0,-1,0},{0,0,1,0},{0,1,0,0},{0,1,0,0}}};
 for(unsigned face=0;face<6;++face){auto& f=out.faces[face];auto axis=directions[face];V4 focus{A(M(axis.X,length),eye.X),A(A(M(axis.Y,length),ordinaryEye.Y),raised),A(M(axis.Z,length),eye.Z),1};f.eye=eye;f.view=lookTo(eye,focus,ups[face]);f.projection=projection;f.w2p=compose(f.view,projection);
 f.camera=current.main;put(f.camera.data()+0x10,eye);put(f.camera.data()+0x20,focus);put(f.camera.data()+0x30,ups[face]);put(f.camera.data()+0x40,f.view);put(f.camera.data()+0x80,f.view);put(f.camera.data()+0xc0,projection);put(f.camera.data()+0x100,f.w2p);put(f.camera.data()+0x148,F(0x3fc90fdb));put(f.camera.data()+0x14c,-1.f);put(f.camera.data()+0x150,0.f);
 f.skyVs=profile.skyVs[face];f.skyPs=profile.skyPs[face];f.cloudVs=profile.cloudVs[face];f.cloudPs=profile.cloudPs[face];f.starVs=profile.starVs[face];f.starGs=profile.starGs[face];f.starPs=profile.starPs[face];
 // Original rigid inverse and translation cancellation; not ordinaryMain rays.
 auto& v=f.view;Matrix rigid{v[0],v[4],v[8],0,v[1],v[5],v[9],0,v[2],v[6],v[10],0,flip(A(A(M(v[13],v[1]),M(v[12],v[0])),M(v[14],v[2]))),flip(A(A(M(v[13],v[5]),M(v[12],v[4])),M(v[14],v[6]))),flip(A(A(M(v[13],v[9]),M(v[12],v[8])),M(v[14],v[10]))),1};
 for(int row=0;row<4;++row){int at=row*4;float w=rigid[at+3];rigid[at]=A(M(w,M(eye.X,-1)),rigid[at]);rigid[at+1]=A(M(w,M(eye.Y,-1)),rigid[at+1]);rigid[at+2]=A(M(w,M(eye.Z,-1)),rigid[at+2]);}
 put(f.skyVs.data(),compose(compose(source_matrix::inverse(projection),rigid),sky));put(f.skyVs.data()+64,0.f);put(f.skyVs.data()+80,localToGlobal);
 float x=M(eye.X,.01f),y=A(A(M(eye.Y,.01f),altitude.Z),offset),e=M(eye.Z,.01f),w=eye.W;V4 pos{A(A(A(M(y,sky[4]),M(x,sky[0])),M(e,sky[8])),M(w,sky[12])),A(A(A(M(x,sky[1]),M(y,sky[5])),M(e,sky[9])),M(w,sky[13])),A(A(A(M(x,sky[2]),M(y,sky[6])),M(e,sky[10])),M(w,sky[14])),A(A(A(M(x,sky[3]),M(y,sky[7])),M(e,sky[11])),M(w,sky[15]))};
 put(f.skyPs.data(),shared.fog);put(f.skyPs.data()+16,shared.scat);put(f.skyPs.data()+80,pos);float pixel=D(F(0x3fc90fdb),float(profile.width*2)),sunRadius=M(M(sunSize,F(0x3c187400)),.5f),moonRadius=M(M(moonSize,F(0x3c142917)),.5f);
 put(f.skyPs.data()+96,V4{pixel>sunRadius?pixel:sunRadius,pixel>moonRadius?pixel:moonRadius,sz,mw});V4 package=get<V4>(global.data.data());put(f.skyPs.data()+124,package.X==moon.X&&package.Y==moon.Y&&package.Z==moon.Z&&package.W==moon.W?1.f:0.f);
 std::memcpy(f.skyPs.data()+128,p+232,64);put(f.skyPs.data()+192,moonMatrix);put(f.skyPs.data()+256,source_atmosphere_camera::screenPosition(projectedSun,f.w2p,F(0x3fc90fdb),-1,profile.width,profile.width,sunSize,F(0x3c187400)));
 put(f.skyPs.data()+272,source_atmosphere_camera::screenPosition({M(moon.X,-1),M(moon.Y,-1),M(moon.Z,-1),M(moon.W,-1)},f.w2p,F(0x3fc90fdb),-1,profile.width,profile.width,moonSize,F(0x3c142917)));put(f.skyPs.data()+288,altitude);put(f.skyPs.data()+332,M(M(get<float>(current.main.data()+0x278),100),.01f));
 put(f.cloudVs.data()+16,source_matrix::inverse(f.w2p));put(f.cloudPs.data(),package);put(f.cloudPs.data()+16,eye);put(f.cloudPs.data()+32,V4{});put(f.cloudPs.data()+48,shared.scat);std::memcpy(f.cloudPs.data()+384,lightPacket.cloudPixel.data()+448,64);put(f.cloudPs.data()+508,M(get<float>(current.main.data()+0x278),100));
 put(f.starVs.data(),compose(out.controllerInverse,f.w2p));float geometryThreshold=D(F(0x3b000000),cloudLight.geometry40);put(f.starVs.data()+344,geometryThreshold);put(f.starPs.data()+8,geometryThreshold);
 }
 return out;
}
} // namespace source_current_ambient


#ifndef SOURCE_CURRENT_AMBIENT_CPU_ONLY
struct NativeCurrentAmbientFrame {source_current_ambient::Profile profile;source_current_ambient::Frame current;
 std::array<std::vector<char>,6> skyVertex,skyPixel;};
static NativeCurrentAmbientFrame prepareNativeCurrentAmbientFrame(ID3D11Device* device,const fs::path& source,
 NativeAmbient& ambient,NativeAmbientCloudContinuous& cloud){NativeCurrentAmbientFrame r;r.profile=source_current_ambient::readProfile(source);
 auto make=[&](const auto& bytes){std::vector<char> data(bytes.size());std::memcpy(data.data(),bytes.data(),bytes.size());return nativeMainCloudBuffer(device,data);};
 for(UINT face=0;face<6;++face){r.skyVertex[face].assign(reinterpret_cast<const char*>(r.profile.skyVs[face].data()),reinterpret_cast<const char*>(r.profile.skyVs[face].data()+144));r.skyPixel[face].assign(reinterpret_cast<const char*>(r.profile.skyPs[face].data()),reinterpret_cast<const char*>(r.profile.skyPs[face].data()+336));
  ambient.skyConstants[face].vs=make(r.profile.skyVs[face]);ambient.skyConstants[face].ps=make(r.profile.skyPs[face]);
  // Keep the actual shared WORK Cloud byte cache/buffer used by its phase
  // updater. Only its immutable VS needs to become a current DEFAULT buffer.
  cloud.vertexConstants[face]=make(cloud.vertexBytes[face]);ambient.cloudConstants[face].vs=cloud.vertexConstants[face];
  ambient.starConstants[face].ps=make(ambient.starAnimation.faces[face].ps);
 }
 return r;
}
static void prepareNativeCurrentAmbientInputs(NativeCurrentAmbientFrame& owner,const source_current_season::Frame& current,
 const source_current_season_atmosphere::CloudLightInput& cloudLight){owner.current=source_current_ambient::prepareCurrent(owner.profile,current,cloudLight);}
static void publishNativeCurrentAmbientFrame(ID3D11DeviceContext* context,NativeCurrentAmbientFrame& owner,
 NativeAmbient& ambient,NativeAmbientCloudContinuous& cloud,const std::array<source_camera::V4,6>* currentCloudClip=nullptr){
 ambient.starAnimation.clockScale=owner.current.starClockScale;ambient.skyEnabled=owner.current.skyEnabled?1:0;
 for(UINT face=0;face<6;++face){const auto& current=owner.current.faces[face];
  std::memcpy(owner.skyVertex[face].data(),current.skyVs.data(),144);std::memcpy(owner.skyPixel[face].data(),current.skyPs.data(),336);
  context->UpdateSubresource(ambient.skyConstants[face].vs.Get(),0,nullptr,owner.skyVertex[face].data(),0,0);context->UpdateSubresource(ambient.skyConstants[face].ps.Get(),0,nullptr,owner.skyPixel[face].data(),0,0);
  std::memcpy(cloud.vertexBytes[face].data()+16,current.cloudVs.data()+16,144);
  //1196650 is supplied by its independently closed current face producer.
  //Absent input preserves the existing clip cache explicitly; it is not a
  //claim that the old plane is current after the camera changes.
  if(currentCloudClip&&(face==0||face==1||face==4||face==5))std::memcpy(cloud.vertexBytes[face].data(),&(*currentCloudClip)[face],16);
  std::memcpy(cloud.pixelBytes[face].data(),current.cloudPs.data(),112);std::memcpy(cloud.pixelBytes[face].data()+384,current.cloudPs.data()+384,64);std::memcpy(cloud.pixelBytes[face].data()+508,current.cloudPs.data()+508,4);
  context->UpdateSubresource(cloud.vertexConstants[face].Get(),0,nullptr,cloud.vertexBytes[face].data(),0,0);
  //Existing updateNativeAmbientCloudContinuous uploads this same PCB after
  //consuming node3's already advanced offsets. Do not overwrite weather/phase.
  auto& star=ambient.starAnimation.faces[face];std::memcpy(star.vs.data(),current.starVs.data(),64);std::memcpy(star.vs.data()+336,current.starVs.data()+336,16);std::memcpy(star.ps.data(),current.starPs.data(),16);
  ambient.starAnimation.faceHeight[face]=current.eye.Y;
  context->UpdateSubresource(ambient.starConstants[face].ps.Get(),0,nullptr,star.ps.data(),0,0);
  //StarVS64 phase/table80 remain owned; its normal actual-face draw uploads
  //the updated completeVS after precisely one11902E0 phase step.
 }
}
#endif
