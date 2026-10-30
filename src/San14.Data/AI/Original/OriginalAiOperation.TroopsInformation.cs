using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    internal OriginalAiNodeStatus ReadTroopsInformation(uint input, int mode, uint output)
    {
        if (!TryTroops(input, out var group) || !Adapter.ReadOriginalAiTroopsValidityCurrent(InputsCurrent, group))
            return OriginalAiNodeStatus.Failure;
        var value = Adapter.ReadOriginalAiTroopsInformationCurrent(Current, InputsCurrent, Actor, group, mode);
        switch (value)
        {
            case TroopsInformationInteger scalar: return SetInteger(output, scalar.Value);
            case TroopsInformationEntity entity: return SetEntity(output, entity.Value);
            case TroopsInformationHex hex: return SetHex(output, hex.Value);
            case TroopsInformationArmies armies:
                ReplaceTypedCandidates(output, WorldEntityKind.Army, armies.Values); return OriginalAiNodeStatus.Success;
            case TroopsInformationHexes hexes:
                ReplaceHexCandidates(output, hexes.Values); return OriginalAiNodeStatus.Success;
            case TroopsInformationUnchanged: return OriginalAiNodeStatus.Success;
            default: throw new InvalidDataException("original_ai.troops_information.result");
        }
    }

    internal OriginalAiNodeStatus SetTroopsPurpose(uint groupKey, int purpose, uint targetKey)
    {
        if (!TryTroops(groupKey, out var group) || !Adapter.ReadOriginalAiTroopsValidityCurrent(InputsCurrent, group))
            return OriginalAiNodeStatus.Failure;
        OriginalAiEntity? entity = TryEntity(targetKey, out var at) ? at : null;
        OriginalAiHex? hex = TryHex(targetKey, out var cell) ? cell : null;
        var prepared = Adapter.PrepareOriginalAiTroopsPurposeCurrent(Current, InputsCurrent, Actor, group, purpose, entity, hex);
        return prepared.Order is { } order ? ApplyNativeCommand(order)
            : prepared.Success ? OriginalAiNodeStatus.Success : OriginalAiNodeStatus.Failure;
    }

    internal OriginalAiNodeStatus SetTroopsRelayHexes(uint groupKey, uint listKey)
    {
        if (!TryTroops(groupKey, out var group) || !Adapter.ReadOriginalAiTroopsValidityCurrent(InputsCurrent, group))
            return OriginalAiNodeStatus.Failure;
        if (!TryTypedCandidates(listKey, CandidateListKind.Hex, out var list)) return OriginalAiNodeStatus.Failure;
        return ApplyNativeCommand(new SetArmyGroupRelayHexesOrder(Actor.ForceId, group.GroupId,
            list.HexValues.Take(10).Select(hex => hex.Id).ToArray()));
    }
}

internal abstract record TroopsInformationValue;
internal sealed record TroopsInformationInteger(int Value) : TroopsInformationValue;
internal sealed record TroopsInformationEntity(OriginalAiEntity Value) : TroopsInformationValue;
internal sealed record TroopsInformationHex(OriginalAiHex Value) : TroopsInformationValue;
internal sealed record TroopsInformationArmies(IReadOnlyList<OriginalAiEntity> Values) : TroopsInformationValue;
internal sealed record TroopsInformationHexes(IReadOnlyList<OriginalAiHex> Values) : TroopsInformationValue;
internal sealed record TroopsInformationUnchanged : TroopsInformationValue;
