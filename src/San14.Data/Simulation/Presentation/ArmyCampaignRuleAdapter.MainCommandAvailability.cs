using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Normal command-menu predicates borrow the header's current
    /// source. Their candidate/permission kernels are shared with the actual
    /// pages and writers, never inferred from the presence of a UI binding.</summary>
    private bool ReadMainCommandAvailabilityCurrent(GameState state, SourceWorldContext current, MapMenuCommand command)
    {
        var force = current.CurrentPlayerForce ?? throw MainCommandMissing("availability_player");
        int forceId = MainNeed(force.PhysicalSlot, "availability_force_identity");
        var district = ForcePrimaryDistrictSource.Resolve(current, force);
        int districtId = MainNeed(district.PhysicalSlot, "availability_primary_district");
        if (!MainNeed(current.ReadDistrictNativeValidity(district), "availability_district_validity")) return false;
        int books = MainNeed(state.Districts.GetValueOrDefault(districtId)?.OrdersRemaining, "availability_command_books");
        bool Restricted() => ReadApplicationInformationRestriction(state) ?? throw MainCommandMissing("availability_selected_user");
        int Gold() => BaseCurrencyAdditionSource.ReadCurrentGold(ReadPersonnelHireTreasuryCurrent(current, districtId), current);
        bool ExecutablePerson(bool idle)
        {
            foreach (int person in NormalTurnServingPeople(current, forceId, districtId, true))
            {
                if (DuelAftermathSpecialPerson(current, person)) continue;
                if (idle && (MainNeed(OfficerHealthSourceAdapter.ReadByte(current, person), "availability_person_health") >= 2 ||
                    (MainNeed(current.ReadArmyReturnPersonFlags196(person), "availability_person_flags") & 1) != 0 ||
                    PersonCurrentBusySource.Read(current, person, MainCommandMissing))) continue;
                return true;
            }
            return false;
        }
        switch (command)
        {
            case MapMenuCommand.Search:
                return !Restricted() && books >= PersonReturnCommands.ReadTalentBookCost(state, TalentOrderKind.Search) && ExecutablePerson(true);
            case MapMenuCommand.Recruit:
                return books >= PersonReturnCommands.ReadTalentBookCost(state, TalentOrderKind.Recruit) && ExecutablePerson(true);
            case MapMenuCommand.Reward:
            {
                //An empty quote reads the actual payer/books without sampling
                //a selected person's loyalty gain or consuming random state.
                var quote = ReadPersonnelRewardPreviewCurrent(state, current, districtId, []);
                return ReadRewardCandidatesCurrent(state, current, districtId).Count != 0 &&
                    books >= quote.BookCost && quote.GoldBefore >= 100;
            }
            case MapMenuCommand.Merit:
            case MapMenuCommand.Award:
            {
                if (Restricted()) return false;
                bool rankup = command == MapMenuCommand.Merit;
                int cost = CreditParameter(state, rankup ? "rankup-books" : "give-books", 1);
                int gold = CreditParameter(state, rankup ? "rankup-gold" : "give-gold", rankup ? 100 : 2500);
                return books >= cost && Gold() >= gold && NormalTurnServingPeople(current, forceId, districtId, true)
                    .Any(person => CreditEligibleCurrent(state, current, person, rankup));
            }
            case MapMenuCommand.Employ:
            {
                if (Restricted() || !PersonnelHireServiceAllowedCurrent(state)) return false;
                var rules = ReadPersonnelHireRulesCurrent(state, current, districtId);
                return rules.MaximumSelection > 0 && rules.Books >= rules.BookCost;
            }
            case MapMenuCommand.Trade:
                return NormalTurnForeignTradeAvailable(state, current, force, district, districtId);
            case MapMenuCommand.Goodwill:
            {
                if (Restricted()) return false;
                var source = new DiplomacyUiSource(this, state, current, _scenario, forceId, districtId,
                    DiplomaticMissionKind.Goodwill, _ => throw MainCommandMissing("availability_card_not_requested"));
                if (source.AvailableBooks < source.RequiredBooks || source.Executors.Count == 0 || source.Targets.Count == 0) return false;
                //70D380 accepts actual currency, retained qualified Items,
                //or a qualified Area. It is not just a treasury threshold.
                if (source.MaxGold >= 1000 || source.MaxSupplies >= 10000) return true;
                var items = source.Items();
                if (items.Value is not { } itemRows) throw new InvalidDataException(items.UnresolvedInput);
                if (itemRows.Count != 0) return true;
                foreach (int target in source.Targets)
                {
                    var areas = source.Areas(target);
                    if (areas.Value is not { } areaRows) throw new InvalidDataException(areas.UnresolvedInput);
                    if (areaRows.Count != 0) return true;
                }
                return false;
            }
            case MapMenuCommand.Spy:
                //70CE80/710B30 does not impose health/busy gates on the
                //resident assignment page; away/inspect use their own gates.
                return books >= IntelligenceParameter(state, "home-book") && ExecutablePerson(false) &&
                    ReadIntelligenceTargetsCurrent(state, current, forceId, 0).Any(row => row.Enabled);
            case MapMenuCommand.Scout:
                return !Restricted() && books >= IntelligenceParameter(state, "away-book") &&
                    Gold() >= IntelligenceParameter(state, "away-gold-5") && ExecutablePerson(true) &&
                    ReadIntelligenceTargetsCurrent(state, current, forceId, 1).Any(row => row.Enabled);
            case MapMenuCommand.Patrol:
                return !Restricted() && books >= IntelligenceParameter(state, "inspect-book") &&
                    ReadDiplomacyDistrictBudget(current, districtId) >= IntelligenceParameter(state, "inspect-military-10") &&
                    Gold() >= IntelligenceParameter(state, "inspect-gold-10") && ExecutablePerson(true) &&
                    ReadIntelligenceTargetsCurrent(state, current, forceId, 2).Any(row => row.Enabled);
            case MapMenuCommand.Rumor:
            {
                if (Restricted()) return false;
                var rules = CaptureStrategicCommandRules(state, current, forceId, 24);
                return rules.Books >= rules.BookCost && rules.Gold >= rules.GoldCost &&
                    rules.MilitaryIntelligence >= 0 && rules.Executors.Count >= StrategicParameter(state, "rumor.minimum-executors", 1) &&
                    rules.Targets.Count >= StrategicParameter(state, "rumor.minimum-targets", 1);
            }
            default: throw MainCommandMissing("availability_command_" + command);
        }
    }

    private static T MainNeed<T>(T? value, string field) where T : struct
        => value ?? throw MainCommandMissing(field);
}
