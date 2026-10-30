namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class DefinitionInformationCurrent
    {
        private int[] TacticCandidates()
        {
            var ids = new List<int>();
            for (int slot = 0; slot < 201; slot++)
            {
                var reference = current.ReadSkillTacticReference(slot) ?? throw Missing("tactic.pointer");
                if (!Need(current.ReadSkillTacticValidity(reference), "tactic.validity")) continue;
                int id = Need(reference.PhysicalSlot, "tactic.identity");
                if (TacticCost(id) == 0 || (unchecked((uint)(id - 108)) & 0xFFFFFFF9u) == 0) continue;
                var eligible = rules.ReadSpecialPersonTacticEligibilityCurrent(current, id);
                if (eligible.Allowed is not true)
                {
                    if (eligible.Allowed is null) candidateIssues.Add(eligible.UnresolvedOwner ?? "tactic.special_person_unlock");
                    continue;
                }
                ids.Add(id);
            }
            return ids.ToArray();
        }
        private byte TacticByte(int id, int offset) => Need(current.ReadSkillTacticRawByte(new(id), offset), "tactic.byte."+offset.ToString("X"));
        private int TacticCost(int id) => unchecked((short)(TacticByte(id, 0x42) | TacticByte(id, 0x43) << 8));
        private int TacticOpcode(int id, int pair) => current.ReadTacticEffectOpcode(new(id), pair);
        private string TacticEffectName(int id, int pair)
        {
            int code = TacticOpcode(id, pair);
            return code is >= 1 and <= 23 ? Data(0, 0x151 + code) : "";
        }
        private int TacticRange(int id) => TacticParameterQueries.ReadTargetMode(new(id), current);
        private string TacticRangeText(int id) => TacticRange(id) switch { 0 => "", 1 => Data(1,0x239), 2 => Data(1,0x23A), var value => Number(value) };
        private int TacticMagnitude(int id, int pair)
        {
            int code = TacticOpcode(id, pair);
            return code switch
            {
                1 or 15 or 16 or 18 or 20 => current.ReadTacticEffectValue(new(id), pair),
                >= 5 and <= 14 or 21 or 22 => current.ReadTacticRaw68(new(id), pair),
                _ => 0
            };
        }
        private int TacticDuration(int id, int pair)
        {
            int code = TacticOpcode(id, pair);
            return code is >= 2 and <= 14 or 17 or 21 or 22 or 23
                ? current.ReadTacticEffectValue(new(id), pair) / 4 : 0;
        }
        private int TacticRawDuration(int id, int pair) => current.ReadTacticRaw64(new(id), pair);
        private string TacticCategory(int id)
        {
            int category = TacticByte(id, 0x40);
            return category is >= 1 and <= 9 ? Data(0, 0x147 + category) : "";
        }
        private PreparedCell TacticCell(int id, int column)
        {
            if (column == 0) return MakeCell(() => state.Tactics.GetValueOrDefault(id)?.Name ?? throw Missing("tactic.name"), () => new NumberKey(id));
            if (column == 1) return MakeCell(() => TacticCategory(id), () => new NumberKey(TacticByte(id, 0x40)));
            if (column == 2) return MakeCell(() => TacticRangeText(id), () => new NumberKey(TacticRange(id), TacticRange(id) == 0));
            if (column == 3) return MakeCell(() => Data(1, (Need(current.ReadSkillTacticFlags(new(id)), "tactic.flags") & 1) != 0 ? 0x338 : 0x339),
                () => new NumberKey(Need(current.ReadSkillTacticFlags(new(id)), "tactic.flags") & 1));
            if (column is 4 or 8 or 12 or 13)
            {
                int pair = column is 8 or 13 ? 1 : 0;
                return MakeCell(() => TacticEffectName(id, pair), () => new NumberKey(TacticOpcode(id, pair)));
            }
            if (column is 5 or 9)
            {
                int pair = column == 9 ? 1 : 0;
                return MakeCell(() => TacticMagnitude(id, pair) is int value && value != 0 ? Number(value) : Data(1, 0x353),
                    () => new NumberKey(TacticMagnitude(id, pair), TacticMagnitude(id, pair) == 0));
            }
            if (column is 6 or 10)
            {
                int pair = column == 10 ? 1 : 0;
                return MakeCell(() => TacticDuration(id, pair) is int value && value != 0
                    ? Data(1, 0x324).Replace("%d", Number(value), StringComparison.Ordinal) : Data(1,0x353),
                    () => new NumberKey(TacticDuration(id,pair), TacticDuration(id,pair) == 0));
            }
            if (column is 7 or 11)
            {
                int pair = column == 11 ? 1 : 0;
                return MakeCell(() => TacticRawDuration(id,pair) is int value && value != 0
                    ? Data(1,0x48B).Replace("%d",Number(value),StringComparison.Ordinal) : Data(1,0x353),
                    () => new NumberKey(TacticRawDuration(id,pair),TacticRawDuration(id,pair) == 0));
            }
            throw Missing("tactic.column."+column);
        }
    }
}
