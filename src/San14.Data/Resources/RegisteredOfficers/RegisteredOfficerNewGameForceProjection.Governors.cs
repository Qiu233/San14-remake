using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

public static partial class RegisteredOfficerNewGameForceProjection
{
    private static void ApplyGovernors(S14Document document,ScenarioCityPreview snapshot,
        IDictionary<int,CityPreviewRecord> cities,IDictionary<int,GatePreviewRecord> gates,
        IReadOnlyDictionary<int,ForcePreviewRecord> forces,IReadOnlyDictionary<int,DistrictPreviewRecord> districts,
        ISet<int> configuredCities)
    {
        var table=S14ScenarioTables.ReadCharacterNames(document,snapshot.Locale);
        var positions=S14ScenarioTables.ReadCharacterPlacements(document,snapshot.Locale);
        var ids=S14ScenarioTables.ReadCharacterIdentities(document,snapshot.Locale);
        var credit=S14CreditEffectReader.ReadCharacterReferences(document,snapshot.Locale).Characters;
        var context=new SourceWorldContext();context.SetBasePersonPointer(0);context.SetPersonTablePointer(0,0);
        //An initial scenario has no running Person command/associated
        //tasks. These are the same source constructors' empty owners.
        context.SetCurrentPersonLinkedEntities([]);
        foreach(var person in positions)
        {
            var reference=credit[person.Slot];
            context.SetPersonRecord(new(person.Slot,ids[person.Slot].CharacterId,
                reference.RawFlagsWord??throw Missing("Person_behavior_flags"),reference.RawTailCode,
                person.StatusCode,person.SecondaryCitySlot));
            context.SetPersonFirstLocation(person.Slot,person.CitySlot);
            context.SetPersonDistrictSlot(person.Slot,person.DistrictSlot);
            if(ids[person.Slot].CharacterId!=0)context.SetPersonTablePointer(ids[person.Slot].CharacterId,person.Slot);
        }
        context.SetRegisteredPersonPhysicalSlots(positions.Where(person=>person.StatusCode!=9)
            .Select(person=>(int?)person.Slot).ToArray());
        foreach(var force in forces.Values)
        {context.SetForceRecord(new(force.Slot,force.RulerSlot));context.SetForceTablePointer(force.Slot,force.Slot);}
        foreach(var district in districts.Values)
        {context.SetDistrictRecord(new(district.Slot,district.ForceSlot,district.Number,district.LeaderSlot));context.SetDistrictTablePointer(district.Slot,district.Slot);}
        var queries=new NewGameGovernorQueries(document,table,snapshot);
        int delta=snapshot.Locale=="SC"?56:0;
        int cityStart=8+document.LeadingRecordCount!.Value*2+0x22,gateStart=cityStart+52*23+8;
        foreach(int id in configuredCities.Order())
        {
            var city=cities[id];var target=new BaseReference(BaseKind.City,id);
            context.SetCityRecord(new(id,(ushort)id,city.RawValidityFlag69??throw Missing("City_validity")));
            context.SetArmyReturnBaseGovernor2E(target,city.OfficerSlot);
            Apply(target,cityStart+id*23,governor=>cities[id]=city with{OfficerSlot=governor,
                OfficerName=Name(governor)});
        }
        foreach(var gate in gates.Values.Where(gate=>gate.DistrictSlot!=0&&
            configuredCities.Any(city=>gate.ForceSlot==cities[city].ForceSlot)).ToArray())
        {
            var target=new BaseReference(BaseKind.Gate,gate.Slot);
            context.SetGateValidityFlag(gate.Slot,gate.RawValidityFlag69);
            context.WriteBaseObjectLink(target,(ushort)gate.LocationSlot);
            context.SetArmyReturnBaseGovernor2E(target,gate.OfficerSlot);
            Apply(target,gateStart+gate.Slot*20,governor=>gates[gate.LocationSlot]=gate with{
                OfficerSlot=governor,OfficerName=Name(governor)});
        }

        void Apply(BaseReference target,int at,Action<ushort> update)
        {
            var resolution=ArmyReturnGovernorUpdater.PrepareCurrent(target,context,queries);
            var packet=resolution.Packet??throw Missing(resolution.UnresolvedOwner??"governor_resolution");
            ArmyReturnGovernorUpdater.ApplyCurrentWrites(context,packet);
            Word(document.DecodedPayload,at,packet.GovernorWord2EAfter);update(packet.GovernorWord2EAfter);
            foreach(var write in packet.PersonWrites)
                document.DecodedPayload[table.RecordOffset+write.PersonPhysical*table.RecordStride+161+delta]=write.StatusAfter;
        }
        string? Name(int id)=>id==0?null:table.Names[ids.Single(value=>value.CharacterId==id).Slot].DisplayName;
    }

    private sealed class NewGameGovernorQueries(S14Document document,S14CharacterTable table,ScenarioCityPreview snapshot)
        :IArmyReturnGovernorCurrentQueries
    {
        public ArmyReturnGovernorComparatorFields? ReadCurrentComparatorFields(int physical)
        {
            var row=document.DecodedPayload.AsSpan(table.RecordOffset+physical*table.RecordStride,table.RecordStride);
            int delta=snapshot.Locale=="SC"?56:0;byte office=row[162+delta];
            var definition=(snapshot.RankParameters??throw Missing("Office_catalog")).Offices[office];
            return new(0,office,definition.Raw45,definition.Raw39,office,
                BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(213+delta,2)),row[163+delta],row[166+delta]);
        }
    }
}
