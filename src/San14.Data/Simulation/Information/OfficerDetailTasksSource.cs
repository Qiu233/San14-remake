using San14.Core;
namespace San14.Data;
public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class OfficerDetailsCurrentReader
    {
        private CurrentPersonTaskSource? personTaskSource;
        private CurrentPersonTaskSource PersonTasks => personTaskSource ??= new(state, current, physical,
            owner._repository, owner._scenario, owner.Seed.Language);
        private string? ReadTaskName() => PersonTasks.ReadTaskName();
        private string? ReadTaskTarget() => PersonTasks.ReadTaskTarget();
        private string? ReadTaskDuration() => PersonTasks.ReadTaskDuration();
        internal string? InformationTaskName() => PersonTasks.InformationTaskName();
        internal int? InformationTaskNameKey() => PersonTasks.InformationTaskNameKey();
        internal string? InformationTaskTarget() => PersonTasks.InformationTaskTarget();
        internal string? InformationTaskDuration() => PersonTasks.InformationTaskDuration();
        internal int? InformationTaskDurationKey() => PersonTasks.InformationTaskDurationKey();
        internal CurrentPersonTaskTarget InformationTaskTargetKey() => PersonTasks.InformationTaskTargetKey();
    }
}
