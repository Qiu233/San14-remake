// Resource import and constant-buffer output for the normal post timeline.
// Include after NativeNormalPostPass helpers.
#pragma once
#include "../../Runtime/PreparedFileInput.h"
#include <map>
#include "PostProcessHistory.h"
#include "DynamicDofFrame.h"
#include <optional>

struct PostConstantBinding {
    std::string stage,file;
    UINT slot{},parameter{};
};
enum class PostPassRole { Other, ApertureShape, CocInitialize, CocAccumulate, CocResolve, ScatterRear, ScatterFore };
struct PostPassTemplate {
    std::string name;
    PostPassRole role{};
    std::vector<PostConstantBinding> bindings;
};
struct PostProcessTemplates {
    float heatCoefficient{};
    UINT gatherMode{},depthOfFieldMode{};
    San14::Render::DynamicDof::Inputs dofProfile;
    std::optional<San14::Render::DynamicDof::Inputs> currentDof;
    std::optional<int64_t> currentOpticsCamera;
    std::vector<PostPassTemplate> passes;
    std::map<std::string,std::vector<char>> constants;
};
struct NormalPostTimeline {
    San14::Render::PostProcessHistory history;
    PostProcessTemplates templates;
    San14::Render::DynamicDof::State dofState;
    std::optional<int64_t> returnedOpticsCamera;
};
struct PostFramePass {
    std::string name;
    PostPassRole role{};
    size_t templateIndex{};
};
struct NormalPostFrame {
    San14::Render::PostHistoryFrame history;
    std::optional<San14::Render::DynamicDof::Frame> dof;
    std::optional<int64_t> opticsCamera;
    std::vector<PostFramePass> passes;
    std::map<std::string,std::vector<char>> constants;
};
static PostPassRole postPassRole(const std::string& name) {
    if(name=="053-dof")return PostPassRole::ApertureShape;
    if(name=="066-dof")return PostPassRole::CocInitialize;
    if(name=="067-dof")return PostPassRole::CocAccumulate;
    if(name=="070-dof")return PostPassRole::CocResolve;
    if(name=="062-dof")return PostPassRole::ScatterRear;
    if(name=="063-dof")return PostPassRole::ScatterFore;
    return PostPassRole::Other;
}
struct NativeNormalPostStagingMap {
    UINT side{};
    bool returnedNull{};
    std::array<char,32> bytes{}; // Meaningful only after a successful current Map.
};

static NormalPostTimeline prepareNormalPostTimeline(const fs::path& source) {
    NormalPostTimeline owner;auto& templates=owner.templates;
    San14::Render::PostHistorySeed seed;UINT width,height,history,change,applied;
    NativePreparedInputFile fixed(source/"continuous-fixed.txt");
    fixed>>templates.heatCoefficient>>width>>height>>templates.gatherMode>>templates.depthOfFieldMode
        >>seed.warmupApplications>>history>>seed.stagingReadSide>>change>>applied>>seed.previousApplyTime;
    if(!fixed)throw std::runtime_error("Normal post fixed inputs absent");
    seed.cocWriteSide=history!=0;seed.apertureChanged=change!=0;seed.previousGatherApplied=applied!=0;
    owner.history=San14::Render::PostProcessHistory(seed);
    NativePreparedInputFile profile(source/"current-dof-profile.bin",std::ios::binary);
    auto word=[&](){uint32_t v{};profile.read(reinterpret_cast<char*>(&v),4);return v;};
    auto& fields=templates.dofProfile;fields.contextHeight=height;fields.contextWord=word();fields.descriptorMode=word();fields.quality=int(word());
    for(auto extent:{&fields.rear,&fields.scene,&fields.prefiltered,&fields.preComposite})
        {extent->present=word()!=0;extent->width=uint16_t(word());extent->height=uint16_t(word());}
    if(!profile)throw std::runtime_error("Current DOF source extent profile absent");
    owner.dofState.shapeHeight=owner.dofState.initializeHeight=height;

    NativePreparedInputFile order(source/"ordered-required-color-passes.txt");std::string name;
    if(!order)throw std::runtime_error("Normal post pass directory absent");
    while(order>>name) {
        PostPassTemplate pass;pass.name=name;pass.role=postPassRole(name);
        NativePreparedInputFile cbs(source/name/"cbuffers.txt");
        if(!cbs)throw std::runtime_error("Normal post CB layout absent");
        PostConstantBinding binding;
        while(cbs>>binding.stage>>binding.slot>>binding.file) {
            if(binding.file.size()<7||binding.file.substr(0,2)!="cb")throw std::runtime_error("Unsupported post CB filename");
            binding.parameter=UINT(std::stoul(binding.file.substr(2,binding.file.size()-6)));
            pass.bindings.push_back(binding);
            const auto key=name+"/"+binding.file;
            if(templates.constants.find(key)==templates.constants.end())templates.constants[key]=load(source/name/binding.file);
        }
        templates.passes.push_back(std::move(pass));
    }
    // Physical history storage and its sides belong to the renderer. The
    // timeline consumes the prepared pass library, not a duplicate pair census.
    if(templates.passes.empty())throw std::runtime_error("Normal post pass library is empty");
    return owner;
}

