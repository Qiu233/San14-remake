namespace San14.Data;

public enum DeploymentPersonResponsibilityKind { Empty, NotApplicable, AreaName, Recruit, Train, Spy }

/// <summary>Display meaning, independent of the responsibility sorting weight.
/// An area reference identifies the current name; captions and fonts belong to UI.</summary>
public readonly record struct DeploymentPersonResponsibility(
    DeploymentPersonResponsibilityKind Kind, int? AreaId = null);
