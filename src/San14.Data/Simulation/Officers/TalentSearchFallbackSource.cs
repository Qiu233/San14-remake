using San14.Core;

namespace San14.Data;

internal enum TalentSearchSceneryControl { Ordinary, SpecialWithoutDiscovery, SpecialWithDiscovery }
internal sealed record TalentSearchGemPosition(ushort Area, byte Remaining);
/// <summary>Actual missing current owners only; none may return a guessed default.</summary>
internal sealed record TalentSearchFallbackInputs(
    Func<AreaReference, ushort?> ReadAreaWord36,
    Func<GemReference, int, TalentSearchGemPosition?> ReadGemPosition,
    Func<TalentSearchSceneryControl?> ReadSceneryControl,
    Func<NativeMapSeasonDate> ReadDate);
internal enum TalentSearchFallbackKind { Scenery, Item, Gem, Gold, NoOrdinaryFind }
/// <summary>NoOrdinaryFind still requires the caller's138E/1390 special outcome
/// checks and the real command report/merit/cleanup tail.</summary>
internal sealed record TalentSearchFallbackReceipt(TalentSearchFallbackKind Kind,
    int? PhysicalSlot, int Gold, int SampleAfter, string Branch);
internal sealed record TalentSearchFallbackResult(TalentSearchFallbackReceipt? Receipt, string? UnresolvedOwner);
internal sealed record TalentSearchFallbackTrialResult(TalentSearchFallbackReceipt? Receipt,
    int SampleAfter, string? UnresolvedOwner);

/// <summary>Ordinary280D60,27FB30,280710 selection and numbers, in source order.
/// The caller owns the actual positive writer, reports and one final publication.
/// All temporary descriptors belong to the supplied current pool.</summary>
internal static class TalentSearchFallbackSource
{
    private const string Prefix = "simulation.talent-search.";
    //Advisor2F5CB0 calls these independent queries with separate initial samples.
    //A miss does not run the next family or write a gameplay outcome.
    internal static TalentSearchFallbackTrialResult TryScenery(GameState state, SourceWorldContext current,
        int executorPhysical, AreaReference targetArea, int priorSample,
        Func<TacticSourceSamplerWorld> readSampler, TacticRemakeCurrentAbilities abilities,
        TalentSearchFallbackInputs inputs)
        => TryOne(true, state, current, executorPhysical, targetArea, priorSample, readSampler, abilities, inputs);

    internal static TalentSearchFallbackTrialResult TryItem(GameState state, SourceWorldContext current,
        int executorPhysical, AreaReference targetArea, int priorSample,
        Func<TacticSourceSamplerWorld> readSampler, TacticRemakeCurrentAbilities abilities,
        TalentSearchFallbackInputs inputs)
        => TryOne(false, state, current, executorPhysical, targetArea, priorSample, readSampler, abilities, inputs);

    private static TalentSearchFallbackTrialResult TryOne(bool scenery, GameState state, SourceWorldContext current,
        int executorPhysical, AreaReference targetArea, int priorSample,
        Func<TacticSourceSamplerWorld> readSampler, TacticRemakeCurrentAbilities abilities,
        TalentSearchFallbackInputs inputs)
    {
        var operation = new Operation(state, current, executorPhysical, targetArea,
            priorSample, readSampler, abilities, inputs);
        try { return new(scenery ? operation.Scenery() : operation.Item(), operation.SampleAfter, null); }
        catch (InvalidDataException error) { return new(null, operation.SampleAfter, error.Message); }
    }

    internal static TalentSearchFallbackTrialResult TryGold(GameState state, SourceWorldContext current,
        int executorPhysical, AreaReference targetArea, int priorSample,
        Func<TacticSourceSamplerWorld> readSampler, TacticRemakeCurrentAbilities abilities,
        TalentSearchFallbackInputs inputs)
    {
        var operation = new Operation(state, current, executorPhysical, targetArea,
            priorSample, readSampler, abilities, inputs);
        try { return new(operation.Gold(), operation.SampleAfter, null); }
        catch (InvalidDataException error) { return new(null, operation.SampleAfter, error.Message); }
    }

    internal static TalentSearchFallbackResult Resolve(GameState state, SourceWorldContext current,
        int executorPhysical, AreaReference targetArea, int priorSample,
        Func<TacticSourceSamplerWorld> readSampler, TacticRemakeCurrentAbilities abilities,
        TalentSearchFallbackInputs inputs)
    {
        try
        {
            var operation = new Operation(state, current, executorPhysical, targetArea,
                priorSample, readSampler, abilities, inputs);
            return new(operation.Scenery() ?? operation.Item() ?? operation.Gold(), null);
        }
        catch (InvalidDataException error) { return new(null, error.Message); }
    }

