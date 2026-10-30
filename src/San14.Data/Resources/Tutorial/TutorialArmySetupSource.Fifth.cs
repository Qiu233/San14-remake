using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>5652E0's explicit land formation and single instant path.
    /// This wrapper has approach1, independently of5650C0's approach2.</summary>
    internal GameState CreateFifthTutorialPositionedArmyCurrent(GameState state,int personPhysical,int hex,int troops,int landFormation)
    {
        var current = CaptureOwnedArmyReturnWorld(state).Context;
        int person = TutorialArmyKnown((current.ReadTransferPersonTableReference(personPhysical)
            ?? throw TutorialArmyMissing("positioned_Person_pointer")).PhysicalSlot,"positioned_Person");
        int identity = TutorialArmyKnown(current.ReadPersonIdentity(person),"positioned_identity");
        var created = CreateTutorialArmyPacketCurrent(state,identity,troops,new(9,checked((ushort)hex)),
            new("stay",new(DeploymentTargetKind.Hex,hex)),13,sourceLandFormation:landFormation,sourceEnemyApproach:1);
        return created.UnitId is int id ? MoveTutorialArmyCurrent(created.State,id,[hex]) : created.State;
    }

    /// <summary>The tutorial's direct29F220(mode1) calls, then actual pending
    /// destruction28E6D0 and command maintenance274D70. It adds no stage/time.</summary>
    internal GameState ReturnFifthTutorialArmiesCurrent(GameState state,IReadOnlyList<int> physicalPeople)
    {
        var operation = CaptureOwnedArmyReturnWorld(state);
        var current = operation.Context;
        var target = current.ReadCityTableReference(7) ?? throw TutorialArmyMissing("return_City7");
        foreach (int physical in physicalPeople)
        {
            int person = TutorialArmyKnown((current.ReadTransferPersonTableReference(physical)
                ?? throw TutorialArmyMissing("return_Person_pointer")).PhysicalSlot,"return_Person");
            var army = PersonArmyLocationSource.ReadCommanderArmy(current,person) ?? throw TutorialArmyMissing("return_Army");
            if (!TutorialArmyKnown(current.ReadArmyUnitNativeValidity(army),"return_Army_validity")) continue;
            int at = TutorialArmyKnown(army.PhysicalSlot,"return_Army_physical");
            var image = FArmy(state,at);
            var prepared = PrepareOwnedArmyReturnCurrent(state,army,target,1,current,operation.Pool,
                CreateCaptureGovernorQueries(state),new(false,false),
                image.NativeTacticRuntime?.Status10 == 2 ? image.NativeTacticRuntime.Flags26 : null);
            var packet = prepared.Packet ?? throw TutorialArmyMissing(prepared.UnresolvedOwner ?? "return_packet");
            state = OwnedArmyReturnWorld.Borrow(state,current,operation.Pool).CommitPrepared(state,packet)
                ?? throw TutorialArmyMissing("return_publication");
        }
        state = OwnedArmyLifecycleDetachedInputs.PublishCurrent(state,current);
        state = ConsumeArmyLifecyclePendingCurrent(state,current,operation.Pool)
            ?? throw TutorialArmyMissing("return_pending_cleanup");
        state = state.WithArmyLifecycle(state.ReadArmyLifecycle()! with { Indexes = operation.Pool.ExportOwnedState() });
        //274D70 traverses only the surviving native registry. Use the same
        //ordinary per-Army business leaf, without manufacturing stage28.
        current = CaptureWritableTransportRouteCurrent(state);
        var entries = current.ReadRegistryReferences(RegistryKind.Army) ?? throw TutorialArmyMissing("return_command_registry");
        var maintenance = FacilityOperation.Playable(state);
        foreach (var entry in entries)
        {
            if (entry is not { Kind:RegistryKind.Army,PhysicalSlot:int at }) throw TutorialArmyMissing("return_command_pointer");
            current.SetOwnedPersonCommandManager(state.PersonnelCommands);
            state = MaintainOrdinaryArmyCommand(state,FArmy(state,at),maintenance,null,current);
        }
        return state.WithArmyLifecycle(state.ReadArmyLifecycle()! with
            { Indexes = (current.ReadOwnedCurrentListPool() ?? throw TutorialArmyMissing("return_command_pool")).ExportOwnedState() });
    }

    /// <summary>48D450's real sabotage target and its separate advancing
    /// Chouhi packet, installed before instruction186.</summary>
    internal GameState PrepareFifthTutorialDestructionCurrent(GameState state)
    {
        var operation = CaptureOwnedArmyReturnWorld(state);
        var current = operation.Context;
        const int hex = 9386;
        BindFacilityConstructionInputs(state,current);
        //World73958 is the31-entry Facility definition table;73970 is its
        //actual third definition. Force28 is the packet's independent owner.
        var definition = current.ReadObjectDefinitionReference(3) ?? throw TutorialArmyMissing("sabotage_definition3");
        var created = MapObjectCreationSource.CreateFacility(state,current,hex,definition,28);
        state = created.State;
        if (created.Object is not { } obj) throw TutorialArmyMissing("sabotage_receiver_allocation");
        state = FacilityActivationCurrent.EnableAfterRepairCompletion(state,current,obj);
        var site = current.ReadHexTableReference(hex) ?? throw TutorialArmyMissing("sabotage_Hex");
        int own = TutorialArmyKnown(current.ReadHexForceSlot(site),"sabotage_Hex_force");
        if (own != 28)
        {
            var player = current.ReadCurrentPlayerForce() ?? throw TutorialArmyMissing("sabotage_Player");
            int ruler = TutorialArmyKnown(current.ReadMoraleWaveRuler22EAC0(player),"sabotage_Player_ruler");
            int p = TutorialArmyKnown((current.ReadPersonLookupReference(ruler) ?? throw TutorialArmyMissing("sabotage_Player_Person")).PhysicalSlot,"sabotage_Player_physical");
            int home = TutorialArmyKnown(current.ReadPersonFirstLocation(p),"sabotage_Player_Home");
            var city = CurrentGateMapQueries.FindCity(BaseLookup.Find(home,current),current);
            int cityId = TutorialArmyKnown(city.PhysicalSlot,"sabotage_territory_City");
            state = new MonthlyCampaignMaintenanceSource(this,state,current,operation.Pool)
                .ApplyTutorialTerritory([hex],28,cityId,showAppearance:true);
        }
        int at = TutorialArmyKnown(obj.PhysicalSlot,"sabotage_Object_physical");
        if (current.AssignHexObject(site,at,1)) state = OwnedMapObjectLifecycle.WriteHexBinding(state,current,hex,checked((ushort)at));
        var receiver = current.CurrentMapObjectDirectory!.ReceiverAtPhysical(at);
        var effects = state.CampaignEffectRequests ?? throw TutorialArmyMissing("sabotage_effect_owner");
        state = state with { CampaignEffectRequests = effects.Append(CampaignEffectSource.ArmyFacilityConstruction,
            state.NativeMapSeason?.AdvancedCalendarSteps ?? throw TutorialArmyMissing("sabotage_date"),
            [new(null,null) { FacilityConstructionPresentation = new(FacilityConstructionPresentationOperation.EnsureInitialFacilityModel,
                receiver,hex,MapObjectSourceDirectory.DefinitionKey(3),null) },
             new(null,null) { FacilityConstructionPresentation = new(FacilityConstructionPresentationOperation.NotifyFacilityCreated,
                receiver,hex,MapObjectSourceDirectory.DefinitionKey(3),null) }]) };
        state = state.WithArmyLifecycle(state.ReadArmyLifecycle()! with { Indexes = operation.Pool.ExportOwnedState() });
        current = CaptureOwnedArmyReturnWorld(state).Context;
        int person = TutorialArmyKnown((current.ReadTransferPersonTableReference(656) ?? throw TutorialArmyMissing("sabotage_actor")).PhysicalSlot,"sabotage_actor_physical");
        int identity = TutorialArmyKnown(current.ReadPersonIdentity(person),"sabotage_actor_identity");
        var army = CreateTutorialArmyPacketCurrent(state,identity,3000,new(9,hex),new("move",new(DeploymentTargetKind.Hex,hex)),1,
            sourceLandFormation:1,sourceEnemyApproach:2,sourceAfterAction:1);
        return army.UnitId is int id ? MoveTutorialArmyCurrent(army.State,id,[9604]) : army.State;
    }
}
