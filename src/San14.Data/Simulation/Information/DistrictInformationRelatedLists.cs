using System.Collections.Immutable;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>8F5B20's four distinct subordinate lists. Membership and
    /// ordering come from the District receiver, not global Force filtering.</summary>
    public InformationList QueryDistrictSubordinateInformation(GameState state, int playerForceId,
        int districtId, InformationListKind kind)
    {
        var ids = QueryDistrictInformationRelatedIds(state, districtId, kind);
        if (!ids.Available || ids.Value is not { } selected)
            return new("", [], [], ids.UnresolvedInput ?? "district-information.related.membership");
        var list = kind == InformationListKind.Officers
            ? QueryProvidedOfficerInformation(state, playerForceId, selected)
            : QueryInformationPage(state, playerForceId, kind, false);
        //Format with the established information producer, but restore the
        //actual provided source order, including a repeated registered entry.
        var rows = list.Rows.GroupBy(row => row.Id).ToDictionary(group => group.Key, group => group.First());
        var ordered = ImmutableArray.CreateBuilder<InformationRow>();
        var missing = new List<string>();
        if (list.UnresolvedOwner is not null) missing.Add(list.UnresolvedOwner);
        foreach (int id in selected)
        {
            if (rows.TryGetValue(id, out var row)) ordered.Add(row with { });
            else missing.Add("district-information.related.formatted_" + kind + "_" + id);
        }
        return list with { Rows = ordered.ToImmutable(),
            UnresolvedOwner = missing.Count == 0 ? null : string.Join("; ", missing.Distinct()) };
    }

    public AreaAdministrationQuery<IReadOnlyList<int>> QueryDistrictInformationRelatedIds(
        GameState state, int districtId, InformationListKind kind)
    {
        try
        {
            if (kind is not (InformationListKind.Cities or InformationListKind.Gates or
                InformationListKind.Officers or InformationListKind.Armies)) throw DistrictRelatedMissing("list_kind");
            var current = CaptureDeploymentPersonSelectionCurrent(state);
            current.RestoreOwnedArmyReturnSlots(state, state.ReadArmyLifecycle() ?? throw DistrictRelatedMissing("Army_lifecycle"));
            ForceUnregisterInputs.BindCurrent(state, current);
            OwnedArmyReturnServiceInputs.BindCurrent(state, current);
            OwnedArmyLifecycleDetachedInputs.BindCurrent(state, current);
            PersonArmyCaptureInputs.BindCurrent(state, current);
            var district = current.ReadDistrictTableReference(districtId)
                ?? throw DistrictRelatedMissing("District_pointer");
            if (!DistrictRelatedNeed(current.ReadDistrictNativeValidity(district), "District_validity"))
                throw DistrictRelatedMissing("District_invalid");
            int actual = DistrictRelatedNeed(district.PhysicalSlot, "District_identity");
            return AreaAdministrationQuery<IReadOnlyList<int>>.Known(
                ReadDistrictInformationRelatedIdsCurrent(state, current, actual, kind));
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<IReadOnlyList<int>>.Unknown(error.Message); }
    }

    private static IReadOnlyList<int> ReadDistrictInformationRelatedIdsCurrent(GameState state,
        SourceWorldContext current, int district, InformationListKind kind)
    {
        //8F5B20 allocates a fresh output list; each successful native append
        //alone contributes a row. The descriptor and nodes share this current.
        var temporary = current.CreateTemporaryHexList() ?? throw DistrictRelatedMissing("temporary_descriptor");
        var result = new List<int>();
        try
        {
            if (district is < 1 or > 51) return result.AsReadOnly();
            switch (kind)
            {
                case InformationListKind.Cities:
                    //224300/334590 traverse the fixed pointer table0..51.
                    for (int slot = 0; slot < 52; slot++)
                    {
                        var city = current.ReadCityTableReference(slot) ?? throw DistrictRelatedMissing("City_table");
                        if (DistrictRelatedNeed(current.ReadBaseDistrictSlot(city), "City_District") != district) continue;
                        if (!DistrictRelatedNeed(current.ReadBaseNativeValidity(city), "City_validity")) continue;
                        Append(RegistryKind.City, DistrictRelatedNeed(city.PhysicalSlot, "City_identity"),
                            DistrictRelatedNeed(city.PhysicalSlot, "City_identity"));
                    }
                    break;
                case InformationListKind.Gates:
                    //229600/334590 use manager80 order; the consumer then
                    //checks validity and the actual Base kind6, in that order.
                    foreach (var row in current.ReadRegisteredBases() ?? throw DistrictRelatedMissing("Base_registry80"))
                    {
                        var gate = row ?? throw DistrictRelatedMissing("Base_payload");
                        if (DistrictRelatedNeed(current.ReadBaseDistrictSlot(gate), "Base_District") != district) continue;
                        if (!DistrictRelatedNeed(current.ReadBaseNativeValidity(gate), "Base_validity")) continue;
                        if (gate.Kind != BaseKind.Gate) continue;
                        Append(RegistryKind.Gate, DistrictRelatedNeed(gate.PhysicalSlot, "Gate_identity"),
                            DistrictRelatedNeed(current.ReadBaseObjectLink(gate), "Gate4E"));
                    }
                    break;
                case InformationListKind.Officers:
                    //234850(param0): registration order, E0 membership,
                    //status1..4; then (!232CB0 ||232A50), then native validity.
                    foreach (int? row in current.ReadRegisteredPersonPhysicalSlots()
                        ?? throw DistrictRelatedMissing("Person_registry100"))
                    {
                        int person = DistrictRelatedNeed(row, "Person_pointer");
                        if (DistrictRelatedNeed(current.ReadPersonDistrictSlot(person), "PersonE0") != district) continue;
                        if (DistrictRelatedNeed(current.ReadPersonStatus(person), "PersonE6") is < 1 or > 4) continue;
                        var force = current.ReadPersonForce(person) ?? throw DistrictRelatedMissing("Person_Force");
                        bool special = DistrictRelatedNeed(current.ReadForcePhysicalSlot(force), "Person_Force_ordinal") is >= 46 and <= 50;
                        if (special && !DuelAftermathSpecialForce(current, person)) continue;
                        if (!DistrictRelatedNeed(current.ReadPersonNativeValidity(person), "Person_validity")) continue;
                        Append(RegistryKind.Person, person, DistrictRelatedNeed(current.ReadPersonIdentity(person), "Person10"));
                    }
                    break;
                case InformationListKind.Armies:
                    //222E60/334520: manager60, valid commander E0, and
                    //!233560. The outer consumer adds the special-force gate.
                    foreach (var row in current.ReadRegistryReferences(RegistryKind.Army)
                        ?? throw DistrictRelatedMissing("Army_registry60"))
                    {
                        int physical = DistrictRelatedNeed(row?.PhysicalSlot, "Army_pointer");
                        var army = new ArmyUnitReference(physical);
                        int raw = DistrictRelatedNeed(current.ReadArmyUnitCommanderId(army), "Army12_commander");
                        var person = (raw <= 6000 ? current.ReadPersonLookupReference(raw)
                            : current.ReadTransferPersonTableReference(0)) ?? throw DistrictRelatedMissing("Army_commander_lookup");
                        int at = DistrictRelatedNeed(person.PhysicalSlot, "Army_commander_pointer");
                        int own = DistrictRelatedNeed(current.ReadPersonNativeValidity(at), "Army_commander_validity")
                            ? DistrictRelatedNeed(current.ReadPersonDistrictSlot(at), "Army_commanderE0") : 0;
                        if (own != district) continue;
                        if (DistrictRelatedNeed(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current, physical),
                            "Army233560")) continue;
                        int force = DistrictRelatedNeed(current.ReadForcePhysicalSlot(current.ReadArmyUnitForceReference(army)), "Army_Force");
                        if (force is >= 46 and <= 50 && !current.IsArmyUnitBoundToSpecialForce(army)) continue;
                        if (!DistrictRelatedNeed(current.ReadArmyUnitNativeValidity(army), "Army_validity")) continue;
                        if (!AppendReceiver(RegistryKind.Army, physical)) continue;
                        var receiver = (state.ReadArmyLifecycle() ?? throw DistrictRelatedMissing("Army_lifecycle")).ReceiverSlots()[physical];
                        var unit = receiver.ActiveUnitId is int id ? state.Units.GetValueOrDefault(id) : receiver.PendingImage;
                        result.Add(unit?.Id ?? throw DistrictRelatedMissing("Army_current_Unit_identity"));
                    }
                    break;
                default: throw DistrictRelatedMissing("list_kind");
            }
            return result.AsReadOnly();
        }
        finally
        {
            current.ClearTemporaryHexList(temporary);
            current.ReleaseTemporaryHexList(temporary);
        }

        bool AppendReceiver(RegistryKind receiverKind, int physical)
        {
            var node = current.AllocateRegistryNode(new RegistryListHandle(temporary.Identity))
                ?? throw DistrictRelatedMissing("temporary_node");
            if (!node.Identity.HasValue) return false;
            current.WriteRegistryNodePayload(node, new(receiverKind, physical));
            return true;
        }
        void Append(RegistryKind receiverKind, int physical, int id)
        { if (AppendReceiver(receiverKind, physical)) result.Add(id); }
    }

    private static T DistrictRelatedNeed<T>(T? value, string field) where T : struct
        => value ?? throw DistrictRelatedMissing(field);
    private static InvalidDataException DistrictRelatedMissing(string field)
        => new("district-information.related." + field);
}
