#pragma once
#include <algorithm>
#include <array>
#include <cmath>
#include <cstdint>
#include <cstring>
#include <filesystem>
#include <fstream>
#include <iterator>
#include <stdexcept>
#include <vector>

// Category2's finite D95990 -> D84810 -> D8A620 -> A22170 -> palette
// producer. Compile with /fp:strict (no FMA/reassociation). This consumes an
// actual returned SequentialPlayer phase; D953F0 loop selection is separate.
struct NativeAccessoryPoseVector { float x,y,z,w; };
struct NativeAccessoryPoseRoot {
    NativeAccessoryPoseVector scale,rotation,translation;
}; // actual Scale34.xyz/W0, Quaternion24.xyzw, Position18.xyz/W1
struct NativeAccessoryPoseJoint {
    NativeAccessoryPoseVector scale,rotation,translation;
}; // local scale.w is the untouched source parent/control DWORD
struct NativeAccessoryPoseMatrix { std::array<float,16> words; };
struct NativeAccessoryPoseCoefficient { std::array<NativeAccessoryPoseVector,4> terms; };
struct NativeAccessoryPoseChannel {
    uint32_t descriptorOffset,type,firstCoefficient;
    std::vector<uint16_t> keyTimes;
};
struct NativeAccessoryPoseTrack { int32_t targetJoint; std::vector<uint32_t> channels; };
struct NativeAccessoryPosePaletteEntry { int32_t joint,matrix; uint32_t rawValue4; };
struct NativeAccessoryPoseSource {
    float duration8,frameRate; uint32_t frameCount;
    std::vector<NativeAccessoryPoseJoint> defaults;
    std::vector<int32_t> parents;
    std::vector<NativeAccessoryPoseTrack> tracks;
    std::vector<NativeAccessoryPoseChannel> channels;
    std::vector<NativeAccessoryPoseCoefficient> coefficients;
    std::vector<NativeAccessoryPoseMatrix> references;
    std::vector<std::vector<NativeAccessoryPosePaletteEntry>> palettes;
};
struct NativeAccessoryPosePalette { std::vector<NativeAccessoryPoseVector> rows; };
struct NativeAccessoryPoseFrame {
    float returnedSeconds=0,clipSeconds=0; uint32_t fixedFrameTime=0;
    std::vector<NativeAccessoryPoseJoint> localPose,resolvedPose;
    std::vector<NativeAccessoryPoseMatrix> matrices;
    std::vector<NativeAccessoryPosePalette> palettes;
};
struct NativeAccessoryPoseOwner { NativeAccessoryPoseFrame frame; };
static_assert(sizeof(NativeAccessoryPoseVector)==16 && sizeof(NativeAccessoryPoseRoot)==48 &&
    sizeof(NativeAccessoryPoseJoint)==48 && sizeof(NativeAccessoryPoseMatrix)==64);

