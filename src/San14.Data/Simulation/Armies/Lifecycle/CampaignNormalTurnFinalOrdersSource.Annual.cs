using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class NormalTurnFinalOrdersSource
    {
        //290B10: the annual competition between the three paired doctrines.
        //Its first population pass runs even before the elapsed-game gate.
        private void AnnualDoctrineCompetition()
        {
            var forces = Temporary();
            try
            {
                int minimum = 0; bool any = false;
                for (int slot = 0; slot < 52; slot++)
                {
                    var force = Force(slot);
                    if (!Ordinary(force)) continue;
                    int id = Id(force), before = N(Owner(id).PopulationTotal, "population_previous1C0"), population = 0;
                    foreach (var area in Areas(force))
                        if (N(current.ReadAreaNativeValidity(area), "population_Area_validity"))
                            population = unchecked(population + N(state.Areas.GetValueOrDefault(N(area.PhysicalSlot, "population_Area"))?.NativeNormalAftermath?.Quantity52, "population_Area52"));
                    int percent = NativeRound((float)unchecked(population < before ? before - population : population - before) / before * 100f);
                    int change = population < before ? -Math.Min(percent, P("population-decline-limit", 30)) : Math.Min(percent, P("population-growth-limit", 30));
                    AddScore(id, change);
                    WriteForce(id, value => value with { PopulationTotal = Math.Max(population, 0) });
                    Add(forces, RegistryKind.Force, id);
                    int score = Score(id);
                    minimum = any ? Math.Min(minimum, score) : score; any = true;
                }
                if (Elapsed <= 0) return;
                var active = Values(forces, RegistryKind.Force).Select(x => x.Physical).ToList();
                int[] totals = new int[3];
                foreach (int id in active)
                {
                    int group = Doctrine(new(id)) switch { 1 or 6 => 0, 2 or 3 => 1, 4 or 5 => 2, _ => -1 };
                    if (group >= 0) totals[group] = unchecked(totals[group] + Score(id));
                }
                int maximum = totals.Max();
                var leadingGroups = new List<int>();
                for (int i = 0; i < 3; i++) if (totals[i] == maximum) leadingGroups.Add(i);
                //24AD90 permutes whole three-word records with the same
                //pure salt progression as1D7220, not App or an extra draw.
                ShuffleRecords(leadingGroups);
                int first = leadingGroups[0] switch { 0 => 1, 1 => 2, _ => 4 };
                int second = PairedDoctrine(first);
                int? losingForce = null;
                if (active.Count > 2)
                {
                    var candidates = Temporary();
                    try
                    {
                        foreach (int id in active)
                            if (Doctrine(new(id)) is int doctrine && doctrine != first && doctrine != second && Score(id) <= minimum)
                                Add(candidates, RegistryKind.Force, id);
                        if (Values(candidates, RegistryKind.Force).Count != 0) losingForce = Shuffle(candidates, RegistryKind.Force)[0];
                    }
                    finally { Release(candidates); }
                }
                var best = Temporary();
                var processed = Temporary();
                try
                {
                    int bestScore = 0; bool bestFound = false;
                    foreach (int id in active)
                    {
                        Add(processed, RegistryKind.Force, id);
                        WriteForce(id, value => value with { Flags12 = (byte)(Flags(id) & 0xC7) });
                        var force = new ForceReference(id);
                        int doctrine = Doctrine(force);
                        if (doctrine == first || doctrine == second)
                        {
                            WriteForce(id, value => value with { Flags12 = (byte)(Flags(id) | 8) });
                            foreach (var area in Areas(force)) current.WriteAreaFlags38(area, (byte)(AreaFlags(area) & 0xF0));
                            foreach (int person in ForcePeople(force))
                            {
                                if (!PersonValid(person)) continue;
                                int personId = PersonId(person);
                                var owner = state.Officers.GetValueOrDefault(personId) ?? throw Missing("annual_Person_owner");
                                int raw = owner.DoctrineId ?? owner.AbilityState?.ObservedDoctrineId ?? throw Missing("annual_Person14C");
                                if (raw == Doctrine(force))
                                {
                                    int amount = P("annual-loyalty", 10);
                                    if (amount > 0) IncreaseLoyalty(person, Ruler(current.ReadPersonForce(person) ?? throw Missing("annual_Person_Force")), amount);
                                }
                            }
                            foreach (int other in active)
                                if (Values(processed, RegistryKind.Force).Any(x => x.Physical == other) &&
                                    (Doctrine(new(other)) == first || Doctrine(new(other)) == second))
                                    state = package.ApplyNormalTurnForceRelationCurrent(state, current, pool, force, new(other), P("annual-relation", 30));
                            AddReserve(id, Doctrine(force), P("annual-doctrine-reserve", 1));
                            AddReserve(id, PairedDoctrine(Doctrine(force)), P("annual-doctrine-reserve", 1));
                            int score = Score(id);
                            if (!bestFound || score > bestScore)
                            { bestFound = true; bestScore = score; current.ClearTemporaryHexList(best); Add(best, RegistryKind.Force, id); }
                            else if (score == bestScore) Add(best, RegistryKind.Force, id);
                        }
                        if (losingForce is int loser && Valid(new ForceReference(loser)) && loser == id)
                            WriteForce(id, value => value with { Flags12 = (byte)(Flags(id) | 0x10) });
                        SetScore(id, 0);
                    }
                    //8976B0 only refreshes the header view. Numerical owners
                    //above already hold the source result; no display ACK.
                    if (Values(best, RegistryKind.Force).Count == 0) throw Missing("annual_empty_best_native_head");
                    int chosen = Shuffle(best, RegistryKind.Force)[0];
                    int chosenDoctrine = Doctrine(new(chosen));
                    string? firstName = null, secondName = null;
                    int paired = PairedDoctrine(chosenDoctrine);
                    if (chosenDoctrine != 0 && paired != 0)
                    {
                        firstName = DoctrineName(chosenDoctrine); secondName = DoctrineName(paired);
                        Scene(31); Sound(5);
                        Narration(Format(0x5EE1, new(Force0: chosen, String0: firstName, String1: secondName)));
                        Narration(Format(0x5EE2, new(String0: firstName, String1: secondName)));
                        Scene(null);
                    }
                    if (losingForce is int losing && Valid(new ForceReference(losing)))
                    {
                        Scene(10); Sound(18); Narration(Format(0x5EE0, new(Force0: losing))); Scene(null);
                    }
                    var primary = ForcePrimaryDistrictSource.Resolve(current, Player);
                    if (N(current.ReadDistrictNativeValidity(primary), "annual_primary_validity"))
                    {
                        int speaker = Speaker();
                        if (PersonValid(speaker))
                        {
                            bool playerLeading = Valid(Player) && (Flags(Id(Player)) & 8) != 0;
                            var operands = new HistoryMessageOperands(Person0: PersonId(speaker),
                                Person1: PersonId(Person(N(current.ReadForceRecord(Id(Player))?.RawRuler10, "annual_player_ruler"))),
                                String0: playerLeading ? null : firstName ?? DoctrineName(chosenDoctrine),
                                String1: playerLeading ? null : secondName ?? DoctrineName(paired));
                            History(Format(playerLeading ? 0x53DB : 0x53E3, operands), speaker, 0, true);
                        }
                    }
                    if (Valid(Player) && (Flags(Id(Player)) & 8) != 0) RecordLeadingDoctrineYear();
                    //3EA490 is the platform achievement service, outside the
                    //campaign model. It is not a second numerical writer.
                }
                finally { Release(processed); Release(best); }
            }
            finally { Release(forces); }
        }

        private void AddReserve(int force, int doctrine, int amount)
        {
            if (doctrine is < 1 or > 6) return;
            var owner = state.ForceCreditReserves ?? throw Missing("reserve_owner");
            if (!owner.PointsByForce.TryGetValue(force, out var values) || values.IsDefault || values.Length != 6) throw Missing("reserve_vector");
            state = state with { ForceCreditReserves = owner with { PointsByForce = owner.PointsByForce.SetItem(force,
                values.SetItem(doctrine - 1, (byte)Math.Clamp(unchecked(values[doctrine - 1] + amount), 0, 99))) } };
        }
        private static int PairedDoctrine(int doctrine) => doctrine switch { 1 => 6, 2 => 3, 3 => 2, 4 => 5, 5 => 4, 6 => 1, _ => 0 };
        private string DoctrineName(int doctrine) => state.Doctrines.GetValueOrDefault(doctrine)?.Name ?? throw Missing("doctrine_name");
        private void ShuffleRecords<T>(List<T> records)
        {
            int size = records.Count, salt = 0;
            for (int rounds = unchecked(size / 2 * size); rounds > 0; rounds--)
            {
                int a = Sample(size, salt++), b = size - Sample(size, salt++) - 1;
                if (a != b) (records[a], records[b]) = (records[b], records[a]);
            }
        }
        private static int NativeRound(float value)
        {
            float rounded = MathF.Round(value, MidpointRounding.AwayFromZero);
            return !float.IsFinite(rounded) || rounded < int.MinValue || rounded >= 2147483648f ? int.MinValue : (int)rounded;
        }
        private void RecordLeadingDoctrineYear()
        {
            var simulation = state.NativeArmySimulation ?? throw Missing("leading_year_simulation");
            byte before = N(simulation.World.LeadingDoctrineYearCount, "leading_year_count17CC");
            state = state with { NativeArmySimulation = simulation with { World = simulation.World with { LeadingDoctrineYearCount = unchecked((byte)(before + 1)) } } };
            var player = Player;
            int ruler = Person(N(current.ReadForceRecord(Id(player))?.RawRuler10, "leading_year_ruler"));
            var home = BaseLookup.Find(N(current.ReadPersonFirstLocation(ruler), "leading_year_HomeE2"), current);
            var rulerForce = current.ReadPersonForce(ruler) ?? throw Missing("leading_year_Force");
            string name = package.HistoryWriter.CustomFirstName(state, current, rulerForce, Name);
            int color = N(current.ReadForceReportColor(rulerForce), "leading_year_color");
            string label = package.HistoryWriter.FallbackName(state, current, player, Name);
            state = new CampaignChronicleWriter(package.HistoryWriter).Write(state, current,
                new(0x14E, 1, Day, name, color, 0x6E, ImmutableArray.Create(label, string.Empty, string.Empty),
                    ImmutableArray.Create(current.ReadBaseCurrentHexIndex(home), 48400, 48400, 48400)),
                package.ReadApplicationGameplay()?.UserSelectionServicePresent);
            static string Name(OfficerState person, bool full)
            {
                string surname = Head(person.CurrentSurname ?? throw Missing("chronicle_surname"));
                return full ? surname + Head(person.CurrentGivenName ?? throw Missing("chronicle_given_name")) : surname;
            }
            static string Head(string value) => value.IndexOf('\0') is int at && at >= 0 ? value[..at] : value;
        }
    }
}
