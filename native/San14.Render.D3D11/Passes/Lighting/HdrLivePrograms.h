#pragma once
#include "../../Runtime/PreparedFileInput.h"
// Include after the HDR GPU and camera/refraction numeric helpers.
#include "HdrCurrentSelection.h"
#include "CurrentClusterLights.h"
#include "HdrHeightFogState.h"
#include "HdrMaterialModifiers.h"
#include "HdrShaderModules.h"
#include "Interop/HdrProgramBankInput.h"
#include "Interop/HdrProgramBindingInputs.h"
#include <map>
#include <memory>
#include <set>
#include <type_traits>

namespace San14::Render {
struct HdrSamplerRecipe { unsigned slot{}; std::array<D3D11_SAMPLER_DESC,2> descriptions{}; bool paired{}; };
struct HdrClusterWrite { unsigned kind{},offset{},sourceOffset{},bytes{}; };
struct HdrProgramRecipe {
    std::string unsupported;
    std::vector<NativeSourceHdrConstant> constants;
    HdrLightingLayout lighting;
    std::vector<NativeDeferredCameraWrite> camera;
    std::vector<NativeWaterRefractionWrite> refraction;
    std::vector<HdrHeightFogWrite> heightFog;
    std::vector<HdrClusterWrite> cluster;
    std::vector<HdrMaterialWrite> materialWrites;
    std::vector<HdrTextureRecipe> textures;
    std::vector<HdrSamplerRecipe> samplers;
    std::vector<char> material;
    bool hasHatching{}, hasShadowFade{};
};
struct HdrInstalledRoute {
    HdrCurrentRoute current;
    std::uint32_t recipe{};
};
class FreshHdrCausticsModule {
public:
    explicit FreshHdrCausticsModule(const fs::path& path) {
        NativePreparedInputFile input(path); std::string width, height;
        if (!(input >> width >> height)) throw std::runtime_error("HDR Caustics Main dimensions are absent");
        width_ = std::stof(width); height_ = std::stof(height);
    }
    void update(const PreparedMapView& view, const HdrLightState& modifier) {
        if (modifier.causticsUpdateDisabled || !view.ambientSelection()) return;
        if (!(width_ > 0) || !(height_ > 0)) return;
        // The existing View publisher owns only the classification subset.
        // Positive dimensions also require the real A10/A14 source values and
        // image producer; neither is inferred from those classifications.
        throw std::runtime_error("HDR positive Caustics dimensions require the ambient gate fields and texture producer");
    }
    ID3D11ShaderResourceView* image() const { return image_.Get(); }
private:
    float width_{}, height_{};
    ComPtr<ID3D11ShaderResourceView> image_;
};

class HdrLivePrograms {
public:
    void prepare(ID3D11Device* device, const fs::path& directory, unsigned node,
        ID3D11Texture2D* hdr, const std::array<ComPtr<ID3D11Texture2D>, 5>& gbuffer,
        const std::array<DXGI_FORMAT, 6>& formats, ID3D11Texture2D* ggx,HdrShaderModuleBank& modules) {
        device_ = device;
        auto imported = Interop::readHdrProgramBank(directory / std::to_string(node) / "bank.bin");
        if (imported.bank.node() != node || (node != 78 && node != 123 && node != 166))
            throw std::runtime_error("HDR entry requires an implemented normal bank");
        root_ = directory / std::to_string(node);
        modules_=&modules.borrow(root_/"shader-module.bin");
        imported_ = std::move(imported);
        if (imported_->bank.caustics()) caustics_.emplace(directory / "caustics.txt");
        auto makeView = [&](ID3D11Texture2D* texture, DXGI_FORMAT format) {
            D3D11_TEXTURE2D_DESC td{}; texture->GetDesc(&td);
            D3D11_SHADER_RESOURCE_VIEW_DESC sd{}; sd.Format = format;
            if (td.ArraySize > 1) {
                sd.ViewDimension = D3D11_SRV_DIMENSION_TEXTURE2DARRAY;
                sd.Texture2DArray.ArraySize = td.ArraySize; sd.Texture2DArray.MipLevels = td.MipLevels;
            } else { sd.ViewDimension = D3D11_SRV_DIMENSION_TEXTURE2D; sd.Texture2D.MipLevels = td.MipLevels; }
            ComPtr<ID3D11ShaderResourceView> result;
            check(device->CreateShaderResourceView(texture, &sd, &result), "HDR bank actual fixed resource view");
            return result;
        };
        for (unsigned i = 0; i < passes_.gbuffer.size(); ++i) passes_.gbuffer[i] = makeView(gbuffer[i].Get(), formats[i]);
        passes_.ggx = makeView(ggx, DXGI_FORMAT_R16G16_FLOAT);
        std::string key; unsigned resource{}, shader{}, target{}, width{}, height{};
        NativePreparedInputFile declaration(directory / "target.txt");
        declaration >> key >> resource >> shader >> target >> width >> height;
        if (!declaration) throw std::runtime_error("HDR bank actual Scene target declaration is absent");
        D3D11_TEXTURE2D_DESC td{}; hdr->GetDesc(&td);
        if (td.Width != width || td.Height != height || td.Format != DXGI_FORMAT(resource))
            throw std::runtime_error("HDR bank Scene target differs from its actual declaration");
        D3D11_RENDER_TARGET_VIEW_DESC rd{}; rd.Format = DXGI_FORMAT(target); rd.ViewDimension = D3D11_RTV_DIMENSION_TEXTURE2D;
        check(device->CreateRenderTargetView(hdr, &rd, &target_), "HDR bank Scene target view");
        viewport_ = {0, 0, float(width), float(height), 0, 1};
        std::set<std::uint32_t> recipeIds(imported_->recipes.begin(), imported_->recipes.end());
        for (auto id : recipeIds) recipes_.emplace(id, readRecipe(root_ / "recipes" / std::to_string(id)));
        for (const auto& program : imported_->bank.programs()) {
            if (!vertexBytes_.contains(program.vertex)) vertexBytes_.emplace(program.vertex, load(root_ / "programs" / (std::to_string(program.vertex) + ".dxbc")));
            if (!pixelBytes_.contains(program.pixel)) pixelBytes_.emplace(program.pixel, load(root_ / "programs" / (std::to_string(program.pixel) + ".dxbc")));
        }
    }

