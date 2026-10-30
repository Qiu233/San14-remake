#pragma once
#include "../../Runtime/PreparedFileInput.h"
#include <array>
#include <cstdint>
#include <filesystem>
#include <fstream>
#include <optional>
#include <stdexcept>

namespace San14::Render {
// One actual shared A024EF37 lifetime, retained across normal Main requests.
// Include after ShadowMask.h; its existing source D1B660 literal-draw helper
// supplies raster/depth restoration and keeps BA outside the RG write mask.
class SdfNativeSceneOwner {
public:
    void loadFreshNormalSource(const std::filesystem::path& directory) {
        if(sourceLoaded_)throw std::runtime_error("SDF normal source already owns a lifetime");
        NativePreparedInputFile input(directory/"normal-source.bin",std::ios::binary);
        std::array<std::uint32_t,18> words{};
        input.read(reinterpret_cast<char*>(words.data()),sizeof(words));
        if(!input||input.peek()!=std::char_traits<char>::eof()||words[0]!=0x31464453u||words[1]!=1||words[12]!=1)
            throw std::runtime_error("SDF normal fresh-default source packet is unavailable");
        directory_=directory;hostFlags_=words[2];slotOverride_=std::int32_t(words[3]);
        commonFlags_=words[4];mode_=words[5];settingsSlots_=words[6];records_=words[7];volumeGate_=words[8];
        objectPool_={words[9],words[10]};state40_=words[11];
        // The exported recipe is admitted ONLY by the actual TC/SC cold
        // normal default-allocator chain, whose v30 clears the entire130B.
        sphereCounts_.fill(0);dualCounts_.fill(0);publishedSide_=0;
        ocWidth_=std::int32_t(words[13]);ocHeight_=std::int32_t(words[14]);
        ocResource_=DXGI_FORMAT(words[15]);ocRead_=DXGI_FORMAT(words[16]);
        if(words[17]!=1)throw std::runtime_error("native-map.sdf.current_OC_mip_recipe");
        sourceLoaded_=true;
    }

    // Graph2 runs after the current View has actually been prepared. No
    // constructor, resource declaration or frame ordinal supplies ready=true.
    void prepareCurrentView(std::uint32_t actualViewSlot,std::uint32_t actualHostFlags,
        std::uint32_t actualCommonFlags,unsigned width,unsigned height) {
        if(!sourceLoaded_||actualViewSlot>=32||!width||!height)
            throw std::runtime_error("SDF actual current View preparation is unavailable");
        selectedView_=actualViewSlot;commonFlags_=actualCommonFlags;hostFlags_=actualHostFlags;
        // Source D5F170 preserves an existing pool when both words are0.
        // This normal scope starts with the explicit D019B0 zero pool and
        // its no-later-free-list-publisher admission, not an empty Scene claim.
        if(objectPool_[0]||objectPool_[1])
            throw std::runtime_error("native-map.sdf.current_operation0_View_collection_required");
        const unsigned divisor=mode_==1&&(hostFlags_&3u)==1&&settingsSlots_?64u:0u;
        const auto slots=slotOverride_>=0?unsigned(slotOverride_):settingsSlots_;
        if(divisor&&volumeGate_&&slots)
            throw std::runtime_error("native-map.sdf.current_operation1_View_collection_required");
        currentViewPrepared_=true;
        managerPresent_=true;
        if(!objectAttached_)objectAttached_=true; // false-return source attach
        // With an actual null View1C60, D7C450 cannot publish another side.
        // These are retained actual counts, never reset to0 at this query.
        if(!volumeAttached_){volumeAttached_=true;volumeCount_=0;}
        else if(commonFlags_&(1u<<8)) {
            // Actual View1C58 is null from the admitted pool acquisition;
            // D265E0 returnsfalse before atlas/history. D7C6E0 still advances.
            sweepSide_=(sweepSide_+1)&3u;
        }
    }

    // Only the real completed operation0 upload/publication may use this cut.
    // It changes the same owner read by all three wrapper instances.
    void adoptCompletedObjectPublication(unsigned side,unsigned spheres,unsigned duals) {
        if(!objectAttached_||side>=4)throw std::runtime_error("SDF operation0 publication lifecycle");
        sphereCounts_[side]=spheres;dualCounts_[side]=duals;publishedSide_=side;
    }
    void adoptCompletedVolumePopulation(unsigned count) {
        if(!volumeAttached_)throw std::runtime_error("SDF operation1 publication lifecycle");
        volumeCount_=count;
    }
    bool hasCurrentPopulation()const {
        if(!managerPresent_)return false;
        if(volumeAttached_&&volumeCount_)return true; // source short circuit
        return objectAttached_&&std::uint32_t(sphereCounts_[publishedSide_]+dualCounts_[publishedSide_])!=0;
    }

