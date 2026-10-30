#pragma once
#include "ReflectionCaptureDraw.h"
#include "ReflectionFieldPrograms.h"
#include "../../Device/RenderTargetScope.h"
#include <set>

namespace San14::Render::Reflection {
struct CaptureSubmission {std::uint32_t fieldObjectId{},sourceOrdinal{};};
// The capture View owns its render state. Preserve the Main hardware depth
// binding as well as attachments, so its separate NativeSourceDepthState
// remains truthful after nested capture/filter submissions.
class CaptureCallScope {
public:
    explicit CaptureCallScope(ID3D11DeviceContext* context):context_(context),targets_(NativeMainTargetScope::retain(context)) {
        context_->OMGetDepthStencilState(&depth_,&reference_);
    }
    CaptureCallScope(const CaptureCallScope&)=delete;
    CaptureCallScope& operator=(const CaptureCallScope&)=delete;
    ~CaptureCallScope(){FieldPrograms::reset(context_);context_->OMSetDepthStencilState(depth_.Get(),reference_);targets_.restore(context_);}
private:
    ID3D11DeviceContext* context_;NativeMainTargetScope targets_;ComPtr<ID3D11DepthStencilState> depth_;UINT reference_{};
};
struct FieldShadowTarget {
    ID3D11DepthStencilView* depth{};
    ID3D11ShaderResourceView* readableDepth{};
    UINT extent{};
};
// Session/world resource owner for the actual D3D11 node66. Construct after
// current world resources, retire before them. No Godot device is involved.
class ReflectionRenderer {
    struct FieldEntry {FieldStorage storage;FieldFilterProgress filter;UINT lastCapture{};bool captured{};
        FieldEntry(ID3D11Device* device,FieldStorageShape shape):storage(device,shape){}};
public:
    ReflectionRenderer(ID3D11Device* device,ID3D11Texture2D* actualRR,DXGI_FORMAT actualRRFormat,fs::path sourceDirectory)
        :device_(device),source_(std::move(sourceDirectory)),draws_(device) {
        if(!actualRR)throw std::runtime_error("Reflection actual RR owner absent");
        D3D11_TEXTURE2D_DESC desc{};actualRR->GetDesc(&desc);
        if(desc.ArraySize!=3||desc.MipLevels!=1||desc.SampleDesc.Count!=1)
            throw std::runtime_error("Reflection current normal RR shape differs from source");
        for(UINT layer=0;layer<3;++layer) {
            D3D11_RENDER_TARGET_VIEW_DESC target{};target.Format=actualRRFormat;target.ViewDimension=D3D11_RTV_DIMENSION_TEXTURE2DARRAY;
            target.Texture2DArray.FirstArraySlice=layer;target.Texture2DArray.ArraySize=1;
            check(device->CreateRenderTargetView(actualRR,&target,&fallback_[layer]),"Reflection original fallback raw1/2/3 target");
        }
    }
    // EC8440 fallback depends on actual published root existence, independently
    // of captureWork emptiness. Main Common14 bit7 positively excludes active
    // raw4/5 for the ordinary source; it does not disable capture/filter work.
    void submitCurrentFallback(ID3D11DeviceContext* context,const SceneFrame& frame)const {
        frame.requireCurrent();
        if(!frame.hasPublishedRoots())return;
        if(frame.commonFlags14&0x80u)throw std::runtime_error("Reflection active raw4/5 require actual target owners; ordinary RR has three layers");
        CaptureCallScope restore(context);const float clear[4]{};FieldPrograms::reset(context);
        for(const auto& target:fallback_)context->ClearRenderTargetView(target.Get(),clear);
    }
    template<class CurrentTree2Wind=std::nullptr_t>
    std::vector<CaptureSubmission> submitCapture(ID3D11DeviceContext* context,const SceneFrame& frame,const CapturePacketCatalog& catalog,
        const CurrentTree2Wind* actualTree2Wind=nullptr) {
        frame.requireCurrent();synchronizeFields(frame);std::vector<CaptureSubmission> submitted;
        if(frame.captureWork.empty())return submitted;
        if(!frame.actualClipPlanes)throw std::runtime_error("Reflection current native GraphicsClipState planes were not published");
        ensurePrograms();if(!capture_)capture_=std::make_unique<CaptureTargets>(device_.Get());
        CaptureCallScope restore(context);
        {
            for(const auto& work:frame.captureWork) {
                auto found=fields_.find(work.fieldObjectId);
                if(found==fields_.end())throw std::runtime_error("Reflection capture work refers to retired Field");
                auto& field=found->second;
                if(work.sourceOrdinal>=field.storage.probes)throw std::out_of_range("Reflection current cell ordinal");
                for(const auto& face:work.faces) {
                    capture_->begin(context,face.index);
                    if(face.visible)for(UINT primitive:face.primitiveIndices) {
                        if(primitive>=frame.primitives.size())throw std::out_of_range("Reflection actual ordered primitive index");
                        draws_.draw(context,frame.primitives[primitive],catalog,face,work,field.storage.shape.range,*frame.actualClipPlanes,actualTree2Wind);
                    }
                }
                FieldPrograms::reset(context);programs_.convert(context,field.storage,work.sourceOrdinal,*capture_);
                field.lastCapture=work.sourceOrdinal;field.captured=true;
                submitted.push_back({work.fieldObjectId,work.sourceOrdinal});
            }
            return submitted;
        }
    }
    FieldShadowTarget borrowFieldShadowTarget(UINT actualFieldObjectId) {
        auto& field=fields_.at(actualFieldObjectId).storage;
        return {field.shadowDepth.Get(),field.shadowView.Get(),field.shape.shadow};
    }
    // Called only after the actual field-owned shadow producer rendered the
    // same cell. Its allocation cannot self-certify shadow contents.
    void advanceFieldFiltering(ID3D11DeviceContext* context,UINT actualFieldObjectId,const FieldFilterLight& actualLight) {
        auto& entry=fields_.at(actualFieldObjectId);
        if(!entry.captured||actualLight.fieldObjectId!=actualFieldObjectId||actualLight.sourceOrdinal!=entry.lastCapture||
            actualLight.depth!=entry.storage.shadowView.Get())throw std::runtime_error("Reflection field shadow/cell owner mismatch");
        ensurePrograms();CaptureCallScope restore(context);
        {
            programs_.advanceDirect(context,entry.storage,entry.filter,actualLight);
            programs_.advanceFiltering(context,entry.storage,entry.filter);
        }
    }
    void applyCurrentTuning(float indirect,float specular,float biasX,float biasY) {
        programs_.applyCurrentTuning(indirect,specular,biasX,biasY);
    }
    void setCurrentSchedulingMode(UINT fieldObjectId,UINT mode) {
        auto& progress=fields_.at(fieldObjectId).filter;
        if(mode==progress.mode)return;progress.mode=mode;
        if(mode==0){progress.phase=0;progress.layer=0;progress.iteration=0;progress.parity=false;}
    }
    // A resource-layer override/world replacement retires this entire owner;
    // actual actor removal retires only its Field resources during publication.
    void synchronizeFields(const SceneFrame& frame) {
        frame.requireCurrent();
        if(!resourceGeneration_||*resourceGeneration_!=frame.resourceGeneration) {
            fields_.clear();draws_.clear();programs_=FieldPrograms{};capture_.reset();programsPrepared_=false;
            resourceGeneration_=frame.resourceGeneration;
        }
        std::set<UINT> current;
        for(const auto& state:frame.fields) {
            const auto& source=state.definition;const UINT id=source.objectId;
            if(!current.insert(id).second)throw std::runtime_error("Reflection duplicate current Field identity");
            FieldStorageShape shape{source.dimensions[0],source.dimensions[1],source.dimensions[2],source.tileSize,source.gBufSize,source.probeShadowMapSize,
                source.cellSize,source.viewRange,state.managerPosition};
            auto found=fields_.find(id);
            if(found==fields_.end())fields_.try_emplace(id,device_.Get(),shape);
            else {
                const auto& old=found->second.storage.shape;
                if(old.x!=shape.x||old.y!=shape.y||old.z!=shape.z||old.tile!=shape.tile||old.atlas!=shape.atlas||old.shadow!=shape.shadow)
                    throw std::runtime_error("Reflection Field resource changed without resource-layer retirement");
                found->second.storage.shape.pitch=shape.pitch;found->second.storage.shape.range=shape.range;found->second.storage.shape.origin=shape.origin;
            }
        }
        for(auto field=fields_.begin();field!=fields_.end();)if(!current.count(field->first))field=fields_.erase(field);else ++field;
    }
private:
    void ensurePrograms(){if(!programsPrepared_){programs_.prepare(device_.Get(),source_/"programs");programsPrepared_=true;}}
    ComPtr<ID3D11Device> device_;fs::path source_;CaptureDrawCache draws_;FieldPrograms programs_;bool programsPrepared_{};
    std::optional<std::uint64_t> resourceGeneration_;
    std::unique_ptr<CaptureTargets> capture_;std::map<UINT,FieldEntry> fields_;std::array<ComPtr<ID3D11RenderTargetView>,3> fallback_;
};
} // namespace San14::Render::Reflection
