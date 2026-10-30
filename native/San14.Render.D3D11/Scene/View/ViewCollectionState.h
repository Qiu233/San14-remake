#pragma once
#include "../../Runtime/PreparedFileInput.h"
#include "../Models/CityModelAdmission.h"
#include <array>
#include <filesystem>
#include <fstream>
#include <stdexcept>
#include <string>

namespace San14::Render {
struct ModelCollectionMasks {std::uint32_t first{},second{},shadow{};};
inline ModelCollectionMasks readModelCollectionMasks(const std::filesystem::path& path) {
    NativePreparedInputFile input(path);ModelCollectionMasks result;std::string extra;
    if(!(input>>result.first>>result.second>>result.shadow)||(input>>extra))
        throw std::runtime_error("Model collection task masks absent or malformed");
    return result;
}
struct PreparedModelCollectionCallers {NativeModelCollectionCaller cities,field;};
// This renderer owns one normal Main View. Registration and prepared camera
// admission precede World collection; published success follows the View commit.
class ViewCollectionState {
public:
    ViewCollectionState(ModelCollectionMasks cities,ModelCollectionMasks field):cities_(cities),field_(field) {}
    void registerView(std::uint32_t slot) {
        if(slot!=0)throw std::runtime_error("Model collection supports the normal Main View only");
        registered_|=1u<<slot;
    }
    PreparedModelCollectionCallers prepare(bool commonPresent,std::uint32_t commonFlags) const {
        const auto success=successful_|registered_;
        auto caller=[&](const ModelCollectionMasks& masks) {
            // This finite normal owner has no external inherited-mask writers.
            // Its constructor/reset inherited masks are genuinely zero.
            return NativeModelCollectionCaller{registered_,success,masks.first,masks.second,masks.shadow,commonPresent,commonFlags};
        };
        return {caller(cities_),caller(field_)};
    }
    void commit(const PreparedModelCollectionCallers& prepared) noexcept {successful_=prepared.cities.successfulViews;}
    std::uint32_t registeredViews() const {return registered_;}
    std::uint32_t successfulViews() const {return successful_;}
private:
    ModelCollectionMasks cities_,field_;
    std::uint32_t registered_{},successful_{};
};
} // namespace San14::Render
