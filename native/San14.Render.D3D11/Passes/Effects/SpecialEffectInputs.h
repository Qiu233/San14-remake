#pragma once
#include <cstdint>
#include <cstring>
#include <span>
#include <optional>
#include <stdexcept>

struct SpecialEffectInputs {
    std::uint32_t commonFlags{},selectorMask{};
    std::optional<std::uint32_t> selectedLightTag;
    bool admitted() const {
        return (commonFlags&4u)&&selectedLightTag&&*selectedLightTag!=0&&
            static_cast<std::int8_t>(*selectedLightTag)>=0;
    }
};
inline SpecialEffectInputs decodeSpecialEffectInputs(std::span<const std::uint8_t> bytes) {
    if(bytes.size()!=20)throw std::runtime_error("Special Effect SPE1 packet size");
    std::uint32_t words[5];std::memcpy(words,bytes.data(),sizeof(words));
    if(words[0]!=0x31455053u||words[3]>1||(!words[3]&&words[4])||
       (words[3]&&!(words[2]&words[4])))throw std::runtime_error("Special Effect SPE1 admission inputs");
    SpecialEffectInputs out{words[1],words[2],std::nullopt};
    if(words[3])out.selectedLightTag=words[4];return out;
}
