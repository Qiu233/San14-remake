using System.Collections.Immutable;
using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

internal sealed partial class CampaignEventExecutionContext
{
    // This is the invocation's independent ptr_list<Area>, not the five
    // message Area parameters and not a projection of the selected Cities.
    private readonly List<int> _storyAreaPhysicalSlots = [];
    private bool _storyAreasKnown = true;

    internal IReadOnlyList<int>? ReadStoryAreaSelections() => _storyAreasKnown
        ? _storyAreaPhysicalSlots.AsReadOnly() : null;

    internal ImmutableArray<int>? CaptureStoryAreaSelections() => _storyAreasKnown
        ? _storyAreaPhysicalSlots.ToImmutableArray() : null;

    internal void RestoreStoryAreaSelections(ImmutableArray<int>? slots)
    {
        _storyAreaPhysicalSlots.Clear();
        _storyAreasKnown = slots.HasValue;
        if (slots is not { } saved) return;
        // Restoring a captured pointer sequence does not rerun native validity
        // or reconstruct order from the now-current world's registration list.
        _storyAreaPhysicalSlots.AddRange(saved);
    }

    internal void ResetStoryAreaSelections()
    {
        _storyAreaPhysicalSlots.Clear();
        _storyAreasKnown = true;
    }

    internal int ExecuteStoryAreaList(S14ConditionInstruction operation,
        Func<int, S14EventConditionForceReference?> readTargetForce)
    {
        var arguments = operation.RawArguments;
        if (arguments.Count != 8) throw Missing("Area list arguments");
        if (operation.Code != 188) RequireStoryAreas();
        switch (operation.Code)
        {
            case 188:
                ResetStoryAreaSelections();
                return 1;
            case 189:
                foreach (int raw in arguments)
                {
                    var area = StoryAreaLookup(raw);
                    if (Known(StoryAreaCurrent.ReadAreaNativeValidity(area), "added Area validity"))
                        AppendStoryArea(area);
                }
                return 1;
            case 296:
                foreach (int raw in arguments)
                {
                    var area = StoryAreaLookup(raw);
                    if (!Known(StoryAreaCurrent.ReadAreaNativeValidity(area), "removed Area validity")) continue;
                    RequireStoryAreas();
                    _storyAreaPhysicalSlots.Remove(StoryAreaPhysical(area));
                }
                return 1;
            case 191:
                AddStoryAreasByOfficer(arguments, readTargetForce);
                return 1;
            case 316:
                AddStoryAreasByForceHex(arguments[0], readTargetForce);
                return 1;
            case 192:
            {
                // This producer collects only native-valid selected Cities.
                // With no such City it leaves the existing Area list intact.
                var cities = new List<BaseReference>();
                foreach (int selector in arguments)
                {
                    var city = ResolveStoryCitySelector(selector);
                    if (Known(Queries.ReadConditionBaseValidity(city), "Area filter City validity")) cities.Add(city);
                }
                RequireStoryAreas();
                if (cities.Count != 0)
                    _storyAreaPhysicalSlots.RemoveAll(physical =>
                        !cities.Contains(StoryAreaCity(new(physical), original: false)));
                return _storyAreaPhysicalSlots.Count != 0 ? 1 : 0;
            }
            case 321:
            case 322:
            {
                // Unlike192,3D7D30 retains all eight resolved City pointers.
                // Its predicate tests validity as each comparison is reached.
                var cities = arguments.Select(ResolveStoryCitySelector).ToArray();
                RequireStoryAreas();
                bool removeMatching = operation.Code == 322;
                _storyAreaPhysicalSlots.RemoveAll(physical =>
                {
                    var original = StoryAreaCity(new(physical), original: true);
                    bool matched = false;
                    foreach (var city in cities)
                        if (Known(Queries.ReadConditionBaseValidity(city), "original Area filter City validity") && city == original)
                        { matched = true; break; }
                    return removeMatching ? matched : !matched;
                });
                return _storyAreaPhysicalSlots.Count != 0 ? 1 : 0;
            }
            default: throw Missing($"Area list handler {operation.Code}");
        }
    }

    private SourceWorldContext StoryAreaCurrent => inputs.AreaListCurrent
        ?? throw Missing("Area list current owner");

    private void RequireStoryAreas()
    {
        if (!_storyAreasKnown) throw Missing("saved Area list");
    }

    private static int StoryAreaPhysical(AreaReference area) => area.PhysicalSlot
        is int physical and >= 0 and <= 500 ? physical : throw Missing("Area pointer identity");

