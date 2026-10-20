namespace San14.Data;

/// <summary>The three ordered local selection slots, including source zero sentinels.</summary>
public sealed record DeploymentTacticSlots(int First, int Second, int Third)
{
    public int[] ToArray() => [First, Second, Third];
    internal static DeploymentTacticSlots From(IReadOnlyList<int> slots)
    {
        if (slots.Count != 3) throw new ArgumentException("Exactly three selection slots are required.", nameof(slots));
        return new(slots[0], slots[1], slots[2]);
    }
}

/// <summary>Unsubmitted right-pane state. LandFormationId is the last displayed
/// formation, before a new radio action overwrites it.</summary>
public sealed record DeploymentLandFormationEditState(int LandFormationId,
    DeploymentTacticSlots Tactics, DeploymentTacticSlots BackupTactics, bool BackupActive);

public sealed record DeploymentLandFormationEditResult(DeploymentLandFormationEditState? Value,
    string? UnresolvedInput)
{
    public bool Available => Value is not null && UnresolvedInput is null;
}