    void prepareFallbackGpu(ID3D11Device* device,ID3D11Texture2D* rs,
        DXGI_FORMAT viewFormat,unsigned width,unsigned height) {
        if(!currentViewPrepared_)throw std::runtime_error("SDF fallback has no accepted current View");
        auto extent=[](int declared,unsigned current){return declared>0?unsigned(declared):declared==0?current:current>>(unsigned(-declared)&31);};
        D3D11_TEXTURE2D_DESC oc{};oc.Width=extent(ocWidth_,width);oc.Height=extent(ocHeight_,height);
        oc.MipLevels=1;oc.ArraySize=1;oc.Format=ocResource_;oc.SampleDesc.Count=1;oc.Usage=D3D11_USAGE_DEFAULT;
        oc.BindFlags=D3D11_BIND_SHADER_RESOURCE|D3D11_BIND_UNORDERED_ACCESS;
        if(!oc.Width||!oc.Height)throw std::runtime_error("SDF actual OC extent is zero");
        check(device->CreateTexture2D(&oc,nullptr,&ocStorage_),"SDF actual source OC allocation");
        D3D11_SHADER_RESOURCE_VIEW_DESC ocRead{};ocRead.Format=ocRead_;ocRead.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;ocRead.Texture2D.MipLevels=1;
        check(device->CreateShaderResourceView(ocStorage_.Get(),&ocRead,&ocView_),"SDF actual source OC view");
        // Allocation is only a resolved current wrapper. It is not an
        // initialized previous-image receipt; the fallback does not sample it.
        NativeRs69Fallback next;next.enabled=true;next.mask=3;next.width=width;next.height=height;
        next.color[0]=next.color[1]=0;next.color[2]=next.color[3]=1;
        auto originalVS=load(directory_/"fullscreen-vs.dxbc");
        check(device->CreateVertexShader(originalVS.data(),originalVS.size(),nullptr,&next.vs),"SDF original fullscreen VS");
        // Exact intrinsic D1B660 literal fill, independent of optional SDF
        // compute programs. No captured pixels or guessed lighting constants.
        const char* fill="cbuffer FillColor:register(b0){float4 color;} float4 main():SV_Target{return color;}";
        ComPtr<ID3DBlob> code,error;
        check(D3DCompile(fill,std::strlen(fill),"source-D1B660-literal-fill",nullptr,nullptr,"main","ps_5_0",
            D3DCOMPILE_OPTIMIZATION_LEVEL3,0,&code,&error),"SDF literal fallback compile");
        check(device->CreatePixelShader(code->GetBufferPointer(),code->GetBufferSize(),nullptr,&next.ps),"SDF source literal fallback PS");
        std::vector<char> bytes(16);std::memcpy(bytes.data(),next.color,16);
        next.constants=buffer(device,bytes,D3D11_BIND_CONSTANT_BUFFER);
        D3D11_RENDER_TARGET_VIEW_DESC view{};view.Format=viewFormat;view.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2DARRAY;
        view.Texture2DArray.FirstArraySlice=3;view.Texture2DArray.ArraySize=1;
        check(device->CreateRenderTargetView(rs,&view,&next.target),"SDF actual RS3 fallback target");
        next.state40=state40_;fallback_=std::move(next);
    }

    bool executeCurrentNode(ID3D11Device* device,ID3D11DeviceContext* context,
        NativeSourceDepthState& logical,ID3D11DepthStencilView* depth,
        unsigned sourceNode,bool actualRequiredBuffersResolved,
        std::optional<unsigned> actualView158C=std::nullopt) {
        if(!currentViewPrepared_||!actualRequiredBuffersResolved||!ocView_)return false;
        unsigned threshold;
        switch(sourceNode){case 69:threshold=0;break;case 120:threshold=1;break;case 164:threshold=2;break;
            default:throw std::runtime_error("SDF wrapper node identity");}
        if(hasCurrentPopulation()) {
            if(threshold==0)
                throw std::runtime_error("native-map.sdf.actual_nonempty_prepared_producer_required");
            if(!actualView158C)throw std::runtime_error("native-map.sdf.current_View158C_required");
            if(*actualView158C>=threshold)
                throw std::runtime_error("native-map.sdf.actual_nonempty_prepared_producer_required");
        }
        if(!fallback_.enabled)throw std::runtime_error("SDF current RS3 fallback is not prepared");
        fallback_.state40=state40_;
        const bool written=executeRs69Fallback(device,context,logical,fallback_,depth);
        // Real helper-return boundary, including retained once/no-pixel return.
        state40_=fallback_.state40;
        return written;
    }
    std::uint32_t state40()const noexcept{return state40_;}
    void close(){*this=SdfNativeSceneOwner{};}
private:
    std::filesystem::path directory_;
    bool sourceLoaded_{},currentViewPrepared_{},managerPresent_{},objectAttached_{},volumeAttached_{};
    unsigned hostFlags_{},commonFlags_{},mode_{},settingsSlots_{},records_{},volumeGate_{},selectedView_{};
    std::int32_t slotOverride_{};
    std::array<unsigned,2> objectPool_{};
    std::array<unsigned,4> sphereCounts_{},dualCounts_{};
    unsigned publishedSide_{},volumeCount_{},sweepSide_{};
    std::uint32_t state40_{};
    NativeRs69Fallback fallback_;
    int ocWidth_{},ocHeight_{};
    DXGI_FORMAT ocResource_{},ocRead_{};
    ComPtr<ID3D11Texture2D> ocStorage_;
    ComPtr<ID3D11ShaderResourceView> ocView_;
};
}
