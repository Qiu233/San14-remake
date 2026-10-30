using San14.Core;

namespace San14.Data.AI.Original;

public sealed partial class OriginalAiOperation
{
    /// <summary>Source-owned loop rosters. Mode0 uses the actual actor, mode1
    /// uses the native table, and mode2 is handled by the retained list owner.
    /// A command can replace the world while a loop is active; predicates and
    /// the next native node are read through the operation's latest current.</summary>
    internal OriginalAiNodeStatus LoopSourceCandidates(int mode, int resolver, uint output,
        IReadOnlyList<Func<OriginalAiOperation, OriginalAiNodeStatus>> children)
    {
        if (mode == 1 && resolver == 3)
        {
            for (int slot = 0; slot < 501; slot++)
            {
                var pointer = InputsCurrent.ReadStaticTableReference(0x7F8D8, slot)
                    ?? throw ListSourceMissing("military_group_table");
                var group = new OriginalAiTroopsReference(ListSourceNeed(pointer.PhysicalSlot, "military_group_pointer"));
                if (!Adapter.ReadOriginalAiTroopsValidityCurrent(InputsCurrent, group)) continue;
                SetTroops(output, group);
                if (RunSourceLoopElement(children)) break;
            }
            return OriginalAiNodeStatus.Success;
        }
        if (mode != 0) throw ListSourceMissing("mode_" + mode);
        if (Actor.AgentKind is null) throw ListSourceMissing("actor_kind");
        if (Actor.AgentKind is not (OriginalAiAgentKind.Force or OriginalAiAgentKind.District))
            return OriginalAiNodeStatus.Success;
        int? district = Actor.AgentKind == OriginalAiAgentKind.District
            ? Adapter.ReadOriginalAiMyselfDistrict(InputsCurrent, Actor)?.Id
                ?? throw ListSourceMissing("actor_District") : null;
        int? force = district.HasValue ? null : ListSourceNeed(InputsCurrent.ReadForceTableReference(Actor.ForceId is >= 0 and < 52 ? Actor.ForceId : 0)?.PhysicalSlot,
                "actor_Force_pointer");
        // The native filtered iterator rejects an invalid physical actor before
        // reading any candidate field. There is no substitute global roster.
        if ((district ?? force) is < 1 or > 51) return OriginalAiNodeStatus.Success;
        if (resolver is 6 or 7)
        {
            force ??= Adapter.ReadOriginalAiMyselfForce(InputsCurrent, Actor)?.Id ?? throw ListSourceMissing("actor_Force");
            if (force is < 1 or > 51) return OriginalAiNodeStatus.Success;
        }

        if (resolver == 3)
        {
            IEnumerable<int> Districts() => district is int selected ? [selected]
                : SourceLoopRegistry(RegistryKind.District).Where(id =>
                    ListSourceNeed(InputsCurrent.ReadDistrictForceId(id), "District10") == force);
            foreach (int selected in Districts())
            {
                if (selected is < 1 or > 51) continue;
                for (int slot = 0; slot < 501; slot++)
                {
                    var group = Adapter.ReadOriginalAiStrategyGroupAtCurrent(InputsCurrent, selected, slot);
                    if (group is not { } value) continue;
                    SetTroops(output, value);
                    if (RunSourceLoopElement(children)) return OriginalAiNodeStatus.Success;
                }
            }
            return OriginalAiNodeStatus.Success;
        }
        if (resolver == 1)
        {
            foreach (int physical in SourceLoopRegistry(RegistryKind.Person))
            {
                var current = InputsCurrent;
                int owner = district.HasValue
                    ? ListSourceNeed(current.ReadPersonDistrictSlot(physical), "PersonE0")
                    : ListSourceNeed(current.ReadPersonOldForceSlot(physical), "Person_Force");
                if (owner != (district ?? force) || ListSourceNeed(current.ReadPersonStatus(physical), "PersonE6") is < 1 or > 4) continue;
                SetEntity(output, new(WorldEntityKind.Officer, ListSourceNeed(current.ReadPersonIdentity(physical), "Person10")));
                if (RunSourceLoopElement(children)) break;
            }
            return OriginalAiNodeStatus.Success;
        }
        if (resolver == 10)
        {
            // World80 is a mixed Base registry, not Cities concatenated with
            // Gates and not the logical ID order of either domain dictionary.
            foreach (var entry in InputsCurrent.ReadRegisteredBases() ?? throw ListSourceMissing("Base_registry80"))
            {
                var at = entry ?? throw ListSourceMissing("Base_pointer");
                var current = InputsCurrent;
                int owner = ListSourceNeed(district.HasValue ? current.ReadBaseDistrictSlot(at) : current.ReadBaseForceSlot(at), "Base_owner");
                if (owner != (district ?? force)) continue;
                SetEntity(output, new(at.Kind == BaseKind.City ? WorldEntityKind.City : WorldEntityKind.Gate,
                    ListSourceNeed(current.ReadBaseObjectLink(at), "Base4E")));
                if (RunSourceLoopElement(children)) break;
            }
            return OriginalAiNodeStatus.Success;
        }
        if (resolver is 6 or 7)
        {
            foreach (int physical in SourceLoopRegistry(RegistryKind.Area))
            {
                var current = InputsCurrent;
                var area = new AreaReference(physical);
                if (ListSourceNeed(current.ReadAreaCurrentForceSlot(area), "Area_representative_Hex14") != force) continue;
                if (district is int selected)
                {
                    BaseReference at;
                    if (resolver == 7)
                    {
                        int city = ListSourceNeed(current.ReadAreaCitySlot(area), "Area35");
                        at = current.ReadCityTableReference(city < 52 ? city : 0) ?? throw ListSourceMissing("Area_City_pointer");
                    }
                    else
                    {
                        int rawHex = ListSourceNeed(current.ReadAreaHexIndex(area), "Area3A");
                        var hex = current.ReadHexTableReference(rawHex < 48400 ? rawHex : 0) ?? throw ListSourceMissing("Area_Hex_pointer");
                        int rawBase = ListSourceNeed(current.ReadHexObjectLink(hex), "Area_Hex1A");
                        at = BaseLookup.Find(rawBase < 62 ? rawBase : 0, current);
                    }
                    if (ListSourceNeed(current.ReadBaseDistrictSlot(at), "Area_Base_District") != selected) continue;
                }
                if (resolver == 7)
                {
                    SetEntity(output, new(WorldEntityKind.Area, physical));
                    if (RunSourceLoopElement(children)) break;
                }
                else
                    for (int index = AreaHexIterator.FindNextIndex(0, physical, InputsCurrent); index < 48400;
                        index = AreaHexIterator.FindNextIndex(index + 1, physical, InputsCurrent))
                    {
                        var hex = InputsCurrent.ReadHexTableReference(index) ?? throw ListSourceMissing("Hex_table_pointer");
                        SetHex(output, new(ListSourceNeed(hex.PhysicalSlot, "Hex_identity")));
                        if (RunSourceLoopElement(children)) return OriginalAiNodeStatus.Success;
                    }
            }
            return OriginalAiNodeStatus.Success;
        }
        throw ListSourceMissing("resolver_" + resolver);
    }

