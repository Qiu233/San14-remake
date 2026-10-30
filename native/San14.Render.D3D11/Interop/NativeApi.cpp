#define NOMINMAX
#include "NativeApi.h"
#include "Interop.h"
#include "../Runtime/RenderSession.h"
#include <filesystem>
#include <memory>
#include <sstream>
#include <mutex>
#ifdef SAN14_RENDER_OUTPUT_PROBE
#include "../Runtime/Diagnostics/OutputProbe.h"
#endif

namespace {
namespace fs=std::filesystem;
using namespace San14::Render;
thread_local std::string lastError;
static_assert(sizeof(San14RenderInput)==sizeof(RenderInput));
static_assert(sizeof(San14RenderInput)==1160);
static_assert(sizeof(San14RenderReceipt)==968);
static_assert(sizeof(San14AnimationState)==sizeof(AnimationState));
static_assert(sizeof(San14ArmyUnitPosition)==20);
static_assert(sizeof(San14ArmyUnitPosition)==sizeof(ArmyLabelUnitPosition));
static_assert(offsetof(San14ArmyUnitPosition,position)==offsetof(ArmyLabelUnitPosition,position));

struct Owner {
    // The pump may prepare compute work on its worker between render requests.
    // The immediate context and coroutine-owned maps have one serialized owner.
    std::mutex contextGate;
    D3D11InteropOwner interop;
    RenderHost host;
    RenderSession session;
    enum class ArmyPublicationRole { Campaign,StartupAbsent };
    std::optional<ArmyPublicationRole> acceptedArmyRole;
    bool armyResourcesEnabled{};
#ifdef SAN14_RENDER_OUTPUT_PROBE
    ID3D11Texture2D* finalForProbe{}; // Same borrowed lifetime as RenderOutput.
#endif
    Owner(ID3D12Device* device,ID3D12CommandQueue* queue):interop(device,queue) {
        host.device=interop.device();host.context=interop.context();
    }
    ~Owner() {
        // Finish commands using session resources before its coroutine owners
        // retire. Interop subsequently clears retained device bindings.
        try{interop.drain();}catch(HRESULT){}
    }
    void accept(const San14RenderInput& input) {
        const auto role=(input.flags&static_cast<unsigned>(FrameInputFlag::StartupArmyOwnerAbsent))
            ?ArmyPublicationRole::StartupAbsent:ArmyPublicationRole::Campaign;
        if(role==ArmyPublicationRole::StartupAbsent) {
            if(acceptedArmyRole==ArmyPublicationRole::Campaign)
                throw std::runtime_error("A live campaign Army owner cannot return to Startup");
            if(input.armyWorld||input.armyWorldBytes||host.armyWorld)
                throw std::runtime_error("Startup has no live Army World publication owner");
        } else if(armyResourcesEnabled&&!host.armyWorld&&(!input.armyWorld||!input.armyWorldBytes))
            throw std::runtime_error("Campaign adoption requires its first actual Army World publication");

        //Decode new immutable publications before changing the role. A format
        //failure cannot promote a Startup owner or consume its first packet.
        std::optional<ArmyWorldPublication> army;
        if(input.armyWorld)army=copyArmyWorldPublication(input.armyWorld,input.armyWorldBytes);
        else if(input.armyWorldBytes)throw std::runtime_error("Army World packet pointer is absent");
        std::optional<source_current_season::Frame> season;
        if(input.currentSeason)season=source_current_season::decode({input.currentSeason,input.currentSeasonBytes});
        else if(input.currentSeasonBytes)throw std::runtime_error("Current season packet pointer is absent");
        std::optional<CityGateWorldPublication> cities;
        if(input.cityGateWorld)cities=copyCityGateWorldPublication(input.cityGateWorld,input.cityGateWorldBytes);
        else if(input.cityGateWorldBytes)throw std::runtime_error("City/Gate World packet pointer is absent");
        std::optional<AreaCapitalWorldPublication> areas;
        if(input.areaCapitalWorld)areas=copyAreaCapitalWorldPublication(input.areaCapitalWorld,input.areaCapitalWorldBytes);
        else if(input.areaCapitalWorldBytes)throw std::runtime_error("Area Capital World packet pointer is absent");
        host.territoryOwnership.acceptMapColors(input.ownershipMapColors,input.ownershipMapColorCount);
        host.territoryOwnership.accept(input.ownershipColors,input.ownershipCount,input.ownershipRevision);
        std::memcpy(&host.input,&input,sizeof(input));
        if(army)host.armyWorld=std::move(army);
        if(season)host.currentSeason=std::move(season);
        if(cities)host.cityGateWorld=std::move(cities);
        if(areas)host.areaCapitalWorld=std::move(areas);
        acceptedArmyRole=role;
        host.input.ownershipColors=nullptr;host.input.ownershipCount=0;
        host.input.ownershipMapColors=nullptr;host.input.ownershipMapColorCount=0;
        host.input.armyWorld=nullptr;host.input.armyWorldBytes=0;
        host.input.currentSeason=nullptr;host.input.currentSeasonBytes=0;
        host.input.cityGateWorld=nullptr;host.input.cityGateWorldBytes=0;
        host.input.areaCapitalWorld=nullptr;host.input.areaCapitalWorldBytes=0;
        host.input.atmosphereResourceDirectory=nullptr;
    }
    void publish(ID3D12Resource* output,const San14RenderInput& input,OutputUse use,San14RenderReceipt& receipt) {
        if(input.atmosphereResourceDirectory) {
            // Copy the caller string before invoking retained session owners.
            // A preparation failure occurs before request acceptance or resume.
            const fs::path candidate(input.atmosphereResourceDirectory);
            if(candidate.empty()||!host.replaceAtmosphereResources)
                throw std::runtime_error("Atmosphere replacement requires an initialized session and a candidate directory");
            host.replaceAtmosphereResources(candidate);
        }
        accept(input);
        interop.prepareOutput(output,use);
        const auto& frame=session.render();
        interop.copyFinal(frame.texture);
#ifdef SAN14_RENDER_OUTPUT_PROBE
        finalForProbe=frame.texture;
#endif
        std::memcpy(receipt.camera,frame.camera.data(),frame.camera.size());
        receipt.request=frame.request;
    }
};
void recordException() {
    try{throw;}
    catch(HRESULT result){std::ostringstream text;text<<"Direct3D HRESULT=0x"<<std::hex<<uint32_t(result);lastError=text.str();}
    catch(const std::exception& error){lastError=error.what();}
    catch(...){lastError="Native Main failed";}
}
}

