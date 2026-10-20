namespace San14.Data;

public enum AreaAdministrationHandoverKind { DirectCommit, Narration }

/// <summary>One read-only preparation of the current parent draft. An unchanged
/// draft (count0) follows the parent's existing restore/close path.</summary>
public sealed record AreaAdministrationHandover(AreaAdministrationHandoverKind Kind,
    long DraftRevision, int ChangedAreaCount, AreaAdministrationNarration? Narration);

public sealed record NarrationMessageChoice(int MessageId, int Arity, int SelectedIndex);

public sealed record AreaAdministrationNarration(int SpeakerOfficerId, string SpeakerName,
    ushort PortraitAppearanceCode, DeploymentNarrationPortraitRoute PortraitRoute,
    int AddresseeOfficerId, int AreaId, int RootMessageId, int MessageId,
    IReadOnlyList<NarrationMessageChoice> Choices, string Text, IReadOnlyList<string> Pages)
{
    public int? RequestedVoiceId => RootMessageId == 0x5742 ? 33 : null;
    public bool VoiceAvailabilityKnown => false;
}
