using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>Completes the new-force configuration against actual selected
    /// settings and current policy/Person owners, before normal campaign entry.</summary>
    public GameState FinalizeRegisteredOfficersCurrent(GameState state)
    {
        if (_scenario.InitialRegisteredOfficerSelection is not { NewForces.Length: > 0 } selection) return state;
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0)
            throw new InvalidDataException("Registered-officer configuration can only finish on the unused fresh campaign.");
        var forces = selection.NewForces.Select(choice => state.Cities[choice.CityId].ForceId ??
            throw new InvalidDataException("Configured registered-officer force is missing.")).ToHashSet();
        var world = CaptureOwnedArmyReturnWorld(state);
        var current = world.Context;
        var working = state;
        foreach (var person in state.Officers.Values.Where(person => person.ForceId is int force && forces.Contains(force) &&
                     person.Role is OfficerRole.Ruler or OfficerRole.DistrictLeader or OfficerRole.Governor or OfficerRole.Officer))
        {
            int physical = current.ResolveOfficerSource(person.Id) ?? throw new InvalidDataException("Configured Person source is missing.");
            int rulerId = state.Forces[person.ForceId!.Value].RulerId ?? throw new InvalidDataException("Configured ruler is missing.");
            int rulerPhysical = current.ResolveOfficerSource(rulerId) ?? throw new InvalidDataException("Configured ruler source is missing.");
            working = ApplyRecruitInitialLoyaltyCurrent(working, current, physical, new(rulerPhysical));
        }
        //The real configuration recomputes loyalty before choosing governors.
        //Capacity consumes the completed selected settings and policy effects.
        _baseCapacityInputs.BindCurrent(working, current);
        foreach (int location in state.Cities.Values.Where(city => city.ForceId is int force && forces.Contains(force)).Select(city => city.Id)
                     .Concat(state.Gates.Values.Where(gate => gate.ForceId is int force && forces.Contains(force)).Select(gate => gate.Id)))
        {
            var target = BaseLookup.Find(location, current);
            var capacities = BasePostLoad.ReadCapacities(target, current);
            int objectSlot = current.ReadBaseObjectLink(target) ?? throw new InvalidDataException("Configured base object is missing.");
            current.WriteObjectPostLoadField14(new(objectSlot), capacities.Field46);
            ushort stored = current.ReadObjectRawField14(new(objectSlot)) ?? throw new InvalidDataException("Configured base durability is missing.");
            working = PublishCurrentObjectDurability(working, current, new(objectSlot), stored);
            byte tier = BasePostLoad.ReadFlag66(target, current);
            current.SetBasePostLoadFlag66(target, tier);
            working = location <= 51
                ? working with { Cities = new(working.Cities) { [location] = working.Cities[location] with { DamageTier = tier } } }
                : working with { Gates = new(working.Gates) { [location] = working.Gates[location] with { DamageTier = tier } } };
            current.SetBaseResource(target, BaseResourceKind.Troops, Math.Clamp(location <= 51 ? 10000 : 2000, 0, capacities.Troops));
            var queries = new PersonReturnGovernorQueries(working, _governorInputs, current);
            var resolution = ArmyReturnGovernorUpdater.PrepareCurrent(target, current, queries);
            var packet = resolution.Packet ?? throw new InvalidDataException(resolution.UnresolvedOwner ?? "Configured governor is unavailable.");
            ArmyReturnGovernorUpdater.ApplyCurrentWrites(current, packet);
        }
        return OwnedArmyLifecycleDetachedInputs.PublishCurrent(OwnedArmyReturnCurrentFields.PublishCurrent(working, current), current);
    }
}
