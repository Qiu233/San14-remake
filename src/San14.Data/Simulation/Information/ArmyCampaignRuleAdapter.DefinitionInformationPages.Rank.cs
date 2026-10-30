namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class DefinitionInformationCurrent
    {
        // Original rank-indexed UI threshold data. Rank0 is never a list candidate.
        private static readonly int[] RankDisplayThresholds = [0, 40, 40, 40, 30, 24, 20, 12, 10, 5, 40, 40, 40];
        private readonly Dictionary<int, byte[]> rankDisplayParameters = [];
        private Formats.S14RankCatalog RankCatalog() => rules._scenario.Snapshot.RankParameters ?? throw Missing("rank.catalog");
        private int[] RankCandidates()
        {
            var ids = new List<int>();
            for (int slot = 0; slot < 13; slot++)
            {
                try
                {
                    int id = (current.ReadStaticTableReference(0x78490, slot) ?? throw Missing("rank.pointer")).PhysicalSlot ?? 0;
                    if (id == 0) continue;
                    if (id is 11 or 12 && Need(state.CampaignEventWorld?.ScenarioId, "rank.current_scenario") != 19) continue;
                    if (!state.ForceRanks.ContainsKey(id)) throw Missing("rank.current_definition");
                    ids.Add(id);
                }
                catch (InvalidDataException error) { candidateIssues.Add(error.Message); }
            }
            return ids.ToArray();
        }
        private PreparedCell RankCell(int id, int column)
        {
            var row = state.ForceRanks.GetValueOrDefault(id) ?? throw Missing("rank.current_definition");
            if (column is >= 2 and <= 5)
            {
                int value = column switch { 2 => row.AbilityBonus.Leadership, 3 => row.AbilityBonus.Strength,
                    4 => row.AbilityBonus.Intelligence, _ => row.AbilityBonus.Politics };
                return MakeCell(() => value == 0 ? "" : Number(unchecked((sbyte)value)), () => new NumberKey(value, value == 0));
            }
            if (column is >= 9 and <= 12)
            {
                bool enabled = (RankRawByte(id, 162) & (1 << (column - 9))) != 0;
                return MakeCell(() => enabled ? Data(1, 0x338) : "", () => new NumberKey(1, !enabled));
            }
            return column switch
            {
                0 => MakeCell(() => row.Name, () => new NameKey(RankCatalog().Ranks[id].Pinyin)),
                1 => MakeCell(() => Number(row.CommandLimit), () => new NumberKey(row.CommandLimit)),
                7 => MakeCell(() => RankCatalog().Ranks[id].Description),
                8 => MakeCell(() => Number(RankRawByte(id, 163)), () => new NumberKey(RankRawByte(id, 163))),
                13 => MakeCell(() => Number(RankRawByte(id, 165)), () => new NumberKey(RankRawByte(id, 165))),
                14 => MakeCell(() => Number(RankDisplayThreshold(id)), () => new NumberKey(RankDisplayThreshold(id))),
                _ => throw Missing("rank.column." + column)
            };
        }
        private int RankRawByte(int id, int offset)
        {
            var catalog = RankCatalog();
            if ((uint)id >= (uint)catalog.Ranks.Count) throw Missing("rank.identity");
            if (!rankDisplayParameters.TryGetValue(id, out var bytes))
                rankDisplayParameters.Add(id, bytes = Convert.FromHexString(catalog.Ranks[id].RawBytesHex));
            if ((uint)offset >= (uint)bytes.Length) throw Missing("rank.source_field." + offset);
            return bytes[offset];
        }
        private static int RankDisplayThreshold(int id) => (uint)id < (uint)RankDisplayThresholds.Length
            ? RankDisplayThresholds[id] : throw Missing("rank.threshold_identity");
    }
}
