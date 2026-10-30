// The existing native second-focus layer has two current consumers: a command
// display, or the ordinary summary's City focus. Commands retain priority and
// their own draft/candidate lifecycle. This owner translates only the final
// visible choice into the native session's one monotonic publication stream.
internal sealed class D3D11MapDisplayPublicationOwner
{
    private D3D11CommandMapPublication? publication;
    private D3D11CommandMapData? data;
    private ulong revision,dataRevision,generation,commandGeneration;
    private uint hex=D3D11CommandMapData.CellCount;
    private bool commandOwned;
    internal D3D11CommandMapPublication? Capture(D3D11CommandMapPublication? command,
        D3D11CommandMapData? summary,uint summaryHex)
    {
        bool nextCommand=command is {Present:true};
        var nextData=nextCommand?command!.Data:summary;
        uint nextHex=nextCommand?command!.HoverHex:summaryHex;
        if(nextData is null)nextHex=D3D11CommandMapData.CellCount;
        ulong nextCommandGeneration=nextCommand?command!.ProviderGeneration:0;
        bool leaseChanged=nextCommand!=commandOwned || nextCommand&&nextCommandGeneration!=commandGeneration ||
            data?.Kind!=nextData?.Kind;
        if(ReferenceEquals(data,nextData)&&hex==nextHex&&!leaseChanged)return publication;
        bool wasPresent=data is not null,present=nextData is not null;
        if(!wasPresent&&!present&&publication is null)return null;
        if(wasPresent!=present)generation=checked(generation+1);
        else if(present&&leaseChanged)generation=checked(generation+2);
        if(!ReferenceEquals(data,nextData))dataRevision=checked(dataRevision+1);
        data=nextData;hex=nextHex;commandOwned=nextCommand;commandGeneration=nextCommandGeneration;
        ulong current=checked(++revision);
        publication=data?.Kind==D3D11CommandMapDisplayKind.AreaAdministration
            ?new(data,current,dataRevision,generation,hex)
            :D3D11CommandMapPublication.ForCampaignFocus(data,current,present?dataRevision:0,generation,hex);
        return publication;
    }
}
