namespace San14.Data;

public sealed partial class CollectionOfficerCatalog
{
    private static bool CanSortColumn(int id) => id is 0 or 182 or 29 or 183 or 184 or 98 or 27 or 24 or
        >= 13 and <= 17 or >= 30 and <= 33 or >= 40 and <= 65 or >= 186 and <= 192;
    private static bool InitialSortAscending(int id) => id is not (183 or 184 or >= 13 and <= 17 or >= 40 and <= 50 or 60 or 27 or >= 186 and <= 192);
    private static bool SpecialForce(Person person) => person.ForceSlot is >= 46 and <= 50;
    private static bool SpecialPersonnel(Person person) =>
        person.Id is (>= 4111 and <= 4120 or >= 4131 and <= 4140 or >= 4151 and <= 4160 or >= 4171 and <= 4180 or >= 4191 and <= 4200) &&
        person.FirstLocation is >= 1 and <= 61 && !SpecialForce(person);

    private (CollectionSortValue Value, int Group) SortKey(Person person, int column)
    {
        static (CollectionSortValue, int) Number(int number) => (new(Number: number), 0);
        static (CollectionSortValue, int) Text(string? key) => key is null
            ? (new(UnresolvedOwner: "collection.officer.name_sort_key"), 0) : (new(Text: key), 0);
        static (CollectionSortValue, int) Inapplicable() => (new(Number: int.MaxValue), 2);
        switch (column)
        {
            case 0: case 182: return NativeValid(person) ? Text(person.Name.NameSortKey) : Inapplicable();
            case 29: return Number(person.Sex);
            case 183: case 184:
                ushort year = column == 183 ? person.Dialogue.BirthYear : unchecked((ushort)person.TerminalYear);
                return person.Id is >= 1 and <= 1000 && year != 0 ? Number(year) : Inapplicable();
            case >= 13 and <= 17: return Number(person.Abilities[column - 13]);
            case >= 40 and <= 50: return Formation(person, column == 50 ? 14 : column - 39) ? Number(0) : Inapplicable();
            case >= 51 and <= 60:
                int tactic = person.Tactics[column - 51];
                return tactic is > 0 and < 201 && skills.TacticAvailable(tactic) ? Number(tactic) : Inapplicable();
            case 98:
                if (!NativeValid(person)) return Text("");
                if (SpecialForce(person) || SpecialPersonnel(person) || (person.Flags & 0x4000) != 0) return Inapplicable();
                return Text(Policy(person)?.SecondaryText ?? "");
            case 27:
                if (SpecialForce(person) || SpecialPersonnel(person)) return Inapplicable();
                var policy = Policy(person);
                return Number(Math.Max(1, policy is null ? 0 : person.RawPolicyCost + (policy.RawCostAddition ?? 0)));
            case 24:
                if (SpecialForce(person) || SpecialPersonnel(person) || (person.Flags & 0x4000) != 0) return Inapplicable();
                return Text(doctrines.Doctrines[person.Doctrine < 7 ? person.Doctrine : 0].SecondaryText);
            case >= 30 and <= 33:
                int relative = column switch { 30 => person.Relations.B4, 31 => person.Relations.B6, 32 => person.Relations.BA, _ => person.Relations.BC };
                if (relative is < 1 or > 10000) return Inapplicable();
                // The normal identity directory's receiver 6000 is its real
                // out-of-range sentinel. Its reset name keys are empty.
                return people.TryGetValue(relative > 6000 ? 6000 : relative, out var relation)
                    ? Text(relation.Name.NameSortKey) : Text("");
            case >= 61 and <= 65: return Number(person.Traits[column - 61]);
            case 186: return Number(person.Growth[1]);
            case 187: return Number(person.Growth[9]);
            case 188: return Number(person.Growth[8]);
            case 189: return Number(person.Growth[0]);
            case 190: return Number(person.Growth[7]);
            case 191: return Number(person.Growth[3] != 0 ? 3 : person.Growth[4] != 0 ? 2 : person.Growth[5] != 0 ? 1 : 0);
            case 192: return Number(person.Growth[11]);
            default: return (new(UnresolvedOwner: $"collection.officer.sort.column_{column}"), 0);
        }
    }

    /// <summary>Sort the current backing order. The original stable consumer
    /// also preserves the dedicated trait predicate's non-strict reverse.</summary>
    public IReadOnlyList<CollectionOfficerRow> SortRows(IReadOnlyList<CollectionOfficerRow> current,
        int column, bool ascending)
    {
        if (!CanSortColumn(column)) throw new ArgumentOutOfRangeException(nameof(column));
        var rows = current.ToList();
        AreaAdministrationAutomaticOrder.StableSort(rows, Before);
        return rows.AsReadOnly();

        bool Before(CollectionOfficerRow left, CollectionOfficerRow right)
        {
            var a = left.Cells[column]; var b = right.Cells[column];
            if (a.SortValue.UnresolvedOwner is { } missing) throw new InvalidDataException(missing);
            if (b.SortValue.UnresolvedOwner is { } otherMissing) throw new InvalidDataException(otherMissing);
            if (column is >= 61 and <= 65)
                return skills.TraitBefore(a.SortValue.Number!.Value, b.SortValue.Number!.Value, ascending);
            if (a.SortGroup != b.SortGroup) return a.SortGroup < b.SortGroup;
            if (a.SortGroup == 2) return false;
            int compared = a.SortValue.Text is { } x && b.SortValue.Text is { } y
                ? PersonNameOrder.Compare(x, y) : a.SortValue.Number!.Value.CompareTo(b.SortValue.Number!.Value);
            return ascending ? compared < 0 : compared > 0;
        }
    }
}
