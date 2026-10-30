using San14.Data.Formats;

namespace San14.Data;

internal sealed partial class CampaignEventExecutionContext
{
    internal int ExecuteStoryAreaFilter(int code, IReadOnlyList<int> arguments)
    {
        if (arguments.Count != 8) throw Missing("additional Area-filter operands");
        RequireStoryAreas();
        if (code == 293)
        {
            var city = ResolveStoryCitySelector(arguments[0]);
            if (Known(Queries.ReadConditionBaseValidity(city), "removed Area current City validity"))
                _storyAreaPhysicalSlots.RemoveAll(physical => StoryAreaCity(new(physical), original: false) == city);
        }
        else if (code is 313 or 314)
        {
            int raw = arguments[0];
            if (raw < 0) throw Missing("negative Area-filter province index");
            var target = Queries.ReadConditionStateTableSlot(raw < 23 ? raw : 0)
                ?? throw Missing("Area-filter province pointer");
            if (Known(Queries.ReadConditionStateValidity(target), "Area-filter province validity"))
                _storyAreaPhysicalSlots.RemoveAll(physical =>
                {
                    var original = StoryAreaCity(new(physical), original: true);
                    //314's shared3D42A0 predicate rejects an invalid original
                    //City before reading its province.313 has no such gate.
                    if (code == 314 && !Known(Queries.ReadConditionBaseValidity(original), "removed Area original City validity"))
                        return true;
                    int city = Known(original.PhysicalSlot, "Area original City identity");
                    var province = Queries.ReadConditionCityState(city)
                        ?? throw Missing("Area original City province pointer");
                    bool equal = province == target;
                    return code == 314 ? equal : !equal;
                });
        }
        else throw Missing("additional Area-filter opcode");
        return _storyAreaPhysicalSlots.Count != 0 ? 1 : 0;
    }

    internal int ExecuteStoryPersonFilter(int code, IReadOnlyList<int> arguments,
        Func<int, int?>? readDoctrine)
    {
        //379 is the real SBlockControlFunction execution leaf. It is not a
        //Person operation: its actual body returns1 without reading anything.
        if (code == 379) return 1;
        if (code != 434) throw Missing("additional Person-filter opcode");
        if (arguments.Count != 8) throw Missing("Person Doctrine-filter operands");
        int requested = arguments[0];
        if ((uint)(requested - 1) < 6)
            _candidates.RemoveAll(person =>
                (readDoctrine?.Invoke(person.PhysicalSlot) ?? throw Missing("Person raw Doctrine14C")) != requested);
        //No native-Person/Doctrine-validity gate or actor-bank mutation.
        return _candidates.Count != 0 ? 1 : 0;
    }
}
