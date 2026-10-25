namespace San14.Data;

internal sealed partial class NarrationResourceMessages
{
    //1B16D0 actions19/20/21/22/24/25. All six supply only Person0.
    //The installed TC/SC records retain their reached FunctionL, shared
    //Function3 choices, and spatial Function5(Person0,5) calls unchanged.
    private static bool ArmyTraitSpeechRoot(int root) => root is
        0xC4F4 or 0xC7C3 or 0xC7CD or 0xC7D7 or 0xC4CF or 0xC4DB or 0xC77B ||
        ArmyTraitActivationRoots.ContainsValue(root);

    //1B3DA0's exact admitted action26 set, with1B16D0's message dispatch.
    //Trait65 additionally supplies Area0 from the source packet's Hex.Area12;
    //the remaining roots need only Person0. No activation root requests audio.
    private static readonly Dictionary<int, int> ArmyTraitActivationRoots = new()
    {
        [10] = 0xC780, [11] = 0xC781, [12] = 0xC782, [13] = 0xC783,
        [14] = 0xC784, [15] = 0xC787, [16] = 0xC788, [17] = 0xC789,
        [18] = 0xC78A, [19] = 0xC78B, [21] = 0xC78E, [22] = 0xC78F,
        [23] = 0xC790, [24] = 0xC791, [26] = 0xC794, [27] = 0xC795,
        [28] = 0xC796, [29] = 0xC797, [32] = 0xC798, [65] = 0xC799,
        [66] = 0xC79A, [77] = 0xC79B, [86] = 0xC79F, [87] = 0xC7A0,
        [94] = 0xC7A1, [105] = 0xC7A2, [106] = 0xC7A3, [108] = 0xC7A6,
        [150] = 0xC7A9, [152] = 0xC77C, [153] = 0xC7AC, [216] = 0xC7E9,
    };

    internal static int? ArmyTraitActivationRoot(int action, int trait)
    {
        if (action == 26) return ArmyTraitActivationRoots.TryGetValue(trait, out int root) ? root : null;
        return action == 27 && trait == 20 ? 0xC77B : null;
    }

    private static readonly HashSet<int> ArmyTraitSpeechMessages =
    [
        0x0098, 0x0099, 0x009A, 0x009B, 0x009C, 0x009D, 0x009F, 0x00A0,
        0x00A1, 0x00A2, 0x00A3, 0x00A4, 0x00A5, 0x00A6, 0x00A7,
        0xC4CF, 0xC4D0, 0xC4D1, 0xC4D2, 0xC4D3, 0xC4D4, 0xC4D5,
        0xC4D6, 0xC4D7, 0xC4D8, 0xC4D9, 0xC4DA, 0xC4DB, 0xC4DC,
        0xC4DD, 0xC4DE, 0xC4DF, 0xC4E0, 0xC4E1, 0xC4E2, 0xC4E3,
        0xC4E4, 0xC4E5, 0xC4E6, 0xC4F4, 0xC4F5, 0xC4F6, 0xC4F7,
        0xC4F8, 0xC4F9, 0xC4FA, 0xC4FB, 0xC4FC, 0xC4FD, 0xC4FE,
        0xC4FF, 0xC500, 0xC501, 0xC502,
        0xC7C3, 0xC7C4, 0xC7C5, 0xC7C6, 0xC7C7, 0xC7C8, 0xC7C9,
        0xC7CA, 0xC7CB, 0xC7CC, 0xC7CD, 0xC7CE, 0xC7CF, 0xC7D0,
        0xC7D1, 0xC7D2, 0xC7D3, 0xC7D4, 0xC7D5, 0xC7D6, 0xC7D7,
        0xC7D8, 0xC7D9, 0xC7DA, 0xC7DB, 0xC7DC, 0xC7DD, 0xC7DE,
        0xC7DF, 0xC7E0, 0xC7E1,
        //Action26's32 roots and action27/trait20, plus their complete
        //installed TC/SC reference closure. Shared0098..00A7 are above.
        0xC77B, 0xC77C, 0xC77D, 0xC77E,
        0xC780, 0xC781, 0xC782, 0xC783, 0xC784, 0xC785, 0xC786,
        0xC787, 0xC788, 0xC789, 0xC78A, 0xC78B, 0xC78C, 0xC78D,
        0xC78E, 0xC78F, 0xC790, 0xC791, 0xC792, 0xC793, 0xC794,
        0xC795, 0xC796, 0xC797, 0xC798, 0xC799, 0xC79A, 0xC79B,
        0xC79F, 0xC7A0, 0xC7A1, 0xC7A2, 0xC7A3, 0xC7A4, 0xC7A5,
        0xC7A6, 0xC7A7, 0xC7A8, 0xC7A9, 0xC7AA, 0xC7AB, 0xC7AC,
        0xC7AD, 0xC7AE, 0xC7E9,
    ];

    private bool _armyMapSpeech;

    private void RequireArmyMapSpeechRecordEnd(int id, Program program, Parser parser)
    {
        //Both installed C4F8 records end in an incomplete01 J after all
        //three S3 candidates have returned. Native choice execution returns
        //to its caller there; it cannot reach or read that final marker.
        //Only this exact finite leaf/control-flow shape accepts the suffix.
        if (id == 0xC4F8 && program.Statements is [Choice])
            parser.RequireReturnedChoiceEnd();
        else parser.RequireEnd();
    }
}
