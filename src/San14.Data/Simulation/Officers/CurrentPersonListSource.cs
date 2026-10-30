using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>The current list's location category, independent reading and
/// source index. Both directions reverse the complete location comparison.</summary>
public readonly record struct PersonLocationOrderKey(int Kind, int Index, string Text)
{
    public bool Before(PersonLocationOrderKey other, bool reversed = false)
    {
        if (reversed) return other.Before(this);
        if (Kind != other.Kind) return Kind < other.Kind;
        return SameRawKey(Text, other.Text) ? Index < other.Index
            : DeploymentResponsibilityNameOrder.Before(Text, other.Text);
    }

    public int Compare(PersonLocationOrderKey other, bool ascending = true) =>
        Before(other, !ascending) ? -1 : other.Before(this, !ascending) ? 1 : 0;

    private static bool SameRawKey(string left, string right)
    {
        //282A60's bounded UTF16 equality is independent of collation.
        for (int i = 0; i < 1024; i++)
        {
            char a = i < left.Length ? left[i] : '\0', b = i < right.Length ? right[i] : '\0';
            if (a != b) return false;
            if (a == '\0') return true;
        }
        return true;
    }
}

/// <summary>Current list-only item, doctrine and location reads. Caches belong
/// to one publication; they neither fingerprint nor mutate the business world.</summary>
internal sealed class CurrentPersonListSource(GameState state, SourceWorldContext current,
    LoadedScenario? scenario, GameResourceRepository? repository, string? language)
{
    private Dictionary<int, int>? itemCounts;
    private string? itemCountMissing;
    private Dictionary<int, string>? doctrineSortKeys;
    private MessageDocument? messages;

    internal int RegisteredItemCount(int personId)
    {
        //CPersonItemIterator/334870 uses registered Item110 and compares the
        //holder word directly, without the detail page's native-valid table scan.
        if (personId is < 1 or > 6000) return 0;
        if (itemCountMissing is not null) throw new InvalidDataException(itemCountMissing);
        if (itemCounts is null)
        {
            try
            {
                var counts = new Dictionary<int, int>();
                foreach (var item in current.ReadRegistryReferences(RegistryKind.Item) ?? throw Missing("item_registry"))
                {
                    if (item is not { Kind: RegistryKind.Item, PhysicalSlot: int physical }) throw Missing("item_pointer");
                    int holder = K(current.ReadCaptureItemOwner4A(physical), "item_holder");
                    counts[holder] = unchecked(counts.GetValueOrDefault(holder) + 1);
                }
                itemCounts = counts;
            }
            catch (InvalidDataException error) { itemCountMissing = error.Message; throw; }
        }
        return itemCounts.GetValueOrDefault(personId);
    }

    internal string DoctrineSortText(int raw)
    {
        if (raw is < 0 or > 255) throw Missing("doctrine_byte");
        var pointer = current.ReadStaticTableReference(0x78CF8, raw < 7 ? raw : 0) ?? throw Missing("doctrine_table_pointer");
        int physical = K(pointer.PhysicalSlot, "doctrine_table_receiver");
        if (doctrineSortKeys is null)
        {
            var catalog = scenario?.Snapshot.DoctrineParameters ?? throw Missing("doctrine_catalog");
            var keys = new Dictionary<int, string>();
            foreach (var row in catalog.Doctrines)
                if (!keys.TryAdd(row.Slot, row.SecondaryText ?? throw Missing("doctrine_secondary_text")))
                    throw Missing("doctrine_catalog_identity");
            doctrineSortKeys = keys;
        }
        return doctrineSortKeys.GetValueOrDefault(physical) ?? throw Missing("doctrine_receiver");
    }

    internal PersonLocationOrderKey LocationOrder(int personPhysical, bool actual)
    {
        var location = actual
            ? PersonPositionRepair.DecodeSecondLocation(K(current.ReadPersonSecondLocation(personPhysical), "location_second"))
            : PersonPositionRepair.DecodeFirstLocation(K(current.ReadPersonFirstLocation(personPhysical), "location_first"));
        int kind = location.Kind == 10 ? 48 : location.Kind, index = location.Index;
        string text;
        if (kind == 5) text = index == 0 ? Data(1, 0x353) : CitySortText(current,
            scenario ?? throw Missing("location_scenario"), index < 52 ? index : 0);
        else if (kind == 6)
        {
            int physical = K(current.ReadGateTableReference(index < 11 ? index : 0)?.PhysicalSlot, "location_gate");
            var rows = scenario?.Snapshot.Gates ?? throw Missing("location_scenario");
            if ((uint)physical >= (uint)rows.Count || rows[physical].Slot != physical) throw Missing("location_gate_definition");
            text = rows[physical].SecondaryText ?? throw Missing("location_gate_secondary_text");
        }
        else if (kind == 27)
        {
            var army = current.ReadArmyUnitTableReference(index < 501 ? index : 0) ?? throw Missing("location_army");
            int raw = K(current.ReadArmyUnitCommanderId(army), "location_commander");
            int physical = K(current.ReadPersonLookupReference(raw < 6001 ? raw : 0)?.PhysicalSlot, "location_person");
            int id = K(current.ReadPersonIdentity(physical), "location_person_identity");
            text = K(current.ReadPersonNativeValidity(physical), "location_person_validity")
                ? state.Officers.GetValueOrDefault(id)?.CurrentNameSortKey ?? throw Missing("location_person_sort_text") : "";
        }
        else text = "";
        return new(kind, index, text);
    }

    internal string ArmyName(ArmyUnitReference army) => ReadArmyName(state, current, army, Data);

    internal static string ReadArmyName(GameState state, SourceWorldContext current, ArmyUnitReference army,
        Func<int, int, string> data)
    {
        string Empty() => CampaignHistoryWriter.Supplemental(state) ?? throw Missing("current_empty_text");
        if (!K(current.ReadArmyUnitNativeValidity(army), "army_name_validity")) return Empty();
        ushort commander = K(current.ReadArmyUnitCommanderId(army), "army_name_commander");
        var person = (commander <= 6000 ? current.ReadPersonLookupReference(commander) : current.ReadTransferPersonTableReference(0))
            ?? throw Missing("army_name_person_lookup");
        if (person.PhysicalSlot is not int physical) return Empty();
        if (!K(current.ReadPersonNativeValidity(physical), "army_name_person_validity")) return Empty();
        int id = K(current.ReadPersonIdentity(physical), "army_name_person_identity");
        if (current.ResolveOfficerSource(id) != physical || !state.Officers.TryGetValue(id, out var owner))
            throw Missing("army_name_person_owner");
        //22ECB0 ->309450(name,0), full name plus Data1/0352.
        return Head(owner.Name ?? throw Missing("army_name_person_name")) + Head(data(1, 0x352));
    }

    internal static string CitySortText(SourceWorldContext current, LoadedScenario scenario, int cityId)
    {
        var city = current.ReadCityTableReference(cityId) ?? throw Missing("city_sort_lookup");
        int physical = K(city.PhysicalSlot, "city_sort_receiver");
        var rows = scenario.Snapshot.Cities;
        if ((uint)physical >= (uint)rows.Count || rows[physical].Slot != physical) throw Missing("city_sort_definition");
        return rows[physical].SecondaryText ?? throw Missing("city_sort_secondary_text");
    }

    private string Data(int section, int id)
    {
        if (repository is null || language is null) throw Missing("data_messages_owner");
        messages ??= repository.LoadMessages(language, MessageKind.Data).Messages;
        if ((uint)section >= (uint)messages.Sections.Count || (uint)id >= (uint)messages.Sections[section].Records.Count)
            throw Missing("data_message_index");
        return messages.Sections[section].Records[id].Text ?? throw Missing("data_message_text");
    }
    private static string Head(string value) => value.IndexOf('\0') is int at && at >= 0 ? value[..at] : value;
    private static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("person-list." + field);
}
