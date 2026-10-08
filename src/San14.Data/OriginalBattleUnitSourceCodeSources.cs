namespace San14.Data;

public sealed record OriginalBattleUnitSourceWindow(string Name, uint StartRva,
    uint EndRvaExclusive, string Sha256);

public sealed record OriginalBattleUnitSourceProfile(string Language,
    string ExecutableSha256, string DecodedTextSha256,
    IReadOnlyList<OriginalBattleUnitSourceWindow> Windows);

/// <summary>Hash-pinned complete instruction windows measured by the bounded
/// paired audit. These metadata do not load or execute original machine code.</summary>
public static class OriginalBattleUnitSourceCodeSources
{
    public static OriginalBattleUnitSourceProfile ForLanguage(string language) => language switch
    {
        "SC" => new("SC",
            "CEEE6A94EB939B40C329B8EC5AECD938E9D3A51A6663E8E7D27A1498627AFB88",
            "D7F3831C369A76148D87370D91EF0A8A43487D5E13A383886C68A6D50591E8F6",
            Array.AsReadOnly<OriginalBattleUnitSourceWindow>(
            [
                new("common", 0x002A8FD0, 0x002A96B3,
                    "5D709EBA04A0536B9DBC12680F1072235F0A8A3942B781B493B0162F79F3C898"),
                new("fieldAttack", 0x002B23B0, 0x002B2644,
                    "587A2CED4A05B69D1B1A3E4A788D22BBCB2AF7CD112D3BB65857A20388BDBA38"),
                new("siegeTroopAttack", 0x002B2650, 0x002B2895,
                    "53DCA7FAC6D748F49B32624DDD53366E36179E7C66008632CC3D9CF953CD1D55"),
                new("defense", 0x002AA0B0, 0x002AA3AF,
                    "E908C9A839A2A15435564B49D0968E10B4AB8228D63B10F4CA81D55C8A370CD8"),
                new("wallAttack", 0x002AA540, 0x002AA7C3,
                    "90DD5113CC0BCA0E3982A3A7592B0CEC512C503B454FA7B627E621A7ACD22D97"),
                new("fieldCoefficient", 0x002B2930, 0x002B29B4,
                    "DCBAB14F279B87B525A14E68AFD2DF709B577D0A97F66D2D07A464FED38FB341"),
                new("siegeTroopCoefficient", 0x002B28A0, 0x002B2924,
                    "80D943930B12B8AB195A10729150F11D4263F5150B9AA9890969E0D854578D93"),
                new("defenseCoefficient", 0x002AA020, 0x002AA0A4,
                    "096C70BD4C05B713EB6AFB832FDE4A302CB549AB4B7041F036CA87F50BDAD4B2"),
                new("wallCoefficient", 0x002B29C0, 0x002B2A44,
                    "4CB34CF1A6BEB67FC3D2D5EB393CACA83EAED1C22ACDBE5C2FC7039C60B3DBFC"),
                new("wallLeadership", 0x0022E610, 0x0022E67B,
                    "298960CA78C08AEBF86164794A055CB68BB14F7369710A26ED66CC4E0EEBDAEA"),
                new("sqrt", 0x010228F0, 0x01022907,
                    "D699161F4123A8A7C8D6D0B767F0039E716829DB8FA29E0619BFA5DFA0B894FB"),
                new("formationFixedPrefix", 0x0023DB89, 0x0023DE95,
                    "ABD018A7FFEB7AFE5C154A85BEE062D568074AFD3B7C9D39FAD8FEF9D6FCBD36"),
                new("formationCoefficientProjection", 0x0023DE95, 0x0023E399,
                    "8CC7E1A9A846D0B972DA4F34FA8786ED72D0DF13FB2C42E18898A62CEB52DCE7"),
            ])),
        "TC" => new("TC",
            "0751B4FED7A5783371B9541674C0587A6AFD3140E60E706E32EAF35A9432F464",
            "FFBD504FC3DEA6A218EF4E8530A65761296C17191F7D675AB3C49DF64D950766",
            Array.AsReadOnly<OriginalBattleUnitSourceWindow>(
            [
                new("common", 0x002A9180, 0x002A9863,
                    "5DCA13B36D70C66E98FB9A60A7988DE625FEFC5294CE52AF69D6EC0E72CA54C2"),
                new("fieldAttack", 0x002B2570, 0x002B2804,
                    "C7222E23A0FD4964576BCAD89E35ACAD85CD6B3B03CAA8C67636D24E0A4CE531"),
                new("siegeTroopAttack", 0x002B2810, 0x002B2A55,
                    "7DE600D9A80F59F9B364761AD3073346326A58C63F71A394F4B7DA275C33C204"),
                new("defense", 0x002AA290, 0x002AA58F,
                    "A5F5C82FCE9559B71977ABB86C9563A7BEF1B66C7163F0154472FC3303CD5EA6"),
                new("wallAttack", 0x002AA720, 0x002AA9A3,
                    "A7F86AB8E2F37A1DADC710599D0C141E9B786FB04141F0EC841587672E34AA87"),
                new("fieldCoefficient", 0x002B2AF0, 0x002B2B74,
                    "5EB0A8B9FDFE288F3F6283F3F371E72A4363CBA1B5DC6A26CE4D9D2E5B38464D"),
                new("siegeTroopCoefficient", 0x002B2A60, 0x002B2AE4,
                    "71C1D5F7546B3476F1DD88ACBA69F78D7FEF8F654DDA0E512BC62D54150134F4"),
                new("defenseCoefficient", 0x002AA200, 0x002AA284,
                    "577772ABB711CE8785CF780753D9AFDEC9BE5736A75C95CBD840CE01A57DF512"),
                new("wallCoefficient", 0x002B2B80, 0x002B2C04,
                    "D5B80BB03F6FAEB90D9CFC74880FA62BF7CC69C33D0D7E7DFC05ACF6574F9643"),
                new("wallLeadership", 0x0022E390, 0x0022E3FB,
                    "90A7E1E0A6BDDD1F072D46D4F6BCDDED296C7835E8D8334977B62DA9177A69F4"),
                new("sqrt", 0x010219F0, 0x01021A07,
                    "D699161F4123A8A7C8D6D0B767F0039E716829DB8FA29E0619BFA5DFA0B894FB"),
                new("formationFixedPrefix", 0x0023D909, 0x0023DC15,
                    "DD2488505F0C6FAC5C9605377E5CD237034943FAB32D8514CB950FBAC22B0FED"),
                new("formationCoefficientProjection", 0x0023DC15, 0x0023E119,
                    "E6F297F79C0DE06F1294616E5A24BEB356FC3C6ACB2F3503017DEC2C27228282"),
            ])),
        _ => throw new ArgumentOutOfRangeException(nameof(language), "Only inspected CE SC/TC profiles are supported.")
    };
}
