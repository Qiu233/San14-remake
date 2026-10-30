using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>7C4160's tag1 ->353FE0 ->7C2E00 reset descriptor. One current
    /// capture and one flags1 search; no registered Army, live mission, rewrite,
    /// fake ordinary row or world clone. Path count is the actual segment status.</summary>
    public TransportDraftMarch QueryTransportDraftMarch(GameState state, int sourceCityId,
        int officerId, int troops, int navalFormationId, int startHex, int goalHex)
        => ReadTransportDraftMarch(state,sourceCityId,officerId,troops,navalFormationId,startHex,goalHex,()=>CaptureDeploymentDraftMarchCurrent(state));

    private TransportDraftMarch ReadTransportDraftMarch(GameState state, int sourceCityId,
        int officerId, int troops, int navalFormationId, int startHex, int goalHex, Func<SourceWorldContext> capture)
    {
        try
        {
            if (startHex is < 0 or >= 48400 || goalHex is < 0 or >= 48400) throw TM("march-endpoints");
            var current = capture();
            var motion = ReadTransportDraftMotionCurrent(state, current, sourceCityId,
                officerId, troops, navalFormationId);
            var goal = current.ReadHexTableReference(goalHex) ?? throw TM("march-goal-pointer");
            if (goal.PhysicalSlot is null) return NoPath();
            int code = TK(current.ReadHexSurfaceCode(goal), "march-goal-surface");
            var terrain = current.ReadSurfaceTypeTableReference(code <= 30 ? code : 0) ?? throw TM("march-goal-terrain");
            if (TK(current.ReadSurfaceTypeRawField2A(terrain), "march-goal-cost") == 0) return NoPath();
            int[] path = ArmyRouteSearch.Search(new ArmyRouteSearch.Queries(state, current,
                motion, startHex, goalHex, byte.MaxValue, 0, null, 1), out int expanded);
            if (path.Length == 0) return new(DeploymentDraftRouteStatus.NoPath, [], 0, null, expanded, null, null);
            var eta = ReadTransportDraftEtaCurrent(state, current, motion, path[..^1]);
            return new(DeploymentDraftRouteStatus.Found, Array.AsReadOnly(path), path.Length,
                eta.Days, expanded, null, eta.UnresolvedInput);
        }
        catch (InvalidDataException error) { return new(DeploymentDraftRouteStatus.Unresolved, null, null, null, 0, error.Message, null); }
        catch (ArmyRouteSearch.MissingOwner error) { return new(DeploymentDraftRouteStatus.Unresolved, null, null, null, 0, error.Message, null); }
        static TransportDraftMarch NoPath() => new(DeploymentDraftRouteStatus.NoPath, [], 0, null, 0, null, null);
    }

    private TransportDraftMotion ReadTransportDraftMotionCurrent(GameState state, SourceWorldContext current,
        int sourceCityId, int officerId, int troops, int navalFormationId)
    {
        var source = TransportSource(state, current, sourceCityId);
        int person = TK(current.ReadPersonLookupReference(officerId)?.PhysicalSlot, "march-person");
        if (!TK(current.ReadPersonNativeValidity(person), "march-person-validity") ||
            TK(current.ReadPersonIdentity(person), "march-person-identity") != officerId) throw TM("march-person-owner");
        if (TK(current.ReadPersonFirstLocation(person), "march-home") != TK(current.ReadBaseObjectLink(source), "march-source-link")) throw TM("march-source-owner");
        var force = TransportPersonForce(current, officerId);
        _ = TK(force.PhysicalSlot, "march-force");
        if (!TK(current.ReadForceNativeValidity(force), "march-force-validity")) throw TM("march-force-invalid");
        //The reset motion interface consumes the raw Army10/Person
        //District force key and resolves its table entry itself. Do not
        //feed it a resolved physical identity for a second lookup.
        int owner = TK(current.ReadPersonOldForceSlot(person), "march-raw-force-key");
        int naval = navalFormationId;
        if (naval is < 1 or > 20 || TK(TacticRemakeCurrentFormations.ReadRawByte(state, naval, 0x7E), "march-naval-kind") != 2)
            naval = TK(TransportArmyPacketSource.ReadDefaultNaval(state, current, force), "march-naval-fallback");
        return new TransportDraftMotion(state, current, person, owner,
            Math.Clamp(troops, 0, 65535), (byte)naval, Math.Clamp(TransportSourceHex(current, source), 0, 48399));
    }

    private TransportDraftEta ReadTransportDraftEta(GameState state, int sourceCityId, int officerId,
        int troops, int navalFormationId, IReadOnlyList<int> etaPath, Func<SourceWorldContext> capture)
    {
        //7C65D0's short-list branch precedes constructing the reset descriptor.
        if (etaPath.Count < 2) return new(0, null);
        try
        {
            var current = capture();
            var motion = ReadTransportDraftMotionCurrent(state, current, sourceCityId, officerId, troops, navalFormationId);
            return ReadTransportDraftEtaCurrent(state, current, motion, etaPath);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    private static TransportDraftEta ReadTransportDraftEtaCurrent(GameState state, SourceWorldContext current,
        TransportDraftMotion motion, IReadOnlyList<int> etaPath)
    {
        if (etaPath.Count < 2) return new(0, null);
        try
        {
            return new(DeploymentDraftMarchSource.Eta(state, current, motion.LandFormation,
                motion.NavalFormation, motion.EtaMobility, etaPath), null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    /// <summary>Existing type2 ->7D8390 packet ->353FE0 reset descriptor.
    /// Endpoints are resolved physical grid identities from actual
    /// active groups. Descriptor Home remains the real Person E2 Base field;
    /// saved action, options and previous waypoints never enter reset motion.
    /// Search node numbers retain original183C00 semantics: each generated
    /// grid number is looked up by the shared kernel, without pre-normalizing
    /// the endpoints a second time. Returned numbers are native path indices.</summary>
    public TransportDraftMarch QueryTransportUnitDraftMarch(GameState state, int unitId,
        int startHex, int goalHex)
        => ReadTransportUnitDraftMarch(state,unitId,startHex,goalHex,()=>CaptureDeploymentDraftMarchCurrent(state));

    private TransportDraftMarch ReadTransportUnitDraftMarch(GameState state, int unitId,
        int startHex, int goalHex, Func<SourceWorldContext> capture)
    {
        try
        {
            if (startHex is < 0 or >= 48400 || goalHex is < 0 or >= 48400) throw TransportUnitQueryMissing("march-endpoints");
            var current = capture();
            if (!ValidateTransportSelectedSet(state, current, [unitId])) throw TransportUnitQueryMissing("march-type2-selection");
            var unit = state.Units[unitId];
            var army = new ArmyUnitReference(TransportReceiver(state, unit));
            int officer = TU(current.ReadArmyUnitCommanderId(army), "march-commander12");
            int person = TU(current.ReadPersonLookupReference(officer <= 6000 ? officer : 0)?.PhysicalSlot, "march-person");
            if (!TU(current.ReadPersonNativeValidity(person), "march-person-validity")) throw TransportUnitQueryMissing("march-person-invalid");
            var force = current.ReadArmyUnitForceReference(army);
            int rawForce = current.ReadArmyUnitRawForceSlot(army);
            int troops = TU(current.ReadArmyReturnResourceFields(army)?.Ready16, "march-ready16");
            int naval = TU(ArmyConfigurationSourceAdapter.ReadNaval(state, unit), "march-naval1C");
            if (naval is < 1 or > 20 || TU(TacticRemakeCurrentFormations.ReadRawByte(state, naval, 0x7E), "march-naval-kind") != 2)
                naval = TU(TransportArmyPacketSource.ReadDefaultNaval(state, current, force), "march-naval-fallback");
            var homeLocation = PersonPositionRepair.DecodeSecondLocation(TU(current.ReadPersonFirstLocation(person), "march-homeE2"));
            BaseReference? home = homeLocation.Kind switch
            {
                5 => current.ReadCityTableReference(homeLocation.Index),
                6 => current.ReadGateTableReference(homeLocation.Index),
                _ => new(BaseKind.City, null)
            };
            if (home is null) throw TransportUnitQueryMissing("march-home-pointer");
            if (!TU(current.ReadBaseNativeValidity(home), "march-home-validity")) throw TransportUnitQueryMissing("march-home-invalid");
            int homeLookup = Math.Clamp(TransportSourceHex(current, home), 0, 48399);
            var goal = new HexReference(goalHex);
            int code = TU(current.ReadHexSurfaceCode(goal), "march-goal-surface");
            var terrain = current.ReadSurfaceTypeTableReference(code <= 30 ? code : 0) ?? throw TransportUnitQueryMissing("march-goal-terrain");
            if (TU(current.ReadSurfaceTypeRawField2A(terrain), "march-goal-cost") == 0)
                return new(DeploymentDraftRouteStatus.NoPath, [], 0, null, 0, null, null);
            var motion = new TransportDraftMotion(state, current, person, rawForce, troops, (byte)naval, homeLookup);
            int[] path = ArmyRouteSearch.Search(new ArmyRouteSearch.Queries(state, current,
                motion, startHex, goalHex, byte.MaxValue, 0, null, 1), out int expanded);
            return path.Length == 0
                ? new(DeploymentDraftRouteStatus.NoPath, [], 0, null, expanded, null, null)
                : new(DeploymentDraftRouteStatus.Found, Array.AsReadOnly(path), path.Length,
                    null, expanded, null, "transport.unit-target.march-eta-type2-flags1");
        }
        catch (InvalidDataException error) { return new(DeploymentDraftRouteStatus.Unresolved, null, null, null, 0, error.Message, null); }
        catch (ArmyRouteSearch.MissingOwner error) { return new(DeploymentDraftRouteStatus.Unresolved, null, null, null, 0, error.Message, null); }
    }

    private sealed class TransportDraftMotion(GameState state, SourceWorldContext current,
        int person, int force, int troops, byte naval, int home) : ArmyRouteSearch.Movement
    {
        private bool? wall;
        internal override bool AvoidHostile => false;
        internal override byte LandFormation => 19;
        internal override byte NavalFormation => naval;
        internal override int Force => force;
        internal override int HomeHex => home;
        internal override bool WallPermission => wall ??= !TK(TacticRemakeCurrentTraits.CommanderImmune(current, person, 102), "march-wall-permission");
        internal override byte? Action => 0;
        internal override bool HasNoDestination => true;
        internal float EtaMobility(int formation, int previousHex)
        {
            //353FE0 keeps type2, land19, actual naval/Person and troop count.
            //266860 replaces only position with the previous path Hex and
            //requests2B0660's condition-evaluating form, without explicit surface.
            var result = ArmyRouteMobilityEvaluation.Read(state, current,
                new(person, force, 2, previousHex, 19, naval, troops, true, () => (byte)0), formation, null);
            return result.Value ?? throw new InvalidDataException(result.UnresolvedOwner);
        }
        internal override float ReadMobility(int formation, byte explicitSurface)
        {
            var result = ArmyRouteMobilityEvaluation.Read(state, current,
                new(person, force, 2, home, 19, naval, troops, false, () => (byte)0), formation, explicitSurface);
            return result.Value ?? throw new InvalidDataException(result.UnresolvedOwner);
        }
    }
}
