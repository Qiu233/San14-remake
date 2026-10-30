using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //429F80→2A20C0 is automatic NPC City expansion, not unrest.
    internal GameState ApplyNormalTurnMonthlyCityExpansionCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
    {
        if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw CityExpansionMissing("shared_pool");
        var cities = current.CreateTemporaryHexList() ?? throw CityExpansionMissing("temporary");
        try
        {
            //2E02B0 collects the entire source list before any City is changed.
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.City) ?? throw CityExpansionMissing("City_registry"))
            {
                if (entry is not { Kind: RegistryKind.City, PhysicalSlot: int physical }) throw CityExpansionMissing("City_receiver");
                var city = new BaseReference(BaseKind.City, physical);
                if (!CanExpand(city)) continue;
                var node = current.AllocateRegistryNode(new(cities.Identity)) ?? throw CityExpansionMissing("temporary_node");
                if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, new(RegistryKind.City, physical));
            }
            for (var node = current.ReadHexListHead(cities) ?? throw CityExpansionMissing("temporary_head"); node.Identity.HasValue;
                node = current.ReadHexListNodeNext(node) ?? throw CityExpansionMissing("temporary_next"))
            {
                var payload = current.ReadRegistryNodePayload(new(node.Identity)) ?? throw CityExpansionMissing("temporary_payload");
                if (payload.Kind != RegistryKind.City || payload.PhysicalSlot is not int physical) throw CityExpansionMissing("temporary_City");
                var city = new BaseReference(BaseKind.City, physical);
                int before = K(current.ReadCitySizeB8(city), "CityB8");
                if (before is < 1 or > 3) continue;
                int after = Math.Min(unchecked(before + 1), 4);
                current.SetCitySizeB8(physical, (byte)after);
                var owner = state.Cities.GetValueOrDefault(physical) ?? throw CityExpansionMissing("City_owner");
                state = state with { Cities = new(state.Cities) { [physical] = owner with { Size = (CitySize)(after - 1) } } };
                var force = Force(city);
                if (K(current.ReadForceNativeValidity(force), "expansion_Force_validity"))
                {
                    int id = K(current.ReadForcePhysicalSlot(force), "expansion_Force60");
                    if (id != 0 && id is not (>= 46 and <= 51))
                        state = RulerCaptureReleaseSource.ApplyCurrentPopularityAdjustment(state, current, id,
                            Rule(after switch { 2 => "middle-popularity", 3 => "large-popularity", _ => "giant-popularity" }));
                }
                ushort location = K(current.ReadBaseObjectLink(city), "history_City4E");
                string sizeName = ReadHistoryDataText(1, 0x46B + after);
                var text = FormatNormalTurnReport(state, current, 0x5E87, new(Base0: location, String0: sizeName));
                state = text.State;
                int governor = K(current.ReadArmyReturnBaseGovernor2E(city), "history_governor2E");
                if (governor is < 1 or > 6000) governor = 5012;
                int district = K(current.ReadBaseDistrictSlot(city), "history_District");
                int hex = current.ReadBaseCurrentHexIndex(city);
                int day = (ReadArmyDestructionSampler(state, current) ?? throw CityExpansionMissing("history_World450")).Day450;
                var effects = new List<CampaignEffectRequest>();
                state = WriteHistory(state, current, new(0, district, hex, day, text.Text, text.Text, governor, MarkBit0: 1), effects);
                if (effects.Count != 0) state = state with { CampaignEffectRequests =
                    (state.CampaignEffectRequests ?? throw CityExpansionMissing("pending_owner")).Append(CampaignEffectSource.MonthlyMaintenance,
                        state.NativeClock?.UpdateOrdinal ?? throw CityExpansionMissing("revision"), effects) };
            }
            return state;
        }
        finally { current.ClearTemporaryHexList(cities); current.ReleaseTemporaryHexList(cities); }

        bool CanExpand(BaseReference city)
        {
            var force = Force(city);
            if (!K(current.ReadForceNativeValidity(force), "candidate_Force_validity")) return false;
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw CityExpansionMissing("player_Force");
            if (K(current.ReadForceNativeValidity(player), "player_validity") && player == force) return false;
            int size = K(current.ReadCitySizeB8(city), "candidate_SizeB8");
            if (size is < 1 or > 3) return false;
            //229FD0 follows the City's actual map Hex, then its Area12.
            var area = CurrentGateMapQueries.FindArea(city, current);
            bool valid = K(current.ReadAreaNativeValidity(area), "center_Area_validity");
            var owner = valid ? state.Areas.GetValueOrDefault(K(area.PhysicalSlot, "center_Area_physical")) ?? throw CityExpansionMissing("center_Area_owner") : null;
            if (valid && K(owner!.Security, "center_Area54") <= 89) return false;
            int levels = 0;
            if (K(current.ReadAreaNativeValidity(area), "center_Area_validity_again"))
            {
                var development = owner?.Development ?? throw CityExpansionMissing("center_development");
                levels = unchecked(CeAreaIncomeMath.Level(Word(development.Commerce, "center_3C")) +
                    CeAreaIncomeMath.Level(Word(development.Agriculture, "center_3E")) + CeAreaIncomeMath.Level(Word(development.Barracks, "center_40")));
            }
            return Rule(size switch { 1 => "middle-levels", 2 => "large-levels", _ => "giant-levels" }) <= levels;
        }
        ForceReference Force(BaseReference city)
        {
            int raw = K(current.ReadBaseForceSlot(city), "City_Force");
            return current.ReadForceTableReference(raw < 52 ? raw : 0) ?? throw CityExpansionMissing("City_Force_pointer");
        }
        int Rule(string name)
        {
            string key = "simulation.normal-turn.city-expansion." + name;
            var bank = state.NativeArmySimulation?.Parameters ?? throw CityExpansionMissing("parameters");
            if (bank.Float32Values.ContainsKey(key)) throw CityExpansionMissing("integer_parameter_" + name);
            if (bank.Int32Values.TryGetValue(key, out int saved)) return saved;
            if (Seed.Language == "SC")
            {
                //SC279CD0 and2A0DF0 independently read these six Int32 values.
                //The level and popularity data are1A4BBAC/BBB8/BBC0 and1A4A838/A844/A850.
                return name switch { "middle-levels" => 6, "large-levels" => 9, "giant-levels" => 12,
                    "middle-popularity" => 3, "large-popularity" => 10, "giant-popularity" => 20, _ => throw CityExpansionMissing("parameter_" + name) };
            }
            if (Seed.Language != "TC") throw CityExpansionMissing("locale_parameter_" + name);
            //TC immutable rule data1A4AB7C/88/90 and1A49808/14/20.
            return name switch { "middle-levels" => 6, "large-levels" => 9, "giant-levels" => 12,
                "middle-popularity" => 3, "large-popularity" => 10, "giant-popularity" => 20, _ => throw CityExpansionMissing("parameter_" + name) };
        }
        static T K<T>(T? value, string name) where T : struct => value ?? throw CityExpansionMissing(name);
        static ushort Word(int? value, string name) => value is >= 0 and <= 65535 ? (ushort)value : throw CityExpansionMissing(name);
    }
    private static InvalidDataException CityExpansionMissing(string name) => new("normal-turn.city-expansion." + name);
}
