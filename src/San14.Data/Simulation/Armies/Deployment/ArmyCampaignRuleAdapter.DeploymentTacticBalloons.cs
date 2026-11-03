using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>One reader over the accepted choice query's existing current.
    /// Body and disabled reason never capture a World or change its indexes.</summary>
    private sealed class DeploymentTacticBalloonCurrent(ArmyCampaignRuleAdapter rules,
        GameState state, SourceWorldContext current, int selectedFormation, int selectedCount)
    {
        private readonly Dictionary<int, TacticBalloonContent> bodies = [];
        private readonly Dictionary<int, string> warnings = [];
        private NarrationResourceMessages? messages;

        internal AreaAdministrationQuery<DeploymentTacticBalloon> Read(DeploymentTacticChoice choice)
        {
            try
            {
                // The producer resolves the real hovered control ID through
                // the current table, independently of its Compatible display.
                var tactic = current.ReadSkillTacticReference(choice.TacticId)
                    ?? throw Missing("tactic_pointer");
                if (!Need(current.ReadSkillTacticValidity(tactic), "tactic_validity"))
                    throw Missing("invalid_tactic_receiver");
                int id = Need(tactic.PhysicalSlot, "tactic_identity");
                if (!bodies.TryGetValue(id, out var body))
                {
                    byte category = TacticParameterQueries.ReadTargetMode(tactic, current);
                    byte flags = Need(current.ReadSkillTacticFlags(tactic), "target_flags");
                    ushort waiting = (ushort)(Need(current.ReadSkillTacticRawByte(tactic, 0x42), "waiting_low") |
                        Need(current.ReadSkillTacticRawByte(tactic, 0x43), "waiting_high") << 8);
                    body = TacticBalloonContentSource.Create(category, flags, waiting,
                        current.ReadTacticEffectOpcode(tactic, 0), current.ReadTacticEffectOpcode(tactic, 1),
                        rules.ReadHistoryDataText);
                    bodies.Add(id, body);
                }
                int? reason = choice.Enabled ? null : DisabledReason(tactic, id);
                string? warning = null;
                if (reason is int message)
                {
                    if (!warnings.TryGetValue(message, out warning))
                    {
                        messages ??= new(rules._repository.LoadMessages(rules.Seed.Language, MessageKind.Game).Messages);
                        warning = messages.ResolveDeploymentTacticDisabledReason(message);
                        warnings.Add(message, warning);
                    }
                }
                return AreaAdministrationQuery<DeploymentTacticBalloon>.Known(new(body, warning));
            }
            catch (InvalidDataException error)
            { return AreaAdministrationQuery<DeploymentTacticBalloon>.Unknown(error.Message); }
        }

        private int? DisabledReason(TacticReference tactic, int tacticId)
        {
            var formation = current.ReadStaticTableReference(0x77428, selectedFormation)
                ?? throw Missing("selected_formation_pointer");
            // The complete native definition table rejects receiver0. A known
            // null/invalid formation skips the entire warning path, including
            // the selected-count tail; absent lookup remains unknown.
            if (formation.PhysicalSlot is not int id || id == 0) return null;
            uint mask = 0;
            for (int part = 0; part < 4; part++)
                mask |= (uint)Need(current.ReadSkillTacticRawByte(tactic, 0x6C + part), "formation_mask") << (part * 8);
            if ((uint)id > 20 || (mask & (1u << (id & 31))) == 0)
            {
                // These source branches return before count/formation7.
                if (tacticId == 21) return 0x568B;
                if (tacticId == 22) return 0x568C;
                if (tacticId == 23) return 0x568D;
                if (unchecked((uint)(id - 8)) < 3) return 0x568E;
                // Only actual allowed Formation receivers consume category7E.
                // This finite21-slot scan is not a gallery catalog or World scan.
                for (int slot = 0; slot < 21; slot++)
                {
                    var entry = current.ReadStaticTableReference(0x77428, slot)
                        ?? throw Missing("allowed_formation_pointer");
                    int allowed = Need(entry.PhysicalSlot, "allowed_formation_receiver");
                    if ((uint)allowed > 20 || (mask & (1u << (allowed & 31))) == 0) continue;
                    if (Need(TacticRemakeCurrentFormations.ReadRawByte(state, allowed, 0x7E),
                        "allowed_formation_category") == 2) return 0x5688;
                }
            }
            if (selectedCount >= 3) return 0x5689;
            return id == 7 ? 0x568A : null;
        }

        private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("deployment.tactic_balloon." + field);
    }
}
