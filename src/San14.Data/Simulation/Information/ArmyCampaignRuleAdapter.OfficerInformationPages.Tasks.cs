using System.Globalization;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class OfficerInformationCurrent
    {
        private readonly Dictionary<int, OfficerDetailsCurrentReader> taskReaders = [];

        private OfficerDetailsCurrentReader TaskReader(Person person)
        {
            if (taskReaders.TryGetValue(person.Id, out var reader)) return reader;
            return taskReaders[person.Id] = new(rules, state, current, playerForceId, person.Owner, person.PhysicalSlot);
        }

        private PreparedCell TasksCell(Person person, int column) => column switch
        {
            37 => MakeCell(() => TaskReader(person).InformationTaskName(), () =>
            {
                int? key = TaskReader(person).InformationTaskNameKey();
                return new NumberKey(key ?? 0, !key.HasValue);
            }),
            86 => MakeCell(() => TaskReader(person).InformationTaskTarget(),
                () => new TaskTargetKey(TaskReader(person).InformationTaskTargetKey())),
            88 => MakeCell(() => TaskReader(person).InformationTaskDuration(), () =>
            {
                int? key = TaskReader(person).InformationTaskDurationKey();
                return new NumberKey(key ?? 0, !key.HasValue);
            }),
            34 => MakeCell(() => Need(current.ReadPersonNativeValidity(person.PhysicalSlot), "task_returning_validity")
                    ? Data(1, (Need(current.ReadArmyReturnPersonFlags196(person.PhysicalSlot), "task_returning_flags") & 1) == 0
                        ? 0x338 : 0x339) : Data(1, 0x353),
                //34AD00 has no native-validity gate and negates bit0.
                () => new NumberKey(Cells.ReturningKey(person.Candidate))),
            204 => MakeCell(() => Number(Cells.Reset(person.Candidate)), () =>
            {
                var key = Cells.ResetKey(person.Candidate);
                return new NumberKey(key.Number, key.NotApplicable);
            }),
            _ => throw Missing("tasks_column." + column)
        };

        private sealed record TaskTargetKey(CurrentPersonTaskTarget Target) : SortKey
        {
            internal override bool Before(SortKey other, bool ascending)
            {
                if (other is not TaskTargetKey key) throw Missing("task_target_sort_key_type");
                return CurrentPersonTaskSource.BeforeTarget(Target, key.Target, !ascending);
            }
        }
    }

}