    private sealed class Operation(GameState state, SourceWorldContext current, int executor,
        AreaReference area, int initialSample, Func<TacticSourceSamplerWorld> readSampler,
        TacticRemakeCurrentAbilities abilities, TalentSearchFallbackInputs inputs)
    {
        private int sample = initialSample;
        internal int SampleAfter => sample;
        private TacticSourceSamplerWorld? sampler;
        private TacticSourceSamplerWorld Sampler() => sampler ??= readSampler();
        private int Area() => Need(area.PhysicalSlot, "target_Area_pointer");
        private ushort AreaWord36() => Need(inputs.ReadAreaWord36(area), "target_Area36");
        private ushort ExecutorId() => Need(current.ReadPersonIdentity(executor), "executor_Person10");
        private int P(string suffix) => Parameter(Prefix + suffix);
        private int Parameter(string key) => state.NativeArmySimulation?.Parameters is { } bank &&
            !bank.Float32Values.ContainsKey(key) && bank.Int32Values.TryGetValue(key, out int value)
                ? value : throw Missing("current_parameter:" + key);
        private int Politics()
        {
            int scale = Parameter(TalentSearchSource.Trait63ScaleKey);
            if (!Need(current.ReadPersonNativeValidity(executor), "executor_native_validity")) return 0;
            int political = abilities.ResolveDimension(state, ExecutorId(), 3) ?? throw Missing("current_politics22FE40");
            int trait = TacticRemakeCurrentTraits.CommanderEffectSum(current, executor, 63) ?? throw Missing("current_trait63");
            return unchecked(political + unchecked(unchecked(trait * political) * scale) / 100);
        }
        private int Chance(int political, int count, int? limit, int divisor, int? secondary = null)
        {
            float chance = (float)political * MathF.Sqrt((float)(limit is int cap ? Math.Min(count, cap) : count) * 100f);
            chance /= (float)divisor;
            if (secondary is int extra) chance /= (float)extra;
            return Math.Clamp(NativeInt(chance), 0, 100);
        }
        private int SampleArea() => TacticEffectSourceSamplerMath.Sample(100, 1, ExecutorId(), AreaWord36(), 0, 0, Sampler());
        private TalentSearchFallbackReceipt Found(TalentSearchFallbackKind kind, int? physical, string branch, int gold = 0)
            => new(kind, physical, gold, sample, branch);

        internal TalentSearchFallbackReceipt? Scenery()
        {
            var list = Temporary();
            try
            {
                for (int slot = 0; slot < 101; slot++)
                {
                    var scenery = current.ReadSceneryTableReference(slot) ?? throw Missing("Scenery_table_pointer");
                    if (!Need(current.ReadSceneryNativeValidity(scenery), "Scenery_validity")) continue;
                    if ((Need(current.ReadSceneryFlags47(scenery), "Scenery47") & 1) != 0) continue;
                    ushort hexId = Need(current.ReadSceneryHexIndex(scenery), "Scenery44");
                    var hex = current.ReadHexTableReference(hexId < 48400 ? hexId : 0) ?? throw Missing("Scenery_Hex_pointer");
                    int sceneryArea = hex.PhysicalSlot is null ? 0 : Need(current.ReadHexAreaSlot(hex), "Scenery_Hex12");
                    if (sceneryArea != Area()) continue;
                    byte force = Need(current.ReadHexForceSlot(hex), "Scenery_Hex14");
                    if (force != Need(current.ReadPersonOldForceSlot(executor), "executor_Force22CF60")) continue;
                    Append(list, new RegistryObjectReference(RegistryKind.Scenery,
                        Need(scenery.PhysicalSlot, "Scenery_physical")));
                }
                var nodes = Read(list);
                if (nodes.Count == 0) return null;
                var control = inputs.ReadSceneryControl() ?? throw Missing("Scenery_special_mode570850_570840");
                if (control == TalentSearchSceneryControl.SpecialWithoutDiscovery) return null;
                if (control == TalentSearchSceneryControl.SpecialWithDiscovery)
                    return Found(TalentSearchFallbackKind.Scenery, Physical(nodes[0].Payload), "scenery-special");
                if (nodes.Count > 1) Shuffle(nodes);
                int political = Politics();
                int percent = Chance(political, nodes.Count, P("scenery-count-limit"), P("scenery-discovery-divisor"));
                int fresh = SampleArea();
                if (sample == -1) sample = fresh;
                return sample < percent
                    ? Found(TalentSearchFallbackKind.Scenery, Physical(nodes[0].Payload), "scenery-ordinary") : null;
            }
            finally { Release(list); }
        }

        internal TalentSearchFallbackReceipt? Item()
        {
            //The source captures22CB40 before allocating its four category lists.
            var force = current.ReadPersonForce(executor) ?? throw Missing("item_executor_Force");
            var ordinary = Temporary(); HexListHandle? type12 = null, type13 = null, gems = null;
            try
            {
                type12 = Temporary(); type13 = Temporary();
                for (int slot = 0; slot < 202; slot++)
                {
                    var item = current.ReadRegistryTableReference(RegistryKind.Item, slot) ?? throw Missing("Item_table_pointer");
                    int physical = Need(item.PhysicalSlot, "Item_physical");
                    if (Need(current.ReadRegistryItemRawField50(item), "Item50") != 2) continue;
                    if (state.Items is not { } items || !items.TryGetValue(physical, out var owned)) throw Missing("current_Item_owner");
                    if (Need(owned.Lifecycle?.RetainedLocationCode, "Item4C") != Area()) continue;
                    byte itemCategory = DuelNumericParameters.ReadItemProtectionCategory(state, physical);
                    Append(itemCategory == 12 ? type12 : itemCategory == 13 ? type13 : ordinary, item);
                }
                gems = Temporary();
                for (int slot = 0; slot < 31; slot++)
                {
                    var gem = current.ReadSkillGemReference(slot) ?? throw Missing("Gem_table_pointer");
                    _ = Need(gem.PhysicalSlot, "Gem_physical");
                    for (int i = 0; i < 5; i++)
                    {
                        var position = inputs.ReadGemPosition(gem, i) ?? throw Missing("Gem_search_location3E_active48");
                        if (position.Area != Area() || position.Remaining == 0) continue;
                        Append(gems, gem); break;
                    }
                }
                var ordinaryNodes = Read(ordinary); var type12Nodes = Read(type12);
                var type13Nodes = Read(type13); var gemNodes = Read(gems);
                if (ordinaryNodes.Count + type12Nodes.Count + type13Nodes.Count + gemNodes.Count == 0) return null;
                int fresh = SampleArea();
                int trialSample = sample == -1 ? fresh : sample;
                int ordinaryWeight = ordinaryNodes.Count == 0 ? 0 : P("item-ordinary-category-weight");
                int type13Weight = type13Nodes.Count == 0 ? 0 : P("item-type13-category-weight");
                int type12Weight = type12Nodes.Count == 0 ? 0 : P("item-type12-category-weight");
                int category;
                List<Node> selected;
                if (trialSample <= ordinaryWeight) { category = 1; selected = ordinaryNodes; }
                else if (trialSample <= unchecked(ordinaryWeight + type13Weight)) { category = 2; selected = type13Nodes; }
                else if (trialSample <= unchecked(unchecked(ordinaryWeight + type13Weight) + type12Weight)) { category = 3; selected = type12Nodes; }
                else { category = 0; selected = gemNodes; }
                //A category can be selected with no nodes (notably a zero sample
                //and zero weight). The original does not substitute another list.
                if (selected.Count == 0) return null;
                int political = Politics();
                int? percent = null;
                if (category is 0 or 1)
                    percent = Chance(political, selected.Count, P("item-count-limit"), P("item-discovery-divisor"));
                else if (category == 2)
                {
                    int cities = PersonArmyCaptureSource.ReadOwnedCityCount(current, force) ?? throw Missing("item_Force_CityCount22AB60");
                    if (cities >= P("item-type13-city-threshold"))
                        percent = Chance(political, selected.Count, null, P("item-discovery-divisor"), P("item-type13-divisor"));
                }
                else percent = Chance(political, selected.Count, null, P("item-discovery-divisor"), P("item-type12-divisor"));
                if (percent is not null && selected.Count > 1) Shuffle(selected);
                sample = trialSample;
                if (percent is not int chance || sample >= chance) return null;
                return Found(category == 0 ? TalentSearchFallbackKind.Gem : TalentSearchFallbackKind.Item,
                    Physical(selected[0].Payload), category == 0 ? "gem" : $"item-category{category}");
            }
            finally
            {
                if (gems is not null) Release(gems);
                if (type13 is not null) Release(type13);
                if (type12 is not null) Release(type12);
                Release(ordinary);
            }
        }

        internal TalentSearchFallbackReceipt Gold()
        {
            int political = Politics();
            NativeMapSeasonDate date = inputs.ReadDate();
            int fresh = TacticEffectSourceSamplerMath.Sample(100, 1,
                unchecked(date.Year + date.Month + date.Day), ExecutorId(), 0, 0, Sampler());
            if (fresh >= Math.Max(political / 2, 1))
                return Found(TalentSearchFallbackKind.NoOrdinaryFind, null, "gold-miss-specials-still-required");
            int multiplier = TacticEffectSourceSamplerMath.Sample(3, 1, ExecutorId(), Area(), 0, 0, Sampler());
            int additive = SampleArea();
            int gold = unchecked(unchecked((multiplier + 1) * political) + additive);
            if (Need(current.ReadPersonNativeValidity(executor), "gold_executor_validity"))
            {
                var force = current.ReadPersonForce(executor) ?? throw Missing("gold_executor_Force");
                if (Need(current.ReadForceNativeValidity(force), "gold_Force_validity"))
                {
                    int slot = Need(current.ReadForcePhysicalSlot(force), "gold_Force_physical");
                    byte flags = Need(state.NativeArmySimulation?.Forces.GetValueOrDefault(slot)?.Flags12, "gold_Force12");
                    int bonus = (flags & 8) != 0 ? P("gold-force-flag-additive") : 0;
                    int percent = (flags & 16) != 0 ? P("gold-force-flag-multiplier") : 100;
                    gold = unchecked(unchecked(gold + bonus) * percent) / 100;
                }
            }
            return Found(TalentSearchFallbackKind.Gold, null, "gold", gold);
        }

        private HexListHandle Temporary()
        {
            if (current.ReadOwnedCurrentListPool() is null) throw Missing("current_pool");
            return current.CreateTemporaryHexList() ?? throw Missing("temporary_allocation");
        }
        private void Append(HexListHandle list, object payload)
        {
            var node = current.AllocateHexListNode(list);
            if (node.Identity is int identity) current.WriteCurrentListPayload(identity, payload);
        }
        private sealed record Node(HexListNodeReference Reference, object Payload);
        private List<Node> Read(HexListHandle list)
        {
            var result = new List<Node>();
            var node = current.ReadRegistryHead(new(list.Identity)) ?? throw Missing("temporary_head");
            while (node.Identity is int identity)
            {
                result.Add(new(new(identity), current.ReadCurrentListPayload(identity) ?? throw Missing("temporary_payload")));
                node = current.ReadRegistryNodeNext(node) ?? throw Missing("temporary_next");
            }
            return result;
        }
        private void Shuffle(List<Node> nodes)
        {
            //1D7220 swaps payloads in actual nodes. GemReference remains a Gem,
            //rather than being disguised as an Army/Person registry identity.
            int salt = 0, count = nodes.Count;
            for (int rounds = (count / 2) * count; rounds > 0; rounds--)
            {
                int a = TacticEffectSourceSamplerMath.Sample(count, 1, salt, 0, 0, 0, Sampler());
                int b = count - 1 - TacticEffectSourceSamplerMath.Sample(count, 1, salt + 1, 0, 0, 0, Sampler());
                salt = unchecked(salt + 2);
                if (a == b) continue;
                var left = nodes[a]; var right = nodes[b];
                current.WriteCurrentListPayload(left.Reference.Identity!.Value, right.Payload);
                current.WriteCurrentListPayload(right.Reference.Identity!.Value, left.Payload);
                nodes[a] = left with { Payload = right.Payload }; nodes[b] = right with { Payload = left.Payload };
            }
        }
        private void Release(HexListHandle list)
        { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
        private static int Physical(object payload) => payload switch
        {
            RegistryObjectReference reference => Need(reference.PhysicalSlot, "selected_registry_pointer"),
            GemReference reference => Need(reference.PhysicalSlot, "selected_Gem_pointer"),
            _ => throw Missing("selected_pointer_kind")
        };
    }
    private static int NativeInt(float value) => value is >= int.MinValue and < 2147483648f ? (int)value : int.MinValue;
    private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("talent.search.fallback." + field);
}
