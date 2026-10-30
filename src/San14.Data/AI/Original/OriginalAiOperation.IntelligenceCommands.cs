using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    internal OriginalAiNodeStatus RemoveIntelligenceAssignment(uint slotKey)
    {
        if (!TryInteger(slotKey, out int oneBased)) return OriginalAiNodeStatus.Failure;
        int district = IntelligenceActorDistrict();
        var prepared = Adapter.PrepareDistrictIntelligenceRemovalCurrent(Current, InputsCurrent, district, unchecked(oneBased - 1));
        return ApplyIntelligenceSlotPreparation(prepared);
    }

    internal OriginalAiNodeStatus SetIntelligenceAssignment(uint targetKey, uint personKey, uint slotKey)
    {
        if (!TryEntity(targetKey, out var target) || target.Kind != WorldEntityKind.Force ||
            !TryEntity(personKey, out var person) || person.Kind != WorldEntityKind.Officer ||
            !TryInteger(slotKey, out int oneBased)) return OriginalAiNodeStatus.Failure;
        int district = IntelligenceActorDistrict();
        var prepared = Adapter.PrepareDistrictIntelligenceSetCurrent(Current, InputsCurrent, district,
            unchecked(oneBased - 1), target.Id, person.Id);
        return ApplyIntelligenceSlotPreparation(prepared);
    }

    private int IntelligenceActorDistrict()
    {
        if (Actor.AgentKind != OriginalAiAgentKind.District)
            throw new InvalidDataException("original_ai.intelligence_slots.District_actor_scope");
        int requested = Actor.DistrictId ?? throw new InvalidDataException("original_ai.intelligence_slots.District_actor_input");
        return InputsCurrent.ReadDistrictTableReference(requested is >= 0 and <= 51 ? requested : 0)?.PhysicalSlot
            ?? throw new InvalidDataException("original_ai.intelligence_slots.District_actor_pointer");
    }

    private OriginalAiNodeStatus ApplyIntelligenceSlotPreparation(OriginalAiCommandPreparation preparation) =>
        !preparation.Success ? OriginalAiNodeStatus.Failure : preparation.Order is { } order
            ? ApplyNativeCommand(order) : OriginalAiNodeStatus.Success;
}
