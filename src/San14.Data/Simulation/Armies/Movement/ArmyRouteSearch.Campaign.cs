using San14.Core;

namespace San14.Data;

internal static partial class ArmyRouteSearch
{
    //73D340 uses1845B0(mode1, flags1) with a null Army and actual Force.
    //The selected key is the returned total cost, not path length.
    internal static int CampaignTargetCost(GameState state, SourceWorldContext current,
        int force, int start, int goal)
    {
        if (!current.ContainsMapHex(start) || !current.ContainsMapHex(goal)) return 0;
        var fields = state.NativeBaseCaptureCurrent ?? throw new InvalidDataException("campaign.deployment.route_Hex16_owner");
        ushort? ReadHex16(int physical) => (uint)physical < (uint)fields.HexWords16.Length
            ? fields.HexWords16[physical] : throw new InvalidDataException("campaign.deployment.route_Hex16_index");
        var query = new Queries(state, current, new CampaignMovement(force), start, goal, 0, 0, null, 1,
            hasArmyDescriptor: false, readHexWord16: ReadHex16);
        try { _ = Search(query, out _, out int cost, hasArmyDescriptor: false); return cost; }
        catch (MissingOwner error) { throw new InvalidDataException(error.Message, error); }
    }
    private sealed class CampaignMovement(int force) : Movement
    {
        internal override bool AvoidHostile => false;
        internal override byte LandFormation => 0;
        internal override byte NavalFormation => 0;
        internal override int Force => force;
        internal override int HomeHex => 0;
        internal override bool WallPermission => true;
        internal override byte? Action => null;
        internal override bool HasNoDestination => true;
        internal override float ReadMobility(int formation, byte explicitSurface) =>
            throw new InvalidDataException("campaign.deployment.null_army_mobility");
    }
}
