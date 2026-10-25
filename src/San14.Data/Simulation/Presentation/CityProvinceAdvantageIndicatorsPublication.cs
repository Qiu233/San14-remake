using San14.Core;

namespace San14.Data;

/// <summary>The two cached province-advantage attention predicates of a City
/// map label. Unknown rows retain the independently resolved predicate.</summary>
public sealed record CityProvinceAdvantageIndicators(int CityId, bool IsPlayerCity,
    bool? GainOpportunity, bool? LossWarning, string? UnresolvedOwner);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>1A3E60 refreshes these two City predicates, including after
    /// coalition activation/dissolution. It does not refresh diplomacy cards.
    /// Capture once when accepting the refresh, rather than on every frame.</summary>
    public AreaAdministrationQuery<IReadOnlyList<CityProvinceAdvantageIndicators>>
        QueryCityProvinceAdvantageIndicators(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            var current = OwnedArmyReturnCurrentFields.CaptureCurrent(state, _definitions);
            var lifecycle = state.ReadArmyLifecycle() ?? throw ProvinceIndicatorMissing("saved_indexes");
            _ = WorldIndexContext.ImportOwnedState(current, lifecycle.Indexes, state);
            var reader = new ProvinceIndicatorReader(this, state, current);
            var result = state.Cities.Keys.Order().Select(reader.Read).ToArray();
            return AreaAdministrationQuery<IReadOnlyList<CityProvinceAdvantageIndicators>>.Known(result);
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<IReadOnlyList<CityProvinceAdvantageIndicators>>.Unknown(error.Message); }
    }

    private sealed class ProvinceIndicatorReader(ArmyCampaignRuleAdapter owner,
        GameState state, SourceWorldContext current)
    {
        private readonly Dictionary<BaseReference, BaseReference[]> neighborhoods = [];

        internal CityProvinceAdvantageIndicators Read(int cityId)
        {
            bool isPlayer = false;
            bool? gain = null, loss = null;
            var issues = new List<string>();
            try
            {
                var city = City(cityId);
                try { isPlayer = PlayerOwns(city); }
                catch (InvalidDataException error) { issues.Add(error.Message); }
                try { gain = Gain(city); }
                catch (InvalidDataException error) { issues.Add(error.Message); }
                try { loss = Loss(city); }
                catch (InvalidDataException error) { issues.Add(error.Message); }
            }
            catch (InvalidDataException error) { issues.Add(error.Message); }
            return new(cityId, isPlayer, gain, loss,
                issues.Count == 0 ? null : string.Join("; ", issues.Distinct()));
        }

        //2C5E10: acquiring this foreign City would reach the province's
        //advantage threshold, and the source's accessible-frontier test holds.
        private bool Gain(BaseReference city)
        {
            if (Mode() is 2 or 4) return false;
            var player = Player();
            if (!Valid(city) || !Valid(player) || PlayerOwns(city)) return false;
            int playerId = Physical(player);
            int otherCities = 0;
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Force)
                ?? throw Missing("Force_registryC0"))
            {
                if (entry is not { Kind: RegistryKind.Force, PhysicalSlot: int forceId })
                    throw Missing("Force_registry_payload");
                //No additional Force validity gate is present in this loop.
                if (forceId == 51 || forceId is >= 46 and <= 50 || forceId == playerId) continue;
                otherCities = unchecked(otherCities + Need(PersonArmyCaptureSource.ReadOwnedCityCount(
                    current, new(forceId)), "other_Force_City_count22AB60"));
            }
            if (otherCities == 1) return false;
            int province = Province(city);
            int count = ProvinceCities(player, province);
            int requirement = Requirement(province);
            //2C6820 includes the Province/Advantage pointer validity gates.
            if (province != 0 && Advantage(province) != 0 && count >= requirement) return false;
            return count == requirement - 1 && HasExposedNeighbor(city);
        }

        //2C7140: losing this player City would drop its province below the
        //advantage threshold. The player's final City is explicitly excluded.
        private bool Loss(BaseReference city)
        {
            if (Mode() is 2 or 4) return false;
            if (!Valid(Player()) || !Valid(city)) return false;
            var force = Force(city);
            if (!Valid(force) || !PlayerForce(force)) return false;
            if (Need(PersonArmyCaptureSource.ReadOwnedCityCount(current, force), "own_City_count22AB60") == 1)
                return false;
            int province = Province(city);
            return ProvinceCities(force, province) == Requirement(province) && HasExposedNeighbor(city);
        }

        //2C7B70 is not an unrestricted flood fill. First inspect adjacent
        //Gates; accepted Gates suppress their immediate and second-Gate
        //neighborhoods. Only the remaining original neighbors are then tested.
        private bool HasExposedNeighbor(BaseReference origin)
        {
            BaseReference[] adjacent = Neighbors(origin);
            var suppressed = new HashSet<BaseReference> { origin };
            foreach (var first in adjacent)
            {
                if (Need(current.ReadBaseObjectLink(first), "neighbor_Base4E") is not (>= 52 and <= 61)) continue;
                if (Exposed(first, origin)) return true;
                suppressed.Add(first);
                foreach (var second in Neighbors(first))
                {
                    if (!suppressed.Add(second)) continue;
                    if (Need(current.ReadBaseObjectLink(second), "second_Base4E") is not (>= 52 and <= 61)) continue;
                    foreach (var third in Neighbors(second)) suppressed.Add(third);
                }
            }
            foreach (var neighbor in adjacent)
                if (!suppressed.Contains(neighbor) && Exposed(neighbor, origin)) return true;
            return false;
        }

        private bool Exposed(BaseReference neighbor, BaseReference origin)
        {
            var other = Force(neighbor);
            if (!Valid(other)) return false;
            bool own = PlayerOwns(origin), otherOwn = PlayerForce(other);
            if (!own) return otherOwn;
            if (otherOwn) return false;
            if (BandForce(other)) return false;
            return !Need(ArmyMarchDevelopmentSource.ReadFriendlyRelation(state, other, Force(origin)),
                "protected_pair2C3CA0");
        }

        //2329E0 reads the ruler lookup and first location before testing the
        //Force ordinal. Keep its narrower actual predicate, not IsBand flags.
        private bool BandForce(ForceReference force)
        {
            int physical = Physical(force);
            ushort rulerId = current.ReadForceRecord(physical)?.RawRuler10 ?? throw Missing("band_Force_ruler10");
            var ruler = (rulerId <= 6000 ? current.ReadPersonLookupReference(rulerId)
                : current.ReadTransferPersonTableReference(0)) ?? throw Missing("band_ruler_lookup");
            if (ruler.PhysicalSlot is not int person) return false;
            ushort home = Need(current.ReadPersonFirstLocation(person), "band_ruler_E2");
            return home is >= 1 and <= 61 && physical is >= 46 and <= 50;
        }

        private BaseReference[] Neighbors(BaseReference at)
        {
            if (neighborhoods.TryGetValue(at, out var saved)) return saved;
            HexListHandle list = current.CreateTemporaryHexList() ?? throw Missing("neighbors_temporary");
            try
            {
                (owner._captiveFreedomHomes ??= new(owner._scenario)).FillBases22F720(current, at, list);
                var result = new List<BaseReference>();
                var node = current.ReadRegistryHead(new(list.Identity)) ?? throw Missing("neighbors_head");
                while (node.Identity.HasValue)
                {
                    var payload = current.ReadRegistryNodePayload(node) ?? throw Missing("neighbors_payload");
                    result.Add(payload.Kind switch
                    {
                        RegistryKind.City => new(BaseKind.City, payload.PhysicalSlot),
                        RegistryKind.Gate => new(BaseKind.Gate, payload.PhysicalSlot),
                        _ => throw Missing("neighbors_payload_kind")
                    });
                    node = current.ReadRegistryNodeNext(node) ?? throw Missing("neighbors_next");
                }
                return neighborhoods[at] = result.ToArray();
            }
            finally { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
        }

        private int ProvinceCities(ForceReference force, int province)
        {
            int count = 0, forceId = Physical(force);
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.City)
                ?? throw Missing("City_registry90"))
            {
                if (entry is not { Kind: RegistryKind.City, PhysicalSlot: int id }) throw Missing("City_registry_payload");
                var city = new BaseReference(BaseKind.City, id);
                if (Valid(city) && Province(city) == province && Physical(Force(city)) == forceId) count++;
            }
            return count;
        }

        private int Province(BaseReference city)
        {
            int id = Need(city.PhysicalSlot, "City_physical");
            int raw = owner._growthQueries.ReadCityState(state, id) ?? throw Missing("City_provinceCE");
            return Need(current.ReadStaticTableReference(0xDC80, raw < 23 ? raw : 0)?.PhysicalSlot, "Province_pointer");
        }
        private int Advantage(int province)
        {
            int raw = owner._growthQueries.ReadStateAdvantage(state, province) ?? throw Missing("Province_advantage30");
            return Need(current.ReadStaticTableReference(0x85990, raw < 18 ? raw : 0)?.PhysicalSlot, "Advantage_pointer");
        }
        private int Requirement(int province) => owner._growthQueries.ReadAdvantageRequirement(state, Advantage(province))
            ?? throw Missing("Advantage_requiredCitiesB3");
        private int Mode() => state.NativeArmySimulation?.Parameters is { } parameters &&
            !parameters.Float32Values.ContainsKey(PersonGrowthCurrentQueries.WorldMode40Key) &&
            parameters.Int32Values.TryGetValue(PersonGrowthCurrentQueries.WorldMode40Key, out int mode)
                ? mode : throw Missing("World_mode40");
        private BaseReference City(int id) => current.ReadCityTableReference(id) ?? throw Missing("City_pointer");
        private ForceReference Player() => OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw Missing("current_player_Force3A");
        private ForceReference Force(BaseReference at)
        {
            int raw = Need(current.ReadBaseForceSlot(at), "Base_ForceB0");
            return current.ReadForceTableReference(raw is >= 0 and < 52 ? raw : 0) ?? throw Missing("Force_pointer");
        }
        private bool PlayerOwns(BaseReference at) => Valid(Player()) && Force(at) == Player();
        private bool PlayerForce(ForceReference force)
        {
            int player = Physical(Player());
            return player != 0 && Physical(force) == player;
        }
        private bool Valid(ForceReference force) => Need(current.ReadForceNativeValidity(force), "Force_validity");
        private bool Valid(BaseReference at) => Need(current.ReadBaseNativeValidity(at), "Base_validity");
        private int Physical(ForceReference force) => Need(current.ReadForcePhysicalSlot(force), "Force_ordinal60");
        private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => ProvinceIndicatorMissing(field);
    }

    private static InvalidDataException ProvinceIndicatorMissing(string field) =>
        new("city.province-advantage-indicators." + field);
}
