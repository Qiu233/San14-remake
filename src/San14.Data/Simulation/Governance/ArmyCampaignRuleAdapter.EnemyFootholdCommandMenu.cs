using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>72C490/72C790 and759700/759830. The caller supplies whether
    /// the actual route packet has its actor context; no default is invented.</summary>
    public AreaAdministrationQuery<EnemyFootholdCommandMenuProjection> QueryEnemyFootholdCommandMenu(
        GameState state, BaseKind kind, int baseId, int? settledCameraLevel, bool? actorContextPresent)
    {
        try
        {
            return AreaAdministrationQuery<EnemyFootholdCommandMenuProjection>.Known(
                new EnemyFootholdMenuRead(this, state, kind, baseId, settledCameraLevel, actorContextPresent).Read());
        }
        catch (InvalidDataException missing)
        { return AreaAdministrationQuery<EnemyFootholdCommandMenuProjection>.Unknown(missing.Message); }
    }

    private sealed partial class EnemyFootholdMenuRead
    {
        private readonly ArmyCampaignRuleAdapter adapter;
        private readonly GameState state;
        private readonly SourceWorldContext current;
        private readonly BaseKind kind;
        private readonly int baseId, own, area;
        private readonly int? camera;
        private readonly bool? actor;
        private readonly BaseReference foothold;
        private readonly ForceReference player;
        private HashSet<int>? enemyNeighbors;
        private readonly Dictionary<int, int[]> neighbors = [];

        internal EnemyFootholdMenuRead(ArmyCampaignRuleAdapter adapter, GameState state,
            BaseKind kind, int baseId, int? camera, bool? actor)
        {
            this.adapter = adapter; this.state = state; this.kind = kind; this.baseId = baseId;
            this.camera = camera; this.actor = actor;
            if (kind == BaseKind.City ? baseId is < 1 or > 51 : kind != BaseKind.Gate || baseId is < 52 or > 61)
                throw Missing("Base_identity");
            current = adapter.CaptureOwnedArmyReturnWorld(state).Context;
            foothold = kind == BaseKind.City ? current.ReadCityTableReference(baseId) ?? throw Missing("City_pointer")
                : current.ReadGateTableReference(baseId - 51) ?? throw Missing("Gate_pointer");
            if (!K(current.ReadBaseNativeValidity(foothold), "Base_validity")) throw Missing("Base_invalid");
            player = current.ReadCurrentPlayerForce() ?? throw Missing("player_pointer");
            own = K(player.PhysicalSlot, "player_identity");
            area = K(PersonFreedomHomeSource.FindBaseArea229FD0(current, foothold).PhysicalSlot, "Base_Area");
        }

        internal EnemyFootholdCommandMenuProjection Read()
        {
            int owner = K(current.ReadBaseForceSlot(foothold), "Base_Force");
            var ownerForce = Force(owner);
            var targetPresent = Evaluate(() => K(actor, "actor_context") &&
                K(ArmyLogisticsPreparation.ReadNativeRelationGate(state, own, owner), "target_relation2CA650"));
            var strategicPresent = Evaluate(() => K(actor, "actor_context") && AreaAdmission(area, true));
            var targetEnabled = Evaluate(() => SceneryTargetCommandAvailable(state, current) && Zoom());
            var strategicEnabled = Evaluate(() =>
                (K(current.ReadWorldTransferServiceFlags(), "World16A8") & 0x100) == 0 && StrategicAvailable() && Zoom());
            var entries = new List<EnemyFootholdCommandMenuEntry>
            {
                new(EnemyFootholdCommandMenuCommand.UnitTarget, 0, 0x29A, targetPresent, targetEnabled),
                new(EnemyFootholdCommandMenuCommand.Strategic, 1, 0xA, strategicPresent, strategicEnabled),
                new(kind == BaseKind.City ? EnemyFootholdCommandMenuCommand.CityInformation
                    : EnemyFootholdCommandMenuCommand.GateInformation, 2, kind == BaseKind.City ? 0x27 : 0x5FA, Known(true), Known(true)),
                new(EnemyFootholdCommandMenuCommand.AreaInformation, 3, 0x5E, Known(true), Known(true))
            };
            if (kind == BaseKind.City)
                entries.Add(new(EnemyFootholdCommandMenuCommand.Government, 4, 0x29,
                    Known(owner is not (>= 46 and <= 50)), Evaluate(() => K(current.ReadForceNativeValidity(ownerForce), "Government_Force_validity"))));
            return new(kind, baseId, area, owner, current.ReadBaseCurrentHexIndex(foothold), entries.AsReadOnly());
        }

        private bool Zoom()
        {
            int level = K(camera, "settled_camera_level");
            if (level is < 0 or > 6) throw Missing("settled_camera_level_range");
            return level <= 5;
        }

        //70DF40: command books, actual treasury, a free executor and at least
        //one714060 territory. This does not query or construct an editor.
        private bool StrategicAvailable()
        {
            var primary = ForcePrimaryDistrictSource.Resolve(current, player);
            int district = K(primary.PhysicalSlot, "strategic_primary_District");
            int books = state.Districts.GetValueOrDefault(district)?.OrdersRemaining ?? throw Missing("strategic_District14");
            if (books - CommandBookAvailability.Reserved(state, district) < StrategicParameter(state, "kind-13.command-books", 1)) return false;
            int gold = BaseCurrencyAdditionSource.ReadCurrentGold(ReadPersonnelHireTreasuryCurrent(current, district), current);
            if (gold < adapter.ReadStrategicGoldCostCurrent(state, current, own, 13)) return false;
            if (!HasExecutor(district)) return false;
            foreach (var pointer in current.ReadRegisteredAreaReferences() ?? throw Missing("strategic_Area_B0"))
                if (AreaAdmission(K(pointer?.PhysicalSlot, "strategic_Area_pointer"), false)) return true;
            return false;
        }

        //710BD0's existence query shares the ordinary current Person leaves;
        //it never needs the later editor's Intelligence sort or recommendation.
        private bool HasExecutor(int district)
        {
            foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("executor_Person100"))
            {
                int person = K(entry, "executor_pointer");
                if (K(current.ReadPersonDistrictSlot(person), "executor_E0") != district ||
                    K(current.ReadPersonStatus(person), "executor_E6") is < 1 or > 4) continue;
                if (K(OfficerHealthSourceAdapter.ReadByte(current, person), "executor_health161") >= 2 ||
                    (K(current.ReadArmyReturnPersonFlags196(person), "executor_flags15E") & 1) != 0 ||
                    PersonCurrentBusySource.Read(current, person, Missing)) continue;
                int force = K((current.ReadPersonForce(person) ?? throw Missing("executor_Force_pointer")).PhysicalSlot, "executor_Force");
                if (force is >= 46 and <= 50) continue;
                int identity = K(current.ReadPersonIdentity(person), "executor_identity");
                if (BattleRemakeCurrentRegionFactor.IsNativeBandPerson(identity) &&
                    K(current.ReadPersonFirstLocation(person), "executor_E2") is >= 1 and <= 61) continue;
                return true;
            }
            return false;
        }

        //7122C0 and7127E0 are identical. The later710FE0/714060 has a
        //different final protected-pair test, retained here as a separate mode.
        private bool AreaAdmission(int id, bool entry)
        {
            var at = current.ReadAreaTableReference(id) ?? throw Missing("Area_pointer");
            int definitionCity = K(current.ReadAreaValidityCode(at), "Area34");
            bool accessible = CityOwner(definitionCity) == own;
            for (int slot = 0; !accessible && slot < 8; slot++)
            {
                int raw = K(current.ReadAreaNeighborSlot(at, slot), "Area_neighbor56");
                var next = current.ReadAreaTableReference(raw < 501 ? raw : 0) ?? throw Missing("Area_neighbor_pointer");
                if (!K(current.ReadAreaNativeValidity(next), "Area_neighbor_validity")) continue;
                int mask = adapter._scenario.Snapshot.Areas[id].RegionalNeighborMask;
                if ((mask & (1 << slot)) != 0 && K(current.ReadAreaCurrentForceSlot(next), "Area_neighbor_Force") == own &&
                    EnemyNeighbors().Contains(definitionCity)) accessible = true;
            }
            if (!accessible) return false;
            int owner = K(current.ReadAreaCurrentForceSlot(at), "Area_current_Force");
            var force = Force(owner);
            if (!K(current.ReadForceNativeValidity(force), "Area_Force_validity")) return true;
            int assignedCity = K(current.ReadAreaCitySlot(at), "Area35");
            var city = City(assignedCity);
            int hex = K(current.ReadAreaHexIndex(at), "Area3A");
            if (K(current.ReadBaseNativeValidity(city), "Area_City_validity"))
            {
                int link = K(current.ReadBaseObjectLink(city), "Area_City4E");
                var obj = current.ReadObjectTableReference(link < 3001 ? link : 0) ?? throw Missing("Area_City_Object");
                if (K(current.ReadObjectNativeValidity(obj), "Area_City_Object_validity") &&
                    K(current.ReadObjectHexIndex(obj), "Area_City_Object_hex") == hex) return false;
            }
            var representative = current.ReadHexTableReference(hex < 48400 ? hex : 0) ?? throw Missing("Area_Hex");
            if (K(current.ReadHexSurfaceCode(representative), "Area_surface") == 17) return false;
            if (entry) return !K(ArmyMarchDevelopmentSource.ReadFriendlyRelation(state, force, player), "Area_protected_pair2C3CA0");
            if (owner == own || owner is >= 46 and <= 50) return false;
            return Unprotected(owner);
        }

        private bool Unprotected(int owner)
        {
            byte relation;
            if (state.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(owner, out var reset) == true)
                relation = K(reset[0xEA + own], "target_reset_relationEA");
            else
                relation = state.NativeArmySimulation?.Forces.GetValueOrDefault(owner)?.RelationsEA is { Length: 52 } values
                    ? values[own] : throw Missing("target_relationEA");
            if ((relation & 0x7F) != 0) return false;
            return (Flags(own) & 2) == 0 || (Flags(owner) & 2) == 0;
        }

        private byte Flags(int force)
        {
            if (state.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(force, out var reset) == true)
                return K(reset[0x12], "Force_reset12");
            return K(state.NativeArmySimulation?.Forces.GetValueOrDefault(force)?.Flags12, "Force_flags12");
        }

        private HashSet<int> EnemyNeighbors()
        {
            if (enemyNeighbors is not null) return enemyNeighbors;
            enemyNeighbors = [];
            //2243A0 filters the actual52 City table receivers by virtualB0;
            //it does not iterate manager90 or add a native-validity test.
            for (int city = 0; city < 52; city++)
            {
                if (CityOwner(city) != own) continue;
                foreach (int adjacent in Neighbors(city)) if (CityOwner(adjacent) != own) enemyNeighbors.Add(adjacent);
            }
            return enemyNeighbors;
        }

        private int[] Neighbors(int id)
        {
            if (neighbors.TryGetValue(id, out var result)) return result;
            var list = current.CreateTemporaryHexList() ?? throw Missing("neighbor_list");
            var values = new List<int>();
            try
            {
                new PersonFreedomHomeSource(adapter._scenario).FillCities22F430(current, City(id), list);
                var node = current.ReadRegistryHead(new(list.Identity)) ?? throw Missing("neighbor_head");
                while (node.Identity is not null)
                {
                    var payload = current.ReadRegistryNodePayload(node) ?? throw Missing("neighbor_payload");
                    if (payload.Kind != RegistryKind.City) throw Missing("neighbor_kind");
                    values.Add(K(payload.PhysicalSlot, "neighbor_City"));
                    node = current.ReadRegistryNodeNext(node) ?? throw Missing("neighbor_next");
                }
            }
            finally { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
            return neighbors[id] = values.ToArray();
        }

        private BaseReference City(int id) => current.ReadCityTableReference(id < 52 ? id : 0) ?? throw Missing("City_pointer");
        private int CityOwner(int id) => K(current.ReadBaseForceSlot(City(id)), "City_Force");
        private ForceReference Force(int id) => current.ReadForceTableReference(id < 52 ? id : 0) ?? throw Missing("Force_pointer");
        private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("enemy-foothold-menu." + field);
        private static AreaAdministrationQuery<bool> Known(bool value) => AreaAdministrationQuery<bool>.Known(value);
        private static AreaAdministrationQuery<bool> Evaluate(Func<bool> query)
        {
            try { return Known(query()); }
            catch (InvalidDataException missing) { return AreaAdministrationQuery<bool>.Unknown(missing.Message); }
        }
    }
}
