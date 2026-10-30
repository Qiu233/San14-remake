using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    internal OriginalAiNodeStatus CreateDepartureTroops(uint armiesKey, int purpose, uint targetKey, uint output)
    {
        if (!TryTypedCandidates(armiesKey, CandidateListKind.Army, out var armies) || armies.Values.Count == 0)
            return OriginalAiNodeStatus.Failure;
        int[] ids = armies.Values.Select(value => value.Kind == WorldEntityKind.Army ? value.Id
            : throw new InvalidDataException("original_ai.departure_troops.Army_list_kind")).ToArray();
        OriginalAiEntity? target = TryEntity(targetKey, out var entity) ? entity : null;
        OriginalAiHex? hex = TryHex(targetKey, out var cell) ? cell : null;
        var execution = new OriginalAiDepartureTroopsRules(Adapter);
        var result = planning.Apply(new CreateOriginalAiDepartureTroopsOrder(Actor.ForceId, ids, purpose, target, hex),
            execution, commandContext, authority);
        LastCommand = result;
        if (result.MissingRuleKey is { } missing) throw new InvalidDataException(missing + ": " + result.Error);
        if (!result.Accepted || execution.NativeReturn != 1) return OriginalAiNodeStatus.Failure;
        //035990 neither clears an old output nor writes a native-invalid
        //created group. Output0 is the original disabled-output operand.
        if (output != 0 && execution.CreatedGroup is { } group) SetTroops(output, group);
        return OriginalAiNodeStatus.Success;
    }
}
