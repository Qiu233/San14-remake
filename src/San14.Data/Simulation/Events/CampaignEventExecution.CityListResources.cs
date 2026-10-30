using San14.Data.Formats;

namespace San14.Data;

public static partial class CampaignEventExecution
{
    private sealed partial class StagingRuntime
    {
        // Execution372 has the same actual ordered City-list producer as its
        // condition owner. The existing cursor already retains that owner.
        public void PrepareCityListGroup(S14ConditionContext context)
            => conditionRuntime.PrepareCityListGroup(context);

        private int ExecuteStoryCityListResourceMutation(S14ConditionInstruction operation)
        {
            // Rebind after restore as well as fresh entry. This borrows the
            // condition owner's actual list for this synchronous business call.
            Story.BindStoryCitySelections(conditionRuntime.ReadStoryCitySelections());
            return ExecuteWorldMutation(operation);
        }
    }
}

internal sealed partial class CampaignEventExecutionContext
{
    private IReadOnlyList<S14EventConditionCityPointer>? _storyCitySelections;
    internal IReadOnlyList<S14EventConditionCityPointer> StoryCitySelections
        => _storyCitySelections ?? throw Missing("City list business owner");
    internal void BindStoryCitySelections(IReadOnlyList<S14EventConditionCityPointer> cities)
        => _storyCitySelections = cities;
}
