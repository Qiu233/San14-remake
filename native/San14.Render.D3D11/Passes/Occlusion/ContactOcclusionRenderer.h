#pragma once
#include "OcclusionResourceBank.h"
#include "OcclusionDrawState.h"
#include "ContactOcclusionCurrent.h"
#include "Interop/OcclusionVariantSelection.h"

namespace San14::Render {
class ContactOcclusionRenderer {
public:
    void prepare(ID3D11Device* device,OcclusionResourceBank resources,
        const D3D11_RASTERIZER_DESC& raster,const D3D11_BLEND_DESC& blend,
        const NativeDepthMirror& depth,ID3D11Texture2D* normals,DXGI_FORMAT normalFormat,
        ID3D11Texture2D* ab,DXGI_FORMAT abTargetFormat,ID3D11DepthStencilView* mainDepth) {
        if(!normals||!ab||!mainDepth||!depth.depth||!depth.stencil)
            throw std::runtime_error("Contact occlusion requires real Main attachments and the independent depth mirror");
        resources_=std::move(resources);depth_=depth;normals_=normals;ab_=ab;mainDepth_=mainDepth;
        D3D11_TEXTURE2D_DESC descriptor{};ab->GetDesc(&descriptor);width_=descriptor.Width;height_=descriptor.Height;
        D3D11_RENDER_TARGET_VIEW_DESC target{};target.Format=abTargetFormat;target.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D;
        check(device->CreateRenderTargetView(ab,&target,&abTarget_),"Contact occlusion existing AB RTV");
        D3D11_SHADER_RESOURCE_VIEW_DESC view{};view.Format=normalFormat;view.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;
        view.Texture2D.MipLevels=1;check(device->CreateShaderResourceView(normals,&view,&normalsView_),"Contact occlusion current NS view");
        noise_=createOcclusionNoise(device,resources_.noise);
        for(const auto& description:resources_.samplers) {
            ComPtr<ID3D11SamplerState> sampler;check(device->CreateSamplerState(&description,&sampler),"Contact occlusion declared sampler");
            samplers_.push_back(std::move(sampler));
        }
        stock_.prepare(device,resources_.stock,width_,height_);
        state_.prepare(device,raster,blend,resources_.compositionBlend);
    }

