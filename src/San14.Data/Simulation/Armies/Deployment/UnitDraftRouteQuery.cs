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
    private readonly Func<IReadOnlyList<int>,TransportDraftEta> eta;
    internal TransportDraftRouteQuery(Func<int,int,TransportDraftMarch> query,
        Func<IReadOnlyList<int>,TransportDraftEta> eta) { this.query=query; this.eta=eta; }
    public TransportDraftMarch QuerySegment(int startHex,int goalHex) => query(startHex,goalHex);
    /// <summary>7C63D0: concatenate every segment except its final point in
    ///source order. Integrate once, without deduplication or adding segment days.</summary>
    public TransportDraftEta QueryEta(IReadOnlyList<int> assembledEtaPath) => eta(assembledEtaPath);
}

public sealed record TransportDraftEta(int? Days, string? UnresolvedInput);
