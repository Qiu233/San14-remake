using San14.Core;
namespace San14.Data;

public sealed record OriginalBaseCapacities(int Gold, int Supplies, int Troops, int Field46);
public sealed record OriginalBaseResourceCapacities(int Gold, int Supplies, int Troops);

public interface IOriginalBasePostLoadState
{
    bool? ReadBaseNativeValidity(OriginalBaseReference reference);
    int? ReadBaseResource(OriginalBaseReference reference, OriginalBaseResourceKind kind);
    void SetBaseResource(OriginalBaseReference reference, OriginalBaseResourceKind kind, int? value);
    byte? ReadBaseMorale44(OriginalBaseReference reference);
    void SetBaseMorale44(OriginalBaseReference reference, byte? value);
    ushort? ReadBaseObjectLink(OriginalBaseReference reference);
    OriginalObjectReference? ReadObjectTableReference(int slot);
    bool? ReadObjectNativeValidity(OriginalObjectReference reference);
    ushort? ReadObjectRawField14(OriginalObjectReference reference);
    void WriteObjectPostLoadField14(OriginalObjectReference reference, int value);
    int? ReadBaseForceSlot(OriginalBaseReference reference);
    OriginalForceReference? ReadForceTableReference(int slot);
    bool? ReadForceNativeValidity(OriginalForceReference reference);
    int? ReadForcePhysicalSlot(OriginalForceReference reference);
    byte? ReadCitySizeB8(OriginalBaseReference reference);
    ushort? ReadBaseField46(OriginalBaseReference reference);
    bool ReadSpecialBaseCapacity(OriginalBaseReference reference);
    int? ReadBasePostLoadParameter(string key);
    int? ReadBaseRuntimeTroopsExtra();
    int? ReadBaseForcePolicyEffect(OriginalForceReference force, int effect);
    int? ReadBaseForceModifier(OriginalForceReference force, int modifier);
    bool? ReadBaseCapacityPercentageEnabled();
    int? ReadBaseCapacityPercentage();
    void SetBasePostLoadFlag66(OriginalBaseReference reference, byte? value);
}

