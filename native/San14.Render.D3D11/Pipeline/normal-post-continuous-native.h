// Fixed normal Main post193 CPU preparation. Original GPU objects stay in the
// existing normal-post owner/passes. Include after NativeNormalPostPass helpers.
#pragma once
#include <map>

struct NativeNormalPostContinuousConstant {
    std::string stage,file;
    UINT slot{},parameter{};
};
struct NativeNormalPostContinuous {
    float heatCoefficient{},priorApplyClock{};
    UINT width{},height{},gatherC0{},enableDofA4{},count10{},stagingSide28{},beginCounter{};
    bool history16{},changeFStop{},applyBit23{},awaitingReturn{};
    std::vector<std::string> unionOrder;
    std::map<std::string,std::vector<NativeNormalPostContinuousConstant>> bindings;
    std::map<std::string,std::vector<char>> staticConstants;
    std::map<std::string,UINT> selectors;
};
struct NativeNormalPostContinuousPlan {
    float common254{},actualDelta{};
    UINT beginCounter{},countAtPrepare{},selectedStagingSide{},dofMask{};
    bool isFirstExec{},shapeChanged{},history16{},needStagingRead{},before57Applied{};
    bool rearClear{},foreClear{};
    std::vector<std::string> orderedPasses;
    std::map<std::string,std::vector<char>> constants;
    std::map<std::string,UINT> selectors;
};
struct NativeNormalPostStagingMap {
    UINT side{};
    bool returnedNull{};
    std::array<char,32> bytes{}; // Meaningful only after a successful current Map.
};

static NativeNormalPostContinuous prepareNativeNormalPostContinuous(const fs::path& source) {
    NativeNormalPostContinuous owner;UINT history,change,bit;
    std::ifstream fixed(source/"continuous-fixed.txt");
    fixed>>owner.heatCoefficient>>owner.width>>owner.height>>owner.gatherC0>>owner.enableDofA4>>owner.count10>>history>>owner.stagingSide28>>change>>bit>>owner.priorApplyClock;
    if(!fixed)throw std::runtime_error("Continuous normal post fixed source inputs absent");
    owner.history16=history!=0;owner.changeFStop=change!=0;owner.applyBit23=bit!=0;
    std::ifstream order(source/"ordered-required-color-passes.txt");std::string name;
    if(!order)throw std::runtime_error("Continuous normal post static union absent");
    while(order>>name) {
        owner.unionOrder.push_back(name);std::ifstream cbs(source/name/"cbuffers.txt");
        if(!cbs)throw std::runtime_error("Continuous normal post static CB layout absent");
        NativeNormalPostContinuousConstant binding;
        while(cbs>>binding.stage>>binding.slot>>binding.file) {
            if(binding.file.size()<7||binding.file.substr(0,2)!="cb")throw std::runtime_error("Unsupported continuous post CB filename");
            binding.parameter=UINT(std::stoul(binding.file.substr(2,binding.file.size()-6)));
            owner.bindings[name].push_back(binding);
            const auto key=name+"/"+binding.file;
            if(owner.staticConstants.find(key)==owner.staticConstants.end())owner.staticConstants[key]=load(source/name/binding.file);
        }
        owner.bindings.try_emplace(name);
    }
    std::ifstream pairs(source/"history-pairs.txt");std::string table;UINT index,side;
    if(!pairs)throw std::runtime_error("Continuous normal post source count2 selectors absent");
    while(pairs>>table>>index>>side)owner.selectors[table+":"+std::to_string(index)]=side;
    if(owner.unionOrder.size()!=25||owner.selectors.size()!=22)throw std::runtime_error("Continuous normal post fixed source layout differs");
    return owner;
}

