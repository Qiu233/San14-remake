#pragma once
#include "Accessories/AccessoryPose.h"

// Army uses the same D95990/D84810/D8A620/A22170 arithmetic as the closed
// accessory source. Import its actual variable source extents independently;
// the source-exported SM1G2300 flags0/single run must cover every joint.
inline NativeAccessoryPoseSource prepareNativeArmyPoseSource(const std::filesystem::path& directory){
    using namespace native_accessory_pose_detail;
    Reader in(directory/"pose-source.bin");std::array<char,8> magic{};in.read(magic.data(),8);
    if(magic!=std::array<char,8>{'C','A','P','O','S','E','0','1'})throw std::runtime_error("Army pose source schema");
    NativeAccessoryPoseSource source;source.duration8=in.get<float>();source.frameRate=in.get<float>();source.frameCount=in.get<uint32_t>();
    uint32_t joints=in.get<uint32_t>(),maps=in.get<uint32_t>(),tracks=in.get<uint32_t>(),channels=in.get<uint32_t>();
    uint32_t coefficientBytes=in.get<uint32_t>(),references=in.get<uint32_t>(),palettes=in.get<uint32_t>();
    unsigned flags,normalRuns,alternateRuns,run;std::ifstream profile(directory/"profile.txt");profile>>flags>>normalRuns>>alternateRuns>>run;
    if(!profile||flags!=0||normalRuns!=1||alternateRuns!=0||run!=joints||joints>32767||maps>32767||
        !std::isfinite(source.duration8)||source.duration8<=0||!std::isfinite(source.frameRate)||source.frameRate<=0||
        source.frameCount>65535||source.frameCount==0||coefficientBytes%32||palettes==0)
        throw std::runtime_error("Army actual source hierarchy/clip profile");
    source.defaults.resize(joints);in.read(source.defaults.data(),joints*48);source.parents.resize(joints);
    for(size_t i=0;i<joints;++i){std::memcpy(&source.parents[i],&source.defaults[i].scale.w,4);
        if(source.parents[i]>=static_cast<int32_t>(i))throw std::runtime_error("Army source parent order");}
    std::vector<int32_t> jointMap(maps);in.read(jointMap.data(),maps*4);
    std::vector<uint32_t> packed(tracks);in.read(packed.data(),tracks*4);source.channels.resize(channels);
    for(auto& c:source.channels){c.descriptorOffset=in.get<uint32_t>();c.type=in.get<uint32_t>();
        uint32_t count=in.get<uint32_t>();c.firstCoefficient=in.get<uint32_t>();c.keyTimes.resize(count);
        for(auto& time:c.keyTimes){uint32_t v=in.get<uint32_t>();if(v>=source.frameCount)throw std::runtime_error("Army source key range");time=static_cast<uint16_t>(v);}
        if(c.type>2||!std::is_sorted(c.keyTimes.begin(),c.keyTimes.end())||
            std::adjacent_find(c.keyTimes.begin(),c.keyTimes.end())!=c.keyTimes.end()||
            c.firstCoefficient+std::max<size_t>(1,count)>coefficientBytes/32)
            throw std::runtime_error("Army source channel order/coefficient range");}
    for(uint32_t word:packed){uint32_t mapIndex=(word>>4)&0x3FF,count=word&15,offset=word>>14;
        int32_t target=mapIndex<jointMap.size()?jointMap[mapIndex]:-1;
        if(target>=static_cast<int32_t>(joints))throw std::runtime_error("Army source signed joint remap");
        NativeAccessoryPoseTrack track{target,{}};int32_t prior=-1;
        for(uint32_t k=0;k<count;++k){auto found=std::find_if(source.channels.begin(),source.channels.end(),[offset](const auto& c){return c.descriptorOffset==offset;});
            if(found==source.channels.end()||static_cast<int32_t>(found->type)<=prior)throw std::runtime_error("Army source descriptor sequence");
            track.channels.push_back(static_cast<uint32_t>(found-source.channels.begin()));prior=static_cast<int32_t>(found->type);
            offset+=2+static_cast<uint32_t>((found->keyTimes.size()+1)>>1);}
        source.tracks.push_back(std::move(track));}
    source.coefficients.resize(coefficientBytes/32);for(auto& c:source.coefficients)for(auto& term:c.terms)term=decode(in.get<uint64_t>());
    source.references.resize(references);in.read(source.references.data(),references*64);source.palettes.resize(palettes);
    for(uint32_t i=0;i<palettes;++i){uint32_t index=in.get<uint32_t>(),count=in.get<uint32_t>();
        if(index!=i||count>128)throw std::runtime_error("Army source palette profile");source.palettes[i].resize(count);
        for(auto& e:source.palettes[i]){e.joint=in.get<int32_t>();e.matrix=in.get<int32_t>();e.rawValue4=in.get<uint32_t>();
            if(e.joint<0||e.joint>=static_cast<int32_t>(joints)||
                (e.rawValue4!=static_cast<uint32_t>(e.joint)&&(e.matrix<0||e.matrix>=static_cast<int32_t>(references))))
                throw std::runtime_error("Army source active palette reference");}}
    if(in.at!=in.bytes.size())throw std::runtime_error("Army pose source trailing bytes");return source;
}

