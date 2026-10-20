using San14.Core;
using San14.Data.Formats;
namespace San14.Data;

public interface IStatelessNativeProgressStageRules : IPlayableNativeArmyProgressStageRules { }
public interface ICampaignStageOperation : IPlayableNativeArmyProgressStageRules
{
    CampaignGroupingCheckpoint CaptureCheckpoint();
    void RestoreCheckpoint(CampaignGroupingCheckpoint checkpoint);
}
public sealed class CampaignStoryBindings(IS14AudioEventExecutionState audio, TransferServiceState profile,
    bool? skipCamera, TransferModeState? externalMode, bool? applicationPresent, int? applicationUserIndex,
    Func<CampaignCameraSnapshot?>? captureCamera = null)
{
    public IS14AudioEventExecutionState Audio { get; } = audio;
    public bool? SkipCamera { get; } = skipCamera;
    public TransferModeState? ExternalMode { get; } = externalMode;
    public bool? ApplicationPresent { get; } = applicationPresent;
    public int? ApplicationUserIndex { get; } = applicationUserIndex;
    public CampaignCameraSnapshot CaptureCamera() => captureCamera?.Invoke() ?? throw new InvalidDataException("Story caller requires its actual campaign camera snapshot.");
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
        if (changes.Dirty) copy.WriteDirtyFlag(1);
        return copy;
    }
    internal CampaignServiceChanges Capture(TransferServiceState candidate)
    {
        var events = new List<int>(); var achievements = new List<CampaignAchievementWrite>();
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
        return PreserveClockReads(new(events.ToArray(), achievements.ToArray(), candidate.DirtyFlag == 1));
    }
    internal void Publish(TransferServiceState validated)
    {
        // Attempt-local observations must not become a future operation's clock.
        validated.AchievementClockSnapshotProvider = _profile.AchievementClockSnapshotProvider;
        _profile = validated;
    }
}
