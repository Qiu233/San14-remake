using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

internal static class TutorialFifthLessonTargets
{
    internal static bool AllowsHex(GameState control,SourceWorldContext current,int hexId,int? activeWaypointIndex=null)
    {
        var p = control.TutorialLessonProgress ?? throw new InvalidDataException("tutorial.fifth_lesson.targets.progress");
        int phase = p.ActiveParagraphIndex is int i ? p.ParagraphPhases[i] : -1;
        return (p.ActiveParagraphIndex,phase) switch
        {
            (21,5 or 6) => hexId is 10500 or 10719,
            (22,5 or 6) => hexId == 9831,
            (23,5 or 6) => hexId == 9609,
            _ => true
        };
    }
    internal static bool AllowsArmy(GameState control,int physical,byte? action)
    {
        var p = control.TutorialLessonProgress ?? throw new InvalidDataException("tutorial.fifth_lesson.targets.progress");
        var slots = p.RestrictedArmyPhysicalSlots ?? throw new InvalidDataException("tutorial.fifth_lesson.targets.snapshot");
        return !slots.Contains(physical);
    }
    internal static bool AllowsFalseReportDestination(GameState control,int hexId) =>
        control.TutorialLessonProgress is not { CourseIndex:4,ActiveParagraphIndex:27 } p || p.ParagraphPhases[27]!=1 || hexId==9831;
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState ApplyFifthTutorialParagraphWorldCurrent(GameState state,int index,int phase)
    {
        RequireSelectedTutorialControl(state);
        if (state.TutorialLessonProgress is not { CourseIndex:4 } p || (uint)index>=32)
            throw FifthWorldMissing("paragraph_owner");
        if (index==0 && phase==0)
        {
            state=InitializeFifthTutorialOpeningCurrent(state);
            state=state with { TutorialLessonProgress=state.TutorialLessonProgress! with
                { RestrictedArmyPhysicalSlots=ImmutableArray<int>.Empty } };
        }
        if (index==12 && phase==0)
        {
            state=PrepareFifthTutorialDestructionCurrent(state);
            state=FifthTutorialFilterPeople(state,[656],exclude:true,clear:false);
        }
        if (index==20 && phase==0)
        {
            state=state with { TutorialLessonProgress=state.TutorialLessonProgress! with
                { RestrictedArmyPhysicalSlots=ImmutableArray<int>.Empty } };
            state=ReturnFifthTutorialArmiesCurrent(state,[656]);
            var current=CaptureOwnedArmyReturnWorld(state).Context;
            int person=FifthWorldPerson(current,656);
            int identity=FifthWorldNeed(current.ReadPersonIdentity(person),"construction_Person_identity");
            ushort before=FifthWorldNeed(current.ReadArmyReturnPersonFlags196(person),"construction_Person15E");
            ushort after=(ushort)(before&0xFFFE);
            current.WriteArmyReturnPersonFlags196(person,after);
            var owner=state.Officers.GetValueOrDefault(identity) ?? throw FifthWorldMissing("construction_Person_owner");
            state=state with { Officers=new(state.Officers) { [identity]=owner with
                { Behavior=OfficerBehaviorSourceAdapter.WithSourceFlags(owner.Behavior,after) } } };
            state=SetFirstTutorialCommandMask(state,1);
            state=WriteFirstTutorialControl(state,60,false);
        }
        if (index==20 && phase==-1) state=WriteFirstTutorialControl(state,74,true);
        if (index==23 && phase==8)
        {
            state=CaptureFifthTutorialCityArmies(state,7);
            state=WriteFirstTutorialControl(state,4,false);
            state=SetFirstTutorialCommandMask(state,0);
        }
        if (index==24 && phase==1) state=ReturnFifthTutorialArmiesCurrent(state,[952,656,147]);
        if (index==25 && phase==-1)
        {
            var operation=CaptureOwnedArmyReturnWorld(state);
            var home=operation.Context.ReadCityTableReference(7) ?? throw FifthWorldMissing("new_terrain_City7");
            state=JoinNormalTurnSelfRecommendationCurrent(state,operation.Context,FifthWorldPerson(operation.Context,1000),home);
            state=state.WithArmyLifecycle(state.ReadArmyLifecycle()! with { Indexes=operation.Pool.ExportOwnedState() });
        }
        if (index==26 && phase==0)
            state=CreateFifthTutorialPositionedArmyCurrent(state,149,9165,12000,1);
        if (index==26 && phase==1)
        {
            foreach (var entry in new[] { (Person:1000,Hex:9604,Troops:2000),(Person:656,Hex:9832,Troops:3000),(Person:147,Hex:10052,Troops:3000) })
                state=CreateFifthTutorialPositionedArmyCurrent(state,entry.Person,entry.Hex,entry.Troops,1);
            state=FifthTutorialFilterPeople(state,[1000,147,656],exclude:true,clear:false);
        }
        if (index==27 && phase==0)
        {
            var current=CaptureOwnedArmyReturnWorld(state).Context;
            var enemy=PersonArmyLocationSource.ReadCommanderArmy(current,FifthWorldPerson(current,149)) ?? throw FifthWorldMissing("false_report_enemy");
            if (FifthWorldNeed(current.ReadArmyUnitNativeValidity(enemy),"false_report_enemy_validity"))
            {
                state=FifthTutorialFilterPeople(state,[1000],exclude:false,clear:false);
                state=FifthTutorialFilterPeople(state,[147,656],exclude:true,clear:false);
                state=WriteFirstTutorialControl(state,58,false);
            }
        }
        if (index==27 && phase==2) state=FifthTutorialFilterPeople(state,[1000],exclude:true,clear:false);
        if (index==28 && phase==-1)
        {
            state=state with { TutorialLessonProgress=state.TutorialLessonProgress! with { RestrictedArmyPhysicalSlots=ImmutableArray<int>.Empty } };
            state=SetFirstTutorialCommandMask(state,3);
        }
        if (index==30 && phase==-1)
            state=state with { TutorialLessonProgress=state.TutorialLessonProgress! with { CompletionCourseIndex=4 } };
        return state;
    }

