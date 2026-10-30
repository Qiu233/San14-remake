#pragma once
#include "../../Runtime/PreparedFileInput.h"
// CPU metadata and generated instance banks for one prepared GroundDetails
// resource owner. Geometry, shaders and textures belong to its model media.
#include <array>
#include <cstdint>
#include <fstream>
#include <stdexcept>
#include <string>
#include <vector>

namespace ground_detail_source {
struct Affine48 { std::array<float,12> row; };
static_assert(sizeof(Affine48)==48);
struct Draw { uint32_t providerClass; };
struct Template {
    uint32_t index,nameHash;
    std::array<float,6> sourceBounds;
    std::array<std::vector<uint32_t>,3> lodDraws;
    std::vector<Draw> draws;
};
struct Element {
    uint32_t flags;
    std::array<float,4> lodDistances;
};
struct Declaration {
    uint32_t fieldSizeX,fieldSizeZ,fieldOffsetX,fieldOffsetZ;
    uint32_t cellsX,cellsZ,blocksX,blocksZ;
    uint32_t updateDistance,freeDistance,viewDistance;
    uint32_t absoluteDistanceH,absoluteDistanceV,terrainType,globalFlags;
};
static_assert(sizeof(Declaration)==60);
static_assert(sizeof(Element)==20);
struct Bank { uint32_t x,z,element,templateIndex;std::vector<Affine48> instances; };
struct Block { uint32_t id;std::vector<uint32_t> bankIndices; };
struct Packet {
    Declaration declaration;
    std::vector<Template> templates;std::vector<Element> elements;std::vector<Bank> banks;
    std::vector<Block> blocks; // derived once at import; no grouping allocation per camera
};
class Reader {
    NativePreparedInputFile file;
public:
    explicit Reader(const std::wstring& path):file(path,std::ios::binary) {
        if(!file)throw std::runtime_error("Procedural source packet cannot be opened.");
    }
    template<class T>T value(){T result{};read(&result,sizeof(result));return result;}
    void read(void* data,size_t size){if(!file.read(static_cast<char*>(data),size))throw std::runtime_error("Truncated procedural source packet.");}
    std::vector<uint32_t> words(){std::vector<uint32_t> b(value<uint32_t>());read(b.data(),b.size()*4);return b;}
    bool complete(){return file.peek()==std::char_traits<char>::eof();}
};
inline Packet read(const std::wstring& path) {
    Reader r(path);std::array<char,8> magic;r.read(magic.data(),8);
    if(std::string(magic.data(),8)!="GDPK0002")throw std::runtime_error("Ground detail metadata format differs.");
    Packet p;p.declaration=r.value<Declaration>();auto nt=r.value<uint32_t>(),ne=r.value<uint32_t>(),nb=r.value<uint32_t>();
    const auto& d=p.declaration;
    if(!nt||!ne||!d.blocksX||!d.blocksZ||nb>uint64_t(d.blocksX)*d.blocksZ*ne)
        throw std::runtime_error("Ground detail directory dimensions differ.");
    for(uint32_t i=0;i<nt;i++) {
        Template t;t.index=r.value<uint32_t>();t.nameHash=r.value<uint32_t>();
        r.read(t.sourceBounds.data(),24);for(auto& lod:t.lodDraws)lod=r.words();
        for(uint32_t j=0,n=r.value<uint32_t>();j<n;j++)t.draws.push_back(r.value<Draw>());
        if(t.index!=i)throw std::runtime_error("Original procedural template index is out of order.");
        p.templates.push_back(std::move(t));
    }
    for(uint32_t i=0;i<ne;i++)p.elements.push_back(r.value<Element>());
    for(uint32_t i=0;i<nb;i++) {
        Bank b;b.x=r.value<uint32_t>();b.z=r.value<uint32_t>();b.element=r.value<uint32_t>();b.templateIndex=r.value<uint32_t>();
        b.instances.resize(r.value<uint32_t>());r.read(b.instances.data(),b.instances.size()*48);
        if(b.x>=d.blocksX||b.z>=d.blocksZ||b.element>=ne||b.templateIndex>=nt)throw std::runtime_error("Procedural bank source index exceeds directory.");
        auto blockId=b.z*d.blocksX+b.x;
        if(p.blocks.empty()||p.blocks.back().id!=blockId)p.blocks.push_back({blockId,{}});
        p.blocks.back().bankIndices.push_back(i);
        p.banks.push_back(std::move(b));
    }
    if(!r.complete())throw std::runtime_error("Unconsumed procedural source packet bytes.");return p;
}
}
