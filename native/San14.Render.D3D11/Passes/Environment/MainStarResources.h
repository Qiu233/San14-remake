#pragma once
#include "MainStarMaterialInputs.h"
#include <limits>

// Include after the shared D3D11 program and immutable texture upload helpers.
// Main owns these resources independently of the WORK cube renderer.
struct NativeMainStarConstants {
    ComPtr<ID3D11Buffer> vertex,geometry,pixel;
    std::vector<char> vertexBytes,geometryBytes,pixelBytes;
    San14::Render::Interop::MainStarPassLayout layout;
};
struct NativeMainStarProgram {
    AmbientNativeProgram shaders;
    NativeMainStarConstants constants;
};
struct NativeMainStarResources {
    bool ownerPresent{};
    std::array<std::array<NativeMainStarProgram,2>,2> variants;
    std::array<source_camera::V4,16> twinkleTable{};
    ComPtr<ID3D11Buffer> controllerPoints;
    UINT pointCount{};
    std::vector<AmbientNativeTextureBinding> textures;
};

static AmbientNativeProgram prepareNativeMainStarProgram(ID3D11Device* device,
    const fs::path& directory,unsigned pass) {
    const auto prefix="stage"+std::to_string(pass);
    const auto vertex=load(directory/(prefix+"-vs.dxbc"));
    const auto geometry=load(directory/(prefix+"-gs.dxbc"));
    const auto pixel=load(directory/(prefix+"-ps.dxbc"));
    AmbientNativeProgram result;
    check(device->CreateVertexShader(vertex.data(),vertex.size(),nullptr,&result.vs),"Main Star selected vertex shader");
    check(device->CreateGeometryShader(geometry.data(),geometry.size(),nullptr,&result.gs),"Main Star selected geometry shader");
    check(device->CreatePixelShader(pixel.data(),pixel.size(),nullptr,&result.ps),"Main Star selected pixel shader");
    const D3D11_INPUT_ELEMENT_DESC points[] {
        {"POSITION",0,DXGI_FORMAT_R32G32B32_FLOAT,0,0,D3D11_INPUT_PER_VERTEX_DATA,0},
        {"PSIZE",0,DXGI_FORMAT_R32_FLOAT,0,12,D3D11_INPUT_PER_VERTEX_DATA,0},
        {"COLOR",0,DXGI_FORMAT_R8G8B8A8_UNORM,0,16,D3D11_INPUT_PER_VERTEX_DATA,0}
    };
    const D3D11_INPUT_ELEMENT_DESC shower[] {
        {"POSITION",0,DXGI_FORMAT_R32G32B32_FLOAT,0,0,D3D11_INPUT_PER_VERTEX_DATA,0},
        {"TEXCOORD",0,DXGI_FORMAT_R32G32B32_FLOAT,0,12,D3D11_INPUT_PER_VERTEX_DATA,0},
        {"PSIZE",0,DXGI_FORMAT_R32_FLOAT,0,24,D3D11_INPUT_PER_VERTEX_DATA,0},
        {"COLOR",0,DXGI_FORMAT_R8G8B8A8_UNORM,0,28,D3D11_INPUT_PER_VERTEX_DATA,0}
    };
    check(device->CreateInputLayout(pass?shower:points,pass?4u:3u,
        vertex.data(),vertex.size(),&result.layout),"Main Star actual point-stream layout");
    return result;
}
static NativeMainStarResources prepareNativeMainStarResources(ID3D11Device* device,const fs::path& directory) {
    NativeMainStarResources result;
    const auto presence=load(directory/"program-presence.bin");std::uint32_t present{};
    if(presence.size()!=sizeof(present))throw std::runtime_error("Main Star program declaration extent");
    std::memcpy(&present,presence.data(),sizeof(present));
    if(present>1)throw std::runtime_error("Main Star program declaration format");
    result.ownerPresent=present!=0;
    if(!result.ownerPresent)return result;
    result.twinkleTable=San14::Render::Interop::readMainStarTwinkleTable(directory/"twinkle-table.bin");
    const auto points=load(directory/"controller-points.bin");
    if(points.size()%20||points.size()>std::numeric_limits<UINT>::max())
        throw std::runtime_error("Main Star controller point-stream extent");
    result.pointCount=UINT(points.size()/20);
    if(!points.empty())result.controllerPoints=buffer(device,points,D3D11_BIND_VERTEX_BUFFER);
    result.textures=ambientTextures(device,directory,"star-textures.tsv",std::nullopt);
    for(unsigned variant=0;variant<2;++variant) {
        const auto source=directory/("variant"+std::to_string(variant));
        const auto layouts=San14::Render::Interop::readMainStarMaterialLayout(source/"material-layout.bin");
        for(unsigned pass=0;pass<2;++pass) {
            auto& program=result.variants[variant][pass];
            program.shaders=prepareNativeMainStarProgram(device,source,pass);
            auto& constants=program.constants;constants.layout=layouts[pass];
            auto create=[&](const San14::Render::Interop::MainStarStageLayout& layout,
                std::vector<char>& bytes,ComPtr<ID3D11Buffer>& output) {
                bytes.resize(layout.byteSize);
                D3D11_BUFFER_DESC desc{};desc.ByteWidth=layout.byteSize;desc.Usage=D3D11_USAGE_DEFAULT;
                desc.BindFlags=D3D11_BIND_CONSTANT_BUFFER;
                D3D11_SUBRESOURCE_DATA initial{};initial.pSysMem=bytes.data();
                check(device->CreateBuffer(&desc,&initial,&output),"Main Star selected-layout constants");
            };
            create(constants.layout.vertex,constants.vertexBytes,constants.vertex);
            create(constants.layout.geometry,constants.geometryBytes,constants.geometry);
            create(constants.layout.pixel,constants.pixelBytes,constants.pixel);
        }
    }
    return result;
}
static void publishNativeMainStarMaterial(ID3D11DeviceContext* context,NativeMainStarProgram& program,
    const San14::Render::MainStarMaterialState& state) {
    auto& constants=program.constants;
    constants.layout.vertex.pack(state,constants.vertexBytes);
    constants.layout.geometry.pack(state,constants.geometryBytes);
    constants.layout.pixel.pack(state,constants.pixelBytes);
    context->UpdateSubresource(constants.vertex.Get(),0,nullptr,constants.vertexBytes.data(),0,0);
    context->UpdateSubresource(constants.geometry.Get(),0,nullptr,constants.geometryBytes.data(),0,0);
    context->UpdateSubresource(constants.pixel.Get(),0,nullptr,constants.pixelBytes.data(),0,0);
    auto vertex=constants.vertex.Get(),geometry=constants.geometry.Get(),pixel=constants.pixel.Get();
    context->VSSetConstantBuffers(constants.layout.vertex.slot,1,&vertex);
    context->GSSetConstantBuffers(constants.layout.geometry.slot,1,&geometry);
    context->PSSetConstantBuffers(constants.layout.pixel.slot,1,&pixel);
}
