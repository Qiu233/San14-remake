using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private sealed partial class NormalTurnFinalOrdersSource
    {
        //28FC40 and28EC10 both have their own tutorial header gate, separate
        //from429220/429140's preceding byte0A bit1 suppression.
        private void GoodOmen()
        {
            if (N(package.ReadApplicationTutorialActive(), "good_omen_tutorial"))
            {
                if (!N(TacticRemakeCurrentBattleControl.Read(state, 80), "good_omen_tutorial_bit0")) return;
                var forcedCity = City(7); //WorldDD70 is the real City table entry7.
                if (Valid(forcedCity)) ApplyGoodOmen(forcedCity, true);
                var bank = state.NativeArmySimulation?.Parameters ?? throw Missing("tutorial_parameters");
                string key = TacticRemakeCurrentBattleControl.BitmapKey(10);
                if (!bank.Int32Values.TryGetValue(key, out int bits)) throw Missing("tutorial_byte0A");
                state = state with { NativeArmySimulation = state.NativeArmySimulation! with { Parameters = bank with {
                    Int32Values = new(bank.Int32Values) { [key] = bits & 0xFE } } } };
                return;
            }
            if (Date.Month > 10 || (0x492 >> Date.Month & 1) == 0) return;
            var list = Temporary();
            try
            {
                for (int slot = 0; slot < 52; slot++)
                {
                    var city = City(slot);
                    if (!Valid(city) || !Ordinary(Force(city))) continue;
                    SetCityFlags(city, (byte)(CityFlags(city) & 0xF7));
                    bool disaster = false;
                    foreach (var area in CityAreas(city))
                        if (N(current.ReadAreaNativeValidity(area), "good_omen_Area_validity") && (AreaFlags(area) & 5) != 0)
                        { disaster = true; break; }
                    if (!disaster) Add(list, RegistryKind.City, At(city));
                }
                var values = Values(list, RegistryKind.City);
                if (values.Count == 0) return;
                int chosen = Shuffle(list, RegistryKind.City)[0];
                var target = new BaseReference(BaseKind.City, chosen);
                int troops = N(current.ReadBaseResource(target, BaseResourceKind.Troops), "good_omen_Troops3C");
                int dayOrdinal = PersonReturnCommands.CalendarOrdinal(Date);
                int supplies = N(current.ReadBaseResource(target, BaseResourceKind.Supplies), "good_omen_Supplies");
                int gold = N(current.ReadBaseResource(target, BaseResourceKind.Gold), "good_omen_Gold");
                if (Sample(100, gold, supplies, troops, dayOrdinal) >= P("good-omen-chance", 15)) return;
                SetCityFlags(target, (byte)(CityFlags(target) | 8));
                state = ArmyCampaignRuleAdapter.SetNormalTurnMoraleCurrent(state, current, target,
                    unchecked(N(current.ReadBaseMorale44(target), "good_omen_Morale44") + P("omen-morale", 10)));
                troops = N(current.ReadBaseResource(target, BaseResourceKind.Troops), "good_omen_second_Troops3C");
                dayOrdinal = PersonReturnCommands.CalendarOrdinal(Date);
                supplies = N(current.ReadBaseResource(target, BaseResourceKind.Supplies), "good_omen_second_Supplies");
                gold = N(current.ReadBaseResource(target, BaseResourceKind.Gold), "good_omen_second_Gold");
                bool auspicious = Sample(100, gold / 2, supplies / 3, troops / 5, dayOrdinal / 2) < P("auspicious-sign-chance", 30);
                ApplyGoodOmen(target, auspicious, moraleAlreadyApplied: true);
            }
            finally { Release(list); }
        }

        private void ApplyGoodOmen(BaseReference city, bool auspicious, bool moraleAlreadyApplied = false)
        {
            if (!moraleAlreadyApplied)
            {
                SetCityFlags(city, (byte)(CityFlags(city) | 8));
                state = ArmyCampaignRuleAdapter.SetNormalTurnMoraleCurrent(state, current, city,
                    unchecked(N(current.ReadBaseMorale44(city), "tutorial_omen_Morale44") + P("omen-morale", 10)));
            }
            state = RulerCaptureReleaseSource.ApplyCurrentPopularityAdjustment(state, current, Id(Force(city)),
                auspicious ? P("auspicious-popularity", 150) : P("good-omen-popularity", 100));
            int root;
            if (auspicious)
            {
                foreach (int person in HomePeople(city))
                    if (PersonValid(person)) IncreaseLoyalty(person, Ruler(current.ReadPersonForce(person) ?? throw Missing("omen_Person_Force")), P("auspicious-loyalty", 5));
                //The forced tutorial branch uses5EE7 without this extra draw.
                int selected = moraleAlreadyApplied ? Sample(2, At(city),
                    N(current.ReadBaseResource(city, BaseResourceKind.Gold), "omen_Gold"),
                    N(current.ReadBaseResource(city, BaseResourceKind.Supplies), "omen_Supplies"),
                    N(current.ReadBaseResource(city, BaseResourceKind.Troops), "omen_Troops")) : 1;
                root = 0x5EE6 + selected;
            }
            else root = Date.Month is 7 or 10 ? 0x5EE8 : 0x5EE9;
            Scene(26); Sound(20);
            Narration(Format(root, new(Force0: Id(Force(city)), Base0: N(current.ReadBaseObjectLink(city), "omen_report_Base4E"))));
            Scene(null);
            OmenHistory(city, 0x53B3);
        }

        private void BadOmen()
        {
            if (N(package.ReadApplicationTutorialActive(), "bad_omen_tutorial")) return;
            if (Date.Month > 10 || (0x492 >> Date.Month & 1) == 0) return;
            var list = Temporary();
            try
            {
                for (int slot = 0; slot < 52; slot++)
                {
                    var candidate = City(slot);
                    if (!Valid(candidate) || !Ordinary(Force(candidate))) continue;
                    byte flags = CityFlags(candidate);
                    if ((flags & 8) != 0) { SetCityFlags(candidate, (byte)(flags & 0xF7)); continue; }
                    bool harvest = false;
                    foreach (var area in CityAreas(candidate))
                        if (N(current.ReadAreaNativeValidity(area), "bad_omen_Area_validity") && (AreaFlags(area) & 0x10) != 0)
                        { harvest = true; break; }
                    if (!harvest) Add(list, RegistryKind.City, At(candidate));
                }
                if (Values(list, RegistryKind.City).Count == 0) return;
                var city = new BaseReference(BaseKind.City, Shuffle(list, RegistryKind.City)[0]);
                int troops = N(current.ReadBaseResource(city, BaseResourceKind.Troops), "bad_omen_Troops");
                int ordinal = PersonReturnCommands.CalendarOrdinal(Date);
                int supplies = N(current.ReadBaseResource(city, BaseResourceKind.Supplies), "bad_omen_Supplies");
                int gold = N(current.ReadBaseResource(city, BaseResourceKind.Gold), "bad_omen_Gold");
                if (Sample(100, gold, supplies, troops, ordinal) >= P("bad-omen-chance", 30)) return;
                AddDevelopment(Id(Force(city)), -P("bad-omen-popularity", 100));
                Scene(10); Sound(18);
                Narration(Format(Date.Month is 7 or 10 ? 0x5EEA : 0x5EEB,
                    new(Force0: Id(Force(city)), Base0: N(current.ReadBaseObjectLink(city), "bad_omen_Base4E"))));
                Scene(null); OmenHistory(city, 0x53BC);
            }
            finally { Release(list); }
        }

        private byte CityFlags(BaseReference city) => N(current.ReadCaptureCityByteC9(At(city)), "CityC9");
        private void SetCityFlags(BaseReference city, byte flags)
        {
            current.SetCaptureCityByteC9(At(city), flags);
            var owner = state.NativeBaseCaptureCurrent ?? throw Missing("CityC9_owner");
            var before = owner.Cities.GetValueOrDefault(At(city)) ?? throw Missing("CityC9_record");
            state = state with { NativeBaseCaptureCurrent = owner with { Cities = new(owner.Cities) { [At(city)] = before with { C9 = flags } } } };
        }
        private IEnumerable<AreaReference> CityAreas(BaseReference city)
        {
            foreach (int physical in Registry(RegistryKind.Area))
            {
                var area = new AreaReference(physical);
                if (N(current.ReadAreaCitySlot(area), "Area35") == At(city)) yield return area;
            }
        }
        private byte AreaFlags(AreaReference area) => N(current.ReadAreaFlags38(area), "Area38");
        private void OmenHistory(BaseReference city, int root)
        {
            if (!Valid(Player)) return;
            var primary = ForcePrimaryDistrictSource.Resolve(current, Player);
            if (!N(current.ReadDistrictNativeValidity(primary), "omen_primary_validity") || Force(city) != Player) return;
            int speaker = Speaker();
            if (!PersonValid(speaker)) return;
            string text = Format(root, new(Person0: PersonId(speaker), Person1: PersonId(Ruler(current.ReadPersonForce(speaker) ?? throw Missing("speaker_Force"))),
                Base0: N(current.ReadBaseObjectLink(city), "history_Base4E")));
            History(text, speaker, current.ReadBaseCurrentHexIndex(city));
        }

        //258D00's positive loyalty writer, mode1. Status and relationship
        //caps retain source raw values above the display's0..100 interval.
        private void IncreaseLoyalty(int person, int ruler, int amount)
        {
            if (!PersonValid(person) || !PersonValid(ruler) || amount <= 0) return;
            if (N(current.ReadPersonStatus(person), "loyalty_status") == 1) return;
            int home = N(current.ReadPersonFirstLocation(person), "loyalty_HomeE2");
            int force = Id(current.ReadPersonForce(person) ?? throw Missing("loyalty_Force"));
            if ((home is < 1 or > 61) && force is >= 46 and <= 50) return;
            bool special = DuelAftermathSpecialPerson(current, person);
            ushort flags = N(current.ReadArmyReturnPersonFlags196(person), "loyalty_flags15E");
            if (special || (flags & 0x4000) != 0) return;
            int cap = 100;
            int sworn = OfficeWord(state, person, 0xBC), spouse = OfficeWord(state, person, 0xBA);
            if (PersonId(person) != PersonId(ruler) &&
                (sworn != 0 && sworn == OfficeWord(state, ruler, 0xBC) || spouse != 0 && spouse == PersonId(ruler))) cap = 150;
            else if (OfficeRelationList(state, current, person, ruler, 0xC0)) cap = 120;
            byte before = N(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, person, package._governorInputs), "loyalty_E8");
            int delta = unchecked(Math.Min(unchecked(before + amount), cap) - before);
            if (delta <= 0) return;
            byte after = (flags & 0x2000) != 0 ? (byte)255 : (byte)Math.Min(before + unchecked((byte)delta), 255);
            current.SetPersonDestructionLoyaltyE8(person, after);
            state = ArmyDestructionLoyalty.PublishCurrentRawE8(state, current, person, after);
        }
    }
}
