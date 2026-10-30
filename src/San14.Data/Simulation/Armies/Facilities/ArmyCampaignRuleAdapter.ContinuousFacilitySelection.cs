using San14.Core;
using San14.Data.Formats;
using static San14.Data.FacilityConstructionDefinitions;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    public string QueryFacilityPlacementHint(string locale,string name,bool finalEndpoint)=>
        new NarrationResourceMessages(_repository.LoadMessages(locale,MessageKind.Game).Messages)
            .ResolveFacilityPlacementHint(name,finalEndpoint);

    public FacilityConstructionQuote QueryContinuousFacilityConstructionQuote(GameState state,int unitId,
        int initialHexId,int finalHexId,string definitionKey)
    {
        var first=QueryFacilityConstructionQuote(state,unitId,initialHexId,definitionKey);
        if(first.Allowed!=true)return first;
        if(finalHexId is <0 or >=48400)return first with{Allowed=false,Reason="structure.target"};
        try
        {
            var current=CaptureFacilityConstructionCurrent(state);
            var reference=current.ReadObjectDefinitionReference(MapObjectSourceDirectory.DefinitionSlot(definitionKey))
                ?? throw Missing("continuous_definition_pointer");
            int person=current.ResolveOfficerSource(state.Units[unitId].OfficerId)??throw Missing("continuous_Person_pointer");
            bool allowed=ContinuousConstructionAllows(state,current,initialHexId,finalHexId,reference,
                site=>FacilityConstructionLegality.CheckCommander(state,current,person,site,reference));
            return first with{Allowed=allowed,Reason=allowed?null:"structure.continuation.unavailable"};
        }
        catch(InvalidDataException error){return first with{Allowed=null,UnresolvedInput=error.Message};}
    }

    //7FDAA0: the no-Army path is partitioned into accepted/rejected Hex lists.
    //The commit click requires the rejected list to be empty. Area counters
    //are local draft reservations; they never modify the actual map.
    private static bool ContinuousConstructionAllows(GameState state,SourceWorldContext current,int first,int last,
        ObjectDefinitionReference reference,Func<HexReference,bool> allowed)
    {
        if(Read(current,reference).Category!=3)return false;
        var endpoint=current.ReadHexTableReference(last)??throw Missing("continuous_end_Hex");
        if(!allowed(endpoint))return false;
        int[] path=first==last?[first]:ConstructionContinuationPath(state,current,first,last);
        var remaining=new Dictionary<int,int>();
        int accepted=0;
        foreach(int id in path)
        {
            var hex=current.ReadHexTableReference(id)??throw Missing("continuous_path_Hex");
            int raw=K(current.ReadHexAreaSlot(hex),"continuous_Area_link");
            var area=current.ReadAreaTableReference(raw<=500?raw:0)??throw Missing("continuous_Area_pointer");
            int areaId=K(area.PhysicalSlot,"continuous_Area_identity");
            if(!remaining.TryGetValue(areaId,out int capacity))
            {
                capacity=K(current.ReadAreaFacilityCapacity(area),"continuous_Area_capacity");
                for(int key=AreaHexIterator.FindNextIndex(0,areaId,current);key<48400;
                    key=AreaHexIterator.FindNextIndex(key+1,areaId,current))
                {
                    var entry=current.ReadHexTableReference(key)??throw Missing("continuous_Area_Hex");
                    var obj=FacilityConstructionLegality.ObjectAt(current,entry);
                    if(!K(current.ReadObjectNativeValidity(obj),"continuous_Area_Object_validity"))continue;
                    byte category=K(current.ReadObjectDefinitionRaw2C(FacilityConstructionLegality.DefinitionOf(current,obj)),
                        "continuous_Area_Object_category");
                    if(category is not(1 or 2))capacity--;
                }
            }
            //The retained original selection has a limit of ten Hexes.
            if(capacity<1||accepted>=10||!allowed(hex))return false;
            remaining[areaId]=capacity-1;accepted++;
        }
        return true;
    }
}
