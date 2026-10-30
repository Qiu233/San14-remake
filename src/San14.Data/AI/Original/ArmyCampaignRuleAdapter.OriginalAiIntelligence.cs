using San14.Core;
using San14.Data.AI.Original;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : IIntelligenceSlotCommandRules
{
    CommandResult IIntelligenceSlotCommandRules.RemoveDistrictIntelligenceAssignment(GameState state, ForceState force,
        RemoveDistrictIntelligenceAssignmentOrder order, CommandContext context)
    {
        try
        {
            var current = CaptureOriginalAiDomesticCurrent(state);
            var prepared = PrepareDistrictIntelligenceRemovalCurrent(state, current, order.DistrictId, order.Slot);
            if (!prepared.Success) return CommandResult.Reject("The district intelligence slot cannot be changed.", "intelligence.slot_gate");
            if (prepared.Order is null) return CommandResult.Accept(state);
            var own = IntelligenceSlotForce(current, order.DistrictId);
            int ownId = SlotNeed(own.PhysicalSlot, "own_Force_identity");
            current.SetAssignmentForceAssociation(ownId, order.Slot, 0, 0);
            var changed = PersonAssignmentPrecallInputs.PublishAreaAppointments(state, current, [], [], [(ownId, order.Slot)]);
            changed = SpendStrategicCommandBooksCurrent(changed, current, order.DistrictId, IntelligenceParameter(state, "home-book"));
            return CommandResult.Accept(changed);
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, error.Message); }
    }

    CommandResult IIntelligenceSlotCommandRules.SetDistrictIntelligenceAssignment(GameState state, ForceState force,
        SetDistrictIntelligenceAssignmentOrder order, CommandContext context)
    {
        try
        {
            var current = CaptureOriginalAiDomesticCurrent(state);
            var prepared = PrepareDistrictIntelligenceSetCurrent(state, current, order.DistrictId, order.Slot,
                order.TargetForceId, order.OfficerId);
            if (!prepared.Success) return CommandResult.Reject("The district intelligence assignment is ineligible.", "intelligence.slot_gate");
            if (prepared.Order is null) return CommandResult.Accept(state);
            var own = IntelligenceSlotForce(current, order.DistrictId);
            int ownId = SlotNeed(own.PhysicalSlot, "own_Force_identity");
            int person = IntelligenceSlotPerson(current, order.OfficerId);
            ushort identity = SlotNeed(current.ReadPersonIdentity(person), "Person10");
            var touched = new HashSet<(int Force, int Slot)>();
            //229930 mode0 removes only the first same-person association.
            for (int slot = 0; slot < 3; slot++)
                if (SlotNeed(current.ReadForceAssociationPersonId(own, slot), "existing_Person") == identity)
                { current.SetAssignmentForceAssociation(ownId, slot, 0, 0); touched.Add((ownId, slot)); break; }

            var cities = new HashSet<int>();
            var areas = new HashSet<int>();
            ClearDuty(trainer: false);
            ClearDuty(trainer: true);
            var vectors = new PersonAssignmentPrecallInputs.AreaAppointmentDraft(state);
            //29C340(0,0): clear active Area appointments, retaining Area72/6D.
            while (vectors.Count(person) > 0)
            {
                int raw = vectors.Entry(person, 0);
                if (raw < 0) throw SlotMissing("clear_Area_index");
                var area = current.ReadAreaTableReference(raw <= 500 ? raw : 0) ?? throw SlotMissing("clear_Area_pointer");
                if (!SlotNeed(current.ReadAreaNativeValidity(area), "clear_Area_validity"))
                    throw SlotMissing("clear_Area_vector_no_progress");
                int areaId = SlotNeed(area.PhysicalSlot, "clear_Area_identity");
                ushort old = SlotNeed(current.ReadAssignmentArea36(areaId), "clear_Area36");
                int before = vectors.Count(person);
                if (old != 0)
                {
                    current.SetAssignmentArea36(areaId, 0);
                    PersonAreaAppointmentSource.Change(state, current, vectors, areaId, 0, old);
                    areas.Add(areaId);
                }
                if (vectors.Count(person) >= before) throw SlotMissing("clear_Area_vector_no_progress");
            }
            current.SetAssignmentForceAssociation(ownId, order.Slot, identity, (byte)order.TargetForceId);
            touched.Add((ownId, order.Slot));
            var changed = PersonAssignmentPrecallInputs.PublishAreaAppointments(state, current, cities, areas, touched);
            changed = vectors.Publish(changed);
            changed = SpendStrategicCommandBooksCurrent(changed, current, order.DistrictId, IntelligenceParameter(state, "home-book"));
            return CommandResult.Accept(changed);

            void ClearDuty(bool trainer)
            {
                //2C94B0/2C9F20 compare the related valid City. The actual node
                //then writes the original Base pointer, without22A940 conversion.
                var home = BaseLookup.Find(SlotNeed(current.ReadPersonFirstLocation(person), "PersonE2"), current);
                if (!SlotNeed(current.ReadBaseNativeValidity(home), "duty_Base_validity")) return;
                var city = CurrentGateMapQueries.FindCity(home, current);
                if (!SlotNeed(current.ReadBaseNativeValidity(city), "duty_City_validity")) return;
                int cityId = SlotNeed(city.PhysicalSlot, "duty_City_identity");
                ushort assigned = SlotNeed(trainer ? current.ReadAssignmentCityCC(cityId) : current.ReadAssignmentCityCA(cityId), "duty_Person");
                if (assigned != identity) return;
                if (home.Kind != BaseKind.City)
                    throw SlotMissing(trainer ? "incoming_Gate_CC_writer" : "incoming_Gate_CA_writer");
                int physical = SlotNeed(home.PhysicalSlot, "duty_write_City_identity");
                if (trainer) current.SetAssignmentCityCC(physical, 0); else current.SetAssignmentCityCA(physical, 0);
                cities.Add(physical);
            }
        }
        catch (InvalidDataException error) { return CommandResult.RuleUnavailable(error.Message, error.Message); }
    }

    internal OriginalAiCommandPreparation PrepareDistrictIntelligenceRemovalCurrent(GameState state,
        SourceWorldContext current, int district, int slot)
    {
        if (IntelligenceSlotBooks(state, district) < IntelligenceSlotThreshold(state)) return new(false, null);
        var own = IntelligenceSlotForce(current, district);
        if (!SlotNeed(current.ReadForceNativeValidity(own), "own_Force_validity")) return new(false, null);
        if ((uint)slot >= 3) return new(true, null);
        if (SlotNeed(current.ReadForceAssociationTargetSlot(own, slot), "existing_target") == 0 &&
            SlotNeed(current.ReadForceAssociationPersonId(own, slot), "existing_Person") == 0) return new(true, null);
        return new(true, new RemoveDistrictIntelligenceAssignmentOrder(SlotNeed(own.PhysicalSlot, "own_Force_identity"), district, slot));
    }

    internal OriginalAiCommandPreparation PrepareDistrictIntelligenceSetCurrent(GameState state,
        SourceWorldContext current, int district, int slot, int targetForce, int officer)
    {
        var own = IntelligenceSlotForce(current, district);
        var target = current.ReadForceTableReference(targetForce is >= 0 and <= 51 ? targetForce : 0)
            ?? throw SlotMissing("target_Force_pointer");
        int targetIdentity = SlotNeed(target.PhysicalSlot, "target_Force_identity");
        int person = IntelligenceSlotPerson(current, officer);
        ushort personIdentity = SlotNeed(current.ReadPersonIdentity(person), "Person10");
        //The unusual source no-op reads the TARGET Force's own slots. This is
        //confirmed at3E091/3E0B4, not replaced with an ordinary own-slot test.
        int existingTarget = (uint)slot < 3 ? SlotNeed(current.ReadForceAssociationTargetSlot(target, slot), "noop_target") : 0;
        if (existingTarget == targetIdentity)
        {
            int existingPerson = (uint)slot < 3 ? SlotNeed(current.ReadForceAssociationPersonId(target, slot), "noop_Person") : 0;
            if (existingPerson == personIdentity) return new(true, null);
        }
        if ((uint)slot >= 3 || targetIdentity is < 1 or > 51 || personIdentity is < 1 or > 6000) return new(false, null);
        if (IntelligenceSlotBooks(state, district) < IntelligenceSlotThreshold(state)) return new(false, null);
        if (!SlotNeed(current.ReadForceNativeValidity(own), "own_Force_validity") ||
            !SlotNeed(current.ReadForceNativeValidity(target), "target_Force_validity") ||
            !SlotNeed(current.ReadPersonNativeValidity(person), "Person_validity")) return new(false, null);
        return new(true, new SetDistrictIntelligenceAssignmentOrder(SlotNeed(own.PhysicalSlot, "own_Force_identity"),
            district, slot, targetIdentity, personIdentity));
    }

    private static ForceReference IntelligenceSlotForce(SourceWorldContext current, int district)
    {
        int raw = SlotNeed(current.ReadDistrictForceId(district), "District10_Force");
        return current.ReadForceTableReference(raw <= 51 ? raw : 0) ?? throw SlotMissing("own_Force_pointer");
    }
    private static int IntelligenceSlotPerson(SourceWorldContext current, int officer) =>
        (officer is >= 0 and <= 6000 ? current.ReadPersonLookupReference(officer) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot
            ?? throw SlotMissing("Person_pointer");
    private static int IntelligenceSlotBooks(GameState state, int district)
    {
        int? books = state.Districts.GetValueOrDefault(district)?.OrdersRemaining;
        if (books is null && district == 0 &&
            state.NativeForceUnregisterCurrent?.DistrictWindows10.TryGetValue(0, out var zero) == true && zero.Length > 4)
            books = zero[4];
        return books is int value && value is >= 0 and <= 255 ? value : throw SlotMissing("District14_books");
    }
    private static int IntelligenceSlotThreshold(GameState state) => CityDomesticParameters.Read(state, "trainerCommandCost")
        ?? throw SlotMissing("trainer_command_threshold");
    private static T SlotNeed<T>(T? value, string field) where T : struct => value ?? throw SlotMissing(field);
    private static InvalidDataException SlotMissing(string field) => new("intelligence.slots." + field);
}
