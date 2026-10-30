using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>One current capture for the unsubmitted composition. Each
    /// statistics group preserves its own unknown inputs; no Army is allocated
    /// or registered and no context scratch is published to the saved world.</summary>
    public IReadOnlyDictionary<int, DeploymentDraftFigures> QueryDeploymentDraftFigures(GameState state,
        IReadOnlyList<DeploymentDraftRow> rows)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(rows);
        var results = new Dictionary<int, DeploymentDraftFigures>();
        if (rows.Count == 0) return new System.Collections.ObjectModel.ReadOnlyDictionary<int, DeploymentDraftFigures>(results);
        SourceWorldContext? current = null; string? captureFailure = null;
        try
        {
            if (rows.Select(x => x.Request.OfficerId).Distinct().Count() != rows.Count)
                throw FigureMissing("duplicate_person");
            current = CaptureOfficerCommandCapacityCurrent(state);
            if (state.NativeArmySimulation?.Parameters is not { } parameters || parameters.Language != Seed.Language ||
                parameters.RuleVersion != SimulationRuleIdentity.ForLocale(Seed.Language)) throw FigureMissing("rule_profile");
        }
        catch (InvalidDataException error) { current = null; captureFailure = error.Message; }
        var abilities = current is null ? null : new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        bool indexesPrepared = false; string? indexesFailure = null;
        foreach (var row in rows)
        {
            int id = row.Request.OfficerId;
            DeploymentTroopMarker? marker = null; DeploymentCombatAxes? axes = null, modifiedAxes = null;
            string? markerMissing = captureFailure, axesMissing = captureFailure, modifiedMissing = captureFailure;
            if (current is not null)
            {
                try { marker = ReadMarker(row, Person(id)); } catch (InvalidDataException error) { markerMissing = error.Message; }
                try { axes = ReadAxes(row, Person(id), false); } catch (InvalidDataException error) { axesMissing = error.Message; }
                try { modifiedAxes = ReadAxes(row, Person(id), true); } catch (InvalidDataException error) { modifiedMissing = error.Message; }
            }
            results[id] = new(id, marker, axes, modifiedAxes, markerMissing, axesMissing, modifiedMissing);
        }
        return new System.Collections.ObjectModel.ReadOnlyDictionary<int, DeploymentDraftFigures>(results);

        int Person(int id)
        {
            int physical = _definitions.ResolveOfficerSource(id) ?? throw FigureMissing("person_identity");
            if (!state.Officers.ContainsKey(id) || F(current!.ReadPersonIdentity(physical), "person_identity") != id ||
                !F(current.ReadPersonNativeValidity(physical), "person_validity")) throw FigureMissing("valid_person_owner");
            return physical;
        }
        int P(string key) => state.NativeArmySimulation!.Parameters.Int32Values.TryGetValue(key, out int value)
            ? value : throw FigureMissing(key);
        int Formation(DeploymentDraftRow row)
        {
            int formation = row.Request.FormationId;
            if (formation is < 1 or > 20) throw FigureMissing("valid_explicit_formation");
            return formation;
        }
        DeploymentTroopMarker ReadMarker(DeploymentDraftRow row, int person)
        {
            return DeploymentTroopClassificationSource.Read(state, current!, person, Formation(row), row.Request.Troops).Marker;
        }
        DeploymentCombatAxes ReadAxes(DeploymentDraftRow row, int person, bool modifiers)
        {
            if (!indexesPrepared)
            {
                indexesPrepared = true;
                try
                {
                    var arena = state.ReadArmyLifecycle() ?? throw FigureMissing("saved_indexes");
                    _ = WorldIndexContext.ImportOwnedState(current!, arena.Indexes, state);
                }
                catch (InvalidDataException error) { indexesFailure = error.Message; }
            }
            if (indexesFailure is not null) throw new InvalidDataException(indexesFailure);
            int explicitFormation = Formation(row);
            // The finite ordinary land branch. The original setter can reject
            // kind2 and keep reset0; that branch needs its own proved receipt.
            if (F(TacticRemakeCurrentFormations.ReadRawByte(state, explicitFormation, 0x7E), "formation_kind") != 1)
                throw FigureMissing("ordinary_land_formation");
            int id = row.Request.OfficerId;
            var full = abilities!.Resolve(state, id) ?? throw FigureMissing("effective_abilities");
            int forceId = F(current!.ReadPersonOldForceSlot(person), "current_person_force");
            if (forceId is < 1 or > 51 || !state.Forces.TryGetValue(forceId, out var force)) throw FigureMissing("force_owner");
            var forceReference = current.ReadForceTableReference(forceId) ?? throw FigureMissing("force_pointer");
            byte forceFlags = state.NativeArmySimulation!.Forces.GetValueOrDefault(forceId)?.Flags193 ?? throw FigureMissing("force_flags");
            ushort first = F(current.ReadPersonFirstLocation(person), "first_location");
            //353E80 uses32C7D0: first match in the registered current4E
            //chain, not the City/Gate physical slot named by firstLocation.
            var sourceBase = BaseLookup.Find(first, current);
            int link = F(current.ReadBaseObjectLink(sourceBase), "current_base_object");
            var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0) ?? throw FigureMissing("source_object");
            if (!F(current.ReadObjectNativeValidity(obj), "source_object_validity")) throw FigureMissing("source_object_fallback");
            int hexId = Math.Clamp((int)F(current.ReadObjectHexIndex(obj), "source_hex"), 0, 48399);
            var hex = current.ReadHexTableReference(hexId) ?? throw FigureMissing("hex_pointer");
            byte terrainCode = F(current.ReadHexSurfaceCode(hex), "source_terrain");
            var terrain = _terrain.Catalog.Types[terrainCode <= 30 ? terrainCode : 0];
            int effectiveFormation = explicitFormation;
            if ((terrain.RawField2C & 2) != 0)
            {
                int naval = row.NavalFormationId;
                if (naval is < 1 or > 20 || F(TacticRemakeCurrentFormations.ReadRawByte(state, naval, 0x7E), "naval_kind") != 2)
                    throw FigureMissing("naval_setter_fallback");
                effectiveFormation = naval;
            }
            var tacticContext = new TacticRemakeCurrentContext(current, _catalog, _map, _terrain,
                new(Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language)), Revision, abilities);
            int maximumMorale = F(tacticContext.ForceMoraleMaximum(state, forceId), "maximum_morale");
            if (maximumMorale is < 0 or > 255) throw FigureMissing("maximum_morale_byte_branch");
            int morale = Math.Min(F(current.ReadBaseMorale44(sourceBase), "base_morale"), maximumMorale);
            int troops = Math.Clamp(row.Request.Troops, 0, 65535);
            var previewTraits = modifiers ? DeploymentPreviewTraits.Read(state, current,
                new(person, troops, explicitFormation, hexId, forceId)) : null;
            IReadOnlyList<int> traits = previewTraits?.Effects ?? PermanentTraits(person);
            int command = ArmyCommandAgeEffectSource.ReadResetPreview(state, current, person,
                commanderOnly: !modifiers, unconditionalEffects: traits);
            int percent = FormationPercent(effectiveFormation);
            float regionFactor = 1f, neighborFactor = 1f;
            if (modifiers)
            {
                if (forceId is not (>= 46 and <= 51) &&
                    !(BattleRemakeCurrentRegionFactor.IsNativeBandPerson(id) && first is >= 1 and <= 61))
                {
                    var region = new BattleRemakeCurrentRegionFactor(_regionSeed, tacticContext, current, Revision,
                        new DeploymentPreviewContextFactors.RegionPolicies(current));
                    regionFactor = region.ResolveHexForceReceipt(state, forceId, (ushort)hexId)?.Factor
                        ?? throw FigureMissing("preview_current_region");
                }
                neighborFactor = DeploymentPreviewContextFactors.ReadEmptyNeighborFactor(state, current, person, hexId);
            }
            var moraleCoefficients = F(BattleRemakeCurrentSettings.ReadCommonMoraleCoefficients(state), "morale_settings");
            float common = BattleUnitSourceMath.CommonFactor(new(full.Leadership, troops, 1, morale,
                modifiers, false, moraleCoefficients.Offset, moraleCoefficients.Divisor,
                regionFactor, BattleUnitSourceMath.FormationTerrainFactor(effectiveFormation, terrainCode,
                    P(SimulationParameterKeys.BattleAttributes.Formation6TerrainPercent), P(SimulationParameterKeys.BattleAttributes.Formation14TerrainPercent), 100f), neighborFactor),
                new(_battleSeed.CommonPowerExponent, P(SimulationParameterKeys.BattleAttributes.CommonRootDivisor),
                    P(SimulationParameterKeys.BattleAttributes.CommonLeadershipScale), P(SimulationParameterKeys.BattleAttributes.CommonAdditive),
                    _battleSeed.FlagNumerator, _battleSeed.FlagDenominator, P(SimulationParameterKeys.BattleAttributes.CommonDivisorNumerator), P(SimulationParameterKeys.BattleAttributes.CommonDivisorOffset)));
            int attackPolicy = Policy(0x28);
            if (attackPolicy > 0) throw FigureMissing("positive_attack_policy_eligibility");
            float attack = Stat(BattleUnitArmyStatKind.FieldAttack, traits[5], 0,
                SimulationParameterKeys.BattleAttributes.AttackAbilityLimit, SimulationParameterKeys.BattleAttributes.AttackAbilityStep, SimulationParameterKeys.BattleAttributes.AttackCoefficientScale);
            int siegeBonus = (forceFlags & 2) != 0 && explicitFormation == 5 ? P(SimulationParameterKeys.BattleAttributes.SiegeAbilityBonus) : 0;
            float siege = Stat(BattleUnitArmyStatKind.SiegeTroopAttack, traits[6], siegeBonus,
                SimulationParameterKeys.BattleAttributes.SiegeAbilityLimit, SimulationParameterKeys.BattleAttributes.SiegeAbilityStep, SimulationParameterKeys.BattleAttributes.AttackCoefficientScale);
            int wallBonus = (forceFlags & 2) != 0 && explicitFormation == 5 ? P(SimulationParameterKeys.BattleAttributes.WallAbilityBonus) : 0;
            float wall = BattleUnitSourceMath.ArmyWallAttack(new(full.Leadership, troops,
                Coefficient(BattleUnitArmyStatKind.WallAttack), wallBonus, percent, true, command, traits[8], traits[19], modifiers, 0),
                new(P(SimulationParameterKeys.BattleAttributes.WallTroopOffset), P(SimulationParameterKeys.BattleAttributes.WallTroopDenominator), P(SimulationParameterKeys.BattleAttributes.WallLeadershipDenominator),
                    new(P(SimulationParameterKeys.BattleAttributes.WallAbilityLimit), P(SimulationParameterKeys.BattleAttributes.WallAbilityStep), P(SimulationParameterKeys.BattleAttributes.WallAbilityStep), 0, 100f, 1f)));
            int defenseBonus = (forceFlags & 0x20) != 0 && explicitFormation == 7 ? P(SimulationParameterKeys.BattleAttributes.Formation7ForceFlagBonus) : 0;
            float defense = Stat(BattleUnitArmyStatKind.Defense, traits[7], defenseBonus,
                SimulationParameterKeys.BattleAttributes.DefenseAbilityLimit, SimulationParameterKeys.BattleAttributes.DefenseAbilityStep, SimulationParameterKeys.BattleAttributes.DefenseCoefficientScale);
            return new(BattleUnitSourceMath.TruncateSingleToInt32(attack), BattleUnitSourceMath.TruncateSingleToInt32(siege),
                BattleUnitSourceMath.TruncateSingleToInt32(wall), BattleUnitSourceMath.TruncateSingleToInt32(Mobility()), BattleUnitSourceMath.TruncateSingleToInt32(defense));

            int FormationPercent(int formation, bool transportStat = true)
            {
                if (formation is >= 14 and <= 16 || transportStat && formation is 19 or 20 || forceId is >= 46 and <= 51) return 100;
                int effect = formation <= 7 ? formation : formation <= 10 ? 8 : formation <= 13 ? 9 : 12;
                int level = ForcePolicyScratchCurrent.ReadBeforeFreedom(state, current, forceReference, effect, true).Value;
                return unchecked(100 + unchecked(level * P(BattleRemakeCurrentParameterSeed.FormationEffectKey(effect)!)));
            }
            int Policy(int effect)
            {
                if (!F(current.ReadForceNativeValidity(forceReference), "force_validity")) return 0;
                if (!F(force.PolicyEffects?.IsEnabled(effect), "policy_activation")) return 0;
                return F(current.BaseForceModifierReader?.Invoke(forceReference, effect), "active_policy");
            }
            int Coefficient(BattleUnitArmyStatKind kind)
            {
                var value = BattleRemakeCurrentFormationCoefficientQueries.Read(state, explicitFormation, kind)
                    ?? throw FigureMissing("current_formation_coefficient");
                return BattleUnitSourceMath.FormationCoefficient(kind, value.NativeContainerByteCount, value.StaticField, value.EncodedQueryResult);
            }
            float Stat(BattleUnitArmyStatKind kind, int effect, int bonus, string limit, string step, string scale) =>
                BattleUnitSourceMath.ArmyStat(kind, new(common, Coefficient(kind), bonus, percent, true, command, effect, traits[19], modifiers, 0, false, 0),
                    new(P(limit), P(step), P(step), P(scale), 100f, 1f));
            float Mobility()
            {
                var values = state.NativeArmySimulation.Parameters.Int32Values;
                if (values.GetValueOrDefault(TacticRemakeCurrentBattleControl.VersionKey) != 1 ||
                    !values.TryGetValue(TacticRemakeCurrentBattleControl.HeaderKey, out int tutorial) || tutorial != 0)
                    throw FigureMissing("mobility_tutorial");
                int bytes = F(TacticRemakeCurrentFormations.ReadVectorByteCount(state, explicitFormation), "formation_vector");
                int raw = F(TacticRemakeCurrentFormations.ReadParameter(state, explicitFormation, 0x7F), "mobility_coefficient");
                int coefficient = (bytes & ~3) == 32 ? Math.Clamp(raw, 1, 30) : raw;
                if ((forceFlags & 2) != 0 && explicitFormation == 5) coefficient = unchecked(coefficient + P(SimulationParameterKeys.ArmyMobility.FormationFiveMobilityBonus));
                int trait = unchecked(traits[4] + traits[19] + command);
                int limit = P(SimulationParameterKeys.ArmyMobility.TraitMobilityLimit);
                int capped = trait < -limit ? -limit : trait > limit ? limit : trait;
                float value = (float)unchecked(P(SimulationParameterKeys.ArmyMobility.TraitMobilityStep) * capped) / 100f + 1f;
                value *= (float)unchecked(FormationPercent(effectiveFormation, transportStat: false) * coefficient) / 100f;
                var baseline = current.ReadSurfaceTypeTableReference(1) ?? throw FigureMissing("baseline_terrain");
                int cost = F(current.ReadSurfaceTypeRawField2A(baseline), "baseline_cost");
                // The explicit baseline receiver is physical1, outside the
                // source's advanced-cost set3..14/24. No adjustment is reached.
                if (cost > 0) value *= _mobilitySeed.BaselineTerrainNormalization / (float)cost;
                if (explicitFormation is 6 or 14 && terrainCode is 5 or 6 or >= 8 and <= 11)
                    value *= (float)P(explicitFormation == 6 ? SimulationParameterKeys.ArmyMobility.FormationSixTerrainPercent : SimulationParameterKeys.ArmyMobility.FormationTerrainPercent) / 100f;
                byte flags = F(TacticRemakeCurrentFormations.ReadParameter(state, explicitFormation, 0xA6), "formation_flags");
                int? policy = (flags & 2) != 0 ? 59 : terrainCode is >= 8 and <= 11 ? 60 : null;
                if (policy is int effect) value *= (float)Policy(effect) / 100f + 1f;
                return value;
            }
        }
        int[] PermanentTraits(int person)
        {
            var values = new int[127];
            for (int index = 0; index < 9; index++)
            {
                var trait = TacticRemakeCurrentTraits.ResolveCommanderPersonality(current!, person, index) ?? throw FigureMissing("personality_pointer");
                if (!F(current!.ReadSkillPersonalityValidity(trait), "personality_validity")) continue;
                if (F(current.ReadPersonalityWord(trait, 0xB4), "personality_condition") != ushort.MaxValue) continue;
                for (int pair = 0; pair < 2; pair++)
                {
                    byte effect = F(current.ReadPersonalityByte(trait, pair == 0 ? 0xB6 : 0xBA), "personality_effect");
                    short amount = unchecked((short)F(current.ReadPersonalityWord(trait, pair == 0 ? 0xB8 : 0xBC), "personality_value"));
                    if (effect < 127) values[effect] = unchecked(values[effect] + Math.Clamp((int)amount, -100, 2000));
                }
            }
            return values;
        }
    }

    private static T F<T>(T? value, string input) where T : struct => value ?? throw FigureMissing(input);
    private static InvalidDataException FigureMissing(string input) => new($"deployment.figures.{input}");
}
