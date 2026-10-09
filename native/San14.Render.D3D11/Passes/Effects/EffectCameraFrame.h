#pragma once
// Camera-only producer for the fixed observed CPU particle cohort. No emitter tick.
#include "../../Camera/CameraState.h"
#include "EffectCameraConstants.h"
#include <string>

namespace source_effect_camera {
using source_camera::A;using source_camera::S;using source_camera::M;using source_camera::D;
using source_camera::get;using source_camera::put;using source_camera::Matrix;using source_camera::V4;
using Prepared=std::array<uint8_t,0x1f0>;
struct Candidate {
    unsigned family{},ordinal{},world{},leaf{},particleOrdinal{};float bias{};std::array<float,3> extent{};
    std::array<char,9> group{},texture{};std::array<uint8_t,0xa0> particle{};
    std::array<uint8_t,64> meshInstance{};std::array<uint8_t,56> row{},mask{};
};
struct Profile {unsigned buckets{},queueEntries{},rows{},dynamicUnits32{};float tangent{};std::vector<Candidate> candidates;};
struct Draw {unsigned ordinal{},candidate{},pass{},bucket{},byteOffset{},textureIndex{};float normalized{},depth{};};
struct Frame {Prepared camera{};V4 writerEye{};std::vector<Draw> draws;std::vector<uint8_t> stream,rows,masks;unsigned sprites{},meshes{};};
inline Profile loadProfile(const std::filesystem::path& path){
    using source_camera::read;std::ifstream s(path,std::ios::binary);
    if(!s||read<unsigned>(s)!=0x31434645u)throw std::runtime_error("Effect camera EFC1 profile absent");
    unsigned count=read<unsigned>(s);Profile p;p.buckets=read<unsigned>(s);p.queueEntries=read<unsigned>(s);
    p.rows=read<unsigned>(s);p.dynamicUnits32=read<unsigned>(s);p.tangent=read<float>(s);p.candidates.reserve(count);
    for(unsigned i=0;i<count;i++){
        Candidate c;c.family=read<unsigned>(s);c.ordinal=read<unsigned>(s);c.world=read<unsigned>(s);
        c.leaf=read<unsigned>(s);c.particleOrdinal=read<unsigned>(s);c.bias=read<float>(s);c.extent=read<std::array<float,3>>(s);
        c.group=read<std::array<char,9>>(s);c.texture=read<std::array<char,9>>(s);c.particle=read<std::array<uint8_t,0xa0>>(s);
        c.meshInstance=read<std::array<uint8_t,64>>(s);c.row=read<std::array<uint8_t,56>>(s);c.mask=read<std::array<uint8_t,56>>(s);
        if(c.family!=2&&c.family!=7)throw std::runtime_error("Effect camera fixed source family unsupported");
        p.candidates.push_back(c);
    }return p;
}
inline Prepared prepareCamera(const Profile& p,const source_camera::Prefix& main,V4& eye){
    auto raw=source_camera::lookTo(get<V4>(main.data()+0x10),get<V4>(main.data()+0x20),get<V4>(main.data()+0x30)),view=raw;
    float roll=get<float>(main.data()+0x150);
    if(roll!=0){auto trig=source_camera::sinCos(source_camera::flip(roll));for(unsigned r=0;r<4;r++){
        float x=raw[r*4],y=raw[r*4+1];view[r*4]=S(M(x,trig[1]),M(y,trig[0]));view[r*4+1]=A(M(x,trig[0]),M(y,trig[1]));}}
    float sourceNear=get<float>(main.data()+0x140),sourceFar=get<float>(main.data()+0x144),aspect=get<float>(main.data()+0x14c);
    unsigned flags=get<unsigned>(main.data()+0x158);if(flags&1)throw std::runtime_error("Effect camera profile uses perspective Main");
    float na=flags&2?sourceFar:sourceNear,fa=flags&2?sourceNear:sourceFar,y=D(1,p.tangent),range=S(na,fa),z=D(fa,range);
    Matrix d3d{D(y,aspect),0,0,0,0,y,0,0,0,0,z,-1,0,0,M(z,na),0};
    float tf=M(p.tangent,sourceFar),span=S(sourceFar,sourceNear);
    Matrix projection{D(1,M(tf,aspect)),0,0,0,0,D(1,tf),0,0,0,0,
        source_camera::flip(D(A(sourceNear,sourceFar),M(span,sourceFar))),D(-1,sourceFar),0,0,D(M(sourceNear,-2),span),0};
    Prepared out{};std::memcpy(out.data()+0x10,main.data()+0x10,0x150);
    put(out.data()+0x40,raw);put(out.data()+0x80,view);put(out.data()+0xc0,d3d);put(out.data()+0x100,source_camera::compose(view,d3d));
    put(out.data()+0x170,projection);put(out.data()+0x1b0,source_camera::compose(view,projection));
    // AA4A00 3F66E0 CameraPOD70 inverse translation, not Main raw Eye10.
    eye={source_camera::flip(A(A(M(view[13],view[1]),M(view[12],view[0])),M(view[14],view[2]))),
         source_camera::flip(A(A(M(view[13],view[5]),M(view[12],view[4])),M(view[14],view[6]))),
         source_camera::flip(A(A(M(view[13],view[9]),M(view[12],view[8])),M(view[14],view[10]))),1};
    return out;
}
inline float dot(const std::array<float,4>& a,const std::array<float,4>& b){return A(A(A(M(a[1],b[1]),M(a[0],b[0])),M(a[2],b[2])),M(a[3],b[3]));}
inline std::array<float,4> vector(const uint8_t* p){return get<std::array<float,4>>(p);}
inline bool cull(const Candidate& c,const Prepared& camera,float& depth){
    const auto& p=c.particle;auto clip=get<Matrix>(camera.data()+0x1b0);std::array<float,4> pos{},radius{};
    if(c.family==2){
        float width=M(get<float>(p.data()+0x34),.5f),height=M(get<float>(p.data()+0x38),.5f);
        auto x=vector(p.data()+0x50),y=vector(p.data()+0x60),center=vector(p.data()+0x80);for(float& v:x)v=M(v,width);
        for(unsigned k=0;k<4;k++){std::array<float,4> col{clip[k],clip[4+k],clip[8+k],clip[12+k]};
            radius[k]=A(std::fabs(dot(x,col)),M(std::fabs(dot(y,col)),height));pos[k]=dot(center,col);}
    }else{
        auto transformed=source_camera::compose(get<Matrix>(p.data()+0x50),clip);std::array<float,3> extent{};
        for(unsigned k=0;k<3;k++)extent[k]=M(get<float>(p.data()+0x34+k*4),c.extent[k]);
        for(unsigned k=0;k<4;k++){radius[k]=A(A(M(std::fabs(transformed[k]),extent[0]),M(std::fabs(transformed[4+k]),extent[1])),M(std::fabs(transformed[8+k]),extent[2]));pos[k]=transformed[12+k];}
    }
    float w=A(std::fabs(pos[3]),radius[3]),x=A(radius[0],w),y=A(radius[1],w);
    if(!(source_camera::flip(std::fabs(S(pos[3],radius[3])))<A(radius[2],pos[2])&&S(pos[2],radius[2])<w
         &&-x<pos[0]&&pos[0]<x&&-y<pos[1]&&pos[1]<y)||(p[6]&2)==0)return false;
    float key=w<=.9999881f?w:.9999881f;
    depth=A(key,M(M(S(source_camera::flip(get<float>(camera.data()+0x198)),get<float>(camera.data()+0x19c)),.5f),c.bias));return true;
}
inline float logLinear(float v){if(v<=0)return -128;int exponent;float mantissa=static_cast<float>(std::frexp(static_cast<double>(v),&exponent));return S(A(A(mantissa,mantissa),static_cast<float>(exponent)),2);}
inline void spriteVertices(const Candidate& c,V4 eye,std::vector<uint8_t>& target){
    const uint8_t* p=c.particle.data()+0x20;unsigned flags=get<uint16_t>(p+2),kind=get<uint16_t>(p+10);
    if((flags&0x255)||(kind!=7&&kind!=26))throw std::runtime_error("Unsupported fixed Effect sprite branch");
    auto x=vector(p+0x30),y=vector(p+0x40),normal=vector(p+0x50),center=vector(p+0x60);float width=M(get<float>(p+0x14),.5f),height=M(get<float>(p+0x18),.5f);
    for(unsigned k=0;k<4;k++){x[k]=M(x[k],width);y[k]=M(y[k],height);}
    std::array<std::array<float,4>,4> positions{};for(unsigned k=0;k<4;k++){
        positions[0][k]=A(S(center[k],x[k]),y[k]);positions[1][k]=S(S(center[k],x[k]),y[k]);
        positions[2][k]=A(A(x[k],center[k]),y[k]);positions[3][k]=S(A(x[k],center[k]),y[k]);}
    unsigned index=(p[0xe]+((flags&0x20)?128:0))&255,packed=index<<24;
    if(kind==26){std::array<float,3> delta{S(eye.X,center[0]),S(eye.Y,center[1]),S(eye.Z,center[2])};
        float length=std::sqrt(A(A(M(normal[0],normal[0]),M(normal[1],normal[1])),M(normal[2],normal[2]))),inv=D(1,length);
        float side=A(A(M(delta[1],normal[1]),M(delta[0],normal[0])),M(delta[2],normal[2]))<0?-127.5f:127.5f;
        unsigned rgb[3];for(unsigned k=0;k<3;k++)rgb[k]=static_cast<unsigned>(std::clamp(A(M(M(normal[k],inv),side),127.5f),0.f,255.f));
        packed|=rgb[2]|(rgb[1]<<8)|(rgb[0]<<16);for(auto& v:positions)v[3]=get<float>(p+0x1c);}
    float u0=get<float>(p+0x20),v0=get<float>(p+0x24),u1=get<float>(p+0x28),v1=get<float>(p+0x2c);
    const std::array<std::array<float,2>,4> uv{{{u0,v0},{u0,v1},{u1,v0},{u1,v1}}};size_t start=target.size();target.resize(start+128);
    for(unsigned k=0;k<4;k++){uint8_t* out=target.data()+start+k*32;put(out,positions[k]);put(out+16,get<unsigned>(p+0x10));put(out+20,packed);put(out+24,uv[k]);}
}
template<class CandidateAdmission>
inline Frame prepareCurrent(const Profile& p,const source_camera::Prefix& main,CandidateAdmission admit){
    Frame frame;frame.camera=prepareCamera(p,main,frame.writerEye);float scale=D(1,logLinear(D(get<float>(frame.camera.data()+0x144),get<float>(frame.camera.data()+0x140))));
    for(unsigned i=0;i<p.candidates.size();i++){const auto& c=p.candidates[i];if(!admit(i,c))continue;float depth;if(!cull(c,frame.camera,depth))continue;
        float normalized=A(M(logLinear(depth),scale),1);if(!(normalized>=0&&normalized<1))continue;
        unsigned bucket=static_cast<unsigned>(M(static_cast<float>(p.buckets),normalized));
        frame.draws.push_back({c.ordinal,i,get<uint16_t>(c.row.data()+0x2a),bucket,0,get<uint16_t>(c.row.data()+0x28),normalized,depth});}
    std::sort(frame.draws.begin(),frame.draws.end(),[](const Draw& a,const Draw& b){return a.bucket!=b.bucket?a.bucket>b.bucket:a.ordinal>b.ordinal;});
    frame.stream.reserve(frame.draws.size()*128);frame.rows.reserve(frame.draws.size()*56);frame.masks.reserve(frame.draws.size()*56);
    for(auto& d:frame.draws){const auto& c=p.candidates[d.candidate];d.byteOffset=static_cast<unsigned>(frame.stream.size());
        if(c.family==2){spriteVertices(c,frame.writerEye,frame.stream);frame.sprites++;}else{frame.stream.insert(frame.stream.end(),c.meshInstance.begin(),c.meshInstance.end());frame.meshes++;}
        auto row=c.row,mask=c.mask;put(row.data()+4,d.byteOffset>>5);std::fill(mask.begin()+4,mask.begin()+8,uint8_t(255));
        frame.rows.insert(frame.rows.end(),row.begin(),row.end());frame.masks.insert(frame.masks.end(),mask.begin(),mask.end());}
    if(frame.draws.size()>p.queueEntries||frame.draws.size()>p.rows||frame.stream.size()>size_t(p.dynamicUnits32)*32)throw std::runtime_error("Effect camera source queue capacity exceeded");
    return frame;
}
inline Frame prepareCurrent(const Profile& p,const source_camera::Prefix& main){
    return prepareCurrent(p,main,[](unsigned,const Candidate&){return true;});
}
} // namespace source_effect_camera