namespace native_accessory_pose_detail {
#if defined(_MSC_VER)
#define SAN14_ACCESSORY_NOINLINE __declspec(noinline)
#else
#define SAN14_ACCESSORY_NOINLINE __attribute__((noinline))
#endif
SAN14_ACCESSORY_NOINLINE inline float mul(float a,float b){return a*b;}
SAN14_ACCESSORY_NOINLINE inline float add(float a,float b){return a+b;}
SAN14_ACCESSORY_NOINLINE inline float sub(float a,float b){return a-b;}
SAN14_ACCESSORY_NOINLINE inline float divide(float a,float b){return a/b;}
inline float fromBits(uint32_t word){float f;std::memcpy(&f,&word,4);return f;}
inline NativeAccessoryPoseVector decode(uint64_t word){
    const float multiplier=fromBits(static_cast<uint32_t>(((word>>37)&0x07800000)+0x32000000));
    const int32_t x=static_cast<int32_t>((word>>28)&0xFFFFF000);
    const int32_t y=static_cast<int32_t>((word>>8)&0xFFFFF000);
    const int32_t z=static_cast<int32_t>(word<<12)&static_cast<int32_t>(0xFFFFF000);
    return {mul(static_cast<float>(x),multiplier),mul(static_cast<float>(y),multiplier),
        mul(static_cast<float>(z),multiplier),0};
}
inline float sinPolynomial(float a,bool positive){
    float square=mul(a,a),fourth=mul(square,square);
    float first=positive?sub(6,square):sub(square,6);
    float sum=positive?add(mul(first,20),fourth):sub(mul(first,20),fourth);
    return mul(mul(sum,a),fromBits(0x3C088889));
}
inline float cosPolynomial(float a,bool positive){
    float square=mul(a,a),fourthPlus=add(mul(square,square),360);
    float first=positive?sub(30,square):sub(square,30);
    float product=mul(fourthPlus,first),sum=positive?sub(product,10080):add(product,10080);
    return mul(sum,fromBits(0x3AB60B61));
}
inline std::array<float,2> sinCos(float angle){
    if(angle==0)return {0,1};
    bool positiveSin=angle>=0,positiveCos=true;
    float a=positiveSin?angle:-angle,twoPi=fromBits(0x40C90FDB);
    while(a>=twoPi)a=add(a,-twoPi);
    const float pi=fromBits(0x40490FDB),halfPi=fromBits(0x3FC90FDB);
    if(a>=pi){a=add(a,-pi);positiveSin=!positiveSin;positiveCos=false;}
    if(a>=halfPi){a=sub(pi,a);positiveCos=!positiveCos;}
    if(a<fromBits(0x3F2B16EC))return {sinPolynomial(a,positiveSin),cosPolynomial(a,positiveCos)};
    if(a<fromBits(0x3F688790))return {cosPolynomial(sub(halfPi,a),positiveSin),cosPolynomial(a,positiveCos)};
    float complement=sub(halfPi,a);
    return {cosPolynomial(complement,positiveSin),sinPolynomial(complement,positiveCos)};
}
inline NativeAccessoryPoseVector exponentialQuaternion(NativeAccessoryPoseVector value){
    float squared=add(add(mul(value.x,value.x),mul(value.y,value.y)),mul(value.z,value.z));
    float length=std::sqrt(squared);auto sc=sinCos(mul(length,.5f));
    float factor=length>fromBits(0x37480000)?divide(sc[0],length):.5f;
    return {mul(value.x,factor),mul(value.y,factor),mul(value.z,factor),sc[1]};
}
inline NativeAccessoryPoseVector multiplyQuaternion(NativeAccessoryPoseVector p,NativeAccessoryPoseVector l){
    float dot=add(add(mul(l.x,p.x),mul(l.y,p.y)),mul(l.z,p.z));
    return {add(sub(mul(l.z,p.y),mul(p.z,l.y)),add(mul(p.x,l.w),mul(l.x,p.w))),
        add(sub(mul(p.z,l.x),mul(l.z,p.x)),add(mul(p.y,l.w),mul(l.y,p.w))),
        add(sub(mul(p.x,l.y),mul(l.x,p.y)),add(mul(p.z,l.w),mul(l.z,p.w))),
        sub(mul(l.w,p.w),dot)};
}
inline NativeAccessoryPoseVector rotate(NativeAccessoryPoseVector p,NativeAccessoryPoseVector l){
    float dot=add(add(mul(l.x,p.x),mul(l.y,p.y)),mul(l.z,p.z));
    float x=add(sub(mul(l.z,p.y),mul(p.z,l.y)),mul(l.x,p.w));
    float y=add(sub(mul(p.z,l.x),mul(l.z,p.x)),mul(l.y,p.w));
    float z=add(sub(mul(p.x,l.y),mul(l.x,p.y)),mul(l.z,p.w));
    return {add(mul(x,p.w),add(sub(mul(p.y,z),mul(p.z,y)),mul(p.x,dot))),
        add(mul(y,p.w),add(sub(mul(p.z,x),mul(p.x,z)),mul(p.y,dot))),
        add(mul(z,p.w),add(sub(mul(p.x,y),mul(p.y,x)),mul(p.z,dot))),l.w};
}
inline NativeAccessoryPoseMatrix matrix(const NativeAccessoryPoseJoint& j){
    auto q=j.rotation,s=j.scale,t=j.translation;
    float twiceZ=add(q.z,q.z),twiceX=add(q.x,q.x),twiceY=add(q.y,q.y);
    float zz=mul(q.z,twiceZ),xz=mul(q.x,twiceZ),wx=mul(q.w,twiceX),wz=mul(q.w,twiceZ);
    float xy=mul(q.y,twiceX),yz=mul(q.z,twiceY),yy=mul(q.y,twiceY),wy=mul(q.w,twiceY),xx=mul(q.x,twiceX);
    return {{mul(sub(sub(1,yy),zz),s.x),mul(add(xy,wz),s.x),mul(sub(xz,wy),s.x),0,
        mul(sub(xy,wz),s.y),mul(sub(sub(1,zz),xx),s.y),mul(add(yz,wx),s.y),0,
        mul(add(xz,wy),s.z),mul(sub(yz,wx),s.z),mul(sub(sub(1,xx),yy),s.z),0,t.x,t.y,t.z,1}};
}
inline float component(const float* left,float x,float y,float z,float w){
    return add(add(mul(left[2],z),mul(left[0],x)),add(mul(left[3],w),mul(left[1],y)));
}
inline NativeAccessoryPoseVector column(const NativeAccessoryPoseMatrix& reference,const NativeAccessoryPoseMatrix& joint,size_t c){
    const auto& r=reference.words;const auto& j=joint.words;
    return {component(r.data(),j[c],j[4+c],j[8+c],j[12+c]),
        component(r.data()+4,j[c],j[4+c],j[8+c],j[12+c]),
        component(r.data()+8,j[c],j[4+c],j[8+c],j[12+c]),
        component(r.data()+12,j[c],j[4+c],j[8+c],j[12+c])};
}
struct Reader {
    std::vector<uint8_t> bytes;size_t at=0;
    explicit Reader(const std::filesystem::path& path){std::ifstream in(path,std::ios::binary);
        if(!in)throw std::runtime_error("accessory pose source missing");
        bytes.assign(std::istreambuf_iterator<char>(in),{});}
    void read(void* target,size_t length){if(length>bytes.size()-at)throw std::runtime_error("accessory pose source truncated");
        std::memcpy(target,bytes.data()+at,length);at+=length;}
    template<class T>T get(){T value;read(&value,sizeof(value));return value;}
};
inline float cubicComponent(float c0,float c1,float c2,float c3,float t,float square,float cube){
    return add(mul(c3,cube),add(add(c0,mul(c1,t)),mul(c2,square)));
}
inline NativeAccessoryPoseVector sampleChannel(const NativeAccessoryPoseSource& source,
        const NativeAccessoryPoseChannel& channel,uint32_t fixedTime){
    size_t first=0,remaining=channel.keyTimes.size();uint32_t integerFrame=fixedTime>>16;
    while(remaining>1){size_t half=remaining>>1,midpoint=first+half;
        if(integerFrame<channel.keyTimes[midpoint])remaining=half;
        else {first=midpoint;remaining=(remaining+1)>>1;}}
    uint32_t start=0,end=source.frameCount;
    if(remaining==1){start=channel.keyTimes[first];if(first+1!=channel.keyTimes.size())end=channel.keyTimes[first+1];}
    int32_t duration=static_cast<int32_t>((end-start)<<16);
    const float t=divide(static_cast<float>(fixedTime-(start<<16)),static_cast<float>(duration));
    const auto& c=source.coefficients[channel.firstCoefficient+first].terms;
    float square=mul(t,t),cube=mul(square,t);
    return {cubicComponent(c[0].x,c[1].x,c[2].x,c[3].x,t,square,cube),
        cubicComponent(c[0].y,c[1].y,c[2].y,c[3].y,t,square,cube),
        cubicComponent(c[0].z,c[1].z,c[2].z,c[3].z,t,square,cube),1};
}
#undef SAN14_ACCESSORY_NOINLINE
} // namespace native_accessory_pose_detail

