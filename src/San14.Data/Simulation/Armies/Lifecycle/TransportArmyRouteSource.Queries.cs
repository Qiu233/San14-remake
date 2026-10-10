using San14.Core;

namespace San14.Data;

public static partial class TransportArmyRouteSource
{
    private sealed class Queries
    {
        private readonly GameState state;
        private readonly SourceWorldContext current;
        private readonly UnitState army;
        private readonly int force;
        private readonly bool homeStart, wallPermission;
        private readonly byte navalFormation, kind;
        private readonly ushort target;
        private readonly Dictionary<int, float> mobilityCache = [];
        internal int Start { get; }
        internal int Goal { get; }
        internal byte LandFormation { get; }

        internal Queries(GameState state, SourceWorldContext current, UnitState army,
            int start, int goal, byte kind, ushort target)
        {
            this.state = state; this.current = current; this.army = army;
            Start = start; Goal = goal; this.kind = kind; this.target = target;
            LandFormation = K(ArmyConfigurationSourceAdapter.ReadCurrentLand(state, army), "Army1B");
            navalFormation = K(ArmyConfigurationSourceAdapter.ReadNaval(state, army), "Army1C");
            var person = K(current.ReadPersonLookupReference(army.OfficerId), "Person_pointer");
            int personPhysical = person.PhysicalSlot ?? throw Missing("Person_receiver");
            force = K(current.ReadPersonOldForceSlot(personPhysical), "Army_Force22CD80");
            if (force is < 1 or > 45) throw Missing("ordinary_Force_scope");
            ushort location = K(current.ReadPersonFirstLocation(personPhysical), "PersonE2");
            var home = location <= 51 ? K(current.ReadCityTableReference(location), "Home_City_pointer") :
                location <= 61 ? K(current.ReadGateTableReference(location - 51), "Home_Gate_pointer") :
                K(current.ReadCityTableReference(0), "Home_placeholder_pointer");
            homeStart = BaseHex(home) == start;
            wallPermission = !K(TacticRemakeCurrentTraits.CommanderImmune(current, personPhysical, 102), "Commander_trait102");
        }

