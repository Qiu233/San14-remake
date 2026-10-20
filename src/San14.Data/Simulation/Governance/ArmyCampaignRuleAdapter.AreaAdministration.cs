using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IAreaAdministrationExecution
{
    public CommandResult Execute(GameState state, CityState city,
        IReadOnlyList<AreaAdministrationChange> changes, long reservedCommandBooks, CommandContext context)
    {
        try
        {
            var indexes = state.ReadArmyLifecycle()?.Indexes ?? throw new InvalidDataException("area-administration.saved_indexes");
            var current = OwnedArmyReturnCurrentFields.CaptureCurrent(state, _definitions);
            _ = WorldIndexContext.ImportOwnedState(current, indexes, state);
            _personAssignmentPrecallInputs.BindCurrent(state, current);
            current.CurrentPlayerForce = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
            var incoming = changes.Where(change => change.OverseerId.HasValue &&
                current.ReadAssignmentArea36(change.AreaId) != change.OverseerId.Value).ToArray();
            if (incoming.Length != 0)
            {
                var candidates = AreaAdministrationExecution.Candidates(current, city.Id);
                foreach (var change in incoming)
                    if (change.OverseerId is int id && !candidates.Contains(id))
                        return CommandResult.Reject("Officer is not a current regional administration candidate.", "administration.ineligible");
            }
            return AreaAdministrationExecution.Apply(state, current, city, changes, reservedCommandBooks);
        }
        catch (InvalidDataException unknown)
        { return CommandResult.RuleUnavailable(unknown.Message, "Current regional administration input is unavailable."); }
    }
}
