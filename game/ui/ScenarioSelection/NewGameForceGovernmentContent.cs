using San14.Client.Gameplay;
using San14.Data;

/// <summary>The normal selection page's detached read-only government content.</summary>
internal sealed record NewGameForceGovernmentContent(
    GovernanceRosterPageModel Roster, GovernanceRosterEvaluationView Evaluation,
    NewGameForceGovernancePreview Source);