// Caller supplies the shared current Common254 and actual D7B4D0 delta. No
// internal clock advancement, source import, GPU upload or frame-number branch.
static NativeNormalPostContinuousPlan beginNativeNormalPostContinuous(NativeNormalPostContinuous& owner,
    float common254,float actualDelta,UINT argument3=0,UINT argument4=0) {
    if(owner.awaitingReturn)throw std::runtime_error("Complete the current normal post source invocation before the next Prepare");
    if(!std::isfinite(common254)||!std::isfinite(actualDelta))throw std::runtime_error("Continuous post caller time must be finite");
    NativeNormalPostContinuousPlan plan;plan.common254=common254;plan.actualDelta=std::max(0.f,actualDelta);
    plan.isFirstExec=argument3!=0||argument4!=0||(!owner.applyBit23&&owner.enableDofA4!=0);
    plan.shapeChanged=owner.changeFStop; // Fixed 1080/non4K/nonmulti source scope.
    owner.count10=plan.isFirstExec?2:owner.count10;
    owner.history16=!owner.history16;owner.stagingSide28=owner.stagingSide28==0?1:0;
    plan.countAtPrepare=owner.count10;plan.history16=owner.history16;plan.selectedStagingSide=owner.stagingSide28;
    plan.needStagingRead=owner.count10==0;plan.selectors=owner.selectors;plan.beginCounter=++owner.beginCounter;
    // Fixed quality7, SpriteDOF, functional diaphragm and heat-enabled source
    // bits. 1157230 separately derives shape0 and first clear/retained AA bits.
    plan.dofMask=0x141B4u|(plan.isFirstExec?0x800u:0x1000u)|(plan.shapeChanged?1u:0u);
    const float heat=owner.heatCoefficient*common254;
    for(const auto& name:owner.unionOrder) {
        if(name=="053-dof"&&!plan.shapeChanged||name=="066-dof"&&!plan.isFirstExec||name=="067-dof"&&plan.isFirstExec)continue;
        plan.orderedPasses.push_back(name);
        for(const auto& binding:owner.bindings.at(name)) {
            const auto key=name+"/"+binding.file;if(plan.constants.find(key)!=plan.constants.end())continue;
            auto bytes=owner.staticConstants.at(key);
            if(binding.parameter==18){if(bytes.size()!=144)throw std::runtime_error("Continuous complete CB18 differs");std::memcpy(bytes.data()+120,&heat,4);}
            if(binding.parameter==24){if(bytes.size()!=64)throw std::runtime_error("Continuous complete CB24 differs");std::memcpy(bytes.data()+20,&plan.actualDelta,4);}
            plan.constants.emplace(key,std::move(bytes));
        }
    }
    owner.priorApplyClock=common254;owner.awaitingReturn=true;
    return plan;
}
// Current nonpaused fixed Main adapter. Keep the explicit-delta overload for
// other caller timing; heat Common254 is not a general substitute for DeltaTime.
static NativeNormalPostContinuousPlan beginNativeNormalPostContinuousFromCommon(
    NativeNormalPostContinuous& owner,float common254,UINT argument3=0,UINT argument4=0) {
    const float actualDelta=common254-owner.priorApplyClock;
    return beginNativeNormalPostContinuous(owner,common254,actualDelta,argument3,argument4);
}

// Clone only current pass arguments/COM references from the one-time GPU union.
// Both physical CoC histories and module pairs already exist in the GPU owner.
static std::vector<NativeNormalPostPass> selectNativeNormalPostContinuousPasses(
    const NativeNormalPostContinuousPlan& plan,const std::vector<NativeNormalPostPass>& gpuUnion) {
    std::vector<NativeNormalPostPass> selected;selected.reserve(plan.orderedPasses.size());
    const std::string currentCoc=plan.history16?"Texture66":"Texture65";
    for(const auto& name:plan.orderedPasses) {
        auto found=std::find_if(gpuUnion.begin(),gpuUnion.end(),[&](const auto& pass){return pass.name==name;});
        if(found==gpuUnion.end())throw std::runtime_error("Continuous normal post static GPU template absent");
        auto pass=*found;
        if(name=="066-dof"||name=="067-dof")for(auto& target:pass.targets)if(target.slot==0)target.name=currentCoc;
        if(name=="067-dof")for(auto& input:pass.srvs)if(input.stage=="PS"&&input.slot==1)input.name="Texture60"; // Last source setter.
        if(name=="070-dof")for(auto& input:pass.srvs)if(input.stage=="PS"&&input.slot==1)input.name=currentCoc;
        if(name=="062-dof"||name=="063-dof") {
            pass.stagingSide=plan.selectedStagingSide;
            // Before57 publishes these flags before the actual scatter draws.
            pass.clear=false;pass.clearMask=0;pass.clearPacked=0;
        }
        selected.push_back(std::move(pass));
    }
    return selected;
}

