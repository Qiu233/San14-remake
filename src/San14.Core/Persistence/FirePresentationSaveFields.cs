using System.Text.Json.Nodes;

namespace San14.Core;

internal static class FirePresentationSaveFields
{
    private static IEnumerable<JsonObject> Requests(JsonObject state) =>
        (state["CampaignEffectRequests"]?["PendingBatches"] as JsonArray)?.OfType<JsonObject>()
            .SelectMany(batch=>(batch["Requests"] as JsonArray)?.OfType<JsonObject>()??[])??[];
    private static IEnumerable<JsonObject> LegacyQueues(JsonObject state) =>
        new[]{"PendingArmyFireAnnouncements","PendingArmyFireCasts"}
            .SelectMany(key=>(state[key] as JsonArray)?.OfType<JsonObject>()??[]);
    private static IEnumerable<JsonObject> SpeechEntries(JsonObject state)=>
        (state["ArmyMapSpeech"]?["Entries"] as JsonArray)?.OfType<JsonObject>()??[];

    internal static void RejectLegacyFields(JsonObject state,int version)
    {
        if(version>=185)return;
        bool Future(JsonObject request)=>request["ArmyFireCast"] is not null||request["ArmyFireAnnouncement"] is not null||
            request["FireDamagePresentation"]?["SourceSnapshot"] is not null||
            request["TerrainDamagePresentation"]?["SourceSnapshot"] is not null;
        var oldRequests=(state["NormalCombatRefresh"]?["PendingEffects"] as JsonArray)?.OfType<JsonObject>()
            .SelectMany(batch=>(batch["Effects"] as JsonArray)?.OfType<JsonObject>()??[])??[];
        if(Requests(state).Concat(oldRequests).Any(Future)||LegacyQueues(state).Any(item=>item["SourceSnapshot"] is not null)||
            SpeechEntries(state).Any(entry=>entry["FireCommandOrigin"] is not null)||
            (state["CampaignEffectRequests"]?["PendingBatches"] as JsonArray)?.OfType<JsonObject>().Any(batch=>
                batch["Source"] is JsonValue source&&source.TryGetValue<int>(out int value)&&
                value>=(int)CampaignEffectSource.ArmyFirePresentation)==true)
            throw new InvalidDataException("Legacy world contains future source-time fire presentation operands.");
    }

    internal static void Migrate(JsonObject state,int version)
    {
        foreach(var entry in SpeechEntries(state))
        {
            if(version<185)entry["FireCommandOrigin"]=null;
            else Require(entry,"FireCommandOrigin");
            if(entry["FireCommandOrigin"] is JsonNode origin&&
                (origin is not JsonValue value||!value.TryGetValue<bool>(out _)||
                 entry["ActionCode"] is not JsonValue action||!action.TryGetValue<int>(out int code)||code!=6))throw Invalid();
        }
        foreach(var item in LegacyQueues(state))
        {
            if(version<185)item["SourceSnapshot"]=null;
            else Require(item,"SourceSnapshot");
            if(item["SourceSnapshot"] is JsonNode snapshot)
            {
                if(item.ContainsKey("HexId"))CastSnapshot(snapshot);
                else AnnouncementSnapshot(snapshot);
            }
        }
        foreach(var request in Requests(state))
        {
            if(version<185){request["ArmyFireCast"]=null;request["ArmyFireAnnouncement"]=null;}
            else Require(request,"ArmyFireCast","ArmyFireAnnouncement");
            if(request["ArmyFireCast"] is JsonNode cast)
            {
                var fields=Exact(cast,"UnitId","OfficerId","HexId","SourceSnapshot");
                if(fields["SourceSnapshot"] is JsonNode snapshot)CastSnapshot(snapshot);
            }
            if(request["ArmyFireAnnouncement"] is JsonNode announcement)
            {
                var fields=Exact(announcement,"UnitId","OfficerId","PersonalityId","FromFireCommand","SourceSnapshot");
                if(fields["SourceSnapshot"] is JsonNode snapshot)AnnouncementSnapshot(snapshot);
            }
            foreach(string key in new[]{"FireDamagePresentation","TerrainDamagePresentation"})
                if(request[key] is JsonObject damage)
                {
                    if(version<185)damage["SourceSnapshot"]=null;
                    else Require(damage,"SourceSnapshot");
                    if(damage["SourceSnapshot"] is not JsonNode snapshot)continue;
                    var fields=Exact(snapshot,"ArmyReceiverId","BaseReceiverId","HexId","DisplayModifier");
                    if(fields["ArmyReceiverId"] is JsonNode army)Receiver(army);
                    if(fields["BaseReceiverId"] is JsonNode receiver)Receiver(receiver);
                }
        }
    }
    private static void CastSnapshot(JsonNode snapshot)
    {
        var fields=Exact(snapshot,"SourceReceiverId","SourceView");Receiver(fields["SourceReceiverId"]);
        var view=Exact(fields["SourceView"],"Present","X","Y","Z","W");
        if(view["Present"] is not JsonValue present||!present.TryGetValue<bool>(out _))throw Invalid();
        foreach(string component in new[]{"X","Y","Z","W"})
            if(view[component] is not JsonValue value||!value.TryGetValue<float>(out float number)||!float.IsFinite(number))throw Invalid();
    }
    private static void AnnouncementSnapshot(JsonNode snapshot)
    {
        var fields=Exact(snapshot,"SourceReceiverId","SpeakerHexId");Receiver(fields["SourceReceiverId"]);
    }
    private static void Receiver(JsonNode? node)
    {
        var fields=Exact(node,"Value");
        if(fields["Value"] is not JsonValue value||!value.TryGetValue<int>(out int identity)||identity<=0)throw Invalid();
    }
    private static JsonObject Exact(JsonNode? node,params string[] keys)
    {
        if(node is not JsonObject fields||fields.Count!=keys.Length)throw Invalid();Require(fields,keys);return fields;
    }
    private static void Require(JsonObject fields,params string[] keys)
    {if(keys.Any(key=>!fields.ContainsKey(key)))throw Invalid();}
    private static InvalidDataException Invalid()=>new("Saved source-time fire presentation shape is incomplete or invalid.");
}