    private IEnumerable<int> SourceLoopRegistry(RegistryKind kind)
    {
        var descriptor = InputsCurrent.ReadRegistryDescriptor(kind) ?? throw ListSourceMissing(kind + "_registry");
        var node = InputsCurrent.ReadRegistryHead(descriptor) ?? throw ListSourceMissing(kind + "_head");
        while (node.Identity.HasValue)
        {
            var value = InputsCurrent.ReadRegistryNodePayload(node) ?? throw ListSourceMissing(kind + "_payload");
            if (value.Kind != kind) throw ListSourceMissing(kind + "_payload_type");
            yield return ListSourceNeed(value.PhysicalSlot, kind + "_pointer");
            node = InputsCurrent.ReadRegistryNodeNext(node) ?? throw ListSourceMissing(kind + "_next");
        }
    }

    private bool RunSourceLoopElement(IReadOnlyList<Func<OriginalAiOperation, OriginalAiNodeStatus>> children)
    {
        // The compiler already supplies Enter/Update/Exit around each child.
        // Failure or Running ends this element's child chain, never the loop.
        foreach (var child in children)
        {
            var status = child(this);
            if (TryInteger(LoopBreakKey, out int requested) && requested > 0) break;
            if (status != OriginalAiNodeStatus.Success) break;
        }
        if (!TryInteger(LoopBreakKey, out int stop) || stop <= 0) return false;
        SetInteger(LoopBreakKey, 0);
        return true;
    }

    private static T ListSourceNeed<T>(T? value, string field) where T : struct => value ?? throw ListSourceMissing(field);
    private static InvalidDataException ListSourceMissing(string field) => new("original_ai.list_loop." + field);
}
