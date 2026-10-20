namespace San14.Data;

public enum DeploymentAutomaticStyle { FieldAttack, SiegeTroops, SiegeDurability, Mobility, Occupation }
public sealed record DeploymentAutomaticOption(DeploymentAutomaticStyle Style, bool Enabled);
public sealed record DeploymentAutomaticOptions(IReadOnlyList<DeploymentAutomaticOption>? Options, string? UnresolvedInput)
{
    public bool Available => Options is not null && UnresolvedInput is null;
}

/// <summary>Paired CE automatic composition preferences extracted offline.
/// Entries are formation identities; zero weights deliberately exclude a choice.</summary>
internal static class DeploymentAutomaticPreferences
{
    internal static readonly int[][] FormationWeights =
    [
        [0,100,50,25,50,50,25,0,0,0,0,0,0,0,0,0,0,0,0,0,0],
        [0,25,0,25,10,10,10,0,50,25,50,0,0,0,0,0,0,0,0,0,0],
        [0,25,0,10,10,10,10,0,25,100,50,0,0,0,0,0,0,0,0,0,0],
        [0,0,50,0,0,25,50,200,0,0,0,0,0,0,0,0,0,0,0,0,0],
        [0,50,25,0,25,200,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0]
    ];
}

internal static class DeploymentAutomaticFormationChoice
{
    internal static int Select(IReadOnlyList<int> weights, IReadOnlyList<int> newCounts,
        IReadOnlyList<int> existing, int gold, Func<int, bool> allows, Func<int, int> cost)
    {
        int selected = 0;
        for (int formation = 1; formation < 21; formation++)
        {
            int weight = weights[formation];
            if (weight <= 0) continue;
            // Existing count of the current winner is used on BOTH sides.
            int already = existing[selected];
            float previous = (float)weights[selected] / unchecked(newCounts[selected] + 1 + already);
            float next = (float)weight / unchecked(already + 1 + newCounts[formation]);
            if (!allows(formation)) continue;
            if (unchecked(gold - cost(formation)) < 0) continue;
            if (selected == 0 || next > previous) selected = formation;
        }
        return selected;
    }
}

internal static class DeploymentAutomaticCandidateScore
{
    internal static int Read(San14.Core.OfficerAbilities abilities, int validTactics,
        int stock, int number, DeploymentAutomaticStyle style, Func<int, bool> allows, Func<int> capacity)
    {
        int field = allows(1) || allows(2) || allows(3) || allows(4) || allows(5) || allows(6) ? 10 : 0;
        int attack = allows(1) || allows(3) || allows(4) || allows(5) ? 20 : 0;
        int mobile = allows(2) || allows(3) ? 30 : 0;
        int occupy = allows(5) ? 40 : 0;
        int siege = allows(8) || allows(10) ? 300 : 0;
        int breakWall = allows(9) || allows(10) ? 300 : 0;
        float average = (float)stock / number;
        int integer = !float.IsFinite(average) || average < -2147483648f || average >= 2147483648f ? int.MinValue : (int)average;
        int strength = Math.Min(integer / 20, capacity());
        int common = unchecked(abilities.Leadership * 5 + abilities.Strength + abilities.Intelligence + Math.Min(3, validTactics) * 30);
        return style switch
        {
            DeploymentAutomaticStyle.FieldAttack => unchecked(strength + field + common),
            DeploymentAutomaticStyle.SiegeTroops => unchecked(strength + siege + attack + common),
            DeploymentAutomaticStyle.SiegeDurability => unchecked(strength + breakWall + attack + common),
            DeploymentAutomaticStyle.Mobility => mobile,
            DeploymentAutomaticStyle.Occupation => unchecked(occupy + attack),
            _ => throw new InvalidDataException("deployment.automatic.style")
        };
    }
}
