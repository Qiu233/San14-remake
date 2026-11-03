using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //70CE80/70CBD0/70D0F0 publish an allowed/help pair. The exact same
    //header-owned current capture serves notices and retained submenu rows.
    private MapMainCommandEntryPresentation ReadInformationCommandEntry(GameState state,
        SourceWorldContext current, MapMenuCommand command, bool? restricted, Action bindCurrent)
    {
        int mode = command switch { MapMenuCommand.Spy => 0, MapMenuCommand.Scout => 1,
            MapMenuCommand.Patrol => 2, _ => throw new ArgumentOutOfRangeException(nameof(command)) };
        try
        {
            if (mode != 0)
            {
                if (restricted is null) throw MainCommandMissing("information_application_selection");
                if (restricted.Value) return mode == 1 ? new(command, false, 0x11171)
                    : new(command, false, null) { RetainGameMessage = true };
            }
            bindCurrent();
            var force = current.CurrentPlayerForce ?? throw MainCommandMissing("information_player");
            int forceId = MainNeed(force.PhysicalSlot, "information_player_identity");
            var district = ForcePrimaryDistrictSource.Resolve(current, force);
            int districtId = MainNeed(district.PhysicalSlot, "information_primary_district");
            int books = MainNeed(current.ReadDistrictRawByte(districtId, 0x14), "information_books");
            string bookKey = mode switch { 0 => "home-book", 1 => "away-book", _ => "inspect-book" };
            if (books < IntelligenceParameter(state, bookKey)) return new(command, false, 0x57C7);
            if (mode == 2 && ReadDiplomacyDistrictBudget(current, districtId) < IntelligenceParameter(state, "inspect-military-10"))
                return new(command, false, 0x5C1D);
            if (mode != 0)
            {
                int gold = BaseCurrencyAdditionSource.ReadCurrentGold(ReadPersonnelHireTreasuryCurrent(current, districtId), current);
                if (gold < IntelligenceParameter(state, mode == 1 ? "away-gold-5" : "inspect-gold-10"))
                    return new(command, false, 0x5A1E);
            }
            int people = mode == 2 ? IntelligenceParameter(state, "inspect-minimum-executors") : 1;
            if (!MainInformationHasExecutors(current, forceId, districtId, mode, people))
                return new(command, false, 0x5A1D);
            bool targets = mode == 2
                ? MainInformationHasInspectCities(state, current, districtId, IntelligenceParameter(state, "inspect-minimum-cities"))
                : MainInformationForceTargetCount(state, current, force, forceId, mode) >= IntelligenceParameter(state, "minimum-targets");
            return new(command, targets, mode switch
            {
                0 => targets ? 0x5C19 : 0x5C17,
                1 => targets ? 0x5C1C : 0x5C1A,
                _ => targets ? 0x5C22 : 0x5C21
            });
        }
        catch (InvalidDataException error) { return new(command, null, null, error.Message); }
    }

    private bool MainInformationHasExecutors(SourceWorldContext current, int forceId,
        int districtId, int mode, int minimum)
    {
        if (districtId is < 1 or > 51) return false;
        int count = 0;
        //234850's actual District iterator; no whole-Force, native-validity,
        //page text, sort or ability lookup is part of these entrance queries.
        foreach (int person in NormalTurnServingPeople(current, forceId, districtId, false))
        {
            if (mode != 0 && (MainNeed(OfficerHealthSourceAdapter.ReadByte(current, person), "information_person_health") >= 2 ||
                (MainNeed(current.ReadArmyReturnPersonFlags196(person), "information_person_flags") & 1) != 0 ||
                PersonCurrentBusySource.Read(current, person, MainCommandMissing))) continue;
            if (DuelAftermathSpecialPerson(current, person)) continue;
            //The source compares only after finding a qualifying person. Even
            //a saved nonpositive minimum does not make an empty iterator pass.
            if (++count >= minimum) return true;
        }
        return false;
    }

    private int MainInformationForceTargetCount(GameState state, SourceWorldContext current,
        ForceReference own, int forceId, int mode)
    {
        var list = FinalizationTemporary(current);
        try
        {
            current.ClearTemporaryHexList(list);
            foreach (var receiver in current.ReadRegistryReferences(RegistryKind.Force) ?? throw MainCommandMissing("information_force_registry"))
            {
                if (receiver is not { Kind: RegistryKind.Force, PhysicalSlot: int id })
                    throw MainCommandMissing("information_force_receiver");
                if (id == forceId || id is >= 46 and <= 50) continue;
                //C0 already supplies the actual physical receiver. Looking
                //that ordinal up again could apply an unrelated table alias.
                var target = new ForceReference(id);
                if ((ReadReportForceRelationByte(state, target, forceId, MainCommandMissing) & 127) != 0) continue;
                byte flags = MainNeed(state.NativeArmySimulation?.Forces.GetValueOrDefault(id)?.Flags12, "information_target_flags");
                if ((flags & 2) != 0 &&
                    (MainNeed(state.NativeArmySimulation?.Forces.GetValueOrDefault(forceId)?.Flags12, "information_own_flags") & 2) != 0) continue;
                if (mode == 0 && !(_eventCityNeighborhoods ??= new(_scenario)).AreForceCitiesAdjacent(current, id, forceId)) continue;
                if (mode == 1 && HasIntelligenceAwayMarkerCurrent(state, current, own, target)) continue;
                //6D7F50/6DF5E0 return successful primary-list appends, not
                //a page's Any(Enabled) or number of known Force definitions.
                FinalizationAppend(current, list, receiver);
            }
            return MainNeed(current.ReadTemporaryHexListCount(list), "information_primary_target_count");
        }
        finally { FinalizationRelease(current, list); }
    }

    private bool MainInformationHasInspectCities(GameState state, SourceWorldContext current,
        int districtId, int minimum)
    {
        if (districtId is < 1 or > 51) return false;
        int count = 0;
        //224300 walks the fixed City pointer table0..51, including aliases,
        //then334590 filters raw v78;6BEDE0 also checks actual v80 identity.
        //It does not walk the registered Base list or test native validity.
        for (int slot = 0; slot < 52; slot++)
        {
            var city = current.ReadCityTableReference(slot) ?? throw MainCommandMissing("information_inspect_city_table");
            if (MainNeed(current.ReadBaseDistrictSlot(city), "information_inspect_city_district") != districtId) continue;
            var actual = current.ReadCityDistrictReference(city) ?? throw MainCommandMissing("information_inspect_city_district_pointer");
            if (MainNeed(actual.PhysicalSlot, "information_inspect_city_district_identity") != districtId) continue;
            if (HasIntelligenceInspectCommandCurrent(state, current, city)) continue;
            if (++count >= minimum) return true;
        }
        return false;
    }
}
