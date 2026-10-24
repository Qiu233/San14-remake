using San14.Core;

namespace San14.Data;

/// <summary>A real2808E0 person attempt. A null FoundPersonPhysical is not a
/// completed search: the caller must continue scenery/item/gold branches.</summary>
internal sealed record TalentSearchPersonReceipt(int[] CandidateOrder, int[] ShuffledOrder,
    int? SelectedPersonPhysical, int? FoundPersonPhysical, int PriorSample, int UsedSample,
    int? PoliticalAbility, int? Trait63, int? Percent);
internal sealed record TalentSearchPersonResult(TalentSearchPersonReceipt? Receipt, string? UnresolvedOwner);

/// <summary>The actual2808E0 discovery leaf over an enclosing operation's current
/// and pool. It neither recruits a found person nor publishes state or the pool.</summary>
internal static class TalentSearchSource
{
    internal const string CandidateCountLimitKey = "simulation.talent-search.candidate-count-limit";
    internal const string DiscoveryDivisorKey = "simulation.talent-search.discovery-divisor";
    internal const string Trait63ScaleKey = "simulation.talent-search.trait63-scale";

    /// <param name="liveDisappearFirstMemberPhysicals">The current2D7900 result, in native
    /// command-manager order: the FIRST list20 person of each live Disappear(v98=16) command. This is not
    /// a scan of unrelated prepared orders or the Person registry.</param>
    internal static TalentSearchPersonResult ResolvePerson(GameState state, SourceWorldContext current,
        int executorPhysical, AreaReference targetArea, IReadOnlyList<int> liveDisappearFirstMemberPhysicals,
        Func<TacticSourceSamplerWorld> readSampler, TacticRemakeCurrentAbilities abilities, int priorSample = -1,
        IReadOnlyList<int>? currentAreaCandidatePhysicals = null)
    {
        HexListHandle? candidates = null, excluded = null;
        TacticSourceSamplerWorld? reachedSampler = null;
        TacticSourceSamplerWorld Sampler() => reachedSampler ??= readSampler();
        try
        {
            int area = Need(targetArea.PhysicalSlot, "target_Area_pointer");
            if (current.ReadOwnedCurrentListPool() is null) throw Missing("current_pool");
            //2808E0 constructs its output temporary before the2D7900 exclusion list.
            candidates = current.CreateTemporaryHexList() ?? throw Missing("candidate_list_allocation");
            excluded = current.CreateTemporaryHexList() ?? throw Missing("exclusion_list_allocation");
            foreach (int physical in liveDisappearFirstMemberPhysicals)
                Append(excluded, new(RegistryKind.Person, physical));
            var excludedPeople = Read(excluded).Select(row => Need(row.Payload.PhysicalSlot, "excluded_Person_pointer")).ToHashSet();
            //2D9750 traverses the independent1851-entry physical table, including0.
            //233780(flag8) is a raw status test with no added Person-validity gate.
            if (currentAreaCandidatePhysicals is not null)
                foreach (int physical in currentAreaCandidatePhysicals) Append(candidates, new(RegistryKind.Person, physical));
            else for (int slot = 0; slot < 1851; slot++)
            {
                var person = current.ReadRegistryTableReference(RegistryKind.Person, slot) ?? throw Missing("Person_table_pointer");
                int physical = Need(person.PhysicalSlot, "Person_table_null_pointer");
                if (Need(current.ReadPersonStatus(physical), "PersonE6") != 8) continue;
                var location = PersonPositionRepair.DecodeSecondLocation(Need(current.ReadPersonSecondLocation(physical), "PersonE4"));
                int? locationArea = location.Kind switch
                {
                    10 => location.Index,
                    5 => BaseArea(current.ReadCityTableReference(location.Index <= 51 ? location.Index : 0)
                        ?? throw Missing("Person_City_pointer")),
                    6 => BaseArea(current.ReadGateTableReference(location.Index <= 10 ? location.Index : 0)
                        ?? throw Missing("Person_Gate_pointer")),
                    _ => null
                };
                if (locationArea != area || excludedPeople.Contains(physical)) continue;
                Append(candidates, new(RegistryKind.Person, physical));
            }
            Release(excluded); excluded = null;
            var nodes = Read(candidates);
            int[] order = nodes.Select(row => Need(row.Payload.PhysicalSlot, "candidate_Person_pointer")).ToArray();
            if (nodes.Count == 0) return new(new(order, [], null, null, priorSample, priorSample, null, null, null), null);
            if (nodes.Count > 1) PersonFreedomHomeSource.Shuffle(current, nodes, Sampler());
            int[] shuffled = nodes.Select(row => Need(row.Payload.PhysicalSlot, "shuffled_Person_pointer")).ToArray();
            int selected = shuffled[0];
            int political = 0, trait = 0;
            //The coefficient is an actual current scalar; no source-image/default fallback.
            int traitScale = Parameter(Trait63ScaleKey);
            if (Need(current.ReadPersonNativeValidity(executorPhysical), "executor_native_validity"))
            {
                int id = Need(current.ReadPersonIdentity(executorPhysical), "executor_Person10");
                political = abilities.ResolveDimension(state, id, 3) ?? throw Missing("executor_politics22FE40");
                trait = TacticRemakeCurrentTraits.CommanderEffectSum(current, executorPhysical, 63)
                    ?? throw Missing("executor_trait63_2B3170");
            }
            int adjusted = unchecked(political + unchecked(unchecked(trait * political) * traitScale) / 100);
            int limit = Parameter(CandidateCountLimitKey), divisor = Parameter(DiscoveryDivisorKey);
            float countFactor = MathF.Sqrt((float)Math.Min(nodes.Count, limit) * 100f);
            float rate = (float)adjusted * countFactor;
            rate /= (float)divisor;
            int percent = Math.Clamp(NativeInt(rate), 0, 100);
            //The original pure sample call occurs even when a prior command sample is reused.
            int sample = TacticEffectSourceSamplerMath.Sample(100, 1,
                Need(current.ReadPersonIdentity(executorPhysical), "sample_executor10"),
                Need(current.ReadPersonIdentity(selected), "sample_selected10"), 0, 0, Sampler());
            if (priorSample != -1) sample = priorSample;
            bool found = sample < percent; //3FCA30 is strict, not <=.
            return new(new(order, shuffled, selected, found ? selected : null, priorSample, sample,
                political, trait, percent), null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
        finally
        {
            if (excluded is not null) Release(excluded);
            if (candidates is not null) Release(candidates);
        }

        int Parameter(string key) => state.NativeArmySimulation?.Parameters is { } bank &&
            !bank.Float32Values.ContainsKey(key) && bank.Int32Values.TryGetValue(key, out int value)
                ? value : throw Missing("current_parameter:" + key);
        void Append(HexListHandle list, RegistryObjectReference payload)
        {
            var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw Missing("temporary_node_allocation");
            if (node.Identity is not null) current.WriteRegistryNodePayload(node, payload);
        }
        List<PersonFreedomHomeSource.Entry> Read(HexListHandle list)
        {
            var rows = new List<PersonFreedomHomeSource.Entry>();
            var node = current.ReadRegistryHead(new(list.Identity)) ?? throw Missing("temporary_head");
            while (node.Identity is int)
            {
                rows.Add(new(new(node.Identity), current.ReadRegistryNodePayload(node) ?? throw Missing("temporary_payload")));
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("temporary_next");
            }
            return rows;
        }
        void Release(HexListHandle list)
        { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
        int BaseArea(BaseReference receiver)
        {
            //22A0E0 uses22D640 then actual Hex12; invalid Object falls through Hex0.
            var hex = CurrentGateMapQueries.FindHex(receiver, current);
            return hex.PhysicalSlot is null ? 0 : Need(current.ReadHexAreaSlot(hex), "Person_Base_Hex12");
        }
    }
    private static int NativeInt(float value) => value is >= int.MinValue and < 2147483648f ? (int)value : int.MinValue;
    private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("talent.search." + field);
}
