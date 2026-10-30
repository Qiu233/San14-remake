using San14.Core;
using static San14.Data.TrapParameters;

namespace San14.Data;

internal sealed partial class TrapEffectApplication
{
    private void PrepareFacilitySupportAuras(ObjectReference facility,int kind,HexListHandle[] channels)
    {
        //1907A0 walks both collected channels, preserving duplicate Army
        //attempts. 1908F0 uses188880(Hex2A), not the Army-view position.
        float scale=kind==3?0.6f:0.5f;
        foreach(var channel in channels)
            foreach(var hex in FacilityEffectTargets.ReadHexes(Current,channel))
            {
                int index=Need(Current.ReadHexPhysicalSlot(hex),"aura_Hex_identity");
                var indexed=Current.ReadHexTableReference(index<48400?index:0)??throw Missing("aura_indexed_Hex");
                if(!indexed.PhysicalSlot.HasValue)continue;
                int candidate=unchecked((short)Need(Current.ReadHexRaw1C(indexed),"aura_indexed1C"));
                if(candidate is <1 or >500)continue;
                int raw=unchecked((short)Need(Current.ReadHexRaw1C(hex),"aura_source1C"));
                if(raw<0)throw Missing("aura_negative_Army_pointer");
                var army=Current.ReadArmyUnitTableReference(raw<=500?raw:0)??throw Missing("aura_Army_pointer");
                if(!Need(Current.ReadArmyUnitNativeValidity(army),"aura_Army_validity"))continue;
                Feedback.Requests.Add(new(null,null){FacilitySupportAura=new(Feedback.Receiver(facility),ArmyReceiver(army),
                    Feedback.ArmyId(army),kind,Need(Current.ReadArmyUnitRawHex2A(army),"aura_Army_Hex2A"),scale)});
            }
    }

    private void ApplyFacilityStatistics(ObjectReference facility,int channel,HexListHandle targets,int dimension,bool positive)
    {
        var reference=Definition(facility);
        var definition=Current.ReadFacilityEffectDefinition(reference)??throw Missing("statistics_definition");
        int amount=definition.Channel(channel).Amount;
        //The ordinary positive and negative duration constants are both9.
        //They are immutable rule data extracted offline, not view clock ticks.
        const int duration=9;
        foreach(var hex in FacilityEffectTargets.ReadHexes(Current,targets))
        {
            var selected=FieldHexOccupantSource.ReadCurrent(State,Current,Need(hex.PhysicalSlot,"statistics_Hex"));
            var target=selected.Receipt??throw Missing(selected.UnresolvedOwner??"statistics_target");
            if(target.NativeKind!=27)continue;
            var army=Current.ReadArmyUnitTableReference(target.NativePhysicalSlot<=500?target.NativePhysicalSlot:0)
                ??throw Missing("statistics_Army_pointer");
            if(!Need(Current.ReadArmyUnitNativeValidity(army),"statistics_Army_validity"))continue;
            var force=Current.ReadArmyUnitForceReference(army);
            if(force is null)throw Missing("statistics_Force_pointer");
            if(Need(Current.ReadForceNativeValidity(force),"statistics_Force_validity"))
            {
                int kind=Need(reference.PhysicalSlot,"statistics_definition_kind");
                //1915F0 tests22DDA0's definition identity3/4, not its
                //category2. The target Force's57 contribution accumulates
                //into the shared channel amount across the ordered walk.
                if(kind is 3 or 4)
                    amount=unchecked(amount+ForcePolicy(force,57));
            }
            var image=Image(army);var native=image.NativeTacticRuntime??throw Missing("statistics_runtime");
            if(native.StatModifiers4A is not{Length:6}||native.StatDurations50 is not{Length:6})throw Missing("statistics_six_pairs");
            var modifiers=native.StatModifiers4A.ToArray();var durations=native.StatDurations50.ToArray();
            sbyte before=modifiers[dimension];byte beforeDuration=durations[dimension];
            (modifiers[dimension],durations[dimension])=ArmyCampaignRuleAdapter.MergeAutomaticArmyModifier(
                before,beforeDuration,positive?amount:unchecked(-amount),duration);
            WriteImage(army,image with{NativeTacticRuntime=native with{StatModifiers4A=modifiers,StatDurations50=durations}});
            //The Object-caster report only records a changed modifier. A
            //duration-only extension returns before any322F/3230 history.
            if(before!=modifiers[dimension]&&Feedback.PlayerDistrict(army))
                Feedback.StatisticReport(army,facility,dimension,before,modifiers[dimension]);
            var view=package.CaptureFacilityPeriodicArmyViewCurrent(State,Current,army);
            Feedback.Requests.Add(new(null,null){FacilityStatisticPresentation=new(Feedback.Receiver(facility),ArmyReceiver(army),
                Feedback.ArmyId(army),Need(Current.ReadObjectHexIndex(facility),"statistics_Object_Hex10"),
                Need(Current.ReadArmyUnitRawHex2A(army),"statistics_Army_Hex2A"),Need(reference.PhysicalSlot,"statistics_definition"),
                dimension,positive,amount){SourceView=view}});
            if(!positive)AddVictimForce(force);
        }
    }

    private void ApplyFacilityNegativeMorale(ObjectReference facility,HexListHandle targets,int amount)
    {
        foreach(var hex in FacilityEffectTargets.ReadHexes(Current,targets))
        {
            var selected=FieldHexOccupantSource.ReadCurrent(State,Current,Need(hex.PhysicalSlot,"negative_morale_Hex"));
            var target=selected.Receipt??throw Missing(selected.UnresolvedOwner??"negative_morale_target");
            if(target.NativeKind!=27)continue;
            var army=Current.ReadArmyUnitTableReference(target.NativePhysicalSlot<=500?target.NativePhysicalSlot:0)
                ??throw Missing("negative_morale_Army");
            if(!Need(Current.ReadArmyUnitNativeValidity(army),"negative_morale_validity"))continue;
            _=BasePostLoad.ReadMoraleUpper(Current.ReadArmyUnitForceReference(army),Current);
            var adjusted=ArmyMoraleNonzeroCurrent.Apply(State,Current,army,unchecked(-amount));
            if(adjusted.NativeReturn is null||adjusted.Receipt is not{} receipt)throw Missing(adjusted.UnresolvedOwner??"negative_morale_callback");
            WriteImage(army,ArmyMoraleSourceAdapter.WithCurrent(Image(army),receipt.After1A));
            Feedback.Requests.Add(new(null,null){MilitaryMusicPulse=new(Feedback.Receiver(facility),Feedback.ArmyId(army),
                Need(Current.ReadArmyUnitRawHex2A(army),"negative_morale_Hex2A"))
                {Positive=false,SourceView=package.CaptureFacilityPeriodicArmyViewCurrent(State,Current,army)}});
            AddVictimForce(Current.ReadArmyUnitForceReference(army));
            if((Current.ReadPhysicalObject(Need(facility.PhysicalSlot,"negative_morale_Object"))??throw Missing("negative_morale_record")).RawField12==8)
                State=package.QueueFacilityMoraleSpeechCurrent(State,Current,army,moment);
        }
    }

    private ArmyReceiverId ArmyReceiver(ArmyUnitReference army)=>(State.ReadArmyLifecycle()??throw Missing("presentation_lifecycle"))
        .Slots[Need(army.PhysicalSlot,"presentation_Army_pointer")].ReceiverId;
}
