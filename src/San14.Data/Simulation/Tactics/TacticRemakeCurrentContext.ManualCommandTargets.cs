using San14.Core;

namespace San14.Data;

public sealed partial class TacticRemakeCurrentContext
{
    /// <summary>Current target-selection Hex chain. This does not activate a
    /// tactic or replace its separate eligibility and execution gates.</summary>
    internal IReadOnlyList<int>? ReadManualCommandTargetHexes(GameState state, UnitState caster, int tacticId)
    {
        if (!Bound(state) || tacticId < 0 || tacticId >= _catalog.Rows.Count) return null;
        try
        {
            if (!ManualCommandTargetKnown(ArmyValid(state, caster), "selector_caster_validity") ||
                !ManualCommandTargetKnown(ReadManualTacticValidity(state, tacticId), "selector_tactic_validity")) return [];
            int rawSource = ManualCommandTargetKnown(ArmyMarchSourceAdapter.ReadCurrentHex(state, caster), "selector_caster_Hex");
            int? source = ResolveHex(rawSource);
            if (source is not int sourceHex) return [];
            if (caster.NativeTacticRuntime is not { } native ||
                ReadCurrentProgram(state, _catalog.Rows[tacticId]) is not { } program || !ManualProgram(program)) return null;
            int range = ManualCommandTargetKnown(ReadArmyNativePrimaryRange(state, caster), "selector_primary_range");
            var targets = new List<int>();
            var targetSet = new HashSet<int>();
            //The source retains this separate list across both instructions,
            //including footprints of centers whose instruction vector is empty.
            var footprints = new List<int>();
            var footprintSet = new HashSet<int>();

            foreach (var instruction in program.Instructions)
            {
                if (instruction.Opcode is not (1 or 2 or 3 or 4 or 10 or 11 or 12 or 13 or 14 or 16 or 17 or 18 or 22 or 23)) continue;
                if (native.Abnormal25 == 0)
                {
                    foreach (int hex in NativeHexOrder(sourceHex, range).Skip(1))
                    {
                        var read = FieldHexOccupantSource.ReadCurrent(state, _definitions, hex);
                        var selected = read.Receipt ?? throw ManualCommandTargetMissing(read.UnresolvedOwner ?? "selector_Hex_occupant");
                        int? center;
                        if (selected.NativeKind == 27)
                            center = ArmyCenter(selected.NativePhysicalSlot);
                        else if (selected.NativeKind is 5 or 6 && TargetsBases())
                        {
                            var receiver = BaseReceiver(selected.NativeKind, selected.NativePhysicalSlot);
                            if (!ManualCommandTargetKnown(_definitions.ReadBaseNativeValidity(receiver), "selector_Base_validity")) continue;
                            int rawCenter = _definitions.ReadBaseCurrentHexIndex(receiver);
                            //The Gate test uses its actual linked Object's Hex,
                            //before resolving the selectable Hex pointer.
                            if (selected.NativeKind == 6 && LinkedDistance(sourceHex, rawCenter) > range) continue;
                            center = ResolveHex(rawCenter);
                            if (center is int baseHex) AddFootprint(baseHex, selected.NativeKind);
                        }
                        else continue;
                        if (center is int candidate) AddCenter(candidate, instruction.Index);
                    }
                }
                else
                {
                    if (caster.NativeArmyOrigin?.NativeSlotIndex is not int physical) return null;
                    var registry = _definitions.ReadRegistryTableReference(RegistryKind.Army, physical)
                        ?? throw ManualCommandTargetMissing("selector_caster_registry");
                    var army = _definitions.ReadArmyUnitTableReference(physical)
                        ?? throw ManualCommandTargetMissing("selector_caster_Army_pointer");
                    byte kind = ManualCommandTargetKnown(_definitions.ReadTacticClearByte(registry, 0x3C), "selector_restricted_target_kind");
                    int identity = ManualCommandTargetKnown(_definitions.ReadTacticArmyTarget3E(army), "selector_restricted_target_identity");
                    int maxDistance = range;
                    int? center = null;
                    if (kind == 27) center = ArmyCenter(identity);
                    else if (kind is 5 or 6 && TargetsBases())
                    {
                        var receiver = BaseReceiver(kind, identity);
                        if (ManualCommandTargetKnown(_definitions.ReadBaseNativeValidity(receiver), "selector_restricted_Base_validity"))
                            center = ResolveHex(_definitions.ReadBaseCurrentHexIndex(receiver));
                        if (kind == 5) maxDistance = 2;
                    }
                    if (center is int candidate && LinkedDistance(sourceHex, candidate) <= maxDistance)
                        AddCenter(candidate, instruction.Index);
                }
                if (targets.Count != 0)
                {
                    //These are independently deduplicated source lists. The
                    //final append deliberately retains cross-list duplicates.
                    targets.AddRange(footprints);
                    return targets.ToArray();
                }
            }
            //The selector always offers its source Hex if no selected-centered
            //instruction yielded a candidate. Formal execution remains stricter.
            return [sourceHex];

            bool TargetsBases() => (ManualCommandTargetKnown(ReadCurrentTargetFlags(state, _catalog.Rows[tacticId]),
                "selector_tactic_target_flags") & 1) != 0;

            int? ArmyCenter(int identity)
            {
                var army = _definitions.ReadArmyUnitTableReference(identity is >= 0 and <= 500 ? identity : 0)
                    ?? throw ManualCommandTargetMissing("selector_target_Army_pointer");
                if (!ManualCommandTargetKnown(_definitions.ReadArmyUnitNativeValidity(army), "selector_target_Army_validity")) return null;
                return ResolveHex(ManualCommandTargetKnown(_definitions.ReadArmyUnitRawHex2A(army), "selector_target_Army_Hex"));
            }

            BaseReference BaseReceiver(int kind, int identity) => (kind == 5
                ? _definitions.ReadCityTableReference(identity is >= 0 and <= 51 ? identity : 0)
                : _definitions.ReadGateTableReference(identity is >= 0 and <= 10 ? identity : 0))
                ?? throw ManualCommandTargetMissing("selector_target_Base_pointer");

            int? ResolveHex(int raw)
            {
                var hex = _definitions.ReadHexTableReference(raw is >= 0 and < 48400 ? raw : 0)
                    ?? throw ManualCommandTargetMissing("selector_Hex_pointer");
                return hex.PhysicalSlot;
            }

            void AddCenter(int center, int instructionIndex)
            {
                if (targetSet.Contains(center)) return;
                var vectors = CaptureManualInstructionHexVectorsCurrent(state, caster, tacticId, program, sourceHex, center)
                    ?? throw ManualCommandTargetMissing("selector_instruction_Hex_vectors");
                if (vectors[instructionIndex].OrderedTargets.Length == 0) return;
                targetSet.Add(center); targets.Add(center);
            }

            void AddFootprint(int center, int kind)
            {
                foreach (int neighbor in NativeHexOrder(center, 1).Skip(1))
                {
                    var hex = _definitions.ReadHexTableReference(neighbor)
                        ?? throw ManualCommandTargetMissing("selector_footprint_Hex_pointer");
                    if (hex.PhysicalSlot is not int physical) continue;
                    int link = ManualCommandTargetKnown(_definitions.ReadHexObjectLink(hex), "selector_footprint_Object_link");
                    bool include = kind == 5 ? link is >= 1 and <= 51 : link is >= 52 and <= 61;
                    if (include && footprintSet.Add(physical)) footprints.Add(physical);
                }
            }
        }
        catch (InvalidDataException) { return null; }
    }

    private static T ManualCommandTargetKnown<T>(T? value, string owner) where T : struct =>
        value ?? throw ManualCommandTargetMissing(owner);

    private static InvalidDataException ManualCommandTargetMissing(string owner) =>
        new("manual_command_target." + owner);
}