// Invoke at actual Before57, after Init selected side28, before clearing/writing
// this request's B5. The Map is external; this pure CPU helper never reads a GPU.
static void applyNativeNormalPostBefore57(NativeNormalPostContinuous& owner,NativeNormalPostContinuousPlan& plan,
    const NativeNormalPostStagingMap* actualMap,std::vector<NativeNormalPostPass>& selected) {
    if(!owner.awaitingReturn||plan.before57Applied)throw std::runtime_error("Continuous normal post Before57 lifecycle differs");
    if(plan.needStagingRead) {
        if(!actualMap||actualMap->side!=plan.selectedStagingSide)throw std::runtime_error("Current Init-selected controller32B staging Map required");
        if(actualMap->returnedNull){plan.rearClear=true;plan.foreClear=true;}
        else {UINT rear,fore;std::memcpy(&rear,actualMap->bytes.data()+4,4);std::memcpy(&fore,actualMap->bytes.data()+20,4);
            plan.rearClear=rear!=0;plan.foreClear=fore!=0;}
    }else {plan.rearClear=true;plan.foreClear=true;}
    for(auto& pass:selected)if(pass.name=="062-dof"||pass.name=="063-dof") {
        pass.clear=pass.name=="062-dof"?plan.rearClear:plan.foreClear;
        pass.clearMask=pass.clear?15:0;pass.clearPacked=0;
    }
    plan.before57Applied=true;
}

// Upload the full current bytes immediately before this request executes.
// File/CB20 variants stay separate; no parameter-index-only buffer sharing.
static void uploadNativeNormalPostContinuousConstants(ID3D11DeviceContext* context,
    const NativeNormalPostContinuous& owner,const NativeNormalPostContinuousPlan& plan,
    const std::vector<NativeNormalPostPass>& selected) {
    std::vector<ID3D11Buffer*> uploaded;
    for(const auto& pass:selected)for(const auto& layout:owner.bindings.at(pass.name)) {
        auto binding=std::find_if(pass.constants.begin(),pass.constants.end(),[&](const auto& cb){return cb.stage==layout.stage&&cb.slot==layout.slot;});
        if(binding==pass.constants.end())throw std::runtime_error("Continuous normal post prepared CB layout differs");
        auto buffer=binding->buffer.Get();if(std::find(uploaded.begin(),uploaded.end(),buffer)!=uploaded.end())continue;
        const auto& bytes=plan.constants.at(pass.name+"/"+layout.file);
        context->UpdateSubresource(buffer,0,nullptr,bytes.data(),0,0);uploaded.push_back(buffer);
    }
}

// Call only after actual directory execution, Finish, End/114AB40, outer
// restoration and controller return. Root flips its GPU aliases exactly once
// with endNormalPost; this function advances the independent CPU source owner.
static void finishEndReturnedNativeNormalPostContinuous(NativeNormalPostContinuous& owner,
    const NativeNormalPostContinuousPlan& plan,UINT returnAl=1,UINT callIndex=0,UINT callCount=1) {
    if(!owner.awaitingReturn||!plan.before57Applied)throw std::runtime_error("Normal post Finish/End needs current Before57 and execution");
    if(owner.count10) --owner.count10;
    for(auto& entry:owner.selectors)entry.second^=1;
    owner.changeFStop=false;
    if(returnAl!=0&&callIndex==callCount-1)owner.applyBit23=owner.gatherC0!=0;
    owner.awaitingReturn=false;
}
