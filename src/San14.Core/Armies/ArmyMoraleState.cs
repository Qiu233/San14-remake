namespace San14.Core;

/// <summary>The provisional rules and the current campaign rules maintain
/// independent morale. Unknown current morale never borrows the provisional value.
/// Neither value is a frozen departure snapshot or a cached force maximum.</summary>
public sealed record ArmyMoraleState(int ProvisionalValue, int? CurrentValue);
