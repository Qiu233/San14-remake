using San14.Core;
using San14.Data;
using TransportSetupQuery = San14.Data.TransportSetupView;

/// <summary>One local quantity-page owner. Person changes re-read the source
/// capacities and preferences, then clamp the same edited cargo quantities.
/// No query reserves stock or writes saved preferences.</summary>
internal sealed class TransportSetupDraft
{
    private GameState world;
    private readonly ArmyCampaignRuleAdapter rules;
    private readonly int source;
    internal TransportSetupQuery Setup { get; private set; }
    internal TransportQuantityView? Quantities { get; private set; }
    internal int? OfficerId => Setup.SelectedOfficerId;
    internal bool CanAccept => OfficerId.HasValue && Setup.DefaultNavalFormationId.HasValue &&
        Quantities is { CanDecide: true, Troops: not null, Gold: not null, Supplies: not null };

    internal TransportSetupDraft(GameState world, ArmyCampaignRuleAdapter rules, int source,
        TransportSetupSelection? previous = null)
    {
        this.world = world; this.rules = rules; this.source = source;
        Setup = rules.QueryTransportSetup(world, source, previous?.OfficerId);
        Quantities = previous is not null && Setup.SelectedOfficerId is int officer
            ? rules.QueryTransportQuantities(world, source, officer, previous.Troops, previous.CargoGold, previous.CargoSupplies)
            : Setup.InitialQuantities;
    }
    internal bool SelectOfficer(int id)
    {
        if (Setup.Persons?.Any(person => person.OfficerId == id && person.DisabledReasons == DeploymentPersonDisabledReason.None) != true)
            return false;
        var old = Quantities;
        Setup = rules.QueryTransportSetup(world, source, id);
        Quantities = old is { Troops: int troops, Gold: int gold, Supplies: int supplies }
            ? rules.QueryTransportQuantities(world, source, id, troops, gold, supplies) : Setup.InitialQuantities;
        return true;
    }
    internal void Edit(TransportSetupQuantity quantity, int value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
        if (OfficerId is null || Quantities is not { } quantities) return;
        switch (quantity)
        {
            case TransportSetupQuantity.Troops when quantities.MaximumTroops is int troopMaximum:
                value = Math.Min(value, troopMaximum);
                quantities = quantities with { Troops = value,
                    RemainingTroops = Setup.SourceTroops is int troopStock ? unchecked(troopStock - value) : null }; break;
            case TransportSetupQuantity.Gold when quantities.MaximumGold is int goldMaximum:
                value = Math.Min(value, goldMaximum);
                quantities = quantities with { Gold = value,
                    RemainingGold = Setup.SourceGold is int goldStock ? unchecked(goldStock - value) : null }; break;
            case TransportSetupQuantity.Supplies when quantities.MaximumSupplies is int suppliesMaximum:
                value = Math.Min(value, suppliesMaximum);
                quantities = quantities with { Supplies = value,
                    RemainingSupplies = Setup.SourceSupplies is int suppliesStock ? unchecked(suppliesStock - value) : null }; break;
            case TransportSetupQuantity.Troops or TransportSetupQuantity.Gold or TransportSetupQuantity.Supplies: return;
            default: throw new ArgumentOutOfRangeException(nameof(quantity));
        }
        bool? canDecide = quantities.IsPersonValid == false ? false : quantities.IsPersonValid != true ? null :
            quantities is { Troops: int t, Gold: int g, Supplies: int f } ? t != 0 || g != 0 || f != 0 : null;
        Quantities = quantities with { CanDecide = canDecide };
    }
    internal TransportSetupSelection Accept()
    {
        if (!CanAccept) throw new InvalidOperationException("Transport quantities are unavailable.");
        return new(OfficerId!.Value, Quantities!.Troops!.Value, Quantities.Gold!.Value, Quantities.Supplies!.Value,
            Setup.DefaultNavalFormationId!.Value, Setup.DefaultReturnOfficerToSource, Setup.DefaultSourceBehaviorChoice);
    }
    internal void AcceptPreferenceWorld(GameState previous, GameState replacement)
    {
        if (!ReferenceEquals(world, previous)) throw new InvalidOperationException("Transport Make belongs to another world.");
        // The accepted defaults command changes preferences only. Make keeps
        // its own original packet until a later real Person change refreshes it.
        world = replacement;
    }
}
