using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The ordinary mode0 route group's first row searches once;
    /// every row integrates the shared route with its own motion inputs.
    /// This finite entry accepts one segment and no explicit waypoints.</summary>
    public DeploymentDraftMarch QueryDeploymentDraftMarch(GameState state,
        IReadOnlyList<DeploymentDraftRow> groupRows, int startHex, int goalHex)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(groupRows);
        try
        {
            if (groupRows.Count == 0) throw DraftMarchMissing("empty_route_group");
            if (startHex is < 0 or >= 48400 || goalHex is < 0 or >= 48400) throw DraftMarchMissing("segment_endpoints");
            if (groupRows.Any(row => row.Request.WaypointHexIds.Count != 0)) throw DraftMarchMissing("single_segment_scope");
            var current = CaptureDeploymentDraftMarchCurrent(state);
            var first = ReadDeploymentDraftMarchInputs(state, current, groupRows[0]);
            var motion = new DeploymentDraftMarchSource.Motion(state, current, first);
            int[] path = DeploymentDraftMarchSource.Search(state, current, motion, startHex, goalHex, out int expanded);
            int[] etaPath = path.Length == 0 ? [] : path[..^1];
            var rows = new List<DeploymentDraftMarchRow>();
            foreach (var row in groupRows)
            {
                int? days = null; string? missing = null;
                try
                {
                    if (etaPath.Length < 2) days = 0;
                    else
                    {
                        var input = ReferenceEquals(row, groupRows[0]) ? first : ReadDeploymentDraftMarchInputs(state, current, row);
                        days = DeploymentDraftMarchSource.Eta(state, current,
                            new DeploymentDraftMarchSource.Motion(state, current, input), etaPath);
                    }
                }
                catch (InvalidDataException error) { missing = error.Message; }
                rows.Add(new(row.Request.OfficerId, days, missing));
            }
            return new(path.Length > 0 ? DeploymentDraftRouteStatus.Found : DeploymentDraftRouteStatus.NoPath,
                path, etaPath, rows.AsReadOnly(), expanded, null);
        }
        catch (InvalidDataException error) { return Unknown(error.Message); }
        catch (ArmyRouteSearch.MissingOwner error) { return Unknown(error.Message); }

        DeploymentDraftMarch Unknown(string reason) => new(DeploymentDraftRouteStatus.Unresolved, null, null,
            groupRows.Select(row => new DeploymentDraftMarchRow(row.Request.OfficerId, null, reason)).ToArray(), 0, reason);
    }
    private SourceWorldContext CaptureDeploymentDraftMarchCurrent(GameState state)
    {
        var current = CaptureOfficerCommandCapacityCurrent(state);
        OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
        BaseCaptureCurrentInputs.BindCurrent(state, current);
        var arena = state.ReadArmyLifecycle() ?? throw DraftMarchMissing("saved_indexes");
        _ = WorldIndexContext.ImportOwnedState(current, arena.Indexes, state);
        current.RestoreOwnedArmyReturnSlots(state, arena);
        if (state.NativeArmySimulation?.Parameters is not { } profile || profile.Language != Seed.Language ||
            profile.RuleVersion != SimulationRuleIdentity.ForLocale(Seed.Language)) throw DraftMarchMissing("rule_profile");
        return current;
    }

    private DeploymentDraftMarchInputs ReadDeploymentDraftMarchInputs(GameState state, SourceWorldContext current, DeploymentDraftRow row)
    {
        int id = row.Request.OfficerId;
        int person = _definitions.ResolveOfficerSource(id) ?? throw DraftMarchMissing("person_identity");
        if (!state.Officers.ContainsKey(id) || DraftMarchNeed(current.ReadPersonIdentity(person), "person_identity") != id ||
            !DraftMarchNeed(current.ReadPersonNativeValidity(person), "person_validity")) throw DraftMarchMissing("valid_person");
        int land = row.Request.FormationId, naval = row.NavalFormationId;
        if (land is < 1 or > 20 || DraftMarchNeed(TacticRemakeCurrentFormations.ReadRawByte(state, land, 0x7E), "land_kind") != 1)
            throw DraftMarchMissing("land_setter_branch");
        if (naval is < 1 or > 20 || DraftMarchNeed(TacticRemakeCurrentFormations.ReadRawByte(state, naval, 0x7E), "naval_kind") != 2)
            throw DraftMarchMissing("naval_setter_fallback");
        int force = DraftMarchNeed(current.ReadPersonOldForceSlot(person), "person_force");
        if (force is < 1 or > 45) throw DraftMarchMissing("ordinary_force_scope");
        ushort home = DraftMarchNeed(current.ReadPersonFirstLocation(person), "person_first_location");
        var source = BaseLookup.Find(home, current);
        // 353E80's invalid home Base follows physical City22. Keep the
        // unproved alternate owner explicit instead of using UI source.
        if (!DraftMarchNeed(current.ReadBaseNativeValidity(source), "home_base_validity")) throw DraftMarchMissing("home_base_fallback_city22");
        int link = DraftMarchNeed(current.ReadBaseObjectLink(source), "home_object_link");
        var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0) ?? throw DraftMarchMissing("home_object_pointer");
        int rawHex = DraftMarchNeed(current.ReadObjectNativeValidity(obj), "home_object_validity")
            ? DraftMarchNeed(current.ReadObjectHexIndex(obj), "home_object_hex") : 48400;
        int hex = Math.Clamp(rawHex, 0, 48399);
        var abilities = new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
        var context = new TacticRemakeCurrentContext(current, _catalog, _map, _terrain,
            new(Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language)), Revision, abilities);
        int maxMorale = DraftMarchNeed(context.ForceMoraleMaximum(state, force), "force_morale_maximum");
        if (maxMorale is < 0 or > 255) throw DraftMarchMissing("morale_byte_branch");
        int morale = Math.Min(DraftMarchNeed(current.ReadBaseMorale44(source), "home_morale"), maxMorale);
        return new(id, person, force, Math.Clamp(row.Request.Troops, 0, 65535), (byte)land, (byte)naval, hex, morale);
    }

    private static T DraftMarchNeed<T>(T? value, string field) where T : struct => value ?? throw DraftMarchMissing(field);
    private static InvalidDataException DraftMarchMissing(string field) => new("deployment.march." + field);
}
