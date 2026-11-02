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
static_assert(sizeof(San14RenderInput)==1168);
static_assert(offsetof(San14RenderInput,patternResourceDirectory)==1160);
static_assert(offsetof(RenderInput,patternResourceDirectory)==1160);
static_assert(sizeof(San14RenderReceipt)==968);
static_assert(sizeof(San14AnimationState)==sizeof(AnimationState));
static_assert(sizeof(San14ArmyUnitPosition)==20);
static_assert(sizeof(San14ArmyUnitPosition)==sizeof(ArmyLabelUnitPosition));
static_assert(offsetof(San14ArmyUnitPosition,position)==offsetof(ArmyLabelUnitPosition,position));

static_assert(sizeof(San14Native11BridgeCreateV1)==64);
static_assert(offsetof(San14Native11BridgeCreateV1,output0Resource12)==32);
static_assert(sizeof(San14Native11BridgeStateV2)==232);
static_assert(offsetof(San14Native11BridgeStateV2,slots)==120);
static_assert(sizeof(Native11Bridge::State)==sizeof(San14Native11BridgeStateV2));
static_assert(sizeof(San14Native11DeviceInfoV1)==40);
static_assert(sizeof(San14TerrainProgramSourceV1)==24);
static_assert(offsetof(San14Native11DeviceInfoV1,godotDevice12)==8);
static_assert(offsetof(San14Native11DeviceInfoV1,adapterLuid)==32);
struct Native11DeviceHandle {std::shared_ptr<Native11Device> device;};
Native11DeviceHandle& deviceHandle(void* value) {
    if(!value)throw E_POINTER;return *static_cast<Native11DeviceHandle*>(value);
}
struct QueueBridgeHandle {
    std::shared_ptr<RenderQueueBridge> bridge;
    std::shared_ptr<Native11Bridge> native11;
};
QueueBridgeHandle& bridgeHandle(void* value) {
    if(!value)throw E_POINTER;return *static_cast<QueueBridgeHandle*>(value);
}
std::shared_ptr<RenderQueueBridge> bridgeFrom(void* value) {
    auto& handle=bridgeHandle(value);
    if(!handle.bridge)throw std::runtime_error("Native11 bridge requires state_v2, not a D3D12 Native queue");
    return handle.bridge;
}
struct Owner {
    // The pump may prepare compute work on its worker between render requests.
    // The immediate context and coroutine-owned maps have one serialized owner.
    std::mutex contextGate;
    D3D11InteropOwner interop;
    RenderHost host;
    RenderSession session;
    struct Execution final {
        std::shared_ptr<Native11Device> device;
        Native11Device::Role deviceRole;
        std::unique_lock<std::mutex> sceneRole;
        D3D11InteropOwner::SceneStateScope state;
        explicit Execution(Owner& owner):device(owner.interop.deviceOwner()),
            deviceRole(device?device->acquire():Native11Device::Role{}),
            sceneRole(owner.contextGate),state(owner.interop) {}
    };
    Execution execution(){return Execution(*this);}
    struct PendingScene {
        Microsoft::WRL::ComPtr<ID3D11Texture2D> texture;
        San14RenderReceipt receipt{};
    };
    std::optional<PendingScene> pendingScene;
    bool sceneFailed{};
    enum class ArmyPublicationRole { Campaign,StartupAbsent };
    std::optional<ArmyPublicationRole> acceptedArmyRole;
    bool armyResourcesEnabled{};
#ifdef SAN14_RENDER_OUTPUT_PROBE
    ID3D11Texture2D* finalForProbe{}; // Same borrowed lifetime as RenderOutput.
#endif
    Owner(ID3D12Device* device,ID3D12CommandQueue* queue):interop(device,queue) {
        host.device=interop.device();host.context=interop.context();
    }
    explicit Owner(std::shared_ptr<RenderQueueBridge> bridge)
        :interop(bridge->device(),bridge->nativeQueue(),bridge) {
        host.device=interop.device();host.context=interop.context();
        host.submitScenePrefix=[this]{interop.flushRecordedCommands();};
    }
    explicit Owner(std::shared_ptr<Native11Bridge> bridge):interop(std::move(bridge)) {
        host.device=interop.device();host.context=interop.context();
        // Keep the ordinary EA75 scene prefix policy for the first native11
        // comparison. This is the real native context, never an On12 context.
        host.submitScenePrefix=[this]{interop.flushRecordedCommands();};
    }
    ~Owner() {
        auto guard=execution();
        // Finish commands using session resources before its coroutine owners
        // retire. Interop subsequently clears retained device bindings.
        try{interop.drain();}catch(HRESULT){}
        // A prepared or failed copy retains its scene until real retirement.
        // The session's other GPU owners are destroyed after this body.
        pendingScene.reset();
        // Coroutine-owned GPU resources and callbacks retire while this scene's
        // actual context state and the process execution role are still held.
        session=RenderSession{};
    }
    void requireSceneIdle() const {
        if(pendingScene)throw std::runtime_error("The prepared Main scene must be copied before another scene or animation update");
        if(sceneFailed)throw std::runtime_error("The failed Main scene protocol requires owner retirement");
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
        host.input.patternResourceDirectory=nullptr;
    }
    void replaceSeasonResources(const San14RenderInput& input) {
        if(input.atmosphereResourceDirectory||input.patternResourceDirectory) {
            // Copy the caller strings before invoking retained session owners.
            const fs::path atmosphere=input.atmosphereResourceDirectory?fs::path(input.atmosphereResourceDirectory):fs::path{};
            const fs::path pattern=input.patternResourceDirectory?fs::path(input.patternResourceDirectory):fs::path{};
            if((input.atmosphereResourceDirectory&&atmosphere.empty())||
                (input.patternResourceDirectory&&pattern.empty())||!host.replaceSeasonResources)
                throw std::runtime_error("Season resource replacement requires an initialized session and actual candidate directories");
            // Queue324's copy returns after submission, not GPU completion.
            // Only a changed resource generation enters this explicit drain;
            // both image families then prepare before either owner installs.
            interop.drain();
            host.replaceSeasonResources(atmosphere,pattern);
        }
    }
    void publish(ID3D12Resource* output,const San14RenderInput& input,OutputUse use,San14RenderReceipt& receipt) {
        requireSceneIdle();
        replaceSeasonResources(input);
        accept(input);
        interop.prepareOutput(output,use);
        const auto& frame=session.render();
        interop.copyFinal(frame.texture,frame.request);
#ifdef SAN14_RENDER_OUTPUT_PROBE
        finalForProbe=frame.texture;
#endif
        std::memcpy(receipt.camera,frame.camera.data(),frame.camera.size());
        receipt.request=frame.request;
    }
    void prepareScene(const San14RenderInput& input,const San14CommandMapInputV2* command,
            const San14RouteOverlayInputV1* route,const San14CurrentEffectInputV1* effects,
            San14RenderReceipt& receipt) {
        requireSceneIdle();
        try {
            std::optional<decltype(host.commandMap)> commandCandidate;
            if(command){commandCandidate.emplace(host.commandMap);commandCandidate->accept(*command);}
            if(route){if(!host.replaceRoutes)throw std::runtime_error("Route owner is absent");host.replaceRoutes(*route);}
            if(effects){if(!host.replaceCurrentEffects)throw std::runtime_error("Current Effect owner is absent");host.replaceCurrentEffects(*effects);}
            replaceSeasonResources(input);
            if(commandCandidate)host.commandMap=std::move(*commandCandidate);
            accept(input);
            // Reserve the one scene slot before resuming. A rendering failure
            // cannot permit another Main to overwrite partially recorded work.
            pendingScene.emplace();
            const auto& frame=session.render();
            if(!frame.texture)throw std::runtime_error("Prepared Main scene has no output texture");
            pendingScene->texture=frame.texture;
            std::memcpy(pendingScene->receipt.camera,frame.camera.data(),frame.camera.size());
            pendingScene->receipt.request=frame.request;
            receipt=pendingScene->receipt;
        }catch(...){sceneFailed=true;throw;}
    }
    void copyScene(ID3D12Resource* output,std::uint64_t request,OutputUse use) {
        try {
            if(sceneFailed)throw std::runtime_error("The failed Main scene protocol requires owner retirement");
            if(!pendingScene||!pendingScene->texture||pendingScene->receipt.request!=request)
                throw std::runtime_error("Output copy requires the exact prepared Main request");
            if(use!=OutputUse::RenderTarget&&use!=OutputUse::ShaderRead)throw E_INVALIDARG;
            interop.prepareOutput(output,use);
            interop.copyFinal(pendingScene->texture.Get(),request);
#ifdef SAN14_RENDER_OUTPUT_PROBE
            finalForProbe=pendingScene->texture.Get();
#endif
            pendingScene.reset();
        }catch(...){sceneFailed=true;throw;}
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

void* __cdecl san14_render_queue_bridge_create_v1(void* device,void* godotQueue,void* output0,void* output1) {
    try {
        auto handle=std::make_unique<QueueBridgeHandle>();
        handle->bridge=std::make_shared<RenderQueueBridge>(static_cast<ID3D12Device*>(device),
            static_cast<ID3D12CommandQueue*>(godotQueue),static_cast<ID3D12Resource*>(output0),static_cast<ID3D12Resource*>(output1));
        lastError.clear();return handle.release();
    }catch(...){recordException();return nullptr;}
}
void* __cdecl san14_render_native11_device_create_v1(void* device,San14Native11DeviceInfoV1* result,uint32_t bytes) {
    try {
        if(!result||bytes!=sizeof(*result))throw E_INVALIDARG;*result={};
        auto handle=std::make_unique<Native11DeviceHandle>();
        handle->device=std::make_shared<Native11Device>(static_cast<ID3D12Device*>(device));
        const auto& owner=*handle->device;
        *result={sizeof(*result),1,reinterpret_cast<uint64_t>(owner.godotDevice()),
            reinterpret_cast<uint64_t>(owner.device()),reinterpret_cast<uint64_t>(owner.context()),owner.adapterLuid()};
        lastError.clear();return handle.release();
    }catch(...){recordException();return nullptr;}
}
int __cdecl san14_render_native11_device_destroy_v1(void* value) {
    try {
        auto& handle=deviceHandle(value);
        if(!handle.device->beginClose())throw std::runtime_error("Native11 device owner still has bridge children");
        delete &handle;lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}
void* __cdecl san14_render_native11_bridge_create_v2(void* value,void* queue,uint32_t width,uint32_t height,
        San14Native11BridgeCreateV1* result,uint32_t bytes) {
    try {
        if(!result||bytes!=sizeof(*result))throw E_INVALIDARG;*result={};
        auto device=deviceHandle(value).device;
        auto handle=std::make_unique<QueueBridgeHandle>();
        handle->native11=std::make_shared<Native11Bridge>(std::move(device),static_cast<ID3D12CommandQueue*>(queue),width,height);
        const auto& bridge=*handle->native11;
        *result={sizeof(*result),1,width,height,UINT(DXGI_FORMAT_B8G8R8A8_UNORM),
            UINT(D3D12_RESOURCE_FLAG_ALLOW_RENDER_TARGET|D3D12_RESOURCE_FLAG_ALLOW_SIMULTANEOUS_ACCESS),2,0,
            reinterpret_cast<uint64_t>(bridge.output(0)),reinterpret_cast<uint64_t>(bridge.output(1)),
            reinterpret_cast<uint64_t>(bridge.device()),bridge.adapterLuid()};
        lastError.clear();return handle.release();
    }catch(...){recordException();return nullptr;}
}
int __cdecl san14_render_native11_terrain_programs_load_v1(void* value,
        const San14TerrainProgramSourceV1* sources,uint32_t count) {
    try {
        if(!sources||!count)throw E_INVALIDARG;
        std::vector<NativeTerrainProgramDeclaration> declarations;declarations.reserve(count);
        for(uint32_t i=0;i<count;i++) {
            const auto& source=sources[i];
            if(!source.logicalKey||!source.identity||!source.directory)throw E_POINTER;
            declarations.push_back({source.logicalKey,source.identity,source.directory});
        }
        deviceHandle(value).device->loadTerrainPrograms(declarations);
        lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}

int __cdecl san14_render_native11_route_media_load_v1(void* value,
        const San14TerrainProgramSourceV1* sources,uint32_t count) {
    try {
        if(!sources||!count)throw E_INVALIDARG;
        std::vector<NativeRouteGpuDeclaration> declarations;declarations.reserve(count);
        for(uint32_t i=0;i<count;i++) {
            const auto& source=sources[i];
            if(!source.logicalKey||!source.identity||!source.directory)throw E_POINTER;
            declarations.push_back({source.logicalKey,source.identity,source.directory});
        }
        deviceHandle(value).device->loadRouteMedia(declarations);
        lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}

int __cdecl san14_render_native11_primary_models_load_v1(void* value,
        const San14PrimaryModelGpuSourceV1* sources,uint32_t count) {
    try {
        if(!sources||!count)throw E_INVALIDARG;
        std::vector<NativePrimaryModelGpuDeclaration> declarations;declarations.reserve(count);
        for(uint32_t i=0;i<count;i++) {
            const auto& source=sources[i];
            if(!source.logicalKey||!source.identity||!source.directory)throw E_POINTER;
            declarations.push_back({source.logicalKey,source.identity,source.directory});
        }
        deviceHandle(value).device->loadPrimaryModels(declarations);
        lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}

void* __cdecl san14_render_native11_bridge_create_v1(void* device,void* queue,
        uint32_t width,uint32_t height,San14Native11BridgeCreateV1* result,uint32_t bytes) {
    try {
        if(!result||bytes!=sizeof(*result))throw E_INVALIDARG;*result={};
        auto handle=std::make_unique<QueueBridgeHandle>();
        handle->native11=std::make_shared<Native11Bridge>(static_cast<ID3D12Device*>(device),
            static_cast<ID3D12CommandQueue*>(queue),width,height);
        const auto& bridge=*handle->native11;
        *result={sizeof(*result),1,width,height,UINT(DXGI_FORMAT_B8G8R8A8_UNORM),
            UINT(D3D12_RESOURCE_FLAG_ALLOW_RENDER_TARGET|D3D12_RESOURCE_FLAG_ALLOW_SIMULTANEOUS_ACCESS),2,0,
            reinterpret_cast<uint64_t>(bridge.output(0)),reinterpret_cast<uint64_t>(bridge.output(1)),
            reinterpret_cast<uint64_t>(bridge.device()),bridge.adapterLuid()};
        lastError.clear();return handle.release();
    }catch(...){recordException();return nullptr;}
}
int __cdecl san14_render_queue_bridge_signal_consumed_v1(void* value,uint64_t* result) {
    try{if(!result)throw E_POINTER;auto& handle=bridgeHandle(value);
        *result=handle.native11?handle.native11->signalConsumed():bridgeFrom(value)->signalConsumed();lastError.clear();return 0;}
    catch(...){recordException();return -1;}
}
int __cdecl san14_render_queue_bridge_wait_ready_v1(void* value,void* output,uint64_t request,uint64_t* result) {
    try{if(!result)throw E_POINTER;auto& handle=bridgeHandle(value);
        *result=handle.native11?handle.native11->waitReady(static_cast<ID3D12Resource*>(output),request):
            bridgeFrom(value)->waitReady(static_cast<ID3D12Resource*>(output),request);lastError.clear();return 0;}
    catch(...){recordException();return -1;}
}
int __cdecl san14_render_queue_bridge_seal_godot_tail_v1(void* value,uint64_t* result) {
    try{if(!result)throw E_POINTER;auto& handle=bridgeHandle(value);
        *result=handle.native11?handle.native11->sealGodotTail():bridgeFrom(value)->sealGodotTail();lastError.clear();return 0;}
    catch(...){recordException();return -1;}
}
int __cdecl san14_render_queue_bridge_state_v1(void* value,San14RenderQueueBridgeStateV1* result,uint32_t bytes) {
    try {
        static_assert(sizeof(San14RenderQueueBridgeSlotV1)==48);
        static_assert(sizeof(San14RenderQueueBridgeStateV1)==184);
        static_assert(offsetof(San14RenderQueueBridgeStateV1,device)==16);
        static_assert(offsetof(San14RenderQueueBridgeStateV1,slots)==88);
        if(!result||bytes!=sizeof(*result))throw E_INVALIDARG;
        const auto current=bridgeFrom(value)->state();
        *result={};result->structBytes=sizeof(*result);result->version=1;
        result->retiring=current.retiring;result->retired=current.retired;
        result->device=current.device;result->nativeQueue=current.nativeQueue;result->godotQueue=current.godotQueue;
        result->nativeFence=current.nativeFence;result->godotFence=current.godotFence;
        result->lastNativeSignal=current.lastNativeSignal;result->lastGodotConsumed=current.lastGodotConsumed;
        result->nativeCompleted=current.nativeCompleted;result->godotCompleted=current.godotCompleted;
        for(size_t i=0;i<2;++i) {
            const auto& slot=current.slots[i];result->slots[i]={slot.resource,slot.lastReadyRequest,slot.lastReadyValue,
                slot.lastConsumedValue,slot.lastNativeWaitValue,slot.lastGodotWaitReadyValue};
        }
        lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}
int __cdecl san14_render_queue_bridge_retire_v1(void* value) {
    try{auto& handle=bridgeHandle(value);if(handle.native11)handle.native11->retire();else bridgeFrom(value)->retire();lastError.clear();return 0;}
    catch(...){recordException();return -1;}
}
int __cdecl san14_render_queue_bridge_destroy_v1(void* value) {
    try {
        auto& handle=bridgeHandle(value);
        const bool ready=handle.native11?handle.native11->canDestroy():bridgeFrom(value)->canDestroy();
        if(!ready)throw std::runtime_error("Queue bridge must complete actual GPU retirement before destruction");
        delete static_cast<QueueBridgeHandle*>(value);lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}
int __cdecl san14_render_queue_bridge_state_v2(void* value,San14Native11BridgeStateV2* result,uint32_t bytes) {
    try {
        if(!result||bytes!=sizeof(*result))throw E_INVALIDARG;
        auto& handle=bridgeHandle(value);
        if(!handle.native11)throw std::runtime_error("Legacy On12 bridge requires state_v1");
        const auto state=handle.native11->state();std::memcpy(result,&state,sizeof(*result));
        lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}
static void* createMapWorker(void* bridgeValue,void* output,
        const wchar_t* terrainSourceDirectory,int width,int height,uint32_t worldDetails,
        const San14RenderInput* input,San14RenderReceipt* receipt,
        const wchar_t* terrainProgramKey,const wchar_t* terrainProgramIdentity) {
    try {
        if(!input||!receipt)throw E_POINTER;
        auto& handle=bridgeHandle(bridgeValue);
        if(!handle.native11)bridgeFrom(bridgeValue)->claimMapOwner();
        RenderConfiguration configuration{PreparedResourceDirectories{terrainSourceDirectory},unsigned(width),unsigned(height),
            WorldVisuals::fromInteropFlags(worldDetails)};
        const bool startup=(input->flags&static_cast<unsigned>(FrameInputFlag::StartupArmyOwnerAbsent))!=0;
        if(startup&&(input->armyWorld||input->armyWorldBytes))throw std::runtime_error("Startup has no live Army World publication owner");
        if(configuration.world.armies&&!startup&&(!input->armyWorld||!input->armyWorldBytes))
            throw std::runtime_error("Actual Army population requires its initial World publication, including an empty World");
        auto device=handle.native11?handle.native11->deviceOwner():nullptr;
        auto deviceRole=device?device->acquire():Native11Device::Role{};
        std::shared_ptr<const NativeTerrainPrograms> terrainPrograms;
        if(terrainProgramKey||terrainProgramIdentity) {
            if(!terrainProgramKey||!*terrainProgramKey||!terrainProgramIdentity||!*terrainProgramIdentity||
                !device||!device->sharedScenes())throw std::runtime_error("Resident Landscape programs require their actual application device");
            // Reject a missing generation before claiming this output bridge.
            terrainPrograms=device->borrowTerrainPrograms(terrainProgramKey,terrainProgramIdentity);
        }
        std::shared_ptr<const NativePrimaryModelGpuMedia> primaryGpuMedia;
        std::shared_ptr<const NativePrimaryPassGpuMedia> primaryPassGpu;
        const bool requirePrimaryGpuMedia=device&&device->sharedScenes();
        if(requirePrimaryGpuMedia) {
            const auto reference=readNativePrimaryModelGpuReference(configuration.resources.root()/"primary-gpu-media.ref");
            primaryGpuMedia=device->borrowPrimaryModels(reference.logicalKey,reference.identity);
            auto pass=primaryGpuMedia->passes.find(reference.locale);
            if(pass==primaryGpuMedia->passes.end())throw std::runtime_error("Primary pass locale was not loaded at startup");
            primaryPassGpu=pass->second;
        }
        std::vector<NativeRouteGpuBorrowed> routeMedia;
        if(device&&device->sharedScenes()) {
            for(const auto& source:readNativeRouteGpuReferences(configuration.resources.root()/"route-gpu-media.ref"))
                routeMedia.push_back({source.directory,device->borrowRouteMedia(source.logicalKey,source.identity)});
            if(routeMedia.empty())throw std::runtime_error("Shared map opening has no declared route GPU generation");
        }
        std::unique_ptr<Owner> owner;
        if(handle.native11) {
            if(width<=0||height<=0||UINT(width)!=handle.native11->width()||UINT(height)!=handle.native11->height())throw E_INVALIDARG;
            handle.native11->requireOutput(static_cast<ID3D12Resource*>(output));
            handle.native11->claimMapOwner();owner=std::make_unique<Owner>(handle.native11);
        } else {auto bridge=bridgeFrom(bridgeValue);owner=std::make_unique<Owner>(std::move(bridge));}
        auto guard=owner->execution();
        auto& host=owner->host;
        host.terrainPrograms=std::move(terrainPrograms);
        host.primaryGpuMedia=std::move(primaryGpuMedia);
        host.primaryPassGpu=std::move(primaryPassGpu);
        host.requirePrimaryGpuMedia=requirePrimaryGpuMedia;
        host.routeGpuMedia=std::move(routeMedia);
        host.requireRouteGpuMedia=device&&device->sharedScenes();

        owner->armyResourcesEnabled=configuration.world.armies;
        owner->session=createRenderSession(host,std::move(configuration));
        owner->publish(static_cast<ID3D12Resource*>(output),*input,OutputUse::RenderTarget,*receipt);
        lastError.clear();return owner.release();
    }catch(...){recordException();return nullptr;}
}
void* __cdecl san14_render_create_map_worker_v1(void* bridgeValue,void* output,
        const wchar_t* terrainSourceDirectory,int width,int height,uint32_t worldDetails,
        const San14RenderInput* input,San14RenderReceipt* receipt) {
    return createMapWorker(bridgeValue,output,terrainSourceDirectory,width,height,worldDetails,input,receipt,nullptr,nullptr);
}
void* __cdecl san14_render_create_map_worker_v2(void* bridgeValue,void* output,
        const wchar_t* terrainSourceDirectory,int width,int height,uint32_t worldDetails,
        const wchar_t* terrainProgramKey,const wchar_t* terrainProgramIdentity,
        const San14RenderInput* input,San14RenderReceipt* receipt) {
    if(!terrainProgramKey||!*terrainProgramKey||!terrainProgramIdentity||!*terrainProgramIdentity) {
        lastError="Application Landscape program source identity is absent";return nullptr;
    }
    return createMapWorker(bridgeValue,output,terrainSourceDirectory,width,height,worldDetails,input,receipt,terrainProgramKey,terrainProgramIdentity);
}

int __cdecl san14_render_next(void* value,void* output,const San14RenderInput* input,uint32_t outputUse,San14RenderReceipt* receipt) {
    try{auto& owner=*static_cast<Owner*>(value);auto guard=owner.execution();owner.publish(static_cast<ID3D12Resource*>(output),*input,static_cast<OutputUse>(outputUse),*receipt);lastError.clear();return 0;}
    catch(...){recordException();return -1;}
}
int __cdecl san14_render_next_command_map_v1(void* value,void* output,const San14RenderInput* input,
        const San14CommandMapInputV1* command,uint32_t outputUse,San14RenderReceipt* receipt) {
    try {
        auto& owner=*static_cast<Owner*>(value);
        auto guard=owner.execution();
        owner.requireSceneIdle();
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
        auto guard=owner.execution();
        owner.requireSceneIdle();
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
        auto guard=owner.execution();
        owner.requireSceneIdle();
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
        auto guard=owner.execution();
        owner.requireSceneIdle();
        std::optional<decltype(owner.host.commandMap)> commandCandidate;
        if(command){commandCandidate.emplace(owner.host.commandMap);commandCandidate->accept(*command);}
        if(route){if(!owner.host.replaceRoutes)throw std::runtime_error("Route owner is absent");owner.host.replaceRoutes(*route);}
        if(effects){if(!owner.host.replaceCurrentEffects)throw std::runtime_error("Current Effect owner is absent");owner.host.replaceCurrentEffects(*effects);}
        if(commandCandidate)owner.host.commandMap=std::move(*commandCandidate);
        owner.publish(static_cast<ID3D12Resource*>(output),*input,static_cast<OutputUse>(outputUse),*receipt);
        lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}
int __cdecl san14_render_prepare_scene_v1(void* value,const San14RenderInput* input,
        const San14CommandMapInputV2* command,const San14RouteOverlayInputV1* route,
        const San14CurrentEffectInputV1* effects,San14RenderReceipt* receipt) {
    try {
        if(!value||!input||!receipt)throw E_POINTER;
        auto& owner=*static_cast<Owner*>(value);auto guard=owner.execution();
        owner.prepareScene(*input,command,route,effects,*receipt);
        lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}
int __cdecl san14_render_copy_scene_v1(void* value,void* output,uint64_t request,uint32_t outputUse) {
    try {
        if(!value)throw E_POINTER;
        auto& owner=*static_cast<Owner*>(value);auto guard=owner.execution();
        owner.copyScene(static_cast<ID3D12Resource*>(output),request,static_cast<OutputUse>(outputUse));
        lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}
int __cdecl san14_effect_retire_owner_v1(void* value,uint64_t sourceOwner) {
    try {
        auto& owner=*static_cast<Owner*>(value);auto guard=owner.execution();
        if(!sourceOwner||!owner.host.retireEffectOwner)throw std::runtime_error("Story Effect retirement owner is absent");
        owner.host.retireEffectOwner(sourceOwner);lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}
uint64_t __cdecl san14_effect_direct_create_v1(void* value,const uint8_t* bytes,uint32_t count) {
    try{auto& owner=*static_cast<Owner*>(value);auto guard=owner.execution();auto& host=owner.host;if(!bytes||!count||!host.createDirectEffect)throw std::runtime_error("Direct Effect create owner/package absent");
        const auto id=host.createDirectEffect(bytes,count);lastError.clear();return id;
    }catch(...){recordException();return 0;}
}
int __cdecl san14_effect_direct_submit_v1(void* value,uint64_t id,const uint8_t* bytes,uint32_t count) {
    try{auto& owner=*static_cast<Owner*>(value);auto guard=owner.execution();auto& host=owner.host;if(!bytes||!count||!host.submitDirectEffect)throw std::runtime_error("Direct Effect submit owner/package absent");
        host.submitDirectEffect(id,bytes,count);lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}
int __cdecl san14_effect_direct_live_v1(void* value,uint64_t id,int32_t* live) {
    try{auto& owner=*static_cast<Owner*>(value);auto guard=owner.execution();auto& host=owner.host;if(!live||!host.directEffectLive)throw std::runtime_error("Direct Effect live owner/output absent");
        *live=host.directEffectLive(id);lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}
int __cdecl san14_effect_direct_release_v1(void* value,uint64_t id) {
    try{auto& owner=*static_cast<Owner*>(value);auto guard=owner.execution();auto& host=owner.host;if(!host.releaseDirectEffect)throw std::runtime_error("Direct Effect release owner absent");
        host.releaseDirectEffect(id);lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}
int __cdecl san14_effect_direct_draw_v1(void* value,uint64_t id,const San14CurrentEffectInputV1* material,int64_t ordinal) {
    try{auto& owner=*static_cast<Owner*>(value);auto guard=owner.execution();auto& host=owner.host;if(!material||!host.setDirectEffectDraw)throw std::runtime_error("Direct Effect draw owner/material absent");
        host.setDirectEffectDraw(id,*material,ordinal);lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}
int __cdecl san14_effect_direct_lighting_v1(void* value,uint64_t id,const uint8_t* bytes,uint32_t count,int64_t ordinal) {
    try{auto& owner=*static_cast<Owner*>(value);auto guard=owner.execution();auto& host=owner.host;
        if((!bytes&&count)||!host.setDirectEffectLighting)throw std::runtime_error("Direct Effect lighting owner/input absent");
        host.setDirectEffectLighting(id,bytes,count,ordinal);lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}
int __cdecl san14_effect_lighting_cluster_v1(void* value,const uint8_t* bytes,uint32_t count) {
    try{auto& owner=*static_cast<Owner*>(value);auto guard=owner.execution();auto& host=owner.host;
        if(!bytes||!count||!host.configureEffectLighting)throw std::runtime_error("Effect CLC owner/input absent");
        host.configureEffectLighting(bytes,count);lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}
int __cdecl san14_render_animation_event(void* value,const San14RenderInput* input,uint32_t fpsMode) {
    try {
        auto& owner=*static_cast<Owner*>(value);
        auto guard=owner.execution();
        owner.requireSceneIdle();
        owner.accept(*input);
        owner.host.animationUpdate(fpsMode);
        lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}
void __cdecl san14_render_animation_state(void* value,San14AnimationState* result) {
    auto& owner=*static_cast<Owner*>(value);auto guard=owner.execution();std::memcpy(result,&owner.host.animationState,sizeof(*result));
}
int __cdecl san14_render_army_unit_positions(void* value,San14ArmyUnitPosition* result,uint32_t capacity) {
    try {
        auto& owner=*static_cast<Owner*>(value);auto guard=owner.execution();const auto positions=owner.host.armyUnitPositions();
        if(positions.size()>capacity)throw std::runtime_error("Army Unit position output capacity");
        if(!positions.empty())std::memcpy(result,positions.data(),positions.size()*sizeof(*result));
        lastError.clear();return int(positions.size());
    }catch(...){recordException();return -1;}
}
void __cdecl san14_render_destroy(void* value) {
    auto* owner=static_cast<Owner*>(value);if(!owner)return;
    // This outer role covers every member destructor, not merely ~Owner's body.
    auto device=owner->interop.deviceOwner();
    auto role=device?device->acquire():Native11Device::Role{};
    delete owner;
}
const char* __cdecl san14_render_error(){return lastError.c_str();}
#ifdef SAN14_RENDER_OUTPUT_PROBE
extern "C" __declspec(dllexport) int __cdecl san14_render_probe_output(void* value,const wchar_t* directory) {
    try {
        auto& owner=*static_cast<Owner*>(value);
        auto guard=owner.execution();
        saveFinalOutputProbe(owner.interop.device(),owner.interop.context(),
            owner.finalForProbe,std::filesystem::path(directory));
        lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}
#endif

// Private explicit protocol opt-in. Other existing callers remain synchronous.
extern "C" __declspec(dllexport) int __cdecl san14_render_enable_double_buffered_output_v1(void* value) {
    try {
        if(!value)throw E_POINTER;
        auto& owner=*static_cast<Owner*>(value);auto guard=owner.execution();
        owner.interop.enableDoubleBufferedOutput();
        owner.host.submitScenePrefix=[ownerPtr=&owner]{ownerPtr->interop.flushRecordedCommands();};
        lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}
extern "C" __declspec(dllexport) int __cdecl san14_render_drain_submitted_output_v1(void* value) {
    try {
        if(!value)throw E_POINTER;
        auto& owner=*static_cast<Owner*>(value);auto guard=owner.execution();
        owner.interop.drain();lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}
// Explicit finite diagnostic snapshot, never a per-frame correctness scan.
extern "C" __declspec(dllexport) int __cdecl san14_render_output_queue_state_v1(void* value,
        San14::Render::D3D11InteropOwner::OutputQueueState* result,uint32_t bytes) {
    try {
        static_assert(sizeof(*result)==88);
        if(!value||!result||bytes!=sizeof(*result))throw E_INVALIDARG;
        auto& owner=*static_cast<Owner*>(value);auto guard=owner.execution();
        *result=owner.interop.outputQueueState();lastError.clear();return 0;
    }catch(...){recordException();return -1;}
}
