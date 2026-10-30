using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    internal OriginalAiNodeStatus FootholdInformation(uint input, int mode, uint output)
    {
        if (!TryEntity(input, out var at) || at.Kind is not (WorldEntityKind.City or WorldEntityKind.Gate))
            return OriginalAiNodeStatus.Failure;
        var receiver = BaseLookup.Find(at.Id, InputsCurrent);
        if (!(InputsCurrent.ReadBaseNativeValidity(receiver) ?? throw BaseInformationMissing("Foothold_validity")))
            return OriginalAiNodeStatus.Failure;
        if (mode == 33) return SetInteger(output, CandidateEvaluation(at));
        return AdoptBaseInformation(output, Adapter.ReadOriginalAiFootholdInformationCurrent(Current, InputsCurrent, Actor, receiver, mode));
    }

    internal OriginalAiNodeStatus AreaInformation(uint input, int mode, uint output)
    {
        if (!TryEntity(input, out var at) || at.Kind != WorldEntityKind.Area) return OriginalAiNodeStatus.Failure;
        var receiver = InputsCurrent.ReadAreaTableReference(at.Id is >= 0 and <= 500 ? at.Id : 0)
            ?? throw BaseInformationMissing("Area_pointer");
        if (!(InputsCurrent.ReadAreaNativeValidity(receiver) ?? throw BaseInformationMissing("Area_validity")))
            return OriginalAiNodeStatus.Failure;
        if (mode == 21) return SetInteger(output, CandidateEvaluation(at));
        return AdoptBaseInformation(output, Adapter.ReadOriginalAiAreaInformationCurrent(Current, InputsCurrent, receiver, mode));
    }

    internal OriginalAiNodeStatus MyselfDistrictInformation(int mode, uint output)
    {
        var at = Adapter.ReadOriginalAiMyselfDistrict(InputsCurrent, Actor);
        if (at is not { } district || !(InputsCurrent.ReadDistrictNativeValidity(district.Id)
            ?? throw BaseInformationMissing("MyselfDistrict_validity"))) return OriginalAiNodeStatus.Failure;
        return ReadDistrictInformation(district.Id, mode, output);
    }

    internal OriginalAiNodeStatus DistrictInformation(uint input, int mode, uint output)
    {
        //The variable node requires an actual object, but unlike MyselfDistrict
        //does not add a native-validity gate before its selected getter.
        if (!TryEntity(input, out var at) || at.Kind != WorldEntityKind.District) return OriginalAiNodeStatus.Failure;
        int district = InputsCurrent.ReadDistrictTableReference(at.Id is >= 0 and <= 51 ? at.Id : 0)?.PhysicalSlot
            ?? throw BaseInformationMissing("District_pointer");
        return ReadDistrictInformation(district, mode, output);
    }

    private OriginalAiNodeStatus ReadDistrictInformation(int district, int mode, uint output)
    {
        if (mode is >= 8 and <= 11)
        {
            string name = mode switch { 8 => "AREA_OF_INFLUENCE", 9 => "CAUTION_FOOTHOLD_LIST",
                10 => "CAUTION_TROOPS_LIST", _ => "CAUTION_ARMY_LIST" };
            return TryBorrowDistrictListAlias(district, ResourceNameHash.Compute(name), output);
        }
        //The source clears/creates its Foothold list before testing the
        //District's Force. Keep that mutation even when the getter fails.
        if (mode == 19) ReplaceFootholdCandidates(output, []);
        if (mode == 12) return SetInteger(output, CandidateEvaluation(new(WorldEntityKind.District, district)));
        return AdoptBaseInformation(output, Adapter.ReadOriginalAiDistrictInformationCurrent(Current, InputsCurrent, district, mode));
    }

    private OriginalAiNodeStatus AdoptBaseInformation(uint output, BaseInformationValue value)
    {
        switch (value)
        {
            case BaseInformationInteger scalar: return SetInteger(output, scalar.Value);
            case BaseInformationEntity entity: return SetAncestorEntity(output, entity.Value);
            case BaseInformationHex hex: return SetHex(output, hex.Value);
            case BaseInformationEntities list:
                ReplaceTypedCandidates(output, list.Kind, list.Values); return OriginalAiNodeStatus.Success;
            case BaseInformationHexes list:
                ReplaceHexCandidates(output, list.Values); return OriginalAiNodeStatus.Success;
            case BaseInformationRegion region: return SetRegionReference(output, region.PhysicalSlot);
            case BaseInformationFailure: return OriginalAiNodeStatus.Failure;
            case BaseInformationUnchanged: return OriginalAiNodeStatus.Success;
            default: throw BaseInformationMissing("result_type");
        }
    }

    private static InvalidDataException BaseInformationMissing(string field) => new("original_ai.base_information." + field);
}

internal abstract record BaseInformationValue;
internal sealed record BaseInformationInteger(int Value) : BaseInformationValue;
internal sealed record BaseInformationEntity(OriginalAiEntity? Value) : BaseInformationValue;
internal sealed record BaseInformationHex(OriginalAiHex Value) : BaseInformationValue;
internal sealed record BaseInformationEntities(WorldEntityKind Kind, IReadOnlyList<OriginalAiEntity> Values) : BaseInformationValue;
internal sealed record BaseInformationHexes(IReadOnlyList<OriginalAiHex> Values) : BaseInformationValue;
internal sealed record BaseInformationRegion(int PhysicalSlot) : BaseInformationValue;
internal sealed record BaseInformationFailure : BaseInformationValue;
internal sealed record BaseInformationUnchanged : BaseInformationValue;
