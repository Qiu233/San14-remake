using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    // Reuses the existing operation/world-reference cache. Source producers do
    // not rebuild a current world for each field or list element.
    internal SourceWorldContext InputsCurrent => DomesticCurrent.Source;

    internal OriginalAiNodeStatus FootholdPeople(uint input, uint output, bool officerCandidates)
    {
        if (!TryEntity(input, out var foothold) || foothold.Kind is not (WorldEntityKind.City or WorldEntityKind.Gate))
            return OriginalAiNodeStatus.Failure;
        var values = Adapter.ReadOriginalAiFootholdPeopleCurrent(InputsCurrent, foothold.Id, officerCandidates);
        if (values is null) return OriginalAiNodeStatus.Failure;
        // The native producer clears and reuses an existing output list. Keep
        // its identity, including an actual object-equality comparison later.
        ReplaceTypedCandidates(output, WorldEntityKind.Officer, values);
        return OriginalAiNodeStatus.Success;
    }
    internal OriginalAiNodeStatus CandidateListLength(uint input, uint output, WorldEntityKind kind) =>
        TypedCandidateLength(input, output, CandidateKind(kind));

    internal bool CandidateListContains(uint entityKey, uint listKey, WorldEntityKind kind) =>
        TypedCandidateContains(entityKey, listKey, CandidateKind(kind));

    internal bool ActorDistrictPurpose(int purpose)
    {
        if (Actor.AgentKind is null) throw AncestorMissing("actor_agent_kind");
        //0B5D90 returns a known null for a Force actor, not District0.
        if (Actor.AgentKind == OriginalAiAgentKind.Force) return false;
        if (Actor.AgentKind != OriginalAiAgentKind.District || Actor.DistrictId is not int district)
            throw AncestorMissing("actor_District");
        return Adapter.ReadOriginalAiDistrictPurposeCurrent(InputsCurrent, district, purpose);
    }
    internal bool DistrictIsCurrentPlayer(uint key) => TryEntity(key, out var entity) &&
        entity.Kind == WorldEntityKind.District &&
        Adapter.ReadOriginalAiDistrictIsCurrentPlayer(Current, InputsCurrent, entity.Id);
    internal bool DistrictUpdateEligible(uint key, int percentage) => TryEntity(key, out var entity) &&
        entity.Kind == WorldEntityKind.District &&
        Adapter.ReadOriginalAiDistrictUpdateEligible(Current, InputsCurrent, entity.Id, percentage);

    private static InvalidDataException AncestorMissing(string field) => new("original-ai.ancestor." + field);
}
