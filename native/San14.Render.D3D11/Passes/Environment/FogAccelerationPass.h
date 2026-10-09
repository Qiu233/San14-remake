#pragma once
// Include after model constant-buffer creation and the render-target scope.
#include "FogAccelerationCamera.h"
namespace San14::Render {

// Accepted-frame fog reduction owns its target, programs, current camera data,
// and inherited-blend cache for the complete render session.
class FogAccelerationPass {
  public:
    FogAccelerationPass()=default;
    FogAccelerationPass(const FogAccelerationPass&)=delete;
    FogAccelerationPass& operator=(const FogAccelerationPass&)=delete;

    void prepare(ID3D11Device* device,const fs::path& source) {
        resources_=prepareResources(device,source);
        camera_=prepareCamera(device,source,resources_);
    }
    void updateCamera(ID3D11DeviceContext* context,const source_camera::Prefix& camera) {
        updateCameraResources(context,camera_,resources_,camera);
    }
    void execute(ID3D11Device* device,ID3D11DeviceContext* context) {
        executeResources(device,context,resources_);
    }
    ID3D11ShaderResourceView* samplingView() const { return resources_.view.Get(); }

  private:
    struct PointDraw {
        ComPtr<ID3D11VertexShader> vertex;
        ComPtr<ID3D11PixelShader> pixel;
        std::array<ComPtr<ID3D11Buffer>,D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT> vertexConstants,pixelConstants;
        UINT count{};
        D3D11_PRIMITIVE_TOPOLOGY topology{};
    };
    struct BlendStates {
        D3D11_BLEND_DESC entry{},exit{};
        ComPtr<ID3D11BlendState> initial,restored;
    };
    struct Resources {
        ComPtr<ID3D11Texture2D> texture;
        ComPtr<ID3D11ShaderResourceView> view;
        ComPtr<ID3D11RenderTargetView> target;
        std::array<PointDraw,2> passes;
        ComPtr<ID3D11BlendState> minimum;
        D3D11_VIEWPORT viewport{};
        D3D11_RECT scissor{};
        // Only actual source blend-entry changes need another pair of states.
        // This is a resource lifetime cache, not a request/frame identity check.
        std::vector<BlendStates> blendStates;
    };

    static Resources prepareResources(ID3D11Device* device,const fs::path& source) {
        UINT resource,sampled,target,width,height;
        std::ifstream descriptor(source/"target.txt");descriptor>>resource>>sampled>>target>>width>>height;
        if(!descriptor||width!=1||height!=1)throw std::runtime_error("Original fog acceleration 1x1 target absent");
        Resources out;D3D11_TEXTURE2D_DESC td{};
        td.Width=width;td.Height=height;td.MipLevels=td.ArraySize=1;td.Format=DXGI_FORMAT(resource);
        td.SampleDesc.Count=1;td.Usage=D3D11_USAGE_DEFAULT;td.BindFlags=D3D11_BIND_RENDER_TARGET|D3D11_BIND_SHADER_RESOURCE;
        check(device->CreateTexture2D(&td,nullptr,&out.texture),"PrepareMain original fog acceleration target");
        D3D11_SHADER_RESOURCE_VIEW_DESC sd{};sd.Format=DXGI_FORMAT(sampled);sd.ViewDimension=D3D11_SRV_DIMENSION_TEXTURE2D;sd.Texture2D.MipLevels=1;
        check(device->CreateShaderResourceView(out.texture.Get(),&sd,&out.view),"PrepareMain original fog acceleration SRV");
        D3D11_RENDER_TARGET_VIEW_DESC rd{};rd.Format=DXGI_FORMAT(target);rd.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2D;
        check(device->CreateRenderTargetView(out.texture.Get(),&rd,&out.target),"PrepareMain original fog acceleration RTV");
        out.viewport={0,0,float(width),float(height),0,1};out.scissor={0,0,LONG(width),LONG(height)};
        std::ifstream manifest(source/"passes.txt");std::string relative;
        if(!manifest)throw std::runtime_error("Original fog acceleration pass list absent");
        UINT index=0;
        while(manifest>>relative) {
            if(index>=out.passes.size()||relative!=(index==0?"pass0":"pass1"))throw std::runtime_error("Original fog acceleration pass order differs");
            auto& pass=out.passes[index];const auto directory=source/relative;
            auto vs=load(directory/"vs.dxbc"),ps=load(directory/"ps.dxbc");
            check(device->CreateVertexShader(vs.data(),vs.size(),nullptr,&pass.vertex),"PrepareMain original fog acceleration VS");
            check(device->CreatePixelShader(ps.data(),ps.size(),nullptr,&pass.pixel),"PrepareMain original fog acceleration PS");
            std::ifstream cbs(directory/"cbuffers.txt");std::string stage,file;UINT slot;
            if(!cbs)throw std::runtime_error("Original fog acceleration CB list absent");
            while(cbs>>stage>>slot>>file){auto cb=buffer(device,load(directory/file),D3D11_BIND_CONSTANT_BUFFER);
                if(stage=="VS"||stage=="vs")pass.vertexConstants.at(slot)=cb;
                else if(stage=="PS"||stage=="ps")pass.pixelConstants.at(slot)=cb;
                else throw std::runtime_error("Original fog acceleration CB stage differs");}
            UINT topology;std::ifstream geometry(directory/"geometry.txt");geometry>>pass.count>>topology;
            if(!geometry||pass.count!=(index==0?1u:500u)||topology!=1)throw std::runtime_error("Original fog acceleration POINT draw differs");
            pass.topology=D3D11_PRIMITIVE_TOPOLOGY(topology);++index;
        }
        if(index!=2)throw std::runtime_error("Original fog acceleration requires two passes");
        auto blendBytes=load(source/"pass1"/"blend-desc.bin");D3D11_BLEND_DESC minimum{};
        if(blendBytes.size()!=sizeof(minimum))throw std::runtime_error("Original fog acceleration MIN descriptor size");
        std::memcpy(&minimum,blendBytes.data(),sizeof(minimum));
        check(device->CreateBlendState(&minimum,&out.minimum),"PrepareMain original fog acceleration ONE/ONE/MIN");
        return out;
    }