    void execute(ID3D11Device* device,ID3D11DeviceContext* context,NativeSourceDepthState& logical,
        const D3D11_VIEWPORT& returnViewport,const contact_occlusion::Settings& settings,
        const source_camera::CameraOptics& lens,unsigned activeGroups,unsigned hostClass,
        const std::vector<contact_occlusion::LightRecord>& lights,bool commonPresent) {
        const contact_occlusion::CommonPublication common{commonPresent&&stock_.hasCommon138(),commonPresent&&stock_.hasCommon140()};
        const auto current=contact_occlusion::prepareCurrent(settings,lens,width_,height_,activeGroups,hostClass,lights,common,
            returnViewport.MinDepth,returnViewport.MaxDepth);
        if(!current.cameraGate) {
            if(settings.contrast>0)throw std::runtime_error("Contact occlusion alternate View population has no runtime owner");
            return;
        }
        // Primary entry precedes group admission in the source. Zero admitted
        // groups retain these narrow writers, without scratch or stock exit.
        state_.bindEntry(device,context,logical);
        if(current.groups.empty())return;
        const auto& selected=resources_.match(Interop::prepareOcclusionVariantKey(resources_,current.selection));
        prepareSurfaces(device,current,settings);
        OcclusionConstantValues values;values.depth=current.depth;values.view=current.view;values.blur=current.blur;
        values.radius=current.radius;values.ray=current.ray;values.occlusion=current.occlusion;values.radiusRange=current.radiusRange;
        for(const auto& group:current.groups)values.references.push_back(group.mask);
        const BOOL entryDepthTest=logical.description.DepthEnable;
        context->OMSetRenderTargets(0,nullptr,nullptr);
        context->GSSetShader(nullptr,nullptr,0);context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);
        for(const auto pass:current.passes) {
            if(pass>4)throw std::runtime_error("Unsupported selected occlusion pass");
            const auto& recipe=selected.passes.at(pass);const auto& surface=surfaces_[pass];
            programs_.bind(device,context,recipe.vertex,resources_.program(recipe.vertex,1),values,surface.width,surface.height);
            programs_.bind(device,context,recipe.pixel,resources_.program(recipe.pixel,0),values,surface.width,surface.height);
            state_.bindScratch(device,context,logical);
            std::array<ID3D11ShaderResourceView*,32> inputs{};
            std::array<ID3D11SamplerState*,D3D11_COMMONSHADER_SAMPLER_SLOT_COUNT> samplers{};
            for(const auto& binding:recipe.textures)inputs.at(binding.slot)=texture(binding.input,pass,current);
            // Samplers follow the actual wrapper supplying each input. View
            // depth and half-depth are generated mip-one images; raw MainD1
            // retains its declared sampler key. Filtering is a current choice.
            samplers[0]=samplers_.at(depthSampler(pass,current)).Get();
            if(pass==2) {samplers[1]=samplers_.at(1).Get();samplers[2]=samplers_.at(2).Get();}
            if(pass==3||pass==4)samplers[3]=samplers_.at(current.selection.linearBlur?4:0).Get();
            context->PSSetShaderResources(0,UINT(inputs.size()),inputs.data());context->PSSetSamplers(0,UINT(samplers.size()),samplers.data());
            const D3D11_VIEWPORT viewport{0,0,float(surface.width),float(surface.height),0,1};context->RSSetViewports(1,&viewport);
            context->IASetInputLayout(nullptr);context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
            ID3D11Buffer* empty=nullptr;UINT stride=0,offset=0;context->IASetVertexBuffers(0,1,&empty,&stride,&offset);
            context->IASetIndexBuffer(nullptr,DXGI_FORMAT_UNKNOWN,0);
            auto target=surface.target.Get();context->OMSetRenderTargets(1,&target,pass==4?mainDepth_.Get():nullptr);
            context->Draw(3,0);
            unbindInputs(context);context->OMSetRenderTargets(0,nullptr,nullptr);
        }
        const D3D11_VIEWPORT viewport{0,0,float(width_),float(height_),0,1};context->RSSetViewports(1,&viewport);
        for(const auto& group:current.groups) {
            state_.bindComposition(device,context,logical,group.mask,current.stencilReadMask);
            stock_.bind(context,composition(group.composition),group.fade);
            ID3D11ShaderResourceView* inputs[]{surfaces_[4].view.Get(),
                group.composition==contact_occlusion::Composition::Common140?normalsView_.Get():nullptr};
            context->PSSetShaderResources(0,2,inputs);
            auto target=abTarget_.Get();context->OMSetRenderTargets(1,&target,mainDepth_.Get());context->Draw(4,0);
            unbindInputs(context);
        }
        // Node64 owns the outer target pop. This exit changes only the fields
        // set by the source occlusion scope; it does not restore the context.
        state_.exit(device,context,logical,entryDepthTest);stock_.bindExit(context);
        context->RSSetViewports(1,&returnViewport);
    }

private:
    static unsigned depthSampler(unsigned pass,const contact_occlusion::CurrentFrame& current) {
        constexpr unsigned GeneratedPoint=0,MainDepthLinear=3,GeneratedLinear=4,MainDepthPoint=6;
        const bool generated=(pass!=0&&current.usesViewDepth)||(pass==2&&current.halfResolution);
        const bool linear=pass==2?current.usesViewDepth:pass>=3&&current.selection.linearBlur;
        return generated?(linear?GeneratedLinear:GeneratedPoint):(linear?MainDepthLinear:MainDepthPoint);
    }
    static OcclusionComposition composition(contact_occlusion::Composition value) {
        switch(value) {
            case contact_occlusion::Composition::Default3:return OcclusionComposition::Default3;
            case contact_occlusion::Composition::Common138:return OcclusionComposition::Common138;
            case contact_occlusion::Composition::Common140:return OcclusionComposition::Common140;
        }
        throw std::runtime_error("Unsupported contact occlusion composition");
    }
    void prepareSurfaces(ID3D11Device* device,const contact_occlusion::CurrentFrame& current,
        const contact_occlusion::Settings& settings) {
        const unsigned reducedWidth=current.halfResolution?width_>>1:width_,reducedHeight=current.halfResolution?height_>>1:height_;
        const unsigned blurHeight=current.halfResolution&&settings.blurAtHalfSize?height_>>1:height_;
        const std::array<std::array<unsigned,2>,5> extents{{{width_,height_},{width_>>1,height_>>1},
            {reducedWidth,reducedHeight},{width_,blurHeight},{width_,height_}}};
        for(const unsigned pass:current.passes) {
            if(pass>=surfaces_.size())throw std::runtime_error("Unsupported selected occlusion target");
            auto& surface=surfaces_[pass];const auto& extent=extents[pass];
            if(surface.texture&&surface.width==extent[0]&&surface.height==extent[1])continue;
            surface=createOcclusionSurface(device,extent[0],extent[1],resources_.candidates[pass]);
        }
    }
    ID3D11ShaderResourceView* texture(OcclusionTextureInput input,unsigned pass,
        const contact_occlusion::CurrentFrame& current) const {
        switch(input) {
            case OcclusionTextureInput::Depth:
                if(pass==0)return depth_.depth.Get();
                if(pass==2&&current.halfResolution)return surfaces_[1].view.Get();
                return current.usesViewDepth?surfaces_[0].view.Get():depth_.depth.Get();
            case OcclusionTextureInput::Stencil:return depth_.stencil.Get();
            case OcclusionTextureInput::Normals:return normalsView_.Get();
            case OcclusionTextureInput::Noise:return noise_.view.Get();
            case OcclusionTextureInput::Occlusion:
                if(pass==3)return surfaces_[2].view.Get();
                if(pass==4)return surfaces_[3].view.Get();
                break;
        }
        throw std::runtime_error("Unsupported contact occlusion texture role");
    }
    static void unbindInputs(ID3D11DeviceContext* context) {
        std::array<ID3D11ShaderResourceView*,32> empty{};context->PSSetShaderResources(0,UINT(empty.size()),empty.data());
    }
    OcclusionResourceBank resources_;
    OcclusionGpuPrograms programs_;
    OcclusionStock stock_;
    OcclusionDrawState state_;
    OcclusionNoise noise_;
    NativeDepthMirror depth_;
    ComPtr<ID3D11Texture2D> normals_,ab_;
    ComPtr<ID3D11DepthStencilView> mainDepth_;
    ComPtr<ID3D11ShaderResourceView> normalsView_;
    ComPtr<ID3D11RenderTargetView> abTarget_;
    std::array<OcclusionSurface,5> surfaces_;
    std::vector<ComPtr<ID3D11SamplerState>> samplers_;
    unsigned width_{},height_{};
};
} // namespace San14::Render
