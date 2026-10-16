#pragma once
#include <limits>

namespace San14::Render {
enum class OcclusionComposition : unsigned { Default3,Common138,Common140 };
struct OcclusionStockSource {
    std::vector<char> vertex,default3,common138,common140;
    D3D11_SAMPLER_DESC sampler{};
};
// Common publication is represented by successful creation of the supplied
// project shaders. Exit always uses its own Default3 binding, independent of
// which groups happened to compose in the current frame.
class OcclusionStock {
public:
    void prepare(ID3D11Device* device,const OcclusionStockSource& source,unsigned width,unsigned height) {
        if(source.vertex.empty()||source.default3.empty())throw std::runtime_error("Occlusion stock media is absent");
        check(device->CreateVertexShader(source.vertex.data(),source.vertex.size(),nullptr,&vertex_),"Occlusion stock VS");
        auto pixel=[&](const std::vector<char>& code,ComPtr<ID3D11PixelShader>& shader) {
            if(!code.empty())check(device->CreatePixelShader(code.data(),code.size(),nullptr,&shader),"Occlusion stock PS publication");
        };
        pixel(source.default3,default3_);pixel(source.common138,common138_);pixel(source.common140,common140_);
        check(device->CreateSamplerState(&source.sampler,&sampler_),"Occlusion composition sampler");
        const D3D11_INPUT_ELEMENT_DESC layout[]{
            {"POSITION",0,DXGI_FORMAT_R32G32B32_FLOAT,0,0,D3D11_INPUT_PER_VERTEX_DATA,0},
            {"COLOR",0,DXGI_FORMAT_R8G8B8A8_UNORM,0,12,D3D11_INPUT_PER_VERTEX_DATA,0},
            {"TEXCOORD",0,DXGI_FORMAT_R32G32_FLOAT,0,16,D3D11_INPUT_PER_VERTEX_DATA,0}};
        check(device->CreateInputLayout(layout,UINT(std::size(layout)),source.vertex.data(),source.vertex.size(),&layout_),"Occlusion stock layout");
        struct Vertex {float x,y,z;unsigned color;float u,v;};
        const std::array<Vertex,4> rectangle{{{0,0,0,~0u,0,0},{float(width),0,0,~0u,1,0},
            {0,float(height),0,~0u,0,1},{float(width),float(height),0,~0u,1,1}}};
        std::vector<char> bytes(sizeof(rectangle));std::memcpy(bytes.data(),rectangle.data(),bytes.size());
        vertices_=buffer(device,bytes,D3D11_BIND_VERTEX_BUFFER);
        const std::array<float,8> constants{2.f/float(width),-2.f/float(height),-1,1,1,1,1,1};
        bytes.resize(sizeof(constants));std::memcpy(bytes.data(),constants.data(),bytes.size());
        vertexConstants_=buffer(device,bytes,D3D11_BIND_CONSTANT_BUFFER);
        std::array<float,4> pixelConstants{-std::numeric_limits<float>::max(),0,0,0};
        bytes.resize(sizeof(pixelConstants));std::memcpy(bytes.data(),pixelConstants.data(),bytes.size());
        defaultConstants_=buffer(device,bytes,D3D11_BIND_CONSTANT_BUFFER);
        D3D11_BUFFER_DESC bd{};bd.ByteWidth=16;bd.BindFlags=D3D11_BIND_CONSTANT_BUFFER;bd.Usage=D3D11_USAGE_DEFAULT;
        check(device->CreateBuffer(&bd,nullptr,&fadeConstants_),"Occlusion current fade CB");
    }
    bool hasCommon138() const {return common138_.Get()!=nullptr;}
    bool hasCommon140() const {return common140_.Get()!=nullptr;}
    void bind(ID3D11DeviceContext* context,OcclusionComposition composition,float fade) const {
        ID3D11PixelShader* pixel=nullptr;ID3D11Buffer* constants=nullptr;
        switch(composition) {
            case OcclusionComposition::Default3:pixel=default3_.Get();constants=defaultConstants_.Get();break;
            case OcclusionComposition::Common138:pixel=common138_.Get();constants=fadeConstants_.Get();break;
            case OcclusionComposition::Common140:pixel=common140_.Get();constants=fadeConstants_.Get();break;
        }
        if(!pixel)throw std::runtime_error("Selected occlusion Common shader was not published");
        if(composition!=OcclusionComposition::Default3) {
            const std::array<float,4> values{fade,0,0,0};context->UpdateSubresource(fadeConstants_.Get(),0,nullptr,values.data(),0,0);
        }
        context->VSSetShader(vertex_.Get(),nullptr,0);context->PSSetShader(pixel,nullptr,0);
        auto vertexConstants=vertexConstants_.Get();context->VSSetConstantBuffers(0,1,&vertexConstants);
        context->PSSetConstantBuffers(0,1,&constants);
        auto sampler=sampler_.Get();context->PSSetSamplers(0,1,&sampler);
        context->IASetInputLayout(layout_.Get());context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP);
        auto vertices=vertices_.Get();UINT stride=24,offset=0;context->IASetVertexBuffers(0,1,&vertices,&stride,&offset);
        context->IASetIndexBuffer(nullptr,DXGI_FORMAT_UNKNOWN,0);
    }
    void bindExit(ID3D11DeviceContext* context) const {
        context->PSSetShader(default3_.Get(),nullptr,0);
        auto constants=defaultConstants_.Get();context->PSSetConstantBuffers(0,1,&constants);
    }
private:
    ComPtr<ID3D11VertexShader> vertex_;
    ComPtr<ID3D11PixelShader> default3_,common138_,common140_;
    ComPtr<ID3D11Buffer> vertices_,vertexConstants_,defaultConstants_,fadeConstants_;
    ComPtr<ID3D11InputLayout> layout_;
    ComPtr<ID3D11SamplerState> sampler_;
};
} // namespace San14::Render