        internal bool HexPresent(int raw) => K(current.ReadHexTableReference(raw), "Hex_pointer").PhysicalSlot is not null;
        private HexReference Hex(int raw) => K(current.ReadHexTableReference(raw), "Hex_pointer");
        internal SurfaceTypeReference Surface(int raw) => K(current.ReadSurfaceTypeTableReference(raw <= 30 ? raw : 0), "Terrain_pointer");
        private SurfaceTypeReference Terrain(int raw) => Surface(K(current.ReadHexSurfaceCode(Hex(raw)), "Hex10"));
        private ObjectReference Object(int raw)
        {
            ushort link = K(current.ReadHexObjectLink(Hex(raw)), "Hex1A");
            return K(current.ReadObjectTableReference(link <= 3000 ? link : 0), "Object_pointer");
        }
        private int BaseHex(BaseReference @base)
        {
            ushort link = K(current.ReadBaseObjectLink(@base), "Base4E");
            var obj = K(current.ReadObjectTableReference(link <= 3000 ? link : 0), "Base_Object_pointer");
            return K(current.ReadObjectNativeValidity(obj), "Base_Object_validity") ? K(current.ReadObjectHexIndex(obj), "Object10") : Count;
        }
        private bool IsCity(ObjectReference obj) => obj.PhysicalSlot is >= 1 and <= 51;
        private bool IsGate(ObjectReference obj) => obj.PhysicalSlot is >= 52 and <= 61;
        private ObjectDefinitionReference Scene(ObjectReference obj)
        {
            var data = K(current.ReadPhysicalObject(obj.PhysicalSlot ?? throw Missing("Object_receiver")), "Object_fields");
            return K(current.ReadObjectDefinitionReference(data.RawField12 <= 30 ? data.RawField12 : 0), "ObjectData_pointer");
        }
        internal bool AdmitGoal()
        {
            var obj = Object(Goal);
            if (K(current.ReadObjectNativeValidity(obj), "goal_Object_validity") && (IsCity(obj) || IsGate(obj)) &&
                force != K(current.ReadHexForceSlot(Hex(Goal)), "goal_Hex14")) return false;
            return !HostileAtHex(Goal);
        }
        internal bool HostileAtHex(int raw) => K(ArmyLogisticsPreparation.ReadNativeRelationGate(state,
            force, K(current.ReadHexForceSlot(Hex(raw)), "Hex14")), "2CA650");
        internal int TerrainCost(SurfaceTypeReference terrain)
        {
            if (terrain.PhysicalSlot is not int physical) return 0;
            int raw = K(current.ReadSurfaceTypeRawField2A(terrain), "Terrain2A");
            if (K(BattleRemakeCurrentSettings.ReadSelectorE58(state), "EnvironmentE58") != 0 ||
                physical is not (>= 3 and <= 14 or 24)) return raw;
            int percent = K(BattleRemakeCurrentSettings.ReadAdvancedParameter(state, 26), "advanced26");
            return Math.Clamp(unchecked(raw * percent) / 100, 225, 999);
        }
        internal int BaseEdge(int raw)
        {
            var terrain = Terrain(raw);
            if (terrain.PhysicalSlot is null) return 0;
            int value = TerrainCost(terrain);
            if (wallPermission)
            {
                ushort wall = K(current.ReadCaptureHexWord16(Hex(raw).PhysicalSlot!.Value), "Hex16");
                if (wall != 0) value = unchecked(value + unchecked((int)((uint)wall * (uint)Parameter(SimulationParameterKeys.TransportRoute.WallPenaltyPercent))) / 100);
            }
            return value;
        }
        internal float Mobility(int formation, byte explicitSurface)
        {
            var value = TransportArmyRouteMobilitySource.Read(state, current, army, formation <= 20 ? formation : 0, explicitSurface);
            return value.Value ?? throw Missing(value.UnresolvedOwner ?? "2B0660");
        }
        internal float TerrainMobility(int raw)
        {
            var terrain = Terrain(raw);
            int physical = terrain.PhysicalSlot ?? throw Missing("1866C0_Terrain_pointer");
            if (mobilityCache.TryGetValue(physical, out float value)) return value;
            byte flags = K(PersonArmyCaptureInputs.ReadTerrainFlags2C(state, physical), "Terrain2C");
            int formation = (flags & 2) != 0 ? navalFormation : LandFormation;
            value = Mobility(formation, K(current.ReadHexSurfaceCode(Hex(raw)), "step_Hex10"));
            mobilityCache.Add(physical, value); return value;
        }

