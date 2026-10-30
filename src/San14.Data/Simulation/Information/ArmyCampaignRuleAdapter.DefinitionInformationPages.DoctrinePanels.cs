namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class DefinitionInformationCurrent
    {
        private Formats.S14DoctrinePanelCatalog DoctrinePanelCatalog() =>
            (rules.informationPanels ??= rules._repository.LoadDoctrinePanels(rules._scenario.FixedDataSource.SourcePath)).Catalog;
        private int[] DoctrinePanelCandidates() => DefinitionTableCandidates(0x85F20, 80,
            id => id != DefinitionTableAnchor(0x85F20) && id - DefinitionTableAnchor(0x85F20) != 1 && DoctrinePanelCatalog().Panels[id].RawBytes[18] != 7);
        private PreparedCell DoctrinePanelCell(int id, int column)
        {
            var row = DoctrinePanelCatalog().Panels[id];
            return column switch
            {
                0 => MakeCell(() => row.Name, () => new NumberKey(id - DefinitionTableAnchor(0x85F20))),
                1 => MakeCell(() => Data(0, 0x17B9 + id - DefinitionTableAnchor(0x85F20))),
                2 or 4 => MakeCell(() => DoctrinePanelRequirementName(row, column == 2 ? 19 : 21),
                    () => new NumberKey(row.RawBytes[column == 2 ? 19 : 21])),
                3 or 5 => MakeCell(() => DoctrinePanelRequirementAmount(row, column == 3 ? 19 : 21),
                    () => new NumberKey(row.RawBytes[column == 3 ? 20 : 22])),
                _ => throw Missing("doctrine_panel.column." + column)
            };
        }
        private int? DoctrinePanelRequirementDoctrine(Formats.S14DoctrinePanelDefinition row, int offset)
        {
            int raw = row.RawBytes[offset];
            return (current.ReadStaticTableReference(0x78CF8, raw < 7 ? raw : 0) ?? throw Missing("doctrine_panel.requirement_pointer")).PhysicalSlot;
        }
        private string DoctrinePanelRequirementName(Formats.S14DoctrinePanelDefinition row, int offset) =>
            DoctrinePanelRequirementDoctrine(row, offset) is int id && id != 0 ? DoctrineDefinitionText(id, false) : Data(1, 0x353);
        private string DoctrinePanelRequirementAmount(Formats.S14DoctrinePanelDefinition row, int offset) =>
            DoctrinePanelRequirementDoctrine(row, offset) is int id && id != 0 ? Number(row.RawBytes[offset + 1]) : Data(1, 0x353);
        // The native registry stores pointer identity separately from definition ordinal.
        private int DefinitionTableAnchor(int table) => Need((current.ReadStaticTableReference(table, 0) ?? throw Missing("definition.anchor_pointer")).PhysicalSlot, "definition.anchor_receiver");
        private int[] DefinitionTableCandidates(int table, int count, Func<int, bool> include)
        {
            var ids = new List<int>();
            for (int slot = 0; slot < count; slot++)
            {
                try
                {
                    int? id = (current.ReadStaticTableReference(table, slot) ?? throw Missing("definition.table_pointer")).PhysicalSlot;
                    if (id is int physical && include(physical)) ids.Add(physical);
                }
                catch (InvalidDataException error) { candidateIssues.Add(error.Message); }
            }
            return ids.ToArray();
        }
    }
}
