using San14.Data.Formats;

namespace San14.Data;

public static partial class CampaignEventExecution
{
    private sealed partial class StagingRuntime
    {
        internal void BindStoryCityTargets()
        {
            if (_story is not null)
            {
                _story.BindStoryTargetCityReader(conditionRuntime.ReadStoryTargetCitySelection);
                conditionRuntime.BindStoryCityResolver(selector =>
                    new S14EventConditionCityPointer(_story.ResolveStoryCitySelector(selector).PhysicalSlot));
            }
        }

        private int ExecuteStoryCityListOperation(S14ConditionInstruction operation)
        {
            BindStoryCityTargets();
            return conditionRuntime.ExecuteStoryCityList(operation.Code, operation.RawArguments,
                selector => new S14EventConditionCityPointer(Story.ResolveStoryCitySelector(selector).PhysicalSlot),
                selector => Story.ResolvePerson(selector, conditionRuntime.ReadMessageTargetForce),
                selector => Story.ResolveResultForce(selector, conditionRuntime.ReadMessageTargetForce));
        }
    }
}
