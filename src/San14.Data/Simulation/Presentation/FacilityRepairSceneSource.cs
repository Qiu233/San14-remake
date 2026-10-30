using San14.Core;

namespace San14.Data;

/// <summary>Immutable Object-view initialization inputs. A null RepairLoop
/// retains an unknown reached predicate; it must not retire a known loop.</summary>
public sealed record FacilityRepairSceneRow(int ReceiverId, int DefinitionKind, int HexId,
    bool? RepairLoop, int? AmbientEffectCode, int? SourceCommanderId, string? Missing);

public sealed record FacilityRepairSceneFrame(IReadOnlyList<FacilityRepairSceneRow> Rows,
    IReadOnlyList<MapStaticModelInstanceFailure> Missing);

public sealed partial class ArmyCampaignRuleAdapter
{
    private GameState? facilityRepairSceneOwner;
    private FacilityRepairSceneFrame? facilityRepairSceneFrame;

    /// <summary>1C8500 ->1C5B20: rebind each valid ordinary facility's two
    /// Object-owned loop slots. Call at world publication or map rebinding,
    /// never from the render tick. This captures once per immutable business
    /// owner set; no renderer/view or source context escapes.</summary>
    public FacilityRepairSceneFrame QueryFacilityRepairScene(GameState world)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (facilityRepairSceneOwner is { } previous && facilityRepairSceneFrame is { } known &&
            (ReferenceEquals(world, previous) || SameFacilityRepairSceneOwners(previous, world)))
        {
            facilityRepairSceneOwner = world;
            return known;
        }

