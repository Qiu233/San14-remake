using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Open the actual Gihou destination page once. Pointer motion and
    /// selection consume this publication; neither needs another capture/path.</summary>
    public ArmyFalseReportDestinationSelection QueryArmyFalseReportDestinations(GameState state,
        int forceId, int targetUnitId) => CreateArmyFalseReportTargetQuery(state, forceId, targetUnitId).Selection;

    public ArmyFalseReportTargetQuery CreateArmyFalseReportTargetQuery(GameState state, int forceId, int targetUnitId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (forceId is < 0 or > 51 || !state.Units.TryGetValue(targetUnitId, out var target))
            return Unavailable(new(false, [], null));
        try
        {
            var current = CaptureDeploymentDraftMarchCurrent(state);
            var own = new ForceReference(forceId);
            var army = new ArmyUnitReference(target.NativeArmyOrigin?.NativeSlotIndex
                ?? throw FalseReportDestinationMissing("target_Army_receiver"));
            if (!FalseReportTargetAllowed(state, current, own, army)) return Unavailable(new(false, [], null));
            var selection = new ArmyFalseReportDestinationSelection(true, ReadFalseReportDestinations(current, own, army), null);
            var candidateHexes = selection.Destinations!.Select(value => value.HexId).ToHashSet();
            int raw = FRKnown(current.ReadArmyUnitRawHex2A(army), "preview_start2A");
            int start = FRKnown(current.ReadHexTableReference(raw < 48400 ? raw : 0)?.PhysicalSlot, "preview_start_pointer");
            //7DA5D0/7D8280 read the enemy's current1B/1C/16 configuration;
            //353E80 then resets its preview motion. No live or fake Army is
            //created, and the enemy's saved five points are not inherited.
            int land = FRKnown(ArmyConfigurationSourceAdapter.ReadCurrentLand(state, target), "preview_land1B");
            int naval = FRKnown(ArmyConfigurationSourceAdapter.ReadNaval(state, target), "preview_naval1C");
            var row = new DeploymentDraftRow(new(target.OfficerId, land, target.Troops, [], []), naval);
            return new(selection, start, Pointer, Route);

            ArmyFalseReportPointer Pointer(int physical)
            {
                if (physical is < 0 or >= 48400) return new(false, false, null, null);
                try
                {
                    var pressed = new HexReference(physical);
                    int surface = FRKnown(current.ReadHexSurfaceCode(pressed), "preview_pressed_surface");
                    var terrain = current.ReadSurfaceTypeTableReference(surface <= 30 ? surface : 0)
                        ?? throw FalseReportDestinationMissing("preview_pressed_terrain");
                    if (terrain.PhysicalSlot is null || FRKnown(current.ReadSurfaceTypeRawField2A(terrain), "preview_pressed_cost") == 0)
                        return new(false, false, null, null);
                    return new(true, FalseReportPreviewPassable(state, current, own, physical), candidateHexes.Contains(physical), null);
                }
                catch (InvalidDataException error) { return new(null, null, null, error.Message); }
            }
            DeploymentDraftRouteSegment Route(int physical)
            {
                var admission = Pointer(physical);
                if (admission.CanPreviewRoute is null) return Unknown(admission.UnresolvedInput ?? "army.false_report_destination.preview_unknown");
                if (admission.CanPreviewRoute == false) return new(DeploymentDraftRouteStatus.NoPath, [], [], 0, null);
                try
                {
                    var input = ReadDeploymentDraftMarchInputs(state, current, row);
                    var motion = new DeploymentDraftMarchSource.Motion(state, current, input);
                    int[] path = DeploymentDraftMarchSource.Search(state, current, motion, start, physical, out int expanded);
                    return new(path.Length > 0 ? DeploymentDraftRouteStatus.Found : DeploymentDraftRouteStatus.NoPath,
                        Array.AsReadOnly(path), Array.AsReadOnly(path.Length == 0 ? [] : path[..^1]), expanded, null);
                }
                catch (InvalidDataException error) { return Unknown(error.Message); }
                catch (ArmyRouteSearch.MissingOwner error) { return Unknown(error.Message); }
            }
        }
        catch (InvalidDataException error) { return Unavailable(new(null, null, error.Message)); }
        static DeploymentDraftRouteSegment Unknown(string reason) => new(DeploymentDraftRouteStatus.Unresolved, null, null, 0, reason);
        static ArmyFalseReportTargetQuery Unavailable(ArmyFalseReportDestinationSelection selection) => new(selection, null,
            _ => new(null, null, null, selection.UnresolvedInput), _ => Unknown(selection.UnresolvedInput ?? "army.false_report_destination.unavailable"));
    }

    //7E3170 calls2C6AD0(HexPhysical,0,own,0,0), then excludes own
    //category1 Object. The outer surface check belongs to the original
    //physical press; the passability getter really looks up HexTable again.
    private static bool FalseReportPreviewPassable(GameState state, SourceWorldContext current, ForceReference own, int physical)
    {
        var at = current.ReadHexTableReference(physical) ?? throw FalseReportDestinationMissing("preview_Hex_lookup");
        if (at.PhysicalSlot is null) return false;
        int code = FRKnown(current.ReadHexSurfaceCode(at), "preview_surface");
        var terrain = current.ReadSurfaceTypeTableReference(code <= 30 ? code : 0)
            ?? throw FalseReportDestinationMissing("preview_terrain_pointer");
        if (terrain.PhysicalSlot is null || FRKnown(current.ReadSurfaceTypeRawField2A(terrain), "preview_cost") == 0) return false;
        var obj = FacilityConstructionLegality.ObjectAt(current, at);
        int objectPhysical = FRKnown(obj.PhysicalSlot, "preview_Object_pointer");
        if (objectPhysical is >= 52 and <= 61)
        {
            if (FRKnown(current.ReadObjectHexIndex(obj), "preview_Gate_Hex") != physical ||
                FRKnown(ArmyLogisticsPreparation.ReadNativeRelationGate(state, FRKnown(own.PhysicalSlot, "preview_own"),
                    FRKnown(current.ReadHexForceSlot(at), "preview_Hex_Force")), "preview_Gate_relation")) return false;
        }
        else
        {
            var definition = FacilityConstructionLegality.DefinitionOf(current, obj);
            if (FRKnown(current.ReadObjectDefinitionValidity(definition), "preview_definition_validity") &&
                FRKnown(current.ReadObjectDefinitionRaw2C(definition), "preview_definition_category") != 4) return false;
        }
        //The second branch reads the original physical CHex's Object again.
        var originalObject = FacilityConstructionLegality.ObjectAt(current, new HexReference(physical));
        int forceRaw = FRKnown(current.ReadObjectForceSlot13(originalObject), "preview_original_Object_Force");
        var force = current.ReadForceTableReference(forceRaw <= 51 ? forceRaw : 0)
            ?? throw FalseReportDestinationMissing("preview_original_Force_pointer");
        return force != own || FRKnown(current.ReadObjectDefinitionRaw2C(
            FacilityConstructionLegality.DefinitionOf(current, originalObject)), "preview_original_category") != 1;
    }

    //7C70D0's independent suffix: Other Army, not formation19/20, not
    //all three nonzero abnormalities, no saved command, and a real Trap.
    private static bool FalseReportTargetAllowed(GameState state, SourceWorldContext current,
        ForceReference own, ArmyUnitReference target)
    {
        if (!FRKnown(current.ReadArmyUnitNativeValidity(target), "target_validity")) return false;
        var other = current.ReadArmyUnitForceReference(target)
            ?? throw FalseReportDestinationMissing("target_Force_pointer");
        if (FRKnown(current.ReadForceNativeValidity(other), "target_Force_validity") &&
            (other == own || FRKnown(current.ReadForceNativeValidity(own), "own_Force_validity") &&
                FRKnown(ArmyMarchDevelopmentSource.ReadFriendlyRelation(state, own, other), "target_relation"))) return false;
        int physical = FRKnown(target.PhysicalSlot, "target_identity");
        var image = FArmy(state, physical);
        if (FRKnown(ArmyConfigurationSourceAdapter.ReadCurrentLand(state, image), "target_formation1B") is 19 or 20)
            return false;
        var runtime = image.NativeTacticRuntime ?? throw FalseReportDestinationMissing("target_abnormal_owner");
        if (runtime.Abnormal23 != 0 && runtime.Abnormal24 != 0 && runtime.Abnormal25 != 0) return false;
        if (FRKnown(ArmySavedCommandKnowledge.ReadControl(state, image), "target_control140") != 0) return false;
        return ReadFalseReportDestinations(current, own, target, firstOnly: true).Count != 0;
    }

    //2AC5B0: target Area plus its real one- and two-link neighbors, then
    //the actual E0 Object list in source order. Membership is by receiver,
    //not projected Area IDs, City distance, arbitrary ground or an Army path.
    private static IReadOnlyList<ArmyFalseReportDestination> ReadFalseReportDestinations(
        SourceWorldContext current, ForceReference own, ArmyUnitReference target, bool firstOnly = false)
    {
        int raw = FRKnown(current.ReadArmyUnitRawHex2A(target), "target_Hex2A");
        var at = current.ReadHexTableReference(raw < 48400 ? raw : 0)
            ?? throw FalseReportDestinationMissing("target_Hex_pointer");
        int areaRaw = FRKnown(current.ReadHexAreaSlot(at), "target_Area_link");
        var area = current.ReadAreaTableReference(areaRaw <= 500 ? areaRaw : 0)
            ?? throw FalseReportDestinationMissing("target_Area_pointer");
        var members = ReadFalseReportAreaRange(current, area);
        var result = new List<ArmyFalseReportDestination>();
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Object)
            ?? throw FalseReportDestinationMissing("Object_registry_E0"))
        {
            if (entry is not { Kind: RegistryKind.Object, PhysicalSlot: int physical })
                throw FalseReportDestinationMissing("Object_registry_pointer");
            var obj = new ObjectReference(physical);
            var definition = FacilityConstructionLegality.DefinitionOf(current, obj);
            if (FRKnown(current.ReadObjectDefinitionRaw2C(definition), "Object_definition2C") != 4) continue;
            int ownerRaw = FRKnown(current.ReadObjectForceSlot13(obj), "Object_Force13");
            var owner = current.ReadForceTableReference(ownerRaw <= 51 ? ownerRaw : 0)
                ?? throw FalseReportDestinationMissing("Object_Force_pointer");
            if (owner != own || !FRKnown(FacilityActivationCurrent.ReadEffectsEnabled(current, obj), "Object_active16")) continue;
            int position = FRKnown(current.ReadObjectHexIndex(obj), "Object_Hex10");
            var center = current.ReadHexTableReference(position < 48400 ? position : 0)
                ?? throw FalseReportDestinationMissing("Object_Hex_pointer");
            int linked = FRKnown(current.ReadHexAreaSlot(center), "Object_Area_link");
            var location = current.ReadAreaTableReference(linked <= 500 ? linked : 0)
                ?? throw FalseReportDestinationMissing("Object_Area_pointer");
            if (!members.Contains(location)) continue;
            result.Add(new(physical, FRKnown(center.PhysicalSlot, "Object_Hex_identity")));
            if (firstOnly) break;
        }
        return result.AsReadOnly();
    }

    private static HashSet<AreaReference> ReadFalseReportAreaRange(SourceWorldContext current, AreaReference area)
    {
        var members = new HashSet<AreaReference> { area };
        foreach (var next in ReadFalseReportAreaNeighbors(current, area))
        {
            members.Add(next);
            foreach (var second in ReadFalseReportAreaNeighbors(current, next)) members.Add(second);
        }
        return members;
    }

    private static List<AreaReference> ReadFalseReportAreaNeighbors(SourceWorldContext current, AreaReference from)
    {
            var values = new List<AreaReference>();
            for (int index = 0; index < 8; index++)
            {
                ushort link = FRKnown(current.ReadAreaNeighborSlot(from, index), "Area_neighbor56");
                if (link == 0) continue;
                var next = current.ReadAreaTableReference(link <= 500 ? link : 0)
                    ?? throw FalseReportDestinationMissing("Area_neighbor_pointer");
                if (FRKnown(current.ReadAreaNativeValidity(next), "Area_neighbor_validity")) values.Add(next);
            }
            return values;
    }

    private static T FRKnown<T>(T? value, string field) where T : struct =>
        value ?? throw FalseReportDestinationMissing(field);
    private static InvalidDataException FalseReportDestinationMissing(string field) =>
        new("army.false_report_destination." + field);
}
