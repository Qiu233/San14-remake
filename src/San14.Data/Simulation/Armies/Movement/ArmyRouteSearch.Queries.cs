using San14.Core;

namespace San14.Data;

internal static partial class ArmyRouteSearch
{
    internal sealed class Queries
    {
        private readonly GameState state;
        private readonly SourceWorldContext current;
        private readonly Movement motion;
        private readonly int force;
        private readonly bool homeStart;
        private readonly byte navalFormation, kind;
        private readonly ushort target;
        private readonly int? registeredSearchArmyPhysical;
        private readonly byte searchFlags;
        private readonly bool hasArmyDescriptor;
        private readonly IReadOnlySet<int>? excludedHexIds;
        private readonly bool penalizeSurface7;
        private readonly Func<int, byte?>? readHexForce;
        private readonly Func<int, ushort?>? readHexWord16;
        private readonly Dictionary<int, float> mobilityCache = [];
        internal bool AvoidHostile { get; }
        internal int Start { get; }
        internal int Goal { get; }
        internal int MapWidth => current.MapWidth;
        internal int TileCount => current.MapTileCount;
        internal bool ContainsHex(int hex) => current.ContainsMapHex(hex);
        internal int Neighbor(int hex, int direction) =>
            PersonReturnPathSource.Neighbor(hex, direction, current.MapWidth, current.MapHeight);
        internal byte LandFormation { get; }
        internal bool UniformCost => (searchFlags & 5) == 0;

        internal Queries(GameState state, SourceWorldContext current, Movement motion,
            int start, int goal, byte kind, ushort target, int? registeredSearchArmyPhysical, byte flags = 1,
            bool hasArmyDescriptor = true, Func<int, ushort?>? readHexWord16 = null,
            IReadOnlySet<int>? excludedHexIds = null, bool penalizeSurface7 = false, Func<int, byte?>? readHexForce = null)
        {
            if (flags is not (1 or 4 or 8 or 0x21 or 0x62 or 0x71)) throw Missing("supported_route_search_flags");
            searchFlags = flags;
            this.state = state; this.current = current; this.motion = motion;
            this.registeredSearchArmyPhysical = registeredSearchArmyPhysical;
            this.hasArmyDescriptor = hasArmyDescriptor; this.readHexWord16 = readHexWord16;
            this.excludedHexIds = excludedHexIds; this.penalizeSurface7 = penalizeSurface7; this.readHexForce = readHexForce;
            Start = start; Goal = goal; this.kind = kind; this.target = target;
            AvoidHostile = (flags & 0xC) == 0 && motion.AvoidHostile; LandFormation = motion.LandFormation;
            navalFormation = motion.NavalFormation; force = motion.Force;
            homeStart = motion.HomeHex == start;
        }

        internal bool ExcludedByDescriptor(int raw) => (searchFlags & 0xC) == 0 && excludedHexIds?.Contains(raw) == true;
        internal int DescriptorPenalty(int raw) => (searchFlags & 0xC) == 0 && penalizeSurface7 && K(current.ReadHexSurfaceCode(Hex(raw)), "descriptor_Hex10") == 7 ? 10000 : 0;

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
            return K(current.ReadObjectNativeValidity(obj), "Base_Object_validity") ? K(current.ReadObjectHexIndex(obj), "Object10") : TileCount;
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
            if (!AvoidHostile) return true;
            var obj = Object(Goal);
            if (K(current.ReadObjectNativeValidity(obj), "goal_Object_validity") && (IsCity(obj) || IsGate(obj)) &&
                force != K(ReadHexForce(Goal), "goal_Hex14")) return false;
            return !HostileAtHex(Goal);
        }
        private byte? ReadHexForce(int raw) => readHexForce is { } read
            ? read(K(Hex(raw).PhysicalSlot, "override_Hex_identity")) : current.ReadHexForceSlot(Hex(raw));
        internal bool HostileAtHex(int raw) => K(ArmyLogisticsPreparation.ReadNativeRelationGate(state,
            force, K(ReadHexForce(raw), "Hex14")), "2CA650");
        internal int TerrainCost(SurfaceTypeReference terrain) => ArmyRouteTerrainCost.Read(state, current, terrain);
        internal int BaseEdge(int raw)
        {
            if (UniformCost) return 10000;
            var terrain = Terrain(raw);
            if (terrain.PhysicalSlot is null) return 0;
            int value = TerrainCost(terrain);
            if (motion.WallPermission)
            {
                int physical = Hex(raw).PhysicalSlot ?? throw Missing("wall_Hex_pointer");
                ushort wall = K(readHexWord16 is null ? current.ReadCaptureHexWord16(physical) : readHexWord16(physical), "Hex16");
                if (wall != 0) value = unchecked(value + unchecked((int)((uint)wall * (uint)Parameter(SimulationParameterKeys.TransportRoute.WallPenaltyPercent))) / 100);
            }
            return value;
        }
        internal float Mobility(int formation, byte explicitSurface) => motion.ReadMobility(formation, explicitSurface);
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
        internal int ScaleEdgeCost(int raw, int cost) => searchFlags is 4 or 8 || !hasArmyDescriptor
            ? cost : NativeInt((float)cost / TerrainMobility(raw));

