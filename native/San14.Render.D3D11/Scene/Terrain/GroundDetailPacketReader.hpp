#pragma once
// Thin CPU import of write_native_packet.py's source packet. No device objects,
// current-camera state, artificial population positions or repeated source hashes.
#include <array>
#include <cstdint>
#include <fstream>
#include <stdexcept>
#include <string>
#include <vector>

namespace ground_detail_source {
struct Affine48 { std::array<float,12> row; };
static_assert(sizeof(Affine48)==48);
struct VertexBuffer { uint32_t index,stride,count;std::vector<uint8_t> data; };
struct IndexBuffer { uint32_t index,bits,count;std::vector<uint8_t> data; };
struct Attribute { uint32_t slot,offset,type,semantic; };
struct Layout { uint32_t index;std::vector<uint32_t> references;std::vector<Attribute> attributes; };
struct Draw {
    uint32_t index,layout,indexBuffer,indexStart,indexCount,vertexStart,vertexCount;
    uint32_t primitiveCode,flags,raw12,raw20,raw24;
    uint32_t normalShaderFile,providerClass,ownerHash,providerHash,shaderIdentifier;
};
struct Template {
    uint32_t index,nameHash,modelFile,isInstancing;
    std::array<float,6> sourceBounds;
    std::array<std::vector<uint32_t>,3> lodDraws;
    std::vector<VertexBuffer> vertices;std::vector<IndexBuffer> indices;
    std::vector<Layout> layouts;std::vector<Draw> draws;
};
struct Element {
    uint32_t index,type,flags,templateNameHash,seed;
    std::array<float,9> parameters;std::array<float,4> lodDistances;
};
struct Bank { uint32_t x,z,element,templateIndex;std::vector<Affine48> instances; };
struct Block { uint32_t id;std::vector<uint32_t> bankIndices; };
struct Packet {
    // FieldSizeXZ, FieldOffsetXZ, CellsXZ, BlocksXZ, Update/Free/ViewDistance,
    // AbsoluteDistanceHV, TerrainType, GlobalFlags; exact original integer words.
    std::array<uint32_t,15> declaration;
    std::vector<Template> templates;std::vector<Element> elements;std::vector<Bank> banks;
    std::vector<Block> blocks; // derived once at import; no grouping allocation per camera
};
class Reader {
    std::ifstream file;
public:
    explicit Reader(const std::wstring& path):file(path,std::ios::binary) {
        if(!file)throw std::runtime_error("Procedural source packet cannot be opened.");
    }
    template<class T>T value(){T result{};read(&result,sizeof(result));return result;}
    void read(void* data,size_t size){if(!file.read(static_cast<char*>(data),size))throw std::runtime_error("Truncated procedural source packet.");}
    std::vector<uint8_t> blob(){std::vector<uint8_t> b(value<uint32_t>());read(b.data(),b.size());return b;}
    std::vector<uint32_t> words(){std::vector<uint32_t> b(value<uint32_t>());read(b.data(),b.size()*4);return b;}
    bool complete(){return file.peek()==std::char_traits<char>::eof();}
};
inline Packet read(const std::wstring& path) {
    Reader r(path);std::array<char,8> magic;r.read(magic.data(),8);
    if(std::string(magic.data(),8)!="PPCE0001")throw std::runtime_error("Wrong original procedural source packet format.");
    Packet p;r.read(p.declaration.data(),60);auto nt=r.value<uint32_t>(),ne=r.value<uint32_t>(),nb=r.value<uint32_t>();
    if(nt!=31||ne!=27||nb>128*128*27)throw std::runtime_error("Original procedural packet directory differs.");
    for(uint32_t i=0;i<nt;i++) {
        Template t;t.index=r.value<uint32_t>();t.nameHash=r.value<uint32_t>();t.modelFile=r.value<uint32_t>();t.isInstancing=r.value<uint32_t>();
        r.read(t.sourceBounds.data(),24);for(auto& lod:t.lodDraws)lod=r.words();
        for(uint32_t j=0,n=r.value<uint32_t>();j<n;j++){VertexBuffer v;v.index=r.value<uint32_t>();v.stride=r.value<uint32_t>();v.count=r.value<uint32_t>();v.data=r.blob();t.vertices.push_back(std::move(v));}
        for(uint32_t j=0,n=r.value<uint32_t>();j<n;j++){IndexBuffer v;v.index=r.value<uint32_t>();v.bits=r.value<uint32_t>();v.count=r.value<uint32_t>();v.data=r.blob();t.indices.push_back(std::move(v));}
        for(uint32_t j=0,n=r.value<uint32_t>();j<n;j++){Layout l;l.index=r.value<uint32_t>();l.references=r.words();for(uint32_t k=0,m=r.value<uint32_t>();k<m;k++)l.attributes.push_back(r.value<Attribute>());t.layouts.push_back(std::move(l));}
        for(uint32_t j=0,n=r.value<uint32_t>();j<n;j++)t.draws.push_back(r.value<Draw>());
        if(t.index!=i)throw std::runtime_error("Original procedural template index is out of order.");
        p.templates.push_back(std::move(t));
    }
    for(uint32_t i=0;i<ne;i++)p.elements.push_back(r.value<Element>());
    for(uint32_t i=0;i<nb;i++) {
        Bank b;b.x=r.value<uint32_t>();b.z=r.value<uint32_t>();b.element=r.value<uint32_t>();b.templateIndex=r.value<uint32_t>();
        b.instances.resize(r.value<uint32_t>());r.read(b.instances.data(),b.instances.size()*48);
        if(b.x>=p.declaration[6]||b.z>=p.declaration[7]||b.element>=ne||b.templateIndex>=nt)throw std::runtime_error("Procedural bank source index exceeds directory.");
        auto blockId=b.z*p.declaration[6]+b.x;
        if(p.blocks.empty()||p.blocks.back().id!=blockId)p.blocks.push_back({blockId,{}});
        p.blocks.back().bankIndices.push_back(i);
        p.banks.push_back(std::move(b));
    }
    if(!r.complete())throw std::runtime_error("Unconsumed procedural source packet bytes.");return p;
}
}