// Clock advancement is supplied by the accepted frame. The template directory
// chooses resource programs; the timeline decides initialization and feedback.
static NormalPostFrame beginNormalPostFrame(NormalPostTimeline& owner,
    float applyTime,float actualDelta,bool resetHistory=false,bool resetView=false) {
    NormalPostFrame frame;
    frame.history=owner.history.prepare(applyTime,actualDelta,owner.templates.depthOfFieldMode!=0,resetHistory,resetView);
    const float heat=owner.templates.heatCoefficient*applyTime;
    if(owner.templates.currentDof) {
        frame.opticsCamera=owner.templates.currentOpticsCamera;
        auto fields=*owner.templates.currentDof;
        fields.changeFStop=fields.changeFStop&&frame.opticsCamera!=owner.returnedOpticsCamera;
        fields.firstExecution=frame.history.initializeCoc;fields.heat[2]=heat;
        frame.dof=San14::Render::DynamicDof::prepare(fields,owner.dofState);
    }
    uint32_t reachedDofMask=0;

    for(size_t index=0;index<owner.templates.passes.size();++index) {
        const auto& pass=owner.templates.passes[index];
        if(frame.dof&&pass.name.ends_with("-dof")) {
            const unsigned directoryIndex=unsigned(std::stoul(pass.name.substr(0,3)));
            const auto& mapping=San14::Render::DynamicDof::directoryPasses;
            auto found=std::find(mapping.begin(),mapping.end(),directoryIndex);
            if(found==mapping.end())throw std::runtime_error("DOF union has no original cache index");
            const uint32_t bit=1u<<unsigned(found-mapping.begin());
            if(!(frame.dof->passMask&bit))continue;
            reachedDofMask|=bit;
        } else if(pass.role==PostPassRole::ApertureShape&&!frame.history.rebuildAperture||
            pass.role==PostPassRole::CocInitialize&&!frame.history.initializeCoc||
            pass.role==PostPassRole::CocAccumulate&&frame.history.initializeCoc)continue;
        frame.passes.push_back({pass.name,pass.role,index});
        for(const auto& binding:pass.bindings) {
            const auto key=pass.name+"/"+binding.file;if(frame.constants.find(key)!=frame.constants.end())continue;
            auto bytes=owner.templates.constants.at(key);
            if(binding.parameter==18){if(bytes.size()!=144)throw std::runtime_error("Complete post CB18 differs");
                if(frame.dof)std::memcpy(bytes.data(),frame.dof->constants.data(),144);else std::memcpy(bytes.data()+120,&heat,4);}
            if(binding.parameter==24){if(bytes.size()!=64)throw std::runtime_error("Complete post CB24 differs");std::memcpy(bytes.data()+20,&frame.history.deltaTime,4);}
            frame.constants.emplace(key,std::move(bytes));
        }
    }
    if(frame.dof&&reachedDofMask!=frame.dof->passMask)throw std::runtime_error("Current DOF selected program is outside loaded original ordinary resource union");
    owner.history.begin(frame.history);
    return frame;
}
static NormalPostFrame beginNormalPostFrameFromClock(NormalPostTimeline& owner,float applyTime,
    bool resetHistory=false,bool resetView=false) {
    return beginNormalPostFrame(owner,applyTime,applyTime-owner.history.state().previousApplyTime,resetHistory,resetView);
}

