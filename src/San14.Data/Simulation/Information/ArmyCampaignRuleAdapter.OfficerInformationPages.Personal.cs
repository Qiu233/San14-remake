using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class OfficerInformationCurrent
    {

        private PreparedCell PersonalCell(Person person, int column)
        {
            var candidate = person.Candidate;
            return column switch
            {
                24 => MakeCell(() => Cells.Doctrine(candidate) ?? Data(1, 0x353),
                    () => PersonalDoctrineKey(person)),
                28 => MakeCell(() => Cells.Age(candidate, true) is int age ? Number(age) : Data(1, 0x353),
                    () => PersonalNumberKey(Cells.AgeKey(candidate))),
                29 => MakeCell(() => Cells.Sex(candidate) switch
                    { 0 => Data(1, 0x495), 1 => Data(1, 0x496), _ => "" },
                    () => new NumberKey(Cells.SexKey(candidate))),
                30 or 31 or 32 or 33 => PersonalRelationCell(person, column switch
                    { 30 => 0xB4, 31 => 0xB6, 32 => 0xBA, _ => 0xBC }),
                186 or 187 or 188 or 189 or 190 or 192 => MakeCell(
                    () => Number(PersonalInnerDisplay(PersonalInnerValue(person, column))),
                    () => new NumberKey(PersonalInnerValue(person, column))),
                191 => MakeCell(() => Data(1, PersonalMaterialPriority(person) switch
                    { 3 => 0x20C, 2 => 0x20D, 1 => 0x20E, _ => 0x353 }),
                    () => new NumberKey(PersonalMaterialPriority(person))),
                _ => throw Missing("personal.column." + column)
            };
        }

        private PreparedCell PersonalRelationCell(Person person, int field) => MakeCell(
            () => Cells.Relation(person.Candidate, field, false),
            () =>
            {
                var key = Cells.RelationKey(person.Candidate, field);
                return new NameKey(key.Text ?? "", key.NotApplicable);
            });

        private static SortKey PersonalNumberKey(CityDomesticPersonCellSource.SortValue key) =>
            new NumberKey(key.Number, key.NotApplicable);

        private SortKey PersonalDoctrineKey(Person person)
        {
            var key = Cells.DoctrineKey(person.Candidate);
            return new NameKey(key.Text ?? "", key.NotApplicable);
        }

        private int PersonalInnerValue(Person person, int column) => column switch
        {
            186 => PersonalDisposition(person),
            187 => PersonalPreference(person, 0x17C),
            188 => PersonalPreference(person, 0x17A),
            189 => PersonalPreference(person, 0x16A),
            190 => PersonalPreference(person, 0x178),
            192 => PersonalPreference(person, 0x180),
            _ => throw Missing("personal.inner.column." + column)
        };

        private int PersonalDisposition(Person person)
        {
            int value = Need(current.ReadAllegianceDisposition(person.PhysicalSlot), "personal.allegiance_disposition");
            return value is >= short.MinValue and <= short.MaxValue ? value
                : throw Missing("personal.allegiance_disposition_range");
        }

        private int PersonalPreference(Person person, int field) =>
            Need(RulerCaptureReleaseInputs.ReadCurrentWord(state, person.PhysicalSlot, field),
                "personal.preference." + field.ToString("X"));

        private static int PersonalInnerDisplay(int value) => value switch
        { -20 => 1, -10 => 2, 0 => 3, 10 => 4, 20 => 5, _ => 0 };

        private int PersonalMaterialPriority(Person person)
        {
            // Both getters stop at the first nonzero preference. The display
            // uses its resource label; sorting uses the independent3/2/1 key.
            if (PersonalPreference(person, 0x170) != 0) return 3;
            if (PersonalPreference(person, 0x172) != 0) return 2;
            return PersonalPreference(person, 0x174) != 0 ? 1 : 0;
        }
    }
}
