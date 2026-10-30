using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal int ReadSecondTutorialSelectedOfficerCurrent(GameState state)
    {
        var current = CaptureCurrentDefinitions(state) ?? throw SecondTutorialMissing("selection_current");
        int person = (current.ReadTransferPersonTableReference(952)
            ?? throw SecondTutorialMissing("selection_Person952_pointer")).PhysicalSlot
            ?? throw SecondTutorialMissing("selection_Person952");
        return current.ReadPersonIdentity(person) ?? throw SecondTutorialMissing("selection_Person10");
    }

    internal GameState ApplySecondTutorialParagraphWorldCurrent(GameState state, int index, int phase)
    {
        RequireSelectedTutorialControl(state);
        if (state.TutorialLessonProgress is not { CourseIndex: 1 } progress || (uint)index >= 14)
            throw SecondTutorialMissing("paragraph_owner");
        if (index == 0 && phase == 0)
        {
            //4BAFB0's actual second-lesson entry, before events127 and128.
            foreach (int flag in new[] { 78, 79, 81, 62, 58 }) state = WriteFirstTutorialControl(state, flag, true);
            state = SetFirstTutorialCommandMask(state, 0);
            var current = CaptureCurrentDefinitions(state) ?? throw SecondTutorialMissing("opening_current");
            var city = current.ReadCityTableReference(7) ?? throw SecondTutorialMissing("opening_City7");
            int troops = Math.Clamp(10000, 0, BasePostLoad.ReadTroopCapacity(city, current));
            int food = Math.Clamp(100000, 0, BasePostLoad.ReadSuppliesCapacity(city, current));
            current.SetBaseResource(city, BaseResourceKind.Troops, troops);
            current.SetBaseResource(city, BaseResourceKind.Supplies, food);
            var owner = state.Cities.GetValueOrDefault(7) ?? throw SecondTutorialMissing("opening_City_owner");
            state = state with { Cities = new(state.Cities) { [7] = owner with { Troops = troops, Supplies = food } } };
            int person = (current.ReadTransferPersonTableReference(952)
                ?? throw SecondTutorialMissing("opening_Person_pointer")).PhysicalSlot
                ?? throw SecondTutorialMissing("opening_Person");
            if (current.ReadPersonNativeValidity(person) ?? throw SecondTutorialMissing("opening_Person_validity"))
            {
                var force = current.ReadPersonForce(person) ?? throw SecondTutorialMissing("opening_Person_Force");
                if (current.ReadForceNativeValidity(force) ?? throw SecondTutorialMissing("opening_Force_validity"))
                {
                    int id = force.PhysicalSlot ?? throw SecondTutorialMissing("opening_Force_physical");
                    current.SetForceRankSlot47(id, 9);
                    var forceOwner = state.Forces.GetValueOrDefault(id) ?? throw SecondTutorialMissing("opening_Force_owner");
                    var simulation = state.NativeArmySimulation ?? throw SecondTutorialMissing("opening_simulation");
                    state = state with { Forces = new(state.Forces) { [id] = forceOwner with { RankId = 9 } },
                        NativeArmySimulation = simulation with { Parameters = simulation.Parameters with
                        { Int32Values = new(simulation.Parameters.Int32Values) { [OwnedArmyMoraleWaveInputs.ForceRank47Key(id)] = 9 } } } };
                }
            }
        }
        if (index == 1 && phase == 0)
        {
            state = SetFirstTutorialCommandMask(state, 1); //4E6F50 ->4E7CA0(command0).
            state = WriteFirstTutorialControl(state, 60, false);
        }
        if (index == 1 && phase == -1) state = SetFirstTutorialCommandMask(state, 0);
        if (index == 9 && phase == 4) state = WriteFirstTutorialControl(state, 4, false); //550180 initializer.
        if (index == 10 && phase == 1)
        {
            //487D20 installs its action filter after event139. Capture the
            //actual commander Army now; later pointer queries retain this slot.
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            int person = (current.ReadTransferPersonTableReference(952)
                ?? throw SecondTutorialMissing("tracked_Person_pointer")).PhysicalSlot
                ?? throw SecondTutorialMissing("tracked_Person");
            bool valid = current.ReadPersonNativeValidity(person) ?? throw SecondTutorialMissing("tracked_Person_validity");
            int army = valid ? (PersonArmyLocationSource.ReadCommanderArmy(current, person)
                ?? throw SecondTutorialMissing("tracked_Army_pointer")).PhysicalSlot
                ?? throw SecondTutorialMissing("tracked_Army_physical") : 0;
            state = state with { TutorialLessonProgress = progress with { RestrictedArmyPhysicalSlot = army } };
        }
        if (index == 10 && phase == -1)
            state = state with { TutorialLessonProgress = progress with { RestrictedArmyPhysicalSlot = null } };
        if (index == 11 && phase == 1) state = ApplySecondTutorialReinforcementsCurrent(state);
        if (index == 12 && phase == -1)
            state = state with { TutorialLessonProgress = progress with { CompletionCourseIndex = 1 } };
        return state;
    }

    private GameState ApplySecondTutorialReinforcementsCurrent(GameState state)
    {
        //484AE0 executes this before event142. Retain the normal29E080
        //join, including loyalty, membership, governor and registration tails.
        var operation = CaptureOwnedArmyReturnWorld(state);
        var current = operation.Context;
        var home = current.ReadCityTableReference(7) ?? throw SecondTutorialMissing("reinforcement_City");
        foreach (int slot in new[] { 185, 715 })
        {
            int person = (current.ReadTransferPersonTableReference(slot)
                ?? throw SecondTutorialMissing("reinforcement_Person_pointer")).PhysicalSlot
                ?? throw SecondTutorialMissing("reinforcement_Person");
            int id = current.ReadPersonIdentity(person) ?? throw SecondTutorialMissing("reinforcement_Person10");
            var owner = state.Officers.GetValueOrDefault(id) ?? throw SecondTutorialMissing("reinforcement_owner");
            current.WritePersonStatus(person, 5);
            var status = OfficerServiceStatusSourceAdapter.Decode(5);
            state = state with { Officers = new(state.Officers) { [id] = owner with
                { ServiceStatus = status, Role = OfficerServiceRules.PublishedRole(status, owner.Role) } } };
            state = JoinNormalTurnSelfRecommendationCurrent(state, current, person, home);
            //The following241F20(kind5,physicalCity7) is a separate source
            //write even though29E080 has just put this Person at the same City.
            current.WritePersonSecondLocation(person, 7);
            owner = state.Officers[id];
            state = state with { Officers = new(state.Officers) { [id] = owner with
                { LocationState = current.ReadOfficerLocation(id) ?? throw SecondTutorialMissing("reinforcement_Position") } } };
            state = OwnedArmyLifecycleDetachedInputs.PublishCurrentPersonFirstLocation(state, current, person);
        }
        return state.WithArmyLifecycle(state.ReadArmyLifecycle()! with { Indexes = operation.Pool.ExportOwnedState() });
    }

    public int QuerySecondTutorialCapitalHex(GameState state)
    {
        var current = CaptureCurrentDefinitions(state) ?? throw SecondTutorialMissing("capital_current");
        var area = current.ReadAreaTableReference(TutorialSecondLessonPlan.CapitalAreaId)
            ?? throw SecondTutorialMissing("capital_Area51_pointer");
        return current.ReadAreaHexIndex(area) ?? throw SecondTutorialMissing("capital_Hex3A");
    }

    internal TutorialSecondLessonObservation ReadSecondTutorialObservationCurrent(GameState state,
        TutorialSecondLessonObservation observed)
    {
        if (state.TutorialLessonProgress is not { CourseIndex: 1 } progress) throw SecondTutorialMissing("observation_course");
        if (progress.Stage < 33) return observed;
        var current = CaptureCurrentDefinitions(state) ?? throw SecondTutorialMissing("observation_current");
        var statistics = state.RegionStatistics ?? throw SecondTutorialMissing("Area_statistics");
        foreach (int id in progress.Stage == 35 ? new[] { 51, 49, 48, 50 } : new[] { 51 })
        {
            var area = statistics.Entries.GetValueOrDefault(id) ?? throw SecondTutorialMissing("Area_statistics_entry");
            current.SetAreaRawField76(id, AreaMapStatisticsInitialization.CountToNativeWord(area.TerrainCellCount));
            current.SetAreaRawField68(id, area.IsOwnedCountCurrent
                ? AreaMapStatisticsInitialization.CountToNativeWord(area.OwnedCellCount) : null);
        }
        var capital = current.ReadAreaTableReference(TutorialSecondLessonPlan.CapitalAreaId)
            ?? throw SecondTutorialMissing("capital_Area51_pointer");
        int owner = OwnedCurrentPlayerForce.ReadRawForce3A(state) ?? throw SecondTutorialMissing("player_Force");
        bool belongs = (current.ReadAreaCurrentForceSlot(capital) ?? throw SecondTutorialMissing("capital_Hex14")) == owner;
        int occupation = current.ReadAreaRawField68(capital) ?? throw SecondTutorialMissing("capital_Area68");
        int maximum = current.ReadAreaRawField76(capital) ?? throw SecondTutorialMissing("capital_Area76");
        bool? complete = null;
        if (progress.Stage == 35)
        {
            bool all = true;
            foreach (int id in TutorialSecondLessonPlan.ConquerAreaIds)
            {
                var area = current.ReadAreaTableReference(id) ?? throw SecondTutorialMissing("conquer_Area_pointer");
                if (!(current.ReadAreaNativeValidity(area) ?? throw SecondTutorialMissing("conquer_Area_validity"))) { all = false; continue; }
                int force = current.ReadAreaCurrentForceSlot(area) ?? throw SecondTutorialMissing("conquer_Area_Force");
                if (force != owner ||
                    (current.ReadAreaRawField68(area) ?? throw SecondTutorialMissing("conquer_Area68")) !=
                    (current.ReadAreaRawField76(area) ?? throw SecondTutorialMissing("conquer_Area76"))) all = false;
            }
            complete = all;
        }
        return observed with { CapitalHexBelongsToPlayer = belongs, CapitalOccupation = occupation,
            CapitalMaximumOccupation = maximum, ConquerAreasComplete = complete };
    }

    private static InvalidDataException SecondTutorialMissing(string field) => new("tutorial.second_lesson.world." + field);
}