    private GameState FifthTutorialFilterPeople(GameState state,IReadOnlyList<int> people,bool exclude,bool clear)
    {
        var current=CaptureOwnedArmyReturnWorld(state).Context;
        var progress=state.TutorialLessonProgress ?? throw FifthWorldMissing("filter_progress");
        var source=progress.RestrictedArmyPhysicalSlots ?? throw FifthWorldMissing("filter_snapshot");
        var slots=clear ? ImmutableArray<int>.Empty : source;
        foreach (int slot in people)
        {
            int person=FifthWorldPerson(current,slot);
            if (!FifthWorldNeed(current.ReadPersonNativeValidity(person),"filter_Person_validity")) continue;
            var army=PersonArmyLocationSource.ReadCommanderArmy(current,person) ?? throw FifthWorldMissing("filter_Army");
            if (!FifthWorldNeed(current.ReadArmyUnitNativeValidity(army),"filter_Army_validity")) continue;
            int physical=FifthWorldNeed(army.PhysicalSlot,"filter_Army_physical");
            slots=exclude ? slots.Contains(physical) ? slots : slots.Add(physical) : slots.Remove(physical);
        }
        return slots==source ? state : state with { TutorialLessonProgress=progress with { RestrictedArmyPhysicalSlots=slots } };
    }

    private GameState CaptureFifthTutorialCityArmies(GameState state,int cityId)
    {
        var current=CaptureOwnedArmyReturnWorld(state).Context;
        var city=current.ReadCityTableReference(cityId) ?? throw FifthWorldMissing("filter_City");
        var slots=(state.TutorialLessonProgress!.RestrictedArmyPhysicalSlots ?? throw FifthWorldMissing("filter_snapshot")).ToBuilder();
        foreach (var entry in current.ReadRegistryReferences(RegistryKind.Army) ?? throw FifthWorldMissing("filter_registry"))
        {
            if (entry is not { Kind:RegistryKind.Army,PhysicalSlot:int physical }) throw FifthWorldMissing("filter_registry_pointer");
            if (current.FindArmyUnitCurrentBase(new(physical))==city && !slots.Contains(physical)) slots.Add(physical);
        }
        return state with { TutorialLessonProgress=state.TutorialLessonProgress! with { RestrictedArmyPhysicalSlots=slots.ToImmutable() } };
    }

    private GameState? fifthObservationOwner;
    private SourceWorldContext? fifthObservationCurrent;
    private TutorialFifthLessonObservation? fifthObservationFacts;
    private SourceWorldContext FifthWorldCurrent(GameState state)
    {
        if (fifthObservationOwner is not { } prior || !ReferenceEquals(prior.Units,state.Units) ||
            !ReferenceEquals(prior.Officers,state.Officers) || !ReferenceEquals(prior.Cities,state.Cities) ||
            !ReferenceEquals(prior.Forces,state.Forces) || !ReferenceEquals(prior.Districts,state.Districts) ||
            !ReferenceEquals(prior.MapObjects,state.MapObjects) || !ReferenceEquals(prior.Structures,state.Structures) ||
            !ReferenceEquals(prior.ResourceState,state.ResourceState) || !ReferenceEquals(prior.ArmyLifecycle,state.ArmyLifecycle) ||
            !ReferenceEquals(prior.NativeArmySimulation?.World,state.NativeArmySimulation?.World))
        {
            fifthObservationOwner=state;
            fifthObservationCurrent=CaptureOwnedArmyReturnWorld(state).Context;
            fifthObservationFacts=null;
        }
        return fifthObservationCurrent!;
    }