#ifndef SAN14_EFFECT_CAMERA_CPU_ONLY
struct NativeEffectCameraContinuous {
    source_effect_camera::Profile profile;
    struct Constant {unsigned pass,slot;bool vertex;std::vector<uint8_t> bytes;};
    std::vector<Constant> constants;
};
static NativeEffectCameraContinuous prepareNativeEffectCameraContinuous(ID3D11Device* device,const fs::path& source,NativeEffectInputs& input){
    NativeEffectCameraContinuous out;out.profile=source_effect_camera::loadProfile(source/"profile.bin");
    D3D11_BUFFER_DESC desc{};desc.ByteWidth=out.profile.dynamicUnits32*32;desc.Usage=D3D11_USAGE_DEFAULT;desc.BindFlags=D3D11_BIND_VERTEX_BUFFER;
    input.dynamic.Reset();check(device->CreateBuffer(&desc,nullptr,&input.dynamic),"Effect camera retained dynamic stream");
    for(auto& item:input.passes){unsigned index=item.first;auto& pass=item.second;const auto path=source/"cb"/"initial"/("pass-"+std::to_string(index));
        std::ifstream rows(path/"cbuffers.txt");std::string stage,file;unsigned slot;
        while(rows>>stage>>slot>>file){auto bytes=load(path/file);bool vertex=stage=="vs";
            auto cb=std::find_if(pass.constants.begin(),pass.constants.end(),[&](const auto& c){return c.vertex==vertex&&c.slot==slot;});
            if(cb==pass.constants.end())throw std::runtime_error("Effect camera original CB slot absent");
            desc.ByteWidth=static_cast<UINT>(bytes.size());desc.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
            D3D11_SUBRESOURCE_DATA data{bytes.data(),0,0};cb->buffer.Reset();check(device->CreateBuffer(&desc,&data,&cb->buffer),"Effect camera retained original DEFAULT CB");
            out.constants.push_back({index,slot,vertex,std::vector<uint8_t>(bytes.begin(),bytes.end())});}
    }return out;
}
template<class CandidateAdmission>
static source_effect_camera::Frame updateNativeEffectCameraContinuous(ID3D11DeviceContext* context,NativeEffectCameraContinuous& state,
    NativeEffectInputs& input,const source_camera::Prefix& main,CandidateAdmission admit){
    auto frame=source_effect_camera::prepareCurrent(state.profile,main,admit);
    if(!frame.stream.empty()){D3D11_BOX range{0,0,0,static_cast<UINT>(frame.stream.size()),1,1};context->UpdateSubresource(input.dynamic.Get(),0,&range,frame.stream.data(),0,0);}
    input.draws.clear();input.draws.reserve(frame.draws.size());
    for(const auto& d:frame.draws){const auto& c=state.profile.candidates[d.candidate];input.draws.push_back({d.pass,d.byteOffset,c.family==7?792u:4u,0,0,c.texture.data()});}
    for(auto& current:state.constants)if(source_effect_camera::updateCB(current.pass,current.vertex,current.slot,main.data(),current.bytes.data())){
        auto& list=input.passes.at(current.pass).constants;auto cb=std::find_if(list.begin(),list.end(),[&](const auto& c){return c.vertex==current.vertex&&c.slot==current.slot;});
        context->UpdateSubresource(cb->buffer.Get(),0,nullptr,current.bytes.data(),0,0);}
    return frame;
}
static source_effect_camera::Frame updateNativeEffectCameraContinuous(ID3D11DeviceContext* context,NativeEffectCameraContinuous& state,
    NativeEffectInputs& input,const source_camera::Prefix& main){
    return updateNativeEffectCameraContinuous(context,state,input,main,[](unsigned,const source_effect_camera::Candidate&){return true;});
}
#endif
