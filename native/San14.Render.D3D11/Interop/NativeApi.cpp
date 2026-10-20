#define NOMINMAX
#include "NativeApi.h"
#include "Interop.h"
#include "../Runtime/RenderSession.h"
#include <filesystem>
#include <memory>
#include <sstream>
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
    D3D11InteropOwner interop;
    RenderHost host;
    RenderSession session;
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
        std::memcpy(&host.input,&input,sizeof(input));
        host.territoryOwnership.acceptMapColors(input.ownershipMapColors,input.ownershipMapColorCount);
        host.territoryOwnership.accept(input.ownershipColors,input.ownershipCount,input.ownershipRevision);
        if(input.armyWorld)host.armyWorld=copyArmyWorldPublication(input.armyWorld,input.armyWorldBytes);
        else if(input.armyWorldBytes)throw std::runtime_error("Army World packet pointer is absent");
        if(input.currentSeason)host.currentSeason=source_current_season::decode(
            {input.currentSeason,input.currentSeasonBytes});
        else if(input.currentSeasonBytes)throw std::runtime_error("Current season packet pointer is absent");
        if(input.cityGateWorld)host.cityGateWorld=copyCityGateWorldPublication(input.cityGateWorld,input.cityGateWorldBytes);
        else if(input.cityGateWorldBytes)throw std::runtime_error("City/Gate World packet pointer is absent");
        if(input.areaCapitalWorld)host.areaCapitalWorld=copyAreaCapitalWorldPublication(input.areaCapitalWorld,input.areaCapitalWorldBytes);
        else if(input.areaCapitalWorldBytes)throw std::runtime_error("Area Capital World packet pointer is absent");
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
        if(configuration.world.armies&&(!input->armyWorld||!input->armyWorldBytes))
            throw std::runtime_error("Actual Army population requires its initial World publication, including an empty World");
        auto owner=std::make_unique<Owner>(static_cast<ID3D12Device*>(device),static_cast<ID3D12CommandQueue*>(queue));
        auto& host=owner->host;
        owner->session=createRenderSession(host,std::move(configuration));
        owner->publish(static_cast<ID3D12Resource*>(output),*input,OutputUse::RenderTarget,*receipt);
        lastError.clear();return owner.release();
    }catch(...){recordException();return nullptr;}
}
int __cdecl san14_render_next(void* value,void* output,const San14RenderInput* input,uint32_t outputUse,San14RenderReceipt* receipt) {
    try{static_cast<Owner*>(value)->publish(static_cast<ID3D12Resource*>(output),*input,static_cast<OutputUse>(outputUse),*receipt);lastError.clear();return 0;}
    catch(...){recordException();return -1;}
}
int __cdecl san14_render_next_command_map_v1(void* value,void* output,const San14RenderInput* input,
        const San14CommandMapInputV1* command,uint32_t outputUse,San14RenderReceipt* receipt) {
    try {
        auto& owner=*static_cast<Owner*>(value);
        if(!command)throw std::runtime_error("Command map request extension is absent");
        owner.host.commandMap.accept(*command);
        owner.publish(static_cast<ID3D12Resource*>(output),*input,static_cast<OutputUse>(outputUse),*receipt);
        lastError.clear();return 0;
    } catch(...){recordException();return -1;}
}
int __cdecl san14_render_animation_event(void* value,const San14RenderInput* input,uint32_t fpsMode) {
    try {
        auto& owner=*static_cast<Owner*>(value);
        owner.accept(*input);
        owner.host.animationUpdate(fpsMode);
        lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}
void __cdecl san14_render_animation_state(void* value,San14AnimationState* result) {
    std::memcpy(result,&static_cast<Owner*>(value)->host.animationState,sizeof(*result));
}
int __cdecl san14_render_army_unit_positions(void* value,San14ArmyUnitPosition* result,uint32_t capacity) {
    try {
        const auto positions=static_cast<Owner*>(value)->host.armyUnitPositions();
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