void* __cdecl san14_render_create(void* device,void* queue,void* output,
        const wchar_t* terrainSourceDirectory,int width,int height,uint32_t worldDetails,
        const San14RenderInput* input,San14RenderReceipt* receipt) {
    try {
        RenderConfiguration configuration{
            PreparedResourceDirectories{terrainSourceDirectory}, unsigned(width), unsigned(height),
            WorldVisuals::fromInteropFlags(worldDetails)};
        if(!input)throw std::runtime_error("Initial render input is absent");
        const bool startup=(input->flags&static_cast<unsigned>(FrameInputFlag::StartupArmyOwnerAbsent))!=0;
        if(startup&&(input->armyWorld||input->armyWorldBytes))
            throw std::runtime_error("Startup has no live Army World publication owner");
        if(configuration.world.armies&&!startup&&(!input->armyWorld||!input->armyWorldBytes))
            throw std::runtime_error("Actual Army population requires its initial World publication, including an empty World");
        auto owner=std::make_unique<Owner>(static_cast<ID3D12Device*>(device),static_cast<ID3D12CommandQueue*>(queue));
        auto& host=owner->host;
        owner->armyResourcesEnabled=configuration.world.armies;
        owner->session=createRenderSession(host,std::move(configuration));
        owner->publish(static_cast<ID3D12Resource*>(output),*input,OutputUse::RenderTarget,*receipt);
        lastError.clear();return owner.release();
    }catch(...){recordException();return nullptr;}
}
int __cdecl san14_render_next(void* value,void* output,const San14RenderInput* input,uint32_t outputUse,San14RenderReceipt* receipt) {
    try{auto& owner=*static_cast<Owner*>(value);std::lock_guard guard(owner.contextGate);owner.publish(static_cast<ID3D12Resource*>(output),*input,static_cast<OutputUse>(outputUse),*receipt);lastError.clear();return 0;}
    catch(...){recordException();return -1;}
}
int __cdecl san14_render_next_command_map_v1(void* value,void* output,const San14RenderInput* input,
        const San14CommandMapInputV1* command,uint32_t outputUse,San14RenderReceipt* receipt) {
    try {
        auto& owner=*static_cast<Owner*>(value);
        std::lock_guard guard(owner.contextGate);
        if(!command)throw std::runtime_error("Command map request extension is absent");
        owner.host.commandMap.accept(*command);
        owner.publish(static_cast<ID3D12Resource*>(output),*input,static_cast<OutputUse>(outputUse),*receipt);
        lastError.clear();return 0;
    } catch(...){recordException();return -1;}
}
int __cdecl san14_render_next_command_map_v2(void* value,void* output,const San14RenderInput* input,
        const San14CommandMapInputV2* command,uint32_t outputUse,San14RenderReceipt* receipt) {
    try {
        auto& owner=*static_cast<Owner*>(value);
        std::lock_guard guard(owner.contextGate);
        if(!command)throw std::runtime_error("Command map V2 request extension is absent");
        owner.host.commandMap.accept(*command);
        owner.publish(static_cast<ID3D12Resource*>(output),*input,static_cast<OutputUse>(outputUse),*receipt);
        lastError.clear();return 0;
    } catch(...){recordException();return -1;}
}
int __cdecl san14_render_next_map_overlays_v1(void* value,void* output,const San14RenderInput* input,
        const San14CommandMapInputV2* command,const San14RouteOverlayInputV1* route,
        uint32_t outputUse,San14RenderReceipt* receipt) {
    try {
        auto& owner=*static_cast<Owner*>(value);
        std::lock_guard guard(owner.contextGate);
        // Prepare both extensions before publishing either. Route resource/VB
        // failures cannot consume a command revision or resume the coroutine.
        std::optional<decltype(owner.host.commandMap)> commandCandidate;
        if(command){commandCandidate.emplace(owner.host.commandMap);commandCandidate->accept(*command);}
        if(route) {
            if(!owner.host.replaceRoutes)throw std::runtime_error("Route publication requires an initialized session");
            owner.host.replaceRoutes(*route);
        }
        if(commandCandidate)owner.host.commandMap=std::move(*commandCandidate);
        owner.publish(static_cast<ID3D12Resource*>(output),*input,static_cast<OutputUse>(outputUse),*receipt);
        lastError.clear();return 0;
    } catch(...){recordException();return -1;}
}
int __cdecl san14_render_next_map_effects_v1(void* value,void* output,const San14RenderInput* input,
        const San14CommandMapInputV2* command,const San14RouteOverlayInputV1* route,
        const San14CurrentEffectInputV1* effects,uint32_t outputUse,San14RenderReceipt* receipt) {
    try {
        auto& owner=*static_cast<Owner*>(value);
        std::lock_guard guard(owner.contextGate);
        std::optional<decltype(owner.host.commandMap)> commandCandidate;
        if(command){commandCandidate.emplace(owner.host.commandMap);commandCandidate->accept(*command);}
        if(route){if(!owner.host.replaceRoutes)throw std::runtime_error("Route owner is absent");owner.host.replaceRoutes(*route);}
        if(effects){if(!owner.host.replaceCurrentEffects)throw std::runtime_error("Current Effect owner is absent");owner.host.replaceCurrentEffects(*effects);}
        if(commandCandidate)owner.host.commandMap=std::move(*commandCandidate);
        owner.publish(static_cast<ID3D12Resource*>(output),*input,static_cast<OutputUse>(outputUse),*receipt);
        lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}
int __cdecl san14_effect_retire_owner_v1(void* value,uint64_t sourceOwner) {
    try {
        auto& owner=*static_cast<Owner*>(value);std::lock_guard guard(owner.contextGate);
        if(!sourceOwner||!owner.host.retireEffectOwner)throw std::runtime_error("Story Effect retirement owner is absent");
        owner.host.retireEffectOwner(sourceOwner);lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}
uint64_t __cdecl san14_effect_direct_create_v1(void* value,const uint8_t* bytes,uint32_t count) {
    try{auto& owner=*static_cast<Owner*>(value);std::lock_guard guard(owner.contextGate);auto& host=owner.host;if(!bytes||!count||!host.createDirectEffect)throw std::runtime_error("Direct Effect create owner/package absent");
        const auto id=host.createDirectEffect(bytes,count);lastError.clear();return id;
    }catch(...){recordException();return 0;}
}
int __cdecl san14_effect_direct_submit_v1(void* value,uint64_t id,const uint8_t* bytes,uint32_t count) {
    try{auto& owner=*static_cast<Owner*>(value);std::lock_guard guard(owner.contextGate);auto& host=owner.host;if(!bytes||!count||!host.submitDirectEffect)throw std::runtime_error("Direct Effect submit owner/package absent");
        host.submitDirectEffect(id,bytes,count);lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}
int __cdecl san14_effect_direct_live_v1(void* value,uint64_t id,int32_t* live) {
    try{auto& owner=*static_cast<Owner*>(value);std::lock_guard guard(owner.contextGate);auto& host=owner.host;if(!live||!host.directEffectLive)throw std::runtime_error("Direct Effect live owner/output absent");
        *live=host.directEffectLive(id);lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}
int __cdecl san14_effect_direct_release_v1(void* value,uint64_t id) {
    try{auto& owner=*static_cast<Owner*>(value);std::lock_guard guard(owner.contextGate);auto& host=owner.host;if(!host.releaseDirectEffect)throw std::runtime_error("Direct Effect release owner absent");
        host.releaseDirectEffect(id);lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}
int __cdecl san14_effect_direct_draw_v1(void* value,uint64_t id,const San14CurrentEffectInputV1* material,int64_t ordinal) {
    try{auto& owner=*static_cast<Owner*>(value);std::lock_guard guard(owner.contextGate);auto& host=owner.host;if(!material||!host.setDirectEffectDraw)throw std::runtime_error("Direct Effect draw owner/material absent");
        host.setDirectEffectDraw(id,*material,ordinal);lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}
int __cdecl san14_effect_direct_lighting_v1(void* value,uint64_t id,const uint8_t* bytes,uint32_t count,int64_t ordinal) {
    try{auto& owner=*static_cast<Owner*>(value);std::lock_guard guard(owner.contextGate);auto& host=owner.host;
        if((!bytes&&count)||!host.setDirectEffectLighting)throw std::runtime_error("Direct Effect lighting owner/input absent");
        host.setDirectEffectLighting(id,bytes,count,ordinal);lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}
int __cdecl san14_effect_lighting_cluster_v1(void* value,const uint8_t* bytes,uint32_t count) {
    try{auto& owner=*static_cast<Owner*>(value);std::lock_guard guard(owner.contextGate);auto& host=owner.host;
        if(!bytes||!count||!host.configureEffectLighting)throw std::runtime_error("Effect CLC owner/input absent");
        host.configureEffectLighting(bytes,count);lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}
int __cdecl san14_render_animation_event(void* value,const San14RenderInput* input,uint32_t fpsMode) {
    try {
        auto& owner=*static_cast<Owner*>(value);
        std::lock_guard guard(owner.contextGate);
        owner.accept(*input);
        owner.host.animationUpdate(fpsMode);
        lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}
void __cdecl san14_render_animation_state(void* value,San14AnimationState* result) {
    auto& owner=*static_cast<Owner*>(value);std::lock_guard guard(owner.contextGate);std::memcpy(result,&owner.host.animationState,sizeof(*result));
}
int __cdecl san14_render_army_unit_positions(void* value,San14ArmyUnitPosition* result,uint32_t capacity) {
    try {
        auto& owner=*static_cast<Owner*>(value);std::lock_guard guard(owner.contextGate);const auto positions=owner.host.armyUnitPositions();
        if(positions.size()>capacity)throw std::runtime_error("Army Unit position output capacity");
        if(!positions.empty())std::memcpy(result,positions.data(),positions.size()*sizeof(*result));
        lastError.clear();return int(positions.size());
    }catch(...){recordException();return -1;}
}
void __cdecl san14_render_destroy(void* owner){delete static_cast<Owner*>(owner);}
const char* __cdecl san14_render_error(){return lastError.c_str();}
#ifdef SAN14_RENDER_OUTPUT_PROBE
extern "C" __declspec(dllexport) int __cdecl san14_render_probe_output(void* value,const wchar_t* directory) {
    try {
        auto& owner=*static_cast<Owner*>(value);
        saveFinalOutputProbe(owner.interop.device(),owner.interop.context(),
            owner.finalForProbe,std::filesystem::path(directory));
        lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}
#endif
