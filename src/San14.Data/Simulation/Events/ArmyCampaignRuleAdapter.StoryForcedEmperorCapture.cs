using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

//Explicit invocation values override independently saved threshold inputs.
//No numeric value is inferred from an uninitialized virtual PE image.
internal sealed record StoryForcedEmperorCaptureInputs(int? LoyalOfficerTendencyMinimum,
    int? UnfavorableRulerTendencyMaximum, int? ReceptionChronicleSuppression);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The explicit reception/abolition forms of the City capture
    /// emperor service. The enclosing transfer owns this current, pool and
    /// MonthlyMaintenance effect sink. Automatic mode zero stays with its caller.</summary>
    internal GameState ApplyStoryForcedEmperorCaptureCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, BaseReference city,
        ForceReference? oldForce, ForceReference newForce, int mode,
        List<CampaignEffectRequest> effects, StoryForcedEmperorCaptureInputs? inputs = null)
        => new ForcedEmperorCaptureSource(this, state, current, pool, city, oldForce,
            newForce, mode, effects, EmperorCaptureConfiguration.ResolveForced(state, inputs)).Run();

    private sealed partial class ForcedEmperorCaptureSource(ArmyCampaignRuleAdapter package,
        GameState state, SourceWorldContext current, WorldIndexContext pool,
        BaseReference city, ForceReference? oldForce, ForceReference newForce, int mode,
        List<CampaignEffectRequest> effects, StoryForcedEmperorCaptureInputs? inputs)
    {
        private readonly List<HexListHandle> temporaries = [];

        internal GameState Run()
        {
            if (mode is not (1 or 2)) throw Missing("explicit_mode");
            if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw Missing("current_pool");
            int raw = Need(CityCaptureTerritoryInputs.ReadEmperor1654(state), "Emperor1654");
            if (raw == 0) return state;
            int emperor = Lookup(raw);
            var position = PersonPositionRepair.DecodeSecondLocation(Need(current.ReadPersonSecondLocation(emperor), "Emperor_E4"));
            //The source has no Person-validity gate before these two tests.
            if (position.Kind != 5 || position.Index != Need(city.PhysicalSlot, "City_identity")) return state;
            int owner = Need(newForce.PhysicalSlot, "new_Force_identity");
            int ruler = Lookup(Need(current.ReadForceRecord(owner)?.RawRuler10, "new_Force_ruler10"));
            try
            {
                Reports(emperor, ruler, owner);
                var people = Temporary();
                foreach (int person in ForcePeople(owner))
                {
                    bool admitted = mode == 1
                        //A ruler fails233780(person,1) independently of the
                        //threshold. These pure predicates have no random or
                        //allocation effect, so preserve that known no-append
                        //result without demanding unrelated configuration.
                        ? Need(current.ReadPersonStatus(person), "loyalty_status") != 1 &&
                            Tendency(person) >= Need(inputs?.LoyalOfficerTendencyMinimum, "configuration.loyal_officer_tendency_minimum")
                        : !Related(person, ruler) && Tendency(person) >= 10 && Identity(person) != Identity(ruler);
                    if (admitted) Append(people, RegistryKind.Person, person);
                }
                foreach (var entry in Read(people, RegistryKind.Person))
                {
                    int person = entry.Physical;
                    byte before = Need(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, person, package._governorInputs), "Person_E8");
                    if (mode == 2 ? before > 100 : before >= 100) continue;
                    int value = mode == 2 ? Math.Clamp(before - 10, 0, 255) : Math.Clamp(before + Math.Min(100 - before, 10), 0, 255);
                    if (Need(OfficerBehaviorRules.PreservesMaximumLoyalty(current.ReadPersonBehavior(person)), "Person_flags15E_bit13")) value = 255;
                    current.SetPersonDestructionLoyaltyE8(person, (byte)value);
                    state = ArmyDestructionLoyalty.PublishCurrentRawE8(state, current, person, (byte)value);
                }

                var candidates = Temporary();
                foreach (int other in ReadAttackRequestForceOrder(current))
                {
                    if (other == owner) continue;
                    if (oldForce is not null && Need(current.ReadForceNativeValidity(oldForce), "excluded_Force_validity") &&
                        other == Need(oldForce.PhysicalSlot, "excluded_Force_identity")) continue;
                    int otherRuler = Lookup(Need(current.ReadForceRecord(other)?.RawRuler10, "candidate_Force_ruler10"));
                    if (Tendency(otherRuler) < 10) continue;
                    if (mode == 2 && Relation(other, owner) == 0) continue;
                    Append(candidates, RegistryKind.Force, other);
                }
                if (mode == 2)
                {
                    foreach (int other in ShuffleForces(candidates)) ChangeRelation(owner, other, -20);
                    WriteEmperor(0);
                }
                else if (Read(candidates, RegistryKind.Force).Count != 0)
                {
                    //Only a nonempty first candidate list allocates the two
                    //classification lists; both retain native allocation order.
                    var favorable = Temporary();
                    var unfavorable = Temporary();
                    foreach (var entry in Read(candidates, RegistryKind.Force))
                    {
                        int otherRuler = Lookup(Need(current.ReadForceRecord(entry.Physical)?.RawRuler10, "classification_ruler10"));
                        bool negative = UnfavorableRuler(otherRuler, ruler);
                        Append(negative ? unfavorable : favorable, RegistryKind.Force, entry.Physical);
                    }
                    foreach (int other in ShuffleForces(unfavorable)) ChangeRelation(owner, other, -10);
                    foreach (var entry in Read(favorable, RegistryKind.Force)) ChangeRelation(owner, entry.Physical, 5);
                }
                return state;
            }
            finally
            {
                for (int i = temporaries.Count - 1; i >= 0; i--)
                {
                    Lock(true);
                    try { current.ClearTemporaryHexList(temporaries[i]); current.ReleaseTemporaryHexList(temporaries[i]); }
                    finally { Lock(false); }
                }
            }
        }

        private void Reports(int emperor, int ruler, int owner, bool automatic = false)
        {
            int emperorId = Identity(emperor), rulerId = Identity(ruler);
            if (mode == 2 && !automatic)
            {
                var date = state.NativeMapSeason?.ThirdDate ?? throw Missing("World34_date");
                var notice = Format(0x2AEC, new(Person0: rulerId, Person1: emperorId, Integers: [date.Year, date.Month]));
                effects.Add(new(null, null) { TalentCommandPresentation = new(TalentCommandPresentationOperation.Announcement,
                    null, null, null, null, notice) });
            }
            string main = Format(mode == 2 ? 0x2AED : 0x2B18, new(Person0: rulerId, Person1: emperorId));
            var district = ForcePrimaryDistrictSource.Resolve(current, newForce);
            var abilities = new TacticRemakeCurrentAbilities(current, package._scenario, package.Seed.Language,
                SimulationRuleIdentity.ForLocale(package.Seed.Language));
            int speaker = Need(new PersonDeathHistorySelection(state, current, pool, abilities,
                package._governorInputs, (force, effect) => package._baseCapacityInputs.ReadPanelModifier(state, force, effect))
                .Select(district).PhysicalSlot, "report_speaker_pointer");
            string detail = Format(mode == 2 ? 0x5310 : 0x5316,
                new(Person0: Identity(speaker), Person1: rulerId, Person2: emperorId, Force0: owner,
                    Person3: mode == 1 ? rulerId : null));
            if (!package.TalentForceIsPlayerCurrent(state, current, newForce))
            {
                int districtId = Need(district.PhysicalSlot, "report_primary_District");
                //9354F0's category1, mark0/3/4=1, and mark5/6=0.
                state = package.WriteHistory(state, current, new(1, districtId,
                    current.ReadBaseCurrentHexIndex(city), Day(), main, detail, Identity(speaker), MarkBit0: 1, MarkBit3: 1, MarkBit4: 1), effects);
            }
            //The reception's independent manager+10 gate is not its +C
            //RecordingSuppression. Explicit events still write chronology.
            if (mode == 1 && Need(inputs?.ReceptionChronicleSuppression, "chronicle.reception_suppression") != 0) return;
            string Name(OfficerState person, bool full)
            {
                string surname = Head(person.CurrentSurname ?? throw Missing("chronicle_surname"));
                return full ? surname + Head(person.CurrentGivenName ?? throw Missing("chronicle_given_name")) : surname;
            }
            string PersonName(int id) => Name(state.Officers.GetValueOrDefault(id) ?? throw Missing("chronicle_Person_owner"), true);
            state = new CampaignChronicleWriter(package.HistoryWriter).Write(state, current,
                new(mode == 2 ? 0x8D : 0x8E, 1, Day(), package.HistoryWriter.CustomFirstName(state, current, newForce, Name),
                    Need(current.ReadForceReportColor(newForce), "chronicle_Force49"), mode == 2 ? 0x17u : 0x18u,
                    ImmutableArray.Create(PersonName(rulerId), PersonName(emperorId), string.Empty),
                    ImmutableArray.Create(current.ReadBaseCurrentHexIndex(city), 48400, 48400, 48400)),
                package.ReadApplicationGameplay()?.UserSelectionServicePresent);
        }

        private string Format(int root, HistoryMessageOperands operands)
        {
            var formatted = package.FormatNormalTurnReport(state, current, root, operands);
            state = formatted.State;
            return formatted.Text;
        }
        private int Day() => (package.ReadArmyDestructionSampler(state, current) ?? throw Missing("World450_45C")).Day450;
        private int Lookup(int identity) => Need((identity <= 6000 ? current.ReadPersonLookupReference(identity) :
            current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Person_lookup");
        private int Identity(int physical) => Need(current.ReadPersonIdentity(physical), "Person10");
        private int Tendency(int physical) => Need(RulerCaptureReleaseInputs.ReadCurrentWord(state, physical, 0x17A), "Person17A");
        private bool UnfavorableRuler(int otherRuler, int ruler)
        {
            if (Identity(ruler) == 736) return true;
            if (inputs?.UnfavorableRulerTendencyMaximum is int threshold)
                return Tendency(ruler) <= threshold || Dislikes(otherRuler, ruler);
            //Dislike independently makes the same actual negative partition.
            //No current state, sampler or temporary is changed by this query.
            if (Dislikes(otherRuler, ruler)) return true;
            throw Missing("configuration.unfavorable_ruler_tendency_maximum");
        }
        private bool Dislikes(int first, int second) => OfficeRelationList(state, current, first, second, 0xD0);
        private bool Related(int first, int second) => OfficeRelatedCurrent(state, current, first, second);
        private IEnumerable<int> ForcePeople(int force)
        {
            foreach (int? raw in current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("Person100"))
            {
                int person = Need(raw, "Person100_payload");
                if (force is < 1 or > 51 || Need(current.ReadPersonOldForceSlot(person), "Person_Force") != force) continue;
                if (Need(current.ReadPersonStatus(person), "PersonE6") is >= 1 and <= 4) yield return person;
            }
        }
        private byte Relation(int first, int second) => current.TryReadRulerReleaseRelation13(first, second, out var value)
            ? Need(value, "Force13") : state.NativeArmySimulation?.Forces.GetValueOrDefault(first)?.NormalAftermath?.Relations13 is
                { Length: 52 } values ? values[second] : throw Missing("Force13");
        private void ChangeRelation(int first, int second, int delta) => state = ForceRelationChangeCurrent.Apply(state, current,
            first, second, () => delta, key => Need(ArmyDestructionParameterSeed.ReadCurrent(state, key), "relation_parameter"));
        private void WriteEmperor(int value)
        {
            var simulation = state.NativeArmySimulation ?? throw Missing("Emperor_saved_owner");
            state = state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with
                { Int32Values = new(simulation.Parameters.Int32Values) { [CityCaptureTerritoryInputs.Emperor1654Key] = value } } } };
            CityCaptureTerritoryInputs.BindCurrentEmperor(state, current);
        }
        private HexListHandle Temporary()
        {
            Lock(true);
            try
            {
                var list = current.CreateTemporaryHexList() ?? throw Missing("temporary_descriptor");
                temporaries.Add(list);
                return list;
            }
            finally { Lock(false); }
        }
        private void Append(HexListHandle list, RegistryKind kind, int physical)
        {
            Lock(true);
            try
            {
                var node = current.AllocateHexListNode(list);
                if (node.Identity is int cursor) current.WriteCurrentListPayload(cursor, new RegistryObjectReference(kind, physical));
            }
            finally { Lock(false); }
        }
        private List<(HexListNodeReference Node, int Physical)> Read(HexListHandle list, RegistryKind kind)
        {
            var result = new List<(HexListNodeReference, int)>();
            var node = current.ReadHexListHead(list) ?? throw Missing("temporary_head");
            while (node.Identity is int cursor)
            {
                var entry = current.ReadCurrentListPayload(cursor);
                int physical = entry switch
                {
                    RegistryObjectReference { PhysicalSlot: int p } reference when reference.Kind == kind => p,
                    ForceReference { PhysicalSlot: int p } when kind == RegistryKind.Force => p,
                    TransferPersonReference { PhysicalSlot: int p } when kind == RegistryKind.Person => p,
                    _ => throw Missing("temporary_payload")
                };
                result.Add((node, physical));
                node = current.ReadHexListNodeNext(node) ?? throw Missing("temporary_next");
            }
            return result;
        }
        private IReadOnlyList<int> ShuffleForces(HexListHandle list)
        {
            var nodes = Read(list, RegistryKind.Force);
            var values = nodes.Select(entry => entry.Physical).ToArray();
            if (values.Length <= 1) return values;
            if (values.Length > 1)
            {
                var sampler = package.ReadArmyDestructionSampler(state, current) ?? throw Missing("shuffle_World450_45C");
                int salt = 0;
                for (int remaining = values.Length / 2 * values.Length; remaining > 0; remaining--)
                {
                    int first = TacticEffectSourceSamplerMath.Sample(values.Length, 1, salt++, 0, 0, 0, sampler);
                    int second = values.Length - 1 - TacticEffectSourceSamplerMath.Sample(values.Length, 1, salt++, 0, 0, 0, sampler);
                    (values[first], values[second]) = (values[second], values[first]);
                }
            }
            for (int i = 0; i < values.Length; i++) current.WriteCurrentListPayload(Need(nodes[i].Node.Identity, "shuffle_cursor"),
                new RegistryObjectReference(RegistryKind.Force, values[i]));
            //The native insertion comparator sorts larger city counts first,
            //then the actual shuffle's rank; player Force is placed last.
            var ordered = values.ToList();
            for (int index = 1; index < ordered.Count; index++)
            {
                int selected = ordered[index], at = index;
                while (at > 0 && Before(selected, ordered[at - 1]))
                { ordered[at] = ordered[at - 1]; at--; }
                ordered[at] = selected;
            }
            var byPhysical = values.Select((physical, index) => (physical, nodes[index].Node)).ToDictionary(entry => entry.physical, entry => entry.Node);
            current.ReorderCurrentList(list, ordered.Select(physical => byPhysical[physical]).ToArray());
            return ordered;

            bool Before(int first, int second)
            {
                bool firstPlayer = package.TalentForceIsPlayerCurrent(state, current, new(first));
                bool secondPlayer = package.TalentForceIsPlayerCurrent(state, current, new(second));
                if (firstPlayer != secondPlayer) return !firstPlayer;
                int firstCities = Need(PersonArmyCaptureSource.ReadOwnedCityCount(current, new(first)), "shuffle_first_Force_City_count");
                int secondCities = Need(PersonArmyCaptureSource.ReadOwnedCityCount(current, new(second)), "shuffle_second_Force_City_count");
                return firstCities != secondCities ? firstCities > secondCities : Array.IndexOf(values, first) < Array.IndexOf(values, second);
            }
        }
        private void Lock(bool enter)
        {
            if (!Need(current.ReadRegistryLockEnabled(), "registry_lock")) return;
            if (enter) current.EnterRegistryLock(); else current.ExitRegistryLock();
        }
        private static string Head(string text) => text.IndexOf('\0') is int n && n >= 0 ? text[..n] : text;
        private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("story.forced-emperor-capture." + field);
    }
}
