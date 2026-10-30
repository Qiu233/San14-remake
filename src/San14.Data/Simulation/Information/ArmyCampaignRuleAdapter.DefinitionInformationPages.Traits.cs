using San14.Data.Formats;
using System.Text;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class DefinitionInformationCurrent
    {
        private int[] TraitCandidates()
        {
            var ids = new List<int>();
            for (int slot = 0; slot < 356; slot++)
            {
                var reference = current.ReadPersonalityTableReference(slot) ?? throw Missing("trait.pointer");
                if (!Need(current.ReadSkillPersonalityValidity(reference), "trait.validity")) continue;
                int id = Need(reference.PhysicalSlot, "trait.identity");
                if (Need(current.ReadPersonalityByte(reference, 0xBE), "trait.type") == 0) continue;
                var eligible = rules.ReadSpecialPersonPersonalityEligibilityCurrent(current, id);
                if (eligible.Allowed is not true)
                {
                    if (eligible.Allowed is null) candidateIssues.Add(eligible.UnresolvedOwner ?? "trait.special_person_unlock");
                    continue;
                }
                ids.Add(id);
            }
            //The ordinary initializer partitions the original table order by
            //its actual personality-type priority, retaining same-type order.
            return ids.OrderBy(id => TraitPriority(TraitByte(id, 0xBE))).ToArray();
        }
        private static int TraitPriority(int type) => type switch { 1 => 2, 2 => 4, 3 => 1, 4 => 3, 5 => 5, _ => 0 };
        private byte TraitByte(int id, int offset) => Need(current.ReadPersonalityByte(new(id), offset), "trait.byte."+offset.ToString("X"));
        private ushort TraitWord(int id, int offset) => Need(current.ReadPersonalityWord(new(id), offset), "trait.word."+offset.ToString("X"));
        private static bool CreatedTrait(int id) => id is >= 256 and <= 355;
        private string TraitText(int id, int offset, int units)
        {
            var result = new StringBuilder(units);
            for (int index = 0; index < units; index++)
            {
                ushort code = TraitWord(id, offset + index * 2);
                if (code == 0) return result.ToString();
                result.Append((char)code);
            }
            return result.ToString();
        }
        private San14.Data.Formats.MessageDocument? traitInformationMessages;
        private string TraitDescription(int id) => CreatedTrait(id)
            ? AreaAdministrationOfficerTraitTooltip.InformationListDescription(new(id), current,
                traitInformationMessages ??= rules._repository.LoadMessages(rules.Seed.Language, MessageKind.Data).Messages)
            : TraitText(id, 0x3C, 51);
        private string TraitCondition(int id) => CreatedTrait(id) ? Data(0, 0x16CC + TraitByte(id, 0xA4)) : Data(1, 0x353);
        private string TraitOption(int id)
        {
            if (!CreatedTrait(id)) return Data(1, 0x353);
            int condition = TraitByte(id, 0xA4), option = TraitByte(id, 0xAC);
            return condition is not (9 or 10) && option is 1 or 37 ? Data(1, 0x81B) : Data(0, 0x16CC + option);
        }
        private int TraitAmount(int id, int pair) => Math.Clamp((int)unchecked((short)TraitWord(id, pair == 0 ? 0xB8 : 0xBC)), -100, 2000);
        private int TraitEffect(int id, int pair) => TraitByte(id, pair == 0 ? 0xB6 : 0xBA);
        private string TraitAmountText(int id, int pair) => CreatedTrait(id)
            ? AreaAdministrationOfficerTraitTooltip.Number(TraitEffect(id, pair), TraitAmount(id, pair), Data(1, 0x353)) : Data(1, 0x353);
        private SortKey TraitAmountKey(int id, int pair)
        {
            if (!CreatedTrait(id)) return new NumberKey(0, true);
            if (TraitAmountText(id, pair) == Data(1, 0x353)) return new NumberKey(-100);
            int amount = TraitAmount(id, pair);
            return new NumberKey(TraitEffect(id, pair) == 98 ? -amount : amount);
        }
        private PreparedCell TraitCell(int id, int column) => column switch
        {
            0 => MakeCell(() => current.ReadPersonalityName(new(id)), () => new TraitNameKey(TraitText(id, 0x1C, 16), current.ReadPersonalityName(new(id)))),
            1 => MakeCell(() => TraitDescription(id)),
            2 => MakeCell(() => CreatedTrait(id) ? Data(1, 0x338) : "", () => new NumberKey(CreatedTrait(id) ? 1 : 0)),
            3 => MakeCell(() => TraitCondition(id), () => new NumberKey(CreatedTrait(id) ? TraitByte(id, 0xA4) : 0, !CreatedTrait(id))),
            4 => MakeCell(() => TraitOption(id)),
            5 => MakeCell(() => CreatedTrait(id) ? Data(0, 0x164D + TraitEffect(id, 0)) : Data(1, 0x353), () => new NumberKey(CreatedTrait(id) ? TraitEffect(id, 0) : 0, !CreatedTrait(id))),
            6 => MakeCell(() => TraitAmountText(id, 0), () => TraitAmountKey(id, 0)),
            7 => MakeCell(() => CreatedTrait(id) ? Data(0, 0x164D + TraitEffect(id, 1)) : Data(1, 0x353), () => new NumberKey(CreatedTrait(id) ? TraitEffect(id, 1) : 0, !CreatedTrait(id))),
            8 => MakeCell(() => TraitAmountText(id, 1), () => TraitAmountKey(id, 1)),
            _ => throw Missing("trait.column."+column)
        };
        private sealed record TraitNameKey(string Pinyin, string Name) : SortKey
        {
            internal override bool Before(SortKey other, bool ascending)
            {
                var right = (TraitNameKey)other;
                bool before = Pinyin.Length == 0 && right.Pinyin.Length == 0
                    ? string.CompareOrdinal(Name, right.Name) < 0
                    : Pinyin.Length == 0 ? false : right.Pinyin.Length == 0 || string.CompareOrdinal(Pinyin, right.Pinyin) < 0;
                //The authored reverse callback is the logical complement,
                //including ties; the shared source sorter consumes it as is.
                return ascending ? before : !before;
            }
        }
    }
}
