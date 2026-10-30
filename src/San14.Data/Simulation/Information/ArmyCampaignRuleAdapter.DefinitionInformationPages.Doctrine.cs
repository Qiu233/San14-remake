namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class DefinitionInformationCurrent
    {
        private int[] DoctrineCandidates()
        {
            var ids = new List<int>();
            for (int slot = 0; slot < 11; slot++)
            {
                try
                {
                    int id = (current.ReadStaticTableReference(0x78CF8, slot) ?? throw Missing("doctrine.pointer")).PhysicalSlot ?? 0;
                    if (id != 0 && DoctrineDefinitionText(id, false).Length != 0) ids.Add(id);
                }
                catch (InvalidDataException error) { candidateIssues.Add(error.Message); }
            }
            return ids.ToArray();
        }
        private PreparedCell DoctrineCell(int id, int column) => column switch
        {
            0 => MakeCell(() => DoctrineDefinitionText(id, false),
                () => new NameKey(DoctrineCatalogRow(id).SecondaryText)),
            1 => MakeCell(() => DoctrineDefinitionText(id, true)),
            _ => throw Missing("doctrine.column." + column)
        };
        private Formats.S14DoctrineDefinition DoctrineCatalogRow(int id)
        {
            var catalog = rules._scenario.Snapshot.DoctrineParameters ?? throw Missing("doctrine.catalog");
            if ((uint)id >= (uint)catalog.Doctrines.Count) throw Missing("doctrine.identity");
            return catalog.Doctrines[id];
        }
        private string DoctrineDefinitionText(int id, bool description)
        {
            if (state.Doctrines.TryGetValue(id, out var row)) return description ? row.Description : row.Name;
            if (id is >= 1 and <= 6) throw Missing("doctrine.current_definition");
            var source = DoctrineCatalogRow(id);
            return description ? source.Description : source.Name;
        }
    }
}
