namespace San14.Core;

/// <summary>A current ordinary field Army target. Null selection is unknown;
/// an empty ArmyId is a known absent/ineligible Army. Base and generic Object
/// receivers remain with their respective contact providers.</summary>
public readonly record struct PlayableArmyContact(int? ArmyId);

public interface IPlayableArmyContactRules
{
    Func<int, PlayableArmyContact?> CaptureCurrentArmyContact(GameState state);
    bool? CanAttackFromCurrent(GameState state, UnitState actor);
}