    // Compare actual source state fields only, avoiding padding bytes, fingerprints
    // or file reads. A repeated entry reuses its already created device objects.
    static bool sameBlend(const D3D11_BLEND_DESC& a,const D3D11_BLEND_DESC& b) {
        if(a.AlphaToCoverageEnable!=b.AlphaToCoverageEnable||a.IndependentBlendEnable!=b.IndependentBlendEnable)return false;
        for(UINT slot=0;slot!=8;++slot){const auto& x=a.RenderTarget[slot];const auto& y=b.RenderTarget[slot];
            if(x.BlendEnable!=y.BlendEnable||x.SrcBlend!=y.SrcBlend||x.DestBlend!=y.DestBlend||x.BlendOp!=y.BlendOp||
                x.SrcBlendAlpha!=y.SrcBlendAlpha||x.DestBlendAlpha!=y.DestBlendAlpha||x.BlendOpAlpha!=y.BlendOpAlpha||
                x.RenderTargetWriteMask!=y.RenderTargetWriteMask)return false;}
        return true;
    }
    static BlendStates& prepareBlend(ID3D11Device* device,
        Resources& owner,const D3D11_BLEND_DESC& sourceEntry) {
        for(auto& states:owner.blendStates)if(sameBlend(states.entry,sourceEntry))return states;
        BlendStates states;states.entry=sourceEntry;states.exit=sourceEntry;
        auto first=sourceEntry;first.RenderTarget[0].BlendEnable=FALSE;first.RenderTarget[0].RenderTargetWriteMask=3;
        check(device->CreateBlendState(&first,&states.initial),"PrepareMain fog acceleration inherited pass0 blend");
        // Restore RGB-write enable as a bool while retaining alpha-write enable.
        const BYTE mask=sourceEntry.RenderTarget[0].RenderTargetWriteMask;
        states.exit.RenderTarget[0].RenderTargetWriteMask=BYTE((mask&8)|((mask&7)?7:0));
        check(device->CreateBlendState(&states.exit,&states.restored),"PrepareMain fog acceleration original blend restore");
        owner.blendStates.push_back(std::move(states));return owner.blendStates.back();
    }

