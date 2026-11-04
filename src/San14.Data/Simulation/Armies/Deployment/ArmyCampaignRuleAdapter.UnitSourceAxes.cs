using System.Collections.ObjectModel;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The existing Army detail's two absolute five-axis sets. One
    /// current capture, explicit formation and local terrain preview; no Army
    /// allocation, world write, opposing Army or battle descriptor is involved.
    /// Null selection normalizes to the actual formation's land/naval radio;
    /// it equals explicitly supplying that radio. Surface14/16 is substituted
    /// only when the radio differs from the current formation's kind2 state.</summary>
    public UnitSourceAxes QueryUnitSourceAxes(GameState state, int unitId, bool? landSelected = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.Units.ContainsKey(unitId))
            return ReadUnitSourceAxesCurrent(state, unitId, landSelected, null, null, null, null);
        SourceWorldContext? current = null;
        TacticRemakeCurrentContext? rules = null;
        TacticRemakeCurrentAbilities? abilities = null;
        string? captureMissing = null;
        try
        {
            current = CaptureOfficerCommandCapacityCurrent(state);
            _personRelationInputs.BindCurrent(state, current);
            if (state.NativeArmySimulation?.Parameters is not { } parameters || parameters.Language != Seed.Language ||
                parameters.RuleVersion != SimulationRuleIdentity.ForLocale(Seed.Language)) throw AxisMissing("rule_profile");
            abilities = new(current, _scenario, Seed.Language, parameters.RuleVersion);
            rules = new(current, _catalog, _map, _terrain, _usage, Revision, abilities);
        }
        catch (InvalidDataException error) { captureMissing = error.Message; current = null; }
        return ReadUnitSourceAxesCurrent(state, unitId, landSelected, current, rules, abilities, captureMissing);
    }

    private UnitSourceAxes ReadUnitSourceAxesCurrent(GameState state, int unitId, bool? landSelected,
        SourceWorldContext? current, TacticRemakeCurrentContext? rules, TacticRemakeCurrentAbilities? abilities,
        string? captureMissing, bool listPreview = false, bool? conditionalModifiers = null,
        Func<int, int, int?>? abilityPanelReader = null, bool? commonModifiers = null,
        BattleUnitArmyStatKind? requestedStat = null, Action<BattleUnitArmyStatKind, float>? rawStatObserver = null)
    {
        var missing = new Dictionary<string, string>();
        var empty = new UnitSourceAxisValues(null, null, null, null, null);
        if (!state.Units.TryGetValue(unitId, out var army))
            return new(unitId, landSelected, null, null, null, empty, empty, null,
                new ReadOnlyDictionary<string, string>(new Dictionary<string, string> { ["army"] = "unit.axes.army_owner" }));
        UnitSourceAxisValues? signed = army.NativeTacticRuntime is { StatModifiers4A.Length: 6 } header
            ? new(header.StatModifiers4A[1], header.StatModifiers4A[2], header.StatModifiers4A[4],
                header.StatModifiers4A[0], header.StatModifiers4A[3]) : null;
        SourceWorldContext C() => current ?? throw new InvalidDataException(captureMissing!);
        T K<T>(T? value, string input) where T : struct => value ?? throw AxisMissing(input);
        T? Read<T>(string input, Func<T> read) where T : struct
        {
            try { return read(); }
            catch (InvalidDataException error) { missing[input] = error.Message; return null; }
        }
        int P(string key) => state.NativeArmySimulation?.Parameters is { } parameters &&
            parameters.Int32Values.TryGetValue(key, out int value) ? value : throw AxisMissing(key);
        int Person()
        {
            int physical = (C().ReadPersonLookupReference(army.OfficerId is >= 0 and <= 6000 ? army.OfficerId : 0)
                ?? throw AxisMissing("person_pointer")).PhysicalSlot ?? throw AxisMissing("person_receiver");
            if (!K(C().ReadPersonNativeValidity(physical), "person_validity") ||
                C().ReadPersonIdentity(physical) != army.OfficerId) throw AxisMissing("person_identity");
            return physical;
        }
        int? currentFormation = Read("formation.current", () => K(rules?.EffectiveFormation(state, army), "effective_formation"));
        bool? selectedLand = landSelected ?? Read("formation.selection", () =>
            K(TacticRemakeCurrentFormations.ReadRawByte(state, K(currentFormation, "current_formation"), 0x7E), "current_formation_kind") != 2);
        int? formation = Read("formation.selected", () =>
        {
            int value = K(selectedLand == true ? ArmyConfigurationSourceAdapter.ReadCurrentLand(state, army)
                : selectedLand == false ? ArmyConfigurationSourceAdapter.ReadNaval(state, army) : null, "formation_selection");
            if (value is < 1 or > 20) throw AxisMissing("valid_selected_formation");
            return value;
        });
        byte? surface = Read("surface.current", () =>
        {
            int hex = K(ArmyMarchSourceAdapter.ReadCurrentHex(state, army), "current_hex");
            var pointer = C().ReadHexTableReference(hex) ?? throw AxisMissing("hex_pointer");
            return K(C().ReadHexSurfaceCode(pointer), "hex_surface");
        });
        byte? preview = Read("surface.preview", () =>
        {
            bool actualNaval = K(TacticRemakeCurrentFormations.ReadRawByte(state,
                K(currentFormation, "current_formation"), 0x7E), "current_formation_kind") == 2;
            bool land = K(selectedLand, "formation_selection");
            return land && actualNaval ? (byte)(listPreview ? 16 : 14)
                : !land && !actualNaval ? (byte)(listPreview ? 14 : 16) : K(surface, "current_surface");
        });
        int? effectivePreview = Read("formation.preview_current", () =>
        {
            byte code = K(preview, "preview_surface");
            var terrain = _terrain.Catalog.Types[code <= 30 ? code : 0];
            return K((terrain.RawField2C & 2) != 0 ? ArmyConfigurationSourceAdapter.ReadNaval(state, army)
                : ArmyConfigurationSourceAdapter.ReadCurrentLand(state, army), "preview_effective_formation");
        });
        int? person = Read("commander", Person);
        int? forceLookup = Read("force.lookup", () => (int)K(C().ReadPersonOldForceSlot(K(person, "commander")), "person_force"));
        var forceReference = current is null || forceLookup is null ? null : current.ReadForceTableReference(forceLookup <= 51 ? forceLookup.Value : 0);
        int? forcePhysical = Read("force.physical", () => K(forceReference?.PhysicalSlot, "force_receiver"));
        byte? forceFlags = Read("force.flags", () => K(state.NativeArmySimulation?.Forces.GetValueOrDefault(K(forcePhysical, "force_receiver"))?.Flags193, "force_flags"));
        int Percent(bool mobility = false)
        {
            int selected = K(effectivePreview, "preview_effective_formation");
            if (selected is >= 14 and <= 16 || !mobility && selected is 19 or 20 || K(forcePhysical, "force_receiver") is >= 46 and <= 51) return 100;
            if (forceReference is null) throw AxisMissing("force_pointer");
            if (!K(C().ReadForceNativeValidity(forceReference), "force_validity")) return 100;
            int effect = selected <= 7 ? selected : selected <= 10 ? 8 : selected <= 13 ? 9 : 12;
            int level = ForcePolicyScratchCurrent.ReadBeforeFreedom(state, C(), forceReference, effect, true).Value;
            return unchecked(100 + unchecked(level * P(BattleRemakeCurrentParameterSeed.FormationEffectKey(effect)!)));
        }
        int Policy(int effect)
        {
            if (forceReference is null) throw AxisMissing("force_pointer");
            if (!K(C().ReadForceNativeValidity(forceReference), "force_validity")) return 0;
            var owner = state.Forces.GetValueOrDefault(K(forcePhysical, "force_receiver")) ?? throw AxisMissing("force_owner");
            if (!K(owner.PolicyEffects?.IsEnabled(effect), "policy_activation")) return 0;
            return K(C().BaseForceModifierReader?.Invoke(forceReference, effect), "active_policy");
        }
        int Coefficient(BattleUnitArmyStatKind kind)
        {
            var value = BattleRemakeCurrentFormationCoefficientQueries.Read(state, K(formation, "selected_formation"), kind)
                ?? throw AxisMissing("formation_coefficient");
            return BattleUnitSourceMath.FormationCoefficient(kind, value.NativeContainerByteCount, value.StaticField, value.EncodedQueryResult);
        }
        int[] PermanentTraits()
        {
            var values = new int[127];
            for (int index = 0; index < 9; index++)
            {
                var trait = TacticRemakeCurrentTraits.ResolveCommanderPersonality(C(), K(person, "commander"), index)
                    ?? throw AxisMissing("personality_pointer");
                if (!K(C().ReadSkillPersonalityValidity(trait), "personality_validity")) continue;
                if (K(C().ReadPersonalityWord(trait, 0xB4), "personality_condition") != ushort.MaxValue) continue;
                for (int pair = 0; pair < 2; pair++)
                {
                    byte effect = K(C().ReadPersonalityByte(trait, pair == 0 ? 0xB6 : 0xBA), "personality_effect");
                    short amount = unchecked((short)K(C().ReadPersonalityWord(trait, pair == 0 ? 0xB8 : 0xBC), "personality_amount"));
                    if (effect < values.Length) values[effect] = unchecked(values[effect] + Math.Clamp((int)amount, -100, 2000));
                }
            }
            return values;
        }
        UnitSourceAxisValues Axes(bool modifiers)
        {
            string prefix = modifiers ? "with" : "without";
            int[]? traits = null;
            try { traits = modifiers ? TacticRemakeCurrentTraits.ArmyContributions(C(), K(person, "commander"), ArmyTraitSourceAdapter.Encode(state, army)) : PermanentTraits(); }
            catch (InvalidDataException error) { missing[prefix + ".traits"] = error.Message; }
            int Trait(int index) => traits is { Length: 127 } ? traits[index] : throw AxisMissing("traits");
            var runtime = army.NativeTacticRuntime;
            bool statusOne = runtime?.Status10 == 1;
            int? command = null;
            int Command()
            {
                if(!statusOne)return 0;
                command ??= ArmyCommandAgeEffectSource.ReadCurrent(state,C(),army,
                    traits is{Length:127}?traits:throw AxisMissing("traits"));
                return command.Value;
            }
            sbyte Raw(int index) => runtime is { StatModifiers4A.Length: 6 } ? runtime.StatModifiers4A[index] : throw AxisMissing("status_modifiers");
            int Leadership() => K(abilities?.ResolveDimension(state, army.OfficerId, 0, abilityPanelReader), "leadership");
            bool applyCommonModifiers = commonModifiers ?? modifiers;
            float? common = Read(prefix + ".common", () =>
            {
                if (runtime is null) throw AxisMissing("army_header");
                var moraleCoefficients = K(BattleRemakeCurrentSettings.ReadCommonMoraleCoefficients(state), "morale_settings");
                int morale = Math.Min(K(ArmyMoraleSourceAdapter.Read(army), "morale"), K(rules?.ForceMoraleMaximum(state, K(forcePhysical, "force_receiver")), "morale_maximum"));
                float region = 1f, neighbor = 1f;
                if (applyCommonModifiers)
                {
                    var factor = new BattleRemakeCurrentRegionFactor(_regionSeed, rules!, C(), Revision);
                    region = K(factor.ResolveReceipt(state, army)?.Factor, "saved_region_factor");
                    var adjacent = ArmyNeighborCurrentFactor.Read(state, C(), army);
                    neighbor = adjacent.Factor ?? throw new InvalidDataException(adjacent.UnresolvedOwner!);
                }
                return BattleUnitSourceMath.CommonFactor(new(Leadership(), army.Troops, 1, morale, applyCommonModifiers,
                    runtime.Abnormal23 != 0,
                    moraleCoefficients.Offset, moraleCoefficients.Divisor,
                    region, BattleUnitSourceMath.FormationTerrainFactor(K(effectivePreview, "preview_effective_formation"), K(preview, "preview_surface"),
                        P(SimulationParameterKeys.BattleAttributes.Formation6TerrainPercent), P(SimulationParameterKeys.BattleAttributes.Formation14TerrainPercent), 100f), neighbor),
                    new(_battleSeed.CommonPowerExponent, P(SimulationParameterKeys.BattleAttributes.CommonRootDivisor),
                        P(SimulationParameterKeys.BattleAttributes.CommonLeadershipScale), P(SimulationParameterKeys.BattleAttributes.CommonAdditive),
                        _battleSeed.FlagNumerator, _battleSeed.FlagDenominator, P(SimulationParameterKeys.BattleAttributes.CommonDivisorNumerator), P(SimulationParameterKeys.BattleAttributes.CommonDivisorOffset)));
            });
            int Stat(BattleUnitArmyStatKind kind, int effect, int bonus, int raw, string limit, string step, string scale)
            {
                int doctrine = kind == BattleUnitArmyStatKind.FieldAttack ? Policy(40) : 0;
                if (doctrine > 0) throw AxisMissing("positive_attack_policy_eligibility");
                float value = BattleUnitSourceMath.ArmyStat(kind, new(K(common, "common"), Coefficient(kind), bonus, Percent(), statusOne,
                    Command(), statusOne ? Trait(effect) : 0, statusOne ? Trait(19) : 0, modifiers, Raw(raw), false, doctrine),
                    new(P(limit), P(step), P(step), P(scale), 100f, 1f));
                rawStatObserver?.Invoke(kind, value);
                return BattleUnitSourceMath.TruncateSingleToInt32(value);
            }
            int Bonus(int bit, int selected, string key) => (K(forceFlags, "force_flags") & bit) != 0 && formation == selected ? P(key) : 0;
            int? field = requestedStat is not null && requestedStat != BattleUnitArmyStatKind.FieldAttack ? null : Read(prefix + ".field", () => Stat(BattleUnitArmyStatKind.FieldAttack, 5, 0, 1,
                SimulationParameterKeys.BattleAttributes.AttackAbilityLimit, SimulationParameterKeys.BattleAttributes.AttackAbilityStep, SimulationParameterKeys.BattleAttributes.AttackCoefficientScale));
            int? siege = requestedStat is not null && requestedStat != BattleUnitArmyStatKind.SiegeTroopAttack ? null : Read(prefix + ".siege", () => Stat(BattleUnitArmyStatKind.SiegeTroopAttack, 6, Bonus(2, 5, SimulationParameterKeys.BattleAttributes.SiegeAbilityBonus), 2,
                SimulationParameterKeys.BattleAttributes.SiegeAbilityLimit, SimulationParameterKeys.BattleAttributes.SiegeAbilityStep, SimulationParameterKeys.BattleAttributes.AttackCoefficientScale));
            int? defense = requestedStat is not null && requestedStat != BattleUnitArmyStatKind.Defense ? null : Read(prefix + ".defense", () => Stat(BattleUnitArmyStatKind.Defense, 7, Bonus(0x20, 7, SimulationParameterKeys.BattleAttributes.Formation7ForceFlagBonus), 3,
                SimulationParameterKeys.BattleAttributes.DefenseAbilityLimit, SimulationParameterKeys.BattleAttributes.DefenseAbilityStep, SimulationParameterKeys.BattleAttributes.DefenseCoefficientScale));
            int? wall = requestedStat is not null && requestedStat != BattleUnitArmyStatKind.WallAttack ? null : Read(prefix + ".wall", () => BattleUnitSourceMath.TruncateSingleToInt32(BattleUnitSourceMath.ArmyWallAttack(
                new(Leadership(), army.Troops, Coefficient(BattleUnitArmyStatKind.WallAttack), Bonus(2, 5, SimulationParameterKeys.BattleAttributes.WallAbilityBonus),
                    Percent(), statusOne, Command(), statusOne ? Trait(8) : 0, statusOne ? Trait(19) : 0, modifiers, Raw(4)),
                new(P(SimulationParameterKeys.BattleAttributes.WallTroopOffset), P(SimulationParameterKeys.BattleAttributes.WallTroopDenominator),
                    P(SimulationParameterKeys.BattleAttributes.WallLeadershipDenominator), new(P(SimulationParameterKeys.BattleAttributes.WallAbilityLimit),
                        P(SimulationParameterKeys.BattleAttributes.WallAbilityStep), P(SimulationParameterKeys.BattleAttributes.WallAbilityStep), 0, 100f, 1f)))));
            int? mobility = requestedStat is not null ? null : Read(prefix + ".mobility", () =>
            {
                if (runtime is not { Status10: 1 or 2 }) throw AxisMissing("ordinary_army_status");
                if (P(TacticRemakeCurrentBattleControl.VersionKey) != 1 || P(TacticRemakeCurrentBattleControl.HeaderKey) != 0) throw AxisMissing("mobility_tutorial");
                int selected = K(formation, "selected_formation");
                int bytes = K(TacticRemakeCurrentFormations.ReadVectorByteCount(state, selected), "formation_vector");
                int raw = K(TacticRemakeCurrentFormations.ReadParameter(state, selected, 0x7F), "mobility_coefficient");
                int coefficient = (bytes & ~3) == 32 ? Math.Clamp(raw, 1, 30) : raw;
                coefficient = unchecked(coefficient + Bonus(2, 5, SimulationParameterKeys.ArmyMobility.FormationFiveMobilityBonus));
                int trait = runtime.Status10 == 2 ? Trait(94) : unchecked(Trait(4) + Trait(19) + Command());
                if (runtime.Status10 == 1 && Trait(116) != 0 && K(ArmySavedCommandKnowledge.ReadControl(state, army), "command_control") != 0) trait = unchecked(trait + Trait(116));
                int limit = P(SimulationParameterKeys.ArmyMobility.TraitMobilityLimit), step = P(SimulationParameterKeys.ArmyMobility.TraitMobilityStep);
                int capped = trait < -limit ? -limit : trait > limit ? limit : trait;
                float value = (float)unchecked(step * capped) / 100f + 1f;
                value *= (float)unchecked(Percent(true) * coefficient) / 100f;
                if (modifiers) value *= (float)Raw(0) * step / 100f + 1f;
                var baseline = C().ReadSurfaceTypeTableReference(1) ?? throw AxisMissing("baseline_terrain");
                int cost = K(C().ReadSurfaceTypeRawField2A(baseline), "baseline_cost");
                if (baseline.PhysicalSlot != 1) throw AxisMissing("baseline_terrain_identity");
                if (cost > 0) value *= _mobilitySeed.BaselineTerrainNormalization / (float)cost;
                byte code = K(preview, "preview_surface");
                if (selected is 6 or 14 && code is 5 or 6 or >= 8 and <= 11)
                    value *= (float)P(selected == 6 ? SimulationParameterKeys.ArmyMobility.FormationSixTerrainPercent : SimulationParameterKeys.ArmyMobility.FormationTerrainPercent) / 100f;
                if (runtime.Status10 == 2 && (K(forceFlags, "force_flags") & 8) != 0 && code is >= 8 and <= 11)
                    value *= (float)P(SimulationParameterKeys.ArmyMobility.ForceTerrainPercent) / 100f;
                int selector = K(BattleRemakeCurrentSettings.ReadSelectorE58(state), "settings_selector");
                if (runtime.Status10 == 2 && selector != 0 && K(BattleRemakeCurrentSettings.ReadInt32(state, 0x104), "environment") == 1)
                    value *= (float)P(SimulationParameterKeys.ArmyMobility.EnvironmentTerrainPercent) / 100f;
                byte flags = K(TacticRemakeCurrentFormations.ReadParameter(state, selected, 0xA6), "formation_flags");
                int? effect = (flags & 2) != 0 ? 59 : code is >= 8 and <= 11 ? 60 : null;
                if (effect is int policy) value *= (float)Policy(policy) / 100f + 1f;
                if (runtime.Status10 == 2 && selector == 0) value = (float)K(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 14), "advanced14") * value / 100f;
                return BattleUnitSourceMath.TruncateSingleToInt32(value);
            });
            return new(field, siege, wall, mobility, defense);
        }
        var without = conditionalModifiers == true ? empty : Axes(false);
        var with = conditionalModifiers == false ? empty : Axes(true);
        if (signed is null) missing["signed_modifiers"] = "unit.axes.status_modifiers";
        return new(unitId, selectedLand, formation, surface, preview, without, with, signed, new ReadOnlyDictionary<string, string>(missing));
    }
    private static InvalidDataException AxisMissing(string input) => new("unit.axes." + input);
}
