#pragma once
#include "HdrLivePrograms.h"

namespace San14::Render {
struct DeferredLightingFrame {
    const NativeDeferredCameraFields& camera;
    const PreparedMapView& view;
    const MapViewDefinition& definition;
    const MapViewCameraSettings& cameraSettings;
    const source_current_season::Frame* season;
    std::uint32_t phaseFlags{};
    float commonClock{};
    const source_water_camera::RefractionDerived* refraction{};
    NativeSourceHdrSpecularImages specular;
    const NativeSourceClusterLights* cluster{};
};

// Each execution entry owns a complete imported resource bank. Its current
// associations and program buffers follow the same View and current phase.
class DeferredLightingPass {
public:
    DeferredLightingPass() = default;
    DeferredLightingPass(const DeferredLightingPass&) = delete;
    DeferredLightingPass& operator=(const DeferredLightingPass&) = delete;
    void prepare(ID3D11Device* device, const fs::path& source, unsigned node,
        ID3D11Texture2D* hdr, const std::array<ComPtr<ID3D11Texture2D>, 5>& gbuffer,
        const std::array<DXGI_FORMAT, 6>& formats, ID3D11Texture2D* ggx,HdrShaderModuleBank& modules) {
        live_.prepare(device, source, node, hdr, gbuffer, formats, ggx,modules);
    }
    void update(ID3D11DeviceContext* context, const DeferredLightingFrame& frame) {
        if (!frame.season) throw std::runtime_error("Live HDR selection requires current typed lights and Camera settings");
        live_.update(context, frame.view, frame.definition, frame.cameraSettings, frame.camera,
            *frame.season, frame.phaseFlags, frame.commonClock, frame.refraction, frame.specular, frame.cluster);
    }
    void execute(ID3D11Device* device, ID3D11DeviceContext* context, NativeSourceDepthState& logical,
        ID3D11ShaderResourceView* sampledD1, ID3D11DepthStencilView* dsv,
        const EnvironmentImage& ambient, ID3D11ShaderResourceView* refraction,
        ID3D11ShaderResourceView* transmitted,
        ID3D11ShaderResourceView* blurredDepth, float currentCommonClock,
        uint32_t view19AC, uint32_t view19B0) {
        executeSourceHdrPasses(device, context, logical, live_.passes(), sampledD1, dsv,
            ambient, refraction, transmitted, blurredDepth, currentCommonClock, view19AC, view19B0);
    }
    const NativeSourceHdrPasses& bank() const { return live_.passes(); }
    ID3D11RenderTargetView* targetView() const { return live_.targetView(); }
    const std::vector<HdrInstalledRoute>& routes() const { return live_.routes(); }
private:
    HdrLivePrograms live_;
};
} // namespace San14::Render
