using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    internal OriginalAiNodeStatus HexInformation(uint input, int mode, uint output)
    {
        if (!TryHex(input, out var value)) return OriginalAiNodeStatus.Failure;
        var at = InputsCurrent.ReadHexTableReference(value.Id)
            ?? throw HexFormationInformationMissing("Hex_table_pointer");
        //A present CHex, including physical0, has actual vslot3=true.
        if (at.PhysicalSlot is not int physical) return OriginalAiNodeStatus.Failure;
        if (mode == 4)
            return SetInteger(output, hexEvaluations.TryGetValue(new(physical), out int score) ? score
                : throw HexFormationInformationMissing("Hex_evaluation8"));
        return Adapter.ReadOriginalAiHexInformationCurrent(Current, InputsCurrent, at, mode) switch
        {
            TroopsInformationInteger number => SetInteger(output, number.Value),
            TroopsInformationEntity entity => SetEntity(output, entity.Value),
            HexInformationNull => SetAncestorEntity(output, null),
            TroopsInformationUnchanged => OriginalAiNodeStatus.Success,
            _ => throw HexFormationInformationMissing("Hex_result_type")
        };
    }

    internal OriginalAiNodeStatus FormationInformation(uint input, int mode, uint output)
    {
        if (!TryPersonSkill(input, out var value) || value.Kind != OriginalAiPersonSkillKind.Formation)
            return OriginalAiNodeStatus.Failure;
        //The installed Formation table has21 stable receivers; vslot3 is
        //physical!=0. Its current parameter bytes are a separately saved owner.
        if (value.PhysicalSlot == 0) return OriginalAiNodeStatus.Failure;
        if (value.PhysicalSlot is < 0 or > 20) throw HexFormationInformationMissing("Formation_index");
        int? force = null;
        if (mode < 5)
        {
            var actual = Adapter.ReadOriginalAiMyselfForce(InputsCurrent, Actor);
            force = actual?.Id ?? throw HexFormationInformationMissing("Formation_actual_Force_pointer");
        }
        return SetInteger(output, Adapter.ReadOriginalAiFormationInformationCurrent(Current, value.PhysicalSlot, force, mode));
    }

    private static InvalidDataException HexFormationInformationMissing(string field) => new("original_ai.hex_formation_information." + field);
}

internal sealed record HexInformationNull : TroopsInformationValue;
