using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    //087330/03B720 selector19. Both branches use actual registered receiver
    //order, two distinct diplomacy modes and two independent Army directions.
    private BaseInformationValue ReadOriginalAiDistrictDiplomaticDestinationsCurrent(
        GameState state, SourceWorldContext current, int district)
    {
        int owner = BaseInfoNeed(current.ReadDistrictForceId(district), "diplomatic_District10");
        var own = current.ReadForceTableReference(owner < 52 ? owner : 0)
            ?? throw BaseInfoMissing("diplomatic_Force_pointer");
        if (!BaseInfoNeed(current.ReadForceNativeValidity(own), "diplomatic_Force_validity"))
            return new BaseInformationFailure();
        owner = BaseInfoNeed(own.PhysicalSlot, "diplomatic_Force_identity");

        var frontier = Temporary();
        HexListHandle? allowedDestination = null, allowedHome = null;
        try
        {
            FillFrontier();
            if (!Rows(frontier).Any()) return Result([]);
            allowedDestination = Temporary();
            allowedHome = Temporary();
            foreach (var entry in current.ReadRegistryReferences(RegistryKind.Force)
                ?? throw BaseInfoMissing("diplomatic_Force_registryC0"))
            {
                var forceEntry = entry ?? throw BaseInfoMissing("diplomatic_Force_payload");
                if (forceEntry.Kind != RegistryKind.Force) throw BaseInfoMissing("diplomatic_Force_payload_kind");
                var force = new ForceReference(forceEntry.PhysicalSlot);
                if (!BaseInfoNeed(current.ReadForceNativeValidity(force), "diplomatic_other_Force_validity")) continue;
                int other = BaseInfoNeed(force.PhysicalSlot, "diplomatic_other_Force_identity");
                bool destination = force == own || ReadOriginalAiForeignAdmissionCurrent(state, current, owner, other, 1);
                bool home = force == own || ReadOriginalAiForeignAdmissionCurrent(state, current, owner, other, 2);
                //CForceDistrictIterator filters the actual managerD0 list.
                foreach (var row in current.ReadArmyReturnRegisteredDistricts()
                    ?? throw BaseInfoMissing("diplomatic_District_registryD0"))
                {
                    int at = BaseInfoNeed(row?.PhysicalSlot, "diplomatic_District_payload");
                    if (BaseInfoNeed(current.ReadDistrictForceId(at), "diplomatic_other_District10") != other ||
                        !BaseInfoNeed(current.ReadDistrictNativeValidity(at), "diplomatic_District_validity") || at == district) continue;
                    if (destination) Append(allowedDestination, new(RegistryKind.District, at));
                    if (home) Append(allowedHome, new(RegistryKind.District, at));
                }
            }
            if (!Rows(allowedDestination).Any() && !Rows(allowedHome).Any()) return Result([]);

            var result = new List<OriginalAiEntity>();
            var grouping = new ArmyMapGroupingCurrentQueries(state, current, _scenario, _governorInputs,
                Seed.Language, SimulationRuleIdentity.ForLocale(Seed.Language));
            foreach (var row in current.ReadRegistryReferences(RegistryKind.Army)
                ?? throw BaseInfoMissing("diplomatic_Army_registry60"))
            {
                var entry = row ?? throw BaseInfoMissing("diplomatic_Army_payload");
                if (entry.Kind != RegistryKind.Army) throw BaseInfoMissing("diplomatic_Army_payload_kind");
                var army = new ArmyUnitReference(entry.PhysicalSlot);
                if (!BaseInfoNeed(current.ReadArmyUnitNativeValidity(army), "diplomatic_Army_validity")) continue;
                int physical = BaseInfoNeed(army.PhysicalSlot, "diplomatic_Army_identity");
                if (BaseInfoNeed(grouping.IsAtHome2C96E0(physical), "diplomatic_Army_at_home")) continue;
                var image = state.Units.GetValueOrDefault(TroopsInfoUnitId(state, army))
                    ?? throw BaseInfoMissing("diplomatic_Army_current");
                if (BaseInfoNeed(ArmyConfigurationSourceAdapter.ReadAction(state, image), "diplomatic_Army_action37") is not (4 or 5)) continue;

                //22A730 gates only the commander, then resolves his raw home.
                int commander = BaseInfoNeed(current.ReadArmyUnitCommanderId(army), "diplomatic_Commander12");
                int person = BaseInfoNeed(current.ReadPersonLookupReference(commander <= 6000 ? commander : 0)?.PhysicalSlot,
                    "diplomatic_Commander_pointer");
                int location = BaseInfoNeed(current.ReadPersonNativeValidity(person), "diplomatic_Commander_validity")
                    ? BaseInfoNeed(current.ReadPersonFirstLocation(person), "diplomatic_Commander_homeE2") : 0;
                var homeBase = BaseLookup.Find(location, current);
                if (!BaseInfoNeed(current.ReadBaseNativeValidity(homeBase), "diplomatic_home_validity")) continue;
                var destinationBase = Destination(physical);
                if (destinationBase is null || !BaseInfoNeed(current.ReadBaseNativeValidity(destinationBase), "diplomatic_destination_validity")) continue;

                int destinationDistrict = BaseInfoNeed(current.ReadBaseDistrictReference(destinationBase)?.PhysicalSlot,
                    "diplomatic_destination_District80");
                if (Contains(allowedDestination, new(RegistryKind.District, destinationDistrict)) && Contains(frontier, Payload(homeBase)))
                    Add(homeBase);
                int homeDistrict = BaseInfoNeed(current.ReadBaseDistrictReference(homeBase)?.PhysicalSlot, "diplomatic_home_District80");
                if (Contains(allowedHome, new(RegistryKind.District, homeDistrict)) && Contains(frontier, Payload(destinationBase)))
                    Add(destinationBase);
            }
            return Result(result);

            void Add(BaseReference at)
            {
                var value = AiForceInformationBaseEntity(current, at);
                if (!result.Contains(value)) result.Add(value);
            }
        }
        finally
        {
            Release(allowedHome); Release(allowedDestination); Release(frontier);
        }

        void FillFrontier()
        {
            //2A8540 uses City-only22F430, not the direct Base22F720 list.
            var neighborhoods = new PersonFreedomHomeSource(_scenario);
            foreach (var at in BaseInfoDistrictBases(current, district))
            {
                var neighbors = Temporary();
                try
                {
                    neighborhoods.FillCities22F430(current, at, neighbors);
                    foreach (var row in Rows(neighbors))
                    {
                        if (row.Kind != RegistryKind.City) throw BaseInfoMissing("diplomatic_frontier_not_City");
                        var neighbor = new BaseReference(BaseKind.City, row.PhysicalSlot);
                        int other = BaseInfoNeed(current.ReadBaseForceSlot(neighbor), "diplomatic_frontier_Force");
                        int location = BaseInfoNeed(current.ReadBaseObjectLink(neighbor), "diplomatic_frontier_location4E");
                        if (other is < 1 or > 51 || location is >= 52 and <= 61) continue;
                        var force = current.ReadForceTableReference(other) ?? throw BaseInfoMissing("diplomatic_frontier_Force_pointer");
                        if (BaseInfoNeed(ArmyMarchDevelopmentSource.ReadFriendlyRelation(state, own, force), "diplomatic_frontier_protected_pair") ||
                            other is >= 46 and <= 50) continue;
                        if (!Contains(frontier, row)) Append(frontier, row);
                    }
                }
                finally { Release(neighbors); }
            }
        }
        BaseReference? Destination(int army)
        {
            //229EE0 dispatches32CAF0 from raw38/3A. No override, relay or
            //fallback to the current home belongs to this getter.
            var target = current.ReadArmyLifecycleTargetFields(army) ?? throw BaseInfoMissing("diplomatic_target_owner");
            int kind = BaseInfoNeed(target.Kind38, "diplomatic_target_kind38");
            int identity = BaseInfoNeed(target.Identity3A, "diplomatic_target_identity3A");
            return kind switch
            {
                0 => null,
                2 => BaseLookup.Find(identity, current),
                5 => current.ReadCityTableReference(identity < 52 ? identity : 0) ?? throw BaseInfoMissing("diplomatic_target_City_pointer"),
                6 => current.ReadGateTableReference(identity < 11 ? identity : 0) ?? throw BaseInfoMissing("diplomatic_target_Gate_pointer"),
                _ => throw BaseInfoMissing("diplomatic_target_non_Base_kind_" + kind)
            };
        }
        BaseInformationValue Result(IReadOnlyList<OriginalAiEntity> values) => new BaseInformationEntities(WorldEntityKind.City, values);
        HexListHandle Temporary() => current.CreateTemporaryHexList() ?? throw BaseInfoMissing("diplomatic_temporary_list");
        void Release(HexListHandle? list)
        { if (list is not null) { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); } }
        IEnumerable<RegistryObjectReference> Rows(HexListHandle list)
        {
            var node = current.ReadRegistryHead(new(list.Identity)) ?? throw BaseInfoMissing("diplomatic_temporary_head");
            while (node.Identity.HasValue)
            {
                yield return current.ReadRegistryNodePayload(node) ?? throw BaseInfoMissing("diplomatic_temporary_payload");
                node = current.ReadRegistryNodeNext(node) ?? throw BaseInfoMissing("diplomatic_temporary_next");
            }
        }
        bool Contains(HexListHandle list, RegistryObjectReference payload) => Rows(list).Contains(payload);
        void Append(HexListHandle list, RegistryObjectReference payload)
        {
            var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw BaseInfoMissing("diplomatic_temporary_node");
            if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, payload);
        }
        static RegistryObjectReference Payload(BaseReference at) => new(at.Kind == BaseKind.City ? RegistryKind.City : RegistryKind.Gate, at.PhysicalSlot);
    }
}
