using San14.Core;

namespace San14.Data;

public sealed record OriginalCaptureApplicationOwners(bool? TaskScenePresent, sbyte? WorldTaskBC = null);
public sealed record OriginalPersonArmyCaptureReceipt(int DefeatedPersonId, int WinnerPersonId,
    int? DefeatedArmyPhysical, int? WinnerArmyPhysical, int? DefeatedFormation, int? WinnerFormation,
    int DefeatedForcePhysical, int WinnerForcePhysical, int OwnedCityCount, bool DefeatedAtBase,
    int ThreatCount, int? BaseProbability, int? WinnerTrait39, int? Probability,
    int? Sample, sbyte? WorldHour38, string Branch);
public sealed record OriginalPersonArmyCaptureResult(bool? Captured,
    OriginalPersonArmyCaptureReceipt? Receipt, string? UnresolvedOwner);

/// <summary>TC27E870 capture predicate and source-created current inputs.
/// It mutates only temporary lists in the caller's operation-local pool;
/// successful custody/job callbacks2E0FC0 are a separate consumer.</summary>
public static class OriginalPersonArmyCaptureSource
{
    public const string Revision = "san14:ce-person-Army-capture-source-20261007";
    public static OriginalPersonArmyCaptureResult Evaluate(GameState state, OriginalEventInitializationContext current,
        int defeatedPhysical, OriginalTransferPersonReference winnerPerson, IEffectiveOfficerAbilities abilities,
        NativeMapSeasonDate? operationDate, OriginalTacticSourceSamplerWorld? sampler,
        OriginalCaptureApplicationOwners application)
    {
        int defeatedId = 0, winnerId = 0, defeatedForce = 0, winnerForce = 0, cities = 0, threats = 0;
        bool atBase = false; int? ownArmyPhysical = null, winnerArmyPhysical = null, ownFormation = null, winnerFormation = null;
        try {
            bool scene = Need(application.TaskScenePresent, "current.capture_TaskScene1D3840");
            if (scene && Need(application.WorldTaskBC, "current.capture_WorldBC_signed_task_index") != -1) return Result(false, "task-active");
            if (state.NativeArmySimulation?.Parameters.Int32Values is not { } bank ||
                bank.GetValueOrDefault(OriginalTacticRemakeCurrentBattleControl.VersionKey) != 1 ||
                !bank.TryGetValue(OriginalTacticRemakeCurrentBattleControl.HeaderKey, out int tutorial))
                throw Missing("current.Tutorial_header0");
            if (tutorial != 0) return Result(false, "tutorial");
            int captureMode = Need(OriginalBattleRemakeCurrentSettings.ReadInt32(state, 0x108), "current.Environment_Capture108");
            if (captureMode == 2) return Result(false, "capture-disabled");
            defeatedId = Need(current.ReadPersonIdentity(defeatedPhysical), "current.defeated_Person10");
            ushort first = Need(current.ReadPersonFirstLocation(defeatedPhysical), "current.defeated_PersonE2");
            var ownForce = current.ReadPersonForce(defeatedPhysical) ?? throw Missing("current.defeated_Force22CB40");
            if (ownForce.PhysicalSlot is int ownRaw) defeatedForce = ownRaw;
            //233240(0),232A50,232530 have different first-location gates.
            if (OriginalBattleRemakeCurrentRegionFactor.IsNativeBandPerson(defeatedId) && first is >= 1 and <= 61 &&
                defeatedForce is not (>= 46 and <= 50)) return Result(false, "ordinary-band-person");
            if (defeatedForce is >= 46 and <= 50) return Result(false, first is >= 1 and <= 61 ? "special-base-person" : "special-remote-person");
            if (winnerPerson.PhysicalSlot is not int winnerPhysical ||
                !Need(current.ReadPersonNativeValidity(winnerPhysical), "current.winner_Person_validity")) return Result(false, "invalid-winner");
            winnerId = Need(current.ReadPersonIdentity(winnerPhysical), "current.winner_Person10");
            var winningForce = current.ReadPersonForce(winnerPhysical) ?? throw Missing("current.winner_Force22CB40");
            if (winningForce.PhysicalSlot is not int win) return Result(false, "null-winner-force");
            winnerForce = win;
            if (win is >= 46 and <= 51 || !Need(current.ReadForceNativeValidity(winningForce), "current.winner_Force_validity"))
                return Result(false, "invalid-or-special-winner-force");
            if (!Need(current.ReadPersonNativeValidity(defeatedPhysical), "current.defeated_Person_validity") ||
                ownForce.PhysicalSlot is null || defeatedForce is >= 46 and <= 51 ||
                !Need(current.ReadForceNativeValidity(ownForce), "current.defeated_Force_validity")) return Result(false, "invalid-or-special-defeated-force");
            cities = CountOwnedCities(current, ownForce);
            if (cities == 0) return Result(false, "no-owned-city");
            ushort second = Need(current.ReadPersonSecondLocation(defeatedPhysical), "current.defeated_PersonE4");
            var position = OriginalPersonPositionRepair.DecodeSecondLocation(second);
            atBase = position.Kind is 5 or 6;
            var winningArmy = PersonArmy(current, winnerPhysical);
            bool winnerArmyValid = Need(current.ReadArmyUnitNativeValidity(winningArmy), "current.winner_Army_validity");
            if (winnerArmyValid) {
                winnerArmyPhysical = winningArmy.PhysicalSlot;
                winnerFormation = EffectiveFormation(state, current, winningArmy);
                if (winnerFormation == 3 && !atBase) return Result(false, "winner-formation3-remote");
            }
            if (atBase && cities == 1) return Result(true, "last-base-guaranteed");
            var immune = FirstPersonTrait(current, defeatedPhysical, 0x36);
            if (immune.PhysicalSlot.HasValue && Need(current.ReadPersonalityWord(immune, 0xB4), "current.effect36_Personality_B4") == ushort.MaxValue)
                return Result(false, "personal-escape-immunity");
            var ownArmy = PersonArmy(current, defeatedPhysical);
            bool ownArmyValid = Need(current.ReadArmyUnitNativeValidity(ownArmy), "current.defeated_Army_validity");
            if (ownArmyValid) {
                ownArmyPhysical = ownArmy.PhysicalSlot;
                int radius = P(state, SimulationParameterKeys.ArmyDestruction.PersonalityDefaultRadius);
                var list = current.CreateNearbyTemporary();
                try {
                    _ = OriginalNearbyArmyTemporarySource.FillNearbyArmyTemporary(current, ownArmy, radius, list);
                    var node = current.ReadRegistryHead(new(list.Identity)) ?? throw Missing("current.protector_Army_head");
                    while (node.Identity.HasValue) {
                        var payload = current.ReadRegistryNodePayload(node) ?? throw Missing("current.protector_Army_payload");
                        if (payload.Kind != OriginalRegistryKind.Army) throw new InvalidDataException("Protector list has an unsafe payload kind.");
                        var protector = new OriginalArmyUnitReference(payload.PhysicalSlot);
                        if (Need(current.ReadArmyUnitNativeValidity(protector), "current.protector_Army_validity") && protector.PhysicalSlot != ownArmy.PhysicalSlot &&
                            current.ReadArmyUnitForceReference(protector) == ownForce) {
                            int commander = ArmyCommander(current, protector);
                            var trait = FirstPersonTrait(current, commander, 0x36);
                            if (trait.PhysicalSlot is int physical) {
                                if (physical is < 256 or > 355) {
                                    if (Need(current.ReadPersonalityByte(trait, 0xA4), "current.protector_Personality_A4") == 9)
                                        return Result(false, "nearby-personal-escape-protector");
                                }
                                else {
                                    //The custom branch obtains both current Hexes even
                                    //when its later category9 predicate is false.
                                    var date = operationDate ?? throw Missing("current.operation_date_for_Person_hex");
                                    int otherHex = Hex(current, OriginalPersonReturnCommands.ReadPersonCurrentHex(state, current, commander, date));
                                    int ownHex = Hex(current, OriginalPersonReturnCommands.ReadPersonCurrentHex(state, current, defeatedPhysical, date));
                                    if (Need(current.ReadPersonalityByte(trait, 0xA4), "current.protector_Personality_A4") == 9 &&
                                        Distance(ownHex, otherHex) <= TraitRadius(state, current, trait))
                                        return Result(false, "nearby-custom-escape-protector");
                                }
                            }
                        }
                        node = current.ReadRegistryNodeNext(node) ?? throw Missing("current.protector_Army_next");
                    }
                }
                finally { current.ClearNearbyTemporary(list, true); }
            }
            OriginalPersonArmyCaptureInputs.BindCurrent(state, current);
            if (defeatedId is >= 1 and <= 6000) {
                var items = current.ReadRegistryReferences(OriginalRegistryKind.Item) ?? throw Missing("current.Item_registry110");
                foreach (var item in items) {
                    if (item?.Kind != OriginalRegistryKind.Item || item.PhysicalSlot is not int physical) throw Missing("current.Item_registry_payload");
                    if (Need(current.ReadCaptureItemOwner4A(physical), "current.Item_owner4A") != defeatedId) continue;
                    if (Need(current.ReadCaptureItemSubtype47(physical), "current.Item_subtype47") == 1) return Result(false, "owned-mount");
                }
            }
            int center = 48400, ring = 1;
            if (ownArmyValid) center = Need(current.ReadArmyUnitRawHex2A(ownArmy), "current.defeated_Army2A");
            else if (atBase) {
                var home = position.Kind == 5 ? current.ReadCityTableReference(position.Index <= 51 ? position.Index : 0) :
                    current.ReadGateTableReference(position.Index <= 10 ? position.Index : 0);
                if (home is null) throw Missing("current.Person_Base_pointer");
                if (Need(current.ReadBaseNativeValidity(home), "current.Person_Base_validity")) {
                    center = current.ReadBaseCurrentHexIndex(home); ring = position.Kind == 5 ? 2 : 1;
                }
            }
            var hexes = current.CreateNearbyTemporary();
            try {
                FillOuterRing(current, center, ring, hexes);
                var node = current.ReadHexListHead(hexes) ?? throw Missing("current.capture_outer_ring_head");
                while (node.Identity.HasValue) {
                    var hex = current.ReadHexListNodePayload(node) ?? throw Missing("current.capture_outer_ring_payload");
                    int raw = unchecked((short)Need(current.ReadHexRaw1C(hex), "current.capture_ring_Hex1C"));
                    if (raw is >= 1 and <= 500 && Threatens(state, current, Hex(current, hex), center)) threats++;
                    node = current.ReadHexListNodeNext(node) ?? throw Missing("current.capture_outer_ring_next");
                }
            }
            finally { current.ClearNearbyTemporary(hexes, true); }
            var officer = state.Officers.GetValueOrDefault(defeatedId) ?? throw Missing("current.defeated_Officer_ability_owner");
            var native = officer.NativeAbilityRuntime ?? throw Missing("current.defeated_ability_runtime");
            byte health = Need(current.ReadArmyReturnPersonByte199(defeatedPhysical), "current.defeated_health161");
            var abilityState = native.Health199 == health ? state : state with { Officers = new(state.Officers) {
                [defeatedId] = officer with { NativeAbilityRuntime = native with { Health199 = health } } } };
            var effective = abilities.Resolve(abilityState, defeatedId) ?? throw Missing("current.defeated_22FE40_effective_abilities");
            int maxAbility = Math.Max(effective.Intelligence, effective.Strength);
            float abilityTerm = (float)unchecked((P(state, SimulationParameterKeys.ArmyDestruction.CaptureAbilityBaseline) - maxAbility + 5) * P(state, SimulationParameterKeys.ArmyDestruction.CaptureAbilityPercent)) / 100f;
            float floor = Math.Max(5f, abilityTerm);
            float scaled = (float)unchecked(Math.Max(1, threats) + P(state, SimulationParameterKeys.ArmyDestruction.CaptureThreatAdjustment)) * floor;
            scaled = scaled * (float)P(state, SimulationParameterKeys.ArmyDestruction.CaptureThreatPercent);
            scaled = scaled / 100f;
            int probability = Math.Min(100, Cvtt(scaled));
            if (ownArmyValid) {
                ownFormation = EffectiveFormation(state, current, ownArmy);
                if (ownFormation == 4) probability = unchecked(probability * P(state, SimulationParameterKeys.ArmyDestruction.CaptureFormationFourPercent)) / 100;
            }
            probability = Math.Clamp(probability, 0, 100);
            int selector = Need(OriginalBattleRemakeCurrentSettings.ReadSelectorE58(state), "current.EnvironmentE58");
            if (selector != 0) {
                if (Need(OriginalBattleRemakeCurrentSettings.ReadInt32(state, 0x28), "current.Environment_difficulty28") > 2 && winnerArmyValid &&
                    Need(current.ReadForceNativeValidity(winningForce), "current.reached_winner_Force_validity") && Player(state, current, winningForce))
                    probability = unchecked(probability * P(state, SimulationParameterKeys.ArmyDestruction.HighDifficultyPlayerWinnerCapturePercent)) / 100;
                probability = Math.Clamp(probability, 0, 100);
                if (captureMode == 1) probability = unchecked(probability * P(state, SimulationParameterKeys.ArmyDestruction.CaptureModeOnePercent)) / 100;
                byte status = Need(current.ReadPersonStatus(defeatedPhysical), "current.defeated_PersonE6");
                if (status is 1 or 2) probability = unchecked(probability * P(state, SimulationParameterKeys.ArmyDestruction.CaptureRulerOfficerPercent)) / 100;
                probability = Math.Clamp(probability, 0, 100);
            }
            var reachedDate = operationDate ?? throw Missing("current.operation_World38_hour");
            if (sampler is null) throw Missing("current.operation_World450_45C");
            int draw = OriginalTacticEffectSourceSamplerMath.Sample(100, 1, defeatedId, winnerForce, reachedDate.Hour, 0, sampler);
            int winnerTrait = Need(OriginalTacticRemakeCurrentTraits.CommanderEffectSum(current, winnerPhysical, 0x39), "current.winner_Person_effect39");
            int total = unchecked(probability + winnerTrait);
            if (selector == 0) total = unchecked(total * Need(OriginalBattleRemakeCurrentSettings.ReadAdvancedParameter(state,
                Player(state, current, winningForce) ? 15 : 16), "current.advanced_capture_percent15_or16")) / 100;
            return Result(draw < total, "sampled", probability, winnerTrait, total, draw, reachedDate.Hour);
        }
        catch (MissingOwner missing) { return new(null, null, missing.Message); }
        catch (InvalidDataException missing) { return new(null, null, missing.Message); }

        OriginalPersonArmyCaptureResult Result(bool captured, string branch, int? baseProbability = null,
            int? trait = null, int? probability = null, int? draw = null, sbyte? hour = null) => new(captured,
            new(defeatedId, winnerId, ownArmyPhysical, winnerArmyPhysical, ownFormation, winnerFormation,
                defeatedForce, winnerForce, cities, atBase, threats, baseProbability, trait, probability, draw, hour, branch), null);
    }

