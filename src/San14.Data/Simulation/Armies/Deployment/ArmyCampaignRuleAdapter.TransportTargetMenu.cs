using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public TransportTargetMenu QueryTransportTargetMenu(GameState state, int sourceCityId,
        int officerId, int navalFormationId, int clickedHexId, TransportTargetRouteContext route)
    {
        if (clickedHexId is < 0 or >= 48400) return new(clickedHexId, null, false, null, null, [], null);
        try
        {
            return new TransportTargetReader(this, state, sourceCityId, officerId, navalFormationId, clickedHexId, route).Read();
        }
        catch (InvalidDataException error) { return new(clickedHexId, null, null, null, null, [], error.Message); }
    }

    /// <summary>Mode3 no-kind0 selected-set prefix. Stay precedes the
    /// cached-segment gate; options use a common classifier, not ANDed menus.</summary>
    public TransportTargetMenu QueryTransportUnitsTargetMenu(GameState state,
        IReadOnlyList<int> unitIds, int clickedHexId, TransportTargetRouteContext route)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(unitIds);
        ArgumentNullException.ThrowIfNull(route);
        if (clickedHexId is < 0 or >= 48400) return new(clickedHexId, null, false, null, null, [], null);
        try
        {
            CheckTransportSelectionShape(unitIds);
            var current = CaptureFacilityConstructionCurrent(state);
            if (!ValidateTransportSelectedSet(state, current, unitIds))
                return new(clickedHexId, null, false, null, null, [], null);
            return new TransportTargetReader(state, current, unitIds.Select(id => state.Units[id]).ToArray(),
                clickedHexId, route).Read();
        }
        catch (InvalidDataException error) { return new(clickedHexId, null, null, null, null, [], error.Message); }
    }

    private sealed class TransportTargetReader
    {
        private readonly GameState state;
        private readonly SourceWorldContext current;
        private readonly int sourceId, officer, naval, clicked;
        private readonly TransportTargetRouteContext route;
        private readonly IReadOnlyList<UnitState>? existingUnits;
        private readonly List<UnitTargetMenuOption> options = [];
        private ForceReference own = new(null);
        private HexReference hex = new(null);
        private ObjectReference obj = new(null);
        private BaseReference? targetBase;
        private ArmyUnitReference? targetArmy;
        private UnitTargetMenuKind? kind;
        private UnitTargetMenuRelation? relation;
        private ForceReference? classifiedOwner;
        private int target;
        private int homeHex;
        private string? classMissing, relationMissing;
        internal TransportTargetReader(ArmyCampaignRuleAdapter adapter, GameState state,
            int source, int officer, int naval, int clicked, TransportTargetRouteContext route)
        {
            this.state = state; sourceId = source; this.officer = officer; this.naval = naval;
            this.clicked = clicked; this.route = route; target = clicked;
            current = adapter.CaptureFacilityConstructionCurrent(state);
        }
        internal TransportTargetReader(GameState state, SourceWorldContext current,
            IReadOnlyList<UnitState> units, int clicked, TransportTargetRouteContext route)
        {
            this.state = state; this.current = current; existingUnits = units;
            officer = units[0].OfficerId; this.clicked = clicked; this.route = route; target = clicked;
        }
        internal TransportTargetMenu Read()
        {
            if (existingUnits is null)
            {
            var source = TransportSource(state, current, sourceId);
            homeHex = TransportSourceHex(current, source);
            int p = TK(current.ReadPersonLookupReference(officer)?.PhysicalSlot, "target-person");
            if (!TK(current.ReadPersonNativeValidity(p), "target-person-validity")) return Result(false);
            if (TK(current.ReadPersonFirstLocation(p), "target-person-home") != TK(current.ReadBaseObjectLink(source), "target-source-link"))
                return Result(false);
            own = TransportPersonForce(current, officer);
            if (!TK(current.ReadForceNativeValidity(own), "target-force-validity")) return Result(false);
            }
            else own = current.ReadArmyUnitForceReference(new(TransportReceiver(state, existingUnits[0])));
            var pressed = Hex(clicked);
            byte code = TK(current.ReadHexSurfaceCode(pressed), "target-pressed-surface");
            var surface = current.ReadSurfaceTypeTableReference(code <= 30 ? code : 0) ?? throw TM("target-surface-pointer");
            if (TK(current.ReadSurfaceTypeRawField2A(surface), "target-surface-cost") == 0 ||
                surface.PhysicalSlot == 24 && !NavalAllows24()) return Result(false);
            var values = state.NativeArmySimulation?.Parameters.Int32Values ?? throw TM("target-parameters");
            if (values.GetValueOrDefault(TacticRemakeCurrentBattleControl.VersionKey) != 1 ||
                !values.TryGetValue(TacticRemakeCurrentBattleControl.HeaderKey, out int tutorial)) throw TM("target-tutorial-owner");
            if (tutorial != 0) throw TM("target-active-tutorial");
            obj = Object(pressed);
            if (TK(current.ReadObjectNativeValidity(obj), "target-pressed-object-validity"))
            {
                int raw = TK(current.ReadObjectHexIndex(obj), "target-object-hex");
                int lookup = raw <= 48399 ? raw : 0;
                target = existingUnits is null ? lookup : TK((current.ReadHexTableReference(lookup)
                    ?? throw TM("target-object-hex-pointer")).PhysicalSlot, "target-object-hex-receiver");
            }
            hex = Hex(target); obj = Object(hex);
            try { Classify(); } catch (InvalidDataException error) { classMissing = error.Message; }
            try { Relate(); } catch (InvalidDataException error) { relationMissing = error.Message; }
            if (existingUnits is not null)
                Add("stay", 0x282, 0x56A0, SameSite,
                    () => new("move", new(DeploymentTargetKind.Hex, PhysicalTarget())));
            //This is the no-kind0 prefix of7C70D0. No ordinary combat suffix.
            Add("waypoint", 0x395, 0x569F, () => RoutePositive() && Relation() is UnitTargetMenuRelation.Own or UnitTargetMenuRelation.Friendly &&
                Kind() is UnitTargetMenuKind.Empty or UnitTargetMenuKind.Army or UnitTargetMenuKind.Trap &&
                !(Relation() == UnitTargetMenuRelation.Friendly && Kind() == UnitTargetMenuKind.Army) && !SameSite() && PassableWaypoint(),
                null, waypoint: true);
            Add("move", 0x261, 0x56A1, () => RoutePositive() && Relation() is UnitTargetMenuRelation.Own or UnitTargetMenuRelation.Friendly &&
                Kind() is UnitTargetMenuKind.Empty or UnitTargetMenuKind.Army or UnitTargetMenuKind.Trap &&
                !(Relation() == UnitTargetMenuRelation.Friendly && Kind() == UnitTargetMenuKind.Army) && !SameSite(),
                () => new("move", new(DeploymentTargetKind.Hex, existingUnits is null ? target : PhysicalTarget())));
            Add("enter", 0x26B, 0x56A2, () => RoutePositive() && Kind() == UnitTargetMenuKind.Base &&
                Relation() == UnitTargetMenuRelation.Own && (existingUnits is not null || Index() > 0 || target != homeHex),
                () => new("enter", BaseTarget()));
            return Result(true);
        }
        private int Index() => route.HasActiveRouteGroup && route.FirstActiveWaypointIndex is >= 0 and <= 5
            ? route.FirstActiveWaypointIndex.Value : throw TM("target-active-group-index");
        private bool RoutePositive()
        {
            if (route.SegmentStatuses.Count < Index()) throw TM("target-segment-owner-count");
            if (route.SegmentStatuses.Any(status => status is <= 0)) return false;
            if (route.SegmentStatuses.Any(status => status is null)) throw TM("target-segment-status");
            return true;
        }
        private bool SameSite() => existingUnits is null ? target == homeHex && Index() == 0
            : existingUnits.Count == 1 && ReadTransportUnitTargetPosition(current, officer) == PhysicalTarget() &&
                route.HasActiveRouteGroup && Index() == 0;
        private int PhysicalTarget() => TK(hex.PhysicalSlot, "target-physical-hex");
        private bool NavalAllows24() => existingUnits is null ? naval is 12 or 13 or 17 or 18
            : existingUnits.All(unit => TK(ArmyConfigurationSourceAdapter.ReadNaval(state, unit), "selected-naval") is 12 or 13 or 17 or 18);
        private bool PassableWaypoint()
        {
            int code = TK(current.ReadHexSurfaceCode(hex), "waypoint-surface");
            var surface = current.ReadSurfaceTypeTableReference(code <= 30 ? code : 0) ?? throw TM("waypoint-surface-pointer");
            if (TK(current.ReadSurfaceTypeRawField2A(surface), "waypoint-cost") == 0 || code is 16 or 17) return false;
            return TK(obj.PhysicalSlot, "waypoint-object") == 0 || TK(current.ReadObjectDefinitionRaw2C(Definition()), "waypoint-object-kind") == 4;
        }
        private void Classify()
        {
            //7CDC40 feeds22DEF0(originalHex) to2AB640, which performs
            //another Hex table lookup for both ordinary and transport rows.
            //Keep that category receiver separate from the original target
            //used for Base links, generic Object refinement and relation.
            var classificationHex = current.ReadHexTableReference(PhysicalTarget())
                ?? throw TM("target-classification-hex-pointer");
            var classificationObject = Object(classificationHex);
            int physical = TK(classificationObject.PhysicalSlot, "target-classification-object-identity");
            if (physical is >= 1 and <= 51)
            {
                kind = UnitTargetMenuKind.Base;
                int link = TK(current.ReadHexObjectLink(hex), "target-city-link");
                targetBase = current.ReadCityTableReference(link <= 51 ? link : 0) ?? throw TM("target-city"); return;
            }
            if (physical is >= 52 and <= 61)
            {
                kind = UnitTargetMenuKind.Base;
                int link = TK(current.ReadHexObjectLink(hex), "target-gate-link");
                targetBase = current.ReadGateTableReference(link is >= 52 and <= 61 ? link - 51 : 0) ?? throw TM("target-gate"); return;
            }
            if (physical != 0)
            {
                if (!TK(current.ReadObjectNativeValidity(obj), "target-object-validity")) { kind = UnitTargetMenuKind.Empty; classifiedOwner = own; return; }
                classifiedOwner = ObjectForce();
                var definition = Definition();
                if (!TK(current.ReadObjectDefinitionValidity(definition), "target-definition-validity")) { kind = UnitTargetMenuKind.Empty; return; }
                int category = TK(current.ReadObjectDefinitionRaw2C(definition), "target-definition-kind");
                if (category == 4 && classifiedOwner != own)
                {
                    var occupant = OriginalTargetArmy();
                    if (TK(current.ReadArmyUnitNativeValidity(occupant), "target-trap-occupant"))
                    { kind = UnitTargetMenuKind.Army; targetArmy = occupant; return; }
                }
                kind = category switch { 2 => UnitTargetMenuKind.Facility, 3 => UnitTargetMenuKind.Obstacle,
                    4 => UnitTargetMenuKind.Trap, _ => throw TM("target-definition-category") }; return;
            }
            int marker = unchecked((short)TK(current.ReadHexRaw1C(classificationHex), "target-classification-army-link"));
            if (marker is >= 1 and <= 500)
            { targetArmy = OriginalTargetArmy(); kind = UnitTargetMenuKind.Army; }
            else kind = UnitTargetMenuKind.Empty;
        }
        private void Relate()
        {
            var force = classifiedOwner is not null && Kind() != UnitTargetMenuKind.Army ? classifiedOwner : Kind() switch
            {
                UnitTargetMenuKind.Empty => Force(TK(current.ReadHexForceSlot(hex), "target-hex-force")),
                UnitTargetMenuKind.Army => TK(current.ReadArmyUnitNativeValidity(targetArmy!), "target-army-validity") ? current.ReadArmyUnitForceReference(targetArmy!) : own,
                UnitTargetMenuKind.Base => TK(current.ReadBaseNativeValidity(targetBase!), "target-base-validity") ? Force(TK(current.ReadBaseForceSlot(targetBase!), "target-base-force")) : own,
                _ => ObjectForce()
            };
            if (!TK(current.ReadForceNativeValidity(force), "target-owner-validity")) { relation = UnitTargetMenuRelation.Other; return; }
            relation = force == own ? UnitTargetMenuRelation.Own : TK(ArmyMarchDevelopmentSource.ReadFriendlyRelation(state, own, force), "target-friendly-relation")
                ? UnitTargetMenuRelation.Friendly : UnitTargetMenuRelation.Other;
        }
        private DeploymentTarget BaseTarget()
        {
            int physical = TK(targetBase?.PhysicalSlot, "target-base-identity");
            int id = targetBase!.Kind == BaseKind.City ? physical : physical + 51;
            if (targetBase.Kind == BaseKind.City ? !state.Cities.ContainsKey(id) : !state.Gates.ContainsKey(id)) throw TM("target-base-owner");
            return new(DeploymentTargetKind.Location, id);
        }
        private void Add(string key, int caption, int tooltip, Func<bool> included, Func<DeploymentAction>? action, bool waypoint = false)
        {
            bool? present = null, enabled = null; DeploymentAction? command = null; string? missing = null; int? ordinal = null;
            try { present = included(); if (present == true) { enabled = !waypoint || Index() < 5; if (waypoint) ordinal = Index() + 1; command = action?.Invoke(); } }
            catch (InvalidDataException error) { missing = error.Message; }
            if (present != false) options.Add(new(key, caption, tooltip, present, enabled, command, missing) { WaypointOrdinal = ordinal });
        }
        private ArmyUnitReference OriginalTargetArmy()
        {
            int raw = unchecked((short)TK(current.ReadHexRaw1C(hex), "target-army-link"));
            //The native target lookup includes row0; it can alias a valid
            //receiver. The separate1..500 presence filter is not this lookup.
            if (raw < 0) throw TM("target-negative-army-lookup");
            return current.ReadArmyUnitTableReference(raw <= 500 ? raw : 0) ?? throw TM("target-army-pointer");
        }
        private ForceReference ObjectForce() => Force(TK(current.ReadObjectForceSlot13(obj), "target-object-force"));
        private ForceReference Force(int raw) => current.ReadForceTableReference(raw <= 51 ? raw : 0) ?? throw TM("target-force-pointer");
        private HexReference Hex(int raw) => existingUnits is null
            ? current.ReadHexTableReference(raw) ?? throw TM("target-hex-pointer") : new(raw);
        private ObjectReference Object(HexReference at) => FacilityConstructionLegality.ObjectAt(current, at);
        private ObjectDefinitionReference Definition() => FacilityConstructionLegality.DefinitionOf(current, obj);
        private UnitTargetMenuKind Kind() => kind ?? throw new InvalidDataException(classMissing ?? "transport.setup.target-classification");
        private UnitTargetMenuRelation Relation() => relation ?? throw new InvalidDataException(relationMissing ?? "transport.setup.target-relation");
        private TransportTargetMenu Result(bool? canOpen)
        {
            if (canOpen == true) canOpen = options.Any(option => option.Included == true) ? true : options.Count == 0 ? false : null;
            return new(clicked, existingUnits is null ? target : hex.PhysicalSlot, canOpen, kind, relation, options.AsReadOnly(), classMissing ?? relationMissing);
        }
    }
}