        internal bool Passable(int raw)
        {
            var terrain = Terrain(raw);
            if (terrain.PhysicalSlot is not int physical) return false;
            // Flags4/8 use 2C6C20 rather than the formation, object
            // and occupancy gates of the movement search. The endpoint is
            // still admitted separately by the shared search loop.
            if (searchFlags is 4 or 8)
                return unchecked((short)K(current.ReadSurfaceTypeRawField2A(terrain), "logistics_Terrain2A")) != 0 &&
                    (force == 0 || !HostileAtHex(raw));
            if (!hasArmyDescriptor) return PassableWithoutArmy(raw, terrain, physical);
            bool waterAllowed = physical != 24 || navalFormation is 12 or 13 or 17 or 18;
            if (waterAllowed && unchecked((short)K(current.ReadSurfaceTypeRawField2A(terrain), "2C6AD0_Terrain2A")) != 0)
            {
                var obj = Object(raw);
                if (!IsGate(obj))
                {
                    if ((searchFlags & 0x40) != 0) return true;
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
                byte action = K(motion.Action, "City_Action37");
                //18A240 exempts14/16 only from kind/pointer matching. Attack4
                //can cross its actual target City's cells while still far away.
                if (action is not (14 or 16))
                {
                    if (kind != 5) return false;
                    var destination = K(current.ReadCityTableReference(target <= 51 ? target : 0), "target_City_pointer");
                    if (destination != city) return false;
                }
                if (Distance(center, Start, MapWidth) >= 3) return true;
                if (K(current.ReadBaseForceSlot(city), "City_ForceB0") != force ||
                    !motion.HasNoDestination || action is not (2 or 11)) return false;
                //2C5440(source six ring) only membership; no frame/terrain owner.
                return Enumerable.Range(1, 6).Any(d => Neighbor(center, d) == raw);
            }
            if (physical != 17) return false;
            ushort gateLink = K(current.ReadHexObjectLink(Hex(raw)), "22D0D0_Hex1A");
            var gate = K(current.ReadGateTableReference(gateLink is >= 52 and <= 61 ? gateLink - 51 : 0), "22D0D0_Gate_pointer");
            return K(current.ReadBaseNativeValidity(gate), "22D0D0_Gate_validity") && raw == BaseHex(gate);
        }

        //18A240's actual null-Army branch. It has no Action37, formation
        //mobility, home-exit test or Army-dependent occupied-cell surcharge.
        private bool PassableWithoutArmy(int raw, SurfaceTypeReference terrain, int physical)
        {
            bool passable = unchecked((short)K(current.ReadSurfaceTypeRawField2A(terrain), "null_army_Terrain2A")) != 0;
            if (passable)
            {
                var obj = Object(raw);
                if (IsGate(obj))
                    passable = K(current.ReadObjectHexIndex(obj), "null_army_Gate10") == raw && !HostileAtHex(raw);
                else
                {
                    if ((searchFlags & 0x40) == 0)
                    {
                        var scene = Scene(obj);
                        passable = !K(current.ReadObjectDefinitionValidity(scene), "null_army_Scene_validity") ||
                            K(current.ReadObjectDefinitionRaw2C(scene), "null_army_Scene2C") == 4;
                    }
                }
                if (passable)
                {
                    if (physical != 24) return true;
                    var reference = K(current.ReadForceTableReference(force < 52 ? force : 0), "null_army_Force_pointer");
                    int owner = reference.PhysicalSlot ?? throw Missing("null_army_Force_receiver");
                    string key = BattleRemakeCurrentForcePolicies.ForceFieldKey(owner, 0x15E);
                    var bank = state.NativeArmySimulation?.Parameters ?? throw Missing("null_army_Force15E_owner");
                    if (bank.Float32Values.ContainsKey(key) || !bank.Int32Values.TryGetValue(key, out int naval) || naval is < 0 or > 255)
                        throw Missing("null_army_Force15E");
                    if (naval is 12 or 13 or 17 or 18) return true;
                }
            }
            if (physical == 16) return true;
            if (physical != 17) return false;
            ushort gateLink = K(current.ReadHexObjectLink(Hex(raw)), "null_army_Gate_Hex1A");
            var gate = K(current.ReadGateTableReference(gateLink is >= 52 and <= 61 ? gateLink - 51 : 0), "null_army_Gate_pointer");
            return K(current.ReadBaseNativeValidity(gate), "null_army_Gate_validity") && raw == BaseHex(gate);
        }
        internal bool BlockedScene(int raw)
        {
            if (searchFlags is 4 or 8) return false;
            if ((searchFlags & 0x40) != 0) return false;
            var obj = Object(raw);
            if (!K(current.ReadObjectNativeValidity(obj), "neighbor_Object_validity") || IsCity(obj) || IsGate(obj)) return false;
            var scene = Scene(obj);
            return K(current.ReadObjectDefinitionRaw2C(scene), "neighbor_Scene2C") is 2 or 3;
        }
        internal int OccupiedCost(int raw)
        {
            if (searchFlags is 4 or 8) return 0;
            if (!hasArmyDescriptor) return 0;
            if ((searchFlags & 0x20) != 0) return 0;
            var hex = Hex(raw);
            _ = hex.PhysicalSlot ?? throw Missing("occupancy_Hex_pointer");
            current.BindArmyMapOccupancy(state);
            int occupant = unchecked((short)K(current.ReadHexRaw1C(hex), "current_Hex1C"));
            if (occupant is < 1 or > 500) return 0; //2C6D20's real signed range.
            var occupantReference = K(current.ReadArmyUnitTableReference(occupant), "occupant_Army_pointer");
            if (occupantReference.PhysicalSlot is not int physical) return 0; //187B90's actual null receiver.
            if (state.ReadArmyLifecycle() is not { } arena || (uint)physical >= (uint)arena.ReceiverSlots().Length)
                throw Missing("occupant_Army_receiver");
            var slot = arena.ReceiverSlots()[physical];
            ushort commanderId = K(slot.Commander12, "occupant12");
            var commander = K(current.ReadPersonLookupReference(commanderId <= 6000 ? commanderId : 0), "occupant_Person_pointer");
            int otherForce = commander.PhysicalSlot is int p && K(current.ReadPersonNativeValidity(p), "occupant_Person_validity")
                ? K(current.ReadPersonOldForceSlot(p), "occupant_Force") : 0;
            if (K(ArmyLogisticsPreparation.ReadNativeRelationGate(state, force, otherForce), "occupant_relation"))
                return Parameter(SimulationParameterKeys.TransportRoute.FriendlyOccupantPenalty);
            bool blocks = OccupantBlocksCurrent(state, current, occupantReference,
                hasArmyDescriptor, registeredSearchArmyPhysical);
            return Parameter(blocks ? SimulationParameterKeys.TransportRoute.BlockingOccupantPenalty : SimulationParameterKeys.TransportRoute.NonblockingOccupantPenalty);
        }
        internal bool ForbidsOccupiedNonTarget => (searchFlags & 0x10) != 0;
        internal bool Occupied(int raw)
        {
            current.BindArmyMapOccupancy(state);
            short occupant = unchecked((short)K(current.ReadHexRaw1C(Hex(raw)), "obstacle_Hex1C"));
            return occupant is >= 1 and <= 500;
        }
        private int Parameter(string key) => K(TransportArmyRouteInputs.ReadCurrent(state, key), "parameter_" + key);
    }
}
