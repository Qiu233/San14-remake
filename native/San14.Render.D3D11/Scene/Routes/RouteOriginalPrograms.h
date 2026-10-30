#pragma once
#include "../../Runtime/PreparedFileInput.h"
#include "RouteProgramResourceInputs.h"
#include "../../Resources/ShaderFeatureSelection.h"
#include "../../Passes/Lighting/HdrHeightFogState.h"
#include <fstream>
#include <algorithm>
#include <array>
#include <cstring>
#include <map>
#include <optional>
#include <set>
#include <stdexcept>
#include <string>
#include <vector>

namespace San14::Render {
struct RouteAcceptedInputs {
    const HeightFogSettings& heightSettings;
    bool heightEnabled;
    float commonClock,exposureLuminance;
    std::array<source_camera::V4,6> clipPlanes;
    ID3D11ShaderResourceView* exposure;
    ID3D11SamplerState* exposureSampler;
};
// Accepted-frame values owned by the real View/environment/primitive. Resource
// imports describe layouts; they do not supply current numeric defaults.
struct RouteOriginalFrame {
    source_camera::Matrix worldToClip;
    source_camera::V4 eye;
    std::array<source_camera::V4,6> clipPlanes;
    HdrHeightFogValues heightFog;
    std::array<float,2> toneMap;
    std::optional<source_camera::V4> fog,fogColor,fogParameter1,fogParameter2,lightDirection;
    std::optional<std::array<source_camera::V4,4>> scattering;
    ID3D11ShaderResourceView* exposure{};
    ID3D11SamplerState* exposureSampler{};
};

class RouteOriginalPrograms {
    enum class Semantic {Diffuse,DiffuseSource,TextureIndex,WorldToProjection,LocalToWorld,
        ClipPlanes,Eye,StageCount,SamplerMapping,HeightType,HeightColor,HeightParameter,
        HeightFluctuation,AlphaTest,ToneMap,Fog,FogColor,FogParameter1,FogParameter2,
        LightDirection,Scattering,Skin};
    struct Write {Semantic semantic;unsigned offset{},bytes{},elements{},stride{};};
    struct Constant {
        unsigned slot{},bytes{};
        std::vector<Write> writes;
        std::vector<char> values;
        ComPtr<ID3D11Buffer> buffer;
    };
    struct Stage {
        unsigned kind{};
        bool requiresSkin{};
        std::vector<char> bytecode;
        ComPtr<ID3D11VertexShader> vertex;
        ComPtr<ID3D11PixelShader> pixel;
        ComPtr<ID3D11InputLayout> layout;
        std::map<unsigned,Constant> constants;
    };
    struct Pair {unsigned vertex{},pixel{};};
    std::map<unsigned,Stage> stages_;
    std::vector<Pair> combinations_;
    Pair selected_;
    std::vector<ShaderFeatureKey> features_;
    std::array<unsigned,7> dimensions_{};
    std::map<unsigned,std::string> textures_,samplers_;
    std::array<source_camera::V4,3> localToWorld_;
    unsigned matrixIndex_{};

