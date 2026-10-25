namespace San14.Core;

/// <summary>Reached application operations following the terminal report.
/// The real application and sound manager consume these in source order.</summary>
public enum ApplicationExitOperation { ReturnToTitle, ResetAudioSpecialMode }
public sealed record ApplicationExitRequest(ApplicationExitOperation Operation);
