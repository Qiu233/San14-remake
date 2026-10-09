// Once-imported source View/ordinary54 descriptors; each request supplies real
// CPU populations and current owner presence. No original pointer/address ABI.
#pragma once
#include <array>
#include <cstdint>
#include <fstream>
#include <filesystem>
#include <stdexcept>
#include <vector>
namespace source_view {
struct Record {unsigned mask{},capacity{},classification{};};
struct Profile {
 int mainA00{};float mainA28{};unsigned mainA3C{},oceanGroup{},rainGroup{},terrainExclusion{},selectedF1D{};
 unsigned commonPresent{},commonFlags{},outerGroup{},commonExclusion{},modelSelector{},queueFlag{},queueFlag2{},queueState{};
 std::vector<Record> records;
};
struct Primitive {unsigned instanceIndex{},drawIndex{},exclusion{},allowedRecordBits{};};
inline Profile loadProfile(const std::filesystem::path& path) {
 std::ifstream in(path,std::ios::binary);Profile p;unsigned magic,n;
 auto read=[&](auto& value){in.read(reinterpret_cast<char*>(&value),sizeof(value));};
 read(magic);read(n);read(p.mainA00);read(p.mainA28);read(p.mainA3C);read(p.oceanGroup);read(p.rainGroup);read(p.terrainExclusion);read(p.selectedF1D);
 read(p.commonPresent);read(p.commonFlags);read(p.outerGroup);read(p.commonExclusion);read(p.modelSelector);read(p.queueFlag);read(p.queueFlag2);read(p.queueState);
 if(magic!=0x31505756u||n>32)throw std::runtime_error("Source View import schema");
 p.records.resize(n);for(auto& r:p.records){read(r.mask);read(r.capacity);read(r.classification);if(r.classification&1)throw std::runtime_error("Alternate source counter classification");}
 if(!in)throw std::runtime_error("Source View profile truncated");return p;
}
inline std::vector<Primitive> loadPrimitives(const std::filesystem::path& path) {
 std::ifstream in(path,std::ios::binary|std::ios::ate);auto bytes=static_cast<std::streamoff>(in.tellg());
 if(!in||bytes<0||bytes%16)throw std::runtime_error("Ordinary54 source primitive descriptor extent");
 std::vector<Primitive> result(static_cast<size_t>(bytes)/16);in.seekg(0);in.read(reinterpret_cast<char*>(result.data()),bytes);
 if(!in)throw std::runtime_error("Ordinary54 source primitive descriptors truncated");return result;
}
struct Inputs {
 std::vector<unsigned> ordinaryCounts;std::array<unsigned,4> collectionPrimitiveCounts{};
 unsigned subWrapperCount1BFC{};bool terrainPresent{},selectedCompactPresent{},selectedLitPresent{};
};
struct Frame {
 std::vector<unsigned> ordinaryCounts;unsigned flags19AC{},ocean19B0{},rain19B4{},subWrapperCount1BFC{};
 bool selectedShadowGate{},selectedCompactPresent{},noSubProvider{},rs68Gate{},rs119Gate{};
};
inline unsigned selectedGroup(unsigned group){return group>=1&&group<=7?1u<<(group-1):0;}
inline Frame prepareCurrent(const Profile& p,const Inputs& current) {
 if(current.ordinaryCounts.size()!=p.records.size())throw std::runtime_error("Current View needs ordered complete owned counters");
 Frame f;f.ordinaryCounts=current.ordinaryCounts;f.subWrapperCount1BFC=current.subWrapperCount1BFC;
 for(size_t i=0;i<p.records.size();i++) {
  const auto& r=p.records[i];if(current.ordinaryCounts[i]&&current.ordinaryCounts[i]>=r.capacity)throw std::runtime_error("Native first-counter append capacity");
  if(current.ordinaryCounts[i])f.flags19AC|=r.mask;
  if(current.terrainPresent&&(p.terrainExclusion&r.mask)==0)f.flags19AC|=r.mask;
  f.ocean19B0|=selectedGroup(p.oceanGroup)&r.mask;f.rain19B4|=selectedGroup(p.rainGroup)&r.mask;
 }
 f.flags19AC|=f.ocean19B0|f.rain19B4;
 bool shadowPopulation=current.subWrapperCount1BFC!=0;
 for(unsigned count:current.collectionPrimitiveCounts)shadowPopulation|=count!=0;
 if(shadowPopulation)f.flags19AC|=0x40000000u;
 f.selectedShadowGate=p.mainA00>0&&(f.flags19AC&0x40000000u)&&p.selectedF1D!=0;
 f.selectedCompactPresent=f.selectedShadowGate&&current.selectedCompactPresent;
 f.noSubProvider=!current.selectedLitPresent||(current.subWrapperCount1BFC&15)==0||!f.selectedCompactPresent;
 f.rs68Gate=(f.flags19AC&0x9FFFFFFFu)!=0;f.rs119Gate=f.rs68Gate&&f.ocean19B0!=0;return f;
}
struct Stencil {bool submit{},enabled{};unsigned reference{},readMask{},writeMask{},compare{},fail{},depthFail{},pass{};};
inline Stencil ordinary54(const Profile& p,const Frame& view,const Primitive& primitive,size_t recordIndex,unsigned context3E4) {
 if(recordIndex>=p.records.size())throw std::runtime_error("Current ordinary54 record index");
 Stencil result;const auto& r=p.records[recordIndex];
 if(!(primitive.allowedRecordBits&(1u<<recordIndex))||view.ordinaryCounts[recordIndex]==0||(primitive.exclusion&r.mask)!=0)return result;
 bool positive=(r.mask&0x80000000u)==0;bool selected=positive==((p.queueFlag&0x20)==0);
 unsigned group=p.modelSelector?1u<<((p.modelSelector-1)&31):0,reference=r.mask;bool prefix=(p.queueFlag&0x40)!=0;
 if(group){if(!prefix&&r.mask==group)selected=false;if(prefix)reference|=(context3E4&3)==1?0x60u:0x80u;}
 else if(prefix)reference|=(context3E4&3)==1?0x60u:0x80u;
 else if((p.queueFlag2&4)&&(context3E4&3)==1)reference|=0xD0u;
 result.submit=selected;result.enabled=p.queueState!=15&&(p.queueFlag2&1)==0;
 result.reference=reference&255;result.readMask=result.writeMask=255;result.compare=8;result.fail=result.depthFail=1;result.pass=3;
 return result;
}
// The caller still applies current visibility/source order, then submits each
// actual record's list. A descriptor does not declare its pixel population.
}
