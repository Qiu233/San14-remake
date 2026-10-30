using San14.Core;
using San14.Data.Formats;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    internal (GameState State, int NativeReturn) ApplyStoryCityListResourcesCurrent(
        GameState state, SourceWorldContext current, int code, IReadOnlyList<int> arguments,
        IReadOnlyList<S14EventConditionCityPointer> cities, BaseReference? target)
    {
        if (arguments.Count != 8) throw StoryResourceMissing("City_list_operands");
        _baseCapacityInputs.BindCurrent(state, current);
        var operation = new StoryBaseResourcesCurrent(state, current);
        operation.ExecuteCityList(code, arguments, cities, target);
        return (operation.State, 1);
    }

    private sealed partial class StoryBaseResourcesCurrent
    {
        internal void ExecuteCityList(int code, IReadOnlyList<int> a,
            IReadOnlyList<S14EventConditionCityPointer> cities, BaseReference? target)
        {
            if (code is not (209 or 210 or 305 or 306 or 307 or 308 or 309 or 310 or 312 or 383))
                throw StoryResourceMissing("City_list_opcode");
            if (code is 308 or 309 or 310)
            {
                var to = target ?? throw StoryResourceMissing("City_list_transfer_target");
                if (to.Kind != BaseKind.City) throw StoryResourceMissing("City_list_transfer_kind");
                if (!Valid(to)) return;
                foreach (var pointer in cities)
                {
                    var from = new BaseReference(BaseKind.City, pointer.PhysicalSlot);
                    if (!Valid(from)) continue;
                    // Source ordering is add-to-target THEN clear-source.
                    // A target which also occurs in the list clears itself.
                    if (code == 310)
                    {
                        SetTroops(to, Bound(unchecked(Troops(to) + Troops(from)), TroopCapacity(to)));
                        WriteResource(from, BaseResourceKind.Troops, Math.Min(0, TroopCapacity(from)));
                    }
                    else
                    {
                        var kind = code == 308 ? BaseResourceKind.Gold : BaseResourceKind.Supplies;
                        Add(to, kind, Currency(from, kind));
                        SetCurrency(from, kind, 0);
                    }
                }
                return;
            }
            float rate = a[0] switch { 1 => (float)a[1], 2 => (float)a[1] + 100f,
                3 => 100f - (float)a[1], _ => 100f };
            foreach (var pointer in cities)
            {
                var city = new BaseReference(BaseKind.City, pointer.PhysicalSlot);
                //209/210 deliberately have no outer validity gate.
                if (code is not (209 or 210) && !Valid(city)) continue;
                switch (code)
                {
                    case 209:
                        int troops = Troops(city);
                        SetTroops(city, ModeValue(a[0], troops, a[1]));
                        break;
                    case 210:
                        int morale = Need(current.ReadBaseMorale44(city), "City_list_morale");
                        int rawForce = Need(current.ReadBaseForceSlot(city), "City_list_morale_Force");
                        var force = current.ReadForceTableReference(rawForce < 52 ? rawForce : 0)
                            ?? throw StoryResourceMissing("City_list_morale_Force_pointer");
                        int requested = Bound(ModeValue(a[0], morale, a[1]), BasePostLoad.ReadMoraleUpper(force, current));
                        if (!BasePostLoad.WriteMoraleCurrent(city, requested, current)) break;
                        byte after = Need(current.ReadBaseMorale44(city), "City_list_morale_after");
                        int physical = Need(city.PhysicalSlot, "City_list_morale_pointer");
                        var owner = State.Cities.GetValueOrDefault(physical) ?? throw StoryResourceMissing("City_list_morale_owner");
                        var runtime = owner.NativeReturnRuntime ?? throw StoryResourceMissing("City_list_morale_runtime");
                        State = State with { Cities = new(State.Cities) { [physical] = owner with
                            { Morale = after, NativeReturnRuntime = runtime with { Morale44 = after } } } };
                        break;
                    case 305:
                    case 307:
                        var kind = code == 307 ? BaseResourceKind.Gold : BaseResourceKind.Supplies;
                        SetCurrency(city, kind, Scaled(Currency(city, kind), rate));
                        break;
                    case 306:
                        SetTroops(city, Scaled(Troops(city), rate));
                        break;
                    case 312:
                        SetCurrency(city, BaseResourceKind.Gold, Math.Max(Currency(city, BaseResourceKind.Gold), 5000));
                        SetCurrency(city, BaseResourceKind.Supplies, Math.Max(Currency(city, BaseResourceKind.Supplies), 15000));
                        SetTroops(city, Math.Max(Troops(city), 9000));
                        break;
                    case 383:
                        int endurance = unchecked(BasePostLoad.ReadCapacities(city, current).Field46 * a[0]) / 100;
                        int previous = BasePostLoad.ReadLinkedField14(city, current);
                        if (a[1] == 0 || previous <= endurance) SetEndurance(city, endurance, false);
                        //Even when only-increase prevented the write, source
                        //refreshes the actual linked receiver's damage tier.
                        RefreshDamageTier(city);
                        break;
                }
            }
        }
        private static int ModeValue(int mode, int before, int operand) => mode switch
            { 1 => operand, 2 => unchecked(before + operand), 3 => unchecked(before - operand), _ => before };
        private static int Scaled(int before, float rate)
        {
            float value = (float)before * rate / 100f;
            //Native signed float32 conversion produces INT_MIN outside range.
            return !float.IsFinite(value) || value < int.MinValue || value >= 2147483648f ? int.MinValue : (int)value;
        }
    }
}
