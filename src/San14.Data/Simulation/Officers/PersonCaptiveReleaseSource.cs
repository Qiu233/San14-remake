using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>2E7890 notification1, service return and the reached ordinary
    /// freedom branches. The enclosing operation owns current and its live temporary
    /// lists; this leaf never captures, imports or strictly exports a world.</summary>
    internal GameState ApplyCaptiveReleaseCurrent(GameState input, SourceWorldContext current,
        int personPhysical, NativeMapSeasonDate? sourceDate, Func<int> day450,
        List<CampaignEffectRequest> sink)
        => ApplyCaptiveReleaseCurrent(input, current, personPhysical, sourceDate, day450, sink, null);

    //The production defeat owner can retain the complete phase sampler for
    //a reached freedom shuffle; Day450 alone cannot supply its Salt45C.
    internal GameState ApplyCaptiveReleaseCurrent(GameState input, SourceWorldContext current,
        int personPhysical, NativeMapSeasonDate? sourceDate, Func<int> day450,
        List<CampaignEffectRequest> sink, TacticSourceSamplerWorld? sourceSampler)
        => ApplyCaptiveReleaseCurrent(input, current, personPhysical, sourceDate, day450, sink, sourceSampler, true);

    //29FA40 uses actual2E7890 notification0. Holder resolution and all
    //release/return/freedom writers are shared; only its report gate differs.
    internal GameState ApplyCaptiveReleaseCurrent(GameState input, SourceWorldContext current,
        int personPhysical, NativeMapSeasonDate? sourceDate, Func<int> day450,
        List<CampaignEffectRequest> sink, TacticSourceSamplerWorld? sourceSampler, bool reportRelease)
    {
        InvalidDataException Missing(string field) => new("person-captive-release." + field);
        T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
        if (!Need(current.ReadPersonNativeValidity(personPhysical), "Person_validity")) return input;
        var working = input;
        TransferPersonReference Person(ushort raw) => (raw <= 6000
            ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
            ?? throw Missing("Person_lookup");
        DistrictReference District(byte raw) => current.ReadDistrictTableReference(raw <= 51 ? raw : 0)
            ?? throw Missing("District_lookup");
        bool IsPlayer(ForceReference force)
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(working, current)
                ?? throw Missing("Player_Force_pointer");
            int ordinal = Need(current.ReadForcePhysicalSlot(player), "Player_Force_ordinal");
            return ordinal != 0 && ordinal == Need(current.ReadForcePhysicalSlot(force), "Force_ordinal");
        }
        NativeMapSeasonDate Date() => sourceDate ?? working.NativeMapSeason?.ThirdDate
            ?? throw Missing("return_current_World_third_date");
        TacticSourceSamplerWorld Sampler() => sourceSampler ?? ReadArmyDestructionSampler(working, current)
            ?? throw Missing("freedom_current_World450_45C");
        WorldIndexContext Pool() => current.ReadOwnedCurrentListPool()
            ?? throw Missing("freedom_current_pool");

        //231D90: unknown E4 is not an empty holder. Known tagFF/Area really
        //leave both retained pointers null, independently of table slot0.
        ArmyUnitReference holderArmy = new(null);
        BaseReference holderBase = new(BaseKind.City, null);
        DistrictReference holderDistrict = new(null);
        var holder = PersonPositionRepair.DecodeSecondLocation(Need(current.ReadPersonSecondLocation(personPhysical), "holder_E4"));
        switch (holder.Kind)
        {
            case 27:
                holderArmy = current.ReadArmyUnitTableReference(holder.Index < 501 ? holder.Index : 0)
                    ?? throw Missing("holder_Army_lookup");
                break;
            case 5:
                holderBase = current.ReadCityTableReference(holder.Index < 52 ? holder.Index : 0)
                    ?? throw Missing("holder_City_lookup");
                break;
            case 6:
                holderBase = current.ReadGateTableReference(holder.Index < 11 ? holder.Index : 0)
                    ?? throw Missing("holder_Gate_lookup");
                break;
        }
        if (Need(current.ReadArmyUnitNativeValidity(holderArmy), "holder_Army_validity"))
        {
            var commander = Person(Need(current.ReadArmyUnitCommanderId(holderArmy), "holder_Army12"));
            byte district = commander.PhysicalSlot is int commanderPhysical &&
                Need(current.ReadPersonNativeValidity(commanderPhysical), "holder_Commander_validity")
                ? Need(current.ReadPersonDistrictSlot(commanderPhysical), "holder_Commander_E0") : (byte)0;
            holderDistrict = District(district);
        }
        else if (Need(current.ReadBaseNativeValidity(holderBase), "holder_Base_validity"))
            holderDistrict = current.ReadBaseDistrictReference(holderBase) ?? throw Missing("holder_Base_virtual80");
        if (reportRelease)
            ApplyCaptiveReleaseReports(current, () => working, value => working = value,
                personPhysical, holderArmy, holderBase, holderDistrict, day450, sink);

        //The original force is reread after both actual scratch messages and
        //history.22EA10 reads the ruler's E2 without a ruler validity gate.
        var originalForce = current.ReadPersonForce(personPhysical) ?? throw Missing("original_Force22CB40");
        BaseReference homeCity = new(BaseKind.City, null);
        if (Need(current.ReadForceNativeValidity(originalForce), "original_Force_validity"))
        {
            int force = Need(originalForce.PhysicalSlot, "original_Force_pointer");
            var ruler = Person(Need(current.ReadForceRecord(force)?.RawRuler10, "original_Force_ruler10"));
            int rulerPhysical = Need(ruler.PhysicalSlot, "ruler_pointer_for_E2");
            var rulerHome = BaseLookup.Find(Need(current.ReadPersonFirstLocation(rulerPhysical), "ruler_E2"), current);
            homeCity = CurrentGateMapQueries.FindCity(rulerHome, current);
        }
        if (!Need(current.ReadBaseNativeValidity(homeCity), "origin_City_validity"))
            return ApplyCaptiveNoHomeFreedomCurrent(working, current, Pool(), personPhysical, sourceDate, day450, Sampler, sink);

        current.WritePersonFirstLocation(personPhysical,
            Math.Min(Need(current.ReadBaseObjectLink(homeCity), "origin_City4E"), (ushort)61));
        current.WritePersonStatus(personPhysical, 4);
        var primary = ForcePrimaryDistrictSource.Resolve(current, originalForce);
        int targetDistrict = Need(current.ReadDistrictPhysicalSlot(primary), "primary_District22DD20");
        var associations = new List<PersonAssignmentPrecallResult>();
        var transfer = ArmyOrdinaryReturnRuntime.CreateCurrentPersonDistrictTransfer(current, associations, result =>
        {
            //2783B0's actual3452 history belongs before240DB0 writesE0.
            //Adopt its formatter/history State in this same current operation.
            working = ApplyPersonAssociationRemovalReports(working, current, personPhysical, result, day450, sink);
        });
        PersonDistrictTransfer.Apply(personPhysical, targetDistrict, transfer);
        if (associations.Count != 0)
            working = PersonAssignmentPrecallInputs.PublishDeathTouched(working, current, personPhysical, associations);
        current.WriteFormerForceKnowledge(personPhysical, 0);

        //2EA9A0 repeats Force/Home ownership after the actual status/E0/165
        //writes. A failed owner takes265250 without the no-origin resentment
        //prefix; the retained original Force's service tail still follows.
        var currentForce = current.ReadPersonForce(personPhysical) ?? throw Missing("return_Force22CB40");
        bool freedomRequired = !Need(current.ReadForceNativeValidity(currentForce), "return_Force_validity");
        BaseReference returnHome = new(BaseKind.City, null);
        if (!freedomRequired)
        {
            returnHome = BaseLookup.Find(Need(current.ReadPersonFirstLocation(personPhysical), "return_E2"), current);
            freedomRequired = !Need(current.ReadBaseNativeValidity(returnHome), "return_Home_validity");
            if (!freedomRequired)
                freedomRequired = Need(current.ReadBaseForceSlot(returnHome), "return_Home_virtualB0") !=
                    Need(current.ReadPersonOldForceSlot(personPhysical), "return_Person22CF60");
        }
        if (freedomRequired)
            working = ApplyCaptiveFreedomCurrent(working, current, Pool(), personPhysical, sourceDate, day450, Sampler, sink);
        else
        {
            var position = PersonReturnCommands.ResolvePersonPosition(working, current, personPhysical, Date);
            //A current journey position query may initialize its real deadline.
            //Adopt that latest manager before preparing the next command.
            if (current.ReadOwnedPersonCommandManager() is { } positionedOwner)
                working = working with { PersonnelCommands = positionedOwner };
            int positionHex = Need(position.PhysicalSlot, "return_position22DEF0");
            if (positionHex == current.ReadBaseCurrentHexIndex(returnHome))
                current.WritePersonSecondLocation(personPhysical,
                    Math.Min(Need(current.ReadBaseObjectLink(returnHome), "return_same_Home4E"), (ushort)1061));
            else
            {
                //2EA9A0/1F24F0 really creates a new receiver even when22D6C0
                //finds an earlier associated command.1F68B0 keeps that old
                //member chain, reads its current position before appending the
                //new member, and leaves normal command retirement to its owner.
                var prepared = PersonReturnCommands.Prepare(working, current, personPhysical, Date());
                var packet = prepared.Packet ?? throw Missing(prepared.UnresolvedOwner ?? "return_Prepare");
                var applied = PersonReturnCommands.Apply(working, current, packet);
                working = applied.State ?? throw Missing(applied.UnresolvedOwner ?? "return_Apply");
                if (applied.CommandId.HasValue)
                {
                    var pool = current.ReadOwnedCurrentListPool() ?? throw Missing("return_current_pool");
                    var lifecycle = working.ReadArmyLifecycle() ?? throw Missing("return_saved_lifecycle");
                    working = working.WithArmyLifecycle(lifecycle with { Indexes = pool.ProjectOwnedRoles() });
                }
            }
            working = PublishPerson(working);
            working = current.PublishOfficerAftermath(working, Need(current.ReadPersonIdentity(personPhysical), "Person10"));
        }
        //This is the retained original Force's separate3E7DF0(1,1,0), not
        //the earlier240DB0 service eligibility call.
        if (IsPlayer(originalForce)) transfer.RefreshTransferService(1, 1, 0);
        return working;

        GameState PublishPerson(GameState state)
        {
            ushort id = Need(current.ReadPersonIdentity(personPhysical), "publish_Person10");
            if (!state.Officers.TryGetValue(id, out var person) || current.ResolveOfficerSource(id) != personPhysical)
                throw Missing("publish_Person_identity");
            var native = state.NativeArmySimulation ?? throw Missing("publish_detached_owner");
            if (native.Parameters.Int32Values.GetValueOrDefault(OwnedArmyLifecycleDetachedInputs.VersionKey) != 1)
                throw Missing("publish_detached_version");
            ushort first = Need(current.ReadPersonFirstLocation(personPhysical), "publish_E2");
            byte district = Need(current.ReadPersonDistrictSlot(personPhysical), "publish_E0");
            var force = current.ReadPersonForce(personPhysical) ?? throw Missing("publish_Force");
            int forceId = Need(current.ReadForcePhysicalSlot(force), "publish_Force_ordinal");
            var status = OfficerServiceStatusSourceAdapter.Decode(Need(current.ReadPersonStatus(personPhysical), "publish_E6"));
            var location = current.ReadOfficerLocation(id) ?? throw Missing("publish_location");
            var values = new Dictionary<string, int>(native.Parameters.Int32Values)
            {
                [OwnedArmyLifecycleDetachedInputs.Key("Person", personPhysical, "E2")] = first,
                [OwnedArmyLifecycleDetachedInputs.Key("Person", personPhysical, "E0")] = district
            };
            var saved = state with { Officers = new(state.Officers) { [id] = person with
            {
                LocationId = first == 0 ? null : first, DistrictId = district == 0 ? null : district,
                ForceId = forceId == 0 ? null : forceId, ServiceStatus = status,
                Role = OfficerServiceRules.PublishedRole(status, person.Role), LocationState = location,
                Behavior = current.ReadPersonBehavior(personPhysical)
            } }, NativeArmySimulation = native with { Parameters = native.Parameters with { Int32Values = values } } };
            return OfficerLocationSourceAdapter.PublishPosition(saved, id, current);
        }
    }
}
