using San14.Core;

namespace San14.Data;

internal sealed record StoryEmperorApplication(GameState State, int NativeReturn);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The normal112/113/114 handlers use the real saved Emperor,
    /// Home and position owners. They do not run an ordinary Person death,
    /// assignment, status or Force-membership transaction.</summary>
    internal StoryEmperorApplication ApplyStoryEmperorCurrent(GameState state,
        SourceWorldContext current, int code, int? personPhysical = null, BaseReference? targetBase = null)
    {
        if (code is not (112 or 113 or 114)) throw StoryEmperorMissing("opcode");
        if (code == 112)
        {
            int target = personPhysical ?? throw StoryEmperorMissing("selected_Person");
            if (!Need(current.ReadPersonNativeValidity(target), "selected_Person_validity")) return new(state, 1);
            ushort first = 0, second = 0;
            if (ReadValidEmperor() is int old)
            {
                first = Need(current.ReadPersonFirstLocation(old), "previous_Emperor_E2");
                second = Need(current.ReadPersonSecondLocation(old), "previous_Emperor_E4");
                WriteLocations(old, 0, 0);
            }
            WriteLocations(target, (ushort)Math.Min((int)first, 61), (ushort)Math.Min((int)second, 1061));
            int identity = Need(current.ReadPersonIdentity(target), "selected_Person_ID10");
            WriteEmperor(Math.Min(identity, 6000));
        }
        else if (code == 114)
        {
            //An out-of-range or invalid saved Emperor does not clear1654.
            if (ReadValidEmperor() is int old)
            {
                WriteLocations(old, 0, 0);
                WriteEmperor(0);
            }
        }
        else
        {
            var target = targetBase ?? throw StoryEmperorMissing("selected_Base");
            if (!Need(current.ReadBaseNativeValidity(target), "selected_Base_validity")) return new(state, 1);
            if (ReadValidEmperor() is int emperor)
            {
                int location = Need(current.ReadBaseObjectLink(target), "selected_Base4E");
                //The actual call keeps raw Base4E in its third argument.
                //It does not subtract51 for Gate receivers: kind6 then
                //rejects those global52..61 indices and stores position0.
                int kind = location < 52 ? 5 : location <= 61 ? 6 : 255;
                int index = location;
                ushort position = kind == 5 ? (ushort)(index is >= 0 and <= 51 ? index : 0)
                    : kind == 6 ? (ushort)(index is >= 1 and <= 10 ? index + 51 : 0)
                    : (ushort)(index is >= 1 and <= 500 ? index + 561 : 0);
                WriteLocations(emperor, (ushort)Math.Min(location, 61), position);
            }
        }
        return new(state, 1);

        int? ReadValidEmperor()
        {
            ushort id = Need(CityCaptureTerritoryInputs.ReadEmperor1654(state), "saved_World1654");
            if (id is < 1 or > 6000) return null;
            var selected = current.ReadPersonLookupReference(id) ?? throw StoryEmperorMissing("Emperor_lookup");
            int at = Need(selected.PhysicalSlot, "Emperor_receiver");
            return Need(current.ReadPersonNativeValidity(at), "Emperor_validity") ? at : null;
        }
        void WriteLocations(int physical, ushort first, ushort second)
        {
            int identity = Need(current.ReadPersonIdentity(physical), "written_Person_ID10");
            if (current.ResolveOfficerSource(identity) != physical) throw StoryEmperorMissing("written_Person_binding");
            var person = state.Officers.GetValueOrDefault(identity) ?? throw StoryEmperorMissing("written_Person_owner");
            current.WritePersonFirstLocation(physical, first);
            state = OwnedArmyLifecycleDetachedInputs.PublishCurrentPersonFirstLocation(state, current, physical);
            state = state with { Officers = new(state.Officers) { [identity] = person with
                { LocationId = first == 0 ? null : first } } };
            current.WritePersonSecondLocation(physical, second);
            state = OfficerLocationSourceAdapter.PublishPosition(state, identity, current);
        }
        void WriteEmperor(int id)
        {
            var simulation = state.NativeArmySimulation ?? throw StoryEmperorMissing("saved_Emperor_owner");
            _ = Need(CityCaptureTerritoryInputs.ReadEmperor1654(state), "saved_World1654");
            state = state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with
                { Int32Values = new(simulation.Parameters.Int32Values) { [CityCaptureTerritoryInputs.Emperor1654Key] = id } } } };
            CityCaptureTerritoryInputs.BindCurrentEmperor(state, current);
        }
        static T Need<T>(T? value, string owner) where T : struct => value ?? throw StoryEmperorMissing(owner);
    }

    private static InvalidDataException StoryEmperorMissing(string owner) => new("story.emperor." + owner);
}
