namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class DefinitionInformationCurrent
    {
        private Formats.S14CreditRankCatalog CreditRankCatalog() =>
            (rules.informationCreditRanks ??= rules._repository.LoadCreditRanks(rules._scenario.FixedDataSource.SourcePath)).Catalog;
        private int[] CreditRankCandidates() => DefinitionTableCandidates(0x85DB8, 7, id => id != DefinitionTableAnchor(0x85DB8));
        private PreparedCell CreditRankCell(int id, int column)
        {
            var row = CreditRankCatalog().Ranks[id];
            return column switch
            {
                0 => MakeCell(() => row.Name, () => new NumberKey(id - DefinitionTableAnchor(0x85DB8))),
                1 => MakeCell(() => Number(row.SignedThreshold), () => new NumberKey(row.SignedThreshold)),
                2 => MakeCell(() => Number(row.DoctrinePointAddition), () => new NumberKey(row.DoctrinePointAddition)),
                3 => MakeCell(() => Data(1, row.RawTailValue == 0 ? 0x339 : 0x338), () => new NumberKey(row.RawTailValue == 0 ? 0 : 1)),
                _ => throw Missing("credit_rank.column." + column)
            };
        }
    }
}
