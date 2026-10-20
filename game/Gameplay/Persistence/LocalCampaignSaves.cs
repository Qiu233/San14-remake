using San14.Core;

namespace San14.Client.Gameplay;

/// <summary>Owns the local campaign file layout and read codec. Active sessions
/// own their write codec; neither a world nor a session is retained here.</summary>
internal sealed class LocalCampaignSaves(string directory, ISaveCodec readCodec)
{
    public const int SlotCount = 50;

    private string PreviewPath => Path.Combine(directory, "initial-state.sav");

    private string CampaignPath(int slot)
    {
        if (slot < 1 || slot > SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
        string file = slot == 1 ? "playable-session.sav" : $"playable-session-{slot:00}.sav";
        return Path.Combine(directory, file);
    }

    public bool HasCampaign(int slot) => File.Exists(CampaignPath(slot));
    public void SaveCampaign(GameplaySession session, int slot) => session.Save(CampaignPath(slot), DateTimeOffset.Now);
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
    public void SavePreview(GameplaySession session) => session.SavePreview(PreviewPath);
    public GameState LoadPreview() => GameplaySession.ReadPreview(PreviewPath, readCodec);
}
