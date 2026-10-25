using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState ApplyNormalTurnMonthlyDefectionsCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
        => new MonthlyPersonnelLoyaltySource(this, state, current, pool).RunDefections();

    private sealed partial class MonthlyPersonnelLoyaltySource
    {
        //275B50 processes NPC departures immediately in manager100 order,
        //then sorts and reports the player's deferred departures by Post37.
        internal GameState RunDefections()
        {
            _ = Date;
            if (Elapsed <= P("defection-start-period", 18)) return state;
            RequirePool();
            var candidates = current.CreateTemporaryHexList() ?? throw Missing("defection_temporary");
            foreach (int person in Persons())
            {
                if (Status(person) != 4) continue;
                var force = Force(person);
                if (!N(current.ReadForceNativeValidity(force), "defection_Force_validity")) continue;
                int ruler = RulerOfForce(force);
                if (!Defects(person)) continue;
                if (!IsPlayer(Force(person))) Freedom(person, ruler);
                else
                {
                    var node = current.AllocateRegistryNode(new(candidates.Identity)) ?? throw Missing("defection_temporary_node");
                    if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, new(RegistryKind.Person, person));
                }
            }
            SortDefections(candidates);
            var at = current.ReadHexListHead(candidates) ?? throw Missing("defection_head");
            while (at.Identity.HasValue)
            {
                Sound(7);
                state = package.AppendNormalTurnPersonnelEventCurrent(state, 9);
                int person = DefectionValue(at);
                var force = Force(person);
                int ruler = RulerOfForce(force);
                string statement = Format(0x5E0D, new(Person0: Identity(person), Person1: Identity(ruler)));
                Append([new(null, null) { MonthlyMaintenance = new(MonthlyMaintenanceOperation.ConditionalSpeakerText,
                    null, checked((ushort)Identity(person)), statement) }]);
                state = package.AppendNormalTurnPersonnelEventCurrent(state, null);
                string main = Format(0x5E19, new(Person0: Identity(person), Force0: N(current.ReadForcePhysicalSlot(force), "defection_report_Force60")));
                string detail = Format(0x5239, new(Person0: 5012, Person1: Identity(ruler), Person2: Identity(person)));
                int district = N(ForcePrimaryDistrictSource.Resolve(current, force).PhysicalSlot, "defection_report_primaryDistrict");
                var position = PersonReturnCommands.ResolvePersonPosition(state, current, person, () => Date);
                if (current.ReadOwnedPersonCommandManager() is { } tasks) state = state with { PersonnelCommands = tasks };
                int hex = N(position.PhysicalSlot, "defection_report_Hex");
                var effects = new List<CampaignEffectRequest>();
                state = package.WriteHistory(state, current, new(2, district, hex, Day(), main, detail, 5012,
                    MarkBit0: 1, MarkBit4: 1), effects);
                Append(effects);
                Freedom(person, ruler);
                at = current.ReadHexListNodeNext(at) ?? throw Missing("defection_next");
            }
            current.ClearTemporaryHexList(candidates); current.ReleaseTemporaryHexList(candidates);
            return state;
        }

        private bool Defects(int person)
        {
            var date = Date;
            if (Status(person) != 4) return false;
            int ruler = RulerOfForce(Force(person));
            if ((Flags(person) & 0x4000) != 0 || Loyalty(person) > P("defection-loyalty-limit", 85)) return false;
            if (Spouse(person, ruler) || Sworn(person, ruler)) return false;
            if ((Parent(person, ruler) || Parent(ruler, person)) && OfficeWord(state, person, 0xB2) == OfficeWord(state, ruler, 0xB2)) return false;
            int years = unchecked(date.Year - N(package._governorInputs.ReadCurrentPhysicalWord166(state, person), "defection_join_year166"));
            bool longService = years > 2 || years > 1 && date.Month > 6;
            if (Loyalty(person) >= 60 && !longService) return false;
            //231FC0 is exactly231D90's raw E4 tag, not task position or
            //a fabricated native-ready flag. Only the real City/Gate tags pass.
            if (PersonPositionRepair.DecodeSecondLocation(N(current.ReadPersonSecondLocation(person), "defection_E4")).Kind is not (5 or 6)) return false;
            int probability = DefectionProbability(person);
            return Sample(100, unchecked(date.Year + date.Month + date.Day), Identity(person), 0, 0) < probability;
        }
        private int DefectionProbability(int person)
        {
            int ruler = RulerOfForce(Force(person));
            int maximumLoyalty = P("defection-loyalty-limit", 85);
            byte loyalty = Loyalty(person);
            int affinity = Div(Affinity(person, ruler), P("defection-affinity-divisor", 5));
            int liked = List(person, ruler, like: true) ? 10 : 0;
            int first = Math.Clamp(unchecked(affinity + 1 - liked - loyalty + maximumLoyalty), 0, 50);
            int second = unchecked(first - Disposition(person));
            return second < 0 ? 0 : Math.Min(second, P("defection-probability-limit", 50));
        }
        private void Freedom(int person, int ruler)
        {
            var effects = new List<CampaignEffectRequest>();
            state = package.ApplyCaptiveFreedomCurrent(state, current, pool, person, Date, Day,
                () => sampler ??= package.ReadArmyDestructionSampler(state, current) ?? throw Missing("freedom_sampler"),
                effects, formerRuler: new(ruler));
            Append(effects);
        }
        private bool IsPlayer(ForceReference force)
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("player_Force");
            return N(current.ReadForcePhysicalSlot(player), "player60") != 0 && player == force;
        }
        private int DefectionValue(HexListNodeReference node)
        {
            var value = current.ReadRegistryNodePayload(new(node.Identity));
            return value is { Kind: RegistryKind.Person, PhysicalSlot: int person } ? person : throw Missing("defection_payload");
        }
        private void SortDefections(HexListHandle list)
        {
            var node = current.ReadHexListHead(list) ?? throw Missing("defection_sort_head");
            if (!node.Identity.HasValue) return;
            node = current.ReadHexListNodeNext(node) ?? throw Missing("defection_sort_next");
            while (node.Identity.HasValue)
            {
                var next = current.ReadHexListNodeNext(node) ?? throw Missing("defection_sort_next");
                var previous = current.ReadHexListNodePrevious(node) ?? throw Missing("defection_sort_previous");
                if (Before(DefectionValue(node), DefectionValue(previous)))
                {
                    var insertion = previous;
                    var earlier = current.ReadHexListNodePrevious(previous) ?? throw Missing("defection_sort_previous");
                    while (earlier.Identity.HasValue && Before(DefectionValue(node), DefectionValue(earlier)))
                    { insertion = earlier; earlier = current.ReadHexListNodePrevious(earlier) ?? throw Missing("defection_sort_previous"); }
                    var order = new List<HexListNodeReference>();
                    for (var cursor = current.ReadHexListHead(list) ?? throw Missing("defection_sort_head"); cursor.Identity.HasValue;
                        cursor = current.ReadHexListNodeNext(cursor) ?? throw Missing("defection_sort_next")) order.Add(cursor);
                    order.RemoveAll(value => value.Identity == node.Identity);
                    int index = order.FindIndex(value => value.Identity == insertion.Identity);
                    if (index < 0) throw Missing("defection_sort_insertion");
                    order.Insert(index, node); current.ReorderCurrentList(list, order);
                }
                node = next;
            }
            bool Before(int first, int second)
            {
                int firstPost = N(current.ReadMoraleWavePersonPostE7(first), "defection_PostE7");
                int secondPost = N(current.ReadMoraleWavePersonPostE7(second), "defection_PostE7");
                int a = N(OwnedArmyMoraleWaveInputs.ReadPost37(state, firstPost), "defection_Post37");
                int b = N(OwnedArmyMoraleWaveInputs.ReadPost37(state, secondPost), "defection_Post37");
                return a != b ? a > b : Identity(first) < Identity(second);
            }
        }
        private string Format(int root, HistoryMessageOperands operands)
        { var result = package.FormatNormalTurnReport(state, current, root, operands); state = result.State; return result.Text; }
        private void Sound(int id) => Append([new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.SoundCue, id, null, null, null, null) }]);
        private void Append(IReadOnlyList<CampaignEffectRequest> effects)
        {
            if (effects.Count != 0) state = state with { CampaignEffectRequests = (state.CampaignEffectRequests ?? throw Missing("pending_owner")).Append(
                CampaignEffectSource.MonthlyMaintenance, state.NativeClock?.UpdateOrdinal ?? throw Missing("revision"), effects) };
        }
    }
}
