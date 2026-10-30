#include "../../Runtime/PreparedFileInput.h"
// Original Effect texture groups. Created once for this native experiment.
struct NativeEffectTexture {
    ComPtr<ID3D11Texture2D> texture;
    ComPtr<ID3D11ShaderResourceView> view;
    ComPtr<ID3D11SamplerState> sampler;
};
struct NativeEffectProviders {
    std::map<std::string,NativeEffectTexture> textures;
    std::map<std::string,std::map<UINT,std::string>> groups;
};
static NativeEffectProviders loadNativeEffectProviders(ID3D11Device* device,const fs::path& source) {
    NativeEffectProviders out;std::string key,file,samplerFile;UINT sampled;
    NativePreparedInputFile rows(source/"textures.txt");if(!rows)throw std::runtime_error("Original Effect texture manifest absent");
    while(rows>>key>>file>>sampled>>samplerFile) {
        NativeEffectTexture t;t.texture=loadTexture(device,source/file);
        D3D11_TEXTURE2D_DESC td{};t.texture->GetDesc(&td);
        D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=DXGI_FORMAT(sampled);
        sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sd.Texture2D.MipLevels=td.MipLevels;
        check(device->CreateShaderResourceView(t.texture.Get(),&sd,&t.view),"original Effect texture SRV");
        t.sampler=ambientSampler(device,source/samplerFile);out.textures.emplace(key,std::move(t));
    }
    NativePreparedInputFile groupRows(source/"texture-groups.txt");std::string line,group;UINT slot;
    if(!groupRows)throw std::runtime_error("Original Effect group manifest absent");
    while(std::getline(groupRows,line)) {
        if(line.empty()||line[0]=='#')continue;std::istringstream row(line);row>>group>>slot>>key;
        if(!row)throw std::runtime_error("Original Effect group row is incomplete");
        (void)out.textures.at(key);out.groups[group].emplace(slot,key);
    }
    std::cout<<"source Effect providers textures="<<out.textures.size()<<" groups="<<out.groups.size()<<'\n';
    return out;
}
