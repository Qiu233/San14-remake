using San14.Core;

namespace San14.Data;

public sealed partial class ArmyOperationContext
{
    /// <summary>The actual pre-start normal-load owner. A saved/started world
    /// never substitutes retained opening records for missing current fields.</summary>
    internal static bool IsNewGamePreview(GameState state) => state.Phase == TurnPhase.Initializing &&
        state.NativeClock is null && state.NativeArmySimulation is null;

    internal SourceWorldContext? CaptureNewGamePreview(GameState state)
    {
        if (!IsNewGamePreview(state)) return null;
        //Capture validates the selected content/map identity and overlays the
        //preview's real Force/District/map/portrait owners. Its retained normal
        //Person read is still the authoritative load owner at this stage.
        var current = Capture(state);
        if (current is null) return null;
        //The normal initializer intentionally projects source zero IDs as
        //nullable domain IDs. Recover only those documented known-zero load
        //operands here, never an absent current value in a started/old world.
        foreach (var row in _source.Snapshot.Forces)
            if (row.Slot != 0 && row.RulerSlot == 0 &&
                state.Forces.TryGetValue(row.Slot, out var force) && force.RulerId is null)
                current.SetOwnedForceRulerSlot10(row.Slot, 0);
        foreach (var row in _source.Snapshot.Characters)
        {
            if (row.CharacterId == 0 || !state.Officers.TryGetValue(row.CharacterId, out var person)) continue;
            if (row.CitySlot == 0 && person.LocationId is null) current.SetPersonFirstLocation(row.Slot, 0);
            if (row.DistrictSlot == 0 && person.DistrictId is null) current.SetPersonDistrictSlot(row.Slot, 0);
        }
        //Quantities already have direct preview owners, independently of
        //the later owned return/status/trait bank. Do not bind that absent bank.
        foreach (var city in state.Cities.Values)
        {
            var target = new BaseReference(BaseKind.City, city.Id);
            current.SetBaseResource(target, BaseResourceKind.Gold, city.Gold);
            current.SetBaseResource(target, BaseResourceKind.Supplies, city.Supplies);
            current.SetBaseResource(target, BaseResourceKind.Troops, city.Troops);
        }
        foreach (var gate in state.Gates.Values)
            current.SetBaseResource(new(BaseKind.Gate, gate.Id - 51), BaseResourceKind.Troops, gate.Troops);
        var pool = current.ReadOwnedCurrentListPool() ?? new WorldIndexContext(current);
        foreach (var (kind, count) in new[] { (RegistryKind.Force, 52), (RegistryKind.Person, 1851) })
        {
            current.SetRegistryDescriptor(kind, new(pool.AllocateDescriptor().Identity));
            for (int slot = 0; slot < count; slot++)
                _ = ObjectRegistration.Register(current.ReadRegistryTableReference(kind, slot), current);
        }
        return current;
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    //Shared by force-page previews and the existing batch portrait consumer.
    //Only the actual pre-start load uses retained normal Person source fields.
    private SourceWorldContext CapturePersonPresentationCurrent(GameState state) =>
        ArmyOperationContext.IsNewGamePreview(state)
            ? _definitions.CaptureNewGamePreview(state) ?? throw new InvalidDataException("new-game.preview.source-owner")
            : OwnedArmyReturnCurrentFields.CaptureCurrent(state, _definitions);
}
