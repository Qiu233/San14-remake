#pragma once
#include "MainStarResources.h"
#include "MainStarRuntime.h"
#include "MainSkyBlendState.h"

namespace San14::Render {
class MainStarPass {
public:
    void prepare(ID3D11Device* device,const fs::path& source,StarTwinklePhase& phase) {
        resources_=prepareNativeMainStarResources(device,source);
        initial_=Interop::readMainStarInputs(source/"current-state.bin");
        runtime_.bindTwinklePhase(phase);
    }
    void updateNode3(ID3D11Device* device,const MainStarCurrentInputs* current,float delta,
        const source_camera::CameraTransform& camera,float heightTerm,bool updateOrientation,
        const source_weather_fog::Power& power) {
        // A fresh zero shader declaration skips creation of the CPU Star owner.
        // Later package references do not clear an already constructed owner.
        if(!resources_.ownerPresent)return;
        const auto& inputs=current?*current:initial_;
        if(auto candidate=runtime_.prepareConfiguration(inputs.settings)) {
            std::array<ComPtr<ID3D11Buffer>,2> buffers;
            if(!candidate->output.empty()) {
                D3D11_BUFFER_DESC description{};
                description.ByteWidth=UINT(candidate->output.size()*sizeof(MainStarShowerVertex));
                description.BindFlags=D3D11_BIND_VERTEX_BUFFER;description.Usage=D3D11_USAGE_DYNAMIC;
                description.CPUAccessFlags=D3D11_CPU_ACCESS_WRITE;
                for(auto& output:buffers)
                    check(device->CreateBuffer(&description,nullptr,&output),"Main Star shower candidate buffer");
            }
            // Allocation of both physical sides precedes the single install.
            runtime_.commitConfiguration(std::move(*candidate));showerBuffers_=std::move(buffers);activeSide_=0;
        }
        runtime_.updateNode3(inputs,delta,camera,heightTerm,updateOrientation,power);
    }
    void execute(ID3D11Device* device,ID3D11DeviceContext* context,unsigned pass,
        const source_camera::CameraTransform& camera,const SkyDrawPolicy& policy,
        const CommonRenderPolicy& common,UINT width,UINT height,UINT controllerWidth,UINT targetCount,
        ID3D11ShaderResourceView* exposure,ID3D11SamplerState* exposureSampler,
        const source_weather_fog::Power& power) {
        if(policy.suppressStars||!resources_.ownerPresent)return;
        const auto& settings=runtime_.inputs().settings;
        if(pass&&runtime_.expectedCount(settings)<=1)return;
        // The caller installs its reached blend before the draw body tests
        // geometry count and diameter. A skipped body does not undo that state.
        bindNativeMainSkyBlend(device,context,blend_[pass],false,true,common.skyAlphaBlend,targetCount);
        const auto count=pass?UINT(runtime_.showerCapacity()):resources_.pointCount;
        if(!count||!(settings.diameterTexel>=.5f))return;
        const unsigned variant=settings.exposureScale<0?1u:0u;
        if(variant&&(!exposure||!exposureSampler))
            throw std::runtime_error("Main Star exposure variant needs the actual output texture and sampler");
        if(pass&&runtime_.outputDirty()) {
            const unsigned next=1u-activeSide_;D3D11_MAPPED_SUBRESOURCE mapped{};
            check(context->Map(showerBuffers_[next].Get(),0,D3D11_MAP_WRITE_DISCARD,0,&mapped),"Main Star shower upload");
            std::memcpy(mapped.pData,runtime_.output().data(),runtime_.output().size()*sizeof(MainStarShowerVertex));
            context->Unmap(showerBuffers_[next].Get(),0);activeSide_=next;runtime_.acceptUpload();
        }
        auto& program=resources_.variants[variant][pass];
        const auto material=runtime_.prepareDraw(camera,width,height,controllerWidth,
            resources_.twinkleTable,!resources_.textures.empty(),power);
        publishNativeMainStarMaterial(context,program,material);
        context->IASetInputLayout(program.shaders.layout.Get());
        context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_POINTLIST);
        auto vertices=pass?showerBuffers_[activeSide_].Get():resources_.controllerPoints.Get();
        UINT stride=pass?32u:20u,offset=0;context->IASetVertexBuffers(0,1,&vertices,&stride,&offset);
        context->VSSetShader(program.shaders.vs.Get(),nullptr,0);
        context->GSSetShader(program.shaders.gs.Get(),nullptr,0);
        context->PSSetShader(program.shaders.ps.Get(),nullptr,0);
        context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);
        ID3D11ShaderResourceView* starView=resources_.textures.empty()?nullptr:resources_.textures.front().view.Get();
        ID3D11SamplerState* starSampler=resources_.textures.empty()?nullptr:resources_.textures.front().sampler.Get();
        auto exposureView=variant?exposure:nullptr;auto sampler=variant?exposureSampler:nullptr;
        context->PSSetShaderResources(0,1,&starView);context->PSSetSamplers(0,1,&starSampler);
        context->VSSetShaderResources(8,1,&exposureView);context->VSSetSamplers(8,1,&sampler);
        context->Draw(count,0);
        ID3D11ShaderResourceView* noView=nullptr;ID3D11SamplerState* noSampler=nullptr;
        context->PSSetShaderResources(0,1,&noView);context->PSSetSamplers(0,1,&noSampler);
        context->VSSetShaderResources(8,1,&noView);context->VSSetSamplers(8,1,&noSampler);
        context->GSSetShader(nullptr,nullptr,0);
    }
private:
    NativeMainStarResources resources_;
    MainStarCurrentInputs initial_;
    MainStarRuntime runtime_;
    std::array<ComPtr<ID3D11Buffer>,2> showerBuffers_;
    unsigned activeSide_{};
    std::array<NativeMainSkyBlendCache,2> blend_;
};
} // namespace San14::Render
