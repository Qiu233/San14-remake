using San14.Data.Formats;

namespace San14.Data;

public static partial class CampaignEventExecution
{
    private sealed partial class StagingRuntime
    {
        private int StageMessageMonthAndProvince(S14ConditionInstruction operation)
        {
            var arguments = operation.RawArguments;
            if (arguments.Count != 8) throw new InvalidDataException("story.month-province.operands");
            if (operation.Code == 258)
            {
                //Untouched slots retain their actual old value, including unknown.
                string?[] values = Names?.Cast<string?>().ToArray() ?? new string?[5];
                for (int index = 0; index < 5; index++)
                {
                    int month = arguments[index];
                    if (unchecked((uint)(month - 1)) >= 13) continue;
                    if (month == 13)
                        month = (persons as IS14BasicEventConditionQueries)?.ReadConditionDate()?.Month
                            ?? throw new InvalidDataException("story.month-parameters.current_third_date");
                    values[index] = storyInputs?.ReadMonthMessageName?.Invoke(month)
                        ?? throw new InvalidDataException("story.month-parameters.resource_name");
                }
                //Names is the existing optional bank. Do not manufacture an
                //empty value for an unknown retained slot.
                Names = Array.AsReadOnly(values.Select(value => value!).ToArray());
                return 0;
            }
            if (operation.Code != 370) throw new InvalidDataException("story.month-province.opcode");
            int raw = arguments[0];
            if (raw < 0) throw new InvalidDataException("story.province-parameters.negative_table_selector");
            var selected = persons.ReadConditionStateTableSlot(raw < 23 ? raw : 0)
                ?? throw new InvalidDataException("story.province-parameters.State_lookup");
            if (!(persons.ReadConditionStateValidity(selected)
                ?? throw new InvalidDataException("story.province-parameters.State_validity"))) return 0;
            int physical = selected.PhysicalSlot
                ?? throw new InvalidDataException("story.province-parameters.State_receiver");
            provinceParameters ??= new int?[10];
            provinceParameters[0] = physical;
            return 1;
        }
    }
}
