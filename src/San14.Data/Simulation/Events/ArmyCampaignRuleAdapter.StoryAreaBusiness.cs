using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal (GameState State, int NativeReturn) ApplyStoryAreaBusinessCurrent(GameState state,
        SourceWorldContext current, WorldIndexContext pool, int code, IReadOnlyList<int> arguments,
        IReadOnlyList<int>? areaPhysicalSlots, ForceReference? targetForce)
    {
        if (arguments.Count != 8 || code is < 193 or > 202) throw StoryAreaBusinessMissing("operation");
        var areas = areaPhysicalSlots;
        var working = state;
        if (code == 201)
        {
            var force = targetForce ?? throw StoryAreaBusinessMissing("selected_Force");
            if (force.PhysicalSlot is not null)
            {
                _ = BorrowOwnedArmyReturnWorld(working, current, pool);
                current.BindArmyMapOccupancy(working);
                int owner = StoryAreaBusinessNeed(current.ReadForcePhysicalSlot(force), "selected_Force60");
                foreach (int physical in areas ?? throw StoryAreaBusinessMissing("saved_Area_list"))
                {
                    for (int index = AreaHexIterator.FindNextIndex(0, physical, current); index < 48400;
                        index = AreaHexIterator.FindNextIndex(index + 1, physical, current))
                    {
                        var hex = current.ReadHexTableReference(index) ?? throw StoryAreaBusinessMissing("Area_Hex");
                        //2C6D20 excludes an occupying Army; the complete185D00
                        //shared writer retains its own terrain/object gates.
                        if (unchecked((short)StoryAreaBusinessNeed(current.ReadHexRaw1C(hex), "Hex_Army1C")) is >= 1 and <= 500) continue;
                        int rawArea = StoryAreaBusinessNeed(current.ReadHexAreaSlot(hex), "Hex_Area12");
                        var area = current.ReadAreaTableReference(rawArea < 501 ? rawArea : 0)
                            ?? throw StoryAreaBusinessMissing("Hex_Area");
                        if (!StoryAreaBusinessNeed(current.ReadAreaNativeValidity(area), "Hex_Area_validity")) continue;
                        int rawCity = StoryAreaBusinessNeed(current.ReadAreaValidityCode(area), "Area_original_City34");
                        var city = current.ReadCityTableReference(rawCity < 52 ? rawCity : 0)
                            ?? throw StoryAreaBusinessMissing("original_City");
                        int rawOwner = StoryAreaBusinessNeed(current.ReadBaseForceSlot(city), "original_City_Force");
                        var cityForce = current.ReadForceTableReference(rawOwner < 52 ? rawOwner : 0)
                            ?? throw StoryAreaBusinessMissing("original_City_Force_pointer");
                        if (cityForce != force)
                        {
                            ushort rulerId = StoryAreaBusinessNeed(current.ReadConditionForceRulerPersonId(new(force.PhysicalSlot.Value)), "new_Force_ruler");
                            var ruler = (rulerId <= 6000 ? current.ReadPersonLookupReference(rulerId)
                                : current.ReadTransferPersonTableReference(0)) ?? throw StoryAreaBusinessMissing("new_ruler_pointer");
                            int person = StoryAreaBusinessNeed(ruler.PhysicalSlot, "new_ruler_receiver");
                            var home = BaseLookup.Find(StoryAreaBusinessNeed(current.ReadPersonFirstLocation(person), "new_ruler_HomeE2"), current);
                            city = CurrentGateMapQueries.FindCity(home, current);
                        }
                        working = TransferAbsorptionHexCurrent(working, current, hex, owner,
                            StoryAreaBusinessNeed(city.PhysicalSlot, "receiving_City"), enqueueEncirclement: false);
                    }
                }
                working = BaseCapturePublicationSource.Publish(working, current, pool, projectOwnedRoles: true);
            }
            return (RefreshEditedEventRegionStatistics(working, current), 1);
        }
        foreach (int physical in areas ?? throw StoryAreaBusinessMissing("saved_Area_list"))
        {
            var area = current.ReadAreaTableReference(physical) ?? throw StoryAreaBusinessMissing("Area_receiver");
            if (code == 202)
            {
                ushort before = StoryAreaBusinessNeed(current.ReadAssignmentArea36(physical), "assigned_Person36");
                if (before == 0) continue;
                if (unchecked((short)before) < 0) throw StoryAreaBusinessMissing("signed_assigned_Person");
                current.SetAssignmentArea36(physical, 0);
                var removed = PersonAssignmentPrecallSource.Apply2E01E0AreaRemoval(current, physical, before);
                if (removed.UnresolvedOwner is not null) throw StoryAreaBusinessMissing(removed.UnresolvedOwner);
                var writes = new[] { new PersonAssignmentPrecallWrite("Area", physical, 0x36, before, 0) };
                working = PersonAssignmentPrecallInputs.PublishDeathTouched(working, current,
                    removed.PersonPhysical ?? 0, [new(removed.NativeReturn, writes, [], null)],
                    publishVector: removed.PersonPhysical.HasValue && removed.CountAfter.HasValue);
                continue;
            }
            if (code is 193 or 194)
            {
                var old = working.Areas.GetValueOrDefault(physical) ?? throw StoryAreaBusinessMissing("Area_security_owner");
                int value = code == 193 ? arguments[0] : StoryAreaBusinessValue(
                    StoryAreaBusinessNeed(old.Security, "Area_security54"), arguments[0], arguments[1]);
                value = Math.Clamp(value, 0, 100);
                int owner = StoryAreaBusinessNeed(current.ReadAreaCurrentForceSlot(area), "security_Force");
                if (owner is >= 46 and <= 50) continue;
                working = working with { Areas = new(working.Areas) { [physical] = old with { Security = value } } };
                continue;
            }
            var field = code is 195 or 196 ? CeAreaDevelopmentField.Agriculture
                : code is 197 or 198 ? CeAreaDevelopmentField.Commerce : CeAreaDevelopmentField.Barracks;
            var writer = new OwnedAreaDevelopmentContext(working, current, _normalAftermathInputs);
            int requested = code % 2 != 0 ? Math.Max(arguments[0], 1)
                : Math.Clamp(StoryAreaBusinessValue(StoryAreaBusinessNeed(writer.ReadAreaProgress(area, field),
                    "Area_development"), arguments[0], arguments[1]), 0, 1000);
            _normalAftermathInputs.Development.WriteCurrentProgress(area, field, requested, 1, writer);
            working = writer.State;
        }
        //Only this one lazy current reader changes. No recapture or chain of
        //old closures is retained after several mutations in one invocation.
        current.ConditionAreaSecurityReader = physical => working.Areas.GetValueOrDefault(physical)?.Security is int value
            && value is >= 0 and <= 255 ? (byte)value : null;
        return (working, 1);
    }

    private static int StoryAreaBusinessValue(int before, int mode, int amount) => mode switch
    { 1 => amount, 2 => unchecked(before + amount), 3 => unchecked(before - amount), _ => before };
    private static T StoryAreaBusinessNeed<T>(T? value, string name) where T : struct =>
        value ?? throw StoryAreaBusinessMissing(name);
    private static InvalidDataException StoryAreaBusinessMissing(string name) => new("story.Area_business." + name);
}
