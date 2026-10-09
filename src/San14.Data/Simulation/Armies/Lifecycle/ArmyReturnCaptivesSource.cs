using San14.Core;

namespace San14.Data;

public sealed record ArmyReturnCaptivesOwners(bool? NativeGameStatePresent);
public sealed record ArmyReturnCaptivesReceipt(int[] PersonOrder, int NativeReturn,
    PersonPendingMeritReceipt? CommanderMerit, int GuardRequirement, bool GuardShortage);
public sealed record ArmyReturnCaptivesResult(GameState? State,
    ArmyReturnCaptivesReceipt? Receipt, string? UnresolvedOwner);

/// <summary>2F8F00's player/native-IGameState-absent branch, including actual
/// shared Person/Item temporaries, pending merit and the reached guard query.
/// Prisoners stay status6 with their original service. No player treatment
/// decision, AI treatment or nonempty Item transfer is inferred.</summary>
public sealed class ArmyReturnCaptivesSource(ArmyCampaignRuleAdapter package,
    GameState input, ArmyReturnCaptivesOwners owners)
{
    private GameState _working = input;

    internal string? ApplyTransportPersonReturn(SourceWorldContext current, int commanderPhysical)
    {
        var returned = TransportPersonReturnSource.TryApplyCurrent(_working, current, commanderPhysical);
        if (returned.State is not { } continued || returned.NativeReturn is null)
            return returned.UnresolvedOwner ?? "current.transport_PersonReturn2EA9A0";
        _working = continued;
        return null;
    }

    /// <summary>29F5xx type2 merit runs after morale and before cargo debit.
    /// Its returned state is retained through the later captive publication.</summary>
    internal string? ApplyTransportMerit(SourceWorldContext current,
        ArmyUnitReference army, int acceptedCargo)
    {
        try
        {
            int parameter = P(_working, SimulationParameterKeys.ArmyDestruction.TransportReturnCommanderMerit);
            float requested = ((float)acceptedCargo / 100000.0f + 1.0f) * unchecked(parameter * 10);
            int amount = !float.IsFinite(requested) || requested < int.MinValue || requested >= 2147483648f
                ? int.MinValue : (int)requested;
            GameState working = OwnedArmyReturnCurrentFields.PublishCurrent(_working, current);
            ushort commanderId = K(current.ReadArmyUnitCommanderId(army), "29F5xx Army12");
            var result = package.ApplyReturnCaptiveMerit(working, current, commanderId, amount);
            if (result.Outcome == PersonPendingMeritOutcome.CurrentInputsUnresolved)
                return "current.transport1695C0";
            _working = result.State;
            return null;
        }
        catch (InvalidDataException error) { return error.Message; }
    }

    internal ArmyReturnCaptivesResult ApplyReport(SourceWorldContext current,
        HexListHandle output, ArmyUnitReference army, BaseReference target, int nativeMode)
    {
        try
        {
            int[] people = FillArmyAssociates(current, output, army);
            if (people.Length == 0) return new(_working, new([], 0, null, 0, false), null);
            //Home, traits and resource writes preceding2F8F00 are already in
            //this same private current. Merit must see those current Persons.
            GameState working = OwnedArmyReturnCurrentFields.PublishCurrent(_working, current);
            ushort commanderId = K(current.ReadArmyUnitCommanderId(army), "2F8F00 Army12");
            int commander = Person(current, commanderId);
            PersonPendingMeritReceipt? merit = null;
            if (K(current.ReadPersonNativeValidity(commander), "2F8F00 Commander validity"))
            {
                int amount = unchecked(P(working, SimulationParameterKeys.ArmyDestruction.CaptiveReturnCommanderMerit) * 10);
                var earned = package.ApplyReturnCaptiveMerit(working, current, commanderId, amount);
                if (earned.Outcome == PersonPendingMeritOutcome.CurrentInputsUnresolved)
                    throw Missing("current.2F8F00_Commander1695C0");
                working = earned.State; merit = earned.Receipt;
            }
            _ = Person(current, K(current.ReadArmyReturnBaseGovernor2E(target), "2F8F00 Base2E lookup")); //22ACF0.
            int forceId = K(current.ReadBaseForceSlot(target), "2F8F00 BaseForceB0");
            var force = current.ReadForceTableReference(forceId) ?? throw Missing("2F8F00 Force pointer");
            _ = Person(current, current.ReadForceRecord(forceId)?.RawRuler10 ?? throw Missing("2F8F00 Force10 lookup"));
            int location = K(current.ReadBaseObjectLink(target), "2F8F00 Base4E");
            int requirement; bool shortage;
            if (location is >= 52 and <= 61)
            {
                int sum = 0;
                foreach (int person in RegisteredPersons(current))
                {
                    byte status = K(current.ReadPersonStatus(person), "234920 PersonE6");
                    //Dependency projection: ineligible status cannot enter the
                    //read-only Home iterator even when its E2 is not exposed.
                    if (status is < 1 or > 4) continue;
                    if (K(current.ReadPersonFirstLocation(person), "234920 PersonE2") != location) continue;
                    sum = unchecked(sum + ReadGuard(working, current, person));
                }
                var gateCaptives = NewTemporary(current);
                try
                {
                    current.ClearTemporaryHexList(gateCaptives);
                    int count = FillBaseAssociates(current, gateCaptives, location).Length;
                    requirement = unchecked(unchecked((count + 1) * P(working, SimulationParameterKeys.ArmyDestruction.CaptiveTroopRequirementAddend)) + unchecked(P(working, SimulationParameterKeys.ArmyDestruction.GateCaptiveGuardMultiplier) * sum));
                    shortage = K(current.ReadBaseResource(target, BaseResourceKind.Troops), "2F8F00 Gate virtual90") < requirement;
                }
                finally { Release(current, gateCaptives); }
            }
            else
            {
                //1017FE0 is a direct int32 Base50 getter, not268230.
                int ready = K(current.ReadBaseResource(target, BaseResourceKind.Troops), "2F8F00 City virtual90");
                int wounds = K(current.ReadArmyReturnBaseWounded40(target), "2F8F00 City wounded22DAA0");
                int raw50 = K(current.ReadBaseReturnField50(target), "current.2F8F00_City_raw50");
                requirement = unchecked(raw50 + P(working, SimulationParameterKeys.ArmyDestruction.CaptiveTroopRequirementAddend));
                shortage = unchecked(ready + wounds) < requirement;
            }
            foreach (int person in people) ReadEmptyItemTransfer(current, target, person);
            var player = current.ReadCurrentPlayerForce() ?? throw Missing("2F8F00 World3A PlayerForce");
            bool isPlayer = K(current.ReadForceNativeValidity(player), "2F8F00 Player validity") && player == force;
            if (!isPlayer) throw Missing("branch.2F8F00_AI_prisoner_treatment");
            if (nativeMode != 0) throw Missing("branch.2F8F00_nonzero_mode_E2_162_writes");
            if (owners.NativeGameStatePresent is not false)
                throw Missing("current.2F8F00_571630_nativeIGameState_or_TreatPrisonerState");
            //Source null571630 skips2441D0/573900. The final loop still runs.
            foreach (int person in people)
                if (K(current.ReadPersonNativeValidity(person), "2F8F00 final Person validity"))
                    current.WriteArmyReturnPersonFlags196(person, (ushort)(K(current.ReadArmyReturnPersonFlags196(person), "2F8F00 final15E") & 0xFFEF));
            return new(working, new(people, 1, merit, requirement, shortage), null);
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }

    internal static int[] FillArmyAssociates(SourceWorldContext current, HexListHandle output,
        ArmyUnitReference army) => FillBaseAssociates(current, output,
            army.PhysicalSlot is >= 1 and <= 500 ? army.PhysicalSlot.Value + 61 : 0);

    internal static int[] FillBaseAssociates(SourceWorldContext current, HexListHandle output, int location)
    {
        var people = new List<int>();
        foreach (int person in RegisteredPersons(current))
        {
            if (K(current.ReadPersonStatus(person), "230C70 PersonE6") != 6) continue;
            if (K(current.ReadPersonSecondLocation(person), "230C70 PersonE4") != location) continue;
            if (!current.AppendPersonReturnTemporary(output, person)) throw Missing("branch.230C70_node_allocation");
            people.Add(person);
        }
        return people.ToArray();
    }

    private static int ReadGuard(GameState state, SourceWorldContext current, int person)
    {
        if (!K(current.ReadPersonNativeValidity(person), "2B4DE0 Person validity")) return 0;
        byte post = K(OwnedArmyMoraleWaveInputs.ReadPersonPostE7(state, current, person), "2B4DE0 PostE7");
        int selected = K(OwnedArmyMoraleWaveInputs.ReadPostNativeValidity(state, post), "2B4DE0 Post validity") ? post : 73;
        var values = state.NativeArmySimulation?.Parameters.Int32Values ?? throw Missing("2B4DE0 saved parameter owner");
        if (!values.TryGetValue(OwnedArmyGovernorComparatorInputs.OfficeGuard36Key(selected), out int office) || office is < 0 or > 255)
            throw Missing("current.2B4DE0_Post36");
        int amount = unchecked(office * P(state, SimulationParameterKeys.ArmyDestruction.CaptiveGuardOfficeMultiplier));
        if ((K(current.ReadArmyReturnPersonFlags196(person), "2B4DE0 Person15E") & 0x4000) != 0) amount = P(state, SimulationParameterKeys.ArmyDestruction.CaptiveGuardFlaggedAmount);
        int effect = K(TacticRemakeCurrentTraits.CommanderEffectSum(current, person, 46), "2B4DE0 effect46");
        int trait = unchecked(P(state, SimulationParameterKeys.ArmyDestruction.CaptiveGuardTraitMultiplier) * effect);
        var force = current.ReadPersonForce(person) ?? throw Missing("2B4DE0 current PersonForce");
        var advantage = PersonReturnPathSource.ReadForceAdvantage(state, current, force, 12);
        int land = K(advantage.Value, advantage.UnresolvedOwner ?? "2B4DE0 advantage12");
        int first = unchecked(unchecked(P(state, SimulationParameterKeys.ArmyDestruction.CaptiveGuardLandMultiplier) * land) * amount);
        return unchecked(unchecked(amount - first / 100) - unchecked(trait * amount) / 100);
    }

    private static void ReadEmptyItemTransfer(SourceWorldContext current, BaseReference target, int person)
    {
        var district = current.ReadBaseDistrictReference(target) ?? throw Missing("2F8F00 target District80");
        int districtSlot = K(district.PhysicalSlot, "2F8F00 District pointer");
        int leader = Person(current, K(current.ReadDistrictLeader(districtSlot), "2F8F00 District12"));
        var leaderForce = current.ReadPersonForce(leader) ?? throw Missing("2F8F00 leaderForce22EA50");
        int ruler = K(current.ReadForceNativeValidity(leaderForce), "2F8F00 leaderForce validity")
            ? Person(current, current.ReadForceRecord(K(leaderForce.PhysicalSlot, "2F8F00 leaderForce pointer"))?.RawRuler10 ?? throw Missing("2F8F00 leaderForce ruler10")) : Person(current, 0);
        if (!K(current.ReadPersonNativeValidity(ruler), "2F8F00 item receiver validity")) return;
        var temporary = NewTemporary(current);
        try
        {
            current.ClearTemporaryHexList(temporary);
            ushort id = K(current.ReadPersonIdentity(person), "2F8F00 Item current Person10");
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Item) ?? throw Missing("2F8F00 Item registry"))
            {
                var item = entry ?? throw Missing("2F8F00 Item payload");
                if (item.Kind != RegistryKind.Item || item.PhysicalSlot is not int physical) throw Missing("2F8F00 Item pointer");
                if (K(current.ReadCaptureItemOwner4A(physical), "2F8F00 Item4A") == id)
                    throw Missing("branch.2F8F00_nonempty_Item_transfer");
            }
        }
        finally { Release(current, temporary); }
    }

    private static int[] RegisteredPersons(SourceWorldContext current) =>
        (current.ReadRegisteredPersonPhysicalSlots() ?? throw Missing("230C70 manager100")).Select(x => x ?? throw Missing("230C70 Person payload")).ToArray();
    private static int Person(SourceWorldContext current, ushort id) =>
        (current.ReadPersonLookupReference(id <= 6000 ? id : 0) ?? throw Missing("2F8F00 Person lookup")).PhysicalSlot ?? throw Missing("2F8F00 Person pointer");
    private static HexListHandle NewTemporary(SourceWorldContext current)
    {
        var temporary = current.CreateTemporaryHexList() ?? throw Missing("2F8F00 temporary allocation");
        if (!temporary.Identity.HasValue) throw Missing("branch.2F8F00_descriptor_allocation");
        return temporary;
    }
    private static void Release(SourceWorldContext current, HexListHandle descriptor)
    { current.ClearTemporaryHexList(descriptor); current.ReleaseTemporaryHexList(descriptor); }
    private static int P(GameState state, string key) => K(ArmyDestructionParameterSeed.ReadCurrent(state, key), $"2F8F00 parameter TC{key}");
    private static T K<T>(T? value, string owner) where T : struct => value ?? throw Missing(owner);
    private static InvalidDataException Missing(string owner) => new(owner);
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal PersonPendingMeritResult ApplyReturnCaptiveMerit(GameState state, SourceWorldContext current,
        int personId, int requested) => _pendingMerit.TryApplyWithinArmyOperation(state, current, personId, requested);
}