// Direct body action sampling consumes the returned phase. Sequential flag
// players select their clip time separately through D91E60 below.
inline const NativeAccessoryPoseFrame& sampleNativeArmyPose(const NativeAccessoryPoseSource& source,
        NativeAccessoryPoseOwner& owner,float actualReturnedPhase,const NativeAccessoryPoseRoot& currentWorld,bool actualAnimationUpdate=true){
    using namespace native_accessory_pose_detail;
    if(!std::isfinite(actualReturnedPhase)||actualReturnedPhase<0||actualReturnedPhase>source.duration8)
        throw std::runtime_error("Army action requires actual current returned phase within imported clip");
    auto& frame=owner.frame;if(frame.localPose.size()!=source.defaults.size())throw std::runtime_error("Army owner/source skeleton differs");
    frame.returnedSeconds=frame.clipSeconds=actualReturnedPhase;
    float scaled=mul(mul(actualReturnedPhase,source.frameRate),65536.f);
    frame.fixedFrameTime=std::min(static_cast<uint32_t>(static_cast<int64_t>(scaled)),source.frameCount<<16);
    if(actualAnimationUpdate)for(const auto& track:source.tracks){if(track.targetJoint<0)continue;auto& local=frame.localPose[static_cast<size_t>(track.targetJoint)];
        const auto& defaults=source.defaults[static_cast<size_t>(track.targetJoint)];local.rotation=defaults.rotation;local.translation=defaults.translation;
        local.scale.x=defaults.scale.x;local.scale.y=defaults.scale.y;local.scale.z=defaults.scale.z;
        for(uint32_t index:track.channels){const auto& c=source.channels[index];auto v=sampleChannel(source,c,frame.fixedFrameTime);
            if(c.type==0)local.rotation=exponentialQuaternion(v);else if(c.type==1)local.translation=v;else{local.scale.x=v.x;local.scale.y=v.y;local.scale.z=v.z;}}}
    for(size_t i=0;i<frame.localPose.size();++i){const auto& l=frame.localPose[i];int32_t parent=source.parents[i];
        NativeAccessoryPoseJoint p{currentWorld.scale,currentWorld.rotation,currentWorld.translation};if(parent>=0)p=frame.resolvedPose[static_cast<size_t>(parent)];
        auto q=multiplyQuaternion(p.rotation,l.rotation);
        auto t=rotate(p.rotation,{mul(l.translation.x,p.scale.x),mul(l.translation.y,p.scale.y),mul(p.scale.z,l.translation.z),l.translation.w});
        NativeAccessoryPoseJoint r{{mul(l.scale.x,p.scale.x),mul(l.scale.y,p.scale.y),mul(l.scale.z,p.scale.z),p.scale.w},q,
            {add(p.translation.x,t.x),add(p.translation.y,t.y),add(p.translation.z,t.z),p.translation.w}};
        float square=add(add(add(mul(q.y,q.y),mul(q.x,q.x)),mul(q.z,q.z)),mul(q.w,q.w)),length=std::sqrt(square);
        if(!std::isfinite(length)||length==0)throw std::runtime_error("Army composed quaternion is zero/nonfinite");float factor=divide(1,length);
        r.rotation={mul(q.x,factor),mul(factor,q.y),mul(factor,q.z),mul(factor,q.w)};frame.resolvedPose[i]=r;frame.matrices[i]=matrix(r);}
    for(size_t p=0;p<source.palettes.size();++p)for(size_t i=0;i<source.palettes[p].size();++i){const auto& e=source.palettes[p][i];const auto& joint=frame.matrices[static_cast<size_t>(e.joint)];
        for(size_t c=0;c<3;++c){auto& row=frame.palettes[p].rows[i*3+c];if(e.rawValue4==static_cast<uint32_t>(e.joint)){const auto& j=joint.words;row={j[c],j[4+c],j[8+c],j[12+c]};}
            else row=column(source.references[static_cast<size_t>(e.matrix)],joint,c);}}
    return frame;
}
struct NativeArmyPoseCache {
    bool initialized{},hasSample{};
    float phase{};
    NativeAccessoryPoseRoot root{};
    const NativeAccessoryPoseSource* source{};
    bool sequentialClip{};
};
inline const NativeAccessoryPoseFrame& prepareNativeArmyCurrentPose(const NativeAccessoryPoseSource& source,
    NativeAccessoryPoseOwner& owner,NativeArmyPoseCache& cache,float phase,const NativeAccessoryPoseRoot& root,bool hasSample,
    bool sequentialClip=false){
    const bool sameSource=cache.initialized&&cache.source==&source&&cache.sequentialClip==sequentialClip;
    const bool samePhase=sameSource&&std::memcmp(&phase,&cache.phase,sizeof(phase))==0;
    const bool sameRoot=cache.initialized&&std::memcmp(&root,&cache.root,sizeof(root))==0;
    if(samePhase&&sameRoot&&cache.hasSample==hasSample)return owner.frame;
    const bool sampleLocal=hasSample&&(!cache.initialized||!cache.hasSample||!samePhase);
    const auto& frame=sequentialClip?sampleNativeAccessoryPose(source,owner,phase,root,sampleLocal):
        sampleNativeArmyPose(source,owner,phase,root,sampleLocal);
    cache={true,hasSample,phase,root,&source,sequentialClip};return frame;
}

// A successful grouped cache hit shares the sampled model's local joints.
// Resolved joints and palettes still belong to this actor's current root.
inline void publishNativeArmySharedLocal(const NativeAccessoryPoseSource& source,
    NativeAccessoryPoseOwner& owner,NativeArmyPoseCache& cache,
    const std::vector<NativeAccessoryPoseJoint>& local,float phase,const NativeAccessoryPoseRoot& root){
    owner.frame.localPose=local;
    sampleNativeArmyPose(source,owner,phase,root,false);
    cache={true,true,phase,root,&source};
}
