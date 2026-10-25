using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //1EC980's successful creation tail. The raid caller passes param2=1;
    //2D2CA0 has already completed registration, governor and group writers.
    private GameState CompleteSpecialForceRaidCreationCurrent(GameState state, SourceWorldContext current,
        int armyId, int personPhysical)
    {
        int rawDistrict = RaidCompletionKnown(current.ReadPersonDistrictSlot(personPhysical), "PersonE0");
        var district = current.ReadDistrictTableReference(rawDistrict <= 51 ? rawDistrict : 0)
            ?? throw RaidCompletionMissing("District_pointer");
        if (RaidCompletionKnown(current.ReadDistrictNativeValidity(district), "District_validity"))
        {
            int physical = RaidCompletionKnown(district.PhysicalSlot, "District_physical");
            int before = RaidCompletionKnown(current.ReadDistrictRawByte(physical, 0x14), "District14");
            byte after = (byte)Math.Clamp(before - 1, 0, 200);
            var owner = state.Districts.GetValueOrDefault(physical) ?? throw RaidCompletionMissing("District_owner");
            var native = state.NativeForceUnregisterCurrent ?? throw RaidCompletionMissing("District_saved_owner");
            var window = native.DistrictWindows10.GetValueOrDefault(physical)?.ToArray()
                ?? throw RaidCompletionMissing("District_saved_window");
            if (window.Length < 5) throw RaidCompletionMissing("District_saved_window_length");
            window[4] = after;
            current.SetDistrictRawByte(physical, 0x14, after);
            state = state with
            {
                Districts = new(state.Districts) { [physical] = owner with { OrdersRemaining = after } },
                NativeForceUnregisterCurrent = native with
                    { DistrictWindows10 = new(native.DistrictWindows10) { [physical] = window } }
            };
            //1D3840 is an application service, independent of the saved BC
            //index. Only a present service reaches that index and233A90.
            bool account = ReadApplicationInformationRestriction(state)
                ?? throw RaidCompletionMissing("Application_user_selection_service");
            if (account && BaseCaptureRewardSource.ReadPlayerRulerDistrict(current, district, RaidCompletionMissing) &&
                (RaidCompletionKnown(current.ReadWorldTransferServiceFlags(), "accounting_World16A8") & 0x100) == 0)
                state = ApplyCommandBookUsageCurrent(state, current, 1);
        }

        var army = state.Units.GetValueOrDefault(armyId) ?? throw RaidCompletionMissing("created_Army_owner");
        int physicalArmy = army.NativeArmyOrigin?.NativeSlotIndex ?? throw RaidCompletionMissing("created_Army_physical");
        var home = current.FindArmyUnitCurrentBase(new(physicalArmy));
        if (RaidCompletionKnown(current.ReadBaseNativeValidity(home), "currency_Home_validity"))
        {
            int land = RaidCompletionKnown(ArmyConfigurationSourceAdapter.ReadCurrentLand(state, army), "Army1B");
            int naval = RaidCompletionKnown(ArmyConfigurationSourceAdapter.ReadNaval(state, army), "Army1C");
            int cost = unchecked(FormationCost(land) + FormationCost(naval));
            state = BaseCurrencyAdditionSource.Add(state, current, home, unchecked(-cost),
                city => BasePostLoad.ReadGoldCapacity(city, current)).State;
        }

        //This raid's action4/5 does not enter the action14 target-repair
        //branch. The final1EC980 search writes48 only when it has that node;
        //it does not store the cached goal1F8 used by the later301DD0 caller.
        army = state.Units[armyId];
        var routed = OrdinaryArmyRouteSource.PrepareCreation(state, current, army);
        var receipt = routed.Receipt ?? throw RaidCompletionMissing(routed.UnresolvedOwner ?? "initial_route");
        if (receipt.NextHexWrite is ushort next)
        {
            var march = army.March ?? throw RaidCompletionMissing("created_Army_March");
            state = state with { Units = new(state.Units) { [armyId] = army with
                { March = march with { NextStep = ArmyMarchSourceAdapter.DecodeDestination(next) } } } };
        }
        return state;

        int FormationCost(int raw)
        {
            int formation = raw < 21 ? raw : 0;
            //The native Formation validity excludes placeholder0. Invalid
            //indices resolve to that receiver before the9A read.
            return formation == 0 ? 0 : RaidCompletionKnown(TacticRemakeCurrentFormations.ReadRawWord(state, formation, 0x9A),
                "Formation9A");
        }
    }

    private static T RaidCompletionKnown<T>(T? value, string field) where T : struct =>
        value ?? throw RaidCompletionMissing(field);
    private static InvalidDataException RaidCompletionMissing(string field) =>
        new("normal-turn.special-force-raids.completion." + field);
}