    void update(ID3D11DeviceContext* context, const PreparedMapView& view,
        const MapViewDefinition& definition, const MapViewCameraSettings& cameraSettings,
        const NativeDeferredCameraFields& camera, const source_current_season::Frame& current,
        unsigned phaseFlags, float commonClock, const source_water_camera::RefractionDerived* refraction,
        const NativeSourceHdrSpecularImages& specular, const NativeSourceClusterLights* cluster=nullptr) {
        if (!current.hdrSelection) throw std::runtime_error("HDR current selection settings are absent");
        const auto& imported = imported_.value();
        const auto color = prepareHdrColorMode(imported.constructorFlags, imported.bank.ocean(), phaseFlags);
        if (!color) { passes_.passes.clear(); routes_.clear(); return; }
        const auto settings = prepareHdrSelectionSettings(*current.hdrSelection, view, definition.common, phaseFlags, *color);
        const auto associations = prepareHdrAssociations(imported.bank, definition.common, view, current, imported.bank.ocean());
        if(!modules_)throw std::runtime_error("HDR shader module owner has not been prepared");
        auto moduleCandidate=*modules_;
        std::vector<NativeSourceHdrPass> pending;
        std::vector<HdrInstalledRoute> pendingRoutes;
        std::optional<HdrFeatureScratch> scratch;
        const auto mode=current.hdrSelection->commonEffectMode;
        auto heightCandidate=prepareHdrHeightFog(heightFog_,current.heightFog,camera.eye[2],
            mode>=1&&mode<=5&&(definition.common.featureFlags&0x200)!=0&&view.ambientSelection().has_value());
        pending.reserve(associations.size()); pendingRoutes.reserve(associations.size());
        for (auto association : associations) {
            auto route = prepareHdrCurrentRoute(current, view, cameraSettings, association, *color, scratch);
            const auto selected = imported.bank.select(route.feature, settings);
            const auto recipeId = imported.recipes[selected.featureIndex * imported.bank.variantCount() + selected.variantIndex];
            const auto& recipe = recipes_.at(recipeId);
            if (!recipe.unsupported.empty()) throw std::runtime_error("Selected HDR recipe has no actual producer: " + recipe.unsupported);
            const auto& outer = current.lights[association.outer].lighting;
            const auto& inner = current.lights[association.inner].lighting;
            const auto& modifier = inner.modifier.present ? inner : outer;
            if (caustics_) caustics_->update(view, modifier);
            const bool fadeAllowed=imported.bank.ocean()||!settings.context.sharedTargets||
                (definition.common.present&&(definition.common.featureFlags&0x40)!=0);
            if(recipe.materialWrites.empty()&&
                ((recipe.hasHatching&&!modifier.preservesFreshHatching)||
                 (recipe.hasShadowFade&&!modifier.preservesFreshShadowFade&&fadeAllowed)))
                throw std::runtime_error("Selected HDR program requires its current material semantic layout");
            auto old = std::find_if(routes_.begin(), routes_.end(), [&](const auto& prior) {
                return prior.recipe == recipeId && prior.current.association.outer == association.outer &&
                    prior.current.association.inner == association.inner;
            });
            auto pass = old == routes_.end() ? createPass(recipe, selected.program)
                : passes_.passes[std::size_t(std::distance(routes_.begin(), old))];
            pass.identity = "HDR" + std::to_string(imported.bank.node()) + ":" + std::to_string(association.outer) + ":" + std::to_string(association.inner);
            pass.reference = association.innerGroup | static_cast<unsigned>(*color);
            pass.specularSourceIndex=current.hdrSelection->specularSourceIndex;
            for (const auto& texture:recipe.textures) if(texture.role=="TranslucentSpecular") {
                const auto side=pass.specularSourceIndex;
                selectHdrSpecularKey(texture,side,specular.logicalKeys);
                if (!specular.views[side]) throw std::runtime_error("Selected HDR specular physical image is absent");
                pass.fixedPixelViews[texture.slot]=specular.views[side];
            }
            auto& constant = pass.constants.at(pass.lightingConstantIndex);
            // 97E590 visits the shader's entire module chain on every real
            // Prepare, including a selected program which does not read UV.
            std::optional<HdrHatchingValues> hatching;
            if(moduleCandidate.hasHatching) {
                if(!modifier.preservesFreshHatching&&!modifier.modifier.material)
                    throw std::runtime_error("Current Hatching module has no selected modifier data");
                HdrMaterialModifier reset;reset.rampStart=-1;reset.repeat={1,1};
                const auto& input=modifier.preservesFreshHatching?reset:*modifier.modifier.material;
                hatching=prepareHdrHatching(input,commonClock,moduleCandidate.hatching);
            }
            writeHdrMaterialModifiers(recipe.materialWrites,modifier,fadeAllowed,hatching,constant.bytes);
            for(const auto& texture:recipe.textures) {
                const int lane=clusterLane(texture.role);
                if(lane<0)continue;
                if(!cluster||!cluster->computeSucceeded)
                    throw std::runtime_error("Selected HDR ClusterLightList has no successful current compute owner");
                auto resource=cluster->resources[std::size_t(lane)];
                // A producer which is truly absent may bind null for its own
                // unused spot/point lanes; the shared index owner is required.
                const bool required=lane==0||(lane<=3?cluster->pointCount!=0:cluster->spotCount!=0);
                if(required&&!resource)throw std::runtime_error("Selected HDR cluster typed resource is absent: "+texture.role);
                pass.fixedPixelViews[texture.slot]=resource;
            }
            if(!recipe.cluster.empty()) {
                // Prepared rows contain only RDEF-used lanes. Disabled
                // Scaling leaves its unread storage untouched rather than
                // manufacturing a value for the optional current setter.
                if(!cluster||!cluster->computeSucceeded)
                    throw std::runtime_error("Selected HDR cluster constants have no current compute owner");
                for(const auto& write:recipe.cluster) {
                    const void* value{};
                    switch(write.kind) {
                        case 0:value=cluster->depthParameters.data();break;
                        case 1:value=cluster->patchCounts.data();break;
                        case 2:if(cluster->scaling)value=&*cluster->scaling;break;
                        case 3:if(cluster->fadeDistance)value=cluster->fadeDistance->data();break;
                    }
                    if(!value)throw std::runtime_error("Selected HDR cluster field has no current writer");
                    std::memcpy(constant.bytes.data()+write.offset,static_cast<const char*>(value)+write.sourceOffset,write.bytes);
                }
            }
            writeHdrLighting(recipe.lighting, route.lighting, constant.bytes);
            if (!recipe.heightFog.empty()) {
                const auto effectMode=current.hdrSelection->commonEffectMode;
                const auto height=prepareHdrHeightFogValues(heightCandidate,commonClock,
                    effectMode>=2&&effectMode<=5&&*color!=HdrColorMode::CameraSpecular);
                writeHdrHeightFog(recipe.heightFog,height,constant.bytes);
            }
            NativeDeferredCameraConstant cameraLayout; cameraLayout.writes = recipe.camera;
            applyNativeDeferredCameraConstant(cameraLayout, camera, constant.bytes);
            if (!recipe.refraction.empty()) {
                if (!refraction) throw std::runtime_error("Selected HDR program requires the actual refraction owner");
                applyNativeWaterRefractionConstant(recipe.refraction, *refraction, constant.bytes);
            }
            pending.push_back(std::move(pass)); pendingRoutes.push_back({std::move(route), recipeId});
        }
        // All selected resources and CPU packets succeeded before replacing
        // the installed associations. Failed preparation leaves the old set.
        passes_.passes.swap(pending); routes_.swap(pendingRoutes);
        *modules_=moduleCandidate;
        heightFog_=std::move(heightCandidate);
        for (auto& pass : passes_.passes) for (auto& constant : pass.constants)
            context->UpdateSubresource(constant.buffer.Get(), 0, nullptr, constant.bytes.data(), 0, 0);
    }
    NativeSourceHdrPasses& passes() { return passes_; }
    const NativeSourceHdrPasses& passes() const { return passes_; }
    ID3D11RenderTargetView* targetView() const { return target_.Get(); }
    const std::vector<HdrInstalledRoute>& routes() const { return routes_; }
private:
    static int clusterLane(const std::string& role) {
        constexpr std::array<std::string_view,6> roles{"ClusterIndices","ClusterPositions","ClusterAttributes",
            "ClusterExAttributes","ClusterSpotAttributes","ClusterSpotExAttributes"};
        const auto found=std::find(roles.begin(),roles.end(),role);
        return found==roles.end()?-1:int(std::distance(roles.begin(),found));
    }
    static bool sourceNullTexture(const std::string& role, const std::string& key) {
        return key == "00000000" && (role == "Hatching" || role == "Ramp" || role == "IES");
    }
    static HdrProgramRecipe readRecipe(const fs::path& directory) {
        HdrProgramRecipe result;
        NativePreparedInputFile unsupported(directory / "unsupported.txt");
        if (!unsupported) throw std::runtime_error("HDR program recipe capability declaration is absent");
        result.unsupported.assign(std::istreambuf_iterator<char>(unsupported), {});
        if (result.unsupported.find_first_not_of(" \t\r\n") != std::string::npos) return result;
        result.unsupported.clear();
        unsigned hatching{}, fade{};
        NativePreparedInputFile policy(directory / "policy.txt"); policy >> hatching >> fade;
        if (!policy || hatching > 1 || fade > 1) throw std::runtime_error("HDR program live writer policy format");
        result.hasHatching = hatching != 0; result.hasShadowFade = fade != 0;
        std::string stage, file; unsigned slot{};
        NativePreparedInputFile constants(directory / "cbuffers.txt");
        if (!constants) throw std::runtime_error("HDR program constant declarations are absent");
        while (constants >> stage >> slot >> file) {
            if ((stage != "VS" && stage != "PS") || slot >= 14) throw std::runtime_error("HDR program constant stage or slot");
            auto bytes = load(directory / file);
            if (bytes.empty() || bytes.size() > 65536 || bytes.size() % 16 != 0) throw std::runtime_error("HDR program constant extent");
            result.constants.push_back({stage == "VS", slot, file, std::move(bytes), {}});
        }
        if (!constants.eof()) throw std::runtime_error("HDR program constant declarations are truncated");
        result.lighting = Interop::readHdrLightingLayout(directory / "hdr-lighting-layout.bin");
        auto fields = [&](const char* name, auto& writes, const auto& widths) {
            NativePreparedInputFile input(directory / name);
            if (!input) throw std::runtime_error("HDR selected semantic layout is absent");
            typename std::remove_reference_t<decltype(writes)>::value_type write;
            while (input >> write.kind >> write.offset >> write.sourceOffset >> write.bytes) {
                if (write.kind >= widths.size() || std::uint64_t(write.sourceOffset) + write.bytes > widths[write.kind] ||
                    std::uint64_t(write.offset) + write.bytes > result.lighting.constantBytes)
                    throw std::runtime_error("HDR selected semantic layout range");
                writes.push_back(write);
            }
            if (!input.eof()) throw std::runtime_error("HDR selected semantic layout is truncated");
        };
        fields("camera-writes.txt", result.camera, std::array<unsigned, 6>{16, 64, 48, 64, 64, 64});
        fields("refraction-writes.txt", result.refraction, std::array<unsigned, 5>{12, 12, 12, 8, 48});
        fields("height-fog-writes.txt", result.heightFog, std::array<unsigned, 4>{12,48,48,240});
        // Older prepared banks predate the live cluster writer. They cannot
        // contain a ready Cluster role, and do not require an empty new file.
        if(fs::exists(nativePreparedFilePath(directory/"cluster-writes.txt")))
            fields("cluster-writes.txt",result.cluster,std::array<unsigned,4>{16,8,4,8});
        if(fs::exists(nativePreparedFilePath(directory/"material-writes.txt")))
            fields("material-writes.txt",result.materialWrites,std::array<unsigned,4>{16,16,28,4});
        result.textures=Interop::readHdrTextureRecipes(directory / "textures.txt");
        for (const auto& file:Interop::readHdrSamplerFiles(directory / "samplers.txt")) {
            HdrSamplerRecipe sampler;sampler.slot=file.slot;sampler.paired=file.paired;
            const auto image=std::find_if(result.textures.begin(),result.textures.end(),[&](const auto& texture){return texture.slot==file.slot;});
            if(file.paired&&(image==result.textures.end()||image->role!="TranslucentSpecular"))
                throw std::runtime_error("HDR paired sampler has no matching specular image declaration");
            if(image!=result.textures.end()&&image->role=="TranslucentSpecular"&&!file.paired)
                throw std::runtime_error("HDR late specular requires both actual sampler recipes");
            for(unsigned side=0;side<(file.paired?2u:1u);++side) {
                const auto bytes=load(directory/file.files[side]);
                if(bytes.size()!=sizeof(D3D11_SAMPLER_DESC))throw std::runtime_error("HDR sampler descriptor format");
                std::memcpy(&sampler.descriptions[side],bytes.data(),bytes.size());
            }
            if(!file.paired)sampler.descriptions[1]=sampler.descriptions[0];
            result.samplers.push_back(sampler);
        }
        if (fs::exists(nativePreparedFilePath(directory / "material-table.bin"))) result.material = load(directory / "material-table.bin");
        return result;
    }
    NativeSourceHdrPass createPass(const HdrProgramRecipe& recipe, HdrProgramReference program) {
        NativeSourceHdrPass pass;
        if (!vertices_.contains(program.vertex)) {
            ComPtr<ID3D11VertexShader> shader; const auto& bytes = vertexBytes_.at(program.vertex);
            check(device_->CreateVertexShader(bytes.data(), bytes.size(), nullptr, &shader), "HDR bank selected vertex program");
            vertices_.emplace(program.vertex, std::move(shader));
        }
        if (!pixels_.contains(program.pixel)) {
            ComPtr<ID3D11PixelShader> shader; const auto& bytes = pixelBytes_.at(program.pixel);
            check(device_->CreatePixelShader(bytes.data(), bytes.size(), nullptr, &shader), "HDR bank selected pixel program");
            pixels_.emplace(program.pixel, std::move(shader));
        }
        pass.vs = vertices_.at(program.vertex); pass.ps = pixels_.at(program.pixel);
        pass.constants = recipe.constants;
        bool globals = false;
        for (std::size_t i = 0; i < pass.constants.size(); ++i) {
            auto& constant = pass.constants[i];
            constant.buffer = sourceHdrDefaultBuffer(device_.Get(), constant.bytes, D3D11_BIND_CONSTANT_BUFFER);
            if (!constant.vertex && constant.slot == recipe.lighting.slot) {
                if (constant.bytes.size() != recipe.lighting.constantBytes) throw std::runtime_error("HDR selected Globals extent differs");
                pass.lightingConstantIndex = i; globals = true;
            }
        }
        if (!globals) throw std::runtime_error("HDR selected Globals binding is absent");
        if (!recipe.material.empty()) {
            pass.material = sourceHdrDefaultBuffer(device_.Get(), recipe.material, D3D11_BIND_SHADER_RESOURCE);
            D3D11_SHADER_RESOURCE_VIEW_DESC sd{}; sd.Format = DXGI_FORMAT_R32_UINT;
            sd.ViewDimension = D3D11_SRV_DIMENSION_BUFFER; sd.Buffer.NumElements = UINT(recipe.material.size() / 4);
            check(device_->CreateShaderResourceView(pass.material.Get(), &sd, &pass.materialView), "HDR selected material table");
        }
        for (const auto& texture : recipe.textures) {
            ID3D11ShaderResourceView* fixed{}; NativeSourceHdrProducer producer{}; bool dynamic = false;
            const auto& role = texture.role;
            if (role == "AB" || role == "GBufferAB") fixed = passes_.gbuffer[0].Get();
            else if (role == "NS" || role == "GBufferNS") fixed = passes_.gbuffer[1].Get();
            else if (role == "RS" || role == "GBufferRS") fixed = passes_.gbuffer[2].Get();
            else if (role == "RR" || role == "GBufferRR") fixed = passes_.gbuffer[3].Get();
            else if (role == "Ggx" || role == "GGX") fixed = passes_.ggx.Get();
            else if (role == "MaterialTable") fixed = pass.materialView.Get();
            else if (role == "Caustics") {
                if (!caustics_) throw std::runtime_error("Selected HDR Caustics module is absent");
                fixed = caustics_->image();
            }
            else if (role == "D1" || role == "GBufferDepth") { producer = NativeSourceHdrProducer::SampledD1; dynamic = true; }
            else if (role == "AmbientSpecular") { producer = NativeSourceHdrProducer::Ambient; dynamic = true; }
            else if (role == "RefractionDepth") { producer = NativeSourceHdrProducer::Refraction; dynamic = true; }
            else if (role == "TransmittedSpecular") { producer = NativeSourceHdrProducer::Transmitted; dynamic = true; }
            else if (role == "TranslucentSpecular") continue; // Installed from its current physical owner in update.
            else if (clusterLane(role)>=0) continue; // Installed from successful current node71 in update.
            else if (role == "BlurredDepth") { producer = NativeSourceHdrProducer::BlurredDepth; dynamic = true; }
            else if (!sourceNullTexture(role, texture.keys[0]))
                throw std::runtime_error("Selected HDR texture role has no actual owner: " + role);
            if (dynamic) pass.dynamicBindings.push_back({texture.slot, producer, role});
            else if (!fixed && role != "Caustics" && !sourceNullTexture(role, texture.keys[0]))
                throw std::runtime_error("Selected HDR fixed texture owner is absent: " + role);
            pass.fixedPixelViews[texture.slot] = fixed;
        }
        for (const auto& source : recipe.samplers) {
            NativeSourceHdrSampler sampler;sampler.slot=source.slot;sampler.descriptions=source.descriptions;sampler.paired=source.paired;
            sampler.environment=std::any_of(recipe.textures.begin(),recipe.textures.end(),[&](const auto& texture) {
                return texture.role=="AmbientSpecular"&&texture.slot==source.slot;
            });
            if(!sampler.environment) {
                for(unsigned side=0;side<(sampler.paired?2u:1u);++side)
                    check(device_->CreateSamplerState(&sampler.descriptions[side], &sampler.states[side]), "HDR selected resource sampler");
                if(!sampler.paired)sampler.states[1]=sampler.states[0];
            }
            pass.samplers.push_back(std::move(sampler));
        }
        pass.target = target_; pass.viewport = viewport_; pass.count = 3; pass.topology = D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST;
        pass.depthTest = FALSE; pass.cull = D3D11_CULL_NONE; pass.stencilEnable = TRUE;
        pass.stencilCompare = D3D11_COMPARISON_EQUAL; pass.readMask = 0xff;
        D3D11_BLEND_DESC blend{}; blend.RenderTarget[0].RenderTargetWriteMask = D3D11_COLOR_WRITE_ENABLE_ALL;
        check(device_->CreateBlendState(&blend, &pass.blend), "HDR normal disabled blend");
        return pass;
    }
    ComPtr<ID3D11Device> device_;
    fs::path root_;
    std::optional<Interop::ImportedHdrProgramBank> imported_;
    std::optional<FreshHdrCausticsModule> caustics_;
    HdrHeightFogState heightFog_;
    HdrShaderModules* modules_{};
    std::map<std::uint32_t, HdrProgramRecipe> recipes_;
    std::map<std::uint32_t, std::vector<char>> vertexBytes_, pixelBytes_;
    std::map<std::uint32_t, ComPtr<ID3D11VertexShader>> vertices_;
    std::map<std::uint32_t, ComPtr<ID3D11PixelShader>> pixels_;
    NativeSourceHdrPasses passes_;
    std::vector<HdrInstalledRoute> routes_;
    ComPtr<ID3D11RenderTargetView> target_;
    D3D11_VIEWPORT viewport_{};
};
} // namespace San14::Render