    static Semantic semantic(const std::string& name) {
        static const std::map<std::string,Semantic> names{
            {"cDiff",Semantic::Diffuse},{"vDiffSrc",Semantic::DiffuseSource},{"texIdx",Semantic::TextureIndex},
            {"mW2P",Semantic::WorldToProjection},{"mL2W",Semantic::LocalToWorld},{"ksgClipPlane",Semantic::ClipPlanes},
            {"vEye",Semantic::Eye},{"nStageNum",Semantic::StageCount},{"vSAMap",Semantic::SamplerMapping},
            {"hFogType",Semantic::HeightType},{"cHFog",Semantic::HeightColor},{"hFogParam",Semantic::HeightParameter},
            {"hFogFlucPrms",Semantic::HeightFluctuation},{"vATest",Semantic::AlphaTest},{"vToneMapParams",Semantic::ToneMap},
            {"fog",Semantic::Fog},{"cFog",Semantic::FogColor},{"fogParam1For3PL",Semantic::FogParameter1},
            {"fogParam2For3PL",Semantic::FogParameter2},{"litDir",Semantic::LightDirection},{"Scat",Semantic::Scattering},
            {"bSkin",Semantic::Skin}};
        auto found=names.find(name);
        if(found==names.end())throw std::runtime_error("Route used shader constant has no producer: "+name);
        return found->second;
    }
    template<class T> static const T& need(const std::optional<T>& value,const char* name) {
        if(!value)throw std::runtime_error(name);return *value;
    }
    static void copy(Constant& constant,const Write& write,const void* source,unsigned bytes) {
        std::memcpy(constant.values.data()+write.offset,source,bytes);
    }
    template<class T,std::size_t N> static void scalarArray(Constant& constant,const Write& write,const std::array<T,N>& values) {
        for(std::size_t i=0;i<N;++i)std::memcpy(constant.values.data()+write.offset+i*16,&values[i],sizeof(T));
    }
    static void admitWrite(const Write& write,unsigned type,unsigned base,unsigned rows,unsigned columns) {
        unsigned expected=16;
        switch(write.semantic) {
            case Semantic::WorldToProjection:expected=64;break;
            case Semantic::LocalToWorld:
                if(type!=1||base!=3||rows!=1||columns!=4||
                    (write.elements!=3&&write.elements!=384)||write.bytes!=write.elements*16)
                    throw std::runtime_error("Route matrix-array import shape");
                return;
            case Semantic::ClipPlanes:expected=96;break;
            case Semantic::HeightColor:case Semantic::HeightParameter:expected=48;break;
            case Semantic::StageCount:case Semantic::AlphaTest:expected=4;break;
            case Semantic::ToneMap:expected=8;break;
            case Semantic::Scattering:expected=64;break;
            case Semantic::HeightType:case Semantic::Skin:
                if(type!=0||rows!=1||columns!=1||write.elements!=3||write.stride!=16||write.bytes!=36||
                    base!=(write.semantic==Semantic::Skin?1u:2u))
                    throw std::runtime_error("Route scalar3 import packing");
                return;
            case Semantic::HeightFluctuation:
                if(type!=0||base!=3||rows!=1||columns!=1||write.elements!=60||write.stride!=16||write.bytes!=948)
                    throw std::runtime_error("Route fluctuation import packing");
                return;
            default:break;
        }
        if(base!=3||write.bytes!=expected)throw std::runtime_error("Route named constant import shape");
        if(write.semantic==Semantic::WorldToProjection) {
            if(type!=2||rows!=4||columns!=4||write.elements!=0)
                throw std::runtime_error("Route row-matrix import packing");
        } else if(write.semantic==Semantic::StageCount||write.semantic==Semantic::AlphaTest) {
            if(type!=0||rows!=1||columns!=1||write.elements!=0)
                throw std::runtime_error("Route scalar import packing");
        } else {
            unsigned count=0,width=4;
            if(write.semantic==Semantic::TextureIndex||write.semantic==Semantic::LightDirection)count=1;
            else if(write.semantic==Semantic::ClipPlanes)count=6;
            else if(write.semantic==Semantic::HeightColor||write.semantic==Semantic::HeightParameter)count=3;
            else if(write.semantic==Semantic::Scattering)count=4;
            else if(write.semantic==Semantic::ToneMap)width=2;
            if(type!=1||rows!=1||columns!=width||write.elements!=count)
                throw std::runtime_error("Route vector import packing");
        }
    }
    static void update(Constant& constant,const RouteOriginalFrame& frame,const std::array<float,4>& tint,
        const std::array<source_camera::V4,3>& localToWorld) {
        // These are the actual unmodulated packet/category19 values, not
        // defaults for missing current material or a fabricated COLOR stream.
        const std::array<float,4> diffuseSource{0,0,1,1},textureIndex{0,0,0,0},samplerMapping{1,1,0,0};
        const float stageCount=1,alphaTest=source_camera::D(1.f,255.f);
        for(const auto& write:constant.writes) switch(write.semantic) {
            case Semantic::Diffuse:copy(constant,write,tint.data(),16);break;
            case Semantic::DiffuseSource:copy(constant,write,diffuseSource.data(),16);break;
            case Semantic::TextureIndex:copy(constant,write,textureIndex.data(),16);break;
            case Semantic::WorldToProjection:copy(constant,write,frame.worldToClip.data(),64);break;
            case Semantic::LocalToWorld:copy(constant,write,localToWorld.data(),48);break;
            case Semantic::ClipPlanes:copy(constant,write,frame.clipPlanes.data(),96);break;
            case Semantic::Eye:copy(constant,write,&frame.eye,16);break;
            case Semantic::StageCount:copy(constant,write,&stageCount,4);break;
            case Semantic::SamplerMapping:copy(constant,write,samplerMapping.data(),16);break;
            case Semantic::HeightType:scalarArray(constant,write,frame.heightFog.types);break;
            case Semantic::HeightColor:copy(constant,write,frame.heightFog.colors.data(),48);break;
            case Semantic::HeightParameter:copy(constant,write,frame.heightFog.parameters.data(),48);break;
            case Semantic::HeightFluctuation:scalarArray(constant,write,frame.heightFog.fluctuations);break;
            case Semantic::AlphaTest:copy(constant,write,&alphaTest,4);break;
            case Semantic::ToneMap:copy(constant,write,frame.toneMap.data(),8);break;
            case Semantic::Fog:copy(constant,write,&need(frame.fog,"Route current fog is absent"),16);break;
            case Semantic::FogColor:copy(constant,write,&need(frame.fogColor,"Route current fog color is absent"),16);break;
            case Semantic::FogParameter1:copy(constant,write,&need(frame.fogParameter1,"Route current vertex fog parameter is absent"),16);break;
            case Semantic::FogParameter2:copy(constant,write,&need(frame.fogParameter2,"Route current pixel fog parameter is absent"),16);break;
            case Semantic::LightDirection:copy(constant,write,&need(frame.lightDirection,"Route current light direction is absent"),16);break;
            case Semantic::Scattering:copy(constant,write,need(frame.scattering,"Route current scattering is absent").data(),64);break;
            case Semantic::Skin:throw std::runtime_error("Route rigid primitive selected a skinned matrix class");
        }
    }
public:
    void prepare(ID3D11Device* device,const fs::path& directory) {
        RouteOriginalPrograms next;
        std::string prefix;NativePreparedInputFile kindSource(directory/"route-program-kind.txt");
        std::getline(kindSource,prefix);
        // D362B0's actual CNS-HfTm branch erases all twelve feature words.
        // Other program prefixes need their own reached caller inputs.
        if(!kindSource||prefix!="CNS-HfTm")throw std::runtime_error("Route source program needs its actual feature caller");
        NativePreparedInputFile primitive(directory/"primitive-matrix.txt");std::array<unsigned,4> request;
        for(auto& word:request)primitive>>word;
        for(auto& row:next.localToWorld_)primitive>>row.X>>row.Y>>row.Z>>row.W;
        primitive>>next.matrixIndex_;
        if(!primitive||request!=std::array<unsigned,4>{})throw std::runtime_error("Route source primitive matrix declaration");
        unsigned id,kind;std::string file;
        NativePreparedInputFile programs(directory/"programs.txt");
        if(!programs)throw std::runtime_error("Route original program media is absent");
        while(programs>>id>>kind>>file) {
            Stage stage;stage.kind=kind;stage.bytecode=load(directory/file);
            if(kind!=0&&kind!=1)throw std::runtime_error("Route source shader stage requires its actual consumer");
            if(!next.stages_.emplace(id,std::move(stage)).second)throw std::runtime_error("Duplicate route source program");
        }
        if(!programs.eof()||next.stages_.empty())throw std::runtime_error("Route source program rows are incomplete");
        unsigned passes;NativePreparedInputFile dimensions(directory/"dimensions.txt");dimensions>>passes;
        for(auto& count:next.dimensions_)dimensions>>count;
        if(!dimensions||passes!=1||next.dimensions_[0]==0||next.matrixIndex_>=std::max(next.dimensions_[2],1u))throw std::runtime_error("Route source dimension declaration is unsupported");
        for(std::size_t axis=0;axis<next.dimensions_.size();++axis)
            if(axis!=0&&axis!=2&&next.dimensions_[axis]>1)
                throw std::runtime_error("Route source dimension needs its actual current selector");
        NativePreparedInputFile features(directory/"features.txt");
        ShaderFeatureKey key;
        while(features>>id>>key.flags>>key.directionalLights>>key.pointLights>>key.spotLights>>key.hemisphereLights
            >>key.directionalMode>>key.fogMode>>key.fogQuality>>key.shadowMode>>key.shadowQuality>>key.localShadowMode>>key.materialMode) {
            if(id!=next.features_.size())throw std::runtime_error("Route feature declaration order");
            next.features_.push_back(key);
        }
        if(!features.eof()||next.features_.size()!=next.dimensions_[0])throw std::runtime_error("Route source feature rows are incomplete");
        NativePreparedInputFile combinations(directory/"combinations.txt");Pair pair;
        while(combinations>>id>>pair.vertex>>pair.pixel) {
            if(id!=next.combinations_.size()||next.stages_.at(pair.vertex).kind!=1||next.stages_.at(pair.pixel).kind!=0)
                throw std::runtime_error("Route original combination declaration");
            next.combinations_.push_back(pair);
        }
        std::size_t expected=1;
        for(unsigned count:next.dimensions_)expected*=std::max(count,1u);
        if(!combinations.eof()||next.combinations_.size()!=expected)throw std::runtime_error("Route source combination extent");
        NativePreparedInputFile fields(directory/"constant-layout.txt");
        unsigned slot,total,offset,bytes,flags,type,base,rows,columns,elements;std::string name;
        while(fields>>id>>slot>>total>>name>>offset>>bytes>>flags>>type>>base>>rows>>columns>>elements) {
            if(slot>=14||!total||total%16||total>65536||offset>total||bytes>total-offset)
                throw std::runtime_error("Route constant-buffer import range");
            auto& constant=next.stages_.at(id).constants[slot];
            if(constant.bytes&&constant.bytes!=total)throw std::runtime_error("Route constant-buffer extent changed within a declaration");
            constant.slot=slot;constant.bytes=total;
            if(flags&2) {
                Write write{semantic(name),offset,bytes,elements,elements&&type==0?16u:columns*4u};
                admitWrite(write,type,base,rows,columns);
                if(write.semantic==Semantic::Skin||(write.semantic==Semantic::LocalToWorld&&elements!=3))
                    next.stages_.at(id).requiresSkin=true;
                constant.writes.push_back(write);
            }
        }
        if(!fields.eof())throw std::runtime_error("Route constant-layout row is incomplete");
        NativePreparedInputFile resources(directory/"resource-slots.txt");
        for(const auto& resource:readRouteProgramResourceSlots(resources)) {
            if(next.stages_.at(resource.program).kind!=0)
                throw std::runtime_error("Route source resource binding format");
            auto& bindings=resource.kind==2?next.textures_:next.samplers_;
            auto [at,inserted]=bindings.emplace(resource.slot,resource.name);
            if(!inserted&&at->second!=resource.name)throw std::runtime_error("Route source resource slot has conflicting names");
            if(resource.name!="sStage0"&&resource.name!="sExposure"&&resource.name!="__smpsStage0"&&resource.name!="__smpsExposure")
                throw std::runtime_error("Route source resource needs a new actual provider: "+resource.name);
        }
        NativePreparedInputFile configurations(directory/"configurations.txt");
        std::map<unsigned,std::string> providers;
        unsigned nameHash,secondWord,parameterCount;
        while(configurations>>id>>nameHash>>secondWord>>parameterCount>>name) {
            if(name!="ToneMap"&&name!="HeightFog")
                throw std::runtime_error("Route named configuration needs its actual provider: "+name);
            if(!providers.emplace(id,name).second)throw std::runtime_error("Route duplicate configuration ordinal");
        }
        if(!configurations.eof()||providers.size()!=2)throw std::runtime_error("Route provider chain is incomplete");
        // The complete real chain has no SSSTransmit provider. D362B0's
        // named query therefore preserves state0 independently of feature selection.
        std::map<std::string,std::map<unsigned,unsigned>> settings;
        NativePreparedInputFile parameters(directory/"configuration-parameters.txt");unsigned index,value;
        while(parameters>>id>>index>>nameHash>>value) {
            auto provider=providers.find(id);
            if(provider==providers.end()||!settings[provider->second].emplace(nameHash,value).second)
                throw std::runtime_error("Route provider parameter declaration");
        }
        if(!parameters.eof())throw std::runtime_error("Route provider parameter row is incomplete");
        const std::map<unsigned,unsigned> tone{{1744706u,3u},{2379u,0u},{1124103255u,4u},{1457372u,0u}};
        const std::map<unsigned,unsigned> height{{1135172717u,3u},{1744706u,0xFFFFFFFFu},
            {2890608965u,0xFFFFFFFFu},{2891140406u,0xFFFFFFFFu},{2891671847u,0xFFFFFFFFu},{1253090260u,1u}};
        if(settings["ToneMap"]!=tone||settings["HeightFog"]!=height)
            throw std::runtime_error("Route source configuration requires its corresponding provider semantics");
        // The CNS feature overwrite and this rigid primitive are immutable
        // resource-owned inputs. Resolve once for this resource generation;
        // every draw still uploads its accepted numeric frame.
        const ShaderFeatureKey currentFeature{};
        std::size_t selected=ShaderFeatureSelection::select(currentFeature,next.features_);
        for(std::size_t axis=1;axis<next.dimensions_.size();++axis)
            selected=selected*std::max(next.dimensions_[axis],1u)+(axis==2?next.matrixIndex_:0u);
        next.selected_=next.combinations_.at(selected);
        auto& vertex=next.stages_.at(next.selected_.vertex);auto& pixel=next.stages_.at(next.selected_.pixel);
        if(vertex.requiresSkin||pixel.requiresSkin)
            throw std::runtime_error("Route rigid primitive selected a skinned matrix class");
        check(device->CreateVertexShader(vertex.bytecode.data(),vertex.bytecode.size(),nullptr,&vertex.vertex),"Route selected original VS");
        bool instanced=false;vertex.layout=loadNativeModelLayout(device,directory,vertex.bytecode,instanced);
        if(instanced)throw std::runtime_error("Route rigid input layout became instanced");
        check(device->CreatePixelShader(pixel.bytecode.data(),pixel.bytecode.size(),nullptr,&pixel.pixel),"Route selected original PS");
        for(auto* stage:{&vertex,&pixel})for(auto& [binding,constant]:stage->constants) {
            constant.values.resize(constant.bytes); // Padding and genuinely unused variables only.
            D3D11_BUFFER_DESC desc{};desc.ByteWidth=constant.bytes;desc.Usage=D3D11_USAGE_DYNAMIC;
            desc.BindFlags=D3D11_BIND_CONSTANT_BUFFER;desc.CPUAccessFlags=D3D11_CPU_ACCESS_WRITE;
            check(device->CreateBuffer(&desc,nullptr,&constant.buffer),"Route original constant buffer");
        }
        *this=std::move(next);
    }

