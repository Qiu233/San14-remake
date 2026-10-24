namespace San14.Data;

/// <summary>One existing-Army target page's shared current. UI owns segment
/// caching and calls only on changed candidate/control points, never per draw.</summary>
public sealed class UnitDraftRouteQuery
{
    private readonly Func<int,int,int,DeploymentDraftRouteSegment> query;
    internal UnitDraftRouteQuery(Func<int,int,int,DeploymentDraftRouteSegment> query) => this.query=query;
    public DeploymentDraftRouteSegment QuerySegment(int firstUnitId,int startHex,int goalHex) => query(firstUnitId,startHex,goalHex);
}

public sealed class TransportDraftRouteQuery
{
    private readonly Func<int,int,TransportDraftMarch> query;
    internal TransportDraftRouteQuery(Func<int,int,TransportDraftMarch> query) => this.query=query;
    public TransportDraftMarch QuerySegment(int startHex,int goalHex) => query(startHex,goalHex);
}
