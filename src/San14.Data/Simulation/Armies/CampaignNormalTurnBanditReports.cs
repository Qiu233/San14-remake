using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //25D2B0's reached player-City tail. The caller has already created real
    //Armies; this method only formats source-time work and writes history.
    //The enclosing incursion operation publishes its one ordered effect batch.
    internal GameState ApplyNormalTurnIncursionReportCurrent(GameState state, SourceWorldContext current,
        int cityPhysical, int areaPhysical, int bandDistrict, List<CampaignEffectRequest> effects)
    {
        var city = current.ReadCityTableReference(cityPhysical < 52 ? cityPhysical : 0)
            ?? throw BanditReportMissing("City_pointer");
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw BanditReportMissing("player_Force");
        if (!BanditReportNeed(current.ReadForceNativeValidity(player), "player_Force_validity")) return state;
        int owner = BanditReportNeed(current.ReadBaseForceSlot(city), "City_Force");
        var force = current.ReadForceTableReference(owner < 52 ? owner : 0)
            ?? throw BanditReportMissing("City_Force_pointer");
        if (force != player) return state;

        //405E40's first operand is the host, not a sound number. Its actual
        //second operand is16; no BSS audio-handle value enters game rules.
        effects.Add(new(null, null) { TalentCommandPresentation = new(
            TalentCommandPresentationOperation.SoundCue, 16, null, null, null, null) });
        int reporter = Lookup(BanditReportNeed(current.ReadArmyReturnBaseGovernor2E(city), "City_governor2E"));
        if (!Valid(reporter))
        {
            var district = current.ReadBaseDistrictReference(city) ?? throw BanditReportMissing("City_District_virtual80");
            var pool = current.ReadOwnedCurrentListPool() ?? throw BanditReportMissing("speaker_shared_pool");
            var selected = new PersonDeathHistorySelection(state, current, pool, SearchAbilities(current), _governorInputs,
                (forceId, effect) => _baseCapacityInputs.ReadPanelModifier(state, forceId, effect)).Select(district);
            reporter = BanditReportNeed(selected.PhysicalSlot, "speaker_2B44E0_pointer");
        }
        int reporterId = BanditReportNeed(current.ReadPersonIdentity(reporter), "speaker_ID");
        int index = bandDistrict is >= 46 and <= 50 ? bandDistrict - 45 : 0;
        int announcementRoot = 0x5E2B + index;
        int mainRoot = 0x5E32 + index;
        int supplementalRoot = bandDistrict is >= 46 and <= 50 ? 0x509E + (bandDistrict - 46) * 7 : 0x5097;
        var noticeOperands = bandDistrict is 47 or 51 || bandDistrict is < 46 or > 51
            ? new HistoryMessageOperands(Person0: reporterId, Area0: areaPhysical)
            : new HistoryMessageOperands(Area0: areaPhysical);
        var notice = Format(announcementRoot, noticeOperands);
        RequireNoDetachedVoice(notice, "announcement");
        effects.Add(new(null, null) { TalentCommandPresentation = new(
            TalentCommandPresentationOperation.Announcement, null, null, null, null, notice.Text) });

        var main = Format(mainRoot, new(Area0: areaPhysical));
        int ruler = BanditReportNeed(current.ReadMoraleWaveRuler22EAC0(force), "City_Force_ruler");
        var supplemental = Format(supplementalRoot, new(Person0: reporterId, Person1: ruler, Area0: areaPhysical));
        RequireNoDetachedVoice(main, "history_main");
        RequireNoDetachedVoice(supplemental, "history_supplemental");
        var primary = ForcePrimaryDistrictSource.Resolve(current, player);
        int districtId = BanditReportNeed(current.ReadDistrictPhysicalSlot(primary), "player_primary_District");
        int hex = BanditReportNeed(current.ReadAreaHexIndex(new(areaPhysical)), "Area_Hex3A");
        int day = (ReadArmyDestructionSampler(state, current) ?? throw BanditReportMissing("World450")).Day450;
        return WriteHistory(state, current, new(1, districtId, hex, day, main.Text, supplemental.Text,
            Valid(reporter) ? reporterId : 5012, MarkBit0: 0, MarkBit4: 1), effects);

        int Lookup(int id) => (id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot
            ?? throw BanditReportMissing("Person_lookup");
        bool Valid(int person) => BanditReportNeed(current.ReadPersonNativeValidity(person), "speaker_validity");
        HistoryTextResult Format(int root, HistoryMessageOperands operands)
        {
            var formatted = FormatNormalTurnReport(state, current, root, operands, next => state = next);
            state = formatted.State;
            return formatted;
        }
    }

    //No OfficerText call exists in this source tail. A future message variant
    //with a detached voice needs its actual playback intent, not a fabricated
    //officer subtitle or a silently discarded voice request.
    private static void RequireNoDetachedVoice(HistoryTextResult text, string branch)
    {
        if (text.Voices.Count != 0) throw BanditReportMissing(branch + "_detached_resource_voice");
    }
    private static T BanditReportNeed<T>(T? value, string owner) where T : struct => value ?? throw BanditReportMissing(owner);
    private static InvalidDataException BanditReportMissing(string owner) => new("normal.turn.bandit-report." + owner);
}