// Clone only current pass arguments/COM references from the one-time GPU union.
// Both physical CoC histories and module pairs already exist in the GPU owner.
#ifndef SOURCE_POST_FRAME_CPU_ONLY
static std::vector<NativeNormalPostPass> selectNormalPostFramePasses(
    const NormalPostFrame& frame,const std::vector<NativeNormalPostPass>& gpuUnion) {
    std::vector<NativeNormalPostPass> selected;selected.reserve(frame.passes.size());
    const std::string currentCoc=frame.history.cocWriteSide?"Texture66":"Texture65";
    for(const auto& entry:frame.passes) {
        const auto& name=entry.name;
        auto found=std::find_if(gpuUnion.begin(),gpuUnion.end(),[&](const auto& pass){return pass.name==name;});
        if(found==gpuUnion.end())throw std::runtime_error("Continuous normal post static GPU template absent");
        auto pass=*found;
        if(entry.role==PostPassRole::CocInitialize||entry.role==PostPassRole::CocAccumulate)for(auto& target:pass.targets)if(target.slot==0)target.name=currentCoc;
        if(entry.role==PostPassRole::CocAccumulate)for(auto& input:pass.srvs)if(input.stage=="PS"&&input.slot==1)input.name="Texture60"; // Last source setter.
        if(entry.role==PostPassRole::CocResolve)for(auto& input:pass.srvs)if(input.stage=="PS"&&input.slot==1)input.name=currentCoc;
        if(entry.role==PostPassRole::ScatterRear||entry.role==PostPassRole::ScatterFore) {
            pass.stagingSide=frame.history.stagingReadSide;
            // Feedback publishes these flags before the actual scatter draws.
            pass.clear=false;pass.clearMask=0;pass.clearPacked=0;
        }
        selected.push_back(std::move(pass));
    }
    return selected;
}

// Runs just before scatter execution, before this application's counters are
// cleared/written. Mapping remains in the GPU owner; binary decoding stays here.
static void applyNormalPostScatterFeedback(NormalPostTimeline& owner,NormalPostFrame& frame,
    const NativeNormalPostStagingMap* actualMap,std::vector<NativeNormalPostPass>& selected) {
    San14::Render::ScatterFeedback feedback;
    if(!owner.history.applicationInProgress()||frame.history.feedbackApplied)
        throw std::runtime_error("Normal post scatter feedback lifecycle differs");
    if(frame.history.readScatterFeedback) {
        if(!actualMap||actualMap->side!=frame.history.stagingReadSide)
            throw std::runtime_error("Current post staging Map required");
        if(actualMap->returnedNull)feedback={true,true};
        else {UINT rear,fore;std::memcpy(&rear,actualMap->bytes.data()+4,4);std::memcpy(&fore,actualMap->bytes.data()+20,4);
            feedback={rear!=0,fore!=0};}
    }
    owner.history.acceptScatterFeedback(frame.history,feedback);
    for(size_t index=0;index<frame.passes.size();++index) {
        const auto role=frame.passes[index].role;
        if(role!=PostPassRole::ScatterRear&&role!=PostPassRole::ScatterFore)continue;
        auto& pass=selected.at(index);
        pass.clear=role==PostPassRole::ScatterRear?frame.history.clearRearScatter:frame.history.clearForeScatter;
        pass.clearMask=pass.clear?15:0;pass.clearPacked=0;
    }
}

// Upload the full current bytes immediately before this request executes.
// File/CB20 variants stay separate; no parameter-index-only buffer sharing.
static void uploadNormalPostFrameConstants(ID3D11DeviceContext* context,
    const PostProcessTemplates& templates,const NormalPostFrame& frame,
    const std::vector<NativeNormalPostPass>& selected) {
    std::vector<ID3D11Buffer*> uploaded;
    for(size_t index=0;index<selected.size();++index)for(const auto& layout:templates.passes.at(frame.passes.at(index).templateIndex).bindings) {
        const auto& pass=selected[index];
        auto binding=std::find_if(pass.constants.begin(),pass.constants.end(),[&](const auto& cb){return cb.stage==layout.stage&&cb.slot==layout.slot;});
        if(binding==pass.constants.end())throw std::runtime_error("Continuous normal post prepared CB layout differs");
        auto buffer=binding->buffer.Get();if(std::find(uploaded.begin(),uploaded.end(),buffer)!=uploaded.end())continue;
        const auto& bytes=frame.constants.at(pass.name+"/"+layout.file);
        context->UpdateSubresource(buffer,0,nullptr,bytes.data(),0,0);uploaded.push_back(buffer);
    }
}

#endif
// GPU aliases rotate first, then this accepted application completes its CPU
// timeline. The next application sees the retained feedback and clock.
static void completeNormalPostFrame(NormalPostTimeline& owner,const NormalPostFrame& frame,
    UINT returned=1,UINT callIndex=0,UINT callCount=1) {
    owner.history.complete(frame.history,owner.templates.gatherMode!=0,returned!=0,callIndex,callCount);
    if(frame.dof){owner.dofState=frame.dof->next;owner.returnedOpticsCamera=frame.opticsCamera;}
}
