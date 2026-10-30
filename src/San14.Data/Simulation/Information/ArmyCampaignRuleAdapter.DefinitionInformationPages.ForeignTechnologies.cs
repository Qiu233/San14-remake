namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class DefinitionInformationCurrent
    {
        private IReadOnlyList<Formats.S14ForeignTechnologyDefinition> ForeignTechnologyCatalog() =>
            (rules.informationForeignTechnologies ??= rules._repository.LoadForeignTechnologies(rules._scenario.FixedDataSource.SourcePath)).Definitions;
        private int[] ForeignTechnologyCandidates() => DefinitionTableCandidates(0x85A48, 9, id => id != DefinitionTableAnchor(0x85A48));
        private PreparedCell ForeignTechnologyCell(int id, int column)
        {
            var row = ForeignTechnologyCatalog()[id];
            return column switch
            {
                0 => MakeCell(() => row.Name, () => new NameKey(row.Description)),
                1 => MakeCell(() => Data(0, 0x138B + id - DefinitionTableAnchor(0x85A48))),
                _ => throw Missing("foreign_technology.column." + column)
            };
        }
    }
}