// Import once, alongside normal original-resource provenance checking. Parsed
// channels/remap/defaults/reference matrices/palettes never reload per request.
inline NativeAccessoryPoseSource prepareNativeAccessoryPoseSource(const std::filesystem::path& directory){
    using namespace native_accessory_pose_detail;
    Reader in(directory/"pose-source.bin");std::array<char,8> magic{};in.read(magic.data(),8);
    if(magic!=std::array<char,8>{'C','A','P','O','S','E','0','1'})throw std::runtime_error("accessory pose source format");
    NativeAccessoryPoseSource source;source.duration8=in.get<float>();source.frameRate=in.get<float>();source.frameCount=in.get<uint32_t>();
    uint32_t joints=in.get<uint32_t>(),maps=in.get<uint32_t>(),tracks=in.get<uint32_t>(),channels=in.get<uint32_t>();
    uint32_t coefficientBytes=in.get<uint32_t>(),references=in.get<uint32_t>(),palettes=in.get<uint32_t>();
    if(joints!=17||palettes!=3||source.frameCount!=90||source.frameRate!=60||source.duration8!=1.5f||coefficientBytes%32)
        throw std::runtime_error("accessory pose requires the imported category2 source profile");
    source.defaults.resize(joints);in.read(source.defaults.data(),joints*48);source.parents.resize(joints);
    for(size_t i=0;i<joints;++i){std::memcpy(&source.parents[i],&source.defaults[i].scale.w,4);
        if(source.parents[i]>=static_cast<int32_t>(i))throw std::runtime_error("accessory source parent order");}
    std::vector<int32_t> jointMap(maps);in.read(jointMap.data(),maps*4);
    std::vector<uint32_t> packed(tracks);in.read(packed.data(),tracks*4);
    source.channels.resize(channels);
    for(auto& channel:source.channels){channel.descriptorOffset=in.get<uint32_t>();channel.type=in.get<uint32_t>();
        uint32_t count=in.get<uint32_t>();channel.firstCoefficient=in.get<uint32_t>();channel.keyTimes.resize(count);
        for(auto& time:channel.keyTimes){uint32_t value=in.get<uint32_t>();if(value>=source.frameCount)throw std::runtime_error("accessory source key range");time=static_cast<uint16_t>(value);}
        if(channel.type>2||!std::is_sorted(channel.keyTimes.begin(),channel.keyTimes.end())||
            std::adjacent_find(channel.keyTimes.begin(),channel.keyTimes.end())!=channel.keyTimes.end()||
            channel.firstCoefficient+std::max<size_t>(1,count)>coefficientBytes/32)
            throw std::runtime_error("accessory source channel range/order");}
    for(uint32_t word:packed){uint32_t mapIndex=(word>>4)&0x3FF,count=word&15,offset=word>>14;
        int32_t target=mapIndex<jointMap.size()?jointMap[mapIndex]:-1;
        if(target>=static_cast<int32_t>(joints))throw std::runtime_error("accessory source joint remap");
        NativeAccessoryPoseTrack track{target,{}};int32_t prior=-1;
        for(uint32_t k=0;k<count;++k){auto found=std::find_if(source.channels.begin(),source.channels.end(),[offset](const auto& c){return c.descriptorOffset==offset;});
            if(found==source.channels.end()||static_cast<int32_t>(found->type)<=prior)throw std::runtime_error("accessory source descriptor sequence");
            track.channels.push_back(static_cast<uint32_t>(found-source.channels.begin()));prior=static_cast<int32_t>(found->type);
            offset+=2+static_cast<uint32_t>((found->keyTimes.size()+1)>>1);}
        source.tracks.push_back(std::move(track));}
    source.coefficients.resize(coefficientBytes/32);
    for(auto& coefficient:source.coefficients)for(auto& term:coefficient.terms)term=decode(in.get<uint64_t>());
    source.references.resize(references);in.read(source.references.data(),references*64);
    source.palettes.resize(palettes);
    const std::array<uint32_t,3> expectedEntries={9,11,1};
    for(uint32_t i=0;i<palettes;++i){uint32_t index=in.get<uint32_t>(),count=in.get<uint32_t>();
        if(index!=i||count!=expectedEntries[i])throw std::runtime_error("accessory source palette profile");
        source.palettes[i].resize(count);
        for(auto& entry:source.palettes[i]){entry.joint=in.get<int32_t>();entry.matrix=in.get<int32_t>();entry.rawValue4=in.get<uint32_t>();
            if(entry.joint<0||entry.joint>=static_cast<int32_t>(joints)||
                (entry.rawValue4!=static_cast<uint32_t>(entry.joint)&&(entry.matrix<0||entry.matrix>=static_cast<int32_t>(references))))
                throw std::runtime_error("accessory source active palette reference");}}
    if(in.at!=in.bytes.size())throw std::runtime_error("accessory pose source trailing data");
    return source;
}
inline NativeAccessoryPoseOwner createNativeAccessoryPoseOwner(const NativeAccessoryPoseSource& source){
    NativeAccessoryPoseOwner owner;auto& f=owner.frame;f.localPose=source.defaults;
    f.resolvedPose.resize(source.defaults.size());f.matrices.resize(source.defaults.size());f.palettes.resize(source.palettes.size());
    for(size_t i=0;i<f.palettes.size();++i)f.palettes[i].rows.resize(source.palettes[i].size()*3);
    return owner;
}
// The returned buffers belong to this owner until its next update. No frame ID,
// canned pose file, matrix cache keyed by frame, or complete inactive CB is used.
inline const NativeAccessoryPoseFrame& sampleNativeAccessoryPose(const NativeAccessoryPoseSource& source,
        NativeAccessoryPoseOwner& owner,float actualReturnedPhase,const NativeAccessoryPoseRoot& currentWorld,
        bool actualAnimationUpdate=true){
    using namespace native_accessory_pose_detail;
    if(!std::isfinite(actualReturnedPhase)||actualReturnedPhase<0||actualReturnedPhase>source.duration8)
        throw std::runtime_error("invalid actual accessory SequentialPlayer phase");
    const std::array<NativeAccessoryPoseVector,3> rootFields={currentWorld.scale,currentWorld.rotation,currentWorld.translation};
    for(const auto& value:rootFields)if(!std::isfinite(value.x)||!std::isfinite(value.y)||!std::isfinite(value.z)||!std::isfinite(value.w))
        throw std::runtime_error("invalid actual accessory World root");
    auto& frame=owner.frame;
    if(frame.localPose.size()!=source.defaults.size())throw std::runtime_error("accessory owner not created for source");
    frame.returnedSeconds=actualReturnedPhase;frame.clipSeconds=std::fmin(actualReturnedPhase,sub(source.duration8,1.1920929e-05f));
    float scaled=mul(mul(frame.clipSeconds>0?frame.clipSeconds:0,source.frameRate),65536.f);
    frame.fixedFrameTime=std::min(static_cast<uint32_t>(static_cast<int64_t>(scaled)),source.frameCount<<16);
    // The original near-animation gate leaves local10 intact while hierarchy
    // placement still uses this World's current root. A fresh far group keeps
    // its imported defaults; it must not be sampled at clip frame zero.
    if(actualAnimationUpdate)for(const auto& track:source.tracks){if(track.targetJoint<0)continue;
        auto& local=frame.localPose[static_cast<size_t>(track.targetJoint)];const auto& defaults=source.defaults[static_cast<size_t>(track.targetJoint)];
        // D84810 restores absent fields, preserves +0C, and retains untouched joints.
        local.rotation=defaults.rotation;local.translation=defaults.translation;
        local.scale.x=defaults.scale.x;local.scale.y=defaults.scale.y;local.scale.z=defaults.scale.z;
        for(uint32_t index:track.channels){const auto& channel=source.channels[index];auto value=sampleChannel(source,channel,frame.fixedFrameTime);
            if(channel.type==0)local.rotation=exponentialQuaternion(value);
            else if(channel.type==1)local.translation=value;
            else {local.scale.x=value.x;local.scale.y=value.y;local.scale.z=value.z;}}}
    for(size_t i=0;i<frame.localPose.size();++i){const auto& local=frame.localPose[i];int32_t parent=source.parents[i];
        NativeAccessoryPoseJoint p{currentWorld.scale,currentWorld.rotation,currentWorld.translation};
        if(parent>=0)p=frame.resolvedPose[static_cast<size_t>(parent)];
        auto q=multiplyQuaternion(p.rotation,local.rotation);
        auto translated=rotate(p.rotation,{mul(local.translation.x,p.scale.x),mul(local.translation.y,p.scale.y),mul(p.scale.z,local.translation.z),local.translation.w});
        NativeAccessoryPoseJoint resolved{{mul(local.scale.x,p.scale.x),mul(local.scale.y,p.scale.y),mul(local.scale.z,p.scale.z),p.scale.w},q,
            {add(p.translation.x,translated.x),add(p.translation.y,translated.y),add(p.translation.z,translated.z),p.translation.w}};
        float squared=add(add(add(mul(q.y,q.y),mul(q.x,q.x)),mul(q.z,q.z)),mul(q.w,q.w));
        float length=std::sqrt(squared);
        if(!std::isfinite(length)||length==0)throw std::runtime_error("invalid accessory composed quaternion");
        float factor=divide(1,length);
        resolved.rotation={mul(q.x,factor),mul(factor,q.y),mul(factor,q.z),mul(factor,q.w)};
        frame.resolvedPose[i]=resolved;frame.matrices[i]=matrix(resolved);}
    for(size_t p=0;p<source.palettes.size();++p)for(size_t i=0;i<source.palettes[p].size();++i){
        const auto& entry=source.palettes[p][i];const auto& joint=frame.matrices[static_cast<size_t>(entry.joint)];
        for(size_t c=0;c<3;++c){auto& row=frame.palettes[p].rows[i*3+c];
            if(entry.rawValue4==static_cast<uint32_t>(entry.joint)){const auto& j=joint.words;row={j[c],j[4+c],j[8+c],j[12+c]};}
            else row=column(source.references[static_cast<size_t>(entry.matrix)],joint,c);}}
    return frame;
}
