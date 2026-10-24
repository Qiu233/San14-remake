using San14.Core;

namespace San14.Data;

/// <summary>
/// The ordered generation banks built by3294A0 before335D00. A generation
/// session copies these lists and owns their subsequent shuffle order.
/// </summary>
internal sealed class PersonnelHireGenerationCatalog
{
    private readonly IReadOnlyList<int>[][] _traits;
    private readonly IReadOnlyList<int>[] _tactics;

    public IReadOnlyList<int> TypeCodes { get; } = Array.AsReadOnly(new[] { 1, 2, 3, 4 });
    public IReadOnlyList<int> FormationIds { get; }

    private PersonnelHireGenerationCatalog(List<int> formations, List<int>[][] traits, List<int>[] tactics)
    {
        FormationIds = formations.AsReadOnly();
        _traits = traits.Select(bySex => bySex.Select(bank => (IReadOnlyList<int>)bank.AsReadOnly()).ToArray()).ToArray();
        _tactics = tactics.Select(bank => (IReadOnlyList<int>)bank.AsReadOnly()).ToArray();
    }

    public IReadOnlyList<int> TraitIds(int type, int sex)
    {
        CheckType(type);
        if (sex is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(sex));
        return _traits[type][sex];
    }

    public IReadOnlyList<int> TacticIds(int type)
    {
        CheckType(type);
        return _tactics[type];
    }

    internal static PersonnelHireGenerationCatalog Load(GameState state, SourceWorldContext current)
    {
        var formations = new List<int>();
        var traits = Enumerable.Range(0, 5).Select(_ => new[] { new List<int>(), new List<int>() }).ToArray();
        var tactics = Enumerable.Range(0, 5).Select(_ => new List<int>()).ToArray();

        //2340E0 admits every nonzero formation identity; C0 chooses this bank.
        for (int slot = 0; slot < 21; slot++)
        {
            var formation = current.ReadStaticTableReference(0x77428, slot)
                ?? throw Missing("formation_pointer");
            if (formation.PhysicalSlot is not int physical || physical == 0) continue;
            if (TacticRemakeCurrentFormations.ReadRawByte(state, physical, 0xC0)
                is not byte classification) throw Missing("formation_generation_class");
            if (classification == 1) formations.Add(physical);
        }

        //234380: custom traits256..355 additionally require their current B6.
        //The E0 category is independent of the trait's ordinary effect code.
        for (int slot = 0; slot < 356; slot++)
        {
            var trait = current.ReadPersonalityTableReference(slot) ?? throw Missing("trait_pointer");
            if (!(current.ReadSkillPersonalityValidity(trait) ?? throw Missing("trait_validity"))) continue;
            int physical = trait.PhysicalSlot ?? throw Missing("trait_identity");
            byte classification = current.ReadPersonalityByte(trait, 0xE0)
                ?? throw Missing("trait_generation_class");
            switch (classification)
            {
                case >= 1 and <= 4:
                    traits[classification][0].Add(physical);
                    traits[classification][1].Add(physical);
                    break;
                case 5:
                    for (int type = 2; type <= 4; type++) traits[type][1].Add(physical);
                    break;
                case 6:
                    for (int type = 1; type <= 4; type++) traits[type][1].Add(physical);
                    break;
                case 9:
                    for (int type = 1; type <= 4; type++) traits[type][0].Add(physical);
                    for (int type = 1; type <= 4; type++) traits[type][1].Add(physical);
                    break;
            }
        }

        //234610 admits every nonzero tactic identity. Each bank keeps original
        //table iteration order; no sorting, deduplication or random draw occurs.
        for (int slot = 0; slot < 201; slot++)
        {
            var tactic = current.ReadSkillTacticReference(slot) ?? throw Missing("tactic_pointer");
            if (!(current.ReadSkillTacticValidity(tactic) ?? throw Missing("tactic_validity"))) continue;
            int physical = tactic.PhysicalSlot ?? throw Missing("tactic_identity");
            byte classification = current.ReadSkillTacticRawByte(tactic, 0x88)
                ?? throw Missing("tactic_generation_class");
            if (classification is 1 or 3)
            {
                tactics[1].Add(physical);
                tactics[2].Add(physical);
            }
            if (classification is 2 or 3)
            {
                tactics[3].Add(physical);
                tactics[4].Add(physical);
            }
        }

        //Type0 banks are genuinely reset-empty in3294A0, rather than unknown.
        return new(formations, traits, tactics);
    }

    private static void CheckType(int type)
    {
        if (type is < 0 or > 4) throw new ArgumentOutOfRangeException(nameof(type));
    }

    private static InvalidDataException Missing(string field) =>
        new($"Personnel hire generation catalog requires current owner: {field}.");
}
