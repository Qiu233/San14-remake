#pragma once
// Weather response, atmospheric scattering and altitude-based ambient lighting.
// Binary profiles and fog records are decoded at the resource boundaries.
#include "WeatherFogState.h"

namespace source_weather_fog {
using source_camera::A;using source_camera::S;using source_camera::M;using source_camera::D;
using source_camera::V3;using source_camera::V4;
inline float F(uint32_t bits){float value;std::memcpy(&value,&bits,4);return value;}
inline constexpr float AmbientTableScale=1e-5f;
inline constexpr float CameraHeightScale=.01f;
inline constexpr float BackgroundIntensity=.3f;
inline const float MinimumFogDistance=F(0x37480000);
inline const float IsotropicMiePhase=F(0x3da2f983); // 1/(4*pi), rounded to float32.
inline const float RayleighPhase=F(0x3d747645); // 3/(16*pi), rounded to float32.
__declspec(noinline) inline double DA(double a,double b){return a+b;}
__declspec(noinline) inline double DS(double a,double b){return a-b;}
__declspec(noinline) inline double DM(double a,double b){return a*b;}
__declspec(noinline) inline double DD(double a,double b){return a/b;}
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
    if(p.lighting.atmosphereTop<effectiveHeight)return {1,1,1,1};auto sample=sampleGrid(p,heightCoordinate,angle);float scale=AmbientTableScale;
    return {maximum(0,M(sample.X,scale)),maximum(0,M(sample.Y,scale)),maximum(0,M(sample.Z,scale)),maximum(0,M(sample.W,scale))};
}
inline Frame prepareCurrent(const Profile& p,float eyeY,float currentAltitude){
    const auto& response=p.response;
    const auto& settings=p.fog;
    const auto& coefficients=p.scattering;
    Frame out;
    float cloud=clamp(M(p.conditions.cloudCover,response.cloudScale));
    float humidityWeight=response.cloudHumidityEnd>response.cloudHumidityStart?clamp(D(S(p.conditions.humidity,response.cloudHumidityStart),S(response.cloudHumidityEnd,response.cloudHumidityStart))):p.conditions.humidity>=response.cloudHumidityStart?1.f:0.f;
    float weather=M(cloud,humidityWeight),strength=response.strength,heightBlend=A(M(strength,response.heightMultiplier),S(1,strength));
    float opacity=powSse(response.opacityBase,strength,p.power);
    float weight=response.scatteringHumidityEnd==response.scatteringHumidityStart?(p.conditions.humidity>response.scatteringHumidityStart?1.f:0.f):clamp(D(S(p.conditions.humidity,response.scatteringHumidityStart),S(response.scatteringHumidityEnd,response.scatteringHumidityStart)));
    float complement=S(1,weight);
    float rayleighScale=A(M(weight,response.rayleighWetScale),M(complement,response.rayleighDryScale)),heightDenominator=M(M(heightBlend,response.atmosphereHeight),100);
    float raised=powSse(D(eyeY,heightDenominator),4,p.power),density=A(M(maximum(raised,1),strength),S(1,strength));
    density=M(density,opacity);density=M(density,A(M(weight,response.mieWetScale),M(complement,response.mieDryScale)));
    float mieMaximum=maximum(coefficients.mieExtinction.X,maximum(coefficients.mieExtinction.Z,coefficients.mieExtinction.Y));
    V4 mie{M(M(density,coefficients.mie.X),D(coefficients.mieExtinction.X,mieMaximum)),M(M(density,coefficients.mie.Y),D(coefficients.mieExtinction.Y,mieMaximum)),M(M(density,coefficients.mie.Z),D(coefficients.mieExtinction.Z,mieMaximum)),0};
    V4 rayleigh{M(M(coefficients.rayleigh.X,coefficients.rayleighExtinction.X),rayleighScale),M(M(coefficients.rayleigh.Y,coefficients.rayleighExtinction.Y),rayleighScale),M(M(coefficients.rayleigh.Z,coefficients.rayleighExtinction.Z),rayleighScale),0};
    out.saturation=response.saturationStart==response.saturationEnd?(weather>response.saturationEnd?0.f:1.f):S(1,clamp(D(S(weather,response.saturationStart),S(response.saturationEnd,response.saturationStart))));rayleigh=adjust(rayleigh,out.saturation);
    out.currentAltitude=currentAltitude;out.effectiveHeight=A(M(currentAltitude,1000),M(eyeY,CameraHeightScale));
    out.heightCoordinate=clamp(powSse(clamp(D(out.effectiveHeight,p.lighting.atmosphereTop)),.5f,p.power));
    out.sunAmbient=sampleAmbient(p,out.effectiveHeight,out.heightCoordinate,p.lighting.sunAngle);out.moonAmbient=sampleAmbient(p,out.effectiveHeight,out.heightCoordinate,p.lighting.moonAngle);
    V4 sun=adjust(out.sunAmbient,out.saturation),moon=adjust(out.moonAmbient,out.saturation);
    V4 background{M(A(M(moon.X,p.lighting.moonRgb.X),M(sun.X,p.lighting.sunRgb.X)),BackgroundIntensity),M(A(M(moon.Y,p.lighting.moonRgb.Y),M(sun.Y,p.lighting.sunRgb.Y)),BackgroundIntensity),M(A(M(moon.Z,p.lighting.moonRgb.Z),M(sun.Z,p.lighting.sunRgb.Z)),BackgroundIntensity),0};
    out.fog={background,FogMode::Scattering,M(settings.startDistance,100),M(M(heightBlend,response.atmosphereHeight),100),
        p.lighting.planetRadius,{rayleigh.X,rayleigh.Y,rayleigh.Z},1.f,{mie.X,mie.Y,mie.Z},
        M(S(1,M(strength,BackgroundIntensity)),settings.anisotropy),M(settings.distancePivot,100),
        A(M(settings.blendWeight,settings.blendTarget),S(1,settings.blendWeight))};
    return out;
}
inline Frame prepareCurrent(const Profile& p,float eyeY){return prepareCurrent(p,eyeY,p.conditions.altitude);}
inline Shared shared(const FogState& record,V3 directionalRgb){
    Shared out;out.color=record.background;float pivot=record.distancePivot,minimum=MinimumFogDistance,x=pivot>minimum?pivot:minimum;
    out.fog={x,D(1,x),record.blend,record.planetRadius};
    V4 ray{record.rayleigh.X,record.rayleigh.Y,record.rayleigh.Z,record.density},mie{record.mie.X,record.mie.Y,record.mie.Z,record.anisotropy};float sx=A(ray.X,mie.X),sy=A(mie.Y,ray.Y),sz=A(mie.Z,ray.Z);
    if(ray.X>0)sx=D(ray.W,sx);if(ray.Y>0)sy=D(ray.W,sy);if(ray.Z>0)sz=D(ray.W,sz);
    sy=M(sy,directionalRgb.Y);sx=M(sx,directionalRgb.X);sz=M(sz,directionalRgb.Z);
    float g2=M(mie.W,mie.W),phase=M(S(1,g2),IsotropicMiePhase);
    out.scat={V4{ray.X,ray.Y,ray.Z,record.startDistance},V4{mie.X,mie.Y,mie.Z,record.endDistance},
        V4{M(sx,M(ray.X,RayleighPhase)),M(sy,M(ray.Y,RayleighPhase)),M(sz,M(ray.Z,RayleighPhase)),M(mie.W,-2)},
        V4{M(sx,M(phase,mie.X)),M(sy,M(phase,mie.Y)),M(sz,M(phase,mie.Z)),A(g2,1)}};return out;
}
inline FogState package(const Frame& frame,float fogBlend){
    auto result=frame.fog;
    if(fogBlend>0)result.blend=A(M(S(1,fogBlend),result.blend),fogBlend);
    return result;
}
} // namespace source_weather_fog
