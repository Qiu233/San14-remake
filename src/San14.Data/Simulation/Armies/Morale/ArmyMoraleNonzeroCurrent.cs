using San14.Core;

namespace San14.Data;

public sealed record ArmyMoraleCurrentResult(int? NativeReturn,
    ArmyMoraleAdjustmentReceipt? Receipt, string? UnresolvedOwner);

/// <summary>CE2709A0's actual current-context prefix through its nonzero
/// routing return. A zero route has already performed241720's byte store;
/// its enclosing operation must discard the current world until that
/// independent routing callback is implemented.</summary>
public static class ArmyMoraleNonzeroCurrent
{
    public const string Revision = "san14:ce-2709A0-current-nonzero-20261007";
    public static ArmyMoraleCurrentResult Apply(GameState state,
        SourceWorldContext current, ArmyUnitReference army, int adjustment)
    {
        try
        {
            if (!Known(current.ReadArmyUnitNativeValidity(army), "Army_validity")) return new(0, null, null);
            var fields = current.ReadArmyReturnResourceFields(army) ?? throw Missing("Army_resource_fields");
            byte raw = Known(fields.Morale1A, "Army_raw1A");
            int? trait = null, scale = null;
            if (adjustment < 0)
            {
                ushort commander = Known(current.ReadArmyUnitCommanderId(army), "Army_commander12");
                var person = current.ReadPersonLookupReference(commander) ?? throw Missing("Commander_pointer");
                int physical = Known(person.PhysicalSlot, "Commander_physical");
                //2709FE stack arg5=0,270A02 R9D=0; selector0 includes all
                //cached36 plus current nine active/permanent commander slots.
                var values = TacticRemakeCurrentTraits.ArmyContributions(current, physical,
                    current.ReadArmyReturnTraitFields(army)?.Traits64) ?? throw Missing("Army_26A740_mode0_0");
                trait = values[13];
                if (trait != 0)
                {
                    var parameters = state.NativeArmySimulation?.Parameters ?? throw Missing("current_parameters");
                    string key = SimulationParameterKeys.Morale.NegativeAdjustmentScale; //CE TC1A49FE0, i32 IMUL.
                    if (parameters.Float32Values.ContainsKey(key) || !parameters.Int32Values.TryGetValue(key, out int number))
                        throw Missing("current_scale1A49FE0");
                    scale = number;
                }
            }
            //22EB30 reads the current commander's District→Force separately
            //twice;241720 obtains its own fresh cap through the same getter.
            int getter = BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(army), current);
            int clamp = BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(army), current);
            int setter = BasePostLoad.ReadMoraleUpper(current.ReadArmyUnitForceReference(army), current);
            var receipt = ArmyMoraleAdjustmentSource.Calculate(raw, adjustment, trait, scale,
                getter, clamp, setter, Revision) ?? throw Missing("2709A0_scalar_inputs");
            current.SetArmyReturnResourceFields(Known(army.PhysicalSlot, "Army_physical"), fields with { Morale1A = receipt.After1A });
            return receipt.RoutingTestValue != 0 ? new(0, receipt, null)
                : new(null, receipt, "branch.2709A0_zero_morale_routing");
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
    }
    private static T Known<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("Current2709A0 owner is unknown: " + field);
}
