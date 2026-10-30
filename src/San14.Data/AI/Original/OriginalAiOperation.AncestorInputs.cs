using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    internal OriginalAiNodeStatus AncestorMyselfDistrict(uint destination)
        => Actor.AgentKind == OriginalAiAgentKind.Force ? SetAncestorEntity(destination, null)
            : SetAncestorEntity(destination, Adapter.ReadOriginalAiMyselfDistrict(InputsCurrent, Actor));

    internal OriginalAiNodeStatus AncestorMyselfForce(uint destination)
        => SetAncestorEntity(destination, Adapter.ReadOriginalAiMyselfForce(InputsCurrent, Actor));

    internal OriginalAiNodeStatus AncestorGameInformation(int mode, uint destination)
        => SetInteger(destination, Adapter.ReadOriginalAiGameInformation(Current, mode));

    internal OriginalAiNodeStatus AncestorDistrictInformation(uint input, int mode, uint destination)
    {
        if (!TryEntity(input, out var district) || district.Kind != WorldEntityKind.District)
            return OriginalAiNodeStatus.Failure;
        // The variable node reads its actual object without the separate native-validity gate
        // used by GetMyselfDistrictInfo. In particular, District0 is a real table receiver.
        return PublishDistrictInformation(district.Id, mode, destination);
    }

    internal OriginalAiNodeStatus AncestorMyselfDistrictInformation(int mode, uint destination)
    {
        OriginalAiEntity? district = Adapter.ReadOriginalAiMyselfDistrict(InputsCurrent, Actor);
        if (district is not { } value || !Adapter.ReadOriginalAiDistrictValidity(InputsCurrent, value.Id))
            return OriginalAiNodeStatus.Failure;
        return PublishDistrictInformation(value.Id, mode, destination);
    }

    private OriginalAiNodeStatus PublishDistrictInformation(int district, int mode, uint destination)
        => mode is 2 or 4
            ? SetAncestorEntity(destination, Adapter.ReadOriginalAiDistrictEntity(InputsCurrent, district, mode))
            : SetInteger(destination, Adapter.ReadOriginalAiDistrictScalar(InputsCurrent, district, mode));

    internal OriginalAiNodeStatus AncestorMyselfForceInformation(int mode, uint destination)
    {
        OriginalAiEntity? force = Adapter.ReadOriginalAiMyselfForce(InputsCurrent, Actor);
        if (force is not { } value || !Adapter.ReadOriginalAiForceValidity(InputsCurrent, value.Id))
            return OriginalAiNodeStatus.Failure;
        return mode == 2
            ? SetAncestorEntity(destination, Adapter.ReadOriginalAiForceRuler(InputsCurrent, value.Id))
            : SetInteger(destination, Adapter.ReadOriginalAiForceIdentity(InputsCurrent, value.Id));
    }

    internal OriginalAiNodeStatus AncestorRandom(uint destination, int bound,
        uint firstKey, uint secondKey, uint thirdKey, uint fourthKey)
    {
        // 091400 explicitly substitutes0 for each failed optional integer read.
        int Read(uint key) => TryInteger(key, out int value) ? value : 0;
        int first = Read(firstKey), second = Read(secondKey), third = Read(thirdKey), fourth = Read(fourthKey);
        if (bound < 2) return SetInteger(destination, 0);
        return SetInteger(destination, Adapter.ReadOriginalAiRandom(Current, InputsCurrent,
            bound, first, second, third, fourth));
    }

    internal OriginalAiNodeStatus AncestorForceAssociationPeople(uint input, uint destination)
    {
        if (!TryEntity(input, out var force) || force.Kind != WorldEntityKind.Force ||
            !Adapter.ReadOriginalAiForceValidity(InputsCurrent, force.Id)) return OriginalAiNodeStatus.Failure;
        var people = Adapter.ReadOriginalAiForceAssociationPeople(InputsCurrent, force.Id);
        ClearCandidates(destination, WorldEntityKind.Officer);
        foreach (var person in people) candidateLists[destination].Values.Add(person);
        return OriginalAiNodeStatus.Success;
    }

    private OriginalAiNodeStatus SetAncestorEntity(uint destination, OriginalAiEntity? value)
    {
        if (value is { } entity) return SetEntity(destination, entity);
        // B27E20 can write a genuine null object. Do not manufacture District0 for Force actors.
        entities.Remove(destination); integers.Remove(destination); candidateLists.Remove(destination);
        candidateNullEntities.Add(destination);
        return OriginalAiNodeStatus.Success;
    }
}
