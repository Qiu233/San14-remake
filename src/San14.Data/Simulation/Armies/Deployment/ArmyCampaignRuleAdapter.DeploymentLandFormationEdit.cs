using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Source radio edit of one unsubmitted right-pane selection.
    /// Missing reached current input rejects the entire replacement.</summary>
    public DeploymentLandFormationEditResult QueryDeploymentLandFormationEdit(GameState state,
        int officerId, DeploymentLandFormationEditState previous, int newLandFormationId)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(previous.Tactics); ArgumentNullException.ThrowIfNull(previous.BackupTactics);
        try
        {
            if ((uint)(newLandFormationId - 1) >= 20 || (uint)previous.LandFormationId >= 21)
                throw FormationEditMissing("formation_identity");
            if (!state.Officers.ContainsKey(officerId)) throw FormationEditMissing("current_person_owner");
            var parameters = state.NativeArmySimulation?.Parameters;
            if (parameters is null || parameters.Language != Seed.Language ||
                parameters.RuleVersion != SimulationRuleIdentity.ForLocale(Seed.Language))
                throw FormationEditMissing("rule_profile");
            var current = CaptureOfficerCommandCapacityCurrent(state);
            DeploymentTacticCurrentInputs.BindCurrent(state, current);
            var tactics = previous.Tactics; var backup = previous.BackupTactics;
            bool active = previous.BackupActive; int transition = 0;
            if (previous.LandFormationId != newLandFormationId)
            {
                bool oldSpecial = InSpecialClass(previous.LandFormationId), newSpecial = InSpecialClass(newLandFormationId);
                if (newSpecial)
                {
                    if (!oldSpecial) transition = 1;
                    tactics = Cover(tactics);
                }
                else if (oldSpecial)
                {
                    transition = 2;
                    if (!active) { tactics = Cover(tactics); backup = tactics; }
                }
                else backup = tactics;
            }
            tactics = DeploymentTacticSelectionSource.FilterAndNormalize(current, newLandFormationId, tactics);
            if (transition == 1) active = true;
            else if (transition == 2)
            {
                if (active)
                {
                    if (backup.ToArray().Any(id => id < 0)) throw FormationEditMissing("negative_backup_tactic_index");
                    tactics = backup;
                }
                active = false;
            }
            return new(new(newLandFormationId, tactics, backup, active), null);

            DeploymentTacticSlots Cover(DeploymentTacticSlots old)
            {
                int physical = _definitions.ResolveOfficerSource(officerId) ?? throw FormationEditMissing("person_identity");
                if (current.ReadPersonIdentity(physical) != officerId) throw FormationEditMissing("current_person_identity");
                return DeploymentTacticSelectionSource.CoverCandidates(current, officerId, physical,
                    newLandFormationId, old, () =>
                        new TacticRemakeCurrentAbilities(current, _scenario, Seed.Language, parameters.RuleVersion)
                            .Resolve(state, officerId) ?? throw FormationEditMissing("effective_abilities"));
            }
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    private static bool InSpecialClass(int formation) => unchecked((uint)(formation - 7)) < 4;
    private static InvalidDataException FormationEditMissing(string field) => new("deployment.land_edit." + field);
}
