using San14.Core;

namespace San14.Data;

/// <summary>Shared operation-local Area progress and Force level-change writer.</summary>
internal sealed class OwnedAreaDevelopmentContext(GameState state, SourceWorldContext current,
    NormalArmyAftermathInputs inputs) : ICeAreaDevelopmentWriteContext
{
    public GameState State { get; private set; } = state;
    public ushort? ReadAreaProgress(AreaReference area, CeAreaDevelopmentField field)
    {
        var values = State.Areas.GetValueOrDefault(area.PhysicalSlot!.Value)?.Development;
        int? value = field switch { CeAreaDevelopmentField.Commerce => values?.Commerce,
            CeAreaDevelopmentField.Agriculture => values?.Agriculture, _ => values?.Barracks };
        return value is >= 0 and <= 65535 ? (ushort)value : null;
    }
    public void WriteAreaProgress(AreaReference area, CeAreaDevelopmentField field, ushort value)
    {
        var old = State.Areas[area.PhysicalSlot!.Value]; var development = old.Development!;
        var after = field switch { CeAreaDevelopmentField.Commerce => development with { Commerce = value },
            CeAreaDevelopmentField.Agriculture => development with { Agriculture = value }, _ => development with { Barracks = value } };
        State = State with { Areas = new(State.Areas) { [old.Id] = old with { Development = after } } };
    }
    public ushort? ReadAreaRepresentativeHexIndex(AreaReference area) => current.ReadAreaHexIndex(area);
    public HexReference? ReadHexTableReference(int slot) => current.ReadHexTableReference(slot);
    public byte? ReadHexForceSlot(HexReference hex) => current.ReadHexForceSlot(hex);
    public ForceReference? ReadForceTableReference(int slot) => current.ReadForceTableReference(slot);
    public bool? ReadForceNativeValidity(ForceReference force) => current.ReadForceNativeValidity(force);
    public int? ReadForcePhysicalIdentity(ForceReference force) => current.ReadForcePhysicalSlot(force);
    public short? ReadForceDevelopmentWord1BE(ForceReference force) =>
        force.PhysicalSlot is int slot ? State.NativeArmySimulation!.Forces.GetValueOrDefault(slot)?.NormalAftermath?.Development1BE : null;
    public int? ReadCurrentDevelopmentLevelChangeCoefficient() => inputs.ReadCurrent(State, SimulationParameterKeys.NormalAftermath.AreaDevelopmentLevelChangeCoefficient);
    public void WriteForceDevelopmentWord1BE(ForceReference force, short value)
    {
        var simulation = State.NativeArmySimulation!; int slot = force.PhysicalSlot!.Value; var old = simulation.Forces[slot];
        State = State with { NativeArmySimulation = simulation with { Forces = new(simulation.Forces)
            { [slot] = old with { NormalAftermath = old.NormalAftermath! with { Development1BE = value } } } } };
    }
}
