using San14.Core;

namespace San14.Data;

public sealed record OriginalCaptureBaseThreatReceipt(OriginalBaseReference Attacker,
    int CandidateHex, int TargetHex, int AttackerRepresentativeHex, int TargetRepresentativeHex,
    int Radius, int Troops3C, int AttackerForce13, int TargetForce, string Branch);
public sealed record OriginalCaptureBaseThreatResult(bool? Threatens,
    OriginalCaptureBaseThreatReceipt? Receipt, string? UnresolvedOwner);

/// <summary>Ordinary source2CA7B0 City/Gate attacking branch. Actual object
/// pointers select type, while the original Hex1A selects the Base receiver.
/// Nonbase object definitions remain a separate reached query.</summary>
public static class OriginalCaptureBaseThreatSource
{
    public static OriginalCaptureBaseThreatResult Evaluate(GameState state, OriginalEventInitializationContext current,
        int candidateHex, int targetHex)
    {
        try
        {
            OriginalCaptureBaseThreatInputs.BindCurrent(state, current);
            var candidate = Hex(current, candidateHex); var target = Hex(current, targetHex);
            _ = Need(current.ReadHexRaw1C(candidate), "candidate_Hex1C");
            int targetArmySlot = unchecked((short)Need(current.ReadHexRaw1C(target), "target_Hex1C"));
            ushort link = Need(current.ReadHexObjectLink(candidate), "candidate_Hex1A");
            if (link == 0) throw Missing("branch.nonbase_attacking_Army_fallthrough");
            var obj = current.ReadObjectTableReference(link < 3001 ? link : 0) ?? throw Missing("attacker_Object_pointer");
            int objectPhysical = Need(obj.PhysicalSlot, "attacker_Object_physical");
            int force = Need(current.ReadObjectForceSlot13(obj), "attacker_Object13");
            OriginalBaseReference attacker; int radius;
            if (objectPhysical is >= 1 and <= 51)
            { attacker = current.ReadCityTableReference(link < 52 ? link : 0) ?? throw Missing("attacker_City_pointer"); radius = 3; }
            else if (objectPhysical is >= 52 and <= 61)
            { attacker = current.ReadGateTableReference(link is >= 52 and <= 61 ? link - 51 : 0) ?? throw Missing("attacker_Gate_pointer"); radius = 2; }
            else throw Missing("branch.nonbase_attacking_Object_Army_fallthrough");
            var fields = current.ReadCaptureBaseThreatFields(attacker) ?? throw Missing("Base49_4B_62_64_owner");
            if (Need(fields.Byte49, "Base49") != 0) return new(false, null, null);
            if (Need(fields.Byte4B, "Base4B") != 0)
            {
                var descriptor = DescribeTarget(current, target);
                if (Need(fields.Byte62, "Base_forced_kind62") != descriptor.Kind ||
                    Need(fields.Word64, "Base_forced_ID64") != descriptor.Id) return new(false, null, null);
            }
            int start = Need(current.ReadObjectHexIndex(obj), "attacker_Object10");
            int troops = Need(current.ReadBaseResource(attacker, OriginalBaseResourceKind.Troops), "Base_current3C");
            int otherForce, end = Need(current.ReadHexPhysicalSlot(target), "target_Hex_physical");
            ushort targetLink = Need(current.ReadHexObjectLink(target), "target_Hex1A");
            if (targetLink != 0)
            {
                var targetObject = current.ReadObjectTableReference(targetLink < 3001 ? targetLink : 0) ?? throw Missing("target_Object_pointer");
                int physical = Need(targetObject.PhysicalSlot, "target_Object_physical");
                if (physical is >= 1 and <= 61)
                {
                    otherForce = Need(current.ReadObjectForceSlot13(targetObject), "target_Object13");
                    end = Need(current.ReadObjectHexIndex(targetObject), "target_Object10");
                    if (physical <= 51) radius++;
                    return Finish();
                }
                byte kind = current.ReadPhysicalObject(physical)?.RawField12 ?? throw Missing("target_Object12");
                var definition = current.ReadObjectDefinitionReference(kind < 31 ? kind : 0) ?? throw Missing("target_Object_definition_pointer");
                if (Need(current.ReadObjectDefinitionValidity(definition), "target_Object_definition_validity") &&
                    Need(current.ReadObjectDefinitionRaw2C(definition), "target_Object_definition2C") is 2 or 3)
                {
                    otherForce = Need(current.ReadObjectForceSlot13(targetObject), "target_Object13");
                    end = Need(current.ReadObjectHexIndex(targetObject), "target_Object10");
                    return Finish();
                }
            }
            if (targetArmySlot is < 1 or > 500) return new(false, null, null); //2C6D20 only checks Hex validity/slot.
            var army = current.ReadArmyUnitTableReference(targetArmySlot) ?? throw Missing("target_Army_pointer");
            otherForce = current.ReadArmyUnitRawForceSlot(army);
            return Finish();

            OriginalCaptureBaseThreatResult Finish()
            {
                var receipt = new OriginalCaptureBaseThreatReceipt(attacker, candidateHex, targetHex, start, end,
                    radius, troops, force, otherForce, "ordinary-base");
                int distance = Distance(start, end);
                bool result = distance <= radius && troops > 0 && Need(
                    OriginalArmyLogisticsPreparation.ReadNativeRelationGate(state, force, otherForce), "relation2CA650");
                return new(result, receipt, null);
            }
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }
    private static (int Kind, int Id) DescribeTarget(OriginalEventInitializationContext current, OriginalHexReference target)
    {
        ushort link = Need(current.ReadHexObjectLink(target), "forced_target_Hex1A");
        var obj = current.ReadObjectTableReference(link < 3001 ? link : 0) ?? throw Missing("forced_target_Object_pointer");
        int armySlot = unchecked((short)Need(current.ReadHexRaw1C(target), "forced_target_Hex1C"));
        bool objectValid = Need(current.ReadObjectNativeValidity(obj), "forced_target_Object_validity");
        int physical = Need(obj.PhysicalSlot, "forced_target_Object_physical");
        if (objectValid && physical is >= 1 and <= 61)
        {
            var selected = physical <= 51 ? current.ReadCityTableReference(physical) : current.ReadGateTableReference(physical - 51);
            var body = selected ?? throw Missing("forced_target_Base_pointer");
            if (Need(current.ReadBaseNativeValidity(body), "forced_target_Base_validity"))
                return (physical <= 51 ? 5 : 6, Need(body.PhysicalSlot, "forced_target_Base_physical"));
        }
        if (armySlot != 0)
        {
            if (armySlot < 0) throw Missing("branch.forced_target_negative_Army_pointer");
            var army = current.ReadArmyUnitTableReference(armySlot < 501 ? armySlot : 0) ?? throw Missing("forced_target_Army_pointer");
            if (Need(current.ReadArmyUnitNativeValidity(army), "forced_target_Army_validity"))
            {
                if (Need(current.ReadOwnedArmyExclusion233560(new(OriginalRegistryKind.Army, army.PhysicalSlot)), "forced_target_Army233560"))
                    return (255, 0);
                return (27, Need(army.PhysicalSlot, "forced_target_Army_physical"));
            }
        }
        return objectValid ? (11, physical) : (255, 0);
    }
    private static OriginalHexReference Hex(OriginalEventInitializationContext current, int raw) =>
        current.ReadHexTableReference(unchecked((uint)raw) < 48400 ? raw : 0) ?? throw Missing("Hex_pointer");
    private static int Distance(int first, int second)
    {
        int x1 = first % 220, y1 = first / 220, x2 = second % 220, y2 = second / 220;
        int dx = Math.Abs(x2 - x1), dy = Math.Abs(2 * (y2 - y1) - (x1 % 2 == 0 ? 1 : 0) + (x2 % 2 == 0 ? 1 : 0));
        return dy < dx ? dx : dx + (dy - dx) / 2;
    }
    private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("Current2CA7B0 Base threat owner is unknown: " + field);
}
