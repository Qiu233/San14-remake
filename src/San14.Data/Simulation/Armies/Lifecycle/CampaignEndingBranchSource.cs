using System.Buffers.Binary;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    private const string EndingTrackSelectionKey = "campaign.ending:track-selection";

    //338EC0 serializes World17AC independently of World44's scenario mode.
    //In the verified CE373-byte static World it follows the64 i32 entries
    //at104..359. Only fresh loading installs this value; old missing saves
    //do not acquire the constructor's earlier zero as their current value.
    internal GameState InstallFreshCampaignEndingBranchInputs(GameState state)
    {
        var world = _scenario.Snapshot.WorldParameters ?? throw EndingBranchMissing("fresh_World_source");
        var native = state.NativeArmySimulation ?? throw EndingBranchMissing("fresh_parameter_owner");
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            _scenario.Metadata.FormatMarker != 0x66 || world.RawBytes.Length < 373 ||
            state.ContentHashes.GetValueOrDefault(_scenario.ScenarioSource.ResourceUri) != _scenario.ScenarioSource.Sha256 ||
            native.Parameters.Int32Values.ContainsKey(EndingTrackSelectionKey) ||
            native.Parameters.Float32Values.ContainsKey(EndingTrackSelectionKey))
            throw EndingBranchMissing("fresh_track_selection_boundary");
        int selection = BinaryPrimitives.ReadInt32LittleEndian(world.RawBytes.AsSpan(360, 4));
        return state with { NativeArmySimulation = native with { Parameters = native.Parameters with
            { Int32Values = new(native.Parameters.Int32Values) { [EndingTrackSelectionKey] = selection } } } };
    }

    internal static bool ReadEndingAlternateTrackCurrent(GameState state)
    {
        var bank = state.NativeArmySimulation?.Parameters ?? throw EndingBranchMissing("track_selection_owner");
        if (bank.Float32Values.ContainsKey(EndingTrackSelectionKey) ||
            !bank.Int32Values.TryGetValue(EndingTrackSelectionKey, out int value))
            throw EndingBranchMissing("World17AC_track_selection");
        return value == 1; //3348F0, not language or World44.
    }

    internal bool ReadEndingImperialStageCurrent(GameState state, SourceWorldContext current)
    {
        int player = EndingPlayer(state, current);
        return EndingBranchNeed(current.ReadMoraleWaveForceRank47(player), "player_Force47") == 1 ||
            EndingBranchNeed(current.ReadForceKingdomNameIndex(player), "player_Force48") == 6;
    }

    internal bool ReadEndingEmperorStageCurrent(GameState state, SourceWorldContext current)
    {
        int player = EndingPlayer(state, current);
        if (player != EndingBranchNeed(current.ReadConditionEmperorForcePhysicalSlot(), "emperor_Force32D280")) return false;
        var force = current.ReadForceTableReference(player) ?? throw EndingBranchMissing("emperor_player_Force");
        int rulerId = EndingBranchNeed(current.ReadMoraleWaveRuler22EAC0(force), "emperor_player_ruler");
        var ruler = current.ReadPersonLookupReference(rulerId <= 6000 ? rulerId : 0)
            ?? throw EndingBranchMissing("emperor_player_ruler_lookup");
        if (ruler.PhysicalSlot is not int physical) return false;
        if (!EndingBranchNeed(current.ReadPersonNativeValidity(physical), "emperor_player_ruler_validity")) return false;
        return unchecked((short)EndingBranchNeed(RulerCaptureReleaseInputs.ReadCurrentWord(state, physical, 0x17A),
            "emperor_player_ruler17A")) > 10;
    }

    internal int ReadEndingFinalStageCurrent(GameState state, SourceWorldContext current, bool declined)
    {
        int player = EndingPlayer(state, current);
        if (EndingBranchNeed(current.ReadForceKingdomNameIndex(player), "final_player_Force48") == 7) return 6;
        if (declined) return 7;
        if (AllSpecialForcesDependOnPlayer()) return 8;
        if (AllForeignTechnologiesAcquired()) return 9;
        int elapsed = unchecked((state.CampaignEventWorld ?? throw EndingBranchMissing("elapsed_World454_owner")).ElapsedPeriodOffset +
            (int)(state.NativeMapSeason ?? throw EndingBranchMissing("elapsed_period_calendar")).AdvancedPeriods);
        if (elapsed < 361) return 10;
        return HasProsperousRealm() ? 11 : 12;

        //43C9D0: only native-valid2329E0 special forces participate. Empty
        //eligible membership returns true, as the original C0 loop does.
        bool AllSpecialForcesDependOnPlayer()
        {
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Force)
                ?? throw EndingBranchMissing("special_ForceC0"))
            {
                if (entry is not { Kind: RegistryKind.Force, PhysicalSlot: int slot })
                    throw EndingBranchMissing("special_ForceC0_payload");
                var force = current.ReadForceTableReference(slot) ?? throw EndingBranchMissing("special_Force_pointer");
                if (!EndingBranchNeed(current.ReadForceNativeValidity(force), "special_Force_validity")) continue;
                ushort rulerId = EndingBranchNeed(current.ReadForceRecord(slot)?.RawRuler10, "special_Force_ruler10");
                var ruler = current.ReadPersonLookupReference(rulerId <= 6000 ? rulerId : 0)
                    ?? throw EndingBranchMissing("special_ruler_lookup");
                //2329E0 checks the pointer and E2, without a Person-validity gate.
                if (ruler.PhysicalSlot is not int person) continue;
                ushort home = EndingBranchNeed(current.ReadPersonFirstLocation(person), "special_ruler_HomeE2");
                if (home is < 1 or > 61 || slot is < 46 or > 50) continue;
                byte affiliation = EndingBranchNeed(state.NativeArmySimulation?.Forces.GetValueOrDefault(slot)?.Flags194,
                    "special_Force_dependence194");
                if (affiliation != player) return false;
            }
            return true;
        }

        //43CA90 uses the five actual foreign-country pointers and each
        //valid receiver's ordinal. This is technology acquisition, not Gems.
        bool AllForeignTechnologiesAcquired()
        {
            for (int slot = 0; slot < 5; slot++)
            {
                var country = current.ReadStaticTableReference(0x85A20, slot)
                    ?? throw EndingBranchMissing("foreign_country_pointer");
                int physical = EndingBranchNeed(country.PhysicalSlot, "foreign_country_receiver");
                if (physical == 0) continue; //234060's native invalid placeholder.
                int index = Math.Clamp(physical, 0, 4); //22DE00 followed by its clamp.
                var levels = state.NativeArmySimulation?.Forces.GetValueOrDefault(player)?.ForeignTradeProgress?.AcquiredTechnologyLevels
                    ?? throw EndingBranchMissing("foreign_technology_owner");
                if (levels.Length != 5) throw EndingBranchMissing("foreign_technology_five_levels");
                if (levels[index] != 4) return false;
            }
            return true;
        }

        //43C840: ForceAreaIterator's two development levels, followed by
        //ForceCityIterator's raw size4 count and raw size1 integer percentage.
        bool HasProsperousRealm()
        {
            int developmentSum = 0;
            foreach (var area in current.ReadRegisteredAreaReferences() ?? throw EndingBranchMissing("development_AreaB0"))
            {
                if (area is null) throw EndingBranchMissing("development_Area_pointer");
                if (player is < 1 or > 51 ||
                    EndingBranchNeed(current.ReadAreaCurrentForceSlot(area), "development_Area_Force") != player) continue;
                int id = EndingBranchNeed(area.PhysicalSlot, "development_Area_identity");
                var development = state.Areas.GetValueOrDefault(id)?.Development
                    ?? throw EndingBranchMissing("development_Area_owner");
                ushort commerce = checked((ushort)EndingBranchNeed(development.Commerce, "development_commerce3C"));
                ushort agriculture = checked((ushort)EndingBranchNeed(development.Agriculture, "development_agriculture3E"));
                developmentSum = unchecked(developmentSum + CeAreaIncomeMath.Level(commerce) + CeAreaIncomeMath.Level(agriculture));
            }
            if (developmentSum <= 2699) return false;
            int count = 0, sizeFour = 0, sizeOne = 0;
            for (int slot = 0; slot < 52; slot++)
            {
                var city = current.ReadCityTableReference(slot) ?? throw EndingBranchMissing("development_City_pointer");
                if (player is < 1 or > 51 || EndingBranchNeed(current.ReadBaseForceSlot(city), "development_City_Force") != player) continue;
                int size = EndingBranchNeed(current.ReadCitySizeB8(city), "development_City_sizeB8");
                count = unchecked(count + 1);
                if (size == 4) sizeFour = unchecked(sizeFour + 1);
                if (size == 1) sizeOne = unchecked(sizeOne + 1);
            }
            return sizeFour > 6 && (count < 1 || unchecked(sizeOne * 100) / count < 51);
        }
    }

    private static int EndingPlayer(GameState state, SourceWorldContext current)
        => EndingBranchNeed((OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw EndingBranchMissing("player_Force32EFA0")).PhysicalSlot, "player_Force_identity");

    private static T EndingBranchNeed<T>(T? value, string field) where T : struct
        => value ?? throw EndingBranchMissing(field);
    private static InvalidDataException EndingBranchMissing(string field) => new("campaign.ending.branch." + field);
}
