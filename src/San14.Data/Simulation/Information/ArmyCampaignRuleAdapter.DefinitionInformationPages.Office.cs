namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class DefinitionInformationCurrent
    {
        private int[] OfficeCandidates()
        {
            int title = OfficeCurrentTitle();
            var ids = new List<int>(); var names = new HashSet<string>(StringComparer.Ordinal);
            for (int slot = 0; slot < 256; slot++)
            {
                try
                {
                    int id = (current.ReadStaticTableReference(0x784F8, slot) ?? throw Missing("office.pointer")).PhysicalSlot ?? 0;
                    if (id == 73 || !Need(OwnedArmyMoraleWaveInputs.ReadPostNativeValidity(state, id), "office.validity")) continue;
                    if (Need(OwnedArmyMoraleWaveInputs.ReadPostCommandCapacity(state, id), "office.command_capacity") == 0) continue;
                    int flags = OfficeSavedByte(OwnedArmyGovernorComparatorInputs.OfficeFlags3DKey(id));
                    int bit = title switch { 6 => 1, 44 => 2, 45 => 4, _ => 0 };
                    bool special = bit != 0 && (flags & bit) != 0;
                    if (!special && (flags & 7) != 0) continue;
                    string name = (state.OfficerOffices.GetValueOrDefault(id) ?? throw Missing("office.current_definition")).Name;
                    if (special && names.Contains(name)) continue;
                    ids.Add(id); names.Add(name);
                }
                catch (InvalidDataException error) { candidateIssues.Add(error.Message); }
            }
            int none = (current.ReadStaticTableReference(0x784F8, 73) ?? throw Missing("office.none_pointer")).PhysicalSlot ?? 0;
            if (state.OfficerOffices.ContainsKey(none)) ids.Add(none);
            else candidateIssues.Add(Missing("office.none_definition").Message);
            return ids.ToArray();
        }
        private PreparedCell OfficeCell(int id, int column)
        {
            var row = state.OfficerOffices.GetValueOrDefault(id) ?? throw Missing("office.current_definition");
            if (column is >= 3 and <= 6)
            {
                int Value() => column switch
                {
                    3 => Need(OwnedArmyMoraleWaveInputs.ReadPost38(state, current, id), "office.leadership"),
                    4 => row.AbilityBonus.Strength,
                    5 => row.AbilityBonus.Intelligence,
                    _ => row.AbilityBonus.Politics
                };
                int value = Value();
                return MakeCell(() => value != 0 ? Number(unchecked((sbyte)value)) : "",
                    () => new NumberKey(value, value == 0));
            }
            return column switch
            {
                0 => MakeCell(() => row.Name, () => new NumberKey(id)),
                1 => MakeCell(() => Number(Need(OwnedArmyMoraleWaveInputs.ReadPostCommandCapacity(state, id), "office.command_capacity")),
                    () => new NumberKey(Need(OwnedArmyMoraleWaveInputs.ReadPostCommandCapacity(state, id), "office.command_capacity"))),
                2 => MakeCell(() => Number(OfficeSalary(id)), () => new NumberKey(OfficeSalary(id))),
                9 => MakeCell(() => OfficeRequiredRankName(id), () => new NumberKey(OfficeRequiredRank(id))),
                _ => throw Missing("office.column." + column)
            };
        }
        private int OfficeCurrentTitle()
        {
            var pointer = current.ReadForceTableReference(playerForceId) ?? throw Missing("office.player_force_pointer");
            return Need(current.ReadForceKingdomNameIndex(Need(pointer.PhysicalSlot, "office.player_force")), "office.player_title");
        }
        private int OfficeSavedByte(string key)
        {
            var values = state.NativeArmySimulation?.Parameters ?? throw Missing("office.parameters");
            if (values.Float32Values.ContainsKey(key) || !values.Int32Values.TryGetValue(key, out int value) || (uint)value > byte.MaxValue)
                throw Missing("office.saved_byte." + key);
            return value;
        }
        private int OfficeSalary(int id) => unchecked(OfficeSavedByte(OwnedArmyGovernorComparatorInputs.OfficeGuard36Key(id)) *
            NativeMonthlySalaryRules.ForLanguage(rules.Seed.Language).OfficeMultiplier);
        private int OfficeRequiredRank(int id)
        {
            int grade = Need(OwnedArmyMoraleWaveInputs.ReadPost37(state, id), "office.grade");
            for (int slot = 0; slot < 13; slot++)
            {
                int rank = (current.ReadStaticTableReference(0x78490, slot) ?? throw Missing("office.rank_pointer")).PhysicalSlot ?? 0;
                if (rank != 0 && Need(OwnedArmyMoraleWaveInputs.ReadRankAC(state, rank), "office.rank_grade") == grade) return rank;
            }
            return 0;
        }
        private string OfficeRequiredRankName(int id)
        {
            int flags = OfficeSavedByte(OwnedArmyGovernorComparatorInputs.OfficeFlags3DKey(id));
            if ((flags & 1) != 0) return "";
            int title = OfficeCurrentTitle();
            if (title == 6 || (flags & 2) != 0 || title == 44 || (flags & 4) != 0 || title == 45) return "";
            int rank = OfficeRequiredRank(id);
            return state.ForceRanks.GetValueOrDefault(rank)?.Name ??
                (rank == 0 ? RankCatalog().Ranks[0].Name : throw Missing("office.required_rank_definition"));
        }
    }
}
