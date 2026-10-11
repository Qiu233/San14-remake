#pragma once
#include "../../Resources/ShaderFeatureSelection.h"
#include <algorithm>
#include <filesystem>
#include <fstream>
#include <optional>
#include <sstream>
#include <stdexcept>
#include <string>
#include <type_traits>

namespace San14::Render {
using ShadowReceiverVariant=std::array<std::uint32_t,24>;
struct ShadowReceiverProgramReference {
    std::uint32_t combination{};
    std::int32_t vertex{},pixel{},recipe{};
};
struct ShadowReceiverProgramSelection {
    std::size_t feature{},variant{};
    ShadowReceiverProgramReference program;
};
class ShadowReceiverProgramBank {
public:
    static ShadowReceiverProgramBank read(const std::filesystem::path& directory) {
        std::ifstream header(directory/"bank.txt");
        std::size_t featureCount{},variantCount{},rowCount{},variantBytes{};std::string extra;
        if(!(header>>featureCount>>variantCount>>rowCount>>variantBytes)||header>>extra||
            !featureCount||!variantCount||variantBytes!=96||
            featureCount>std::numeric_limits<std::size_t>::max()/variantCount||
            rowCount!=featureCount*variantCount)
            throw std::runtime_error("Receiver program bank dimensions");
        ShadowReceiverProgramBank result;
        auto rows=[&](const char* name,auto& values,std::size_t count) {
            using Value=typename std::decay_t<decltype(values)>::value_type;
            std::ifstream input(directory/name,std::ios::binary);values.resize(count);
            input.read(reinterpret_cast<char*>(values.data()),std::streamsize(count*sizeof(Value)));
            if(!input||input.peek()!=std::char_traits<char>::eof())
                throw std::runtime_error("Receiver program bank row extent");
        };
        static_assert(sizeof(ShaderFeatureKey)==48);
        rows("features.bin",result.features_,featureCount);rows("variants.bin",result.variants_,variantCount);
        result.programs_.resize(rowCount);std::vector<bool> seen(rowCount);
        std::ifstream input(directory/"programs.txt");std::string line;
        while(std::getline(input,line)) {
            std::istringstream row(line);std::size_t feature{},variant{};
            if(!(row>>feature)) {if(row.eof())continue;throw std::runtime_error("Malformed receiver program row");}
            ShadowReceiverProgramReference program;
            if(!(row>>variant>>program.combination>>program.vertex>>program.pixel>>program.recipe)||row>>extra||
                feature>=featureCount||variant>=variantCount||program.vertex< -1||program.pixel< -1||program.recipe< -1)
                throw std::runtime_error("Receiver program row format");
            const auto index=feature*variantCount+variant;
            if(seen[index])throw std::runtime_error("Duplicate receiver program row");
            seen[index]=true;result.programs_[index]=program;
        }
        if(std::find(seen.begin(),seen.end(),false)!=seen.end())
            throw std::runtime_error("Receiver program bank is incomplete");
        return result;
    }
    ShadowReceiverVariant initialVariant() const{return variants_.front();}
    const std::vector<ShadowReceiverProgramReference>& programs() const{return programs_;}
    ShadowReceiverProgramSelection select(const ShaderFeatureKey& feature,
        ShadowReceiverVariant& requested,ShadowReceiverVariant& committed) const {
        auto variant=std::find(variants_.begin(),variants_.end(),requested);
        if(variant==variants_.end()) {
            requested=committed;variant=std::find(variants_.begin(),variants_.end(),committed);
            if(variant==variants_.end())throw std::runtime_error("Receiver committed variant is outside its imported bank");
        }
        const auto featureIndex=ShaderFeatureSelection::select(feature,features_);
        const auto variantIndex=std::size_t(variant-variants_.begin());
        committed=requested;
        return {featureIndex,variantIndex,programs_[featureIndex*variants_.size()+variantIndex]};
    }
private:
    std::vector<ShaderFeatureKey> features_;
    std::vector<ShadowReceiverVariant> variants_;
    std::vector<ShadowReceiverProgramReference> programs_;
};
struct ShadowReceiverShaderState {
    ShadowReceiverVariant requested{},committed{};
    explicit ShadowReceiverShaderState(const ShadowReceiverProgramBank& bank):
        requested(bank.initialVariant()),committed(requested){}
    void installRecordBias(float scale){requested[19]=scale>0?0u:1u;}
    void finishRecord(){requested[19]=0;}
};
} // namespace San14::Render