    // Call at every accepted Main's actual node1 producer gate. Source logical blend
    // fields are explicit; the function does not query depth/stencil/raster state.
    // CBs use the supplied fixed-view source packet. No old GPU result is reused.
    static void executeResources(ID3D11Device* device,ID3D11DeviceContext* context,
        D3D11_BLEND_DESC& logicalBlend,Resources& owner) {
        auto& blend=prepareBlend(device,owner,logicalBlend);
        const auto attachments=NativeMainTargetScope::retain(context);
        ComPtr<ID3D11BlendState> hardwareEntry;FLOAT factor[4];UINT sampleMask;
        context->OMGetBlendState(&hardwareEntry,factor,&sampleMask); // Factors/sampleMask only; no GetDesc.
        context->OMSetRenderTargets(0,nullptr,nullptr);
        std::array<ID3D11ShaderResourceView*,32> emptyResources{};
        context->VSSetShaderResources(0,UINT(emptyResources.size()),emptyResources.data());
        context->PSSetShaderResources(0,UINT(emptyResources.size()),emptyResources.data());
        context->RSSetViewports(1,&owner.viewport);context->RSSetScissorRects(1,&owner.scissor);
        context->IASetInputLayout(nullptr);ID3D11Buffer* noVertices=nullptr;UINT zero=0;
        context->IASetVertexBuffers(0,1,&noVertices,&zero,&zero);context->IASetIndexBuffer(nullptr,DXGI_FORMAT_UNKNOWN,0);
        context->HSSetShader(nullptr,nullptr,0);context->DSSetShader(nullptr,nullptr,0);context->GSSetShader(nullptr,nullptr,0);
        for(UINT index=0;index!=owner.passes.size();++index) {
            const auto& pass=owner.passes[index];context->VSSetShader(pass.vertex.Get(),nullptr,0);context->PSSetShader(pass.pixel.Get(),nullptr,0);
            std::array<ID3D11Buffer*,D3D11_COMMONSHADER_CONSTANT_BUFFER_API_SLOT_COUNT> vsCb{},psCb{};
            for(size_t i=0;i<vsCb.size();++i){vsCb[i]=pass.vertexConstants[i].Get();psCb[i]=pass.pixelConstants[i].Get();}
            context->VSSetConstantBuffers(0,UINT(vsCb.size()),vsCb.data());context->PSSetConstantBuffers(0,UINT(psCb.size()),psCb.data());
            context->IASetPrimitiveTopology(pass.topology);context->OMSetBlendState(index?owner.minimum.Get():blend.initial.Get(),factor,sampleMask);
            auto target=owner.target.Get();context->OMSetRenderTargets(1,&target,nullptr);context->Draw(pass.count,0);
        }
        // Original shader.v90 clears stream output; v70 clears the bound stages.
        // No prior shader or complete pipeline snapshot is restored.
        context->SOSetTargets(0,nullptr,nullptr);
        context->VSSetShader(nullptr,nullptr,0);context->PSSetShader(nullptr,nullptr,0);
        ID3D11Buffer* noConstant=nullptr;context->VSSetConstantBuffers(0,1,&noConstant);context->PSSetConstantBuffers(0,1,&noConstant);
        context->VSSetShaderResources(0,UINT(emptyResources.size()),emptyResources.data());
        context->PSSetShaderResources(0,UINT(emptyResources.size()),emptyResources.data());
        context->OMSetBlendState(blend.restored.Get(),factor,sampleMask);logicalBlend=blend.exit;
        context->OMSetRenderTargets(0,nullptr,nullptr);attachments.restore(context);
    }

    // The accepted-frame caller inherits the active hardware blend descriptor.
    static void executeResources(ID3D11Device* device,ID3D11DeviceContext* context,Resources& owner) {
        ComPtr<ID3D11BlendState> entry;FLOAT factors[4];UINT sampleMask;
        context->OMGetBlendState(&entry,factors,&sampleMask);D3D11_BLEND_DESC description{};
        for(auto& rt:description.RenderTarget)rt={FALSE,D3D11_BLEND_ONE,D3D11_BLEND_ZERO,D3D11_BLEND_OP_ADD,
            D3D11_BLEND_ONE,D3D11_BLEND_ZERO,D3D11_BLEND_OP_ADD,15};
        if(entry)entry->GetDesc(&description);
        executeResources(device,context,description,owner);
    }
    static source_aux_camera::FogAccelerationConstants prepareCamera(
        ID3D11Device* device,const fs::path& source,Resources& fog) {
        source_aux_camera::FogAccelerationConstants constants;
        const auto vertex=load(source/"pass1"/"vs-b0.bin"),pixel=load(source/"pass1"/"ps-b0.bin");
        if(vertex.size()!=constants.vertex.size()||pixel.size()!=constants.pixel.size())
            throw std::runtime_error("Source FogAccel complete camera CB extent differs");
        std::memcpy(constants.vertex.data(),vertex.data(),vertex.size());
        std::memcpy(constants.pixel.data(),pixel.data(),pixel.size());
        fog.passes[1].vertexConstants[0]=modelContinuousConstantBuffer(device,vertex.data(),UINT(vertex.size()));
        fog.passes[1].pixelConstants[0]=modelContinuousConstantBuffer(device,pixel.data(),UINT(pixel.size()));
        return constants;
    }
    static void updateCameraResources(ID3D11DeviceContext* context,
        source_aux_camera::FogAccelerationConstants& constants,Resources& fog,
        const source_camera::Prefix& camera) {
        source_aux_camera::writeCurrentFogAcceleration(constants,camera.data());
        context->UpdateSubresource(fog.passes[1].vertexConstants[0].Get(),0,nullptr,constants.vertex.data(),0,0);
        context->UpdateSubresource(fog.passes[1].pixelConstants[0].Get(),0,nullptr,constants.pixel.data(),0,0);
    }
    Resources resources_;
    source_aux_camera::FogAccelerationConstants camera_;
};
} // namespace San14::Render
