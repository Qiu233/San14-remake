using System.Text.Json.Nodes;
namespace San14.Core;
internal static class DuelPresentationSnapshotSaveFields
{
    internal static void RejectLegacyFields(JsonObject state,int version)
    {
        if(version<=213&&Duels(state).Any(duel=>duel["PresentationSnapshot"] is not null))
            throw new InvalidDataException("Legacy save contains a future duel source-time presentation snapshot.");
    }
    //Call after the existing CampaignDuelService block has normalized its
    //older Registered/CompletedPresentations owners. Other fields stay intact.
    internal static void Migrate(JsonObject state,int version)
    {
        foreach(var duel in Duels(state))
        {
            if(version<=213){duel["PresentationSnapshot"]=null;continue;}
        }
        ValidateWorld(state);
    }
    internal static void ValidateWorld(JsonObject state)
    {
        foreach(var duel in Duels(state))
        {
            if(!duel.ContainsKey("PresentationSnapshot"))throw Invalid();
            if(duel["PresentationSnapshot"] is null)continue;
            if(duel["PresentationSnapshot"] is not JsonObject snapshot||duel["Pair"] is not JsonObject pair||
                !Person(snapshot["Actor"],pair["ActorOfficerId"])||!Person(snapshot["Opponent"],pair["OpponentOfficerId"]))throw Invalid();
        }
    }
    private static bool Person(JsonNode? node,JsonNode? expected)=>node is JsonObject value&&
        value["OfficerId"] is JsonValue id&&id.TryGetValue<int>(out int identity)&&
        expected is JsonValue expectedId&&expectedId.TryGetValue<int>(out int other)&&identity==other&&
        value.ContainsKey("Name")&&Text(value["Name"],true)&&
        value.ContainsKey("PortraitAppearanceIndex")&&IntegerOrNull(value["PortraitAppearanceIndex"],0,65535)&&
        value.ContainsKey("PortraitAdmittedAppearanceIndex")&&IntegerOrNull(value["PortraitAdmittedAppearanceIndex"],0,65535)&&
        (value["PortraitAdmittedAppearanceIndex"] is null||value["PortraitAppearanceIndex"] is not null)&&
        value.ContainsKey("Strength")&&IntegerOrNull(value["Strength"],int.MinValue,int.MaxValue)&&
        value.ContainsKey("Flag")&&(value["Flag"] is null||value["Flag"] is JsonObject flag&&
            Integer(flag["ColorIndex"],0,255)&&Text(flag["Glyph"]))&&
        value["Traits"] is JsonArray traits&&traits.Count<=5&&traits.All(trait=>trait is JsonObject entry&&
            Integer(entry["SourceIndex"],1,355)&&Text(entry["Text"])&&Integer(entry["Style"],0,255))&&
        value["UnresolvedFields"] is JsonArray issues&&issues.All(issue=>Text(issue));
    private static bool IntegerOrNull(JsonNode? node,int minimum,int maximum)=>node is null||Integer(node,minimum,maximum);
    private static bool Integer(JsonNode? node,int minimum,int maximum)=>node is JsonValue value&&value.TryGetValue<int>(out int number)&&number>=minimum&&number<=maximum;
    private static bool Text(JsonNode? node,bool nullable=false)=>node is null?nullable:node is JsonValue value&&value.TryGetValue<string>(out string? text)&&text is not null&&!text.Contains('\0');
    private static InvalidDataException Invalid()=>new("Saved duel source-time presentation knowledge is missing or invalid.");
    private static IEnumerable<JsonObject> Duels(JsonObject state)
    {
        if(state["CampaignDuelService"] is not JsonObject service)yield break;
        if(service["Registered"] is JsonObject registered)yield return registered;
        if(service["CompletedPresentations"] is JsonArray completed)
            foreach(var node in completed)if(node is JsonObject duel)yield return duel;
    }
}
