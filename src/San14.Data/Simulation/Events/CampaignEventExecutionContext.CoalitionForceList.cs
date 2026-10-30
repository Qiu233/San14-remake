using San14.Data.Formats;

namespace San14.Data;

internal sealed partial class CampaignEventExecutionContext
{
    internal int AddStoryCoalitionForces()
    {
        byte remaining = StoryForceCurrent.StoryCoalitionCountdown ?? throw Missing("coalition_World165C");
        if (remaining == 0) return 1;
        foreach (var entry in Queries.ReadRegisteredConditionForcePointers() ?? throw Missing("coalition_ForceC0"))
        {
            var force = (entry ?? throw Missing("coalition_Force_payload")).Value;
            if (force is null || !StoryForceValid(force)) continue;
            //3A58F0 tests the existing list before reading membership. A
            //disabled coalition or no valid registered receiver never reads it.
            RequireStoryForces();
            if (_storyForcePhysicalSlots.Contains(force.PhysicalSlot)) continue;
            if (Known(Queries.ReadConditionForceCoalitionMembership(force), "coalition_Force12_bit1"))
                AppendStoryForce(force);
        }
        return 1;
    }
}

public sealed partial class SourceWorldContext
{
    internal byte? StoryCoalitionCountdown { get; set; }
}
