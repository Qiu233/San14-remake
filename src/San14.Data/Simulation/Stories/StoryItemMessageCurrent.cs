using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //310390: one ordinary Story preparation's saved Item argument bank.
    //The caller already owns current and the exact latest candidate. No UI
    //admission, scenario Item fallback or new World capture participates.
    private sealed class StoryItemMessageCurrent(ArmyCampaignRuleAdapter package,
        Func<GameState> latest, SourceWorldContext current, IReadOnlyList<int>? itemArguments)
    {
        private LoadedForceNameDefinitions? forceNames;

        internal int ItemValue(int argument, int field)
        {
            int raw = Argument(argument);
            if (field == 0) return raw; //Before Item lookup/native validity.
            var item = Item(raw);
            if (item is null) return 0;
            if (field == '3') return Definition(item).Appearance?.Kind
                ?? throw Missing("Item_kind44");
            return Text(item, field).Length; //UTF16 units, including decoration.
        }

        internal string ItemText(int argument, int field)
        {
            int raw = Argument(argument);
            if (field == 0) return ""; //A bare selector produces a number, no text.
            var item = Item(raw);
            return item is null ? "" : Text(item, field);
        }

        private int Argument(int argument)
        {
            if (argument is < 0 or > 9) throw Missing("argument_index");
            //The native bank has ten registers; opcode102 owns only five.
            //Unknown upper slots must not become assumed native reset zeroes.
            if (itemArguments is null || argument >= itemArguments.Count)
                throw Missing("Item" + argument);
            return itemArguments[argument];
        }

        private ItemState? Item(int raw)
        {
            if (raw is < 1 or > 201) return null;
            var pointer = current.ReadRegistryTableReference(RegistryKind.Item, raw)
                ?? throw Missing("Item_lookup");
            if (pointer.PhysicalSlot is not int physical || physical == 0) return null;
            var item = latest().Items?.GetValueOrDefault(physical) ?? throw Missing("Item_current_owner");
            //The current Story capture need not bind combat Item overlays.
            //Read the saved lifecycle which owns the same native50 predicate.
            var status = item.Lifecycle?.Status ?? throw Missing("Item_status50");
            return status.IsListEligible ? item : null;
        }

        private ItemDefinition Definition(ItemState item) =>
            latest().ItemDefinitions.GetValueOrDefault(item.DefinitionId) ?? throw Missing("Item_current_definition");

        private string Text(ItemState item, int field)
        {
            if (field == '2') return Decorate(Definition(item).Name);
            if (field is not ('4' or '5' or '6' or '7')) return "";
            int holderId = item.Holder?.OfficerId ?? (item.Holder is not null ? 0 : throw Missing("Item_holder4A"));
            int holder = Person(holderId);
            if (field == '4')
                return ValidPerson(holder) ? CityDomesticMessageFormatting.FormatAbilityField(PersonName(holder)) : "";
            if (field == '7') return DistrictName(holder);

            //22CB40 reads Person.E0 even if that Person is not native-valid.
            //Fields5/6 must not borrow PersonText's outer Person-validity gate.
            var force = current.ReadPersonForce(holder) ?? throw Missing("holder_Force");
            int forcePhysical = Need(current.ReadForcePhysicalSlot(force), "holder_Force_receiver");
            if (field == '5')
            {
                if (!Need(current.ReadForceNativeValidity(force), "holder_Force_validity")) return "";
                int ruler = Person(Need(current.ReadMoraleWaveRuler22EAC0(force), "Force_ruler10"));
                string name = !ValidPerson(ruler) ? "" : forcePhysical is >= 46 and <= 50
                    ? ShortPersonName(ruler)
                    : PersonName(ruler) + HeadHistory(package.ReadHistoryDataText(1, 0x34F));
                return Decorate(name); //22EE20 does not prefer a custom kingdom.
            }

            //2AFBF0 reads48 before testing the kingdom row. There is no Force
            //validity gate here, and no fallback to the ruler/Force.Name.
            int kingdom = Need(current.ReadForceKingdomNameIndex(forcePhysical), "Force_kingdom48");
            var definitions = (forceNames ??= package._repository.LoadForceNameDefinitions(
                package._scenario.FixedDataSource.SourcePath)).Catalog.Definitions;
            int selected = kingdom <= 100 ? kingdom : 0;
            if ((uint)selected >= (uint)definitions.Count) throw Missing("kingdom_definition");
            return definitions[selected].NativeValidity ? Decorate(definitions[selected].ShortName) : "";
        }

        private string DistrictName(int holder)
        {
            //040020 performs the raw District lookup without Person validity.
            int raw = Need(current.ReadPersonDistrictSlot(holder), "holder_DistrictE0");
            var district = current.ReadDistrictTableReference(raw <= 51 ? raw : 0)
                ?? throw Missing("holder_District_lookup");
            if (!Need(current.ReadDistrictNativeValidity(district), "holder_District_validity")) return "";
            int physical = Need(district.PhysicalSlot, "holder_District_receiver");
            int leader = Person(Need(current.ReadDistrictLeader(physical), "District_leader12"));
            string name = !ValidPerson(leader) ? "" : physical is >= 46 and <= 50
                ? ShortPersonName(leader)
                : PersonName(leader) + HeadHistory(package.ReadHistoryDataText(1, 0x350));
            return Decorate(name); //22ED50/3097E0 then310390's outer^05/^00.
        }

        private int Person(int id)
        {
            var person = (id is >= 0 and <= 6000 ? current.ReadPersonLookupReference(id)
                : current.ReadTransferPersonTableReference(0)) ?? throw Missing("Person_lookup");
            return Need(person.PhysicalSlot, "Person_receiver");
        }
        private bool ValidPerson(int physical) => Need(current.ReadPersonNativeValidity(physical), "Person_validity");
        private OfficerState PersonOwner(int physical) => latest().Officers.GetValueOrDefault(
            Need(current.ReadPersonIdentity(physical), "Person_identity")) ?? throw Missing("Person_current_name_owner");
        private string PersonName(int physical)
        {
            //22F020/22F060 concatenate the actual current two name fields.
            var person = PersonOwner(physical);
            return HeadHistory(person.CurrentSurname ?? throw Missing("Person_surname12")) +
                HeadHistory(person.CurrentGivenName ?? throw Missing("Person_given20"));
        }
        private string ShortPersonName(int physical)
        {
            int id = Need(current.ReadPersonIdentity(physical), "Person_short_identity");
            //231160: created officers and five special identities use full
            //name; every other native receiver returns the surname field.
            return id is >= 5101 and <= 5200 or >= 2062 and <= 2066 ? PersonName(physical)
                : HeadHistory(PersonOwner(physical).CurrentSurname ?? throw Missing("Person_short_surname12"));
        }
        private static string Decorate(string text) => CityDomesticMessageFormatting.FormatCityNameField(text);
        private static T Need<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
        private static InvalidDataException Missing(string name) => new("story.message.Item." + name);
    }
}
