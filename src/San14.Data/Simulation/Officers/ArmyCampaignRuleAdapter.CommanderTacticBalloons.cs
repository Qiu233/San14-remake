namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Uses the HUD query's current definitions. The compact source
    /// keeps only opcodes1..23 in their physical order and joins the actual
    /// description prefix with the shared effect name; it has no range, cost,
    /// magnitude or full-balloon icon rows.</summary>
    private CommanderTacticBalloon ReadCommanderTacticBalloonCurrent(SourceWorldContext current,
        int tacticId, int? reasonMessageId)
    {
        var tactic = current.ReadSkillTacticReference(tacticId)
            ?? throw CommanderHudMissing("balloon.tactic_pointer");
        if (!CommanderHudNeed(current.ReadSkillTacticValidity(tactic), "balloon.tactic_validity"))
            throw CommanderHudMissing("balloon.invalid_tactic");
        var effects = new List<CommanderTacticBalloonEffect>(2);
        for (int slot = 0; slot < 2; slot++)
        {
            int opcode = current.ReadTacticEffectOpcode(tactic, slot);
            if (opcode is < 1 or > 23) continue;
            var description = CommanderTacticBalloonDescriptions.Format(opcode, ReadHistoryDataText);
            effects.Add(new(slot, opcode, description.Text, description.TextPreset));
        }
        return new(effects.AsReadOnly(), reasonMessageId);
    }
}
