namespace San14.Core;

/// <summary>Replaceable period outcome; original talent probabilities and timing remain open.</summary>
public readonly record struct TalentOrderOutcome(int? JoinedOfficerId, ulong RandomState);

public interface IPlayableTalentResolution
{
    TalentOrderOutcome Resolve(GameState current, PreparedTalentOrder task);
}

/// <summary>
/// Deterministic first-playable behavior. Search joins the first undiscovered person
/// at the command city; known free recruitment succeeds. It does not claim original odds.
/// </summary>
public sealed class ProvisionalTalentResolution : IPlayableTalentResolution
{
    public TalentOrderOutcome Resolve(GameState current, PreparedTalentOrder task)
    {
        int? joined = task.Kind switch
        {
            TalentOrderKind.Search => current.Officers.Values
                .Where(officer => officer.ForceId is null &&
                    officer.Presence == OfficerPresence.Undiscovered &&
                    officer.LocationId == task.CityId)
                .OrderBy(officer => officer.Id).Select(officer => (int?)officer.Id)
                .FirstOrDefault(),
            TalentOrderKind.Recruit => task.TargetOfficerId,
            _ => throw new InvalidDataException("Unknown talent task kind.")
        };
        return new(joined, current.RandomState);
    }
}
