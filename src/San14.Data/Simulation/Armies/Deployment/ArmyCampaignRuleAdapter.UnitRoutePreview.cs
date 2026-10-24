using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Ordinary mode3 UI row -> reset353E80 description. One captured
    /// current owner services later flags1 single-segment searches. Saved Army
    /// action, target, trait cache and status modifiers are not copied into it.</summary>
    public UnitRoutePreviewQuery PrepareUnitRoutePreview(GameState state, int unitId)
        => PrepareUnitRoutePreview(state, unitId, () => CaptureDeploymentDraftMarchCurrent(state));

    private UnitRoutePreviewQuery PrepareUnitRoutePreview(GameState state, int unitId, Func<SourceWorldContext> capture)
    {
        ArgumentNullException.ThrowIfNull(state);
        int? initialHex = null;
        try
        {
            if (!state.Units.TryGetValue(unitId, out var army)) throw RoutePreviewMissing("army_owner");
            if (army.NativeArmyOrigin?.NativeSlotIndex is not >= 1 or > 500 ||
                army.NativeTacticRuntime is not { Status10: 1 }) throw RoutePreviewMissing("ordinary_army_header");
            // This is the existing draft-search current owner: actual saved
            // Person/Base/index/receiver bindings, no World copy or encoding.
            var current = capture();
            var receiver = new ArmyUnitReference(army.NativeArmyOrigin.NativeSlotIndex);
            if (!RoutePreviewNeed(current.ReadArmyUnitNativeValidity(receiver), "army_validity") ||
                RoutePreviewNeed(current.ReadArmyUnitCommanderId(receiver), "army_commander") != army.OfficerId)
                throw RoutePreviewMissing("army_receiver_owner");
            int rawHex = RoutePreviewNeed(ArmyMarchSourceAdapter.ReadCurrentHex(state, army), "army_current_hex");
            initialHex = (current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0)
                ?? throw RoutePreviewMissing("current_hex_pointer")).PhysicalSlot
                ?? throw RoutePreviewMissing("current_hex_identity");
            int person = (army.OfficerId is >= 0 and <= 6000 ? current.ReadPersonLookupReference(army.OfficerId)
                : current.ReadTransferPersonTableReference(0))?.PhysicalSlot ?? throw RoutePreviewMissing("commander_pointer");
            if (!RoutePreviewNeed(current.ReadPersonNativeValidity(person), "commander_validity") ||
                RoutePreviewNeed(current.ReadPersonIdentity(person), "commander_identity") != army.OfficerId)
                throw RoutePreviewMissing("valid_commander_owner");
            if (army.Troops is < 0 or > ushort.MaxValue) throw RoutePreviewMissing("native_troop_encoding");

            //7D8280 copies Army1B/1C into the UI row unchanged. The reset
            //descriptor applies241650/241E20 rather than using the live bytes.
            int land = RoutePreviewNeed(ArmyConfigurationSourceAdapter.ReadCurrentLand(state, army), "current_land");
            int naval = RoutePreviewNeed(ArmyConfigurationSourceAdapter.ReadNaval(state, army), "current_naval");
            land = land <= 20 && RoutePreviewNeed(TacticRemakeCurrentFormations.ReadRawByte(state, land, 0x7E), "land_kind") != 2 ? land : 0;
            if (naval > 20 || RoutePreviewNeed(TacticRemakeCurrentFormations.ReadRawByte(state, naval, 0x7E), "naval_kind") != 2)
                naval = DeploymentNavalFormationSource.Resolve(state, current, army.OfficerId, person);
            // The shared mobility kernel's nonordinary/default0 branches are
            // unclosed. Preserve that gap instead of replacing reset0 with1.
            if (land is < 1 or > 20) throw RoutePreviewMissing("reset_land0_motion_branch");
            if (naval is < 1 or > 20 || RoutePreviewNeed(TacticRemakeCurrentFormations.ReadRawByte(state, naval, 0x7E), "normalized_naval_kind") != 2)
                throw RoutePreviewMissing("normalized_naval_motion_branch");

            //353E80 uses Person home/morale; Army current morale and current
            //Hex are not substitutes for the description's home fields.
            int forceLookup = RoutePreviewNeed(current.ReadPersonOldForceSlot(person), "commander_force");
            var force = current.ReadForceTableReference(forceLookup <= 51 ? forceLookup : 0)
                ?? throw RoutePreviewMissing("force_pointer");
            int forcePhysical = force.PhysicalSlot ?? throw RoutePreviewMissing("force_identity");
            var context = new TacticRemakeCurrentContext(current, _catalog, _map, _terrain, _usage, Revision);
            int maximumMorale = RoutePreviewNeed(context.ForceMoraleMaximum(state, forcePhysical), "maximum_morale");
            if (maximumMorale is < 0 or > byte.MaxValue) throw RoutePreviewMissing("morale_byte_branch");
            var home = BaseLookup.Find(RoutePreviewNeed(current.ReadPersonFirstLocation(person), "commander_first_location"), current);
            int morale = Math.Min(RoutePreviewNeed(current.ReadBaseMorale44(home), "home_morale"), maximumMorale);
            var homePosition = RoutePreviewNeed(current.ReadBaseNativeValidity(home), "home_validity") ? home
                : current.ReadCityTableReference(22) ?? throw RoutePreviewMissing("fallback_city22_pointer");
            int link = RoutePreviewNeed(current.ReadBaseObjectLink(homePosition), "home_object_link");
            var homeObject = current.ReadObjectTableReference(link <= 3000 ? link : 0) ?? throw RoutePreviewMissing("home_object_pointer");
            int homeRawHex = RoutePreviewNeed(current.ReadObjectNativeValidity(homeObject), "home_object_validity")
                ? RoutePreviewNeed(current.ReadObjectHexIndex(homeObject), "home_object_hex") : 48400;
            int homeHex = Math.Clamp(homeRawHex, 0, 48399);
            // The existing input's ForcePhysicalSlot name is historical: Motion
            // and its mobility evaluator consume this raw Force lookup once.
            var input = new DeploymentDraftMarchInputs(army.OfficerId, person, forceLookup,
                army.Troops, (byte)land, (byte)naval, homeHex, morale);
            var motion = new DeploymentDraftMarchSource.Motion(state, current, input);
            return new(unitId, initialHex, null, ReadSegment);

            UnitRoutePreviewSegment ReadSegment(int start, int goal)
            {
                if (start is < 0 or >= 48400 || goal is < 0 or >= 48400)
                    return Unknown(start, goal, "unit.route_preview.segment_endpoints");
                try
                {
                    int[] path = DeploymentDraftMarchSource.Search(state, current, motion, start, goal, out int expanded);
                    return new(unitId, start, goal, path.Length == 0 ? DeploymentDraftRouteStatus.NoPath : DeploymentDraftRouteStatus.Found,
                        Array.AsReadOnly(path), expanded, null);
                }
                catch (InvalidDataException error) { return Unknown(start, goal, error.Message); }
                catch (ArmyRouteSearch.MissingOwner error) { return Unknown(start, goal, error.Message); }
            }
        }
        catch (InvalidDataException error) { return new(unitId, initialHex, error.Message, (start, goal) => Unknown(start, goal, error.Message)); }
        catch (ArmyRouteSearch.MissingOwner error) { return new(unitId, initialHex, error.Message, (start, goal) => Unknown(start, goal, error.Message)); }

        UnitRoutePreviewSegment Unknown(int start, int goal, string reason) =>
            new(unitId, start, goal, DeploymentDraftRouteStatus.Unresolved, null, 0, reason);
    }
    private static T RoutePreviewNeed<T>(T? value, string input) where T : struct => value ?? throw RoutePreviewMissing(input);
    private static InvalidDataException RoutePreviewMissing(string input) => new("unit.route_preview." + input);
}
