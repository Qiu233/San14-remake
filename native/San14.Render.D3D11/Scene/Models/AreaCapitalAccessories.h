#pragma once
// Category2 has its own Area roster/player identities, sharing only the
// completed resources and source pose recipe with City/Gate accessories.
inline source_accessory_population::Profile readNativeAreaCapitalAccessories(const std::vector<char>& bytes) {
    std::istringstream input(std::string(bytes.begin(),bytes.end()),std::ios::binary);
    const auto units=primaryRead<unsigned>(input),placements=primaryRead<unsigned>(input);
    if(units!=282||placements!=282)throw std::runtime_error("Current Area accessory roster differs");
    source_accessory_population::Profile out;out.units.resize(units);out.placements.resize(placements);
    for(auto& u:out.units){u.index=primaryRead<unsigned>(input);u.force=primaryRead<int>(input);
        u.requested=primaryRead<unsigned>(input);u.position=primaryRead<std::array<float,3>>(input);}
    for(auto& p:out.placements){p.unitIndex=primaryRead<unsigned>(input);p.ordinal=primaryRead<unsigned>(input);
        p.root=primaryRead<std::array<float,12>>(input);p.obb=primaryRead<std::array<float,20>>(input);}
    for(unsigned i=0;i<units;++i)if(out.units[i].index!=i||out.units[i].requested!=1||out.placements[i].unitIndex!=i||out.placements[i].ordinal!=0)
        throw std::runtime_error("Current Area accessory packed identities differ");
    if(input.peek()!=std::char_traits<char>::eof())throw std::runtime_error("Current Area accessory packet has trailing bytes");
    return out;
}
inline NativeCityAccessoryGpu prepareNativeAreaCapitalAccessories(const NativeCityAccessoryGpu& cities,
        source_accessory_population::Profile population) {
    NativeCityAccessoryGpu out;out.population=std::move(population);
    out.resources=cities.resources;out.materials=cities.materials;
    return out;
}
