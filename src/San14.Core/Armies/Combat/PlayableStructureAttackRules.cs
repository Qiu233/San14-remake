namespace San14.Core;

/// <summary>A formal current-object attack command. Damage and retirement
/// belong to ordinary advancement, not command preview or submission.</summary>
public interface IPlayableArmyStructureAttackRules
{
    CommandResult IssueStructureAttack(GameState state, UnitState army, int structureId,
        int approachHexId, IReadOnlyList<int> waypointHexIds);
}
