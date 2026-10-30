using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The ordinary Story resource family, at its real business
    /// boundary. Selectors belong to the same invocation; this updates only
    /// the reached Base/linked Object and its actual semantic owner.</summary>
    internal (GameState State, int NativeReturn) ApplyStoryBaseResourcesCurrent(GameState state,
        SourceWorldContext current, int code, IReadOnlyList<int> arguments,
        BaseReference first, BaseReference? second = null)
    {
        if (arguments.Count != 8) throw StoryResourceMissing("arguments");
        _baseCapacityInputs.BindCurrent(state, current);
        var operation = new StoryBaseResourcesCurrent(state, current);
        int result = operation.Execute(code, arguments, first, second);
        return (operation.State, result);
    }

    private sealed partial class StoryBaseResourcesCurrent(GameState state, SourceWorldContext current)
    {
        internal GameState State { get; private set; } = state;

        internal int Execute(int code, IReadOnlyList<int> a, BaseReference first, BaseReference? second)
        {
            switch (code)
            {
                case 142:
                    if (Valid(first)) Add(first, BaseResourceKind.Gold, a[1]);
                    return 1;
                case 143: // This ordinary handler has no outer Base validity gate.
                    Add(first, BaseResourceKind.Supplies, a[1]); return 1;
                case 144: // Likewise: use the actual raw3C, not a force admission.
                    SetTroops(first, Bound(unchecked(Troops(first) + a[1]), TroopCapacity(first)));
                    return 1;
                case 145:
                    if (Valid(first)) SetEndurance(first, a[1], refreshDamageTier: true);
                    return 1;
                case 146:
                case 147:
                case 148:
                    var target = second ?? throw StoryResourceMissing("transfer_target");
                    if (!Valid(first) || !Valid(target)) return 1;
                    if (code == 148)
                    {
                        int moved = Troops(first);
                        // The source computes min(0, capacity) before storing.
                        WriteResource(first, BaseResourceKind.Troops, Math.Min(0, TroopCapacity(first)));
                        SetTroops(target, Bound(unchecked(Troops(target) + moved), TroopCapacity(target)));
                    }
                    else
                    {
                        var kind = code == 146 ? BaseResourceKind.Gold : BaseResourceKind.Supplies;
                        int moved = Currency(first, kind);
                        SetCurrency(first, kind, 0);
                        Add(target, kind, moved);
                    }
                    return 1;
                case 273:
                case 274:
                    if (Valid(first))
                    {
                        var kind = code == 273 ? BaseResourceKind.Gold : BaseResourceKind.Supplies;
                        SetCurrency(first, kind, Currency(first, kind) / 2);
                    }
                    return 1;
                case 275:
                    if (Valid(first)) SetTroops(first, Troops(first) / 2);
                    return 1;
                case 276:
                    // Unlike145, this source body does not refresh Base66.
                    if (Valid(first)) SetEndurance(first, BasePostLoad.ReadLinkedField14(first, current) / 2, false);
                    return 1;
                case 277:
                case 278:
                    if (Valid(first))
                    {
                        var kind = code == 277 ? BaseResourceKind.Gold : BaseResourceKind.Supplies;
                        SetCurrency(first, kind, Bound(a[1], CurrencyCapacity(first, kind)));
                    }
                    return 1;
                case 279:
                    if (Valid(first)) SetTroops(first, a[1]);
                    return 1;
                case 346:
                    if (!Valid(first)) return 0;
                    float rate = a[1] switch { 1 => (float)a[2], 2 => (float)a[2] + 100f,
                        3 => 100f - (float)a[2], _ => 100f };
                    float scaled = (float)Troops(first) * rate / 100f;
                    int requested = !float.IsFinite(scaled) || scaled < int.MinValue || scaled >= 2147483648f
                        ? int.MinValue : (int)scaled;
                    SetTroops(first, Bound(requested, TroopCapacity(first)));
                    return 1;
                default: throw StoryResourceMissing("opcode");
            }
        }

        private bool Valid(BaseReference receiver) => Need(current.ReadBaseNativeValidity(receiver), "Base_validity");
        private int Troops(BaseReference receiver) => Need(current.ReadBaseResource(receiver, BaseResourceKind.Troops), "Base_troops");
        private int TroopCapacity(BaseReference receiver) => BasePostLoad.ReadTroopCapacity(receiver, current);
        private int Currency(BaseReference receiver, BaseResourceKind kind) =>
            ReadDiplomacyBaseResourceCurrent(current, receiver, kind);
        private int CurrencyCapacity(BaseReference receiver, BaseResourceKind kind)
        {
            var city = BaseCurrencyAdditionSource.CurrencyCity(receiver, current);
            return kind == BaseResourceKind.Gold ? BasePostLoad.ReadGoldCapacity(city, current)
                : BasePostLoad.ReadSuppliesCapacity(city, current);
        }
        private void Add(BaseReference receiver, BaseResourceKind kind, int amount)
        {
            // Base virtual capacities/getters/setters each route a Gate to
            // its current City. These are not separate Gate money/food stocks.
            int cap = CurrencyCapacity(receiver, kind);
            int requested = Bound(unchecked(Currency(receiver, kind) + amount), cap);
            SetCurrency(receiver, kind, requested);
        }
        private void SetCurrency(BaseReference receiver, BaseResourceKind kind, int requested)
        {
            var city = BaseCurrencyAdditionSource.CurrencyCity(receiver, current);
            int cap = kind == BaseResourceKind.Gold ? BasePostLoad.ReadGoldCapacity(city, current)
                : BasePostLoad.ReadSuppliesCapacity(city, current);
            WriteResource(city, kind, Bound(requested, cap));
        }
        private void SetTroops(BaseReference receiver, int requested) =>
            WriteResource(receiver, BaseResourceKind.Troops, Bound(requested, TroopCapacity(receiver)));

        private void WriteResource(BaseReference receiver, BaseResourceKind kind, int value)
        {
            int physical = Need(receiver.PhysicalSlot, "Base_pointer");
            // Source-reset sentinel fields have no playable Domain0 owner.
            // An already-equal known value changes no persistent state; a
            // changed unowned field remains explicit rather than invented.
            if (physical == 0)
            {
                if (Need(current.ReadBaseResource(receiver, kind), "sentinel_resource") != value)
                    throw StoryResourceMissing("changed_sentinel_owner");
                return;
            }
            if (receiver.Kind == BaseKind.City)
            {
                var city = State.Cities.GetValueOrDefault(physical) ?? throw StoryResourceMissing("City_owner");
                var after = kind switch { BaseResourceKind.Gold => city with { Gold = value },
                    BaseResourceKind.Supplies => city with { Supplies = value }, _ => city with { Troops = value } };
                State = State with { Cities = new(State.Cities) { [physical] = after } };
            }
            else
            {
                if (kind != BaseResourceKind.Troops) throw StoryResourceMissing("Gate_currency_route");
                int location = checked(physical + 51);
                var gate = State.Gates.GetValueOrDefault(location) ?? throw StoryResourceMissing("Gate_owner");
                State = State with { Gates = new(State.Gates) { [location] = gate with { Troops = value } } };
            }
            current.SetBaseResource(receiver, kind, value);
        }

        private void SetEndurance(BaseReference receiver, int requested, bool refreshDamageTier)
        {
            BasePostLoad.WriteLinkedField14(receiver, requested, current);
            int link = Need(current.ReadBaseObjectLink(receiver), "Base_Object_link");
            var obj = current.ReadObjectTableReference(link <= 3000 ? link : 0)
                ?? throw StoryResourceMissing("linked_Object_pointer");
            if (Need(current.ReadObjectNativeValidity(obj), "linked_Object_validity"))
                State = PublishCurrentObjectDurability(State, current, obj, Need(current.ReadObjectRawField14(obj), "Object_durability"));
            if (refreshDamageTier) RefreshDamageTier(receiver);
        }
        private void RefreshDamageTier(BaseReference receiver)
        {
            byte tier = BasePostLoad.ReadFlag66(receiver, current);
            current.SetBasePostLoadFlag66(receiver, tier);
            int physical = Need(receiver.PhysicalSlot, "tier_Base_pointer");
            if (receiver.Kind == BaseKind.City)
            {
                var city = State.Cities.GetValueOrDefault(physical) ?? throw StoryResourceMissing("tier_City_owner");
                State = State with { Cities = new(State.Cities) { [physical] = city with { DamageTier = tier } } };
            }
            else
            {
                int location = checked(physical + 51);
                var gate = State.Gates.GetValueOrDefault(location) ?? throw StoryResourceMissing("tier_Gate_owner");
                State = State with { Gates = new(State.Gates) { [location] = gate with { DamageTier = tier } } };
            }
        }
        private static int Bound(int value, int cap) => value < 0 ? 0 : Math.Min(value, cap);
        private static T Need<T>(T? value, string field) where T : struct => value ?? throw StoryResourceMissing(field);
    }
    private static InvalidDataException StoryResourceMissing(string field) => new("story.base_resources." + field);
}
