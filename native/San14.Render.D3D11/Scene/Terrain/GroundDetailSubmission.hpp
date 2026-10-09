#pragma once
// Owned CPU counterpart of the finite CE BB2110/BB8710 instancing path.
// Include/use after importing the retained Packet. No graphics or process reads.
#include "GroundDetailVisibility.hpp"
#include <sstream>
#include <unordered_map>

namespace ground_detail_source {
struct WorldCommon {bool present;uint32_t flags8,flags14,exclusion404,frustumType64;};
struct WorldDispatch {uint32_t view,first,second,shadow;};
struct TemplateWorld {uint32_t flags44,exclusion78;int32_t caster10C;};
inline std::vector<TemplateWorld> readTemplateWorld(const Packet& p,const std::wstring& path) {
    Reader r(path);
    if(r.value<uint32_t>()!=0x31574447u||r.value<uint32_t>()!=p.templates.size())
        throw std::runtime_error("Procedural template World source format/count differs.");
    std::vector<TemplateWorld> result;result.reserve(p.templates.size());
    for(uint32_t i=0;i<p.templates.size();i++) {
        auto index=r.value<uint32_t>(),name=r.value<uint32_t>();
        auto flags=r.value<uint32_t>(),exclusion=r.value<uint32_t>();auto caster=r.value<int32_t>();
        if(index!=i||name!=p.templates[i].nameHash)throw std::runtime_error("Procedural template World identity differs.");
        result.push_back({flags,exclusion,caster});
    }
    if(!r.complete())throw std::runtime_error("Procedural template World source trailing bytes.");return result;
}
inline std::vector<WorldDispatch> dispatch(uint32_t first,uint32_t second,uint32_t shadow,
    uint32_t presentViews,const WorldCommon& common) {
    std::vector<WorldDispatch> result;
    if(common.present&&(common.flags14&4)==0)return result;
    for(uint32_t view:{0u,6u}){uint32_t bit=1u<<view;
        if(((first|second|shadow)&bit)==0)break; // source returns, not continue
        if(presentViews&bit)result.push_back({view,first&bit,second&bit,shadow&bit});
    }return result;
}
inline WorldDispatch templateMasks(WorldDispatch d,const TemplateWorld& t,
    uint32_t successful3340,const WorldCommon& common) {
    d.first&=successful3340;d.second&=successful3340;
    d.shadow=t.caster10C<0||(common.present&&(common.flags8&0x800)==0)?0:d.shadow&successful3340;
    return d;
}
inline uint32_t effectiveExclusion(const WorldCommon& common){return 0xfffffffdu|(common.present?common.exclusion404:0);}
inline bool firstCasterFamily(uint8_t f1c){return (f1c&1)&&(f1c&2);} // BB8710 caller argument is zero
inline bool secondCasterFamily(uint8_t f1f){return (f1f&0x80)==0&&(f1f&1);}

struct PrimitiveSource {uint32_t templateIndex,lod,sourceOrdinal,draw;std::string relativePath;};
// Mother template-lods.txt is emitted in the actual PartsSet/binder primitive
// order. Its finite31-template categories are all0; Hi/Mid are a separate axis.
inline std::vector<PrimitiveSource> readPrimitiveOrder(const Packet& p,const std::wstring& path) {
    std::ifstream file(path);if(!file)throw std::runtime_error("Procedural Parts source order absent.");
    std::vector<PrimitiveSource> result;std::vector<uint32_t> ordinals(p.templates.size());
    uint32_t ti,lod,draw;std::string name,relative;
    while(file>>ti>>name>>lod>>draw>>relative){
        if(ti>=p.templates.size()||lod>2||uint32_t(std::stoul(name,nullptr,16))!=p.templates[ti].nameHash)
            throw std::runtime_error("Procedural Parts template/LOD source identity differs.");
        const auto& draws=p.templates[ti].lodDraws[lod];
        if(std::find(draws.begin(),draws.end(),draw)==draws.end())throw std::runtime_error("Procedural Parts draw outside source LOD.");
        result.push_back({ti,lod,ordinals[ti]++,draw,relative});
    }
    if(!file.eof())throw std::runtime_error("Incomplete procedural Parts source row.");
    size_t expected=0;for(const auto& t:p.templates)for(const auto& draws:t.lodDraws)expected+=draws.size();
    if(result.size()!=expected)throw std::runtime_error("Procedural Parts source directory incomplete.");
    return result;
}

inline std::vector<uint32_t> candidateBlockOrder(const Packet& p,const CurrentView& v,bool anyNonnegativeCaster) {
    if(v.cullingType!=1||v.scanningType!=0)throw std::runtime_error("Procedural order requires Culling1/Scanning0.");
    const auto& d=p.declaration;float width=float(d[0]/d[6]);
    auto index=[&](float x,unsigned n){return std::clamp(int(x),0,int(n));};
    int ax0=index(D(S(S(v.eye.X,float(d[11])),float(d[2])),width),d[6]),ax1=index(D(S(A(v.eye.X,float(d[11])),float(d[2])),width),d[6]);
    int az0=index(D(S(S(v.eye.Z,float(d[11])),float(d[3])),width),d[7]),az1=index(D(S(A(v.eye.Z,float(d[11])),float(d[3])),width),d[7]);
    int x0=std::max(index(D(S(S(v.eye.X,float(d[10])),float(d[2])),width),d[6]),int(D(S(v.minimum.X,float(d[2])),width)));
    int z0=std::max(index(D(S(S(v.eye.Z,float(d[10])),float(d[3])),width),d[7]),int(D(S(v.minimum.Z,float(d[3])),width)));
    int x1=std::min(index(D(S(A(v.eye.X,float(d[10])),float(d[2])),width),d[6]),int(std::ceil(D(S(v.maximum.X,float(d[2])),width))));
    int z1=std::min(index(D(S(A(v.eye.Z,float(d[10])),float(d[3])),width),d[7]),int(std::ceil(D(S(v.maximum.Z,float(d[3])),width))));
    bool absoluteRange=anyNonnegativeCaster&&(d[11]!=0||d[12]!=0);std::vector<uint32_t> result;
    if(absoluteRange)for(int z=az0;z<az1;z++)for(int x=ax0;x<ax1;x++)result.push_back(uint32_t(z)*d[6]+uint32_t(x));
    for(int z=z0;z<z1;z++)for(int x=x0;x<x1;x++)
        if(!absoluteRange||x<ax0||x>=ax1||z<az0||z>=az1)result.push_back(uint32_t(z)*d[6]+uint32_t(x));
    return result;
}

struct InstanceStream {uint32_t templateIndex,lod;std::vector<Affine48> rows;};
struct OrderedSubmission {PrimitiveSource primitive;uint32_t streamIndex;};
struct OrderedPopulation {std::vector<InstanceStream> streams;std::vector<OrderedSubmission> submissions;};

// Use select(p,currentView) for membership, then this function for native
// scan/element/primitive insertion order. Empty streams represent D4A6E0's
// zero-count type22 command and are retained in the CPU publication.
inline OrderedPopulation aggregate(const Packet& p,const CurrentView& v,const std::vector<Slice>& slices,
    const std::vector<PrimitiveSource>& primitives,const std::vector<TemplateWorld>& templates) {
    if(templates.size()!=p.templates.size())throw std::runtime_error("Procedural current template fields incomplete.");
    std::unordered_map<uint64_t,std::vector<const Slice*>> byCellElement;
    for(const auto& s:slices){if(!s.source||s.lod>2||s.start>s.source->instances.size()||s.count>s.source->instances.size()-s.start)
            throw std::runtime_error("Procedural slice exceeds its original48B bank.");
        uint64_t block=uint64_t(s.source->z)*p.declaration[6]+s.source->x;
        byCellElement[block*p.elements.size()+s.source->element].push_back(&s);
    }
    OrderedPopulation out;
    for(uint32_t ti=0;ti<p.templates.size();ti++)for(uint32_t lod=0;lod<3;lod++)out.streams.push_back({ti,lod,{}});
    bool absoluteRange=std::any_of(templates.begin(),templates.end(),[](const TemplateWorld& t){return t.caster10C>=0;});
    for(uint32_t block:candidateBlockOrder(p,v,absoluteRange))for(uint32_t element=0;element<p.elements.size();element++){
        auto found=byCellElement.find(uint64_t(block)*p.elements.size()+element);if(found==byCellElement.end())continue;
        for(const auto* s:found->second){if(s->source->templateIndex>=p.templates.size())throw std::runtime_error("Procedural template stream index exceeds directory.");
            auto& rows=out.streams[s->source->templateIndex*3+s->lod].rows;
            if(rows.size()+s->count>50000)throw std::runtime_error("Procedural stream exceeds actual finite50000 cap profile.");
            rows.insert(rows.end(),s->source->instances.begin()+s->start,s->source->instances.begin()+s->start+s->count);
        }
    }
    for(const auto& primitive:primitives){
        if(primitive.templateIndex>=p.templates.size()||primitive.lod>2)throw std::runtime_error("Procedural source primitive exceeds directory.");
        // BBAFD7 is reached only when this template has a nonzero aggregate
        // count. Its complete primitive directory then survives in counters,
        // including the LOD rows whose type22 command is replaced with empty.
        auto start=primitive.templateIndex*3;
        if(out.streams[start].rows.empty()&&out.streams[start+1].rows.empty()&&out.streams[start+2].rows.empty())continue;
        out.submissions.push_back({primitive,primitive.templateIndex*3+primitive.lod});
    }
    std::stable_sort(out.submissions.begin(),out.submissions.end(),[](const OrderedSubmission& a,const OrderedSubmission& b){
        return a.primitive.templateIndex==b.primitive.templateIndex?a.primitive.sourceOrdinal<b.primitive.sourceOrdinal:a.primitive.templateIndex<b.primitive.templateIndex;
    });return out;
}
}