    void bind(ID3D11DeviceContext* context,const RouteOriginalFrame& frame,const std::array<float,4>& tint) {
        auto& vertex=stages_.at(selected_.vertex);auto& pixel=stages_.at(selected_.pixel);
        auto publish=[&](Stage& stage) {
            for(auto& [slot,constant]:stage.constants) {
                update(constant,frame,tint,localToWorld_);
                D3D11_MAPPED_SUBRESOURCE mapped{};
                check(context->Map(constant.buffer.Get(),0,D3D11_MAP_WRITE_DISCARD,0,&mapped),"Route original current constants");
                std::memcpy(mapped.pData,constant.values.data(),constant.values.size());context->Unmap(constant.buffer.Get(),0);
                auto buffer=constant.buffer.Get();
                if(stage.kind==1)context->VSSetConstantBuffers(slot,1,&buffer);else context->PSSetConstantBuffers(slot,1,&buffer);
            }
        };
        publish(vertex);publish(pixel);
        context->IASetInputLayout(vertex.layout.Get());
        context->VSSetShader(vertex.vertex.Get(),nullptr,0);context->PSSetShader(pixel.pixel.Get(),nullptr,0);
        context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);context->GSSetShader(nullptr,nullptr,0);
        if(!frame.exposure||!frame.exposureSampler)throw std::runtime_error("Route same-frame exposure provider is absent");
        for(const auto& [slot,name]:textures_)if(name=="sExposure")context->PSSetShaderResources(slot,1,&frame.exposure);
        for(const auto& [slot,name]:samplers_)if(name=="__smpsExposure")context->PSSetSamplers(slot,1,&frame.exposureSampler);
    }
    void bindImage(ID3D11DeviceContext* context,ID3D11ShaderResourceView* image,ID3D11SamplerState* sampler)const {
        for(const auto& [slot,name]:textures_)if(name=="sStage0")context->PSSetShaderResources(slot,1,&image);
        for(const auto& [slot,name]:samplers_)if(name=="__smpsStage0")context->PSSetSamplers(slot,1,&sampler);
    }
    void clearImages(ID3D11DeviceContext* context)const {
        ID3D11ShaderResourceView* empty=nullptr;
        for(const auto& [slot,name]:textures_)context->PSSetShaderResources(slot,1,&empty);
    }
};
} // namespace San14::Render
