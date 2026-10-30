using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>One accepted map-target query for the ordinary single-army
    /// editor. Caller owns input/modal/session permission; this reads original
    /// target classification and menu append gates from the committed world.</summary>
    public UnitTargetMenu QueryUnitTargetMenu(GameState state, int unitId, int clickedHexId,
        UnitTargetRouteContext route)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(route);
        if (!state.Units.TryGetValue(unitId, out var unit) || unit.Mission is null or { Kind: UnitMissionKind.Transport } ||
            clickedHexId is < 0 or >= 48400)
            return new(unitId, clickedHexId, null, false, null, null, [], null);
        try { return new UnitTargetMenuReader(this, state, unit, clickedHexId, route).Read(); }
        catch (InvalidDataException error) { return new(unitId, clickedHexId, null, null, null, null, [], error.Message); }
    }

    /// <summary>Ordinary mode3 selected-set menu. The caller supplies its
    /// ordered selected rows and real first active-group index. Captures one
    /// current owner and evaluates the common source predicates once.</summary>
    public UnitsTargetMenu QueryUnitsTargetMenu(GameState state, IReadOnlyList<int> unitIds,
        int clickedHexId, UnitTargetRouteContext route)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(unitIds);
        ArgumentNullException.ThrowIfNull(route);
        var ids = Array.AsReadOnly(unitIds.ToArray());
        if (ids.Count is < 1 or > 30 || ids.Distinct().Count() != ids.Count || clickedHexId is < 0 or >= 48400)
            return new(ids, clickedHexId, null, false, null, null, [], null);
        var units = new List<UnitState>(ids.Count);
        foreach (int id in ids)
        {
            if (!state.Units.TryGetValue(id, out var selected) || selected.Mission is null or { Kind: UnitMissionKind.Transport })
                return new(ids, clickedHexId, null, false, null, null, [], null);
            units.Add(selected);
        }
        try
        {
            UnitTargetMenu result = new UnitTargetMenuReader(this, state, units[0], clickedHexId,
                route, units.AsReadOnly()).Read();
            return new(ids, result.ClickedHexId, result.TargetHexId, result.CanOpen,
                result.Kind, result.Relation, result.Options, result.UnresolvedInput);
        }
        catch (InvalidDataException error) { return new(ids, clickedHexId, null, null, null, null, [], error.Message); }
    }

    /// <summary>The formal Fire order retains the original map press and its
    /// expected normalized target. It shares selected-receiver, target and Fire
    /// predicates with that menu using the caller's one current capture.
    /// Explicit actor permission is independent of the UI player. No success
    /// probability, route search, Build option or command execution is read.</summary>
    internal (bool? Allowed, string? UnresolvedInput) EvaluateArmyFireCommand(GameState state,
        SourceWorldContext current, int actorForceId, IReadOnlyList<UnitState> armies,
        int clickedPhysical, int targetPhysical, IReadOnlyList<int> freshWaypoints)
    {
        if (armies.Count is < 1 or > 30 || armies.Select(army => army.Id).Distinct().Count() != armies.Count ||
            clickedPhysical is < 0 or >= 48400 || targetPhysical is < 0 or >= 48400 || freshWaypoints.Count > 5 ||
            freshWaypoints.Any(point => point is < 0 or >= 48400)) return (false, null);
        try
        {
            var reader = new UnitTargetMenuReader(this, state, armies[0], clickedPhysical,
                new(true, freshWaypoints.Count), armies, current, actorForceId);
            return reader.ReadFireCommand(targetPhysical);
        }
        catch (InvalidDataException error) { return (null, error.Message); }
    }

    private sealed partial class UnitTargetMenuReader
    {
        private readonly ArmyCampaignRuleAdapter package;
        private readonly GameState state;
        private readonly UnitState? unit;
        private UnitState Unit => unit ?? throw Missing("deployed_army_owner");
        private readonly IReadOnlyList<UnitState> selectedUnits;
        private readonly bool selectedSet;
        private readonly int clicked;
        private readonly UnitTargetRouteContext route;
        private readonly SourceWorldContext current;
        private readonly int? commandActorForceId;
        private readonly List<UnitTargetMenuOption> options = [];
        private HexReference hex = new(null);
        private ObjectReference obj = new(null);
        private ForceReference own = new(null);
        private ForceReference? classifiedOwner;
        private ArmyUnitReference? targetArmy;
        private BaseReference? targetBase;
        private UnitTargetMenuKind? kind;
        private UnitTargetMenuRelation? relation;
        private bool? sameSite;
        private string? sameMissing, classMissing, relationMissing;
        private int targetHex;

        public UnitTargetMenuReader(ArmyCampaignRuleAdapter package, GameState state, UnitState unit,
            int clicked, UnitTargetRouteContext route, IReadOnlyList<UnitState>? selectedUnits = null,
            SourceWorldContext? suppliedCurrent = null, int? commandActorForceId = null)
        {
            this.package = package; this.state = state; this.unit = unit; this.clicked = clicked; this.route = route;
            selectedSet = selectedUnits is not null;
            this.selectedUnits = selectedUnits ?? new[] { unit };
            selectedOfficerIds = this.selectedUnits.Select(value => value.OfficerId).ToArray();
            this.commandActorForceId = commandActorForceId;
            targetHex = clicked;
            current = suppliedCurrent ?? package.CaptureFacilityConstructionCurrent(state);
        }

        public UnitTargetMenu Read()
        {
            if (!PrepareTarget()) return Result(false);

            Add("stay", 0x282, 0x56A0, () => SameSite(), () => new("stay", new(DeploymentTargetKind.Hex, targetHex)));
            Add("waypoint", 0x395, 0x569F, () => PassableWaypoint() && !SameSite(), null,
                enabled: () => ActiveIndex() < 5, ordinal: true);
            Add("move", 0x261, 0x56A1, () => (Kind() is UnitTargetMenuKind.Empty or UnitTargetMenuKind.Army or UnitTargetMenuKind.Trap) && !SameSite(),
                () => new("move", new(DeploymentTargetKind.Hex, targetHex)));
            Add("enter", 0x265, 0x56A2, () => Kind() == UnitTargetMenuKind.Base && Relation() == UnitTargetMenuRelation.Own,
                () => new("enter", BaseTarget()));
            Add("attack", 0x27D, 0x56A3, () => (Kind() is UnitTargetMenuKind.Army or UnitTargetMenuKind.Base or UnitTargetMenuKind.Facility or UnitTargetMenuKind.Obstacle) &&
                Relation() == UnitTargetMenuRelation.Other, () => new("attack", AttackTarget()));
            AddFire();
            AddConstruction();
            Add("repair", 0x10C, 0x56A5, () => Kind() == UnitTargetMenuKind.Obstacle ||
                (Kind() is UnitTargetMenuKind.Facility or UnitTargetMenuKind.Trap) && Relation() == UnitTargetMenuRelation.Own,
                () => new("repair", StructureTarget()));
            Add("demolish", 0x10D, 0x56A6, () => (Kind() is UnitTargetMenuKind.Facility or UnitTargetMenuKind.Trap) && Relation() == UnitTargetMenuRelation.Own,
                () => new("demolish", StructureTarget()));
            // Independent suffixes do not erase known movement/base rows.
            Add("occupy", 0x111, 0x56A7, AreaIncluded,
                () => new("occupy", new(DeploymentTargetKind.Area, Need(TargetArea().PhysicalSlot, "command_area_identity"))));
            Add("capture", 0x114, 0x56C1, CaptureIncluded,
                () => new("capture", AttackTarget()), presentationMissing: "unit.target_menu.false_report_probability");
            return Result(true);
        }

        internal (bool? Allowed, string? UnresolvedInput) ReadFireCommand(int expectedTarget)
        {
            if (!PrepareTarget() || targetHex != expectedTarget) return (false, null);
            return (FireIncluded(), null);
        }

        private bool PrepareTarget()
        {
            if (Unit.NativeArmyOrigin?.NativeSlotIndex is not int physical) throw Missing("selected_army_receiver");
            var selected = selectedSet ? new ArmyUnitReference(physical)
                : current.ReadArmyUnitTableReference(physical) ?? throw Missing("selected_army_pointer");
            if (!Need(current.ReadArmyUnitNativeValidity(selected), "selected_army_validity") ||
                Need(current.ReadArmyUnitCommanderId(selected), "selected_army_commander") != Unit.OfficerId)
                return false;
            if (Unit.NativeTacticRuntime is not { } runtime) throw Missing("selected_army_runtime");
            if (runtime.Status10 != 1) return false;
            own = current.ReadArmyUnitForceReference(selected);
            if (selectedSet && !ValidateSelectedSet()) return false;

            //The mode3 map picker supplies an actual CHex physical ordinal.
            //Saved Army2A and Area member numbers below remain table keys.
            var pressed = selectedSet ? new HexReference(clicked) : Hex(clicked);
            if (!PressedTerrainAllowed(pressed)) return false;
            return PrepareMapTarget(pressed);
        }

        // Shared pointer/normalization/classification leaves. Mode0 supplies
        // real draft Persons, while mode3 separately admits actual Army owners.
        private bool PrepareMapTarget(HexReference pressed)
        {
            //The outer7E3400 checks the actual pressed terrain, including
            //its physical24 naval gate, before7E4890 can snap an Object.
            //7E4890 then normalizes the footprint and checks that receiver's
            //terrain2A independently. It does not repeat the pressed naval gate.
            obj = Object(pressed);
            if (Need(current.ReadObjectNativeValidity(obj), "pressed_object_validity"))
            {
                int raw = ClampHex(Need(current.ReadObjectHexIndex(obj), "pressed_object_hex"));
                hex = Hex(raw);
                targetHex = selectedSet ? Need(hex.PhysicalSlot, "pressed_object_hex_receiver") : raw;
            }
            else { hex = pressed; targetHex = clicked; }
            obj = Object(hex);
            int surfaceCode = Need(current.ReadHexSurfaceCode(hex), "target_surface");
            var surface = current.ReadSurfaceTypeTableReference(surfaceCode <= 30 ? surfaceCode : 0)
                ?? throw Missing("target_surface_pointer");
            if (surface.PhysicalSlot is null || Need(current.ReadSurfaceTypeRawField2A(surface), "target_surface_cost") == 0)
                return false;
            if (!TutorialTargetQualification.AllowsHex(package.ReadTutorialControlState(state), current, targetHex,
                route.HasActiveRouteGroup ? route.FirstActiveWaypointIndex : null)) return false;

            try { Classify(); }
            catch (InvalidDataException error) { classMissing = error.Message; }
            try { ReadRelation(); }
            catch (InvalidDataException error) { relationMissing = error.Message; }
            try
            {
                sameSite = SelectedOfficerIds().Count == 1 && PersonPosition(SelectedOfficerIds()[0]) == hex.PhysicalSlot && route.HasActiveRouteGroup &&
                    (route.FirstActiveWaypointIndex ?? throw Missing("active_group_index")) == 0;
            }
            catch (InvalidDataException error) { sameMissing = error.Message; }

            return true;
        }

        private bool ValidateSelectedSet()
        {
            var arena = state.ReadArmyLifecycle() ?? throw Missing("selected_set_lifecycle");
            var slots = arena.ReceiverSlots();
            int permittedForce;
            if (commandActorForceId is int actor) permittedForce = actor;
            else
            {
                var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
                    ?? throw Missing("selected_set_player_pointer");
                permittedForce = Need(player.PhysicalSlot, "selected_set_player_identity");
            }
            int ownPhysical = Need(own.PhysicalSlot, "selected_set_force_identity");
            if (!Need(current.ReadForceNativeValidity(own), "selected_set_force_validity") || ownPhysical != permittedForce)
                return false;
            foreach (var selectedUnit in selectedUnits)
            {
                if (commandActorForceId.HasValue &&
                    (!state.Units.TryGetValue(selectedUnit.Id, out var actual) || !ReferenceEquals(actual, selectedUnit))) return false;
                int physical = selectedUnit.NativeArmyOrigin?.NativeSlotIndex ?? throw Missing("selected_set_army_receiver");
                if ((uint)physical >= (uint)slots.Length) throw Missing("selected_set_army_identity");
                if (slots[physical].ActiveUnitId != selectedUnit.Id) throw Missing("selected_set_active_owner");
                //NativeSlotIndex is the actual active receiver identity, not
                //a request to reinterpret it through an aliased lookup table.
                var selected = new ArmyUnitReference(physical);
                if (!Need(current.ReadArmyUnitNativeValidity(selected), "selected_set_army_validity")) return false;
                if (Need(current.ReadArmyUnitCommanderId(selected), "selected_set_commander") != selectedUnit.OfficerId)
                    throw Missing("selected_set_commander_identity");
                if (selectedUnit.NativeTacticRuntime is not { } runtime) throw Missing("selected_set_runtime");
                if (runtime.Status10 != 1) return false;
                byte land = Need(ArmyConfigurationSourceAdapter.ReadCurrentLand(state, selectedUnit), "selected_set_land");
                if (land is 19 or 20) return false; //The transport menu is a different source branch.
                var force = current.ReadArmyUnitForceReference(selected);
                if (!Need(current.ReadForceNativeValidity(force), "selected_set_member_force_validity") ||
                    Need(force.PhysicalSlot, "selected_set_member_force_identity") != ownPhysical) return false;
            }
            return true;
        }

        //7D7B70 applies2C50D0(24, current Naval) to every selected Person's
        //real Army. This is a scalar AND, not an intersection of menus.
        private bool AllSelectedNavalAllows24()
        {
            bool testedPerson = false;
            foreach (int officerId in SelectedOfficerIds())
            {
                var person = current.ReadPersonLookupReference(officerId <= 6000 ? officerId : 0)
                    ?? throw Missing("naval_Person_pointer");
                if (person.PhysicalSlot is not int physical ||
                    !Need(current.ReadPersonNativeValidity(physical), "naval_Person_validity")) continue;
                var position = PersonPositionRepair.DecodeSecondLocation(
                    Need(current.ReadPersonSecondLocation(physical), "naval_Person_E4"));
                int lookup = position.Kind == 27 && position.Index <= 500 ? position.Index : 0;
                var associated = current.ReadArmyUnitTableReference(lookup) ?? throw Missing("naval_associated_Army_pointer");
                if (Need(current.ReadArmyUnitCommanderId(associated), "naval_associated_commander") !=
                    Need(current.ReadPersonIdentity(physical), "naval_Person_identity"))
                    associated = current.ReadArmyUnitTableReference(0) ?? throw Missing("naval_default_Army_pointer");
                int naval;
                if (Need(current.ReadArmyUnitNativeValidity(associated), "naval_associated_Army_validity"))
                {
                    int armyPhysical = Need(associated.PhysicalSlot, "naval_associated_Army_receiver");
                    var arena = state.ReadArmyLifecycle() ?? throw Missing("naval_lifecycle");
                    if ((uint)armyPhysical >= (uint)arena.Slots.Count) throw Missing("naval_associated_Army_range");
                    var receiver = arena.Receivers.Find(arena.Slots[armyPhysical].ReceiverId)
                        ?? throw Missing("naval_associated_Army_owner");
                    var army = receiver.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) : receiver.PendingImage;
                    if (army is null) throw Missing("naval_associated_Army_fields");
                    naval = Need(ArmyConfigurationSourceAdapter.ReadNaval(state, army), "naval_associated_Army1C");
                }
                else
                {
                    //233240(Person,0) has its own native band/E2/Force
                    //predicate. Its true branch selects11 directly.
                    var force = current.ReadPersonForce(physical) ?? throw Missing("naval_Person_Force_pointer");
                    int identity = Need(current.ReadPersonIdentity(physical), "naval_Person_identity");
                    bool band = BattleRemakeCurrentRegionFactor.IsNativeBandPerson(identity) &&
                        Need(current.ReadPersonFirstLocation(physical), "naval_Person_E2") is >= 1 and <= 61 &&
                        Need(current.ReadForcePhysicalSlot(force), "naval_Person_Force_identity") is not (>= 46 and <= 50);
                    naval = band || !Need(current.ReadForceNativeValidity(force), "naval_Person_Force_validity")
                        ? 11 : ReadRawForceNaval(force);
                }
                testedPerson = true;
                if (naval is not (12 or 13 or 17 or 18)) return false;
            }
            if (testedPerson) return true;
            return ReadRawForceNaval(own) is 12 or 13 or 17 or 18;

            int ReadRawForceNaval(ForceReference force)
            {
                string key = BattleRemakeCurrentForcePolicies.ForceFieldKey(Need(force.PhysicalSlot, "naval_Force_receiver"), 0x15E);
                if (state.NativeArmySimulation?.Parameters is not { } bank || bank.Float32Values.ContainsKey(key) ||
                    !bank.Int32Values.TryGetValue(key, out int value) || value is < 0 or > 255) throw Missing("naval_Force15E");
                return value;
            }
        }

        private void Classify()
        {
            //7CDC40 calls2AB640 with22DEF0(originalHex). That classifier
            //really performs a second table lookup; its category is distinct
            //from the original receiver used below for trap and relation data.
            int nativeKind = NativeKind(hex);
            if (nativeKind == 5)
            {
                kind = UnitTargetMenuKind.Base;
                int link = Need(current.ReadHexObjectLink(hex), "target_city_link");
                targetBase = current.ReadCityTableReference(link <= 51 ? link : 0) ?? throw Missing("target_city_pointer"); return;
            }
            if (nativeKind == 6)
            {
                kind = UnitTargetMenuKind.Base;
                int link = Need(current.ReadHexObjectLink(hex), "target_gate_link");
                targetBase = current.ReadGateTableReference(link is >= 52 and <= 61 ? link - 51 : 0) ?? throw Missing("target_gate_pointer"); return;
            }
            if (nativeKind == 11)
            {
                //The native generic-Object category is refined with the
                //original target pointer, not classificationHex's Object.
                if (!Need(current.ReadObjectNativeValidity(obj), "classification_object_validity"))
                { kind = UnitTargetMenuKind.Empty; classifiedOwner = own; return; }
                classifiedOwner = ObjectForce(obj);
                var definition = Definition(obj);
                if (!Need(current.ReadObjectDefinitionValidity(definition), "classification_definition_validity")) { kind = UnitTargetMenuKind.Empty; return; }
                int category = Need(current.ReadObjectDefinitionRaw2C(definition), "classification_definition_kind");
                if (category == 4 && classifiedOwner != own)
                {
                    var occupant = OriginalTargetArmy();
                    if (Need(current.ReadArmyUnitNativeValidity(occupant), "trap_occupant_validity"))
                    { kind = UnitTargetMenuKind.Army; targetArmy = occupant; return; }
                }
                kind = category switch { 2 => UnitTargetMenuKind.Facility, 3 => UnitTargetMenuKind.Obstacle,
                    4 => UnitTargetMenuKind.Trap, _ => throw Missing("unsupported_definition_kind") };
                return;
            }
            if (nativeKind == 27)
            {
                kind = UnitTargetMenuKind.Army;
                targetArmy = OriginalTargetArmy();
            }
            else kind = UnitTargetMenuKind.Empty;
        }

        // 2AB640 consumes22DEF0(original), then actually performs a second
        // HexTable lookup. Pointer classification does not refine generic
        // Objects through7CDC40; the menu does so in Classify above.
        private int NativeKind(HexReference original)
        {
            var classificationHex = Hex(Need(original.PhysicalSlot, "classification_Hex_identity"));
            int physical = Need(Object(classificationHex).PhysicalSlot, "classification_Object_receiver");
            if (physical is >= 1 and <= 51) return 5;
            if (physical is >= 52 and <= 61) return 6;
            if (physical != 0) return 11;
            int marker = unchecked((short)Need(current.ReadHexRaw1C(classificationHex), "classification_Hex1C"));
            return marker is >= 1 and <= 500 ? 27 : 255;
        }

        private void ReadRelation()
        {
            var target = classifiedOwner is not null && Kind() != UnitTargetMenuKind.Army ? classifiedOwner : Kind() switch
            {
                UnitTargetMenuKind.Empty => HexForce(),
                UnitTargetMenuKind.Army => Need(current.ReadArmyUnitNativeValidity(targetArmy!), "target_army_validity")
                    ? current.ReadArmyUnitForceReference(targetArmy!) : own,
                UnitTargetMenuKind.Base => Need(current.ReadBaseNativeValidity(targetBase!), "target_base_validity")
                    ? Force(Need(current.ReadBaseForceSlot(targetBase!), "target_base_force")) : own,
                _ => ObjectForce(obj)
            };
            if (!Need(current.ReadForceNativeValidity(target), "target_force_validity")) { relation = UnitTargetMenuRelation.Other; return; }
            if (target == own) { relation = UnitTargetMenuRelation.Own; return; }
            relation = Need(current.ReadForceNativeValidity(own), "selected_force_validity") &&
                Need(ArmyMarchDevelopmentSource.ReadFriendlyRelation(state, own, target), "target_friendly_relation")
                ? UnitTargetMenuRelation.Friendly : UnitTargetMenuRelation.Other;
        }

        private bool PassableWaypoint()
        {
            int code = Need(current.ReadHexSurfaceCode(hex), "waypoint_surface");
            var surface = current.ReadSurfaceTypeTableReference(code <= 30 ? code : 0) ?? throw Missing("waypoint_surface_pointer");
            if (surface.PhysicalSlot is null || Need(current.ReadSurfaceTypeRawField2A(surface), "waypoint_surface_cost") == 0 || code is 16 or 17) return false;
            int physical = Need(obj.PhysicalSlot, "waypoint_object_receiver");
            return physical == 0 || Need(current.ReadObjectDefinitionRaw2C(Definition(obj)), "waypoint_object_kind") == 4;
        }

        private void AddConstruction()
        {
            bool? included = null; string? missing = null;
            try
            {
                if (!(Kind() is UnitTargetMenuKind.Empty or UnitTargetMenuKind.Army || Kind() == UnitTargetMenuKind.Trap && Relation() == UnitTargetMenuRelation.Other) ||
                    Relation() == UnitTargetMenuRelation.Friendly || SameSite())
                    included = false;
                else
                {
                    included = false;
                    HexListHandle? people = null;
                    int person = 0;
                    if (selectedSet)
                    {
                        people = current.CreateNearbyTemporary();
                        try
                        {
                            foreach (var selected in selectedUnits)
                            {
                                int at = Need(current.ReadPersonLookupReference(selected.OfficerId <= 6000 ? selected.OfficerId : 0)?.PhysicalSlot,
                                    "construction_person");
                                current.AppendConstructionPersonTemporary(people, at);
                            }
                        }
                        catch { current.ClearNearbyTemporary(people, true); throw; }
                    }
                    else person = Need(current.ReadPersonLookupReference(Unit.OfficerId <= 6000 ? Unit.OfficerId : 0)?.PhysicalSlot, "construction_person");
                    // This is the real menu's semantic definition search, not
                    // an integrity scan or proof. Known true settles this OR;
                    // unavailable trait collectors remain local unknowns.
                    try
                    {
                        for (int slot = 0; slot < 31; slot++)
                        {
                            try
                            {
                                var definition = current.ReadObjectDefinitionReference(slot) ?? throw Missing("construction_definition_pointer");
                                if (!Need(current.ReadObjectDefinitionValidity(definition), "construction_definition_validity") ||
                                    Need(current.ReadObjectDefinitionRaw2C(definition), "construction_definition_kind") == 1) continue;
                                bool allowed = people is null
                                    ? FacilityConstructionLegality.CheckCommander(state, current, person, hex, definition)
                                    : FacilityConstructionLegality.Check(state, current, people, hex, definition);
                                if (!allowed) continue;
                                included = true;
                                break; //The native menu stops at the first legal definition.
                            }
                            catch (InvalidDataException error) { missing ??= error.Message; }
                        }
                    }
                    finally { if (people is not null) current.ClearNearbyTemporary(people, true); }
                    if (included == false && missing is not null) included = null;
                    if (included == true) missing = null;
                }
            }
            catch (InvalidDataException error) { missing = error.Message; if (selectedSet) included = null; }
            if (included != false) options.Add(new("build", 0x10B, 0x56A4, included, included, null, missing));
        }

        private void AddFire()
        {
            int ordinal = options.Count;
            Add("fire", 0x113, 0x56A9, FireIncluded,
                () => new("fire", new(DeploymentTargetKind.Hex, Need(hex.PhysicalSlot, "fire_target_physical"))));
            if (options.Count == ordinal || options[ordinal].Included != true) return;
            try
            {
                var probability = package.ReadUnitsFireProbability(state, current,
                    selectedUnits.Select(selected => selected.Id).ToArray(), hex);
                options[ordinal] = options[ordinal] with
                {
                    SuccessRate = probability.DisplayRate,
                    PresentationUnresolvedInput = probability.UnresolvedInput
                };
            }
            catch (InvalidDataException error)
            {
                options[ordinal] = options[ordinal] with { PresentationUnresolvedInput = error.Message };
            }
        }

        private bool FireIncluded()
        {
            var targetKind = Kind();
            bool reaches = targetKind switch
            {
                UnitTargetMenuKind.Empty or UnitTargetMenuKind.Army => Relation() != UnitTargetMenuRelation.Friendly,
                UnitTargetMenuKind.Base or UnitTargetMenuKind.Facility => Relation() == UnitTargetMenuRelation.Other,
                _ => true
            };
            if (!reaches) return false;
            int code = Need(current.ReadHexSurfaceCode(hex), "fire_surface");
            var terrain = current.ReadSurfaceTypeTableReference(code <= 30 ? code : 0)
                ?? throw Missing("fire_surface_pointer");
            int physical = Need(terrain.PhysicalSlot, "fire_surface_receiver");
            byte flags = Need(PersonArmyCaptureInputs.ReadTerrainFlags2C(state, physical), "fire_surface_flags");
            if ((flags & 1) == 0 && !((flags & 2) != 0 && targetKind == UnitTargetMenuKind.Army && Relation() == UnitTargetMenuRelation.Other)) return false;
            if (Need(current.ReadCaptureHexWord16(Need(hex.PhysicalSlot, "fire_target_receiver")), "fire_current_hex") != 0) return false;
            if (SameSite())
            {
                var land = HexForce();
                if (land != own && Need(ArmyMarchDevelopmentSource.ReadFriendlyRelation(state, own, land), "fire_land_relation")) return false;
            }
            return true;
        }

        private bool AreaIncluded() => AreaIncluded(TargetArea());

        private bool AreaIncluded(AreaReference area)
        {
            if (!Need(current.ReadAreaNativeValidity(area), "area_validity")) return false;
            int ownForce = Need(own.PhysicalSlot, "area_selected_force");
            int areaForce = Need(current.ReadAreaCurrentForceSlot(area), "area_current_force");
            int representative = Need(current.ReadAreaHexIndex(area), "area_representative");
            if (areaForce != ownForce && areaForce != 0)
            {
                int cityRaw = Need(current.ReadAreaCitySlot(area), "area_city_link");
                var city = current.ReadCityTableReference(cityRaw <= 51 ? cityRaw : 0) ?? throw Missing("area_city_pointer");
                if (Need(current.ReadBaseNativeValidity(city), "area_city_validity"))
                {
                    int link = Need(current.ReadBaseObjectLink(city), "area_city_object_link");
                    var cityObject = current.ReadObjectTableReference(link <= 3000 ? link : 0) ?? throw Missing("area_city_object_pointer");
                    int cityHex = Need(current.ReadObjectNativeValidity(cityObject), "area_city_object_validity")
                        ? Need(current.ReadObjectHexIndex(cityObject), "area_city_object_hex") : 48400;
                    if (cityHex == representative) return false;
                }
                if (Need(current.ReadHexSurfaceCode(Hex(ClampHex(representative))), "area_representative_surface") == 17 ||
                    !Need(ArmyLogisticsPreparation.ReadNativeRelationGate(state, ownForce, areaForce), "area_owner_relation")) return false;
            }
            int physical = Need(area.PhysicalSlot, "area_identity");
            bool? naval = null;
            // This is the original Area-target existence test over its Hex
            // members. It runs on accepted clicks, never each frame or hover.
            for (int h = AreaHexIterator.FindNextIndex(0, physical, current); h < 48400;
                h = AreaHexIterator.FindNextIndex(h + 1, physical, current))
            {
                var candidate = Hex(h);
                int code = Need(current.ReadHexSurfaceCode(candidate), "area_hex_surface");
                if (code == 24)
                {
                    naval ??= AllSelectedNavalAllows24();
                    if (!naval.Value) continue;
                }
                var terrain = current.ReadSurfaceTypeTableReference(code <= 30 ? code : 0) ?? throw Missing("area_surface_pointer");
                if (terrain.PhysicalSlot is null || Need(current.ReadSurfaceTypeRawField2A(terrain), "area_surface_cost") == 0) continue;
                var obstacle = Object(candidate);
                int at = Need(obstacle.PhysicalSlot, "area_hex_object");
                int faction = Need(current.ReadHexForceSlot(candidate), "area_hex_force");
                if (at is >= 52 and <= 61 && (Need(current.ReadObjectHexIndex(obstacle), "area_gate_hex") != h ||
                    Need(ArmyLogisticsPreparation.ReadNativeRelationGate(state, ownForce, faction), "area_gate_relation"))) continue;
                if (Need(ArmyLogisticsPreparation.ReadNativeRelationGate(state, ownForce, faction), "area_hex_relation")) return true;
            }
            return false;
        }

        private AreaReference TargetArea()
        {
            int raw = Need(current.ReadHexAreaSlot(hex), "area_link");
            return current.ReadAreaTableReference(raw <= 500 ? raw : 0) ?? throw Missing("area_pointer");
        }

        private int? PersonPosition(int officerId)
        {
            int person = Need(current.ReadPersonLookupReference(officerId <= 6000 ? officerId : 0)?.PhysicalSlot, "selected_person_pointer");
            if (!Need(current.ReadPersonNativeValidity(person), "selected_person_validity")) return null;
            var position = PersonPositionRepair.DecodeSecondLocation(Need(current.ReadPersonSecondLocation(person), "selected_person_position"));
            if (position.Kind == 27)
            {
                var army = current.ReadArmyUnitTableReference(position.Index <= 500 ? position.Index : 0) ?? throw Missing("position_army_pointer");
                return Need(current.ReadArmyUnitNativeValidity(army), "position_army_validity")
                    ? Hex(ClampHex(Need(current.ReadArmyUnitRawHex2A(army), "position_army_hex"))).PhysicalSlot : null;
            }
            BaseReference? receiver = position.Kind switch
            {
                5 => current.ReadCityTableReference(position.Index <= 51 ? position.Index : 0),
                6 => current.ReadGateTableReference(position.Index <= 10 ? position.Index : 0),
                _ => new(BaseKind.City, null)
            };
            if (receiver is null) throw Missing("position_base_pointer");
            return Need(current.ReadBaseNativeValidity(receiver), "position_base_validity")
                ? CurrentGateMapQueries.FindHex(receiver, current).PhysicalSlot : null;
        }
        private bool CaptureIncluded()
        {
            if (Kind() != UnitTargetMenuKind.Army || Relation() != UnitTargetMenuRelation.Other) return false;
            return FalseReportTargetAllowed(state, current, own ?? throw Missing("selected_force_pointer"),
                targetArmy ?? throw Missing("false_report_target_pointer"));
        }

        private void Add(string key, int caption, int tooltip, Func<bool> include, Func<DeploymentAction>? action,
            Func<bool>? enabled = null, bool ordinal = false, string? presentationMissing = null)
        {
            bool? present = null, active = null; DeploymentAction? command = null; string? missing = null; int? number = null;
            try { present = include(); } catch (InvalidDataException error) { missing = error.Message; }
            if (present == false) return;
            if (present == true)
            {
                try { active = enabled?.Invoke() ?? true; if (ordinal) number = ActiveIndex() + 1; if (action is not null) command = action(); }
                catch (InvalidDataException error) { active = null; missing = error.Message; }
            }
            options.Add(new(key, caption, tooltip, present, active, command, missing)
                { WaypointOrdinal = number, PresentationUnresolvedInput = presentationMissing });
        }

        private DeploymentTarget BaseTarget()
        {
            int physical = Need(targetBase?.PhysicalSlot, "command_base_identity");
            if (targetBase!.Kind == BaseKind.City && state.Cities.ContainsKey(physical)) return new(DeploymentTargetKind.Location, physical);
            int location = physical + 51;
            if (targetBase.Kind == BaseKind.Gate && state.Gates.ContainsKey(location)) return new(DeploymentTargetKind.Location, location);
            throw Missing("command_base_owner");
        }
        private DeploymentTarget StructureTarget()
        {
            var directory = current.CurrentMapObjectDirectory ?? throw Missing("command_object_directory");
            var identity = directory.ReceiverAtPhysical(Need(obj.PhysicalSlot, "command_object_identity"));
            var row = state.MapObjects?.Receivers.FirstOrDefault(value => value.Id == identity);
            if (row?.Subject?.StructureId is int structure && state.Structures?.ContainsKey(structure) == true)
                return new(DeploymentTargetKind.Structure, structure);
            throw Missing("command_structure_owner");
        }
        private DeploymentTarget AttackTarget()
        {
            if (Kind() == UnitTargetMenuKind.Base) return BaseTarget();
            if (Kind() != UnitTargetMenuKind.Army) return StructureTarget();
            int physical = Need(targetArmy?.PhysicalSlot, "command_target_army_identity");
            var arena = state.ReadArmyLifecycle() ?? throw Missing("command_target_army_lifecycle");
            if ((uint)physical >= (uint)arena.Slots.Count) throw Missing("command_target_army_range");
            var owner = arena.Receivers.Find(arena.Slots[physical].ReceiverId) ?? throw Missing("command_target_army_owner");
            if (owner.ActiveUnitId is int id && state.Units.ContainsKey(id)) return new(DeploymentTargetKind.Unit, id);
            throw Missing("command_target_active_unit");
        }
        private ArmyUnitReference OriginalTargetArmy()
        {
            int raw = unchecked((short)Need(current.ReadHexRaw1C(hex), "target_signed_army"));
            //7CDC40/7C70D0 really look up row0 and test its receiver. An
            //aliased row0 can be valid;2C6D20's separate1..500 presence
            //filter must not be substituted into this branch.
            if (raw < 0) throw Missing("target_negative_Army_lookup");
            return current.ReadArmyUnitTableReference(raw <= 500 ? raw : 0) ?? throw Missing("target_army_pointer");
        }
        private ForceReference ObjectForce(ObjectReference value) => Force(Need(current.ReadObjectForceSlot13(value), "target_object_force"));
        private ForceReference HexForce() => Force(Need(current.ReadHexForceSlot(hex), "target_hex_force"));
        private ForceReference Force(int raw) => current.ReadForceTableReference(raw <= 51 ? raw : 0) ?? throw Missing("force_pointer");
        private HexReference Hex(int raw) => current.ReadHexTableReference(raw) ?? throw Missing("hex_pointer");
        private ObjectReference Object(HexReference at) => FacilityConstructionLegality.ObjectAt(current, at);
        private ObjectDefinitionReference Definition(ObjectReference value) => FacilityConstructionLegality.DefinitionOf(current, value);
        private UnitTargetMenuKind Kind() => kind ?? throw new InvalidDataException(classMissing ?? "unit.target_menu.classification");
        private UnitTargetMenuRelation Relation() => relation ?? throw new InvalidDataException(relationMissing ?? "unit.target_menu.relation");
        private bool SameSite() => sameSite ?? throw new InvalidDataException(sameMissing ?? "unit.target_menu.same_site");
        private int ActiveIndex() => route.FirstActiveWaypointIndex is >= 0 and <= 5 && route.HasActiveRouteGroup
            ? route.FirstActiveWaypointIndex.Value : throw Missing("active_group_index");
        private UnitTargetMenu Result(bool? canOpen)
        {
            if (canOpen == true)
                canOpen = options.Any(option => option.Included == true) ? true : options.Count == 0 ? false : null;
            return new(Unit.Id, clicked, targetHex, canOpen, kind, relation, options.AsReadOnly(), classMissing ?? relationMissing);
        }
        private static int ClampHex(int raw) => raw <= 48399 ? raw : 0;
        private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("unit.target_menu." + field);
    }
}
