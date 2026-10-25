using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //26DA10, after both2E9140 calls and before the winning Force refresh.
    //222F20/334660 retain manager60 order and its Home/233560 predicates.
    //The caller retains all capture temporaries and owns final publication.
    internal GameState ApplyBaseCaptureHomeArmiesCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, BaseReference captured,
        NativeMapSeasonDate? operationDate, List<CampaignEffectRequest> effects)
    {
        if (!ReferenceEquals(pool.ArmyReturnOwner, current)) throw HomeArmyMissing("current_pool_binding");
        int location = HomeArmyNeed(current.ReadBaseObjectLink(captured), "Base4E");
        if (location is < 1 or > 61) return state;
        Dictionary<int, OfficerState>? changedPeople = null;
        Dictionary<int, UnitState>? changedUnits = null;
        var descriptor = current.ReadRegistryDescriptor(RegistryKind.Army)
            ?? throw HomeArmyMissing("manager60_descriptor");
        var node = current.ReadRegistryHead(descriptor) ?? throw HomeArmyMissing("manager60_head");
        while (node.Identity.HasValue)
        {
            var entry = current.ReadRegistryNodePayload(node) ?? throw HomeArmyMissing("manager60_payload");
            if (entry is not { Kind: RegistryKind.Army, PhysicalSlot: int physical })
                throw HomeArmyMissing("manager60_Army_pointer");
            if (!HomeArmyNeed(current.ReadOwnedArmyExclusion233560(entry), "Army233560"))
            {
                var army = new ArmyUnitReference(physical);
                int raw = HomeArmyNeed(current.ReadArmyUnitCommanderId(army), "Army12");
                int commander = HomeArmyNeed((raw <= 6000 ? current.ReadPersonLookupReference(raw)
                    : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "Commander_lookup");
                //22A7A0 validates Person before readingE2. Only a matching
                //iterator payload reaches26CDF0's independent Army validity.
                if (HomeArmyNeed(current.ReadPersonNativeValidity(commander), "Commander_validity") &&
                    HomeArmyNeed(current.ReadPersonFirstLocation(commander), "CommanderE2") == location &&
                    HomeArmyNeed(current.ReadArmyUnitNativeValidity(army), "Army_validity"))
                    ApplyOne(physical, commander);
            }
            //Cause4 admits pending70 but does not unlink manager60. Read the
            //actual next link after its callbacks rather than a copied roster.
            node = current.ReadRegistryNodeNext(node) ?? throw HomeArmyMissing("manager60_next");
        }
        Flush();
        return state;

        void Flush()
        {
            if (changedPeople is null && changedUnits is null) return;
            state = state with { Officers = changedPeople ?? state.Officers, Units = changedUnits ?? state.Units };
            changedPeople = null; changedUnits = null;
        }
        void ApplyOne(int physical, int commander)
        {
            //22BB10 has already passed the native Person gate above.
            int rawDistrict = HomeArmyNeed(current.ReadPersonDistrictSlot(commander), "CommanderE0");
            var district = current.ReadDistrictTableReference(rawDistrict < 52 ? rawDistrict : 0)
                ?? throw HomeArmyMissing("District_pointer");
            int districtId = HomeArmyNeed(district.PhysicalSlot, "District_physical");
            int forceId = HomeArmyNeed(current.ReadDistrictForceSlot(district), "District10");
            //234690 is its physical/Force/number predicate, not the separate
            //virtual validity which also requires a living district leader.
            if (districtId == 0 || forceId == 0 ||
                HomeArmyNeed(current.ReadDistrictNumber(districtId), "District11") == 0) return;
            var origin = BaseLookup.Find(HomeArmyNeed(current.ReadPersonFirstLocation(commander), "current_CommanderE2"), current);
            BaseReference? excluded = HomeArmyNeed(current.ReadBaseForceSlot(origin), "Home_Force") == forceId ? null : origin;
            int chosen = ChooseStrategicIndependenceArmyHomeCurrent(current, districtId, excluded);
            if (chosen is < 1 or > 61)
            {
                //The exact259360(null,null,0,4) path owns real resources,
                //Return/freedom, captive release, feedback and pending70.
                Flush();
                var destroyed = new OwnedArmyDestructionStage(this).ApplyAbsorptionCommanderCause4Current(
                    state, current, pool, physical, operationDate ?? state.NativeMapSeason?.ThirdDate
                        ?? throw HomeArmyMissing("cause4_operation_date"), effects);
                state = destroyed.State ?? throw HomeArmyMissing(destroyed.UnresolvedOwner ?? "259360_cause4");
                return;
            }
            WriteHome(commander, (ushort)chosen);
            var captives = current.CreateNearbyTemporary();
            try
            {
                OwnedArmyDestructionStage.CollectCaptivesCurrent(current, physical, captives);
                var item = current.ReadRegistryHead(new(captives.Identity)) ?? throw HomeArmyMissing("captives_head");
                while (item.Identity.HasValue)
                {
                    var person = current.ReadRegistryNodePayload(item) ?? throw HomeArmyMissing("captives_payload");
                    if (person is not { Kind: RegistryKind.Person, PhysicalSlot: int captive })
                        throw HomeArmyMissing("captives_Person_pointer");
                    ushort home = HomeArmyNeed(current.ReadPersonFirstLocation(commander), "fresh_CommanderE2");
                    WriteHome(captive, (ushort)Math.Min((int)home, 61));
                    item = current.ReadRegistryNodeNext(item) ?? throw HomeArmyMissing("captives_next");
                }
            }
            finally { current.ClearNearbyTemporary(captives, true); }
            PublishArmyHome(physical, chosen);
        }
        void WriteHome(int physical, ushort home)
        {
            current.WritePersonFirstLocation(physical, home);
            int id = HomeArmyNeed(current.ReadPersonIdentity(physical), "home_Person10");
            var person = (changedPeople ?? state.Officers).GetValueOrDefault(id) ?? throw HomeArmyMissing("home_Person_owner");
            if (current.ResolveOfficerSource(id) != physical) throw HomeArmyMissing("home_Person_binding");
            //E2 is home, independent of E4/position, task and custody owner.
            (changedPeople ??= new(state.Officers))[id] = person with { LocationId = home };
        }
        void PublishArmyHome(int physical, int home)
        {
            var arena = state.ReadArmyLifecycle() ?? throw HomeArmyMissing("lifecycle_owner");
            if ((uint)physical >= arena.Slots.Count) throw HomeArmyMissing("source_slot");
            var receiver = arena.Receivers.Find(arena.Slots[physical].ReceiverId)
                ?? throw HomeArmyMissing("receiver_owner");
            if (receiver.ActiveUnitId is int id)
            {
                var image = (changedUnits ?? state.Units).GetValueOrDefault(id) ?? throw HomeArmyMissing("active_Army_image");
                (changedUnits ??= new(state.Units))[id] = image with { SourceLocationId = home };
            }
            else
            {
                var image = receiver.PendingImage ?? throw HomeArmyMissing("pending_Army_image");
                state = state.WithArmyLifecycle(arena with
                    { Receivers = arena.Receivers.With(receiver with { PendingImage = image with { SourceLocationId = home } }) });
            }
        }
    }
    private static T HomeArmyNeed<T>(T? value, string field) where T : struct => value ?? throw HomeArmyMissing(field);
    private static InvalidDataException HomeArmyMissing(string field) => new("Base-capture.HomeArmy26CDF0." + field);
}
