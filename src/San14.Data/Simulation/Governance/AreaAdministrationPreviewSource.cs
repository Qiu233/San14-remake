using San14.Core;

namespace San14.Data;

/// <summary>Finite lazy inputs over the same UI projection. Algorithms and saved
/// facts remain in their existing owners; unavailable reached inputs stay null.</summary>
internal sealed class AreaAdministrationPreviewSource(GameState state, SourceWorldContext current,
    LoadedScenario scenario, string language, OwnedBaseCapacityInputs panels, bool capacityInputsBound) : IPersonLinkedEntityLookupState
{
    private TacticRemakeCurrentAbilities? _abilities;

    internal AreaAdministrationQuery<int?> Gold(int area, Func<int, int> count) => Read(() =>
    {
        byte citySlot = K(current.ReadAreaCitySlot(new(area)), "gold_area_city");
        var city = current.ReadCityTableReference(citySlot <= 51 ? citySlot : 0) ?? throw Missing("gold_city_pointer");
        if (!K(current.ReadBaseNativeValidity(city), "gold_city_validity")) return 0;
        int cityForce = K(current.ReadBaseForceSlot(city), "gold_city_force");
        int hex = K(current.ReadAreaHexIndex(new(area)), "gold_area_hex");
        var hexPointer = current.ReadHexTableReference(hex < 48400 ? hex : 0) ?? throw Missing("gold_hex_pointer");
        int rawForce = K(current.ReadHexForceSlot(hexPointer), "gold_hex_force");
        if (cityForce != rawForce) return 0;
        if (cityForce < 0) throw Missing("gold_city_force_domain");
        var force = current.ReadForceTableReference(cityForce <= 51 ? cityForce : 0) ?? throw Missing("gold_force_pointer");
        int forceId = K(current.ReadForcePhysicalSlot(force), "gold_force_identity");
        if (forceId is >= 46 and <= 50)
        {
            if (!capacityInputsBound) throw Missing("gold_capacity_inputs");
            int capacity = BasePostLoad.ReadGoldCapacity(city, current);
            return unchecked(capacity - K(current.ReadBaseResource(city, BaseResourceKind.Gold), "gold_city_stock"));
        }
        ushort rawPerson = K(current.ReadAssignmentArea36(area), "gold_area_person");
        int person = PersonAreaAppointmentSource.Resolve(current, rawPerson);
        ushort basis = GoldBasis(area);
        var row = state.Areas.GetValueOrDefault(area) ?? throw Missing("gold_area_current");
        ushort population = row.NativeNormalAftermath?.Quantity52 is ushort value ? value : throw Missing("gold_population");
        bool valid = K(current.ReadPersonNativeValidity(person), "gold_person_validity");
        int task = valid ? TaskPercent(person, false, count) : 0;
        int ability = valid ? Ability(person, 2) : 0;
        ushort progress = row.Development?.Commerce is >= 0 and <= 65535 ? (ushort)row.Development.Commerce : throw Missing("gold_progress");
        byte security = row.Security is >= 0 and <= 255 ? (byte)row.Security : throw Missing("gold_security");
        byte bonus = K(current.ReadAssignmentArea6D(area), "gold_bonus");
        var statistics = state.RegionStatistics?.Entries.GetValueOrDefault(area) ?? throw Missing("gold_statistics");
        ushort terrain = statistics.TerrainCellCount is >= 0 and <= 65535 ? (ushort)statistics.TerrainCellCount : throw Missing("gold_terrain_count");
        ushort owned = statistics.IsOwnedCountCurrent && statistics.OwnedCellCount is >= 0 and <= 65535
            ? (ushort)statistics.OwnedCellCount : throw Missing("gold_owned_count");
        int trait = K(TacticRemakeCurrentTraits.PersonContribution(current, person, 0x24), "gold_trait24");
        bool forceValid = K(current.ReadForceNativeValidity(force), "gold_force_validity");
        int panel = forceValid ? K(panels.ReadPanelModifier(state, forceId, 0x35), "gold_panel35") : 0;
        int selector = K(BattleRemakeCurrentSettings.ReadSelectorE58(state), "gold_settings_selector");
        int rate = 100;
        int difficulty = selector != 0 ? K(BattleRemakeCurrentSettings.ReadInt32(state, 0x28), "gold_difficulty") : 0;
        var representativeForce = current.ReadForceTableReference(rawForce <= 51 ? rawForce : 0) ?? throw Missing("gold_representative_force");
        var player = current.ReadCurrentPlayerForce() ?? throw Missing("gold_player_pointer");
        int playerId = K(current.ReadForcePhysicalSlot(player), "gold_player_identity");
        bool own = playerId != 0 && playerId == K(current.ReadForcePhysicalSlot(representativeForce), "gold_representative_identity");
        if (selector != 0) rate = own ? difficulty switch { 0 => 200, 1 => 150, _ => 100 }
            : difficulty == 3 ? 150 : difficulty >= 4 ? 200 : 100;
        int? advanced = selector == 0 ? K(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, own ? 0x1F : 0x22), "gold_advanced_percent") : null;
        return CeAreaIncomeMath.CalculateGold(new(basis, population, security, progress, bonus,
            owned, terrain, ability, task, valid, trait, panel, rate, advanced, forceValid)).Gold;
    });

    private ushort GoldBasis(int area)
    {
        var source = state.Areas.GetValueOrDefault(area)?.EconomySource ?? throw Missing("gold_basis");
        if (source.FixedDataSha256 != scenario.FixedDataSource.Sha256 || source.ScenarioSha256 != scenario.ScenarioSource.Sha256)
            throw Missing("gold_basis_source");
        return source.RawField44 is >= 0 and <= 65535 ? (ushort)source.RawField44 : throw Missing("gold_basis_domain");
    }

    internal AreaAdministrationQuery<int?> Food(int area, Func<int, int> count) => Read(() =>
    {
        int rawMonth = state.NativeMapSeason?.ThirdDate?.Month ?? throw Missing("food_world_third_month");
        int month = rawMonth + 1;
        if (month > 12) month = 1;
        byte citySlot = K(current.ReadAreaCitySlot(new(area)), "food_area_city");
        var city = current.ReadCityTableReference(citySlot <= 51 ? citySlot : 0) ?? throw Missing("food_city_pointer");
        if (!K(current.ReadBaseNativeValidity(city), "food_city_validity")) return 0;
        int cityForce = K(current.ReadBaseForceSlot(city), "food_city_force");
        int hex = K(current.ReadAreaHexIndex(new(area)), "food_area_hex");
        var hexPointer = current.ReadHexTableReference(hex < 48400 ? hex : 0) ?? throw Missing("food_hex_pointer");
        int rawForce = K(current.ReadHexForceSlot(hexPointer), "food_hex_force");
        if (cityForce != rawForce) return 0;
        if (cityForce < 0) throw Missing("food_city_force_domain");
        var force = current.ReadForceTableReference(cityForce <= 51 ? cityForce : 0) ?? throw Missing("food_force_pointer");
        int forceId = K(current.ReadForcePhysicalSlot(force), "food_force_identity");
        if (forceId is >= 46 and <= 50)
        {
            if (!capacityInputsBound) throw Missing("food_capacity_inputs");
            int capacity = BasePostLoad.ReadSuppliesCapacity(city, current);
            return unchecked(capacity - K(current.ReadBaseResource(city, BaseResourceKind.Supplies), "food_city_stock"));
        }
        ushort rawPerson = K(current.ReadAssignmentArea36(area), "food_area_person");
        int person = PersonAreaAppointmentSource.Resolve(current, rawPerson);
        ushort basis = FoodBasis(area);
        var row = state.Areas.GetValueOrDefault(area) ?? throw Missing("food_area_current");
        ushort population = row.NativeNormalAftermath?.Quantity52 is ushort value ? value : throw Missing("food_population");
        bool valid = K(current.ReadPersonNativeValidity(person), "food_person_validity");
        int task = valid ? TaskPercent(person, false, count) : 0;
        int ability = valid ? Ability(person, 3) : 0;
        ushort progress = row.Development?.Agriculture is >= 0 and <= 65535 ? (ushort)row.Development.Agriculture : throw Missing("food_progress");
        byte security = row.Security is >= 0 and <= 255 ? (byte)row.Security : throw Missing("food_security");
        byte bonus = K(current.ReadAssignmentArea6D(area), "food_bonus");
        var statistics = state.RegionStatistics?.Entries.GetValueOrDefault(area) ?? throw Missing("food_statistics");
        ushort terrain = statistics.TerrainCellCount is >= 0 and <= 65535 ? (ushort)statistics.TerrainCellCount : throw Missing("food_terrain_count");
        ushort owned = statistics.IsOwnedCountCurrent && statistics.OwnedCellCount is >= 0 and <= 65535
            ? (ushort)statistics.OwnedCellCount : throw Missing("food_owned_count");
        int trait = K(TacticRemakeCurrentTraits.PersonContribution(current, person, 0x25), "food_trait25");
        bool forceValid = K(current.ReadForceNativeValidity(force), "food_force_validity");
        int panel = forceValid ? K(panels.ReadPanelModifier(state, forceId, 0x34), "food_panel34") : 0;
        if (month is < 1 or > 12) throw Missing("food_season_month_domain");
        byte? flags = month is >= 7 and <= 9 ? K(current.ReadAreaFlags38(new(area)), "food_flags38") : null;
        int selector = K(BattleRemakeCurrentSettings.ReadSelectorE58(state), "food_settings_selector");
        int rate = 100;
        int difficulty = selector != 0 ? K(BattleRemakeCurrentSettings.ReadInt32(state, 0x28), "food_difficulty") : 0;
        var representativeForce = current.ReadForceTableReference(rawForce <= 51 ? rawForce : 0) ?? throw Missing("food_representative_force");
        var player = current.ReadCurrentPlayerForce() ?? throw Missing("food_player_pointer");
        int playerId = K(current.ReadForcePhysicalSlot(player), "food_player_identity");
        bool own = playerId != 0 && playerId == K(current.ReadForcePhysicalSlot(representativeForce), "food_representative_identity");
        if (selector != 0) rate = own ? difficulty switch { 0 => 200, 1 => 150, _ => 100 }
            : difficulty == 3 ? 150 : difficulty >= 4 ? 200 : 100;
        int? advanced = selector == 0 ? K(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, own ? 0x20 : 0x23), "food_advanced_percent") : null;
        return CeAreaIncomeMath.CalculateSupplies(new(basis, population, security, progress, bonus,
            owned, terrain, ability, task, valid, trait, panel, month, flags, rate, advanced, forceValid)).Supplies;
    });

    private ushort FoodBasis(int area)
    {
        var source = state.Areas.GetValueOrDefault(area)?.EconomySource ?? throw Missing("food_basis");
        if (source.FixedDataSha256 != scenario.FixedDataSource.Sha256 || source.ScenarioSha256 != scenario.ScenarioSource.Sha256)
            throw Missing("food_basis_source");
        return source.RawField46 is >= 0 and <= 65535 ? (ushort)source.RawField46 : throw Missing("food_basis_domain");
    }

    internal AreaAdministrationQuery<int?> Soldiers(int area) => Read(() =>
    {
        //7826E0 reads CURRENT CityCA before entering the scalar, including before.
        byte citySlot = K(current.ReadAreaCitySlot(new(area)), "troops_area_city");
        var city = current.ReadCityTableReference(citySlot <= 51 ? citySlot : 0) ?? throw Missing("troops_city_pointer");
        int cityPhysical = K(city.PhysicalSlot, "troops_city_identity");
        ushort rawRecruiter = K(current.ReadAssignmentCityCA(cityPhysical), "troops_city_recruiter");
        if (!K(current.ReadBaseNativeValidity(city), "troops_city_validity")) return 0;
        int cityForce = K(current.ReadBaseForceSlot(city), "troops_city_force");
        int hex = K(current.ReadAreaHexIndex(new(area)), "troops_area_hex");
        var hexPointer = current.ReadHexTableReference(hex < 48400 ? hex : 0) ?? throw Missing("troops_hex_pointer");
        int rawForce = K(current.ReadHexForceSlot(hexPointer), "troops_hex_force");
        if (cityForce != rawForce) return 0;
        int stock = K(current.ReadBaseResource(city, BaseResourceKind.Troops), "troops_city_stock");
        if (!capacityInputsBound) throw Missing("troops_capacity_inputs");
        int capacity = BasePostLoad.ReadTroopCapacity(city, current);
        if (stock >= capacity) return 0;
        if (cityForce < 0) throw Missing("troops_city_force_domain");
        var force = current.ReadForceTableReference(cityForce <= 51 ? cityForce : 0) ?? throw Missing("troops_force_pointer");
        int forceId = K(current.ReadForcePhysicalSlot(force), "troops_force_identity");
        if (forceId is >= 46 and <= 50)
        {
            int specialDifficulty = K(BattleRemakeCurrentSettings.ReadInt32(state, 0x28), "troops_special_difficulty");
            int freshStock = K(current.ReadBaseResource(city, BaseResourceKind.Troops), "troops_city_stock");
            int freshCapacity = BasePostLoad.ReadTroopCapacity(city, current);
            return CeAreaIncomeMath.CalculateSpecialTroops(freshStock, freshCapacity, specialDifficulty);
        }
        int recruiter = PersonAreaAppointmentSource.Resolve(current, rawRecruiter);
        if (!K(current.ReadPersonNativeValidity(recruiter), "troops_recruiter_validity")) return 0;
        int person = PersonAreaAppointmentSource.Resolve(current,
            K(current.ReadAssignmentArea36(area), "troops_area_person"));
        ushort basis = TroopsBasis(area);
        var row = state.Areas.GetValueOrDefault(area) ?? throw Missing("troops_area_current");
        ushort population = row.NativeNormalAftermath?.Quantity52 is ushort value ? value : throw Missing("troops_population");
        int ability = K(current.ReadPersonNativeValidity(recruiter), "troops_recruiter_ability_validity") ? Ability(recruiter, 1) : 0;
        ushort progress = row.Development?.Barracks is >= 0 and <= 65535 ? (ushort)row.Development.Barracks : throw Missing("troops_progress");
        var statistics = state.RegionStatistics?.Entries.GetValueOrDefault(area) ?? throw Missing("troops_statistics");
        ushort terrain = statistics.TerrainCellCount is >= 0 and <= 65535 ? (ushort)statistics.TerrainCellCount : throw Missing("troops_terrain_count");
        byte security = row.Security is >= 0 and <= 255 ? (byte)row.Security : throw Missing("troops_security");
        ushort owned = statistics.IsOwnedCountCurrent && statistics.OwnedCellCount is >= 0 and <= 65535
            ? (ushort)statistics.OwnedCellCount : throw Missing("troops_owned_count");
        ushort flags = K(OfficerBehaviorSourceAdapter.EncodeFlags(current.ReadPersonBehavior(recruiter)), "troops_recruiter_flags");
        bool busy = (flags & 1) != 0 || PersonCurrentBusySource.Read(current, recruiter,
            field => Missing("troops_recruiter_" + field));
        byte cf = state.NativeBaseCaptureCurrent?.Cities.GetValueOrDefault(cityPhysical)?.CF ?? throw Missing("troops_city_CF");
        int day = state.NativeMapSeason?.ThirdDate?.Day ?? throw Missing("troops_world_third_day");
        //The original re-reads CF for day>10. This projection has no CF writer.
        int cycles = day <= 10 ? 3 : day <= 20 ? cf + 2 : cf + 1;
        int policy = Policy(city, 0x1C);
        int recruitTrait = K(TacticRemakeCurrentTraits.PersonContribution(current, recruiter, 0x11), "troops_recruiter_trait11");
        int areaTrait = K(TacticRemakeCurrentTraits.PersonContribution(current, person, 0x26), "troops_area_trait26");
        bool forceValid = K(current.ReadForceNativeValidity(force), "troops_force_validity");
        int panel = forceValid ? K(panels.ReadPanelModifier(state, forceId, 0x36), "troops_panel36") : 0;
        int selector = K(BattleRemakeCurrentSettings.ReadSelectorE58(state), "troops_settings_selector");
        int rate = 100;
        int difficulty = selector != 0 ? K(BattleRemakeCurrentSettings.ReadInt32(state, 0x28), "troops_difficulty") : 0;
        var representativeForce = current.ReadForceTableReference(rawForce <= 51 ? rawForce : 0) ?? throw Missing("troops_representative_force");
        var player = current.ReadCurrentPlayerForce() ?? throw Missing("troops_player_pointer");
        int playerId = K(current.ReadForcePhysicalSlot(player), "troops_player_identity");
        bool own = playerId != 0 && playerId == K(current.ReadForcePhysicalSlot(representativeForce), "troops_representative_identity");
        if (selector != 0) rate = own ? difficulty switch { 0 => 200, 1 => 150, 4 => 60, _ => 100 } : 100;
        int? advanced = selector == 0 ? K(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, own ? 0x21 : 0x24), "troops_advanced_percent") : null;
        return CeAreaIncomeMath.CalculateTroops(new(basis, population, security, progress, owned, terrain,
            ability, busy, cycles, policy, recruitTrait, areaTrait, panel, rate, advanced, forceValid)).Troops;
    });

    private ushort TroopsBasis(int area)
    {
        var source = state.Areas.GetValueOrDefault(area)?.EconomySource ?? throw Missing("troops_basis");
        if (source.FixedDataSha256 != scenario.FixedDataSource.Sha256 || source.ScenarioSha256 != scenario.ScenarioSource.Sha256)
            throw Missing("troops_basis_source");
        if (source.FixedRemainingBytesHex is not { Length: 52 }) throw Missing("troops_basis_format");
        byte[] bytes;
        try { bytes = Convert.FromHexString(source.FixedRemainingBytesHex); }
        catch (FormatException) { throw Missing("troops_basis_format"); }
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(6, 2));
    }

    internal ushort AutomaticAreaBasis(int area, int mode) => mode switch
    {
        2 => GoldBasis(area), 3 => FoodBasis(area), 4 => TroopsBasis(area), _ => 0
    };

    internal int AutomaticAbility(int person, int dimension) => Ability(person, dimension);

    internal int AutomaticAbilityDifference(int left, int right, int dimension)
    {
        int rightAbility = Ability(right, dimension);
        int leftAbility = Ability(left, dimension);
        return unchecked(leftAbility - rightAbility);
    }

    internal bool AutomaticTrait111(int person)
    {
        for (int slot = 0; slot < 9; slot++)
            if (K(TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current, person, slot),
                "automatic_trait111." + slot) == 111) return true;
        return false;
    }

    internal AreaAdministrationQuery<int?> Occupation(int area, int person, Func<int, int> count) =>
        Read(() => OccupationCore(area, person, count));

    internal AreaAdministrationQuery<int?> AutoOccupation(int person, Func<int, int> count)
    {
        var checkpoint = current.CaptureAreaPreview([], [], captureCityDistricts: true);
        try { return Read(() => OccupationCore(null, person, count)); }
        finally { current.RestoreAreaPreview(checkpoint); }
    }

    private int OccupationCore(int? area, int person, Func<int, int> count)
    {
        int value = 0;
        if (K(current.ReadPersonNativeValidity(person), "occupation_person_validity"))
        {
            int trait = K(TacticRemakeCurrentTraits.CommanderEffectMaximum(current, person, 0x0F), "occupation_trait0F");
            int charm = Ability(person, 4);
            if (charm is < 0 or > 255) throw Missing("occupation_charm_source_byte_domain");
            int task = K(TacticRemakeCurrentTraits.PersonContribution(current, person, 0x7E), "occupation_trait7E") > 0
                ? 100 : count(person) switch { 0 or 1 => 100, 2 => 85, 3 => 60, _ => throw Missing("occupation_task_current_fallback3710") };
            var force = current.ReadPersonForce(person) ?? throw Missing("occupation_person_force");
            int advantage = ForceAdvantage(force);
            int selector = K(BattleRemakeCurrentSettings.ReadSelectorE58(state), "occupation_settings_selector");
            bool opponent = false;
            if (area is int physicalArea)
            {
                int difficulty = K(BattleRemakeCurrentSettings.ReadInt32(state, selector == 0 ? 0x134 : 0x28), "occupation_difficulty");
                if (difficulty >= 4)
                {
                    var areaForce = AreaForce(physicalArea);
                    var player = current.ReadCurrentPlayerForce() ?? throw Missing("occupation_player_pointer");
                    int identity = K(current.ReadForcePhysicalSlot(player), "occupation_player_identity");
                    opponent = identity == 0 || identity != K(current.ReadForcePhysicalSlot(areaForce), "occupation_area_force_identity");
                }
            }
            bool alternate = selector != 0 && K(BattleRemakeCurrentSettings.ReadInt32(state, 0xE4), "occupation_modeE4") == 1;
            int? advanced = selector == 0
                ? K(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 10), "occupation_advanced10") : null;
            value = AreaAdministrationPreviewMath.Occupation(charm, trait, task, advantage, opponent, alternate, advanced);
        }
        return area is int cityArea ? AreaAdministrationPreviewMath.OccupationWithCityCenter(value, IsCityCenter(cityArea)) : value;
    }

    internal AreaAdministrationQuery<bool?> OccupationHighlight(ushort rawPerson)
    {
        try
        {
            if (rawPerson == 0) return AreaAdministrationQuery<bool?>.Known(false);
            var player = current.ReadCurrentPlayerForce() ?? throw Missing("occupation_highlight_player_pointer");
            if (!K(current.ReadForceNativeValidity(player), "occupation_highlight_player_validity"))
                return AreaAdministrationQuery<bool?>.Known(false);
            return AreaAdministrationQuery<bool?>.Known(ForceAdvantage(player) != 0);
        }
        catch (InvalidDataException unknown) { return AreaAdministrationQuery<bool?>.Unknown(unknown.Message); }
    }

    private int ForceAdvantage(ForceReference force)
    {
        var value = PersonReturnPathSource.ReadForceAdvantage(state, current, force, 9, 0);
        return value.Value ?? throw new InvalidDataException(value.UnresolvedOwner ?? "area-draft.preview_occupation_geography");
    }

    private bool IsCityCenter(int area)
    {
        int raw = K(current.ReadAreaCitySlot(new(area)), "occupation_city_slot");
        var city = current.ReadCityTableReference(raw <= 51 ? raw : 0) ?? throw Missing("occupation_city_pointer");
        if (!K(current.ReadBaseNativeValidity(city), "occupation_city_validity")) return false;
        int link = K(current.ReadBaseObjectLink(city), "occupation_city_object");
        var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0) ?? throw Missing("occupation_object_pointer");
        int hex = K(current.ReadObjectNativeValidity(obj), "occupation_object_validity")
            ? K(current.ReadObjectHexIndex(obj), "occupation_object_hex") : 48400;
        return hex == K(current.ReadAreaHexIndex(new(area)), "occupation_area_hex");
    }

    internal AreaAdministrationQuery<int?> Development(int area, int person, int kind,
        bool fullEffort, Func<int, int> count, bool candidateCity)
    {
        return Read(() =>
        {
            if (!K(current.ReadPersonNativeValidity(person), "person_validity")) return 0;
            var row = state.Areas.GetValueOrDefault(area) ?? throw Missing("area_current");
            int? raw = kind switch { 1 => row.Development?.Commerce, 2 => row.Development?.Agriculture, 3 => row.Development?.Barracks, _ => null };
            if (raw is not (>= 0 and <= 65535)) throw Missing("progress_current." + kind);
            ushort progress = (ushort)raw.Value;
            int level = CeAreaIncomeMath.Level(progress);
            if (level == 5) throw Missing("completed_level_current_bonus");
            int effective = Ability(person, kind switch { 1 => 2, 2 => 3, _ => 1 });
            int task = TaskPercent(person, fullEffort, count);
            bool away = Away(person);
            int id = K(current.ReadPersonIdentity(person), "person_identity");
            var officer = state.Officers.GetValueOrDefault(id) ?? throw Missing("person_owner");
            int doctrine = K(OfficerDoctrineRules.ReadDoctrineId(officer.DoctrineId, officer.AbilityState), "person_doctrine");
            if ((uint)doctrine > 6) throw Missing("person_doctrine_domain");
            var relations = Relations(area, person, excludeSelf: true);
            var city = City(area, person, candidateCity);
            int cityTrait = CityTrait(city, 0x2D), policy = Policy(city, 0x1D);
            var force = AreaForce(area);
            int forceId = K(force.PhysicalSlot, "area_force_identity");
            byte flags = state.NativeArmySimulation?.Forces.GetValueOrDefault(forceId)?.Flags193
                ?? throw Missing("area_force_flags193");
            int own = K(TacticRemakeCurrentTraits.PersonContribution(current, person, 0x21), "person_trait21");
            return AreaAdministrationPreviewMath.Development(effective, task, away, doctrine, kind,
                progress, level, 0, relations.Close, relations.Other, (flags & 1) != 0,
                own, cityTrait, policy, AdvancedPercent(0x1D));
        });
    }

    internal AreaAdministrationQuery<int?> AutoDevelopment(int area, int person, int focus, Func<int, int> count) => Read(() =>
    {
        //3424A0 resolves the candidate's City before its native-validity gate.
        var city = City(area, person, candidateCity: true);
        if (!K(current.ReadPersonNativeValidity(person), "person_validity")) return 0;
        int cityTrait = CityTrait(city, 0x2D), policy = Policy(city, 0x1D);
        //2670D0 performs this prelude even for kind0, whose base then returns0.
        bool away = Away(person);
        int basis = 0;
        if (focus is >= 1 and <= 3)
        {
            if (!K(current.ReadPersonNativeValidity(person), "person_validity"))
                throw Missing("task_current_fallback");
            int task = TaskPercent(person, false, count);
            int effective = Ability(person, focus switch { 1 => 2, 2 => 3, _ => 1 });
            int id = K(current.ReadPersonIdentity(person), "person_identity");
            var officer = state.Officers.GetValueOrDefault(id) ?? throw Missing("person_owner");
            int doctrine = K(OfficerDoctrineRules.ReadDoctrineId(officer.DoctrineId, officer.AbilityState), "person_doctrine");
            if ((uint)doctrine > 6) throw Missing("person_doctrine_domain");
            var row = state.Areas.GetValueOrDefault(area) ?? throw Missing("area_current");
            int? raw = focus switch { 1 => row.Development?.Commerce, 2 => row.Development?.Agriculture, _ => row.Development?.Barracks };
            if (raw is not (>= 0 and <= 65535)) throw Missing("progress_current." + focus);
            ushort progress = (ushort)raw.Value;
            int level = CeAreaIncomeMath.Level(progress);
            var relations = Relations(area, person, excludeSelf: true);
            if (level == 5) throw Missing("completed_level_current_bonus");
            var force = AreaForce(area);
            int forceId = K(force.PhysicalSlot, "area_force_identity");
            byte flags = state.NativeArmySimulation?.Forces.GetValueOrDefault(forceId)?.Flags193
                ?? throw Missing("area_force_flags193");
            basis = AreaAdministrationPreviewMath.DevelopmentBasis(effective, task, away, doctrine,
                focus, progress, level, 0, relations.Close, relations.Other, (flags & 1) != 0);
        }
        int own = K(TacticRemakeCurrentTraits.PersonContribution(current, person, 0x21), "person_trait21");
        return AreaAdministrationPreviewMath.DevelopmentModifiers(basis, own, cityTrait, policy, AdvancedPercent(0x1D));
    });

    internal AreaAdministrationQuery<int?> Security(int area, int person, bool fullEffort, Func<int, int> count)
    {
        return Read(() =>
        {
            if (!K(current.ReadPersonNativeValidity(person), "person_validity")) return 0;
            int? raw = state.Areas.GetValueOrDefault(area)?.Security;
            if (raw is not (>= 0 and <= 255)) throw Missing("security_current");
            int effective = Ability(person, 0), task = TaskPercent(person, fullEffort, count);
            bool away = Away(person);
            var relations = Relations(area, person, excludeSelf: false);
            var city = City(area, person, candidateCity: false);
            int cityTrait = CityTrait(city, 0x4A), policy = Policy(city, 0x1E);
            int own = K(TacticRemakeCurrentTraits.PersonContribution(current, person, 0x22), "person_trait22");
            return AreaAdministrationPreviewMath.Security(effective, task, away, (byte)raw.Value,
                relations.Close, relations.Other, own, cityTrait, policy, AdvancedPercent(0x1C));
        });
    }

    private int Ability(int person, int dimension)
    {
        int id = K(current.ReadPersonIdentity(person), "person_identity");
        _abilities ??= new(current, scenario, language, SimulationRuleIdentity.ForLocale(language));
        return K(_abilities.ResolveDimension(state, id, dimension,
            (force, effect) => panels.ReadPanelModifier(state, force, effect)), "effective_ability." + dimension);
    }

    private int TaskPercent(int person, bool fullEffort, Func<int, int> count)
    {
        if (fullEffort) return 100;
        if (K(TacticRemakeCurrentTraits.PersonContribution(current, person, 0x7E), "person_trait7E") > 0) return 100;
        return count(person) switch { 0 or 1 => 100, 2 => 64, 3 => 44, _ => throw Missing("task_current_fallback") };
    }

    private bool Away(int person)
    {
        ushort flags = K(OfficerBehaviorSourceAdapter.EncodeFlags(current.ReadPersonBehavior(person)), "person_flags");
        if ((flags & 1) != 0) return true;
        //22A200(person,1): actual second-location Army and commander equality.
        if (K(PersonArmyLocationSource.ReadCommanderArmyValidity(state, current, person), "commander_army_validity")) return true;
        var associated = PersonLinkedEntityLookup.Find(person, this);
        return K(ReadLinkedEntityNativeValidity(associated), "associated_validity");
    }

    private (int Close, int Other) Relations(int area, int person, bool excludeSelf)
    {
        var definition = scenario.Snapshot.Areas[area];
        byte mask = definition.RegionalNeighborMask;
        int city = K(current.ReadAreaCitySlot(new(area)), "area_city");
        int own = excludeSelf ? K(current.ReadPersonIdentity(person), "person_identity") : 0;
        int close = 0, other = 0;
        for (int index = 0; index < 8; index++)
        {
            //Source looks up/validates before testing the mask and same City.
            ushort raw = K(current.ReadAreaNeighborSlot(new(area), index), "neighbor_slot");
            var neighbor = current.ReadAreaTableReference(raw <= 500 ? raw : 0) ?? throw Missing("neighbor_pointer");
            if (!K(current.ReadAreaNativeValidity(neighbor), "neighbor_validity")) continue;
            if ((mask & (1 << index)) == 0 || K(current.ReadAreaCitySlot(neighbor), "neighbor_city") != city) continue;
            int physicalArea = K(neighbor.PhysicalSlot, "neighbor_identity");
            ushort rawPerson = K(current.ReadAssignmentArea36(physicalArea), "neighbor_person");
            int next = PersonAreaAppointmentSource.Resolve(current, rawPerson);
            if (!K(current.ReadPersonNativeValidity(next), "neighbor_person_validity")) continue;
            if (excludeSelf && K(current.ReadPersonIdentity(next), "neighbor_person_identity") == own) continue;
            int tier = OwnedPersonRelationInputs.ComputeCurrent(state, current, person, next, 0).Tier;
            if (tier == 1) close++; else if (tier != 2) other++;
        }
        return (close, other);
    }

    private BaseReference City(int area, int person, bool candidateCity)
    {
        if (candidateCity)
            return CurrentGateMapQueries.FindCity(BaseLookup.Find(K(current.ReadPersonFirstLocation(person), "person_first_location"), current), current);
        byte raw = K(current.ReadAreaCitySlot(new(area)), "area_city");
        return current.ReadCityTableReference(raw <= 51 ? raw : 0) ?? throw Missing("city_pointer");
    }

    private int CityTrait(BaseReference city, int effect)
    {
        ushort link = K(current.ReadBaseObjectLink(city), "city_link");
        if (link is < 1 or > 61) return 0;
        var people = current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("person_registry");
        int max = 0;
        foreach (int? entry in people)
        {
            int person = K(entry, "person_registry_pointer");
            if (K(current.ReadPersonFirstLocation(person), "city_person_first_location") != link) continue;
            if (K(current.ReadPersonStatus(person), "city_person_status") is < 1 or > 4) continue;
            int value = K(TacticRemakeCurrentTraits.PersonContribution(current, person, effect), "city_trait." + effect);
            if (value != 0 && value > max) max = value;
        }
        return max;
    }

    private int Policy(BaseReference city, int effect)
    {
        int raw = K(current.ReadBaseForceSlot(city), "city_force");
        var force = current.ReadForceTableReference(raw <= 51 ? raw : 0) ?? throw Missing("city_force_pointer");
        if (!K(current.ReadForceNativeValidity(force), "city_force_validity")) return 0;
        var owner = state.Forces.GetValueOrDefault(K(force.PhysicalSlot, "city_force_identity")) ?? throw Missing("city_force_owner");
        return K(BattleRemakeCurrentForcePolicies.ReadForceEffect(state, owner, effect, current), "city_policy." + effect);
    }

    private ForceReference AreaForce(int area)
    {
        int hex = K(current.ReadAreaHexIndex(new(area)), "area_hex");
        var pointer = current.ReadHexTableReference(hex < 48400 ? hex : 0) ?? throw Missing("area_hex_pointer");
        int raw = K(current.ReadHexForceSlot(pointer), "area_force");
        return current.ReadForceTableReference(raw <= 51 ? raw : 0) ?? throw Missing("area_force_pointer");
    }

    private int? AdvancedPercent(int index) => K(BattleRemakeCurrentSettings.ReadAdvancedSettings(state), "settings_mode")
        ? K(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, index), "advanced_percent." + index) : null;

    //22AC00 uses the actual current ordered task chain. Saved task members and
    //dispatch slots are supplied by the existing typed owner without rebinding
    //or modifying the UI context on a query.
    public IReadOnlyList<PersonLinkedEntityReference?>? ReadCurrentPersonLinkedEntities() =>
        state.PersonnelCommands is { Disappearances: not null } ? current.ReadCurrentPersonLinkedEntities() : null;
    public ushort? ReadPersonIdentity(int physicalSlot) => current.ReadPersonIdentity(physicalSlot);
    public bool? ReadLinkedEntityNativeValidity(PersonLinkedEntityReference reference)
    {
        if (reference.Identity is not int id) return false;
        var owner = state.PersonnelCommands;
        if (owner?.Returns.TryGetValue(id, out var returning) == true) return (returning.DispatchSlot ?? -1) != -1;
        if (owner?.Disappearances?.TryGetValue(id, out var disappearing) == true) return disappearing.DispatchSlot != -1;
        return null;
    }
    public IReadOnlyList<uint?>? ReadLinkedEntityMemberIds(PersonLinkedEntityReference reference)
    {
        if (reference.Identity is not int id) return null;
        var owner = state.PersonnelCommands;
        if (owner?.Returns.TryGetValue(id, out var returning) == true) return returning.MemberIds.Select(x => (uint?)x).ToArray();
        if (owner?.Disappearances?.TryGetValue(id, out var disappearing) == true) return disappearing.MemberIds.Select(x => (uint?)x).ToArray();
        return null;
    }
    private static AreaAdministrationQuery<int?> Read(Func<int> read)
    {
        try { return AreaAdministrationQuery<int?>.Known(read()); }
        catch (InvalidDataException unknown) { return AreaAdministrationQuery<int?>.Unknown(unknown.Message); }
    }
    private static T K<T>(T? value, string key) where T : struct => value ?? throw Missing(key);
    private static InvalidDataException Missing(string key) => new("area-draft.preview_" + key);
}