    private static int CountOwnedCities(OriginalEventInitializationContext current, OriginalForceReference force)
    {
        //22AB60→2243A0 uses the complete pointer table, NOT registry90.
        //3347B0 compares City virtualB0 without a native validity predicate.
        if (force.PhysicalSlot is not (>= 1 and <= 51)) return 0;
        int count = 0;
        for (int slot = 0; slot < 52; slot++) {
            var city = current.ReadCityTableReference(slot) ?? throw Missing("current.Force_City_pointer_table");
            if (Need(current.ReadBaseForceSlot(city), "current.City_ForceB0") == force.PhysicalSlot) count++;
        }
        return count;
    }
    private static OriginalArmyUnitReference PersonArmy(OriginalEventInitializationContext current, int person)
    {
        ushort second = Need(current.ReadPersonSecondLocation(person), "current.PersonE4_for22A200");
        int slot = second is >= 62 and <= 561 ? second - 61 : 0;
        var army = current.ReadArmyUnitTableReference(slot) ?? throw Missing("current.Person_Army_pointer");
        return Need(current.ReadArmyUnitCommanderId(army), "current.Person_Army_commander12") == Need(current.ReadPersonIdentity(person), "current.Person10_for22A200")
            ? army : current.ReadArmyUnitTableReference(0) ?? throw Missing("current.Army0_pointer");
    }
    private static int ArmyCommander(OriginalEventInitializationContext current, OriginalArmyUnitReference army)
    {
        ushort id = Need(current.ReadArmyUnitCommanderId(army), "current.Army_commander12");
        var person = id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0);
        return person?.PhysicalSlot ?? throw Missing("current.Army_commander_Person_pointer");
    }
    private static UnitState ArmyImage(GameState state, OriginalArmyUnitReference army)
    {
        if (army.PhysicalSlot is not int physical || state.NativeArmyLifecycle is not { Slots.Length: 501 } arena) throw Missing("current.saved_Army_image");
        var slot = arena.Slots[physical];
        return (slot.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) : slot.PendingImage) ?? throw Missing("current.saved_Army_image");
    }
    private static int EffectiveFormation(GameState state, OriginalEventInitializationContext current, OriginalArmyUnitReference army)
    {
        int raw = Need(current.ReadArmyUnitRawHex2A(army), "current.Army_Hex2A_for22B2D0");
        var hex = current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw Missing("current.formation_Hex_pointer");
        byte surface = Need(current.ReadHexSurfaceCode(hex), "current.formation_Hex10");
        var terrain = current.ReadSurfaceTypeTableReference(surface <= 30 ? surface : 0) ?? throw Missing("current.formation_Terrain_pointer");
        var config = ArmyImage(state, army).NativeArmyConfiguration ?? throw Missing("current.Army_configuration");
        bool water = terrain.PhysicalSlot is int physical && (Need(OriginalPersonArmyCaptureInputs.ReadTerrainFlags2C(state, physical), "current.Terrain2C") & 2) != 0;
        return Need(water ? config.Formation1C : config.Formation1B, "current.Army_effective_formation1B_or1C");
    }
    private static OriginalPersonalityReference FirstPersonTrait(OriginalEventInitializationContext current, int person, byte effect)
    {
        if (!Need(current.ReadPersonNativeValidity(person), "current.Person_validity_for2AB720")) return new(null);
        for (int i = 0; i < 9; i++) {
            var trait = OriginalTacticRemakeCurrentTraits.ResolveCommanderPersonality(current, person, i) ?? throw Missing("current.Person_trait230730");
            if (!Need(current.ReadSkillPersonalityValidity(trait), "current.Personality_validity")) continue;
            if (Need(current.ReadPersonalityByte(trait, 0xB6), "current.Personality_B6") == effect ||
                Need(current.ReadPersonalityByte(trait, 0xBA), "current.Personality_BA") == effect) return trait;
        }
        return new(null);
    }
    private static int TraitRadius(GameState state, OriginalEventInitializationContext current, OriginalPersonalityReference trait)
    {
        int raw = 0; for (int i = 0; i < 4; i++) raw |= Need(current.ReadPersonalityByte(trait, 0xA8 + i), "current.custom_Personality_A8") << (8 * i);
        return raw >= 1 ? raw : P(state, SimulationParameterKeys.ArmyDestruction.PersonalityDefaultRadius);
    }
    private static void FillOuterRing(OriginalEventInitializationContext current, int center, int radius, OriginalHexListHandle list)
    {
        current.ClearNearbyTemporary(list, false);
        foreach (int key in OuterRingHexes(center, radius)) {
            var hex = current.ReadHexTableReference(key) ?? throw Missing("current.capture_ring_Hex_pointer");
            if (hex.PhysicalSlot.HasValue) current.AppendNearbyHexTemporary(list, hex);
        }
    }
    private static IEnumerable<int> OuterRingHexes(int center, int radius)
    {
        //2B4C70 derives x/y from the raw center, including the known48400
        //sentinel used when a valid Person has neither Army nor Base position.
        //It clips each produced candidate instead of substituting Hex0.
        (int X, int Y)[][] directions =
        [
            [(-1, 0), (0, -1), (1, 0), (-1, 1), (0, 1), (1, 1)],
            [(-1, -1), (0, -1), (1, -1), (-1, 0), (0, 1), (1, 0)]
        ];
        int x = center % 220, y = center / 220;
        for (int step = 0; step < radius; step++) Step(4);
        foreach (int direction in new[] { 2, 3, 6, 5, 4, 1 })
            for (int step = 0; step < radius; step++) {
                Step(direction);
                if ((uint)x < 220 && (uint)y < 220) yield return y * 220 + x;
            }
        void Step(int direction)
        {
            var delta = directions[x & 1][direction - 1];
            x = unchecked(x + delta.X); y = unchecked(y + delta.Y);
        }
    }
    private static bool Threatens(GameState state, OriginalEventInitializationContext current, int candidateHex, int center)
    {
        var hex = current.ReadHexTableReference(candidateHex) ?? throw Missing("current.threat_Hex_pointer");
        ushort link = Need(current.ReadHexObjectLink(hex), "current.threat_Hex1A");
        if (link is >= 1 and <= 61)
        {
            var baseThreat = OriginalCaptureBaseThreatSource.Evaluate(state, current, candidateHex, center);
            return Need(baseThreat.Threatens, baseThreat.UnresolvedOwner ?? "current.2CA7B0_Base_threat");
        }
        //Non-City/Gate objects fall through to the same Army branch.
        int slot = unchecked((short)Need(current.ReadHexRaw1C(hex), "current.threat_Hex1C"));
        var army = current.ReadArmyUnitTableReference(slot <= 500 ? slot : 0) ?? throw Missing("current.threat_Army_pointer");
        var image = ArmyImage(state, army);
        var runtime = image.NativeTacticRuntime ?? throw Missing("current.threat_Army_abnormal_fields");
        if (runtime.Abnormal23 != 0) return false;
        if (runtime.Abnormal25 != 0) throw Missing("branch.2CA7B0_Army_forced_target3C3E");
        int formation = EffectiveFormation(state, current, army);
        int definition = formation <= 20 ? formation : 0;
        byte extended = Need(OriginalTacticRemakeCurrentFormations.ReadParameter(state, definition, 0xA6), "current.threat_Formation_A6");
        var attackingForce = current.ReadArmyUnitForceReference(army);
        if (attackingForce.PhysicalSlot is not int attacker) throw Missing("current.threat_Force_pointer");
        int radius = (extended & 1) != 0 ? 2 : 1;
        byte flags = Need(state.NativeArmySimulation?.Forces.GetValueOrDefault(attacker)?.Flags193, "current.threat_Force193");
        if (flags >= 128 && definition == 10) radius = 2;
        int quantity = Need(current.ReadArmyReturnResourceFields(army)?.Ready16, "current.threat_Army_ready16");
        var target = current.ReadHexTableReference(center) ?? throw Missing("current.threat_target_Hex_pointer");
        ushort targetLink = Need(current.ReadHexObjectLink(target), "current.threat_target_Hex1A");
        int otherForce, targetHex = center;
        if (targetLink is >= 1 and <= 61) {
            var obj = current.ReadObjectTableReference(targetLink) ?? throw Missing("current.threat_target_Object_pointer");
            otherForce = Need(current.ReadObjectForceSlot13(obj), "current.threat_target_Object13");
            targetHex = Need(current.ReadObjectHexIndex(obj), "current.threat_target_Object10");
            if (targetLink <= 51) radius++;
        }
        else {
            if (targetLink != 0) throw Missing("branch.2CA7B0_nonbase_Object_target_definition");
            int targetSlot = unchecked((short)Need(current.ReadHexRaw1C(target), "current.threat_target_Hex1C"));
            if (targetSlot is < 1 or > 500) return false;
            var targetArmy = current.ReadArmyUnitTableReference(targetSlot) ?? throw Missing("current.threat_target_Army_pointer");
            otherForce = current.ReadArmyUnitRawForceSlot(targetArmy);
        }
        return Distance(candidateHex, targetHex) <= radius && quantity > 0 && Need(
            OriginalArmyLogisticsPreparation.ReadNativeRelationGate(state, attacker, otherForce), "current.threat_relation2CA650");
    }
    private static bool Player(GameState state, OriginalEventInitializationContext current, OriginalForceReference force)
    {
        var player = OriginalOwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("current.World_player_Force3A");
        return player.PhysicalSlot is int physical && physical != 0 && physical == force.PhysicalSlot;
    }
    private static int Distance(int first, int second)
    {
        int x1 = first % 220, y1 = first / 220, x2 = second % 220, y2 = second / 220;
        int dx = Math.Abs(x2 - x1), dy = Math.Abs(2 * (y2 - y1) - (x1 % 2 == 0 ? 1 : 0) + (x2 % 2 == 0 ? 1 : 0));
        return dy < dx ? dx : dx + (dy - dx) / 2;
    }
    private static int Hex(OriginalEventInitializationContext current, OriginalHexReference reference) =>
        Need(current.ReadHexPhysicalSlot(reference), "current.Person_Hex_physical");
    private static int P(GameState state, string key) => Need(OriginalArmyDestructionParameterSeed.ReadCurrent(state, key), $"current.capture_parameter{key}");
    private static int Cvtt(float value) => !float.IsFinite(value) || value < int.MinValue || value >= 2147483648f ? int.MinValue : (int)value;
    private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static MissingOwner Missing(string field) => new(field);
    private sealed class MissingOwner(string field) : Exception(field);
}
