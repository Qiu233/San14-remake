#pragma once
// Retained static scene resources. Uses the existing renderer's check/load/buffer
// helpers, just as ModelRenderer/ModelFrame; no additional ambient dependency.
#include "ModelRenderer.h"
#include "ModelFrame.h"
#include "ModelView.h"
#include <map>

namespace San14::Render {
class StaticModelResources {
  public:
    struct Submission {
        NativeModelInputs models;
        NativeModelContinuousPlan plan;
    };

    explicit StaticModelResources(const fs::path &source)
        : source_(source), catalog_(prepareNativeModelContinuous(source)) {}
    StaticModelResources(const StaticModelResources &) = delete;
    StaticModelResources &operator=(const StaticModelResources &) = delete;

    // Imported CPU catalogue exists before GPU allocation. The two allocation
    // stages keep the intervening joint-shadow connection in its caller order.
    void prepareGeometry(ID3D11Device *device) {
        geometry_ = prepareNativeModelContinuousGpu(device, catalog_, source_);
    }
    void prepareStencil(ID3D11Device *device, const fs::path &viewSource,
                        const source_view::Frame &initialView, unsigned contextFlags) {
        stencil_ =
            createStencilState(device, viewSource, catalog_, geometry_, initialView, contextFlags);
    }

    template <class AcceptedCameraFrame, class CurrentFieldDisplay>
    void selectCamera(const AcceptedCameraFrame &frame, const CurrentFieldDisplay &display) {
        candidate_ = selectNativeModelContinuousFromCameraFrame(catalog_, frame);
        display.selectOrdinary(candidate_, catalog_);
    }

    // Upload each selected primitive's constants, then filter its ordinary54
    // stencil. This request never loads files or creates device objects.
    const Submission &updateSubmission(ID3D11DeviceContext *context,
                                       const source_view::Frame &currentView) {
        submission_.models = updateSelectNativeModelContinuous(context, candidate_, geometry_);
        submission_.plan = filterSubmission(stencil_, currentView, candidate_, submission_.models);
        return submission_;
    }

    const NativeModelContinuous &catalog() const { return catalog_; }
    const NativeModelInputs &geometry() const { return geometry_; }
    bool hasGeometry() const { return !geometry_.draws.empty(); }

  private:
    struct StencilState {
        source_view::Profile profile;
        std::vector<source_view::Primitive> primitives;
        std::vector<unsigned> records;
        std::vector<ComPtr<ID3D11DepthStencilState>> states;
        unsigned contextFlags{};
    };
    static StencilState createStencilState(ID3D11Device *device, const fs::path &source,
                                           const NativeModelContinuous &catalog,
                                           const NativeModelInputs &allModels,
                                           const source_view::Frame &initialView,
                                           unsigned contextFlags) {
        StencilState result;
        result.profile = source_view::loadProfile(source / "profile.bin");
        result.primitives = source_view::loadPrimitives(source / "model-stencil-catalog.bin");
        result.contextFlags = contextFlags;
        if (result.primitives.size() != catalog.primitives.size() ||
            allModels.draws.size() != catalog.primitives.size())
            throw std::runtime_error("Ordinary54 complete primitive catalogue differs");
        std::map<std::array<unsigned, sizeof(D3D11_DEPTH_STENCIL_DESC) / sizeof(unsigned)>,
                 ComPtr<ID3D11DepthStencilState>>
            shared;
        for (size_t i = 0; i < result.primitives.size(); ++i) {
            const auto &primitive = result.primitives[i];
            const auto &model = catalog.primitives[i];
            if (primitive.instanceIndex != model.instanceIndex ||
                primitive.drawIndex != model.drawIndex || !primitive.allowedRecordBits ||
                (primitive.allowedRecordBits & (primitive.allowedRecordBits - 1)))
                throw std::runtime_error("Imported ordinary54 class18 record identity differs");
            unsigned record = 0;
            while ((primitive.allowedRecordBits & (1u << record)) == 0)
                ++record;
            auto stencil = source_view::ordinary54(result.profile, initialView, primitive, record,
                                                   contextFlags);
            if (!stencil.submit)
                throw std::runtime_error("Initial normal class18 source record absent");
            D3D11_DEPTH_STENCIL_DESC description{};
            allModels.draws[i].depth->GetDesc(&description);
            description.StencilEnable = stencil.enabled;
            description.StencilReadMask = BYTE(stencil.readMask);
            description.StencilWriteMask = BYTE(stencil.writeMask);
            description.FrontFace.StencilFunc = D3D11_COMPARISON_FUNC(stencil.compare);
            description.FrontFace.StencilFailOp = D3D11_STENCIL_OP(stencil.fail);
            description.FrontFace.StencilDepthFailOp = D3D11_STENCIL_OP(stencil.depthFail);
            description.FrontFace.StencilPassOp = D3D11_STENCIL_OP(stencil.pass);
            description.BackFace = description.FrontFace;
            std::array<unsigned, sizeof(description) / sizeof(unsigned)> key{};
            std::memcpy(key.data(), &description, sizeof(description));
            auto &state = shared[key];
            if (!state)
                check(device->CreateDepthStencilState(&description, &state),
                      "Current source ordinary54 stencil");
            result.records.push_back(record);
            result.states.push_back(state);
        }
        return result;
    }
    static NativeModelContinuousPlan filterSubmission(const StencilState &owner,
                                                      const source_view::Frame &view,
                                                      const NativeModelContinuousPlan &plan,
                                                      NativeModelInputs &selected) {
        NativeModelContinuousPlan submittedPlan;
        submittedPlan.visibleInstances = plan.visibleInstances;
        submittedPlan.visibleUnsupportedInstances = plan.visibleUnsupportedInstances;
        submittedPlan.draws.reserve(plan.draws.size());
        std::vector<NativeModelDraw> submitted;
        submitted.reserve(selected.draws.size());
        for (size_t i = 0; i < plan.draws.size(); ++i) {
            unsigned index = plan.draws[i].primitiveIndex;
            auto stencil = source_view::ordinary54(owner.profile, view, owner.primitives[index],
                                                   owner.records[index], owner.contextFlags);
            if (!stencil.submit)
                continue;
            auto draw = std::move(selected.draws[i]);
            draw.depth = owner.states[index];
            draw.stencilReference = stencil.reference;
            submitted.push_back(std::move(draw));
            submittedPlan.draws.push_back(plan.draws[i]);
        }
        selected.draws = std::move(submitted);
        return submittedPlan; // Preserve source sort keys after actual record filtering.
    }
    fs::path source_;
    NativeModelContinuous catalog_;
    NativeModelInputs geometry_;
    StencilState stencil_;
    NativeModelContinuousPlan candidate_;
    Submission submission_;
};
} // namespace San14::Render
