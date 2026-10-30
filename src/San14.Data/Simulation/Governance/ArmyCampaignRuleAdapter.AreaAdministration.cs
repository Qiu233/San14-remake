using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IAreaAdministrationExecution
{
    public CommandResult Execute(GameState state, CityState city,
        IReadOnlyList<AreaAdministrationChange> changes, long reservedCommandBooks, CommandContext context)
        => ExecuteAreaAdministrationCurrent(state, city, changes, reservedCommandBooks, ordered: false);

    /// <summary>Formal ordered appointment command. The command rule has
    /// authorized every target's force and district, retaining repeated rows.
    /// Uses the ordinary candidate eligibility and assignment writer.</summary>
    public CommandResult ExecuteOrdered(GameState state,
        IReadOnlyList<AreaAdministrationChange> changes, long reservedCommandBooks, CommandContext context)
        => ExecuteAreaAdministrationCurrent(state, null, changes, reservedCommandBooks, ordered: true);

    private CommandResult ExecuteAreaAdministrationCurrent(GameState state, CityState? city,
        IReadOnlyList<AreaAdministrationChange> changes, long reservedCommandBooks, bool ordered)
    {
        try
        {
            var indexes = state.ReadArmyLifecycle()?.Indexes ?? throw new InvalidDataException("area-administration.saved_indexes");
            var current = OwnedArmyReturnCurrentFields.CaptureCurrent(state, _definitions);
            _ = WorldIndexContext.ImportOwnedState(current, indexes, state);
            _personAssignmentPrecallInputs.BindCurrent(state, current);
            current.CurrentPlayerForce = OwnedCurrentPlayerForce.ReadCurrentReference(state, current);
            var incoming = changes.Where(change => change.OverseerId.HasValue &&
                (ordered || current.ReadAssignmentArea36(change.AreaId) != change.OverseerId.Value)).ToArray();
            if (incoming.Length != 0)
            {
                var candidatesByCity = new Dictionary<int, HashSet<int>>();
                foreach (var change in incoming)
                {
                    int targetCity = ordered ? (state.Areas.GetValueOrDefault(change.AreaId)?.CityId
                        ?? throw new InvalidDataException("area-administration.ordered_area_city")) : city!.Id;
                    if (!candidatesByCity.TryGetValue(targetCity, out var candidates))
                        candidatesByCity[targetCity] = candidates = AreaAdministrationExecution.Candidates(current, targetCity);
                    if (change.OverseerId is int id && !candidates.Contains(id))
                        return CommandResult.Reject("Officer is not a current regional administration candidate.", "administration.ineligible");
                }
            }
            var effects = new List<CampaignEffectRequest>();
            GameState Report(GameState candidate, int person, PersonAssignmentPrecallResult assignment) =>
                ApplyAreaAdministrationAssociationReports(candidate, current, person, assignment, effects);
            var result = ordered ? AreaAdministrationExecution.ApplyOrdered(state, current, changes, reservedCommandBooks,
                    ReadApplicationGameplay()?.UserSelectionServicePresent, Report)
                : AreaAdministrationExecution.Apply(state, current, city!, changes, reservedCommandBooks,
                    ReadApplicationGameplay()?.UserSelectionServicePresent, Report);
            if (!result.Accepted || effects.Count == 0) return result;
            return result with { State = PublishAreaAdministrationAssociationNotifications(result.State
                ?? throw new InvalidDataException("area-administration.accepted_state"), effects) };
        }
        catch (InvalidDataException unknown)
        { return CommandResult.RuleUnavailable(unknown.Message, "Current regional administration input is unavailable."); }
    }
}