    internal TutorialFifthLessonObservation ReadFifthTutorialObservationCurrent(GameState state,TutorialFifthLessonObservation observed)
    {
        if (state.TutorialLessonProgress is not { CourseIndex:4 } progress) throw FifthWorldMissing("observation_course");
        if (progress.Stage<89) return observed;
        var current=FifthWorldCurrent(state);
        if (fifthObservationFacts is null)
        {
            bool ready=true;
            foreach (int hex in TutorialFifthLessonPlan.ConstructionSiteHexIds)
            {
                var site=current.ReadHexTableReference(hex) ?? throw FifthWorldMissing("construction_Hex");
                int link=FifthWorldNeed(current.ReadHexObjectLink(site),"construction_Object_link");
                var obj=current.ReadObjectTableReference(link<=3000 ? link : 0) ?? throw FifthWorldMissing("construction_Object");
                if (!FifthWorldNeed(current.ReadObjectNativeValidity(obj),"construction_Object_validity") ||
                    (FifthWorldNeed(current.ReadMapObjectFields16(obj),"construction_Object16")&1)==0) ready=false;
            }
            int enemy=FifthWorldPerson(current,149);
            bool personValid=FifthWorldNeed(current.ReadPersonNativeValidity(enemy),"enemy_Person_validity");
            var enemyArmy=PersonArmyLocationSource.ReadCommanderArmy(current,enemy) ?? throw FifthWorldMissing("enemy_Army");
            bool armyValid=FifthWorldNeed(current.ReadArmyUnitNativeValidity(enemyArmy),"enemy_Army_validity");
            bool commanderValid=false,active=false,matches=false; int? status=null;
            if (armyValid)
            {
                int physical=FifthWorldNeed(enemyArmy.PhysicalSlot,"enemy_Army_physical");
                var image=FArmy(state,physical);
                active=FifthWorldNeed(ArmySavedCommandKnowledge.ReadControl(state,image),"enemy_control140")!=0;
                int identity=FifthWorldNeed(current.ReadArmyUnitCommanderId(enemyArmy),"enemy_commander_identity");
                var commander=(identity<=6000 ? current.ReadPersonLookupReference(identity) : current.ReadTransferPersonTableReference(0))
                    ?? throw FifthWorldMissing("enemy_commander_pointer");
                if (commander.PhysicalSlot is int p)
                {
                    commanderValid=FifthWorldNeed(current.ReadPersonNativeValidity(p),"enemy_commander_validity");
                    if (commanderValid) status=FifthWorldNeed(current.ReadPersonStatus(p),"enemy_commander_status");
                }
                var actor=PersonArmyLocationSource.ReadCommanderArmy(current,FifthWorldPerson(current,1000)) ?? throw FifthWorldMissing("false_report_actor");
                if (FifthWorldNeed(current.ReadArmyUnitNativeValidity(actor),"false_report_actor_validity"))
                {
                    int at=FifthWorldNeed(actor.PhysicalSlot,"false_report_actor_physical");
                    var actorImage=FArmy(state,at);
                    if (FifthWorldNeed(ArmyConfigurationSourceAdapter.ReadAction(state,actorImage),"false_report_action37")==17)
                    {
                        var target=current.ReadArmyLifecycleTargetFields(at) ?? throw FifthWorldMissing("false_report_target");
                        var receiver=state.ReadArmyLifecycle()!.Receivers.FindUnit(actorImage.Id) ?? throw FifthWorldMissing("false_report_receiver");
                        ushort destination=FifthWorldNeed(ArmyMarchSourceAdapter.EncodeDestination(receiver.FalseReportDestination),"false_report_destination13E");
                        matches=target.Kind38==27 && target.Identity3A==physical && destination==9831;
                    }
                }
            }
            fifthObservationFacts=new(observed.Common) { AllConstructionSitesReady=ready,EnemyOfficerValid=personValid,
                EnemyArmyValid=armyValid,EnemyArmyCommanderValid=commanderValid,EnemyArmyCommanderStatus=status,
                FakeNewsOrderMatches=matches,EnemyArmyFalseReportActive=active };
        }
        var facts=fifthObservationFacts;
        return observed with { AllConstructionSitesReady=facts.AllConstructionSitesReady,EnemyOfficerValid=facts.EnemyOfficerValid,
            EnemyArmyValid=facts.EnemyArmyValid,EnemyArmyCommanderValid=facts.EnemyArmyCommanderValid,
            EnemyArmyCommanderStatus=facts.EnemyArmyCommanderStatus,FakeNewsOrderMatches=facts.FakeNewsOrderMatches,
            EnemyArmyFalseReportActive=facts.EnemyArmyFalseReportActive };
    }

    private static int FifthWorldPerson(SourceWorldContext current,int physical) =>
        (current.ReadTransferPersonTableReference(physical) ?? throw FifthWorldMissing("Person_pointer")).PhysicalSlot ?? throw FifthWorldMissing("Person_physical");
    private static T FifthWorldNeed<T>(T? value,string field) where T:struct => value ?? throw FifthWorldMissing(field);
    private static InvalidDataException FifthWorldMissing(string field) => new("tutorial.fifth_lesson.world."+field);
}
