using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

internal static class TutorialSeventhLessonTargets
{
    internal static bool AllowsHex(GameState control, SourceWorldContext current, int hexId, int? activeWaypointIndex = null) => true;
    internal static bool AllowsArmy(GameState control, int physical, byte? action) =>
        !(control.TutorialLessonProgress?.RestrictedArmyPhysicalSlots
            ?? throw new InvalidDataException("tutorial.seventh_lesson.targets.Army_filter")).Contains(physical);
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState ApplySeventhTutorialParagraphWorldCurrent(GameState state, int index, int phase)
    {
        RequireSelectedTutorialControl(state);
        if (state.TutorialLessonProgress is not { CourseIndex: 6 } || (uint)index >= 37)
            throw SeventhMissing("paragraph_owner");
        if (index == 0 && phase == 0) state = InitializeSeventhTutorialOpeningCurrent(state);
        if (index == 14 && phase == 1) state = ClearSeventhTutorialSpyPeopleCurrent(state, [147, 952]);
        if (index == 15 && phase == 0) state = EnsureSeventhTutorialIntelligenceCurrent(state, 10000);
        if (index == 22 && phase == 1) state = WriteFirstTutorialControl(state, 91, false);
        if (index == 22 && phase == -1 || index == 30 && phase == 1)
            state = ClearSeventhTutorialSpyPeopleCurrent(state, [952]);
        if (index == 23 && phase == 0) state = EnsureSeventhTutorialIntelligenceCurrent(state, 5000);
        if (index == 23 && phase == 1)
        {
            state = WriteFirstTutorialControl(state, 92, true);
            state = CreateSeventhTutorialEnemyCurrent(state);
        }
        if (index == 25 && phase == -1) state = WriteFirstTutorialControl(state, 92, false);
        if (index == 27 && phase == 1)
            state = WriteFirstTutorialControl(state, 97, true);
        if (index == 27 && phase == 2) state = WriteFirstTutorialControl(state, 97, false);
        if (index == 31 && phase == 0) state = CreateSeventhTutorialAttackCurrent(state);
        if (index == 31 && phase == -1)
        {
            state = WriteFirstTutorialControl(state, 72, false);
            state = WriteFirstTutorialControl(state, 73, true);
        }
        if (index == 33 && phase == 1) state = ReturnSeventhTutorialAttackerCurrent(state);
        if (index == 33 && phase == -1) state = WriteFirstTutorialControl(state, 80, true);
        if (index == 34 && phase == -1) state = WriteFirstTutorialControl(state, 58, false);
        if (index == 35 && phase == -1)
            state = state with { TutorialLessonProgress = state.TutorialLessonProgress! with { CompletionCourseIndex = 6 } };
        return state;
    }

    private GameState InitializeSeventhTutorialOpeningCurrent(GameState state)
    {
        foreach (int flag in new[] { 78, 62, 61, 58, 72, 88, 90, 94 }) state = WriteFirstTutorialControl(state, flag, true);
        state = WriteFirstTutorialControl(state, 81, false);
        state = SetFirstTutorialCommandMask(state, 0);
        state = state with { TutorialLessonProgress = state.TutorialLessonProgress! with
            { RestrictedArmyPhysicalSlots = ImmutableArray<int>.Empty } };
        var operation = CaptureOwnedArmyReturnWorld(state);
        byte relation = (byte)Math.Clamp(unchecked(DParameter(state, "goodwill-relation-maximum") - 1), 0, 200);
        state = AbsorptionWriteRelation(state, operation.Context, 17, 2, false, relation);
        state = AbsorptionWriteRelation(state, operation.Context, 2, 17, false, relation);
        state = SynchronizeDiplomacyProjection(state);
        return state.WithArmyLifecycle(state.ReadArmyLifecycle()! with { Indexes = operation.Pool.ExportOwnedState() });
    }

    //22E930 selects Force2's actual primary District. Its24 military
    //intelligence stock is independent of City gold and command books.
    private GameState EnsureSeventhTutorialIntelligenceCurrent(GameState state, int minimum)
    {
        var current = CaptureOwnedArmyReturnWorld(state).Context;
        var force = current.ReadForceTableReference(2) ?? throw SeventhMissing("intelligence_Force2");
        var district = ForcePrimaryDistrictSource.Resolve(current, force);
        int physical = SeventhNeed(district.PhysicalSlot, "intelligence_primary_District");
        int before = ReadDistrictAdministrationIntelligenceCurrent(current, physical);
        int after = Math.Clamp(Math.Max(minimum, before), 0, DistrictAdministrationIntelligenceCapacity(state, current, physical));
        WriteDistrictAdministrationIntelligenceCurrent(current, physical, after);
        var fields = state.NativeForceUnregisterCurrent ?? throw SeventhMissing("intelligence_saved_fields");
        return state with { NativeForceUnregisterCurrent = fields with
            { DistrictWindows10 = new(fields.DistrictWindows10) { [physical] = Enumerable.Range(0x10, 28)
                .Select(at => current.ReadDistrictRawByte(physical, at)).ToArray() } } };
    }