        internal bool Passable(int raw)
        {
            var terrain = Terrain(raw);
            if (terrain.PhysicalSlot is not int physical) return false;
            bool waterAllowed = physical != 24 || navalFormation is 12 or 13 or 17 or 18;
            if (waterAllowed && unchecked((short)K(current.ReadSurfaceTypeRawField2A(terrain), "2C6AD0_Terrain2A")) != 0)
            {
                var obj = Object(raw);
                if (!IsGate(obj))
                {
                    var scene = Scene(obj);
                    if (!K(current.ReadObjectDefinitionValidity(scene), "Scene_validity") ||
                        K(current.ReadObjectDefinitionRaw2C(scene), "Scene2C") == 4) return true;
                }
                else if (K(current.ReadObjectHexIndex(obj), "Gate_Object10") == raw && !HostileAtHex(raw)) return true;
            }
            //18A240's real City/Gate exceptions after the general terrain gate.
            if (physical == 16)
            {
                ushort cityLink = K(current.ReadHexObjectLink(Hex(raw)), "22A9E0_Hex1A");
                var city = K(current.ReadCityTableReference(cityLink <= 51 ? cityLink : 0), "22A9E0_City_pointer");
                if (!K(current.ReadBaseNativeValidity(city), "22A9E0_City_validity")) return false;
                int center = BaseHex(city);
                if (homeStart && (Start == center || Goal == raw || Goal == center)) return true;
                var destination = K(current.ReadCityTableReference(target <= 51 ? target : 0), "target_City_pointer");
                if (kind != 5 || destination != city) return false;
                if (Distance(center, Start) >= 3) return true;
                if (K(current.ReadBaseForceSlot(city), "City_ForceB0") != force ||
                    TransportRouteSourceAdapter.ReadWaypoint(state, army, 0) != Count) return false;
                //2C5440(source six ring) only membership; no frame/terrain owner.
                return Enumerable.Range(1, 6).Any(d => PersonReturnPathSource.Neighbor(center, d) == raw);
            }
            if (physical != 17) return false;
            ushort gateLink = K(current.ReadHexObjectLink(Hex(raw)), "22D0D0_Hex1A");
            var gate = K(current.ReadGateTableReference(gateLink is >= 52 and <= 61 ? gateLink - 51 : 0), "22D0D0_Gate_pointer");
            return K(current.ReadBaseNativeValidity(gate), "22D0D0_Gate_validity") && raw == BaseHex(gate);
        }
        internal bool BlockedScene(int raw)
        {
            var obj = Object(raw);
            if (!K(current.ReadObjectNativeValidity(obj), "neighbor_Object_validity") || IsCity(obj) || IsGate(obj)) return false;
            var scene = Scene(obj);
            return K(current.ReadObjectDefinitionRaw2C(scene), "neighbor_Scene2C") is 2 or 3;
        }
        internal int OccupiedCost(int raw)
        {
            var hex = Hex(raw);
            _ = hex.PhysicalSlot ?? throw Missing("occupancy_Hex_pointer");
            current.BindArmyMapOccupancy(state);
            int occupant = unchecked((short)K(current.ReadHexRaw1C(hex), "current_Hex1C"));
            if (occupant is < 1 or > 500) return 0; //2C6D20's real signed range.
            if (state.ReadArmyLifecycle() is not { } arena || occupant >= arena.ReceiverSlots().Length)
                throw Missing("occupant_Army_receiver");
            var slot = arena.ReceiverSlots()[occupant];
            var other = slot.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) : slot.PendingImage;
            ushort commanderId = K(slot.Commander12, "occupant12");
            var commander = K(current.ReadPersonLookupReference(commanderId <= 6000 ? commanderId : 0), "occupant_Person_pointer");
            int otherForce = commander.PhysicalSlot is int p && K(current.ReadPersonNativeValidity(p), "occupant_Person_validity")
                ? K(current.ReadPersonOldForceSlot(p), "occupant_Force") : 0;
            if (K(ArmyLogisticsPreparation.ReadNativeRelationGate(state, force, otherForce), "occupant_relation"))
                return Parameter(SimulationParameterKeys.TransportRoute.FriendlyOccupantPenalty);
            if (other?.NativeTacticRuntime is not { } runtime) throw Missing("occupant_Army24");
            bool blocks = runtime.Abnormal24 >= Parameter(SimulationParameterKeys.TransportRoute.BlockingAbnormalThreshold);
            if (!blocks)
            {
                if (ArmyConfigurationSourceAdapter.ReadAction(state, other) is not byte action) throw Missing("occupant_Action37");
                //2C9830 action2/3/14 returns0 after its24 gate. The actual
                //default switch returns1 without target/range queries.
                if (action is 2 or 3 or 14) blocks = false;
                else if (action is 1 or 4 or 5 or 6 or 7 or 8 or 9 or 10 or 15 or 16 or 17)
                    throw Missing("occupant_2C9830_action_branch");
                else blocks = true;
            }
            return Parameter(blocks ? SimulationParameterKeys.TransportRoute.BlockingOccupantPenalty : SimulationParameterKeys.TransportRoute.NonblockingOccupantPenalty);
        }
        private int Parameter(string key) => K(TransportArmyRouteInputs.ReadCurrent(state, key), "parameter_" + key);
    }
}
