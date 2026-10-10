namespace San14.Core;

/// <summary>Deployment selection and the two current terrain-dependent formations
/// are independent facts. An unknown current formation is never filled by the selection.</summary>
public sealed record ArmyFormationState(int? SelectedLandFormationId, int? CurrentLandFormationId,
    int? NavalFormationId);

/// <summary>Known ordinary field-attack behavior. A present policy with an unknown
/// gate retains module presence without inventing an action.</summary>
public sealed record ArmyOrdinaryFieldAttackPolicy(bool? UsesConfusionGate);
