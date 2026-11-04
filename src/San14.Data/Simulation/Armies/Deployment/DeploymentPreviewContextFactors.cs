using San14.Core;

namespace San14.Data;

/// <summary>Context queries for an unregistered preview. Its native center
/// Hex is supplied explicitly; no live Unit or manager receiver is created.</summary>
internal static class DeploymentPreviewContextFactors
{
    public static float ReadEmptyNeighborFactor(GameState state, SourceWorldContext current,
        int commanderPhysical, int hexId)
    {
        if ((Need(current.ReadArmyReturnPersonFlags196(commanderPhysical), "commander_flags") & 0x4000) != 0)
            return 1f;
        if (state.NativeArmySimulation?.Parameters.Int32Values.TryGetValue(DeploymentDraftFigureRules.NeighborRadiusKey, out int radius) != true)
            throw Missing("current_radius");
        if (radius is < 0 or > 255) throw Missing("supported_radius");
        var center = current.ReadHexTableReference(hexId) ?? throw Missing("center_pointer");
        //2AFC20 consumes2A78D0, which walks rings only and resolves each
        //Hex's signed1C through the current manager table. Valid candidates
        //need the still-unclosed friendship/home/exclusion filters.
        var ring = current.CreateNearbyTemporary();
        try
        {
            _ = NearbyArmyTemporarySource.FillNearbyHexTemporary(current, center, radius, ring);
            var node = current.ReadSkillTemporaryHead(ring) ?? throw Missing("ring_head");
            while (node.Identity.HasValue)
            {
                var hex = current.ReadHexListNodePayload(node) ?? throw Missing("ring_hex");
                int slot = unchecked((short)Need(current.ReadHexRaw1C(hex), "current_occupancy"));
                if (slot is >= 1 and <= 500)
                {
                    var army = current.ReadArmyUnitTableReference(slot) ?? throw Missing("candidate_pointer");
                    if (Need(current.ReadArmyUnitNativeValidity(army), "candidate_validity"))
                        throw Missing("nonempty_candidate_set");
                }
                node = current.ReadHexListNodeNext(node) ?? throw Missing("ring_next");
            }
            //All settings/policy coefficients multiply a count of zero.
            //Their pure source reads cannot change this factor from1.
            return 1f;
        }
        finally { current.ClearNearbyTemporary(ring, true); }
    }

    private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("deployment.figures.preview_neighbors." + field);
}