    private GameState ClearSeventhTutorialSpyPeopleCurrent(GameState state, IReadOnlyList<int> physicalPeople)
    {
        var current = CaptureOwnedArmyReturnWorld(state).Context;
        foreach (int physical in physicalPeople)
        {
            int person = SeventhPerson(current, physical);
            state = ClearTutorialSpyAssignmentCurrent(state, current, person);
            ushort flags = SeventhNeed(current.ReadArmyReturnPersonFlags196(person), "spy_clear_flags");
            ushort after = (ushort)(flags & 0xFFFE);
            current.WriteArmyReturnPersonFlags196(person, after);
            int id = SeventhNeed(current.ReadPersonIdentity(person), "spy_clear_identity");
            var owner = state.Officers.GetValueOrDefault(id) ?? throw SeventhMissing("spy_clear_Person_owner");
            state = state with { Officers = new(state.Officers) { [id] = owner with
                { Behavior = OfficerBehaviorSourceAdapter.WithSourceFlags(owner.Behavior, after) } } };
        }
        return state;
    }

    private GameState CreateSeventhTutorialEnemyCurrent(GameState state)
    {
        var current = CaptureOwnedArmyReturnWorld(state).Context;
        int person = SeventhPerson(current, TutorialSeventhLessonPlan.EnemyOfficerPhysicalSlot);
        int identity = SeventhNeed(current.ReadPersonIdentity(person), "enemy_identity");
        var created = CreateTutorialArmyPacketCurrent(state, identity, 2000, new(5, 7),
            new("attack", new(DeploymentTargetKind.Location, 7)), 4);
        return created.UnitId is int id ? MoveTutorialArmyCurrent(created.State, id, [10264]) : created.State;
    }

