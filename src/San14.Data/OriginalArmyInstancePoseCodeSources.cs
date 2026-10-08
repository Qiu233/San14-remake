using System.Numerics;

namespace San14.Data;

public sealed record OriginalArmyInstancePoseCodeWindow(string Name, uint Rva, int Bytes, string Sha256);
public sealed record OriginalArmyInstancePoseDataWindow(string Name, uint ScRva, uint TcRva,
    int Bytes, string Sha256);
public sealed record OriginalArmyInstancePoseCodeSource(string Language, string ExecutableSha256,
    string DecodedTextSha256, uint FormationTableRva, uint RoleTableRva,
    IReadOnlyList<OriginalArmyInstancePoseCodeWindow> Windows);

/// <summary>Pinned current CE SC/TC finite native writer windows and exact
/// nineteen-row layout source. Paired instruction/data audits are retained in
/// docs/.local/army-instance-pose-leads/lead-contract.json.</summary>
public static class OriginalArmyInstancePoseCodeSources
{
    public const string FormationTableSha256 = "6C43E171DD63FCEF5F9D2AAA5E2E4289B5CC08F71D90E0AC565F0876D64CDB5D";
    public const string RoleTableSha256 = "D7805DD9B1A7D251339A3A61944E0AE2B37BAEFC959308C3AEC6F5C7A7CF1B29";
    public const int BannerRecordIndex = 7;
    public const float FlagHeightOffset = 100;
    public static Vector4 BannerScaleArgument => new(1, 1, 1, 0);
    public static Vector4 FlagScaleArgument => new(.7f, .7f, .7f, 0);
    public static IReadOnlyList<int> FlagRecordIndices { get; } = Array.AsReadOnly(new[] { 2, 11, 13 });
    public static IReadOnlyList<OriginalArmyInstancePoseDataWindow> FlagDataWindows { get; } =
        Array.AsReadOnly(new OriginalArmyInstancePoseDataWindow[]
        {
            new("BannerRecordIndex", 0x1A226BC, 0x1A216BC, 4, "E8613F5A5BC9F9FEEDA32A8E7C80B69DD4878E47B6A91723FB15EB84236B6A2B"),
            new("FlagRecordIndices", 0x1A226C0, 0x1A216C0, 12, "84426B408FA5E1FCABA2E670775D2521089C95E86BA77FFAB0625C3CB42E4CE9"),
            new("FlagHeightOffset", 0x1344C4C, 0x1343C4C, 4, "2238F30DF488055DAF5E8F362CF904E370011A172D20DF193C1B7AE8E5DB8E01"),
            new("BannerScaleArgument", 0x13979C0, 0x13969C0, 16, "66AA5265FB25A9151E9B998B2153E6A19358611FED4E7BDC7B0885EF36CA04DE"),
            new("FlagScaleArgument", 0x13A2970, 0x13A1970, 16, "86AB7B3F9680F9B805746BA208DB03325DC1C5B310EEA796CB7796D8C3ABFE97")
        });
    public static OriginalArmyInstancePoseCodeSource ForLanguage(string language) => language switch
    { "SC" => Sc, "TC" => Tc, _ => throw new InvalidDataException("Army instance pose code profile must be SC or TC.") };

    public static IReadOnlyList<Vector3> FormationRows { get; } = Array.AsReadOnly(new Vector3[]
    {
        new(-5, 2, .2f), new(-2.5f, 2, .1f), new(0, 2, 0), new(2.5f, 2, .1f), new(5, 2, .2f),
        new(-5, 0, .4f), new(-2.5f, 0, .2f), new(0, 0, 0), new(2.5f, 0, .2f), new(5, 0, .4f),
        new(-5, -2, .8f), new(-2.5f, -2, .4f), new(0, -2, .3f), new(2.5f, -2, .4f), new(5, -2, .8f),
        new(3, -2.2f, 1), new(2, -2.2f, .8f), new(-2, -2.2f, .8f), new(-3, -2.2f, 1)
    });

    /// <summary>The twentieth native role is sentinel 7 and consumes no model.
    /// Fifteen zeros select descriptor778; four ones select descriptor750.</summary>
    public static IReadOnlyList<int> RecordRoles { get; } = Array.AsReadOnly(new[]
    { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 7 });

    public static readonly OriginalArmyInstancePoseCodeSource Sc = new("SC",
        "CEEE6A94EB939B40C329B8EC5AECD938E9D3A51A6663E8E7D27A1498627AFB88",
        "D7F3831C369A76148D87370D91EF0A8A43487D5E13A383886C68A6D50591E8F6", 0x1A226D0, 0x1A22670,
        Array.AsReadOnly(new OriginalArmyInstancePoseCodeWindow[]
        {
            new("InitialRecords", 0x1C5910, 517, "9D5DEED37A590970B0C42D31CBB0E18276B48F9AF90F5B36D7AD08056F95A8E9"),
            new("WorldWriter", 0x1CCA70, 3672, "252D4F2252EA37D62A0D30C68E1B7CC302795FCF4E7B079E1DF58BBA1CF271D7"),
            new("OrdinaryWorldBranch", 0x1CD231, 362, "499713E19601E1F950010A63742B8A407DAC5C1FC9932238E63E12118751F893"),
            new("NormalizeYawQuaternion", 0x1CF8B0, 151, "B819C23E7E71842BD63F7017AF1D1D3C0FDF52C7D1A407EAC03A1D01FA556FE9"),
            new("ScalarSin", 0x197410, 308, "1D2A6A3283DF383D0F8FDFB21709688A35FCDB5FB4865FCE8AA9A835F5936C9A"),
            new("ScalarCos", 0x1C2E80, 320, "2281DC80C962E52FE51D4BB5E9CB44545F332D47F1F15A805DDF82FCA58973B7"),
            new("FlagWorldBranches", 0x1CD57E, 697, "123756ADCE62292D5F8A0895204015A05D54F51636D810DCC62042327B261104")
        }));
    public static readonly OriginalArmyInstancePoseCodeSource Tc = new("TC",
        "0751B4FED7A5783371B9541674C0587A6AFD3140E60E706E32EAF35A9432F464",
        "FFBD504FC3DEA6A218EF4E8530A65761296C17191F7D675AB3C49DF64D950766", 0x1A216D0, 0x1A21670,
        Array.AsReadOnly(new OriginalArmyInstancePoseCodeWindow[]
        {
            new("InitialRecords", 0x1C5910, 517, "E27E38DAF599989F7C161BE638982174C2EC319D4AE6F4B6035DDC6BF6B55CCF"),
            new("WorldWriter", 0x1CCA80, 3672, "A31CF338E61A61AD2A8D7A33BBC01CD770DBE99A8D94CA399821432EEAA46C1F"),
            new("OrdinaryWorldBranch", 0x1CD241, 362, "44F9EA504975EB26366F0BCA42FF43A042B9B8208FB8927322F7590645AD93C0"),
            new("NormalizeYawQuaternion", 0x1CF8C0, 151, "00C1E946441AAD87CF67437985069A50C1BC0E56FE26A6BB63B3A39BF0B1039B"),
            new("ScalarSin", 0x197420, 308, "BD659877071F1383DC061BD64A5C0CD0D67196659833A272C24D221296752FF9"),
            new("ScalarCos", 0x1C2E80, 320, "CC5305EC82A5F2097F36263ED73E2667736CA62CF2C9E294594AC70704350FEF"),
            new("FlagWorldBranches", 0x1CD58E, 697, "D6713DAD67EC50C7831E0AF02208626700EF8044BDC7CE8492993E99590C3E1A")
        }));
}
