using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>One actual mode0 target-page owner. No Army allocation,
    /// command submission, path search, or per-hover World capture.</summary>
    public DeploymentTargetMenuQuery CreateDeploymentTargetMenuQuery(GameState state, int targetForceId)
    {
        ArgumentNullException.ThrowIfNull(state);
        SourceWorldContext? captured = null; string? captureMissing = null;
        try
        {
            var current = CaptureWritableTransportRouteCurrent(state);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
            captured = current;
        }
        catch (InvalidDataException error) { captured = null; captureMissing = error.Message; }
        return new(Pointer, Menu);

        bool Valid(IReadOnlyList<DeploymentDraftRow> rows, int clicked) =>
            targetForceId is >= 0 and <= 51 && clicked is >= 0 and < 48400 &&
            rows.Count is >= 1 and <= 30 && rows.All(row => row.Request.OfficerId >= 0) &&
            rows.Select(row => row.Request.OfficerId).Distinct().Count() == rows.Count;
        DeploymentTargetPointer Pointer(IReadOnlyList<DeploymentDraftRow> rows, int clicked)
        {
            if (!Valid(rows, clicked)) return new(false, null);
            if (captured is null) return new(null, captureMissing);
            try { return new UnitTargetMenuReader(this, state, rows, clicked, targetForceId,
                new(false, null), captured).ReadDeploymentPointer(); }
            catch (InvalidDataException error) { return new(null, error.Message); }
        }
        DeploymentTargetMenu Menu(IReadOnlyList<DeploymentDraftRow> rows, int clicked, UnitTargetRouteContext route)
        {
            var ids = Array.AsReadOnly(rows.Select(row => row.Request.OfficerId).ToArray());
            if (!Valid(rows, clicked)) return new(ids, clicked, null, false, false, null, null, [], null);
            if (captured is null) return new(ids, clicked, null, null, null, null, null, [], captureMissing);
            try { return new UnitTargetMenuReader(this, state, rows, clicked, targetForceId,
                route, captured).ReadDeploymentMenu(); }
            catch (InvalidDataException error) { return new(ids, clicked, null, null, null, null, null, [], error.Message); }
        }
    }

    private sealed partial class UnitTargetMenuReader
    {
        private readonly IReadOnlyList<int> selectedOfficerIds;
        private IReadOnlyList<int> SelectedOfficerIds() => selectedOfficerIds;

        public UnitTargetMenuReader(ArmyCampaignRuleAdapter package, GameState state,
            IReadOnlyList<DeploymentDraftRow> rows, int clicked, int targetForceId,
            UnitTargetRouteContext route, SourceWorldContext current)
            : this(package, state, rows.Select(row => row.Request.OfficerId).ToArray(),
                clicked, targetForceId, route, current) { }

        public UnitTargetMenuReader(ArmyCampaignRuleAdapter package, GameState state,
            IReadOnlyList<int> officerIds, int clicked, int targetForceId,
            UnitTargetRouteContext route, SourceWorldContext current)
        {
            this.package = package; this.state = state; this.clicked = clicked; this.route = route;
            this.current = current; targetHex = clicked;
            unit = null; selectedUnits = []; selectedSet = true; commandActorForceId = targetForceId;
            selectedOfficerIds = officerIds;
            // Domain Force IDs identify the current physical Force owner. They
            // are not raw ForceTable requests to reinterpret through an alias.
            own = new(targetForceId);
        }

        private bool PressedTerrainAllowed(HexReference pressed)
        {
            int code = Need(current.ReadHexSurfaceCode(pressed), "pressed_surface");
            var terrain = current.ReadSurfaceTypeTableReference(code <= 30 ? code : 0)
                ?? throw Missing("pressed_surface_pointer");
            if (terrain.PhysicalSlot is not int physical ||
                Need(current.ReadSurfaceTypeRawField2A(terrain), "pressed_surface_cost") == 0) return false;
            return physical != 24 || AllSelectedNavalAllows24();
        }

        internal DeploymentTargetPointer ReadDeploymentPointer()
        {
            var pressed = new HexReference(clicked);
            return new(PressedTerrainAllowed(pressed) && PointerAfterTerrain(pressed), null);
        }

        private bool PointerAfterTerrain(HexReference pressed)
        {
            // 7E3890 permits every non-FF native category immediately. Its
            // FF-only2C6AD0 call has occupancy/naval/ignoreObject arguments0.
            if (NativeKind(pressed) != 255) return true;
            int physical = Need(pressed.PhysicalSlot, "pointer_Hex_identity");
            var at = Hex(physical); //The actual second HexTable lookup remains.
            int code = Need(current.ReadHexSurfaceCode(at), "pointer_surface");
            var terrain = current.ReadSurfaceTypeTableReference(code <= 30 ? code : 0)
                ?? throw Missing("pointer_surface_pointer");
            if (terrain.PhysicalSlot is null || Need(current.ReadSurfaceTypeRawField2A(terrain), "pointer_surface_cost") == 0)
                return false;
            var obstacle = Object(at);
            if (Need(obstacle.PhysicalSlot, "pointer_Object_identity") is >= 52 and <= 61)
                return Need(current.ReadObjectHexIndex(obstacle), "pointer_Gate10") == physical &&
                    !Need(ArmyLogisticsPreparation.ReadNativeRelationGate(state,
                        Need(own.PhysicalSlot, "pointer_Force_identity"),
                        Need(current.ReadHexForceSlot(at), "pointer_Hex14")), "pointer_Gate_relation");
            var definition = Definition(obstacle);
            return !Need(current.ReadObjectDefinitionValidity(definition), "pointer_definition_validity") ||
                Need(current.ReadObjectDefinitionRaw2C(definition), "pointer_definition_kind") == 4;
        }

        internal DeploymentTargetMenu ReadDeploymentMenu()
        {
            var pressed = new HexReference(clicked);
            if (!PressedTerrainAllowed(pressed)) return DeploymentResult(false, false);
            bool? pointer = null; string? pointerMissing = null;
            try { pointer = PointerAfterTerrain(pressed); }
            catch (InvalidDataException error) { pointerMissing = error.Message; }
            if (!PrepareMapTarget(pressed)) return DeploymentResult(pointer, false, pointerMissing);

            // Same-site/index0 still suppresses Move and Waypoint. Item8 is
            // appended only in mode3, so normal new deployment has no Stay.
            Add("waypoint", 0x395, 0x569F, () => PassableWaypoint() && !SameSite(), null,
                enabled: () => ActiveIndex() < 5, ordinal: true);
            Add("move", 0x261, 0x56A1,
                () => (Kind() is UnitTargetMenuKind.Empty or UnitTargetMenuKind.Army or UnitTargetMenuKind.Trap) && !SameSite(),
                () => InitialAction("move", new(DeploymentTargetKind.Hex, targetHex)));
            Add("enter", 0x265, 0x56A2, EnterInitialIncluded, () => InitialAction("enter", BaseTarget()));
            Add("attack", 0x27D, 0x56A3,
                () => (Kind() is UnitTargetMenuKind.Army or UnitTargetMenuKind.Base or UnitTargetMenuKind.Facility or UnitTargetMenuKind.Obstacle) &&
                    Relation() == UnitTargetMenuRelation.Other,
                () => InitialAction("attack", AttackTarget()));
            AddInitialFire();
            Add("build", 0x10B, 0x56A4, () =>
            {
                if (!(Kind() is UnitTargetMenuKind.Empty or UnitTargetMenuKind.Army ||
                    Kind() == UnitTargetMenuKind.Trap && Relation() == UnitTargetMenuRelation.Other) ||
                    Relation() == UnitTargetMenuRelation.Friendly || SameSite()) return false;
                throw Missing("mode0_build_definition_search");
            }, null);
            Add("repair", 0x10C, 0x56A5, () => Kind() == UnitTargetMenuKind.Obstacle ||
                (Kind() is UnitTargetMenuKind.Facility or UnitTargetMenuKind.Trap) && Relation() == UnitTargetMenuRelation.Own,
                () => InitialAction("repair", StructureTarget()));
            Add("demolish", 0x10D, 0x56A6, () => (Kind() is UnitTargetMenuKind.Facility or UnitTargetMenuKind.Trap) &&
                Relation() == UnitTargetMenuRelation.Own, () => InitialAction("demolish", StructureTarget()));
            AddInitialOccupation();
            AddInitialFalseReport();
            return DeploymentResult(pointer, true, pointerMissing);
        }

        private bool EnterInitialIncluded()
        {
            if (Kind() != UnitTargetMenuKind.Base || Relation() != UnitTargetMenuRelation.Own) return false;
            if (route.HasActiveRouteGroup && ActiveIndex() > 0) return true;
            // 7CDD60 resolves only a valid Base from Person's second location.
            // Its null result remains distinct from a real selected Base.
            foreach (int id in selectedOfficerIds)
                if (DraftCurrentBaseHex(id) != hex.PhysicalSlot) return true;
            return false;
        }

        private int? DraftCurrentBaseHex(int id)
        {
            var personReference = current.ReadPersonLookupReference(id <= 6000 ? id : 0)
                ?? throw Missing("entry_Person_pointer");
            int? person = personReference.PhysicalSlot;
            if (person is null || !Need(current.ReadPersonNativeValidity(person.Value), "entry_Person_validity")) return null;
            var location = PersonPositionRepair.DecodeSecondLocation(Need(current.ReadPersonSecondLocation(person.Value), "entry_Person_E4"));
            BaseReference? receiver = location.Kind switch
            {
                5 => current.ReadCityTableReference(location.Index <= 51 ? location.Index : 0),
                6 => current.ReadGateTableReference(location.Index <= 10 ? location.Index : 0),
                _ => new(BaseKind.City, null)
            };
            if (receiver is null) throw Missing("entry_Base_pointer");
            return Need(current.ReadBaseNativeValidity(receiver), "entry_Base_validity")
                ? CurrentGateMapQueries.FindHex(receiver, current).PhysicalSlot : null;
        }

        private DeploymentAction InitialAction(string key, DeploymentTarget target)
            => InitialAction(key, target, out _);

        private DeploymentAction InitialAction(string key, DeploymentTarget target, out int? goal)
        {
            var action = new DeploymentAction(key, target) { ClickedHexId = key == "fire" ? clicked : null };
            int firstPerson = -1;
            // Only commander Force differs across otherwise common packets.
            // Read the shared target once after these actual selected Persons.
            foreach (int id in selectedOfficerIds)
            {
                int person = Need(current.ReadPersonLookupReference(id <= 6000 ? id : 0)?.PhysicalSlot, "initial_Person_pointer");
                if (!Need(current.ReadPersonNativeValidity(person), "initial_Person_validity") ||
                    current.ReadPersonForce(person) != own) throw Missing("initial_selected_Person_Force");
                if (firstPerson < 0) firstPerson = person;
            }
            if (key == "attack" && Kind() is not (UnitTargetMenuKind.Army or UnitTargetMenuKind.Base))
                throw Missing("mode0_structure_attack_command");
            var error = OCReadDeploymentTarget(state, current, firstPerson, [action], out var command);
            if (error is not null) throw Missing(error.MissingRuleKey ?? error.ErrorCode ?? "initial_target_rejected");
            if (command is null || key != "occupy" && command.GoalHex != targetHex)
                throw Missing("initial_target_Hex_projection");
            goal = command.GoalHex;
            return action;
        }

        private DeploymentTargetMenu DeploymentResult(bool? pointer, bool? canOpen, string? missing = null)
        {
            if (canOpen == true) canOpen = options.Any(option => option.Included == true) ? true : options.Count == 0 ? false : null;
            return new(selectedOfficerIds, clicked, targetHex, pointer, canOpen, kind, relation,
                options.AsReadOnly(), classMissing ?? relationMissing ?? missing);
        }
    }
}