    private GameState CreateSeventhTutorialAttackCurrent(GameState state)
    {
        var current = CaptureOwnedArmyReturnWorld(state).Context;
        int person = SeventhPerson(current, TutorialSeventhLessonPlan.AttackOfficerPhysicalSlot);
        int identity = SeventhNeed(current.ReadPersonIdentity(person), "attack_identity");
        int enemy = SeventhPerson(current, TutorialSeventhLessonPlan.EnemyOfficerPhysicalSlot);
        var target = SeventhNeed(current.ReadPersonNativeValidity(enemy), "attack_enemy_validity")
            ? PersonArmyLocationSource.ReadCommanderArmy(current, enemy) ?? throw SeventhMissing("attack_enemy_Army") : new ArmyUnitReference(0);
        int targetPhysical = SeventhNeed(target.PhysicalSlot, "attack_target_physical");
        uint mask = SeventhNeed(current.ReadPersonFormationMask164(person), "attack_formation_mask");
        int? formation = DeploymentFormationEligibility.Allows(mask, 2, identity) ? 2 : null;
        var targetImage = state.Units.Values.FirstOrDefault(unit => unit.NativeArmyOrigin?.NativeSlotIndex == targetPhysical);
        //The source packet stores literal Person.Id656 independently of the
        //physical656 pointer used for its formation and subsequent Army query.
        var created = CreateTutorialArmyPacketCurrent(state, 656, 3000, new(27, checked((ushort)targetPhysical)),
            new("attack", new(DeploymentTargetKind.Unit, targetImage?.Id ?? throw SeventhMissing("attack_target_owner"))),
            7, sourceLandFormation: formation);
        if (created.UnitId is not int id) return created.State;
        state = MoveTutorialArmyCurrent(created.State, id, [10931]);
        var operation = CaptureOwnedArmyReturnWorld(state); current = operation.Context;
        var army = state.Units.GetValueOrDefault(id) ?? throw SeventhMissing("attack_Army_owner");
        int physical = SeventhNeed(army.NativeArmyOrigin?.NativeSlotIndex, "attack_Army_physical");
        _baseCapacityInputs.BindCurrent(state, current);
        byte morale = checked((byte)BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(new(physical)), current));
        current.WriteArmyReturnMorale(physical, morale);
        var slots = state.TutorialLessonProgress!.RestrictedArmyPhysicalSlots ?? throw SeventhMissing("attack_filter");
        return state with { Units = new(state.Units) { [id] = army with { MoraleState = army.MoraleState with { CurrentValue = morale } } },
            TutorialLessonProgress = state.TutorialLessonProgress with
                { RestrictedArmyPhysicalSlots = slots.Contains(physical) ? slots : slots.Add(physical) } };
    }

    public TutorialLessonInstructionCue QuerySeventhTutorialInstructionCueCurrent(GameState state, int index, int phase)
    {
        int personPhysical = index == 23 && phase == 1 ? 156 : index == 31 && phase == 0 ? 656 :
            throw new ArgumentOutOfRangeException(nameof(index));
        var current = CaptureOwnedArmyReturnWorld(state).Context;
        var army = PersonArmyLocationSource.ReadCommanderArmy(current, SeventhPerson(current, personPhysical))
            ?? throw SeventhMissing("focus_Army");
        int hex = SeventhNeed(current.ReadArmyUnitRawHex2A(army), "focus_Army_Hex");
        return new(null, false) { CameraFocusHexId = hex, DurationMilliseconds = 1000 };
    }

    public bool CanAcceptSeventhTutorialSpyOfficer(GameState state, int officerId, bool away) =>
        officerId == ReadTutorialPhysicalOfficerIdentityCurrent(state, away ? 952 : 147);
    public bool CanAcceptSeventhTutorialAllianceOfficer(GameState state, int officerId) =>
        officerId == ReadTutorialPhysicalOfficerIdentityCurrent(state, 952);
    public bool CanAcceptSeventhTutorialPlacateOfficer(GameState state, int officerId) =>
        officerId == ReadTutorialPhysicalOfficerIdentityCurrent(state, 952);
    private GameState? seventhPlacateAreaWorld;
    private int seventhPlacateAreaId;
    public int QuerySeventhTutorialPlacateAreaId(GameState state)
    {
        if (seventhPlacateAreaWorld is { } prior && ReferenceEquals(prior.Areas, state.Areas) &&
            ReferenceEquals(prior.ResourceState, state.ResourceState)) return seventhPlacateAreaId;
        var current = CaptureCurrentDefinitions(state) ?? throw SeventhMissing("target_current");
        var area = current.ReadAreaTableReference(44) ?? throw SeventhMissing("target_Area44");
        int id = SeventhNeed(area.PhysicalSlot, "target_Area_identity");
        seventhPlacateAreaId = id;
        seventhPlacateAreaWorld = state;
        return id;
    }

    private GameState? seventhObservationWorld;
    private TutorialSeventhLessonObservation? seventhObservationFacts;
    internal TutorialSeventhLessonObservation ReadSeventhTutorialObservationCurrent(GameState state, TutorialSeventhLessonObservation observed)
    {
        if (state.TutorialLessonProgress is not { CourseIndex: 6, Stage: >= 158 }) return observed;
        if (seventhObservationWorld is not { } prior || !ReferenceEquals(prior.Units, state.Units) ||
            !ReferenceEquals(prior.Officers, state.Officers) || !ReferenceEquals(prior.ArmyLifecycle, state.ArmyLifecycle) ||
            !ReferenceEquals(prior.ResourceState, state.ResourceState))
        {
            seventhObservationWorld = state;
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            bool complete = true;
            int enemy = SeventhPerson(current, 156);
            if (SeventhNeed(current.ReadPersonNativeValidity(enemy), "objective_Person_validity"))
            {
                var army = PersonArmyLocationSource.ReadCommanderArmy(current, enemy) ?? throw SeventhMissing("objective_Army");
                if (SeventhNeed(current.ReadArmyUnitNativeValidity(army), "objective_Army_validity"))
                {
                    int commander = SeventhNeed(current.ReadArmyUnitCommanderId(army), "objective_commander");
                    int person = SeventhNeed((current.ReadPersonLookupReference(commander <= 6000 ? commander : 0))?.PhysicalSlot, "objective_commander_pointer");
                    if (SeventhNeed(current.ReadPersonNativeValidity(person), "objective_commander_validity"))
                        complete = SeventhNeed(current.ReadPersonStatus(person), "objective_commander_status") == 6;
                }
            }
            int actor = SeventhPerson(current, 656);
            //4AAB00 does not complete the paragraph when its Person is invalid.
            //Only a valid person whose actual commander Army is gone returns.
            bool returnArmy = true;
            if (SeventhNeed(current.ReadPersonNativeValidity(actor), "return_Person_validity"))
                returnArmy = SeventhNeed(current.ReadArmyUnitNativeValidity(PersonArmyLocationSource.ReadCommanderArmy(current, actor)
                    ?? throw SeventhMissing("return_Army")), "return_Army_validity");
            seventhObservationFacts = new(observed.Common) { AttackObjectiveComplete = complete, AttackReturnArmyValid = returnArmy };
        }
        return observed with { AttackObjectiveComplete = seventhObservationFacts!.AttackObjectiveComplete,
            AttackReturnArmyValid = seventhObservationFacts.AttackReturnArmyValid };
    }

    private static int SeventhPerson(SourceWorldContext current, int physical) =>
        SeventhNeed(current.ReadTransferPersonTableReference(physical)?.PhysicalSlot, "Person_pointer");
    private static T SeventhNeed<T>(T? value, string name) where T : struct => value ?? throw SeventhMissing(name);
    private static InvalidDataException SeventhMissing(string name) => new("tutorial.seventh_lesson.world." + name);
}
