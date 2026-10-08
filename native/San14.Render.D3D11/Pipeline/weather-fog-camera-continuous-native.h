#pragma once
// Actual current V418 weather/Fog producer. The raw table and original pow
// coefficients are imported once; accepted camera height is the runtime input.
#include "camera-continuous-native.h"

namespace source_weather_fog {
using source_camera::A;using source_camera::S;using source_camera::M;using source_camera::D;
using source_camera::V3;using source_camera::V4;using source_camera::Prefix;using source_camera::get;using source_camera::put;
inline float F(uint32_t bits){float value;std::memcpy(&value,&bits,4);return value;}
__declspec(noinline) inline double DA(double a,double b){return a+b;}
__declspec(noinline) inline double DS(double a,double b){return a-b;}
__declspec(noinline) inline double DM(double a,double b){return a*b;}
__declspec(noinline) inline double DD(double a,double b){return a/b;}
struct Power {
    std::array<double,257> reciprocal{},logarithms{};
    std::array<double,64> exponentials{};std::array<double,11> constants{};
};
struct Profile {
    std::array<float,46> fields{};Power power;
    std::vector<uint16_t> ambient;
};
struct Frame {
    std::array<uint8_t,72> main72{};float saturation=0,effectiveHeight=0,heightCoordinate=0,currentAltitude=0;
    V4 sunAmbient{},moonAmbient{};
};
struct Shared {V4 fog{},color{};std::array<V4,4> scat{};};
inline Profile readProfile(const std::filesystem::path& source){
    Profile p;std::ifstream s(source/"profile.bin",std::ios::binary);
    p.fields=source_camera::read<std::array<float,46>>(s);
    p.power.reciprocal=source_camera::read<std::array<double,257>>(s);p.power.logarithms=source_camera::read<std::array<double,257>>(s);
    p.power.exponentials=source_camera::read<std::array<double,64>>(s);p.power.constants=source_camera::read<std::array<double,11>>(s);
    if(s.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Weather Fog profile tail");
    std::ifstream table(source/"ambient2d-half4.bin",std::ios::binary);p.ambient.resize(64*256*4);
    table.read(reinterpret_cast<char*>(p.ambient.data()),p.ambient.size()*sizeof(uint16_t));
    if(!table||table.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Weather Ambient2D raw shape");return p;
}
inline float powSse(float value,float exponent,const Power& source){
    if(exponent==0)return 1;if(exponent==1)return value;if(value==0)return 0;
    double x=value,log,t=DS(x,1);
    if(value<1.0625f&&std::fabs(t)<.0625){
        double ratio=DD(t,DA(t,2)),product=DM(ratio,t);ratio=DA(ratio,ratio);
        double square=DM(ratio,ratio),cube=DM(ratio,square),fifth=DM(DM(square,square),cube);
        double p=DM(square,source.constants[7]),q=DM(square,source.constants[8]);
        p=DA(p,source.constants[9]);q=DA(q,source.constants[10]);
        double correction=DA(DM(cube,p),DM(fifth,q));log=DA(t,DS(correction,product));
    }else{
        uint64_t bits;std::memcpy(&bits,&x,8);uint64_t mantissa=bits&0x000fffffffffffffull;
        int i=int((mantissa>>44)+((mantissa>>43)&1));uint64_t normalizedBits=mantissa|0x3fe0000000000000ull,centerBits=(uint64_t(i)|0x3fe00ull)<<44;
        double normalized,center;std::memcpy(&normalized,&normalizedBits,8);std::memcpy(&center,&centerBits,8);
        double r=DM(DS(center,normalized),source.reciprocal[i]),poly=DM(r,source.constants[0]);poly=DA(poly,.5);poly=DM(DM(r,r),poly);poly=DA(poly,r);
        int e=int(bits>>52)-1023;log=DS(DA(DM(double(e),source.constants[1]),source.logarithms[i]),poly);
    }
    double y=DM(double(exponent),log),scaled=DM(y,source.constants[6]);
    int n=_mm_cvtsd_si32(_mm_set_sd(scaled)); // Original nearest-even SSE integer reduction.
    double remainder=DS(y,DM(double(n),source.constants[4])),ep=DM(remainder,source.constants[5]);ep=DA(ep,.5);
    ep=DM(DM(remainder,remainder),ep);ep=DA(ep,remainder);
    double centerExp=source.exponentials[n&63],result=DA(DM(ep,centerExp),centerExp);
    uint64_t resultBits;std::memcpy(&resultBits,&result,8);resultBits+=uint64_t(int64_t(n>>6))<<52;std::memcpy(&result,&resultBits,8);return float(result);
}
inline float decodeHalf(uint16_t value){
    uint32_t sign=value&0x8000u,mantissa=value&0x3ffu,exponent=value&0x7c00u;
    if(exponent!=0)return F(((sign*8)|mantissa)<<13|(exponent+0x1c000)<<13);
    return M(M(sign==0?1.f:-1.f,F(0x38800000)),S(F((mantissa|0x1fc00)<<13),1));
}
inline float maximum(float a,float b){return a>b?a:b;}
inline float clamp(float x){return x>=1?1:maximum(0,x);}
inline V4 sampleGrid(const Profile& p,float height,float angle){
    float x=M(height,64),y=M(angle,256);int rawX=int(x),rawY=int(y);
    float fx=S(x,float(rawX)),fy=S(y,float(rawY));int bx=std::clamp(rawX,0,63),by=std::clamp(rawY,0,255);
    int nx=std::clamp(rawX+(fx>=.5f?1:-1),0,63),ny=std::clamp(rawY+(fy>=.5f?1:-1),0,255);
    float dx=std::fabs(S(A(float(rawX),.5f),x)),dy=std::fabs(S(A(float(rawY),.5f),y));
    auto channel=[&](int cx,int cy,int lane){return decodeHalf(p.ambient[(cy*64+cx)*4+lane]);};V4 out;float* lanes=&out.X;
    for(int lane=0;lane<4;++lane){float first=channel(bx,by,lane);
        if(dx<=0){lanes[lane]=dy<=0?first:A(M(first,S(1,dy)),M(channel(bx,ny,lane),dy));continue;}
        float row1=A(M(S(1,dx),first),M(channel(nx,by,lane),dx));if(dy<=0){lanes[lane]=row1;continue;}
        float row2=A(M(S(1,dx),channel(bx,ny,lane)),M(channel(nx,ny,lane),dx));lanes[lane]=A(M(row2,dy),M(S(1,dy),row1));
    }return out;
}
inline V4 rgbToHsv(V4 rgb){
    float m,r,h=0;
    if(rgb.Y>rgb.X){if(rgb.Z>rgb.Y){m=rgb.Z;r=S(rgb.Z,rgb.X);if(r>0)h=A(D(M(S(rgb.X,rgb.Y),60),r),240);}
        else{m=rgb.Y;r=S(rgb.Y,rgb.Z>rgb.X?rgb.X:rgb.Z);if(r>0)h=A(D(M(S(rgb.Z,rgb.X),60),r),120);}}
    else if(rgb.Z>rgb.X){m=rgb.Z;r=S(rgb.Z,rgb.Y);if(r>0)h=A(D(M(S(rgb.X,rgb.Y),60),r),240);}
    else{m=rgb.X;r=S(rgb.X,rgb.Z>rgb.Y?rgb.Y:rgb.Z);if(r>0)h=D(M(S(rgb.Y,rgb.Z),60),r);}
    if(h<0)h=A(h,360);else if(h>360)h=A(h,-360);return {D(h,360),m==0?0:D(r,m),m,rgb.W};
}
inline V4 hsvToRgb(V4 hsv){
    if(hsv.Y==0)return {hsv.Z,hsv.Z,hsv.Z,hsv.W};float six=M(hsv.X,6);uint32_t sector=uint32_t(uint64_t(six)%6);
    float f=hsv.X<1?S(six,float(sector)):0,p=M(S(1,hsv.Y),hsv.Z),q=M(S(1,M(hsv.Y,f)),hsv.Z),t=M(S(1,M(S(1,f),hsv.Y)),hsv.Z);
    switch(sector){case 0:return {hsv.Z,t,p,hsv.W};case 1:return {q,hsv.Z,p,hsv.W};case 2:return {p,hsv.Z,t,hsv.W};case 3:return {p,q,hsv.Z,hsv.W};case 4:return {t,p,hsv.Z,hsv.W};default:return {hsv.Z,p,q,hsv.W};}
}
inline V4 adjust(V4 rgb,float saturation){if(saturation>=1)return rgb;auto hsv=rgbToHsv(rgb);hsv.Y=M(hsv.Y,saturation);return hsvToRgb(hsv);}
inline V4 sampleAmbient(const Profile& p,float effectiveHeight,float heightCoordinate,float angle){
    if(p.fields[35]<effectiveHeight)return {1,1,1,1};auto sample=sampleGrid(p,heightCoordinate,angle);float scale=F(0x3727c5ac);
    return {maximum(0,M(sample.X,scale)),maximum(0,M(sample.Y,scale)),maximum(0,M(sample.Z,scale)),maximum(0,M(sample.W,scale))};
}
inline Frame prepareCurrent(const Profile& p,float eyeY,float currentAltitude){
    const auto& f=p.fields;Frame out;
    float cloud=clamp(M(f[0],f[2])),humidityWeight=f[4]>f[3]?clamp(D(S(f[1],f[3]),S(f[4],f[3]))):f[1]>=f[3]?1.f:0.f;
    float weather=M(cloud,humidityWeight),strength=f[5],heightBlend=A(M(strength,f[6]),S(1,strength));
    float opacity=powSse(f[7],strength,p.power),weight=f[9]==f[8]?(f[1]>f[8]?1.f:0.f):clamp(D(S(f[1],f[8]),S(f[9],f[8]))),complement=S(1,weight);
    float rayleighScale=A(M(weight,f[11]),M(complement,f[10])),heightDenominator=M(M(heightBlend,f[14]),100);
    float raised=powSse(D(eyeY,heightDenominator),4,p.power),density=A(M(maximum(raised,1),strength),S(1,strength));
    density=M(density,opacity);density=M(density,A(M(weight,f[13]),M(complement,f[12])));
    float mieMaximum=maximum(f[28],maximum(f[30],f[29]));
    V4 mie{M(M(density,f[22]),D(f[28],mieMaximum)),M(M(density,f[23]),D(f[29],mieMaximum)),M(M(density,f[24]),D(f[30],mieMaximum)),0};
    V4 rayleigh{M(M(f[25],f[31]),rayleighScale),M(M(f[26],f[32]),rayleighScale),M(M(f[27],f[33]),rayleighScale),0};
    out.saturation=f[15]==f[16]?(weather>f[16]?0.f:1.f):S(1,clamp(D(S(weather,f[15]),S(f[16],f[15]))));rayleigh=adjust(rayleigh,out.saturation);
    out.currentAltitude=currentAltitude;out.effectiveHeight=A(M(currentAltitude,1000),M(eyeY,F(0x3c23d70a)));
    out.heightCoordinate=clamp(powSse(clamp(D(out.effectiveHeight,f[35])),.5f,p.power));
    out.sunAmbient=sampleAmbient(p,out.effectiveHeight,out.heightCoordinate,f[37]);out.moonAmbient=sampleAmbient(p,out.effectiveHeight,out.heightCoordinate,f[38]);
    V4 sun=adjust(out.sunAmbient,out.saturation),moon=adjust(out.moonAmbient,out.saturation);
    V4 background{M(A(M(moon.X,f[42]),M(sun.X,f[39])),F(0x3e99999a)),M(A(M(moon.Y,f[43]),M(sun.Y,f[40])),F(0x3e99999a)),M(A(M(moon.Z,f[44]),M(sun.Z,f[41])),F(0x3e99999a)),0};
    put(out.main72.data(),background);put(out.main72.data()+0x10,uint32_t(7));put(out.main72.data()+0x14,M(f[17],100));
    put(out.main72.data()+0x18,M(M(heightBlend,f[14]),100));put(out.main72.data()+0x1c,f[34]);put(out.main72.data()+0x20,rayleigh);put(out.main72.data()+0x2c,1.f);
    put(out.main72.data()+0x30,mie);put(out.main72.data()+0x3c,M(S(1,M(strength,F(0x3e99999a))),f[18]));
    put(out.main72.data()+0x40,M(f[19],100));put(out.main72.data()+0x44,A(M(f[20],f[21]),S(1,f[20])));return out;
}
inline Frame prepareCurrent(const Profile& p,float eyeY){return prepareCurrent(p,eyeY,p.fields[36]);}
inline void publish(Prefix& preparedMain,const Frame& frame){put(preparedMain.data()+0x210,frame.currentAltitude);put(preparedMain.data()+0x22c,frame.saturation);std::memcpy(preparedMain.data()+0x340,frame.main72.data(),72);}
inline void publishMode6CameraFog(uint8_t* admittedCbc770LightData,const Frame& frame){
    // Only the actual admitted CBC770 mode6 Camera-copy scope calls this.
    // Raw mode0 light records must retain their own Fog96 instead.
    std::memcpy(admittedCbc770LightData+0x150,frame.main72.data(),72);
}
inline Shared shared(const std::array<uint8_t,72>& record,V3 directionalRgb){
    Shared out;out.color=get<V4>(record.data());float pivot=get<float>(record.data()+0x40),minimum=F(0x37480000),x=pivot>minimum?pivot:minimum;
    out.fog={x,D(1,x),get<float>(record.data()+0x44),get<float>(record.data()+0x1c)};
    V4 ray=get<V4>(record.data()+0x20),mie=get<V4>(record.data()+0x30);float sx=A(ray.X,mie.X),sy=A(mie.Y,ray.Y),sz=A(mie.Z,ray.Z);
    if(ray.X>0)sx=D(ray.W,sx);if(ray.Y>0)sy=D(ray.W,sy);if(ray.Z>0)sz=D(ray.W,sz);
    sy=M(sy,directionalRgb.Y);sx=M(sx,directionalRgb.X);sz=M(sz,directionalRgb.Z);
    float g2=M(mie.W,mie.W),phase=M(S(1,g2),F(0x3da2f983));
    out.scat={V4{ray.X,ray.Y,ray.Z,get<float>(record.data()+0x14)},V4{mie.X,mie.Y,mie.Z,get<float>(record.data()+0x18)},
        V4{M(sx,M(ray.X,F(0x3d747645))),M(sy,M(ray.Y,F(0x3d747645))),M(sz,M(ray.Z,F(0x3d747645))),M(mie.W,-2)},
        V4{M(sx,M(phase,mie.X)),M(sy,M(phase,mie.Y)),M(sz,M(phase,mie.Z)),A(g2,1)}};return out;
}
inline std::array<uint8_t,72> package(const Frame& frame,float main1EC){
    auto record=frame.main72;if(main1EC>0){float blend=get<float>(record.data()+0x44);put(record.data()+0x44,A(M(S(1,main1EC),blend),main1EC));}return record;
}
} // namespace source_weather_fog
