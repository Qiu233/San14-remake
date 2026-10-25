using San14.Core;

namespace San14.Data;

internal sealed record StrategicCommandTravelReceipt(int StartHexId,int EndHexId,int TravelDays);

/// <summary>1F78A0 and796550 use231670 with a null actor and the actor's
/// actual Force in its fifth argument. This is the ordinary mode8 journey.</summary>
internal static class StrategicCommandTravelSource
{
    internal static StrategicCommandTravelReceipt Resolve(GameState state,SourceWorldContext current,
        int executorPhysical,StrategicCommandTarget target,int? secondForceId,NativeMapSeasonDate sourceDate)
    {
        int rawHome=K(current.ReadPersonFirstLocation(executorPhysical),"executor_E2");
        var source=BaseLookup.Find(rawHome,current);
        var start=Hex(current.ReadBaseCurrentHexIndex(source));
        HexReference end;
        switch(target.Kind)
        {
            case StrategicCommandTargetKind.Area:
                var area=current.ReadAreaTableReference(target.Id<501?target.Id:0) ?? throw Missing("Area_pointer");
                end=Hex(K(current.ReadAreaHexIndex(area),"Area3A"));break;
            case StrategicCommandTargetKind.City:
                var city=current.ReadCityTableReference(target.Id<52?target.Id:0) ?? throw Missing("City_pointer");
                end=Hex(current.ReadBaseCurrentHexIndex(city));break;
            case StrategicCommandTargetKind.Force:
                end=Hex(current.ReadBaseCurrentHexIndex(new DiplomacyCommandSource(state,current).Capital(target.Id)));break;
            case StrategicCommandTargetKind.Person:
                int person=current.ResolveOfficerSource(target.Id) ?? throw Missing("Person_pointer");
                end=Hex(current.ReadBaseCurrentHexIndex(BaseLookup.Find(K(current.ReadPersonFirstLocation(person),"Person_E2"),current)));break;
            default:throw Missing("target_kind");
        }
        var output=current.CreateTemporaryHexList() ?? throw Missing("path_descriptor");
        try
        {
            var force=current.ReadPersonForce(executorPhysical) ?? throw Missing("actor_Force");
            var path=PersonReturnPathSource.Fill(state,current,start,end,force,output);
            return new(K(start.PhysicalSlot,"start_Hex"),K(end.PhysicalSlot,"end_Hex"),
                path.Days ?? throw Missing(path.UnresolvedOwner ?? "duration231670"));
        }
        finally{current.ClearTemporaryHexList(output);current.ReleaseTemporaryHexList(output);}
        HexReference Hex(int raw)=>current.ReadHexTableReference(raw is >=0 and <48400?raw:0) ?? throw Missing("Hex_pointer");
    }
    private static T K<T>(T? value,string field)where T:struct=>value??throw Missing(field);
    private static InvalidDataException Missing(string field)=>new("strategic-command.travel."+field);
}