    private AreaReference StoryAreaLookup(int raw)
    {
        if (raw < 0) throw Missing("negative Area table index");
        return StoryAreaCurrent.ReadAreaTableReference(raw < 501 ? raw : 0)
            ?? throw Missing("Area table pointer");
    }

    private void AppendStoryArea(AreaReference area)
    {
        RequireStoryAreas();
        int physical = StoryAreaPhysical(area);
        // Source uniqueness is pointer equality. The persisted value is the
        // resolved physical identity, preserving table aliases and append order.
        if (!_storyAreaPhysicalSlots.Contains(physical)) _storyAreaPhysicalSlots.Add(physical);
    }

    private IReadOnlyList<AreaReference?> StoryRegisteredAreas() => StoryAreaCurrent.ReadRegisteredAreaReferences()
        ?? throw Missing("registered Area pointer sequence");

    private void AddStoryAreasByOfficer(IReadOnlyList<int> arguments,
        Func<int, S14EventConditionForceReference?> readTargetForce)
    {
        foreach (int selector in arguments)
        {
            var person = ResolvePerson(selector, readTargetForce);
            if (!Known(Queries.ReadConditionPersonValidity(person), "Area officer Person validity")) continue;
            var force = StoryAreaCurrent.ReadPersonForce(person.PhysicalSlot)
                ?? throw Missing("Area officer Force pointer");
            if (!Known(StoryAreaCurrent.ReadForceNativeValidity(force), "Area officer Force validity")) continue;
            int owner = Known(StoryAreaCurrent.ReadForcePhysicalSlot(force), "Area officer Force identity");
            //334700's Force Area iterator has this explicit identity gate.
            if (owner is < 1 or > 51) continue;
            foreach (var pointer in StoryRegisteredAreas())
            {
                var area = pointer ?? throw Missing("registered Area pointer");
                if (Known(StoryAreaCurrent.ReadAreaCurrentForceSlot(area), "officer Area representative Hex Force") != owner) continue;
                ushort rawPerson = Known(StoryAreaCurrent.ReadAssignmentArea36(StoryAreaPhysical(area)), "Area assigned Person36");
                var assigned = (rawPerson <= 6000 ? Queries.ReadConditionPersonTableSlot(rawPerson)
                    : Queries.ReadConditionBasePerson()) ?? throw Missing("Area assigned Person lookup");
                if (assigned.Value != person) continue;
                AppendStoryArea(area);
                //3A3270 advances to the next operand at the first match, even
                //when this Area was already present in the invocation list.
                break;
            }
        }
    }

    private void AddStoryAreasByForceHex(int selector,
        Func<int, S14EventConditionForceReference?> readTargetForce)
    {
        var force = ResolveForce(selector, readTargetForce);
        if (force is null || !Known(Queries.ReadConditionForceValidity(force), "Area Hex Force validity")) return;
        var target = new ForceReference(force.PhysicalSlot);
        var current = StoryAreaCurrent;
        foreach (var pointer in StoryRegisteredAreas())
        {
            var area = pointer ?? throw Missing("registered Area pointer");
            int representativeOwner = Known(current.ReadAreaCurrentForceSlot(area), "Area representative Hex Force");
            var representativeForce = current.ReadForceTableReference(representativeOwner < 52 ? representativeOwner : 0)
                ?? throw Missing("Area representative Force pointer");
            if (representativeForce == target) { AppendStoryArea(area); continue; }
            int physical = StoryAreaPhysical(area);
            for (int index = AreaHexIterator.FindNextIndex(0, physical, current); index < 48400;
                index = AreaHexIterator.FindNextIndex(index + 1, physical, current))
            {
                var hex = current.ReadHexTableReference(index) ?? throw Missing("Area Hex pointer");
                int owner = Known(current.ReadHexForceSlot(hex), "Area Hex Force14");
                var actual = current.ReadForceTableReference(owner < 52 ? owner : 0)
                    ?? throw Missing("Area Hex Force pointer");
                if (actual != target) continue;
                AppendStoryArea(area);
                break;
            }
        }
    }

    private BaseReference StoryAreaCity(AreaReference area, bool original)
    {
        byte raw = original ? Known(StoryAreaCurrent.ReadAreaValidityCode(area), "Area original City34")
            : Known(StoryAreaCurrent.ReadAreaCitySlot(area), "Area current City35");
        return StoryAreaCurrent.ReadCityTableReference(raw < 52 ? raw : 0)
            ?? throw Missing("Area City table pointer");
    }
}
