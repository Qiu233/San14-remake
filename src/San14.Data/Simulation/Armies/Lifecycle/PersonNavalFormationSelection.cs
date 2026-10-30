using San14.Core;

namespace San14.Data;

internal interface IPersonNavalFormationInputs
{
    int PersonIdentity { get; }
    ushort FirstLocation { get; }
    int ForceIdentity { get; }
    bool ForceValid { get; }
    byte ForceFormation { get; }
}

/// <summary>The ordinary Person naval default. This selects a stored byte;
/// the Army setter's independent requested-formation admission is not applied
/// again to the returned value.</summary>
internal static class PersonNavalFormationSelection
{
    internal static byte Resolve(IPersonNavalFormationInputs inputs)
    {
        if (BattleRemakeCurrentRegionFactor.IsNativeBandPerson(inputs.PersonIdentity) &&
            inputs.FirstLocation is >= 1 and <= 61 && inputs.ForceIdentity is not (>= 46 and <= 50))
            return 11;
        return inputs.ForceValid ? inputs.ForceFormation : (byte)11;
    }

    internal static byte Resolve(GameState state, SourceWorldContext current, int physical)
        => Resolve(new CurrentInputs(state, current, physical));

    private sealed class CurrentInputs(GameState state, SourceWorldContext current, int physical)
        : IPersonNavalFormationInputs
    {
        public int PersonIdentity => Known(current.ReadPersonIdentity(physical), "person_identity");
        public ushort FirstLocation => Known(current.ReadPersonFirstLocation(physical), "person_first_location");
        private ForceReference Force => current.ReadPersonForce(physical) ?? throw Missing("person_force_pointer");
        public int ForceIdentity => Known(current.ReadForcePhysicalSlot(Force), "force_identity");
        public bool ForceValid => Known(current.ReadForceNativeValidity(Force), "force_validity");
        public byte ForceFormation
        {
            get
            {
                int force = ForceIdentity;
                var parameters = state.NativeArmySimulation?.Parameters ?? throw Missing("force_formation_owner");
                string key = BattleRemakeCurrentForcePolicies.ForceFieldKey(force, 0x15E);
                if (parameters.Float32Values.ContainsKey(key) ||
                    !parameters.Int32Values.TryGetValue(key, out int value) || (uint)value > byte.MaxValue)
                    throw Missing("force_formation_current_byte");
                return (byte)value;
            }
        }
    }

    private static T Known<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("formation.person_naval." + field);
}
