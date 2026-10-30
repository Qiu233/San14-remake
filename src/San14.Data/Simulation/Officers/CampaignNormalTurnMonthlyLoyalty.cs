using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState ApplyNormalTurnMonthlyLoyaltyCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool)
        => new MonthlyPersonnelLoyaltySource(this, state, current, pool).RunLoyalty();

    private sealed partial class MonthlyPersonnelLoyaltySource(ArmyCampaignRuleAdapter package,
        GameState state, SourceWorldContext current, WorldIndexContext pool)
    {
        //29D8B0: periodic loyalty loss, not a blanket decay of every Officer.
        internal GameState RunLoyalty()
        {
            if (Elapsed <= P("loyalty-start-period", 36)) return state;
            if (Remainder(Date.Month, P("loyalty-month-divisor", 3)) != 0) return state;
            RequirePool();
            foreach (int person in Persons())
            {
                byte before = Loyalty(person);
                if (before > 100 || (Flags(person) & 0x6000) != 0) continue;
                byte status = Status(person);
                if (status is not (2 or 3 or 4 or 6)) continue;
                int ruler = Ruler(person);
                if (!Valid(ruler) || Spouse(person, ruler) || Sworn(person, ruler) || Parent(person, ruler) || Parent(ruler, person) ||
                    List(person, ruler, like: true)) continue;
                if (OfficeRelatedCurrent(state, current, person, ruler) && !List(person, ruler, like: false)) continue;

                if (Affinity(person, ruler) < P("stable-affinity-limit", 20) && !List(person, ruler, like: false) && status != 6)
                {
                    int disposition = Disposition(person);
                    if ((Preference(person) <= disposition || disposition > 0) &&
                        (Remainder(Date.Month, P("loyalty-protection-month-divisor", 2)) != 0 ||
                            P("stable-disposition-minimum", null) <= disposition && Preference(person) < P("stable-preference-maximum", null))) continue;
                }
                if (Status(person) != 6)
                {
                    var home = BaseLookup.Find(N(current.ReadPersonFirstLocation(person), "loyalty_HomeE2"), current);
                    int governor = Lookup(N(current.ReadArmyReturnBaseGovernor2E(home), "loyalty_Home_governor2E"));
                    if (Valid(governor) && !PersonCurrentBusySource.Read(current, governor, Missing) &&
                        N(TacticRemakeCurrentTraits.PersonContribution(current, governor, 0x65), "loyalty_governor_trait101") != 0) continue;
                }
                int loss = LoyaltyLoss(person);
                byte after = (byte)Math.Clamp(unchecked(before - loss), 0, 255);
                //267B70's preceding caller has excluded bit13, but the setter
                //still owns the original forced255 rule at its real write.
                if ((Flags(person) & 0x2000) != 0) after = 255;
                current.SetPersonDestructionLoyaltyE8(person, after);
                state = ArmyDestructionLoyalty.PublishCurrentRawE8(state, current, person, after);
            }
            return state;
        }

        private int LoyaltyLoss(int person)
        {
            int ruler = Ruler(person);
            bool disliked = List(person, ruler, like: false);
            int affinity = Math.Clamp(Div(Affinity(person, ruler), P("loss-affinity-divisor", 18)), 0, 4);
            int preference = Div(Preference(person), P("loss-preference-divisor", 10));
            int disposition = Div(Disposition(person), P("loss-disposition-divisor", 10));
            int captive = Status(person) == 6 ? P("captive-loss-addition", 10) : 0;
            int sameClan = OfficeWord(state, person, 0xB2) == OfficeWord(state, ruler, 0xB2) ? 1 : 0;
            int years = Math.Clamp(Div(unchecked(Date.Year - N(package._governorInputs.ReadCurrentPhysicalWord166(state, person), "loss_join_year166")), 5), 0, 3);
            var force = Force(person);
            //The real2ADBC0 effect13 consumes its State temporary list even
            //when a later dislike branch does not use its numerical result.
            var advantage = PersonReturnPathSource.ReadForceAdvantage(state, current, force, 13, 0);
            int effect = advantage.Value ?? throw Missing(advantage.UnresolvedOwner ?? "loyalty_land_advantage13");
            int loss;
            if (!disliked)
            {
                int protection = effect != 0 ? P("land-advantage-protection", 2) : 0;
                loss = Math.Clamp(unchecked(captive - protection - sameClan - years - disposition + preference + affinity), 0, 3);
            }
            else loss = List(person, ruler, like: false)
                ? Math.Clamp(unchecked(affinity * 2 - disposition + captive + preference), 3, 6) : 0;
            if (Status(person) == 6)
            {
                bool captiveDisliked = List(person, ruler, like: false);
                int extra = P(captiveDisliked ? "captive-disliked-extra" : "captive-extra", captiveDisliked ? 4 : 2);
                if (extra > 0) loss = unchecked(loss + extra + Sample(3, Identity(person), Identity(ruler), 0, 0));
            }
            return loss;
        }

        private TacticSourceSamplerWorld? sampler;
        private NativeMapSeasonDate Date => state.NativeMapSeason?.ThirdDate ?? throw Missing("World34_date");
        private int Elapsed => unchecked((state.CampaignEventWorld ?? throw Missing("World454_owner")).ElapsedPeriodOffset +
            (int)(state.NativeMapSeason ?? throw Missing("calendar_owner")).AdvancedPeriods);
        private void RequirePool()
        {
            if (!ReferenceEquals(pool.ArmyReturnOwner, current) || !pool.OwnsArmyReturnProfile()) throw Missing("shared_current_pool");
        }
        private IEnumerable<int> Persons()
        {
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Person) ?? throw Missing("Person_registry"))
            {
                if (entry is not { Kind: RegistryKind.Person, PhysicalSlot: int person }) throw Missing("Person_payload");
                yield return person;
            }
        }
        private byte Status(int person) => N(current.ReadPersonStatus(person), "PersonE6");
        private int Identity(int person) => N(current.ReadPersonIdentity(person), "Person10");
        private bool Valid(int person) => N(current.ReadPersonNativeValidity(person), "Person_native_validity");
        private ushort Flags(int person) => N(current.ReadArmyReturnPersonFlags196(person), "Person15E");
        private byte Loyalty(int person) => N(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, person, package._governorInputs), "PersonE8");
        private int Preference(int person) => N(RulerCaptureReleaseInputs.ReadCurrentWord(state, person, 0x17C), "Person17C");
        private int Disposition(int person)
        {
            int value = N(current.ReadAllegianceDisposition(person), "Person16C");
            return value is >= short.MinValue and <= short.MaxValue ? value : throw Missing("Person16C_width");
        }
        private ForceReference Force(int person) => current.ReadPersonForce(person) ?? throw Missing("Person_Force22CB40");
        private int Lookup(int id) => N((id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Person_lookup");
        private int Ruler(int person)
        {
            var force = Force(person);
            //22EA50 returns the independent Person lookup0 when Force invalid.
            if (!N(current.ReadForceNativeValidity(force), "ruler_Force_validity")) return Lookup(0);
            return RulerOfForce(force);
        }
        private int RulerOfForce(ForceReference force) => Lookup(N(current.ReadForceRecord(N(force.PhysicalSlot, "ruler_Force_pointer"))?.RawRuler10, "ruler_Force10"));
        private bool Pair(int first, int second) => Valid(first) && Valid(second) && Identity(first) != Identity(second);
        private bool Parent(int first, int second) => Pair(first, second) &&
            (OfficeWord(state, first, 0xB4) is var father && father != 0 && father == Identity(second) ||
             OfficeWord(state, first, 0xB6) is var mother && mother != 0 && mother == Identity(second));
        private bool Spouse(int first, int second) => Pair(first, second) &&
            OfficeWord(state, first, 0xBA) is var spouse && spouse != 0 && spouse == Identity(second);
        private bool Sworn(int first, int second) => Pair(first, second) &&
            OfficeWord(state, first, 0xBC) is var group && group != 0 && group == OfficeWord(state, second, 0xBC);
        private bool List(int first, int second, bool like) => OfficeRelationList(state, current, first, second, like ? 0xC0 : 0xD0);
        private int Affinity(int first, int second)
        {
            if (!Valid(first) || !Valid(second)) return 0;
            int distance = Math.Abs(N(OwnedPersonRelationInputs.ReadCurrentAffinity(state, first), "first_BE") -
                N(OwnedPersonRelationInputs.ReadCurrentAffinity(state, second), "second_BE"));
            return Math.Min(Math.Min(distance, 150 - distance), 75);
        }
        private int Sample(int bound, int first, int second, int third, int fourth)
        {
            if (bound < 2) return 0;
            sampler ??= package.ReadArmyDestructionSampler(state, current) ?? throw Missing("World450_45C");
            return TacticEffectSourceSamplerMath.Sample(bound, 1, first, second, third, fourth, sampler);
        }
        private int Day() => (sampler ??= package.ReadArmyDestructionSampler(state, current) ?? throw Missing("World450_45C")).Day450;
        private int P(string name, int? knownTc)
        {
            string key = "simulation.normal-turn.personnel-loyalty." + name;
            var bank = state.NativeArmySimulation?.Parameters ?? throw Missing("parameter_owner");
            if (bank.Float32Values.ContainsKey(key)) throw Missing("integer_parameter_" + name);
            if (bank.Int32Values.TryGetValue(key, out int value)) return value;
            // Both editions' immutable rules have independently qualified
            // sources. Stable-owner fields are saved/fresh only; old absent
            // values never acquire a query-time default.
            return NormalTurnPersonnelLoyaltyParameters.ReadImmutableDefault(package.Seed.Language, name, knownTc)
                ?? throw Missing("current_parameter_" + name);
        }
        private static int Div(int value, int divisor) => divisor == 0 || value == int.MinValue && divisor == -1 ? throw Missing("division") : value / divisor;
        private static int Remainder(int value, int divisor) => divisor == 0 || value == int.MinValue && divisor == -1 ? throw Missing("remainder") : value % divisor;
        private static T N<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        private static InvalidDataException Missing(string field) => new("normal-turn.personnel-loyalty." + field);
    }
}
