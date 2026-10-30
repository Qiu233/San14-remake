using System.Buffers.Binary;
using System.Globalization;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class CollectionOfficerCatalog
{
    // The gallery uses its title-loaded definition, not campaign modifiers or a
    // synthetic Army. The skill loader maps source49/51/80/88 to42/44/62/6A.
    private CollectionTacticBalloon ReadTacticBalloon(S14TacticDefinition tactic)
    {
        var raw = tactic.RawBytes;
        if (raw.Length != 94 || raw[88] > 2)
            throw new InvalidDataException("collection.officer.tactic_balloon_definition");
        var effects = new List<CollectionTacticBalloonEffect>(2);
        for (int slot = 0; slot < 2; slot++)
        {
            int opcode = raw[80 + slot];
            if (opcode is < 1 or > 23) continue;
            var (sprite, name) = opcode switch
            {
                1 => (0x1EB, "000_cmn_icon_st_damage"),
                2 => (0x1EF, "000_cmn_icon_st_konran"),
                3 => (0x1E9, "000_cmn_icon_st_asidome"),
                4 => (0x1EA, "000_cmn_icon_st_chohatu"),
                5 or 10 => (0x15D, "000_cmn_icon_bt_kidou"),
                6 or 11 => (0x15F, "000_cmn_icon_bt_kougun"),
                7 or 12 => (0x161, "000_cmn_icon_bt_koujyou"),
                8 or 13 => (0x157, "000_cmn_icon_bt_bougyo"),
                9 or 14 => (0x15A, "000_cmn_icon_bt_hajyou"),
                15 or 16 => (0x1E1, "000_cmn_icon_siki"),
                17 => (0x1EC, "000_cmn_icon_st_firing"),
                18 => (0x1F1, "000_cmn_icon_taikyu_damage"),
                19 => (0x1D7, "000_cmn_icon_recovery"),
                20 => (0x19D, "000_cmn_icon_heishi_recovery"),
                21 or 22 => (0x147, "000_cmn_icon_all_abilities"),
                23 => (0x1ED, "000_cmn_icon_st_grant"),
                _ => throw new InvalidDataException("collection.officer.tactic_balloon_opcode")
            };
            bool beneficial = opcode is >= 5 and <= 9 or 15 or >= 19 and <= 21;
            effects.Add(new(slot, opcode, Data(0, 0x151 + opcode), sprite, name,
                beneficial ? 11 : 12, beneficial ? 15 : 16));
        }
        // The source uses30C000's integer formatter: its remainder argument is
        // unused. Four ordinary waiting updates make one day in both locales.
        int waiting = BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(49, 2));
        return new(raw[88] == 0 ? "" : Data(1, raw[88] == 1 ? 0x4B1 : 0x4B2),
            Data(1, 0x4B4), Data(1, (raw[51] & 1) != 0 ? 0x338 : 0x339),
            Data(1, 0x6C5), (waiting / TacticEffectSourceWaiting.OrdinaryUpdatesPerDay)
                .ToString(CultureInfo.InvariantCulture), effects.AsReadOnly());
    }
}
