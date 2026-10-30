using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    private GameState? armyAxesWorld;
    private readonly Dictionary<int, UnitSourceAxes> armyAxes = [];

    internal OriginalAiNodeStatus ReadMyselfArmy(uint output)
    {
        //The shared actor resolver returns null for non-Army scopes.
        if (Actor.AgentKind is null) throw new InvalidDataException("original_ai.Army.actor_AgentType");
        if (Actor.AgentKind != OriginalAiAgentKind.Army) return SetAncestorEntity(output, null);
        return SetEntity(output, new(WorldEntityKind.Army, Actor.ArmyId
            ?? throw new InvalidDataException("original_ai.Army.actor_logical_identity")));
    }

    internal OriginalAiNodeStatus SetArmyInstructionAction(uint armyKey, int action)
        => ReadInstructionArmy(armyKey) is { } army
            ? ApplyNativeCommand(new SetArmyInstructionActionOrder(Actor.ForceId, army.Id, action))
            : OriginalAiNodeStatus.Failure;

    internal OriginalAiNodeStatus SetArmyInstructionTarget(uint armyKey, uint targetKey)
    {
        if (ReadInstructionArmy(armyKey) is not { } army) return OriginalAiNodeStatus.Failure;
        OriginalAiEntity? entity = TryEntity(targetKey, out var selected) ? selected : null;
        OriginalAiHex? hex = TryHex(targetKey, out var at) ? at : null;
        var target = Adapter.PrepareOriginalAiArmyInstructionTarget(Current, InputsCurrent, army, entity, hex);
        return target is null ? OriginalAiNodeStatus.Success
            : ApplyNativeCommand(new SetArmyInstructionTargetOrder(Actor.ForceId, army.Id, target));
    }

    internal OriginalAiNodeStatus SetArmyInstructionRelay(uint armyKey, uint listKey)
    {
        if (ReadInstructionArmy(armyKey) is not { } army || !TryTypedCandidates(listKey, CandidateListKind.Hex, out var list))
            return OriginalAiNodeStatus.Failure;
        return ApplyNativeCommand(new SetArmyInstructionRelayOrder(Actor.ForceId, army.Id,
            list.HexValues.Where(hex => Adapter.IsOriginalAiArmyRelayHexCurrent(InputsCurrent, hex)).Take(5).Select(hex => hex.Id).ToArray()));
    }

    internal OriginalAiNodeStatus RetreatArmyInstruction(uint armyKey)
        => ReadInstructionArmy(armyKey) is { } army
            ? ApplyNativeCommand(new RetreatArmyInstructionOrder(Actor.ForceId, army.Id)) : OriginalAiNodeStatus.Failure;

    internal OriginalAiNodeStatus ReadArmyInformation(uint input, int mode, uint output)
    {
        if (ReadInstructionArmy(input) is not { } army) return OriginalAiNodeStatus.Failure;
        if (mode == 17) return SetInteger(output, CandidateEvaluation(new(WorldEntityKind.Army, army.Id)));
        if (!ReferenceEquals(armyAxesWorld, Current)) { armyAxesWorld = Current; armyAxes.Clear(); }
        UnitSourceAxes Axes()
        {
            if (!armyAxes.TryGetValue(army.Id, out var value))
                armyAxes.Add(army.Id, value = Adapter.ReadOriginalAiArmyAxesCurrent(Current, InputsCurrent, army));
            return value;
        }
        var value = Adapter.ReadOriginalAiArmyInformationCurrent(Current, InputsCurrent, Actor, army, mode, Axes);
        return value switch
        {
            TroopsInformationInteger number => SetInteger(output, number.Value),
            TroopsInformationEntity entity => SetEntity(output, entity.Value),
            TroopsInformationHex hex => SetHex(output, hex.Value),
            ArmyInformationTroops group => SetTroops(output, group.Value),
            ArmyInformationSkill skill => SetPersonSkill(output, skill.Value),
            TroopsInformationHexes hexes => PublishHexes(hexes.Values),
            ArmyInformationNull => SetAncestorEntity(output, null),
            TroopsInformationUnchanged => OriginalAiNodeStatus.Success,
            _ => throw new InvalidDataException("original_ai.Army_information_result")
        };
        OriginalAiNodeStatus PublishHexes(IReadOnlyList<OriginalAiHex> values)
        { ReplaceHexCandidates(output, values); return OriginalAiNodeStatus.Success; }
    }

    private UnitState? ReadInstructionArmy(uint key)
    {
        if (!TryEntity(key, out var entity) || entity.Kind != WorldEntityKind.Army) return null;
        return Adapter.ReadOriginalAiInstructionArmyCurrent(Current, InputsCurrent, entity.Id);
    }
}

internal sealed record ArmyInformationNull : TroopsInformationValue;
internal sealed record ArmyInformationTroops(OriginalAiTroopsReference Value) : TroopsInformationValue;
