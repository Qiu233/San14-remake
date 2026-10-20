using San14.Core;

namespace San14.Data;

/// <summary>Current trait propagation, including actual temporary descriptors,
/// rejection removal, cached contributions and persistent related-list history.</summary>
internal static class ArmyTraitApplication
{
    internal static void Apply(GameState state, SourceWorldContext current,
        ArmyReturnTraitCleanup.Current fields, ArmyUnitReference source, PersonalityReference trait,
        HexListHandle targets, ArmyTraitFeedback feedback, Action<int> personChanged)
    {
        if (!Need(current.ReadArmyUnitNativeValidity(source), "source_validity") ||
            !Need(current.ReadSkillPersonalityValidity(trait), "trait_validity")) return;
        ushort key = checked((ushort)(trait.PhysicalSlot ?? throw Missing("trait_identity")));
        int commander = fields.Person(Need(current.ReadArmyUnitCommanderId(source), "source_Commander"));
        for (int index = 0; index < 9; index++)
        {
            ushort mask = Need(current.ReadPersonTraitMask1BC(commander), "active_mask");
            ushort activeKey = (mask & (1 << index)) != 0 ? fields.TraitKey(commander, index) : (ushort)0;
            if (activeKey == key) return;
        }
        var scratch = current.CreateNearbyTemporary();
        var accepted = current.CreateNearbyTemporary();
        var rejected = current.CreateNearbyTemporary();
        try
        {
            foreach (var target in fields.Receivers(new(targets.Identity)))
            {
                current.ClearNearbyTemporary(scratch, false);
                bool succeeds = ArmyTraitConditions.Collect(state, current, target, trait, 2, scratch);
                current.AppendNearbyArmyTemporary(succeeds ? accepted : rejected, target);
            }
            foreach (var target in fields.Receivers(new(rejected.Identity)))
                foreach (var node in fields.Nodes(new(targets.Identity)))
                {
                    var payload = current.ReadRegistryNodePayload(node) ?? throw Missing("rejected_target_payload");
                    if (payload.Kind != RegistryKind.Army) throw Missing("rejected_target_kind");
                    if (payload.PhysicalSlot == target.PhysicalSlot) current.UnlinkArmyReturnTraitNode(new(targets.Identity), node);
                }
            _ = TacticRemakeCurrentTraits.ArmyContributions(current, commander, fields.Fields(source).Traits64)
                ?? throw Missing("complete_current127_contributions");
            bool changed = false;
            foreach (var target in fields.Receivers(new(accepted.Identity)))
            {
                if (target.PhysicalSlot == source.PhysicalSlot)
                {
                    changed = true;
                    int person = fields.Person(Need(current.ReadArmyUnitCommanderId(target), "self_Commander"));
                    int selectedSlot = 0;
                    for (int i = 0; i < 9; i++) if (fields.TraitKey(person, i) == key) { selectedSlot = i; break; }
                    ushort mask = Need(current.ReadPersonTraitMask1BC(person), "self_mask");
                    current.WritePersonTraitMask1BC(person, (ushort)(mask | (1 << selectedSlot)));
                    personChanged(person);
                    short timer = unchecked((short)Need(current.ReadPersonalityWord(trait, 0xB4), "self_timer_definition"));
                    //A zero B4 deliberately does not store a timer.
                    if (timer != 0) current.WritePersonTraitExtra1BE(person, selectedSlot, (ushort)Math.Clamp((int)timer, 0, 720));
                }
                else
                {
                    var before = fields.Fields(target); bool duplicate = false; int empty = -1;
                    ushort sourceCommander = Need(current.ReadArmyUnitCommanderId(source), "external_source_Commander");
                    for (int i = 0; i < 36; i++)
                    {
                        if (before.Traits64[i] == key && before.PersonsAC[i] == sourceCommander) duplicate = true;
                        if (before.Traits64[i] == 0 && empty < 0) empty = i;
                    }
                    if (!duplicate && empty >= 0)
                    {
                        changed = true;
                        var keys = before.Traits64.ToArray(); var people = before.PersonsAC.ToArray(); var timers = before.AuxiliaryF4.ToArray();
                        keys[empty] = (ushort)Math.Clamp((int)key, 0, 355);
                        people[empty] = Math.Min(sourceCommander, (ushort)6000);
                        timers[empty] = (ushort)Math.Min((int)Need(current.ReadPersonalityWord(trait, 0xB4), "external_timer_definition"), 720);
                        current.WriteArmyReturnTraitFields(target.PhysicalSlot ?? throw Missing("external_target_pointer"),
                            new(keys, people, timers, before.Descriptors160));
                        int slot = fields.MatchingTrait(source, key);
                        if (slot >= 0)
                        {
                            var descriptor = fields.Fields(source).Descriptors160[slot];
                            if (!fields.Receivers(descriptor).Any(other => other.PhysicalSlot == target.PhysicalSlot))
                                current.AppendNearbyArmyTemporary(new(descriptor.Identity), target);
                        }
                    }
                }
                //The source's changed flag survives previous targets. Refresh
                //the SOURCE army again even if this target inserted nothing.
                if (changed) feedback.RefreshAndFeedback(source, trait);
            }
        }
        finally
        {
            current.ClearNearbyTemporary(rejected, true);
            current.ClearNearbyTemporary(accepted, true);
            current.ClearNearbyTemporary(scratch, true);
        }
    }
    private static T Need<T>(T? value, string name) where T : struct => value ?? throw Missing(name);
    private static InvalidDataException Missing(string name) => new("army.trait-maintenance.application." + name);
}
