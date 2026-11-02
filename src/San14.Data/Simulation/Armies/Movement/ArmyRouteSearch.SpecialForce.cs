using San14.Core;

namespace San14.Data;

internal static partial class ArmyRouteSearch
{
    //25F2B0 passes the zero-initialized null-Army descriptor to1845B0,
    //with mode1/flags1. Keep its absent Force operand rather than substitute
    //the special actor's Force. Reuse the actual default route tree/kernel.
    internal static int[] SpecialForceRaidPath(GameState state, SourceWorldContext current, int from, int to)
    {
        if (!current.ContainsMapHex(from) || !current.ContainsMapHex(to)) return [];
        var fields = state.NativeBaseCaptureCurrent ?? throw new InvalidDataException("original_ai.special.route.Hex16_owner");
        ushort? Word(int slot) => (uint)slot < (uint)fields.HexWords16.Length ? fields.HexWords16[slot]
            : throw new InvalidDataException("original_ai.special.route.Hex16_index");
        var query = new Queries(state, current, new CampaignMovement(0), from, to, 0, 0, null, 1,
            hasArmyDescriptor: false, readHexWord16: Word);
        try { return Search(query, out _, out _, hasArmyDescriptor: false); }
        catch (MissingOwner error) { throw new InvalidDataException(error.Message, error); }
    }
}
