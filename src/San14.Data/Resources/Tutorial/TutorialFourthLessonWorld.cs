using San14.Core;

namespace San14.Data;

/// <summary>Installed source target filters; these inspect the small tutorial controller.</summary>
internal static class TutorialFourthLessonTargets
{
    internal static bool AllowsHex(GameState control, SourceWorldContext current, int hexId)
    {
        var p = control.TutorialLessonProgress ?? throw new InvalidDataException("tutorial.fourth_lesson.progress");
        if (p.ActiveParagraphIndex == 9 && p.ParagraphPhases[9] is >= 4 and <= 5)
            return hexId == TutorialFourthLessonPlan.ConstructionHexId;
        if (p.ActiveParagraphIndex == 12 && p.ParagraphPhases[12] == 2)
        {
            var city = current.ReadCityTableReference(5) ?? throw new InvalidDataException("tutorial.fourth_lesson.target_city");
            return hexId == current.ReadBaseCurrentHexIndex(city);
        }
        return true;
    }

    internal static bool AllowsArmy(GameState control, int physical, byte? action)
    {
        var p = control.TutorialLessonProgress ?? throw new InvalidDataException("tutorial.fourth_lesson.progress");
        if (p.ActiveParagraphIndex == 12 && p.ParagraphPhases[12] == 2)
            return physical == p.RestrictedArmyPhysicalSlot;
        if (p.ActiveParagraphIndex == 12 && p.ParagraphPhases[12] is 3 or 4 || p.Stage == 61)
            return physical != p.RestrictedArmyPhysicalSlot;
        return true;
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    private GameState? fourthObservationOwner;
    private int fourthObservationStage = -1;
    private int? fourthObservationArmy;
    private FourthTutorialWorldFacts? fourthObservationFacts;
    private sealed record FourthTutorialWorldFacts(bool ConstructionCompleted = false,
        bool CaptureQueued = false, bool TrackedValid = false, bool TrackedAttacks = false,
        bool CityOnFire = false, bool CityOwned = false, bool CityReady = false, bool CityEmpty = false);
    internal GameState ApplyFourthTutorialParagraphWorldCurrent(GameState state, int index, int phase)
    {
        RequireSelectedTutorialControl(state);
        if (state.TutorialLessonProgress is not { CourseIndex: 3 } p || (uint)index >= 16)
            throw FourthTutorialMissing("paragraph_owner");
        if (index == 0 && phase == 0) state = InitializeFourthTutorialOpeningCurrent(state);
        if (index == 0 && phase == 1) state = WriteFirstTutorialControl(state, 70, true);
        if (index is 1 or 9 && phase == 0 || index == 7 && phase == 1)
            state = WriteFirstTutorialControl(state, 60, false);
        if (index == 9 && phase == 0) state = SetFirstTutorialCommandMask(state, 1);
        if (index == 4 && phase == 1) state = WriteFirstTutorialControl(state, 75, true);
        if (index == 7 && phase == 6) state = WriteFirstTutorialControl(state, 60, true);
        if (index == 6 && phase == 0 || index == 9 && phase == 7 || index == 12 && phase == 3)
            state = WriteFirstTutorialControl(state, 4, false);
        if (index == 11 && phase == 0)
        {
            var current = CaptureCurrentDefinitions(state) ?? throw FourthTutorialMissing("attack_current");
            var city = current.ReadCityTableReference(7) ?? throw FourthTutorialMissing("attack_City7");
            var area = current.ReadAreaTableReference(51) ?? throw FourthTutorialMissing("attack_Area51");
            int hex = FourthNeed(current.ReadAreaHexIndex(area), "attack_Area51_Hex");
            int Identity(int slot)
            {
                int physical = FourthNeed((current.ReadTransferPersonTableReference(slot) ?? throw FourthTutorialMissing("attack_Person_pointer")).PhysicalSlot, "attack_Person_physical");
                return FourthNeed(current.ReadPersonIdentity(physical), "attack_Person_identity");
            }
            state = CreateTutorialArmyCurrent(state, Identity(149), 3000, hex, 0, cityTarget: city);
            state = CreateTutorialArmyCurrent(state, Identity(90), 2000, hex, 3, cityTarget: city);
            state = CreateTutorialArmyCurrent(state, Identity(237), 1500, hex, 4, cityTarget: city);
            state = SetFirstTutorialCommandMask(state, 31);
            state = state with { TutorialLessonProgress = state.TutorialLessonProgress! with
                { FourthAttackReinforcementCount = 0, FourthAttackRefreshRequested = true, RestrictedArmyPhysicalSlot = null } };
        }
        if (index == 9 && phase == -1)
        {
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            int id = state.TutorialLessonProgress!.FourthConstructionCommanderId ?? throw FourthTutorialMissing("construction_Commander_capture");
            int physical = FourthNeed((current.ReadPersonLookupReference(id) ?? throw FourthTutorialMissing("construction_Person_pointer")).PhysicalSlot, "construction_Person");
            int army = FourthNeed(current.ReadPersonNativeValidity(physical), "construction_Person_validity")
                ? (PersonArmyLocationSource.ReadCommanderArmy(current, physical) ?? throw FourthTutorialMissing("construction_Army_pointer")).PhysicalSlot ?? 0 : 0;
            state = state with { TutorialLessonProgress = state.TutorialLessonProgress! with { RestrictedArmyPhysicalSlot = army } };
        }
        if (index == 11 && phase == -1 || index == 12 && phase == 2)
            state = CaptureFourthTutorialAttackArmyCurrent(state);
        if (index == 12 && phase == 1) state = WriteFirstTutorialControl(state, 69, true);
        if (index == 12 && phase == 2) state = SetFirstTutorialCommandMask(state, 0);
        if (index == 12 && phase == 3) state = SetFirstTutorialCommandMask(state, 31);
        if (index == 12 && phase == -1)
        {
            state = WriteFirstTutorialControl(state, 69, false);
            state = WriteFirstTutorialControl(state, 70, false);
        }
        if (index == 14 && phase == -1)
            state = state with { TutorialLessonProgress = state.TutorialLessonProgress! with { CompletionCourseIndex = 3 } };
        return state;
    }

    private GameState InitializeFourthTutorialOpeningCurrent(GameState state)
    {
        foreach (int flag in new[] { 78, 79, 81, 62, 58 }) state = WriteFirstTutorialControl(state, flag, true);
        state = SetFirstTutorialCommandMask(state, 0);
        var current = CaptureOwnedArmyReturnWorld(state).Context;
        var city5 = current.ReadCityTableReference(5) ?? throw FourthTutorialMissing("opening_City5");
        int force = FourthNeed(current.ReadBaseForceSlot(city5), "opening_Force");
        var changed = new HashSet<int>();
        var officers = new Dictionary<int, OfficerState>(state.Officers);
        var hexes = new List<int>();
        foreach (int id in new[] { 29, 30, 31, 32, 33 })
        {
            var area = current.ReadAreaTableReference(id) ?? throw FourthTutorialMissing("opening_Area");
            if (!FourthNeed(current.ReadAreaNativeValidity(area), "opening_Area_validity")) continue;
            var assigned = current.AssignAreaCity(area, 5);
            changed.Add(id);
            foreach (int physical in assigned.PersonPhysicalSlotsWritten)
            {
                int person = FourthNeed(current.ReadPersonIdentity(physical), "opening_assignment_Person");
                var before = officers.GetValueOrDefault(person) ?? throw FourthTutorialMissing("opening_assignment_owner");
                officers[person] = before with { LocationId = FourthNeed(current.ReadPersonFirstLocation(physical), "opening_assignment_Home") };
            }
            for (int h = AreaHexIterator.FindNextIndex(0, id, current); h < 48400;
                 h = AreaHexIterator.FindNextIndex(h + 1, id, current)) hexes.Add(h);
        }
        state = PersonReturnDistrictTransferSource.PublishAreaAssignments(state with { Officers = officers }, current, changed, []);
        state = ApplyTutorialTerritoryCurrent(state, hexes, force, 5);
        current = CaptureCurrentDefinitions(state) ?? throw FourthTutorialMissing("opening_resource_current");
        state = SetCityResources(5, null, null, 20000);
        state = SetCityResources(7, 3000, 10000, 10000);
        //The same two source people are joined by29E080 and then explicitly
        //positioned atCity7 in both the second and fourth opening.
        return ApplySecondTutorialReinforcementsCurrent(state);

        GameState SetCityResources(int id, int? gold, int? food, int troops)
        {
            var city = current.ReadCityTableReference(id) ?? throw FourthTutorialMissing("opening_resource_City");
            var owner = state.Cities.GetValueOrDefault(id) ?? throw FourthTutorialMissing("opening_resource_owner");
            int ready = Math.Clamp(troops, 0, BasePostLoad.ReadTroopCapacity(city, current));
            current.SetBaseResource(city, BaseResourceKind.Troops, ready);
            owner = owner with { Troops = ready };
            if (gold is int g)
            {
                g = Math.Clamp(g, 0, BasePostLoad.ReadGoldCapacity(city, current));
                current.SetBaseResource(city, BaseResourceKind.Gold, g); owner = owner with { Gold = g };
            }
            if (food is int f)
            {
                f = Math.Clamp(f, 0, BasePostLoad.ReadSuppliesCapacity(city, current));
                current.SetBaseResource(city, BaseResourceKind.Supplies, f); owner = owner with { Supplies = f };
            }
            return state with { Cities = new(state.Cities) { [id] = owner } };
        }
    }

    /// <summary>4A9FA0 refreshes only after returning to action1. Ordinary
    /// commands and the normal lifecycle return these same officers toCity5.</summary>
    internal GameState AdvanceFourthTutorialAttackCurrent(GameState state, int? actionStackTop)
    {
        if (state.TutorialLessonProgress is not { CourseIndex: 3, ActiveParagraphIndex: 11 } p) return state;
        if (actionStackTop != 1)
            return p.FourthAttackRefreshRequested ? state : state with
                { TutorialLessonProgress = p with { FourthAttackRefreshRequested = true } };
        if (p.ParagraphPhases[11] != 1 || !p.FourthAttackRefreshRequested || FourthCaptureCandidates(state).Any()) return state;
        var current = CaptureOwnedArmyReturnWorld(state).Context;
        int count = p.FourthAttackReinforcementCount;
        var candidates = FourthTutorialHomeArmyImages(state, current, 5);
        //5709A0/510870: player Force Armies in the five named Areas;
        //473010 sorts troop count descending, then commander identity ascending.
        int player = FourthNeed(OwnedCurrentPlayerForce.ReadRawForce3A(state), "wave_Player");
        var targets = FourthTutorialRegisteredArmyImages(state, current).Where(unit => unit.ForceId == player).Where(unit =>
        {
            var hex = current.ReadHexTableReference(unit.HexId) ?? throw FourthTutorialMissing("wave_target_Hex");
            return FourthNeed(current.ReadHexAreaSlot(hex), "wave_target_Area") is 29 or 30 or 31 or 32 or 33;
        });
        var target = FourthArmyOrder(targets).FirstOrDefault();
        if (target is not null && count < 5)
        {
            int available = state.Cities.GetValueOrDefault(5)?.Troops ?? throw FourthTutorialMissing("wave_City5");
            foreach (var (physical, ready) in new[] { (90, 2000), (149, 3000), (237, 1500) })
            {
                current = CaptureCurrentDefinitions(state) ?? throw FourthTutorialMissing("wave_current");
                int person = FourthNeed((current.ReadTransferPersonTableReference(physical) ?? throw FourthTutorialMissing("wave_Person_pointer")).PhysicalSlot, "wave_Person");
                if (!FourthNeed(current.ReadPersonNativeValidity(person), "wave_Person_validity")) continue;
                int identity = FourthNeed(current.ReadPersonIdentity(person), "wave_Person_identity");
                var location = current.ReadOfficerLocation(identity);
                if (location?.Position is not { Kind: OfficerPositionKind.City, EntityId: 5 } || ready > available) continue;
                state = CreateTutorialArmyCurrent(state, identity, ready, 48400, 0, armyTargetId: target.Id);
                if (++count >= 5) break;
            }
        }
        current = CaptureOwnedArmyReturnWorld(state).Context;
        foreach (var image in candidates)
        {
            if (!state.Units.TryGetValue(image.Id, out var unit) || ArmyConfigurationSourceAdapter.ReadAction(state, unit) != 13) continue;
            int physical = FourthNeed(unit.NativeArmyOrigin?.NativeSlotIndex, "wave_hold_Army");
            if (FourthNeed(ArmySavedCommandKnowledge.ReadControl(state, unit), "wave_hold_control") != 0) continue;
            var receiver = state.ReadArmyLifecycle()!.Receivers.FindUnit(unit.Id) ?? throw FourthTutorialMissing("wave_hold_receiver");
            state = OwnedArmyLifecycleTargets.Write(state, receiver.ReceiverId, new(5, 7));
            current.SetArmyLifecycleTargetFields(physical, new(5, 7));
            state = state with { Units = new(state.Units) { [unit.Id] = unit with
                { FieldAttackPolicy = ArmyConfigurationSourceAdapter.DecodePolicy(4) } },
                ResourceState = ArmyConfigurationSourceAdapter.WriteAction(state.ResourceState, unit.Id, 4) };
        }
        return state with { TutorialLessonProgress = state.TutorialLessonProgress! with
            { FourthAttackReinforcementCount = count, FourthAttackRefreshRequested = false } };
    }

    private static IEnumerable<UnitState> FourthArmyOrder(IEnumerable<UnitState> units) =>
        units.OrderByDescending(unit => unit.Troops).ThenBy(unit => unit.OfficerId);

    private static IEnumerable<UnitState> FourthCaptureCandidates(GameState state)
    {
        int player = FourthNeed(OwnedCurrentPlayerForce.ReadRawForce3A(state), "capture_Player");
        var requests = state.PendingCombatSettlements?.Requests ?? throw FourthTutorialMissing("capture_queue");
        return FourthArmyOrder(requests.OfType<BaseCaptureRequest>()
            .Where(request => request.Target is { Kind: SettlementBaseKind.City, Id: 5 } && request.WinnerUnitId is not null)
            .Select(request => state.Units.GetValueOrDefault(request.WinnerUnitId!.Value))
            .Where(unit => unit is not null && unit.ForceId == player).Cast<UnitState>());
    }

    private GameState CaptureFourthTutorialAttackArmyCurrent(GameState state)
    {
        var p = state.TutorialLessonProgress ?? throw FourthTutorialMissing("capture_progress");
        if (p.RestrictedArmyPhysicalSlot is int old && state.Units.Values.Any(unit => unit.NativeArmyOrigin?.NativeSlotIndex == old)) return state;
        var selected = FourthCaptureCandidates(state).FirstOrDefault();
        return state with { TutorialLessonProgress = p with { RestrictedArmyPhysicalSlot = selected?.NativeArmyOrigin?.NativeSlotIndex ?? 0 } };
    }

    private static IReadOnlyList<UnitState> FourthTutorialHomeArmyImages(GameState state, SourceWorldContext current, int location) =>
        FourthTutorialRegisteredArmyImages(state, current).Where(unit =>
        {
            var army = new ArmyUnitReference(FourthNeed(unit.NativeArmyOrigin?.NativeSlotIndex, "HomeArmy_physical"));
            var home = current.FindArmyUnitCurrentBase(army);
            return FourthNeed(current.ReadBaseObjectLink(home), "HomeArmy_Base") == location;
        }).ToArray();

    private static IEnumerable<UnitState> FourthTutorialRegisteredArmyImages(GameState state, SourceWorldContext current)
    {
        var entries = current.ReadRegistryReferences(RegistryKind.Army) ?? throw FourthTutorialMissing("Army_registry");
        foreach (var entry in entries)
        {
            if (entry is not { Kind: RegistryKind.Army, PhysicalSlot: int physical }) throw FourthTutorialMissing("Army_registry_pointer");
            if (FourthNeed(current.ReadOwnedArmyExclusion233560(entry), "Army_excluded")) continue;
            if (state.Units.Values.FirstOrDefault(unit => unit.NativeArmyOrigin?.NativeSlotIndex == physical) is { } unit) yield return unit;
        }
    }

    internal TutorialFourthLessonObservation ReadFourthTutorialObservationCurrent(GameState state, TutorialFourthLessonObservation observed)
    {
        var p = state.TutorialLessonProgress;
        if (p is not { CourseIndex: 3 }) throw FourthTutorialMissing("observation_owner");
        if (p.Stage < 61) return observed;
        bool same = fourthObservationOwner is { } previous && fourthObservationStage == p.Stage &&
            fourthObservationArmy == p.RestrictedArmyPhysicalSlot &&
            ReferenceEquals(previous.NativeArmySimulation?.World, state.NativeArmySimulation?.World) &&
            ReferenceEquals(previous.ArmyLifecycle, state.ArmyLifecycle) &&
            ReferenceEquals(previous.Units, state.Units) && ReferenceEquals(previous.Officers, state.Officers) &&
            ReferenceEquals(previous.Cities, state.Cities) && ReferenceEquals(previous.MapObjects, state.MapObjects) &&
            ReferenceEquals(previous.Forces, state.Forces) && ReferenceEquals(previous.Districts, state.Districts) &&
            ReferenceEquals(previous.Areas, state.Areas) && ReferenceEquals(previous.RegionStatistics, state.RegionStatistics) &&
            ReferenceEquals(previous.ResourceState, state.ResourceState) &&
            ReferenceEquals(previous.NativeBaseCaptureCurrent, state.NativeBaseCaptureCurrent) &&
            ReferenceEquals(previous.PendingCombatSettlements, state.PendingCombatSettlements);
        if (!same)
        {
            fourthObservationFacts = ReadFacts();
            fourthObservationOwner = state; fourthObservationStage = p.Stage;
            fourthObservationArmy = p.RestrictedArmyPhysicalSlot;
        }
        var facts = fourthObservationFacts ?? throw FourthTutorialMissing("observation_facts");
        return observed with { ConstructionCompleted = facts.ConstructionCompleted,
            AttackCityOrderPresent = facts.CaptureQueued, TrackedAttackArmyValid = facts.TrackedValid,
            TrackedArmyAttacksTargetCity = facts.TrackedAttacks, TargetCityHexOnFire = facts.CityOnFire,
            TargetCityPlayerOwned = facts.CityOwned,
            NanpiExplanationReady = facts.CityOwned || facts.CityEmpty ? observed.Common.ActionStackTop == 1 : facts.CityReady };

        FourthTutorialWorldFacts ReadFacts()
        {
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            if (p.Stage == 61)
            {
                var hex = current.ReadHexTableReference(TutorialFourthLessonPlan.ConstructionHexId) ?? throw FourthTutorialMissing("build_Hex");
                int raw = FourthNeed(current.ReadHexObjectLink(hex), "build_Object_link");
                var obj = current.ReadObjectTableReference(raw < 3001 ? raw : 0) ?? throw FourthTutorialMissing("build_Object");
                bool complete = FourthNeed(current.ReadObjectNativeValidity(obj), "build_Object_validity") &&
                    (FourthNeed(current.ReadMapObjectFields16(obj), "build_Object_activation") & 1) != 0;
                return new(ConstructionCompleted: complete);
            }
            bool capture = FourthCaptureCandidates(state).Any();
            if (p.Stage == 62) return new(CaptureQueued: capture);
            var city = current.ReadCityTableReference(5) ?? throw FourthTutorialMissing("target_City5");
            int player = FourthNeed(OwnedCurrentPlayerForce.ReadRawForce3A(state), "target_Player");
            bool owned = FourthNeed(current.ReadBaseForceSlot(city), "target_Force") == player;
            if (p.Stage == 64)
            {
                int wall = BasePostLoad.ReadLinkedField14(city, current);
                int maximum = BasePostLoad.ReadCapacities(city, current).Field46;
                int troops = FourthNeed(current.ReadBaseResource(city, BaseResourceKind.Troops), "target_Troops");
                bool lowWall = unchecked(80 * maximum) / 100 >= wall;
                return new(CityOwned: owned, CityReady: lowWall || troops <= 2000, CityEmpty: lowWall ? wall == 0 : troops == 0);
            }
            if (p.Stage != 63) return new(CityOwned: owned);
            var tracked = state.Units.Values.FirstOrDefault(unit => unit.NativeArmyOrigin?.NativeSlotIndex == p.RestrictedArmyPhysicalSlot);
            bool attacks = tracked is not null && ArmyConfigurationSourceAdapter.ReadAction(state, tracked) == 16 &&
                current.ReadArmyLifecycleTargetFields(FourthNeed(tracked.NativeArmyOrigin?.NativeSlotIndex, "tracked_Army")) is
                    { Kind38: 9, Identity3A: var goal } && goal == current.ReadBaseCurrentHexIndex(city);
            int cityHex = current.ReadBaseCurrentHexIndex(city);
            bool fire = FourthNeed(current.ReadCaptureHexWord16(cityHex), "target_Fire16") != 0;
            return new(CaptureQueued: capture, TrackedValid: tracked is not null, TrackedAttacks: attacks, CityOnFire: fire, CityOwned: owned);
        }
    }

    public int? ReadFourthTutorialFacilityKind(GameState state, int physicalDefinitionId)
    {
        if ((uint)physicalDefinitionId >= 31) throw new ArgumentOutOfRangeException(nameof(physicalDefinitionId));
        ArgumentNullException.ThrowIfNull(state);
        //The presenter supplies the actual definition's physical identity.
        //22DDA0 returns that ordinal itself, independently from category2C;
        //there is no world-dependent calculation or capture on this UI read.
        return physicalDefinitionId;
    }
    private static T FourthNeed<T>(T? value, string field) where T : struct => value ?? throw FourthTutorialMissing(field);
    private static InvalidDataException FourthTutorialMissing(string field) => new("tutorial.fourth_lesson.world." + field);
}
