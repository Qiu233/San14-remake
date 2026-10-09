namespace San14.Core;

/// <summary>Identifies a reusable army receiver, independently of its current
/// unit and of the ordinal used by an imported resource table.</summary>
public readonly record struct ArmyReceiverId(int Value);
