using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class StrategicResolutionCurrent
    {
        private ForceReference AreaForce(int area)
            => ForceAt(StrategyNeed(current.ReadAreaCurrentForceSlot(new(area)), "resolution_Area_Force"));

        //1FB2E0. A genuine second-stage Army allocation is required before
        //a great success can be reported; the ordinary success has no Army.
        private int ResolveIncitement()
        {
            int executor = Executor, area = Target;
            var own = Force(executor); var other = AreaForce(area);
            int ownRuler = Ruler(own), otherRuler = Ruler(other);
            int result = SavedResult(Trial());
            if (task.SavedResult != -1 && result == 2 &&
                package.StrategicCommandIncitementSuccessLimitedCurrent(State, current, area)) result = 1;
            int bandForce = 51;
            if (result == 2)
            {
                var generated = package.ApplyStrategicIncitementArmyCurrent(State, current, area, executor);
                State = generated.State; bandForce = generated.BandForceId;
                if (!generated.Created) result = 1;
            }
            if (Player(own))
                History(result == 0 ? 0x3122 : result == 2 ? bandForce is >= 46 and <= 50 ? 0x3118 : 0x3113 : 0x311D,
                    new(Person0: Id(executor), Person1: Id(ownRuler), Person2: Id(otherRuler), Area0: area),
                    result == 0 ? 0x3128 : 0x3127,
                    new(Person0: Id(executor), Area0: area),
                    StrategyNeed(current.ReadPersonDistrictSlot(executor), "placate_DistrictE0"), Id(executor), result != 0);
            else if (result != 0)
            {
                byte cityId = StrategyNeed(current.ReadAreaCitySlot(new(area)), "placate_Area35");
                var city = current.ReadCityTableReference(cityId < 52 ? cityId : 0) ?? throw StrategyMissing("placate_City_pointer");
                if (Player(ForceAt(StrategyNeed(current.ReadBaseForceSlot(city), "placate_City_Force"))))
                    History(result == 2 ? 0x3129 : 0x312A, new(Person0: 5012, Person1: Id(otherRuler), Person2: Id(ownRuler), Area0: area),
                        result == 2 ? 0x312B : 0x312C, new(Person0: Id(ownRuler), Person1: Id(otherRuler), Area0: area),
                        Primary(other), 5012, false, conditional: true, sound: result == 2 ? 7 : 6);
            }

            var loss = IncitementLoss(executor, result);
            if (result != 0)
            {
                //The original temporary integer list really allocates its
                //three nodes; suppressed allocations are not a synthetic list.
                var list = current.CreateTemporaryHexList() ?? throw StrategyMissing("placate_development_list");
                try
                {
                    for (int dimension = 1; dimension <= 3; dimension++)
                    {
                        var node = current.AllocateHexListNode(list);
                        if (node.Identity is int identity) current.WriteCurrentListPayload(identity, dimension);
                    }
                    var nodes = ReadIntegerNodes(list);
                    ShuffleIntegerNodes(nodes);
                    var owner = State.Areas.GetValueOrDefault(area) ?? throw StrategyMissing("placate_Area_owner");
                    var values = owner.Development ?? throw StrategyMissing("placate_development_owner");
                    int selected = 0, largest = 0;
                    foreach (var node in nodes)
                    {
                        int value = node.Value switch { 1 => StrategyNeed(values.Commerce, "placate_Commerce3C"),
                            2 => StrategyNeed(values.Agriculture, "placate_Agriculture3E"),
                            _ => StrategyNeed(values.Barracks, "placate_Barracks40") };
                        if (value > largest) { largest = value; selected = node.Value; }
                    }
                    var field = selected == 1 ? CeAreaDevelopmentField.Commerce :
                        selected == 2 ? CeAreaDevelopmentField.Agriculture : CeAreaDevelopmentField.Barracks;
                    var writer = new OwnedAreaDevelopmentContext(State, current, package._normalAftermathInputs);
                    int before = StrategyNeed(writer.ReadAreaProgress(new(area), field), "placate_selected_development");
                    package._normalAftermathInputs.Development.WriteCurrentProgress(new(area), field,
                        Math.Clamp(unchecked(before - loss.Development), 0, 1000), 1, writer);
                    State = writer.State;
                    Security(area, -loss.Security);
                }
                finally { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
                if (result != 1) return result;
            }
            else Security(area, -loss.Security);
            Relations(own, other, -P("placate-relation-loss", 5));
            return result;
        }

        //1DF200 consumes one App sample on failure and two on success. The
        //non-player random base is a separate unestablished BSS current value.
        private (int Development, int Security) IncitementLoss(int executor, int result)
        {
            int intelligence = Ability(executor);
            int trait = Trait(executor, 65) > 0 ? P("placate-trait-loss", 1) : 0;
            int bonus = DifficultyBonus(Force(executor), "placate", [2, 2, 1, 1]);
            int development = 0, security;
            if (result == 0)
            {
                int divisor = P("placate-failed-security-divisor", 25);
                security = App(unchecked(bonus + Divide(unchecked(trait * P("placate-trait-offset", 20) -
                    P("placate-intelligence-offset", 30) + intelligence), divisor)));
            }
            else
            {
                int difference = unchecked(intelligence - P("placate-intelligence-offset", 30));
                development = unchecked(App(unchecked(bonus + Divide(difference,
                    P(result == 2 ? "placate-great-development-divisor" : "placate-development-divisor", result == 2 ? 10 : 15)))) +
                    trait + P("placate-development-loss-base", 5));
                security = unchecked(App(unchecked(bonus + Divide(difference,
                    P(result == 2 ? "placate-great-security-divisor" : "placate-security-divisor", result == 2 ? 15 : 20)))) +
                    trait + P("placate-security-loss-base", 4));
            }
            return (development, security);
        }

        private int DifficultyBonus(ForceReference force, string family, int[] sourceValues)
        {
            int selector = StrategyNeed(BattleRemakeCurrentSettings.ReadSelectorE58(State), "resolution_settings_E58");
            int difficulty = StrategyNeed(BattleRemakeCurrentSettings.ReadInt32(State, selector == 0 ? 0x134 : 0x28), "resolution_difficulty");
            if (!Player(force))
            {
                string key = StrategicParameterPrefix + "resolution." + family + "-npc-random-base";
                var bank = State.NativeArmySimulation?.Parameters ?? throw StrategyMissing("resolution_parameter_owner");
                return !bank.Float32Values.ContainsKey(key) && bank.Int32Values.TryGetValue(key, out int value)
                    ? value : throw StrategyMissing(key);
            }
            int index = difficulty == 0 ? 0 : difficulty == 1 ? 1 : difficulty == 2 ? 2 : 3;
            return P(family + "-player-difficulty-" + index, sourceValues[index]);
        }
        private static int Divide(int dividend, int divisor)
        {
            if (divisor == 0 || dividend == int.MinValue && divisor == -1) throw StrategyMissing("resolution_divisor");
            return dividend / divisor;
        }
        private void Security(int area, int delta)
        {
            var owner = State.Areas.GetValueOrDefault(area) ?? throw StrategyMissing("resolution_Area_owner");
            int after = Math.Clamp(unchecked(StrategyNeed(owner.Security, "resolution_Area54") + delta), 0, 100);
            if (ForceId(AreaForce(area)) is >= 46 and <= 50) return;
            State = State with { Areas = new(State.Areas) { [area] = owner with { Security = after } } };
        }

        private sealed record IntegerNode(int Identity, int Value);
        private List<IntegerNode> ReadIntegerNodes(HexListHandle list)
        {
            var result = new List<IntegerNode>();
            var node = current.ReadRegistryHead(new(list.Identity)) ?? throw StrategyMissing("integer_list_head");
            while (node.Identity is int identity)
            {
                if (current.ReadCurrentListPayload(identity) is not int value) throw StrategyMissing("integer_list_payload");
                result.Add(new(identity, value));
                node = current.ReadRegistryNodeNext(node) ?? throw StrategyMissing("integer_list_next");
            }
            return result;
        }
        private void ShuffleIntegerNodes(List<IntegerNode> nodes)
        {
            if (nodes.Count < 2) return;
            var sampler = package.ReadArmyDestructionSampler(State, current) ?? throw StrategyMissing("integer_shuffle_sampler");
            int salt = 0, count = nodes.Count;
            for (int rounds = (count / 2) * count; rounds > 0; rounds--)
            {
                int a = TacticEffectSourceSamplerMath.Sample(count, 1, salt, 0, 0, 0, sampler);
                int b = count - 1 - TacticEffectSourceSamplerMath.Sample(count, 1, salt + 1, 0, 0, 0, sampler);
                salt = unchecked(salt + 2);
                if (a == b) continue;
                var left = nodes[a]; var right = nodes[b];
                current.WriteCurrentListPayload(left.Identity, right.Value);
                current.WriteCurrentListPayload(right.Identity, left.Value);
                nodes[a] = left with { Value = right.Value }; nodes[b] = right with { Value = left.Value };
            }
        }
    }
}
