using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>79CF80's two effect lines for the reached selected/hovered
    /// physical tactic. The page supplies its existing current capture.</summary>
    internal IReadOnlyList<string> ReadPersonnelLearnEffectsCurrent(GameState state,
        SourceWorldContext current, int tacticId)
    {
        var tactic = new TacticReference(tacticId);
        if (current.ReadSkillTacticValidity(tactic) is not bool valid)
            throw new InvalidDataException("personnel.learn.effects.Tactic_validity");
        if (!valid) throw new InvalidDataException("personnel.learn.effects.invalid_Tactic");
        var lines = new string[2];
        for (int index = 0; index < lines.Length; index++)
        {
            //The existing query preserves both the encoded vector and raw
            //62/63 fallback.8A2BC0 maps1..23 to Data0[opcode+151].
            byte effect = current.ReadTacticEffectOpcode(tactic, index);
            lines[index] = effect is >= 1 and <= 23 ? ReadHistoryDataText(0, effect + 0x151) : "";
        }
        return Array.AsReadOnly(lines);
    }
}