/// <summary>Complete direct business flow of city2243E0 and gate224750,
/// including their shared capacity, morale and object-field normalization.
/// Runtime settings and force effects remain explicit current queries.</summary>
public static class OriginalBasePostLoad
{
    /// <summary>The three resource outputs of268230. The independent fourth
    /// output's policy32/settings queries cannot change these outputs and are
    /// not inputs to this dependency projection.</summary>
    public static OriginalBaseResourceCapacities ReadResourceCapacities(OriginalBaseReference reference,
        IOriginalBasePostLoadState state)
    {
        if (!Known(state.ReadBaseNativeValidity(reference), "capacity base validity")) return new(0, 0, 0);
        ushort link = Known(state.ReadBaseObjectLink(reference), "capacity object4E");
        if (link <= 51)
        {
            if (state.ReadSpecialBaseCapacity(reference))
                return new(P(state, SimulationParameterKeys.BaseCapacity.SpecialGoldCapacity), P(state, SimulationParameterKeys.BaseCapacity.SpecialFoodCapacity), P(state, SimulationParameterKeys.BaseCapacity.SpecialTroopCapacity));
            byte size = Known(state.ReadCitySizeB8(reference), "city sizeB8");
            int extra = size switch { 2 => P(state, SimulationParameterKeys.BaseCapacity.MediumCityExtraTroops), 3 => P(state, SimulationParameterKeys.BaseCapacity.LargeCityExtraTroops),
                4 => P(state, SimulationParameterKeys.BaseCapacity.MetropolisExtraTroops), _ => Known(state.ReadBaseRuntimeTroopsExtra(), "runtime troops extra27D4890") };
            return new(unchecked(P(state, SimulationParameterKeys.BaseCapacity.GoldCapacityPerCitySize) * size), unchecked(P(state, SimulationParameterKeys.BaseCapacity.FoodCapacityPerCitySize) * size),
                unchecked(P(state, SimulationParameterKeys.BaseCapacity.BaseTroopCapacity) + extra));
        }
        int troops = unchecked(P(state, SimulationParameterKeys.BaseCapacity.BaseTroopCapacity) + Known(state.ReadBaseRuntimeTroopsExtra(), "runtime troops extra27D4890"));
        OriginalForceReference force = Force(reference, state);
        if (Known(state.ReadForceNativeValidity(force), "capacity fresh force validity"))
            troops = unchecked(troops + Known(state.ReadBaseForceModifier(force, 56), "force modifier56"));
        return new(0, 0, troops);
    }
    public static void Apply(OriginalBaseReference reference, IOriginalBasePostLoadState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!Known(state.ReadBaseNativeValidity(reference), "base validity")) return;
        if (reference.Kind == OriginalBaseKind.City)
        {
            foreach (OriginalBaseResourceKind kind in new[] { OriginalBaseResourceKind.Gold, OriginalBaseResourceKind.Supplies })
            {
                int saved = Known(state.ReadBaseResource(reference, kind), "base resource input");
                OriginalBaseCapacities capacities = ReadCapacities(reference, state);
                int upper = kind == OriginalBaseResourceKind.Gold ? capacities.Gold : capacities.Supplies;
                state.SetBaseResource(reference, kind, SignedBound(saved, upper));
            }
        }
        else if (reference.Kind != OriginalBaseKind.Gate) throw new InvalidDataException("Unknown base slot7 receiver kind.");
        int savedTroops = Known(state.ReadBaseResource(reference, OriginalBaseResourceKind.Troops), "base troops3C");
        int troopsUpper = ReadCapacities(reference, state).Troops;
        int boundedTroops = SignedBound(savedTroops, troopsUpper);
        byte savedMorale = Known(state.ReadBaseMorale44(reference), "base morale44");
        state.SetBaseResource(reference, OriginalBaseResourceKind.Troops, boundedTroops);
        WriteMorale(reference, savedMorale, state);
        int savedRaw14 = ReadLinkedField14(reference, state);
        WriteLinkedField14(reference, savedRaw14, state);
        state.SetBasePostLoadFlag66(reference, ReadFlag66(reference, state));
    }

    public static OriginalBaseCapacities ReadCapacities(OriginalBaseReference reference, IOriginalBasePostLoadState state)
    {
        if (!Known(state.ReadBaseNativeValidity(reference), "capacity base validity")) return new(0, 0, 0, 0);
        OriginalForceReference savedForce = Force(reference, state);
        int gold = 0, supplies = 0, troops, fourth;
        ushort link = Known(state.ReadBaseObjectLink(reference), "capacity object4E");
        if (link <= 51)
        {
            if (state.ReadSpecialBaseCapacity(reference)) return new(P(state, SimulationParameterKeys.BaseCapacity.SpecialGoldCapacity), P(state, SimulationParameterKeys.BaseCapacity.SpecialFoodCapacity), P(state, SimulationParameterKeys.BaseCapacity.SpecialTroopCapacity), P(state, SimulationParameterKeys.BaseCapacity.MaximumDurability));
            byte size = Known(state.ReadCitySizeB8(reference), "city sizeB8");
            gold = unchecked(P(state, SimulationParameterKeys.BaseCapacity.GoldCapacityPerCitySize) * size);
            supplies = unchecked(P(state, SimulationParameterKeys.BaseCapacity.FoodCapacityPerCitySize) * size);
            int troopBase = P(state, SimulationParameterKeys.BaseCapacity.BaseTroopCapacity);
            //TC268230 replaces the runtime candidate with a source global
            //for sizes2..4 before use; its value contributes only for size1
            //or an unrecognized size. It is a scalar, not a query callback.
            int extra = size switch
            {
                2 => P(state, SimulationParameterKeys.BaseCapacity.MediumCityExtraTroops),
                3 => P(state, SimulationParameterKeys.BaseCapacity.LargeCityExtraTroops),
                4 => P(state, SimulationParameterKeys.BaseCapacity.MetropolisExtraTroops),
                _ => Known(state.ReadBaseRuntimeTroopsExtra(), "runtime troops extra27D4890")
            };
            troops = unchecked(troopBase + extra);
            int basis = P(state, SimulationParameterKeys.BaseCapacity.BaseDurability), perSize = P(state, SimulationParameterKeys.BaseCapacity.DurabilityPerAdditionalSize);
            int effect = Known(state.ReadBaseForcePolicyEffect(savedForce, 32), "force policy effect32");
            fourth = unchecked(basis + (size - 1) * perSize + effect * P(state, SimulationParameterKeys.BaseCapacity.DurabilityPolicyScale));
        }
        else
        {
            troops = unchecked(P(state, SimulationParameterKeys.BaseCapacity.BaseTroopCapacity) + Known(state.ReadBaseRuntimeTroopsExtra(), "runtime troops extra27D4890"));
            OriginalForceReference freshForce = Force(reference, state);
            if (Known(state.ReadForceNativeValidity(freshForce), "capacity fresh force validity"))
                troops = unchecked(troops + Known(state.ReadBaseForceModifier(freshForce, 56), "force modifier56"));
            fourth = Known(state.ReadBaseField46(reference), "base field46");
        }
        if (Known(state.ReadBaseCapacityPercentageEnabled(), "current capacity settings gate"))
            fourth = unchecked(Known(state.ReadBaseCapacityPercentage(), "fresh capacity settings percentage") * fourth) / 100;
        return new(gold, supplies, troops, Math.Min(fourth, 9999));
    }

    public static void WriteMorale(OriginalBaseReference reference, int input, IOriginalBasePostLoadState state)
    {
        OriginalForceReference first = Force(reference, state);
        OriginalForceReference second = Force(reference, state);
        int physical = Known(state.ReadForcePhysicalSlot(second), "morale second force physical slot");
        if (unchecked((uint)(physical - 46)) <= 4) return;
        int upper = ReadMoraleUpper(first, state);
        state.SetBaseMorale44(reference, unchecked((byte)SignedBound(input, upper)));
    }
    public static int ReadMoraleUpper(OriginalForceReference force, IOriginalBasePostLoadState state)
    {
        if (!Known(state.ReadForceNativeValidity(force), "morale force validity")) return P(state, SimulationParameterKeys.Morale.Maximum);
        int factor = P(state, SimulationParameterKeys.Morale.PolicyScale);
        int physical = Known(state.ReadForcePhysicalSlot(force), "morale first force physical slot");
        int effect = 0;
        if (unchecked((uint)(physical - 46)) > 4 && Known(state.ReadForcePhysicalSlot(force), "morale repeated force physical slot") != 51)
            effect = Known(state.ReadBaseForcePolicyEffect(force, 10), "force morale policy effect10");
        int upper = unchecked((effect * factor + 100) * P(state, SimulationParameterKeys.Morale.Maximum)) / 100;
        return unchecked(upper + Known(state.ReadBaseForceModifier(force, 71), "force morale modifier71"));
    }
    public static int ReadLinkedField14(OriginalBaseReference reference, IOriginalBasePostLoadState state)
    {
        OriginalObjectReference linked = LinkedObject(reference, state);
        return Known(state.ReadObjectNativeValidity(linked), "linked object validity")
            ? Known(state.ReadObjectRawField14(linked), "linked object field14") : 6000;
    }
    public static void WriteLinkedField14(OriginalBaseReference reference, int input, IOriginalBasePostLoadState state)
    {
        OriginalObjectReference linked = LinkedObject(reference, state);
        if (!Known(state.ReadObjectNativeValidity(linked), "fresh linked object validity")) return;
        int upper = ReadCapacities(reference, state).Field46;
        state.WriteObjectPostLoadField14(linked, SignedBound(input, upper));
    }
    public static byte ReadFlag66(OriginalBaseReference reference, IOriginalBasePostLoadState state)
    {
        if (!Known(state.ReadBaseNativeValidity(reference), "flag base validity")) return 0;
        int saved = ReadLinkedField14(reference, state);
        int upper = ReadCapacities(reference, state).Field46;
        float ratio = (float)saved / (float)upper;
        float percent = ratio * BitConverter.Int32BitsToSingle(P(state, SimulationParameterKeys.Shared.PercentDenominator));
        float ceiling = MathF.Ceiling(percent);
        int n = !float.IsFinite(ceiling) || ceiling < int.MinValue || ceiling >= 2147483648f ? int.MinValue : (int)ceiling;
        int high = P(state, SimulationParameterKeys.BaseCapacity.DamageTierFirstThreshold), middle = P(state, SimulationParameterKeys.BaseCapacity.DamageTierSecondThreshold), low = P(state, SimulationParameterKeys.BaseCapacity.DamageTierThirdThreshold);
        if (n > high && n <= 100) return 0;
        if (n > middle && n <= high) return 1;
        if (n > low && n <= middle) return 2;
        return (byte)(n >= 0 && n <= low ? 3 : 0);
    }
    private static OriginalObjectReference LinkedObject(OriginalBaseReference reference, IOriginalBasePostLoadState state)
    {
        ushort link = Known(state.ReadBaseObjectLink(reference), "current base object4E");
        return state.ReadObjectTableReference(link <= 3000 ? link : 0)
            ?? throw new InvalidDataException("Current linked object pointer is unknown.");
    }
    private static OriginalForceReference Force(OriginalBaseReference reference, IOriginalBasePostLoadState state)
    {
        int raw = Known(state.ReadBaseForceSlot(reference), "base raw force slot22");
        if (raw < 0) throw new InvalidDataException("Negative force index is unsafe in the original table lookup.");
        return state.ReadForceTableReference(raw <= 51 ? raw : 0)
            ?? throw new InvalidDataException("Current base force pointer is unknown.");
    }
    private static int SignedBound(int input, int upper) => input < 0 ? 0 : Math.Min(input, upper);
    private static int P(IOriginalBasePostLoadState state, string key) => Known(state.ReadBasePostLoadParameter(key), $"base parameter{key}");
    private static T Known<T>(T? value, string field) where T : struct
        => value ?? throw new InvalidDataException($"Current {field} is unknown.");
}
