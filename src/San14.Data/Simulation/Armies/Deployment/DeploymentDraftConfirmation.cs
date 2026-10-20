namespace San14.Data;

public enum DeploymentDraftConfirmationKind { Normal, LowMoraleWarning, Unknown }

/// <summary>The ordinary new-draft confirmation result. OfficerId identifies
/// the first warning or unresolved row; Normal has neither officer nor reason.</summary>
public sealed record DeploymentDraftConfirmation(DeploymentDraftConfirmationKind Kind,
    int? OfficerId, string? UnresolvedInput);
