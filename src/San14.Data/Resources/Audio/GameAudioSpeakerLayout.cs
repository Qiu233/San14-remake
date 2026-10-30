namespace San14.Data;

/// <summary>The environment page's independent output selection. Source PCM
/// speaker masks remain properties of the selected samples.</summary>
public enum GameAudioSpeakerLayout { Stereo = 0, Surround51 = 1 }
public sealed record SpeakerLayoutAvailability(bool Available, string? UnresolvedOwner);
