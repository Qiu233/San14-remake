using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    /// <summary>The original mode0/resolver8 loop visits the normal City
    /// pointer table in table order through the declared district/force actor.
    /// It does not substitute a global roster or a previously captured list.</summary>
    internal OriginalAiNodeStatus LoopActorCities(uint output,
        IReadOnlyList<Func<OriginalAiOperation, OriginalAiNodeStatus>> children)
    {
        if (Actor.AgentKind is null)
            throw new InvalidDataException("original_ai.city_loop.agent_kind_input");
        // The normal City pointer table has 52 entries, including its sentinel.
        // The source iterator checks the actor/filter, not City native validity.
        for (int tableSlot = 0; tableSlot < 52; tableSlot++)
        {
            // InputsCurrent borrows the existing operation query owner and is
            // refreshed after a command publishes a new candidate. Later slots
            // therefore see current ownership rather than a snapshot roster.
            var city = Adapter.ReadOriginalAiActorCityLoopCurrent(InputsCurrent, Actor, tableSlot);
            if (city is not { } selected) continue;
            SetEntity(output, selected);
            foreach (var child in children)
            {
                var status = child(this);
                if (TryInteger(LoopBreakKey, out int requested) && requested > 0) break;
                if (status != OriginalAiNodeStatus.Success) break;
            }
            // The source's child chain does not clear this flag. The outer
            // element traversal consumes it and leaves the output at this city.
            if (TryInteger(LoopBreakKey, out int stop) && stop > 0)
            {
                SetInteger(LoopBreakKey, 0);
                break;
            }
        }
        // Exhaustion, no matching city, and a requested break all complete the
        // native loop successfully; child failure only ends that element.
        return OriginalAiNodeStatus.Success;
    }
}
