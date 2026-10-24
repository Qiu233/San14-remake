using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Creates a page-owned query handle. Changed actual candidates may search;
    /// repeated input and camera frames reuse the page cache. Current fields are read once, lazily, for this World;
    /// every actual segment still creates its own reset353E80 motion inputs.</summary>
    public DeploymentDraftRouteQuery CreateDeploymentDraftRouteQuery(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var current = new Lazy<SourceWorldContext>(() => CaptureDeploymentDraftMarchCurrent(state));
        return new(Segment, Eta);

        DeploymentDraftRouteSegment Segment(IReadOnlyList<DeploymentDraftRow> groupRows, int startHex, int goalHex)
        {
            try
            {
                if (groupRows.Count == 0) throw DraftMarchMissing("empty_route_group");
                if (startHex is < 0 or >= 48400 || goalHex is < 0 or >= 48400)
                    throw DraftMarchMissing("segment_endpoints");
                // This API names one adjacent group point pair explicitly.
                // Request actions/waypoints are not reset or recursively
                // followed:353E80 itself supplies the reset preview semantics.
                var captured = current.Value;
                var first = ReadDeploymentDraftMarchInputs(state, captured, groupRows[0]);
                var motion = new DeploymentDraftMarchSource.Motion(state, captured, first);
                int[] path = DeploymentDraftMarchSource.Search(state, captured, motion, startHex, goalHex, out int expanded);
                int[] etaPath = path.Length == 0 ? [] : path[..^1];
                var result = new DeploymentDraftRouteSegment(path.Length > 0 ? DeploymentDraftRouteStatus.Found : DeploymentDraftRouteStatus.NoPath,
                    Array.AsReadOnly(path), Array.AsReadOnly(etaPath), expanded, null)
                    {ControlStartHex=startHex,ControlGoalHex=goalHex};
                if (path.Length == 0) return result;
                int? firstSurface=ReadSurface(path[0]),lastSurface=ReadSurface(path[^1]);
                int? controlFirst=path[0]==startHex?firstSurface:ReadSurface(startHex);
                int? controlLast=path[^1]==goalHex?lastSurface:ReadSurface(goalHex);
                return result with{StartSurfaceCode=firstSurface,EndSurfaceCode=lastSurface,
                    ControlStartSurfaceCode=controlFirst,ControlGoalSurfaceCode=controlLast};

                int? ReadSurface(int raw)
                {
                    try{return Surface(raw);}
                    catch(InvalidDataException missing)
                    {result=result with{PresentationUnresolvedInput=result.PresentationUnresolvedInput??missing.Message};return null;}
                }

                int Surface(int raw)
                {
                    if (raw is < 0 or >= 48400) throw DraftMarchMissing("route_presentation_Hex_range");
                    var hex = captured.ReadHexTableReference(raw)
                        ?? throw DraftMarchMissing("route_presentation_Hex_pointer");
                    if (!hex.PhysicalSlot.HasValue) throw DraftMarchMissing("route_presentation_Hex_null");
                    return captured.ReadHexSurfaceCode(hex) ?? throw DraftMarchMissing("route_presentation_Surface10");
                }
            }
            catch (InvalidDataException error) { return Unknown(error.Message); }
            catch (ArmyRouteSearch.MissingOwner error) { return Unknown(error.Message); }
        }
        DeploymentDraftRouteSegment Unknown(string reason) =>
            new(DeploymentDraftRouteStatus.Unresolved, null, null, 0, reason);
        IReadOnlyList<DeploymentDraftMarchRow> Eta(IReadOnlyList<DeploymentDraftRow> rows, IReadOnlyList<int> etaPath) =>
            ReadDeploymentDraftEta(state, rows, etaPath, () => current.Value);
    }
}
