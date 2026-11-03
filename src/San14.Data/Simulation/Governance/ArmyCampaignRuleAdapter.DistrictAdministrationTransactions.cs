using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IDistrictCommandRules
{
    CommandResult IDistrictCommandRules.ApplyCreate(GameState state, ForceState force,
        CreateDistrictOrder order, CommandContext context) => ApplyDistrictAdministrationCommand(state, force.Id,
            null, order.CityIds, order.LeaderId, order.Settings, order.MilitaryIntelligenceTransfer, false);

    CommandResult IDistrictCommandRules.ApplyReorganize(GameState state, ForceState force,
        ReorganizeDistrictOrder order, CommandContext context) => ApplyDistrictAdministrationCommand(state, force.Id,
            order.DistrictId, order.CityIds, order.LeaderId, order.Settings, order.MilitaryIntelligenceTransfer, false);

    CommandResult IDistrictCommandRules.ApplyDissolve(GameState state, ForceState force,
        DissolveDistrictOrder order, CommandContext context) => ApplyDistrictAdministrationCommand(state, force.Id,
            order.DistrictId, [], 0, null, 0, true);

    private CommandResult ApplyDistrictAdministrationCommand(GameState state, int forceId, int? districtId,
        IReadOnlyList<int> cities, int leaderId, DistrictSettings? settings, int intelligenceTransfer, bool dissolve)
    {
        try
        {
            var world = CaptureOwnedArmyReturnWorld(state);
            var current = world.Context;
            var force = new ForceReference(forceId);
            if (!DistrictNeed(current.ReadForceNativeValidity(force), "command_Force_validity"))
                return CommandResult.Reject("The force is no longer active.", "district.control");
            DistrictReference? edited = districtId is int at ? new(at) : null;
            if (edited is not null && (!DistrictNeed(current.ReadDistrictNativeValidity(edited), "command_District_validity") ||
                DistrictNeed(current.ReadDistrictForceSlot(edited), "command_District_Force") != forceId))
                return CommandResult.Reject("The district is no longer controlled.", "district.control");
            bool primary = districtId is int selected && DistrictNeed(current.ReadDistrictNumber(selected), "command_District_number") == 1;
            DistrictAdministrationRules? rules = null;
            if (dissolve)
            {
                if (primary) return CommandResult.Reject("The primary district cannot be dissolved.", "district.dissolve.primary");
            }
            else
            {
                if (settings is null) throw DistrictMissing("command_settings");
                rules = CaptureDistrictAdministrationRules(state, current, forceId);
                var selection = QueryDistrictAdministrationSelectionCurrent(state, current, forceId, districtId, cities, settings);
                if (selection.CanSubmit is null) throw new InvalidDataException(selection.UnresolvedOwner ?? "district.command_qualification");
                if (selection.CanSubmit != true || !selection.Leaders.Any(row => row.Id == leaderId) ||
                    districtId is null && rules.FirstAvailableNumber == 0)
                    return CommandResult.Reject("The district selection is no longer available.", "district.qualification");
                //73F460/73E850 only reach the stock transfer inside UseIntel.
                //The draft can retain an unused amount while that switch is off.
                if (!primary && settings.Military.Enabled && (intelligenceTransfer < 0 ||
                    intelligenceTransfer > DistrictNeed(selection.MaximumMilitaryIntelligenceTransfer, "command_intelligence_limit")))
                    return CommandResult.Reject("The intelligence transfer exceeds the current budget.", "district.intelligence_budget");
            }

            var working = state;
            var effects = new List<CampaignEffectRequest>();
            var operation = new ArmyReturnDistrictOperationQueries(
                new PersonReturnGovernorQueries(state, _governorInputs, current), () => working, value => working = value,
                Assignment, current, [], this, state.NativeMapSeason?.ThirdDate);
            var oldAdvisor = current.AssignmentAdvisorResetCallbacks;
            var oldAreaRemoval = current.AssignmentAreaOfficerRemoval;
            current.AssignmentAdvisorResetCallbacks = owner =>
            {
                var reset = PersonAdvisorResetSource.ApplyWithinPersonCapture(working, current, world.Pool, owner);
                working = reset.State ?? throw DistrictMissing(reset.UnresolvedOwner ?? "advisor_reset");
                return true;
            };
            current.AssignmentAreaOfficerRemoval = (area, old) =>
            {
                var removal = PersonAssignmentPrecallSource.Apply2E01E0AreaRemoval(current, area, old);
                if (removal.NativeReturn is null) throw DistrictMissing(removal.UnresolvedOwner ?? "area_removal");
                if (removal.NativeReturn != 0 && removal.PersonPhysical is int person)
                    working = PersonAssignmentPrecallInputs.PublishDeathTouched(working, current, person, [], publishVector: true);
                return removal.NativeReturn != 0;
            };
            try
            {
                if (dissolve)
                {
                    var destination = ForcePrimaryDistrictSource.Resolve(current, force);
                    if (destination == edited) throw DistrictMissing("dissolve_primary_self");
                    PersonReturnDistrictReorganizationSource.MergeCurrent(current, edited!, destination, operation);
                }
                else if (edited is null)
                {
                    int leader = DistrictNeed(current.ResolveOfficerSource(leaderId), "create_leader_pointer");
                    var created = CreateDistrictAdministrationCurrent(current, world.Pool, force, leader,
                        DistrictAdministrationBases(current, cities), operation, rules!.FirstAvailableNumber);
                    int physical = DistrictNeed(created.PhysicalSlot, "created_District");
                    WriteDistrictAdministrationSettingsCurrent(current, physical, settings!, operation, newDistrict: true);
                    TransferIntelligence(physical);
                    working = DistrictAdministrationAppointmentReports(working, current, state, forceId, leaderId,
                        DistrictNeed(current.ReadDistrictNumber(physical), "created_number"), effects);
                }
                else
                {
                    int district = DistrictNeed(edited.PhysicalSlot, "edited_District");
                    int originalLeader = Person(DistrictNeed(current.ReadDistrictLeader(district), "original_leader"));
                    int leader = DistrictNeed(current.ResolveOfficerSource(leaderId), "edited_leader_pointer");
                    if (!primary) working = DistrictAdministrationAppointmentReports(working, current, state, forceId, leaderId,
                        DistrictNeed(current.ReadDistrictNumber(district), "edited_number"), effects);
                    //The selected Cities move first, then the remaining Cities
                    //are redistributed using the source's still-current links.
                    foreach (int city in cities)
                        TransferDistrictAdministrationBaseCurrent(current, BaseLookup.Find(checked((ushort)city), current), edited, operation);
                    RedistributeDistrictAdministrationCitiesCurrent(current, force, edited, cities, operation);
                    int interimLeader = Person(DistrictNeed(current.ReadDistrictLeader(district), "interim_leader"));
                    if (!primary)
                    {
                        current.WriteDistrictLeader(district, checked((ushort)Math.Min(leaderId, 6000)));
                        WriteDistrictAdministrationSettingsCurrent(current, district, settings!, operation);
                        TransferIntelligence(district);
                    }
                    //73E850 repeats the actual ForcePerson iterator after the
                    //Base moves. Home ownership is authoritative for E0.
                    foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw DistrictMissing("edit_Person_registry"))
                    {
                        int person = DistrictNeed(entry, "edit_Person_pointer");
                        if (DistrictNeed(current.ReadPersonOldForceSlot(person), "edit_Person_Force") != forceId ||
                            DistrictNeed(current.ReadPersonStatus(person), "edit_Person_status") is < 1 or > 4) continue;
                        var home = BaseLookup.Find(DistrictNeed(current.ReadPersonFirstLocation(person), "edit_Person_E2"), current);
                        if (!DistrictNeed(current.ReadBaseNativeValidity(home), "edit_Home_validity")) continue;
                        int destination = DistrictNeed(current.ReadBaseDistrictSlot(home), "edit_Home_District");
                        var previousService = current.TransferServiceRefresher;
                        current.TransferServiceRefresher = operation.RefreshPersonnelMilestoneCurrent;
                        try
                        {
                            PersonDistrictTransfer.Apply(person, destination, ArmyOrdinaryReturnRuntime.CreateCurrentPersonDistrictTransfer(
                                current, [], result => Assignment(person, result)));
                        }
                        finally { current.TransferServiceRefresher = previousService; }
                    }
                    if (!primary && leader != originalLeader)
                    {
                        BaseReference? newHome = HomeAndStatus(leader, 2), oldHome = HomeAndStatus(originalLeader, 4), interimHome = HomeAndStatus(interimLeader, 4);
                        UpdateGovernor(interimHome); UpdateGovernor(newHome); UpdateGovernor(oldHome);
                    }
                }
                working = OwnedArmyReturnCurrentFields.PublishCurrent(working, current);
                working = OwnedArmyLifecycleDetachedInputs.PublishCurrent(working, current);
                working = PersonAssignmentPrecallInputs.PublishCurrent(working, current);
                working = BaseCaptureCurrentInputs.PublishCurrent(working, current);
                working = PersonReturnDistrictTransferSource.PublishAreaAssignments(working, current, operation.ChangedAreas, operation.ChangedGates);
                working = PersonReturnDistrictReorganizationSource.PublishCurrent(working, current, operation);
                var published = new Dictionary<int, DistrictState>(working.Districts);
                foreach (int district in operation.TouchedDistricts)
                {
                    if (!published.TryGetValue(district, out var before)) continue;
                    bool valid = DistrictNeed(current.ReadDistrictNativeValidity(district), "publish_District_validity");
                    published[district] = before with
                    {
                        OrdersRemaining = valid ? DistrictNeed(current.ReadDistrictRawByte(district, 0x14), "publish_books14") : 0,
                        Settings = valid ? ReadDistrictAdministrationSettingsCurrent(current, district) : null
                    };
                }
                working = working with { Districts = published,
                    PersonnelCommands = current.ReadOwnedPersonCommandManager() ?? throw DistrictMissing("publish_personnel_manager") };
                if (effects.Count != 0) working = working with { CampaignEffectRequests =
                    (working.CampaignEffectRequests ?? throw DistrictMissing("effects_owner")).Append(
                        CampaignEffectSource.PersonnelAdministrationCommands,
                        working.NativeMapSeason?.AdvancedCalendarSteps ?? throw DistrictMissing("effect_calendar"), effects) };
                return CommandResult.Accept(working.WithArmyLifecycle(working.ReadArmyLifecycle()! with { Indexes = world.Pool.ExportOwnedState() }));
            }
            finally { current.AssignmentAdvisorResetCallbacks = oldAdvisor; current.AssignmentAreaOfficerRemoval = oldAreaRemoval; }

            int Person(ushort id) => DistrictNeed((id <= 6000 ? current.ReadPersonLookupReference(id)
                : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, "leader_lookup");
            void Assignment(int person, PersonAssignmentPrecallResult result)
            {
                if (result.NativeReturn is null) throw DistrictMissing(result.UnresolvedOwner ?? "assignment");
                if (result.Reports.Count != 0) working = ApplyPersonAssociationRemovalReports(working, current, person, result,
                    () => (ReadArmyDestructionSampler(working, current) ?? throw DistrictMissing("report_World450")).Day450, effects);
            }
            BaseReference? HomeAndStatus(int person, byte status)
            {
                if (!DistrictNeed(current.ReadPersonNativeValidity(person), "changed_leader_validity")) return null;
                current.WritePersonStatus(person, status);
                return BaseLookup.Find(DistrictNeed(current.ReadPersonFirstLocation(person), "changed_leader_E2"), current);
            }
            void UpdateGovernor(BaseReference? home)
            {
                if (home is null || !DistrictNeed(current.ReadBaseNativeValidity(home), "changed_leader_Home_validity")) return;
                var prepared = ArmyReturnGovernorUpdater.PrepareCurrent(home, current, operation);
                ArmyReturnGovernorUpdater.ApplyCurrentWrites(current, prepared.Packet ?? throw DistrictMissing(prepared.UnresolvedOwner ?? "changed_governor"));
            }
            void TransferIntelligence(int district)
            {
                if (!settings!.Military.Enabled) return;
                int primaryId = DistrictNeed(ForcePrimaryDistrictSource.Resolve(current, force).PhysicalSlot, "transfer_primary");
                int primaryStock = ReadDistrictAdministrationIntelligenceCurrent(current, primaryId);
                int targetStock = ReadDistrictAdministrationIntelligenceCurrent(current, district);
                WriteDistrictAdministrationIntelligenceCurrent(current, district, Math.Clamp(unchecked(targetStock + intelligenceTransfer),
                    0, DistrictAdministrationIntelligenceCapacity(working, current, district)));
                WriteDistrictAdministrationIntelligenceCurrent(current, primaryId, Math.Clamp(unchecked(primaryStock - intelligenceTransfer),
                    0, DistrictAdministrationIntelligenceCapacity(working, current, primaryId)));
                operation.TouchedDistricts.Add(district); operation.TouchedDistricts.Add(primaryId);
            }
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, error.Message); }
    }

    private GameState DistrictAdministrationAppointmentReports(GameState state, SourceWorldContext current,
        GameState source, int forceId, int leaderId, int number, List<CampaignEffectRequest> effects)
    {
        int ruler = DistrictNeed(current.ReadPersonLookupReference(DistrictNeed(current.ReadForceRecord(forceId)?.RawRuler10,
            "report_ruler_id"))?.PhysicalSlot, "report_ruler");
        int leader = DistrictNeed(current.ResolveOfficerSource(leaderId), "report_leader");
        if (!DistrictNeed(current.ReadPersonNativeValidity(ruler), "report_ruler_validity") ||
            !DistrictNeed(current.ReadPersonNativeValidity(leader), "report_leader_validity")) return state;
        int rulerId = DistrictNeed(current.ReadPersonIdentity(ruler), "report_ruler_identity");
        var first = FormatPersonnelAdministrationCurrent(state, current, 0x5963,
            new(Person0: rulerId, Person1: leaderId, String0: ReadHistoryDataText(1, 0x38C + Math.Clamp(number, 1, 8))));
        effects.Add(PersonnelAdministrationOfficerLine(current, source, rulerId, rulerId, first));
        var second = FormatPersonnelAdministrationCurrent(first.State, current, 0x5969, new(Person0: leaderId, Person1: rulerId));
        effects.Add(PersonnelAdministrationOfficerLine(current, source, leaderId, rulerId, second));
        return second.State;
    }
}
