#pragma once
#include <istream>
#include <sstream>
#include <stdexcept>
#include <string>
#include <utility>
#include <vector>

namespace San14::Render {
// The route exporter retains all six RDEF resource columns. Dimension belongs
// to the declaration, before the actual provider name; it is not a provider.
struct RouteProgramResourceSlot {
    unsigned program{},kind{},slot{},count{},dimension{};
    std::string name;
};
inline std::vector<RouteProgramResourceSlot> readRouteProgramResourceSlots(std::istream& input) {
    std::vector<RouteProgramResourceSlot> result;
    std::string line;
    while(std::getline(input,line)) {
        if(line.find_first_not_of(" \t\r")==std::string::npos)continue;
        RouteProgramResourceSlot row;std::string extra;std::istringstream fields(line);
        if(!(fields>>row.program>>row.kind>>row.slot>>row.count>>row.dimension>>row.name)||fields>>extra)
            throw std::runtime_error("Route source resource row is incomplete or has extra columns");
        // RDEF sampler declarations have dimension0; the actual route image
        // and same-frame exposure are Texture2D (D3D_SRV_DIMENSION_TEXTURE2D=4).
        if(row.count!=1||row.slot>=16||(row.kind!=2&&row.kind!=3)||
            row.dimension!=(row.kind==2?4u:0u))
            throw std::runtime_error("Route source resource binding format");
        result.push_back(std::move(row));
    }
    if(!input.eof())throw std::runtime_error("Route source resource row is incomplete");
    return result;
}
} // namespace San14::Render
