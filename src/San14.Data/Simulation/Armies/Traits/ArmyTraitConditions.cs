using San14.Core;

namespace San14.Data;

/// <summary>The ordinary personality condition dispatcher and its 34 callbacks.
/// Mode1 appends actual receiver pointers through the current primary pool;
/// other modes retain the same reads and nested allocations without appending.
/// The caller owns, clears, and eventually releases the output descriptor.</summary>
internal static class ArmyTraitConditions
{
    internal static bool Collect(GameState state, SourceWorldContext current,
        ArmyUnitReference army, PersonalityReference trait, int mode, HexListHandle targets)
    {
        //27E360 checks exclusion before touching the personality. It does not
        //add a native-validity gate shared by all of the individual callbacks.
        if (Excluded(army)) return false;
        ushort commanderFilter = Need(current.ReadPersonalityWord(trait, 0xA2), "commander_filter");
        byte category = Byte(mode == 1 ? 0xA4 : 0xAC);
        int parameter = Int32(mode == 1 ? 0xA8 : 0xB0);
        //22C4E0 always reads the first condition group, including in mode2 and
        //when the selected callback is a null entry in the 101-slot table.
        int radius = Radius();
        switch (category)
        {
            case 1: return Self(Status() == 1);
            case 2: return CollectNearby(1, true, false);
            case 3: return CollectNearby(1, false, false);
            case 4: return Self((Runtime().Flags26 & 2) == 0);
            case 5:
                var runtime = Runtime();
                return Self(runtime.Abnormal23 != 0 || runtime.Abnormal24 != 0 || runtime.Abnormal25 != 0);
            case 6: return Self(Runtime().Abnormal23 != 0);
            case 7: return Self(Runtime().Abnormal24 != 0);
            case 8: return Self(Runtime().Abnormal25 != 0);
            case 9: return CollectNearby(radius, true, false);
            case 10: return CollectNearby(radius, false, false);
            case 11: return Self(Valid());
            case 12:
                if (!Valid()) return false;
                int land = LandFormation();
                return Self(land > 0 && (FormationFlags(land) & 2) != 0);
            case 13: return Self(Valid() && Status() == 2);
            case 14: return Self(Valid() && TerrainCategory() == 4);
            case 15: return Self(Valid() && TerrainCategory() == 3);
            case 16: return Self(Valid() && TerrainCategory() == 2);
            case 17: return Self(Valid() && TerrainCategory() is 3 or 5);
            case 18: return Self(Valid() && TerrainCategory() == 6);
            case 19: return Self(Valid() && TerrainCategory() != 4);
            case 20:
                return Self(Valid() && Need(current.ReadArmyReturnResourceFields(army)?.Ready16,
                    "ready_troops") <= parameter);
            case 21: case 22: case 23: case 24:
                if (!Valid() || Sex(army) != OfficerSex.Female) return false;
                return CollectNearby(category is 23 or 24 ? 1 : DefaultRadius(),
                    category is 21 or 23, true);
            case 25:
                return Self(Valid() && Action() is 11 or 12);
            case 26:
                if (!Valid()) return false;
                return WithNearby(1, candidates =>
                {
                    int count = 0;
                    //This callback has no candidate233560 or Force inequality
                    //gate.2A78D0 already checked native Army validity.
                    foreach (var other in candidates)
                        if (HostileTo(other)) count = unchecked(count + 1);
                    return Self(count >= ArmyTraitMaintenanceParameters.Require(state,
                        ArmyTraitMaintenanceParameters.AdjacentEnemyCountKey));
                });
            case 28:
                return Self(Valid() && Need(current.ReadHexSurfaceCode(Hex()), "surface_code") is >= 15 and <= 18);
            case 31: case 32:
                if (!Valid()) return false;
                return WithNearby(DefaultRadius(), candidates =>
                {
                    int count = 0;
                    foreach (var other in candidates)
                    {
                        if (Excluded(other)) continue;
                        //Retain both raw Force reads and the source direction
                        //of the additional enemy predicate for category32.
                        int otherForce = Force(other), ownForce = Force(army);
                        if (category == 31 ? otherForce == ownForce :
                            otherForce != ownForce && HostileTo(other))
                            count = unchecked(count + 1);
                    }
                    return Self(count >= ArmyTraitMaintenanceParameters.Require(state,
                        ArmyTraitMaintenanceParameters.NearbyArmyCountKey));
                });
            case 34:
                if (!Valid()) return false;
                ushort rawArea = Need(current.ReadHexAreaSlot(Hex()), "area_slot");
                var area = current.ReadAreaTableReference(rawArea <= 500 ? rawArea : 0)
                    ?? throw Missing("area_pointer");
                return Self(Need(current.ReadAreaCurrentForceSlot(area), "area_force") == Force(army));
            case 36: return Self(true);
            case 37:
                //Unlike12, this branch does not test formation validity.
                return Self(Valid() && TerrainCategory() != 4 &&
                    (FormationFlags(LandFormation()) & 2) == 0 && Status() == 1);
            case 38:
                if (!Valid()) return false;
                return WithNearby(DefaultRadius(), candidates =>
                {
                    bool accepted = false;
                    foreach (var other in candidates)
                    {
                        if (Excluded(other)) continue;
                        if (Force(other) == Force(army)) continue;
                        int raw = Force(other);
                        var force = current.ReadForceTableReference(raw <= 51 ? raw : 0)
                            ?? throw Missing("special_force_pointer");
                        int physical = Need(current.ReadForcePhysicalSlot(force), "special_force_identity");
                        if (physical is >= 46 and <= 50 && HostileTo(other))
                        { accepted = true; Append(other); }
                    }
                    return accepted;
                });
            case 41:
                if (!Valid() || Status() != 1) return false;
                int selected = CurrentFormation();
                if (selected > 0 && ((FormationFlags(selected) & 2) != 0 ||
                    Need(TacticRemakeCurrentFormations.ReadRawByte(state, selected, 0x7E), "formation_category") == 2))
                    return false;
                return Self(Action() is 11 or 12);
            default: return false;
        }

        byte Byte(int offset) => Need(current.ReadPersonalityByte(trait, offset), "personality_" + offset.ToString("X"));
        int Int32(int offset)
        {
            int value = 0;
            for (int index = 0; index < 4; index++) value |= Byte(offset + index) << (index * 8);
            return value;
        }
        int DefaultRadius() => Need(ArmyDestructionParameterSeed.ReadCurrent(state,
            SimulationParameterKeys.ArmyDestruction.PersonalityDefaultRadius), "default_radius");
        int Radius()
        {
            byte firstCategory = Byte(0xA4);
            if (firstCategory is 2 or 3 or 23 or 24) return 1;
            if (firstCategory is not (9 or 10 or 21 or 22 or 31 or 32 or 39 or 40)) return 0;
            if (trait.PhysicalSlot is >= 256 and <= 355)
            {
                int custom = Int32(0xA8);
                if (custom >= 1) return custom;
            }
            return DefaultRadius();
        }
        bool Valid() => Need(current.ReadArmyUnitNativeValidity(army), "army_validity");
        byte Status() => Need(current.ReadArmyReturnUnitStatus10(army), "army_status");
        bool Excluded(ArmyUnitReference selected) => Need(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(
            state, current, Need(selected.PhysicalSlot, "excluded_receiver")), "army_exclusion");
        int Force(ArmyUnitReference selected) => current.ReadArmyUnitRawForceSlot(selected);
        bool Hostile(int first, int second) => Need(ArmyLogisticsPreparation.ReadNativeRelationGate(state,
            first, second), "directed_relation");
        bool HostileTo(ArmyUnitReference other)
        {
            int otherForce = Force(other), ownForce = Force(army);
            return Hostile(ownForce, otherForce);
        }
        UnitState Image()
        {
            int physical = Need(army.PhysicalSlot, "army_receiver");
            var receiver = state.ReadArmyLifecycle()?.ReceiverSlots().ElementAtOrDefault(physical)
                ?? throw Missing("army_receiver_owner");
            return receiver.ActiveUnitId is int id
                ? state.Units.GetValueOrDefault(id) ?? throw Missing("active_army_image")
                : receiver.PendingImage ?? throw Missing("retained_army_image");
        }
        NativeArmyTacticRuntime Runtime() => Image().NativeTacticRuntime ?? throw Missing("army_abnormal_state");
        byte Action() => Need(ArmyConfigurationSourceAdapter.ReadAction(state, Image()), "army_action");
        HexReference Hex()
        {
            ushort raw = Need(current.ReadArmyUnitRawHex2A(army), "army_hex");
            return current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw Missing("hex_pointer");
        }
        SurfaceTypeReference Terrain(bool guardHexValidity)
        {
            var hex = Hex();
            //22B8C0 tests native Hex validity;22B220 directly reads Hex10.
            int slot = 0;
            if (!guardHexValidity || hex.PhysicalSlot.HasValue)
            {
                byte raw = Need(current.ReadHexSurfaceCode(hex), "surface_code");
                if (raw <= 30) slot = raw;
            }
            return current.ReadSurfaceTypeTableReference(slot) ?? throw Missing("terrain_pointer");
        }
        byte TerrainCategory() => Need(current.ReadSurfaceTypeWoundedCategory42(Terrain(true)), "terrain_category");
        int Formation(byte raw)
        {
            var formation = current.ReadStaticTableReference(0x77428, raw <= 20 ? raw : 0)
                ?? throw Missing("formation_pointer");
            return formation.PhysicalSlot ?? -1;
        }
        int LandFormation() => Formation(Need(ArmyConfigurationSourceAdapter.ReadCurrentLand(state, Image()), "land_formation"));
        int CurrentFormation()
        {
            var terrain = Terrain(false);
            bool water = terrain.PhysicalSlot is int physical &&
                (Need(PersonArmyCaptureInputs.ReadTerrainFlags2C(state, physical), "terrain_flags") & 2) != 0;
            byte raw = water ? Need(ArmyConfigurationSourceAdapter.ReadNaval(state, Image()), "naval_formation") :
                Need(ArmyConfigurationSourceAdapter.ReadCurrentLand(state, Image()), "land_formation");
            return Formation(raw);
        }
        byte FormationFlags(int physical) => Need(TacticRemakeCurrentFormations.ReadParameter(state,
            physical, 0xA6), "formation_flags");
        OfficerSex Sex(ArmyUnitReference selected)
        {
            ushort id = Need(current.ReadArmyUnitCommanderId(selected), "sex_commander");
            var person = (id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))
                ?? throw Missing("sex_person_pointer");
            int physical = Need(person.PhysicalSlot, "sex_person_receiver");
            int identity = Need(current.ReadPersonIdentity(physical), "sex_person_identity");
            if (current.ResolveOfficerSource(identity) != physical || !state.Officers.TryGetValue(identity, out var owner))
                throw Missing("sex_current_person_owner");
            return Need(owner.Sex, "person_sex");
        }
        bool Self(bool accepted)
        { if (accepted) Append(army); return accepted; }
        void Append(ArmyUnitReference selected)
        {
            if (mode != 1) return;
            RequirePool();
            var node = current.ReadRegistryHead(new(targets.Identity)) ?? throw Missing("targets_head");
            while (node.Identity.HasValue)
            {
                var value = current.ReadRegistryNodePayload(node) ?? throw Missing("targets_payload");
                if (value.Kind != RegistryKind.Army) throw Missing("targets_pointer_kind");
                if (value.PhysicalSlot == selected.PhysicalSlot) return;
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("targets_next");
            }
            //259140's boolean result belongs to the condition, not allocation.
            //A known null descriptor or failed node insertion does not undo it.
            current.AppendNearbyArmyTemporary(targets, selected);
        }
        void RequirePool()
        { if (current.ReadOwnedCurrentListPool() is null) throw Missing("primary_pool"); }
        T WithNearby<T>(int range, Func<IEnumerable<ArmyUnitReference>, T> consume)
        {
            RequirePool();
            var temporary = current.CreateNearbyTemporary();
            try
            {
                //2A7CD0's signed range<=0 loop is empty. Keep2A78D0's center
                //getter and both descriptor lifetimes through the shared helper.
                NearbyArmyTemporarySource.FillNearbyArmyTemporary(current, army, Math.Max(range, 0), temporary);
                return consume(Candidates());
                IEnumerable<ArmyUnitReference> Candidates()
                {
                    var node = current.ReadRegistryHead(new(temporary.Identity)) ?? throw Missing("nearby_head");
                    while (node.Identity.HasValue)
                    {
                        var value = current.ReadRegistryNodePayload(node) ?? throw Missing("nearby_payload");
                        if (value.Kind != RegistryKind.Army) throw Missing("nearby_pointer_kind");
                        yield return new(value.PhysicalSlot);
                        node = current.ReadRegistryNodeNext(node) ?? throw Missing("nearby_next");
                    }
                }
            }
            finally { current.ClearNearbyTemporary(temporary, true); }
        }
        bool CollectNearby(int range, bool nonhostile, bool maleOnly) => WithNearby(range, candidates =>
        {
            bool accepted = false;
            foreach (var other in candidates)
            {
                if (Excluded(other)) continue;
                int otherForce = Force(other), ownForce = Force(army);
                if (Hostile(ownForce, otherForce) == nonhostile) continue;
                if (maleOnly && Sex(other) != OfficerSex.Male) continue;
                if (commanderFilter is >= 1 and <= 6000 &&
                    Need(current.ReadArmyUnitCommanderId(other), "filtered_commander") != commanderFilter) continue;
                accepted = true; Append(other);
            }
            return accepted;
        });
    }

    private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("army.traits.condition." + field);
}
