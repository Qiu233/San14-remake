using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal InformationList ReadInformationBelongingsItems(GameState state, SourceWorldContext current,
        int playerForceId, string title) => ReadInformationItems(state, current, playerForceId, title, true, true);

    /// <summary>The general item dispatcher and the belongings producer share
    /// current fields, but retain their distinct holder admission predicates.</summary>
    private InformationList ReadInformationItems(GameState state, SourceWorldContext current,
        int playerForceId, string title, bool ownOnly, bool belongings)
    {
        var rows = ImmutableArray.CreateBuilder<InformationRow>();
        var issues = new HashSet<string>(StringComparer.Ordinal);
        string[] keys = ["name", "owner", "leadership", "strength", "intelligence", "politics", "charm"];
        var player = current.ReadForceTableReference(playerForceId is >= 0 and < 52 ? playerForceId : 0)
            ?? throw InfoBelongingsMissing("player_Force_lookup");
        for (int lookup = 0; lookup < 202; lookup++)
        {
            try
            {
                var item = current.ReadRegistryTableReference(RegistryKind.Item, lookup)
                    ?? throw InfoBelongingsMissing("Item_table");
                if (!BelongingsKnown(current.ReadRegistryObjectNativeValidity(item), "Item_native_validity")) continue;
                int physical = BelongingsKnown(item.PhysicalSlot, "Item_receiver");
                ushort holderId = BelongingsKnown(current.ReadCaptureItemOwner4A(physical), "Item_holder4A");
                var holder = (holderId < 6001 ? current.ReadPersonLookupReference(holderId)
                    : current.ReadTransferPersonTableReference(0)) ?? throw InfoBelongingsMissing("holder_Person_lookup");
                int person = BelongingsKnown(holder.PhysicalSlot, "holder_Person_receiver");
                if (belongings || ownOnly)
                {
                    if (!BelongingsKnown(current.ReadPersonNativeValidity(person), "holder_Person_validity")) continue;
                    var force = current.ReadPersonForce(person) ?? throw InfoBelongingsMissing("holder_Force_lookup");
                    if (force != player) continue;
                }
                //The normal all-items list has its own status exclusion.
                //The separate belongings producer does not apply this gate.
                if (!belongings)
                {
                    byte status = BelongingsKnown(current.ReadPersonStatus(person), "holder_Person_status");
                    if (status == 255 || status is >= 7 and <= 9) continue;
                }
                var saved = state.Items?.GetValueOrDefault(physical) ?? throw InfoBelongingsMissing("Item_current_owner");
                var definition = state.ItemDefinitions.GetValueOrDefault(saved.DefinitionId)
                    ?? throw InfoBelongingsMissing("Item_current_definition");
                var cells = new Dictionary<string, InformationCell>(StringComparer.Ordinal)
                { ["name"] = new(definition.Name, SortText: definition.Name) };
                cells["owner"] = holderId == 0 ? new("") : state.Officers.TryGetValue(holderId, out var officer)
                    ? new(officer.Name, Number: holderId, SortText: officer.CurrentNameSortKey)
                    : new(UnresolvedOwner: "information.belongings.holder_name");
                var bonus = definition.AbilityBonus;
                cells["leadership"] = BelongingsNumber(bonus?.Leadership, "item_leadership");
                cells["strength"] = BelongingsNumber(bonus?.Strength, "item_strength");
                cells["intelligence"] = BelongingsNumber(bonus?.Intelligence, "item_intelligence");
                cells["politics"] = BelongingsNumber(bonus?.Politics, "item_politics");
                cells["charm"] = BelongingsNumber(bonus?.Charm, "item_charm");
                rows.Add(new((int)InformationListKind.Items, physical, definition.Name,
                    cells.ToImmutableDictionary(StringComparer.Ordinal), new(InformationListKind.Items, physical)));
            }
            catch (InvalidDataException error) { issues.Add(error.Message); }
        }
        return new(title, keys.Select(key => InformationColumnFor(key, title)).ToImmutableArray(), rows.ToImmutable(),
            issues.Count == 0 ? null : string.Join(";", issues));
    }

    internal InformationList ReadInformationBelongingsGems(GameState state, SourceWorldContext current,
        int playerForceId, string title)
    {
        //This binds the saved mutable52/53 fields once for this list. Names
        //and descriptions remain the already loaded immutable definitions.
        DeploymentTacticCurrentInputs.BindCurrent(state, current);
        var rows = ImmutableArray.CreateBuilder<InformationRow>();
        var issues = new HashSet<string>(StringComparer.Ordinal);
        string[] keys = ["name", "count", "tactic", "system", "ability_dependency", "against_base"];
        var player = current.ReadForceTableReference(playerForceId is >= 0 and < 52 ? playerForceId : 0)
            ?? throw InfoBelongingsMissing("player_Force_lookup");
        for (int lookup = 0; lookup < 31; lookup++)
        {
            try
            {
                var gem = current.ReadSkillGemReference(lookup) ?? throw InfoBelongingsMissing("Gem_table");
                if (!BelongingsKnown(current.ReadSkillGemValidity(gem), "Gem_native_validity")) continue;
                int physical = BelongingsKnown(gem.PhysicalSlot, "Gem_receiver");
                string name = _skills.Gems.Gems[physical].Name;
                byte requested = BelongingsKnown(current.ReadSkillGemTactic52(gem), "Gem_tactic52");
                var tactic = current.ReadSkillTacticReference(requested < 201 ? requested : 0)
                    ?? throw InfoBelongingsMissing("Gem_tactic_lookup");
                bool valid = BelongingsKnown(current.ReadSkillTacticValidity(tactic), "Gem_tactic_validity");
                int physicalTactic = BelongingsKnown(tactic.PhysicalSlot, "Gem_tactic_receiver");
                var cells = new Dictionary<string, InformationCell>(StringComparer.Ordinal)
                {
                    ["name"] = new(name, SortText: name),
                    ["count"] = BelongingsKnown(current.ReadForceNativeValidity(player), "Gem_player_validity")
                        ? BelongingsNumber(TalentGemAcquisitionSource.Read(state.GemForceInventory, player, gem), "Gem_count") : new(""),
                    ["tactic"] = valid ? state.Tactics.TryGetValue(physicalTactic, out var definition)
                        ? new(definition.Name, SortText: definition.Name)
                        : new(UnresolvedOwner: "information.belongings.Gem_tactic_definition") : new("")
                };
                byte category = valid ? BelongingsKnown(current.ReadSkillTacticRawByte(tactic, 0x40), "Gem_tactic_system40") : (byte)0;
                cells["system"] = new(category is >= 1 and <= 9 ? ReadHistoryDataText(0, 0x147 + category) : "", Number: category);
                int factor = valid ? DeploymentTacticSelectionSource.ReadFactor(current, tactic) : 0;
                cells["ability_dependency"] = new(factor == 0 ? "" : factor is 1 or 2 ? ReadHistoryDataText(1, 0x238 + factor)
                    : factor.ToString(System.Globalization.CultureInfo.InvariantCulture), Number: factor);
                bool againstBase = valid && (BelongingsKnown(current.ReadSkillTacticFlags(tactic), "Gem_tactic_flags44") & 1) != 0;
                cells["against_base"] = new(ReadHistoryDataText(1, againstBase ? 0x338 : 0x339), Number: againstBase ? 1 : 0);
                //The original belongings list routes item selection to item
                //details; gem rows are display records and do not issue orders.
                rows.Add(new((int)InformationListKind.GemBooks, physical, name,
                    cells.ToImmutableDictionary(StringComparer.Ordinal)));
            }
            catch (InvalidDataException error) { issues.Add(error.Message); }
        }
        int[] captions = [0x41e, 0x41f, 0x234, 0x455, 0x456, 0x4b4];
        return new(title, keys.Select((key, index) => new InformationColumn(key, captions[index], ReadHistoryDataText(1, captions[index]))).ToImmutableArray(), rows.ToImmutable(),
            issues.Count == 0 ? null : string.Join(";", issues));
    }

    private static InformationCell BelongingsNumber(long? value, string field) => value is long actual ? new(Number: actual)
        : new(UnresolvedOwner: "information.belongings." + field);
    private static T BelongingsKnown<T>(T? value, string field) where T : struct => value ?? throw InfoBelongingsMissing(field);
    private static InvalidDataException InfoBelongingsMissing(string field) => new("information.belongings." + field);
}