        var rows = new List<FacilityRepairSceneRow>();
        var missing = new List<MapStaticModelInstanceFailure>();
        try
        {
            //FacilitySceneCurrent's model cache intentionally does not depend
            //on Army changes. Loop slot0 needs this publication's actual Army
            //actions, occupancy, waypoints and main/override target owners.
            var current = CaptureOwnedArmyReturnWorld(world).Context;
            var directory = current.CurrentMapObjectDirectory ?? throw RepairSceneMissing("Object_directory");
            var slots = (world.ReadArmyLifecycle() ?? throw RepairSceneMissing("Army_lifecycle")).ReceiverSlots();
            var armies = new Dictionary<int, RepairSceneArmyFacts>();
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Object)
                ?? throw RepairSceneMissing("Object_registry"))
            {
                if (entry is not { Kind: RegistryKind.Object, PhysicalSlot: int physical })
                    throw RepairSceneMissing("Object_registry_pointer");
                int receiver = directory.ReceiverAtPhysical(physical).Value;
                string identity = "facility:" + receiver;
                try
                {
                    var obj = new ObjectReference(physical);
                    if (!Need(current.ReadObjectNativeValidity(obj), "Object_validity")) continue;
                    var definition = FacilityConstructionLegality.DefinitionOf(current, obj);
                    if (!Need(current.ReadObjectDefinitionValidity(definition), "definition_validity")) continue;
                    //22DDA0 returns the selected definition's physical identity,
                    //not the Object12 lookup ordinal when that table aliases.
                    int kind = Need(definition.PhysicalSlot, "definition_identity");
                    if (kind is < 3 or > 17) continue;
                    int hex = Need(current.ReadObjectHexIndex(obj), "Object_Hex");
                    bool? repair = null;
                    int? commander = null, ambient = null;
                    string? rowMissing = null;
                    try { (repair, commander) = RepairLoop(hex); }
                    catch (InvalidDataException error) { rowMissing = error.Message; }
                    try
                    {
                        //Slot1 is independent of slot0 and has no Army gate.
                        if ((Need(current.ReadMapObjectFields16(obj), "Object_activation") & 1) != 0)
                            ambient = kind switch { 8 => 0x2D, 7 => 0x2E, _ => null };
                    }
                    catch (InvalidDataException error)
                    { rowMissing = rowMissing is null ? error.Message : rowMissing + "; " + error.Message; }
                    rows.Add(new(receiver, kind, hex, repair, ambient, commander, rowMissing));
                    if (rowMissing is not null) missing.Add(new(identity, rowMissing));
                }
                catch (InvalidDataException error) { missing.Add(new(identity, error.Message)); }
            }

            (bool Active, int? Commander) RepairLoop(int objectHex)
            {
                var center = current.ReadHexTableReference(objectHex < 48400 ? objectHex : 0)
                    ?? throw RepairSceneMissing("Object_Hex_pointer");
                int centerPhysical = Need(center.PhysicalSlot, "Object_Hex_identity");
                //1C5C76 supplies radius1 to2A7EE0. Its2A7CD0 ring order
                //excludes the center. Use the shared geometry, then the same
                //signed Hex1C / Army validity / raw action9 /2B54A0 gates.
                foreach (int key in TacticRemakeCurrentContext.NativeHexOrder(centerPhysical, 1).Skip(1))
                {
                    var nearby = current.ReadHexTableReference(key) ?? throw RepairSceneMissing("ring_Hex_pointer");
                    if (nearby.PhysicalSlot is null) continue;
                    int raw = unchecked((short)Need(current.ReadHexRaw1C(nearby), "ring_Hex_Army1C"));
                    if (raw is < 1 or > 500) continue;
                    var facts = Army(raw);
                    if (facts.Missing is not null) throw new InvalidDataException(facts.Missing);
                    if (!facts.Repairs || facts.GoalHexPhysical != centerPhysical) continue;
                    return (true, facts.CommanderId);
                }
                return (false, null);
            }

            RepairSceneArmyFacts Army(int lookup)
            {
                if (armies.TryGetValue(lookup, out var cached)) return cached;
                RepairSceneArmyFacts facts;
                try
                {
                    var army = current.ReadArmyUnitTableReference(lookup) ?? throw RepairSceneMissing("ring_Army_pointer");
                    if (!Need(current.ReadArmyUnitNativeValidity(army), "ring_Army_validity"))
                        facts = new(false, null, null, null);
                    else
                    {
                        int physical = Need(army.PhysicalSlot, "ring_Army_identity");
                        var slot = slots[physical];
                        var image = slot.ActiveUnitId is int id ? world.Units.GetValueOrDefault(id) : slot.PendingImage;
                        if (image is null || image.NativeArmyOrigin?.NativeSlotIndex != physical)
                            throw RepairSceneMissing("ring_Army_current_image");
                        byte action = Need(ArmyConfigurationSourceAdapter.ReadAction(world, image), "ring_Army_action37");
                        if (action != 9) facts = new(false, null, null, null);
                        else
                        {
                            int goal = Need(OrdinaryArmyRouteSource.ReadCurrentTarget(world, image, current), "ring_Army_goal2B54A0");
                            var target = current.ReadHexTableReference(goal < 48400 ? goal : 0)
                                ?? throw RepairSceneMissing("ring_Army_goal_Hex_pointer");
                            //32FB90 rejects a known null CHex; slot0 remains
                            //a real receiver for the source sentinel fallback.
                            facts = target.PhysicalSlot is not int goalPhysical
                                ? new(false, null, null, null)
                                : new(true, goalPhysical, Commander(army), null);
                        }
                    }
                }
                catch (InvalidDataException error) { facts = new(false, null, null, error.Message); }
                armies.Add(lookup, facts);
                return facts;
            }

            int? Commander(ArmyUnitReference army)
            {
                //The repair-loop predicate does not require a valid Person.
                //Expose a commander only when that separate lookup is known;
                //never infer one from a mission or invent an officer identity.
                ushort? raw = current.ReadArmyUnitCommanderId(army);
                if (raw is not ushort id) return null;
                var person = current.ReadPersonLookupReference(id <= 6000 ? id : 0);
                return person?.PhysicalSlot is int physical && current.ReadPersonNativeValidity(physical) == true
                    ? current.ReadPersonIdentity(physical) : null;
            }
        }
        catch (InvalidDataException error) { missing.Add(new("facility-repair-scene", error.Message)); }

        facilityRepairSceneOwner = world;
        return facilityRepairSceneFrame = new(rows.AsReadOnly(), missing.AsReadOnly());

        static T Need<T>(T? value, string field) where T : struct => value ?? throw RepairSceneMissing(field);
    }

    private static bool SameFacilityRepairSceneOwners(GameState previous, GameState world) =>
        //ReadArmyLifecycle() constructs a joined view each time. Compare its
        //actual two owners instead: the domain below and ResourceState's
        //source fields/residues. Effects ACKs change neither of them.
        ReferenceEquals(previous.ArmyLifecycle, world.ArmyLifecycle) &&
        ReferenceEquals(previous.ResourceState, world.ResourceState) &&
        ReferenceEquals(previous.Units, world.Units) &&
        ReferenceEquals(previous.ArmyMapOccupancy, world.ArmyMapOccupancy) &&
        ReferenceEquals(previous.MapObjects, world.MapObjects) &&
        ReferenceEquals(previous.Structures, world.Structures) &&
        //Only the optional commander's actual Person lookup uses these roots;
        //detached receiver identities are held in the saved parameter owner.
        ReferenceEquals(previous.Officers, world.Officers) &&
        ReferenceEquals(previous.NativeArmySimulation?.Parameters, world.NativeArmySimulation?.Parameters) &&
        //Definitions and Hex geometry belong to this adapter. Their campaign
        //content identity can still change when a session is replaced.
        ReferenceEquals(previous.ContentHashes, world.ContentHashes) &&
        previous.MapWidth == world.MapWidth && previous.MapHeight == world.MapHeight &&
        previous.MapSha256 == world.MapSha256;

    private sealed record RepairSceneArmyFacts(bool Repairs, int? GoalHexPhysical, int? CommanderId, string? Missing);
    private static InvalidDataException RepairSceneMissing(string field) => new("facility.repair.scene." + field);
}
