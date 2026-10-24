namespace San14.Data;

/// <summary>The two three-cell cumulative choices in the credit promotion draft.</summary>
internal static class CreditChoiceSource
{
    internal static byte Original(CreditCommandPerson person) =>
        checked((byte)(person.FirstSelectionCount | person.SecondSelectionCount << 4));

    internal static bool IsLegal(CreditCommandPerson person, byte code)
    {
        int first = code & 15, second = code >> 4;
        return first <= 3 && second <= 3 && first >= person.FirstSelectionCount &&
            second >= person.SecondSelectionCount && first + second <= person.SelectionSlotCount;
    }

    //6DB2A0: 0 unselected, 1 available/new selection, 2 original/locked, 3 unavailable.
    internal static IReadOnlyList<int> QueryChoices(CreditCommandPerson person, byte code)
    {
        if (!IsLegal(person, code)) throw new InvalidDataException("personnel-credit.choice_code");
        int first = code & 15, second = code >> 4;
        var cells = new[] { 3, 3, 3, 3, 3, 3 };
        for (int index = 0; index < Math.Min(person.SelectionSlotCount, 3); index++)
        {
            cells[index] = index < first ? (index < person.FirstSelectionCount ? 2 : 1) : 0;
            cells[index + 3] = index < second ? (index < person.SecondSelectionCount ? 2 : 1) : 0;
        }
        if (first + second < person.SelectionSlotCount)
        {
            if (first < 3) cells[first] = 1;
            if (second < 3) cells[second + 3] = 1;
        }
        //6DB2A0's independent tutorial constraint removes only the next cell.
        if (person.TutorialChoiceRestriction)
        {
            if (person.Id == 0xB9) { if (first < 3) cells[first] = 3; }
            else if (second < 3) cells[second + 3] = 3;
        }
        return cells;
    }

    internal static byte ToggleChoice(CreditCommandPerson person, byte code, int group)
    {
        if ((uint)group >= 6 || QueryChoices(person, code)[group] != 1)
            throw new InvalidDataException("personnel-credit.choice_unavailable");
        int first = code & 15, second = code >> 4, index = group % 3;
        if (group < 3) first = first == index + 1 ? index : index + 1;
        else second = second == index + 1 ? index : index + 1;
        byte result = checked((byte)(first | second << 4));
        if (!IsLegal(person, result)) throw new InvalidDataException("personnel-credit.choice_code");
        return result;
    }

    internal static byte ApplyAutomatic(CreditCommandPerson person, int mode,
        Func<bool> readPrefersCivilAbilities)
    {
        if (mode is < 3 or > 5) throw new InvalidDataException("personnel-credit.automatic_mode");
        //6C5C50 reads the four actual abilities before checking remaining capacity.
        bool second = mode == 3 ? readPrefersCivilAbilities() : mode == 5;
        int firstCount = person.FirstSelectionCount, secondCount = person.SecondSelectionCount;
        if (firstCount + secondCount < person.SelectionSlotCount)
        {
            if (second) secondCount++; else firstCount++;
        }
        return checked((byte)(firstCount | secondCount << 4));
    }
}
