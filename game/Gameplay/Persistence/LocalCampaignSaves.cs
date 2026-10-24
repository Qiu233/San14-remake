using San14.Core;

namespace San14.Client.Gameplay;

/// <summary>Owns the local campaign file layout and read codec. Active sessions
/// own their write codec; neither a world nor a session is retained here.</summary>
internal sealed class LocalCampaignSaves(string directory, ISaveCodec readCodec)
{
    public const int SlotCount = 50;
    public const int AutomaticSlotCount = 10;
    public const int TotalReadableSlots = SlotCount + AutomaticSlotCount;

    private string PreviewPath => Path.Combine(directory, "initial-state.sav");

    private string CampaignPath(int slot)
    {
        if (slot < 1 || slot > TotalReadableSlots) throw new ArgumentOutOfRangeException(nameof(slot));
        string file = slot > SlotCount ? $"autosave-session-{slot - SlotCount - 1:00}.sav"
            : slot == 1 ? "playable-session.sav" : $"playable-session-{slot:00}.sav";
        return Path.Combine(directory, file);
    }

    public bool HasCampaign(int slot) => File.Exists(CampaignPath(slot));
    public bool HasAnyCampaign => Enumerable.Range(1, TotalReadableSlots).Any(HasCampaign);

    public void SaveCampaign(GameplaySession session, int slot, CampaignAppRandomState? appRandomSnapshot = null)
    {
        if (slot < 1 || slot > SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
        session.Save(CampaignPath(slot), DateTimeOffset.Now, appRandomSnapshot);
    }

    public int SaveAutomatic(GameplaySession session, CampaignAppRandomState? appRandomSnapshot = null)
    {
        int recentSlot = SlotCount;
        DateTime? recentTime = null;
        // Rotation follows the newest internal save time, then its next slot.
        // It deliberately does not choose an empty or oldest slot, or use mtime.
        // Read only framing and timestamps when an actual save is requested.
        for (int slot = SlotCount + 1; slot <= TotalReadableSlots; slot++)
        {
            DateTimeOffset? savedAt;
            try { savedAt = SessionSaveStore.ReadSavedAt(CampaignPath(slot)); }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
            { continue; }
            // A corrupt or older undated automatic file is not an empty slot.
            // Leave every file intact when its place in the rotation is unknown.
            DateTime localTime = savedAt?.DateTime ?? throw new InvalidDataException(
                $"Automatic campaign slot {slot} has no internal save time.");
            localTime = new DateTime(localTime.Ticks - localTime.Ticks % TimeSpan.TicksPerSecond);
            // The original SYSTEMTIME-to-tm comparison has second resolution;
            // equal times retain the first slot encountered in directory order.
            if (recentTime is null || localTime > recentTime.Value)
            {
                recentTime = localTime;
                recentSlot = slot;
            }
        }
        int nextSlot = recentSlot + 1;
        if (nextSlot > TotalReadableSlots) nextSlot = SlotCount + 1;
        session.Save(CampaignPath(nextSlot), DateTimeOffset.Now, appRandomSnapshot);
        return nextSlot;
    }

    public GameSessionSave LoadCampaign(int slot) => GameplaySession.ReadSave(CampaignPath(slot), readCodec);

    public CampaignSaveSlot ReadSlot(int slot, Func<GameSessionSave, CampaignSaveSummary>? summarize = null)
    {
        string path = CampaignPath(slot);
        GameSessionSave saved;
        try
        {
            // Read directly: File.Exists also returns false for some access
            // errors, which must not turn a saved game into an empty slot.
            saved = GameplaySession.ReadSave(path, readCodec);
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        { return new(slot, CampaignSaveSlotKind.Empty); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        { return new(slot, CampaignSaveSlotKind.Unreadable, Error: error.Message); }
        // Resource presentation is independent of file validity. Its failure
        // must not relabel a correctly decoded campaign as corrupt.
        return new(slot, CampaignSaveSlotKind.Available, (summarize ?? CampaignSaveSummary.Capture)(saved));
    }

    public IReadOnlyList<CampaignSaveSlot> ReadSlots(Func<GameSessionSave, CampaignSaveSummary>? summarize = null) =>
        Enumerable.Range(1, SlotCount).Select(slot => ReadSlot(slot, summarize)).ToArray();
    public IReadOnlyList<CampaignSaveSlot> ReadAutomaticSlots(Func<GameSessionSave, CampaignSaveSummary>? summarize = null) =>
        Enumerable.Range(SlotCount + 1, AutomaticSlotCount).Select(slot => ReadSlot(slot, summarize)).ToArray();
    public void SavePreview(GameplaySession session) => session.SavePreview(PreviewPath);
    public GameState LoadPreview() => GameplaySession.ReadPreview(PreviewPath, readCodec);
}
