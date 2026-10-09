using San14.Core;

namespace San14.Client.Gameplay;

/// <summary>Owns the local campaign file layout and read codec. Active sessions
/// own their write codec; neither a world nor a session is retained here.</summary>
internal sealed class LocalCampaignSaves(string directory, ISaveCodec readCodec)
{
    public const int SlotCount = 10;

    private string PreviewPath => Path.Combine(directory, "initial-state.sav");

    private string CampaignPath(int slot)
    {
        if (slot < 1 || slot > SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
        string file = slot == 1 ? "playable-session.sav" : $"playable-session-{slot:00}.sav";
        return Path.Combine(directory, file);
    }

    public bool HasCampaign(int slot) => File.Exists(CampaignPath(slot));
    public void SaveCampaign(GameplaySession session, int slot) => session.Save(CampaignPath(slot));
    public GameSessionSave LoadCampaign(int slot) => GameplaySession.ReadSave(CampaignPath(slot), readCodec);
    public void SavePreview(GameplaySession session) => session.SavePreview(PreviewPath);
    public GameState LoadPreview() => GameplaySession.ReadPreview(PreviewPath, readCodec);
}
