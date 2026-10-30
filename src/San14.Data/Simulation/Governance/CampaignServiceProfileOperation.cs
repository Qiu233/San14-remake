using San14.Core;

namespace San14.Data;

//One operation borrows the real profile and preserves monotone local writes.
internal sealed class CampaignServiceProfileOperation(TransferServiceState profile)
{
    private TransferServiceState _profile = profile.Copy();
    public TransferServiceState Current => _profile.Copy();
    private readonly Dictionary<int, CampaignAchievementClockRead> _clockReads = [];
    internal void BeginOperation(CampaignServiceChanges saved)
    { _clockReads.Clear(); foreach (var read in saved.ClockReads) _clockReads.Add(read.Id, read); }
    internal CampaignServiceChanges PreserveClockReads(CampaignServiceChanges changes) => changes with
        { ClockReads = _clockReads.Values.OrderBy(c => c.Id).ToArray() };
    internal TransferServiceState Materialize(CampaignServiceChanges changes)
    {
        var copy = _profile.Copy();
        copy.AchievementClockSnapshotProvider = index =>
        {
            if (_clockReads.TryGetValue(index, out var old)) return new(old.YearsSince1900, old.ZeroBasedMonth, old.Day, old.Hour);
            var read = _profile.CaptureClockSnapshotForFlag(index);
            if (read is { YearsSince1900: int year, ZeroBasedMonth: int month, Day: int day, Hour: int hour })
                _clockReads.Add(index, new(index, year, month, day, hour));
            return read;
        };
        foreach (int id in changes.Events)
        {
            if (copy.ReadEventFlag(id) is null) throw new InvalidDataException("Current global service event owner is unknown.");
            copy.SetEventFlag(id, true);
        }
        foreach (var write in changes.Achievements)
        {
            if (copy.ReadPrimaryFlag(write.Id) is not bool old) throw new InvalidDataException("Current global achievement owner is unknown.");
            if (old) continue;
            if (copy.PlatformPolicy != TransferServicePlatformPolicy.Absent) throw new InvalidDataException("Pending achievements require the explicit local platform policy.");
            copy.SetPrimaryFlag(write.Id, true);
            var d = write.Date; copy.SetDate(write.Id, new(d.Year, d.Month, d.Day, d.Hour, d.Reserved));
        }
        //A reached source writer may establish true from unknown scenario
        //knowledge. Other bits remain unknown; an old campaign cannot clear them.
        foreach (int id in changes.CompletedScenarios) copy.SetScenarioCompleted(id, true);
        foreach (int id in changes.UnlockedBonusOfficerIndices) copy.SetBonusOfficerUnlocked(id, true);
        foreach (int id in changes.CompletedTutorialLessons) copy.SetTutorialCompleted(id, true);
        foreach (int id in changes.CompletedEndingTracks)
        {
            uint old = copy.ReadEndingTrack(id)
                ?? throw new InvalidDataException("Current Ending completion track is unknown.");
            if (old == 0) copy.SetEndingTrack(id, 1);
        }
        if (changes.Dirty) copy.WriteDirtyFlag(1);
        return copy;
    }
    internal CampaignServiceChanges Capture(TransferServiceState candidate)
    {
        var events = new List<int>(); var achievements = new List<CampaignAchievementWrite>();
        var scenarios = new List<int>(); var tracks = new List<int>(); var bonusOfficers = new List<int>();
        var tutorialLessons = new List<int>();
        for (int i = 0; i < 1024; i++)
            if (_profile.ReadEventFlag(i) != candidate.ReadEventFlag(i))
            {
                if (_profile.ReadEventFlag(i) != false || candidate.ReadEventFlag(i) != true)
                    throw new InvalidDataException("Story service returned a non-monotone event change.");
                events.Add(i);
            }
        for (int i = 0; i < 64; i++)
            if (_profile.ReadPrimaryFlag(i) != candidate.ReadPrimaryFlag(i))
            {
                if (i >= 57 || _profile.ReadPrimaryFlag(i) != false || candidate.ReadPrimaryFlag(i) != true)
                    throw new InvalidDataException("Story service returned a non-monotone achievement change.");
                var d = candidate.ReadDate(i);
                if (d.Year is null || d.Month is null || d.Day is null || d.Hour is null || d.RawByte5 is null)
                    throw new InvalidDataException("New achievement date is unknown.");
                achievements.Add(new(i, new(d.Year.Value, d.Month.Value, d.Day.Value, d.Hour.Value, d.RawByte5.Value)));
            }
        for (int i = 0; i < 200; i++)
            if (_profile.ReadScenarioCompleted(i) != candidate.ReadScenarioCompleted(i))
            {
                if (candidate.ReadScenarioCompleted(i) != true || _profile.ReadScenarioCompleted(i) == true)
                    throw new InvalidDataException("Ending service returned a non-monotone scenario change.");
                scenarios.Add(i);
            }
        for (int i = 0; i < 3; i++)
            if (_profile.ReadEndingTrack(i) != candidate.ReadEndingTrack(i))
            {
                if (_profile.ReadEndingTrack(i) != 0 || candidate.ReadEndingTrack(i) != 1)
                    throw new InvalidDataException("Ending service returned a non-monotone completion track.");
                tracks.Add(i);
            }
        for (int i = 0; i < 61; i++)
            if (_profile.ReadBonusOfficerUnlocked(i) != candidate.ReadBonusOfficerUnlocked(i))
            {
                if (candidate.ReadBonusOfficerUnlocked(i) != true || _profile.ReadBonusOfficerUnlocked(i) == true)
                    throw new InvalidDataException("Ending service returned a non-monotone bonus officer change.");
                bonusOfficers.Add(i);
            }
        for (int i = 0; i < 20; i++)
            if (_profile.ReadTutorialCompleted(i) != candidate.ReadTutorialCompleted(i))
            {
                if (i >= 8 || candidate.ReadTutorialCompleted(i) != true || _profile.ReadTutorialCompleted(i) == true)
                    throw new InvalidDataException("Tutorial service returned a non-monotone course completion change.");
                tutorialLessons.Add(i);
            }
        return PreserveClockReads(new(events.ToArray(), achievements.ToArray(), candidate.DirtyFlag == 1)
            { CompletedScenarios = scenarios.ToArray(), CompletedEndingTracks = tracks.ToArray(), UnlockedBonusOfficerIndices = bonusOfficers.ToArray(),
                CompletedTutorialLessons = tutorialLessons.ToArray() });
    }
    internal void Publish(TransferServiceState validated)
    {
        // Attempt-local observations must not become a future operation's clock.
        validated.AchievementClockSnapshotProvider = _profile.AchievementClockSnapshotProvider;
        _profile = validated;
    }
}
