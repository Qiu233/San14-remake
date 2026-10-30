using System.Text;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal MapSummaryPage QueryMessageMapSummaryCurrent(GameState state, SourceWorldContext current,
        MapSummaryReceiver receiver)
    {
        if (receiver.Slot == 14) return QueryCityMessageMapSummaryCurrent(state, current, receiver);
        if (receiver.Slot != 11) throw new InvalidDataException("map-summary.message.receiver_slot");
        //Read the actual independent claim owner. A legacy unknown does not
        //become neutral and is never replaced with the representative Hex owner.
        byte? force = current.ReadAreaOccupationClaimForceSlot(new(receiver.ReceiverId));
        return force is byte actual
            ? FormatAreaMessageMapSummaryCurrent(state, current, receiver, actual)
            : new(receiver.Slot, receiver.ReceiverId, "", null, new Dictionary<int, string>())
                { UnresolvedOwner = "map-summary.area-message.current_attacking_Force73_owner" };
    }

    /// <summary>928000's complete formatter for an actual Area73 writer.
    /// The ordinary query must not call this using the Area's current owner.</summary>
    internal MapSummaryPage FormatAreaMessageMapSummaryCurrent(GameState state, SourceWorldContext current,
        MapSummaryReceiver receiver, byte attackingForceSlot)
    {
        var result = new MapSummaryPage(receiver.Slot, receiver.ReceiverId, "", null, new Dictionary<int, string>());
        try
        {
            if (!SummaryArmyNeed(current.ReadAreaNativeValidity(new(receiver.ReceiverId)), "area-message.Area_validity") ||
                attackingForceSlot is < 1 or > 51) return result with { ReceiverValid = false };
            var force = current.ReadForceTableReference(attackingForceSlot) ?? throw SummaryArmyMissing("area-message.Force_pointer");
            if (!SummaryArmyNeed(current.ReadForceNativeValidity(force), "area-message.Force_validity"))
                return result with { ReceiverValid = false };
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw SummaryArmyMissing("area-message.current_player");
            int playerId = SummaryArmyNeed(player.PhysicalSlot, "area-message.player_receiver");
            bool own = playerId != 0 && SummaryArmyNeed(force.PhysicalSlot, "area-message.force_receiver") == playerId;
            string caption;
            if (own) caption = ReadHistoryDataText(1, 0x36C);
            else
            {
                int raw = current.ReadForceRecord(SummaryArmyNeed(force.PhysicalSlot, "area-message.force_receiver"))?.RawRuler10
                    ?? throw SummaryArmyMissing("area-message.Force_ruler10");
                var person = raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0);
                int physical = SummaryArmyNeed(person?.PhysicalSlot, "area-message.ruler_pointer");
                if (!SummaryArmyNeed(current.ReadPersonNativeValidity(physical), "area-message.ruler_validity"))
                    return result with { UnresolvedOwner = "map-summary.area-message.invalid_ruler_retains_prior_child" };
                int identity = SummaryArmyNeed(current.ReadPersonIdentity(physical), "area-message.ruler_identity");
                string name = state.Officers.GetValueOrDefault(identity)?.Name ?? throw SummaryArmyMissing("area-message.ruler_name");
                caption = SummaryMessageString(ReadHistoryDataText(1, 0x36B), name);
            }
            var layout = own ? "00_82_04_sum_area_mes_p" : "00_82_04_sum_area_mes_e";
            var child = new MapSummaryPage(receiver.Slot, receiver.ReceiverId, layout, true,
                new Dictionary<int, string> { [2] = caption })
            {
                TextPresets = new Dictionary<int, int> { [2] = own ? 38 : 40 },
                Colors = new Dictionary<int, MapCornerColors> { [3] = MapSpritePalettes.Create()[own ? 11 : 12] }
            };
            return result with { ReceiverValid = true, Children = [new(own ? "player" : "enemy", null, child)] };
        }
        catch (InvalidDataException error) { return result with { UnresolvedOwner = error.Message }; }
    }

    private MapSummaryPage QueryCityMessageMapSummaryCurrent(GameState state, SourceWorldContext current,
        MapSummaryReceiver receiver)
    {
        var result = new MapSummaryPage(receiver.Slot, receiver.ReceiverId, "", null, new Dictionary<int, string>());
        try
        {
            var city = new BaseReference(BaseKind.City, receiver.ReceiverId);
            if (!SummaryArmyNeed(current.ReadBaseNativeValidity(city), "city-message.City_validity"))
                return result with { ReceiverValid = false };
            int raw = _growthQueries.ReadCityState(state, receiver.ReceiverId) ?? throw SummaryArmyMissing("city-message.CityCE");
            var province = current.ReadStaticTableReference(0xDC80, raw < 23 ? raw : 0)
                ?? throw SummaryArmyMissing("city-message.Province_pointer");
            if (!SummaryArmyNeed(current.ReadConditionStateValidity(new(province.PhysicalSlot)), "city-message.Province_validity"))
                return result with { ReceiverValid = false };
            var reader = new ProvinceIndicatorReader(this, state, current);
            //928950 short-circuits: the loss branch is not queried when the
            //gain predicate succeeds, nor when the gain input is unknown.
            bool gain = reader.ReadSummaryGain(receiver.ReceiverId);
            if (!gain && !reader.ReadSummaryLoss(receiver.ReceiverId)) return result with { ReceiverValid = false };
            int id = SummaryArmyNeed(province.PhysicalSlot, "city-message.Province_receiver");
            var definition = _growthQueries.Definitions.Catalog.States[id];
            if (definition.Slot != id || definition.RawBytes.Length != 32) throw SummaryArmyMissing("city-message.Province_name_definition");
            string name = S14ForceNameDefinitionReader.ReadLiteralUnits(definition.RawBytes.AsSpan(24, 6));
            var child = new MapSummaryPage(receiver.Slot, receiver.ReceiverId, "00_82_04_sum_area_mes_e", true,
                new Dictionary<int, string> { [2] = SummaryMessageString(ReadHistoryDataText(1, gain ? 0x723 : 0x739), name) })
            {
                Images = [new(3, "000_cmn_icon_gadvantage_fuchi", "texture_pk_common")],
                Colors = new Dictionary<int, MapCornerColors> { [3] = MapSpritePalettes.Create()[gain ? 11 : 12] }
            };
            return result with { ReceiverValid = true, Children = [new("province", null, child)] };
        }
        catch (InvalidDataException error) { return result with { UnresolvedOwner = error.Message }; }
    }

    private static string SummaryMessageString(string format, string value)
    {
        var result = new StringBuilder(); int arguments = 0;
        for (int index = 0; index < format.Length; index++)
        {
            if (format[index] != '%') { result.Append(format[index]); continue; }
            if (++index >= format.Length) throw SummaryArmyMissing("message.string_format");
            if (format[index] == '%') { result.Append('%'); continue; }
            if (format[index] == 'l' && index + 1 < format.Length) index++;
            if (format[index] is not ('s' or 'S') || ++arguments != 1) throw SummaryArmyMissing("message.string_operand");
            result.Append(value);
        }
        if (arguments != 1) throw SummaryArmyMissing("message.string_operand_count");
        return result.ToString();
    }

    private sealed partial class ProvinceIndicatorReader
    {
        internal bool ReadSummaryGain(int cityId) => Gain(City(cityId));
        internal bool ReadSummaryLoss(int cityId) => Loss(City(cityId));
    }
}
