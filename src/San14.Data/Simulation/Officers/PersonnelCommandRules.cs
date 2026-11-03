using San14.Core;

namespace San14.Data;

/// <summary>Current explicitly resolved inputs, independent of image initial values.</summary>
public sealed record PersonnelCommandContext(int? CommandBooksCost,
    int? ConfiscationPenalty, bool? IsHiredOfficer,
    PersonnelLoyaltyGuards? LoyaltyGuards, string Revision)
{
    public int? ChargedDistrictId { get; init; }
    public byte? StoredLoyalty { get; init; }
}

public interface IPersonnelCommandContext
{
    PersonnelCommandContext? Resolve(GameState state, ForceState force, PersonnelChange change);
}

/// <summary>
/// Original item loyalty arithmetic and known headquarters charging selection.
/// Current parameters and native guards remain required provider inputs. Office
/// permission, capacity and salary effects are an independent adapter.
/// </summary>
public sealed class PersonnelCommandRules(IPersonnelCommandContext context,
    IPersonnelCommandRules? officeRules = null) : IPersonnelCommandRules
{
    public PersonnelEvaluation Evaluate(GameState state, ForceState force,
        PersonnelChange change, CommandContext commandContext)
    {
        if (change.Kind == PersonnelActionKind.SetOffice)
            return (officeRules ?? UnresolvedPersonnelRules.Instance).Evaluate(state, force, change, commandContext);
        PersonnelCommandContext? current = context.Resolve(state, force, change);
        if (current is null || string.IsNullOrWhiteSpace(current.Revision))
            return Unknown("personnel.current_context");
        if (change.Kind == PersonnelActionKind.GrantItem)
        {
            if (current.IsHiredOfficer is not bool hired) return Unknown("personnel.hired_status");
            if (hired) return new(false, Reason: "Hired officers cannot receive items.");
        }
        if (current.CommandBooksCost is not int books) return Unknown("personnel.command_cost");
        if (books < 0) throw new InvalidDataException("Original personnel command-book parameter is negative.");
        if (current.LoyaltyGuards is not { } guards) return Unknown("personnel.loyalty_guards");
        bool? shouldChange = PersonnelLoyaltyMath.ShouldChange(guards);
        if (shouldChange is null) return Unknown("personnel.loyalty_guards");
        int delta = 0;
        byte? storedBefore = null, storedAfter = null;
        if (shouldChange.Value)
        {
            OfficerState target = state.Officers[change.OfficerId];
            if (guards.ForceStored255Flag is null) return Unknown("personnel.loyalty_guards");
            if (guards.ForceStored255Flag == true && current.StoredLoyalty is null)
                return Unknown("personnel.loyalty_sentinel");
            int? loyaltyValue = current.StoredLoyalty is byte raw ? raw : target.Loyalty;
            if (loyaltyValue is not int loyalty || loyalty is < 0 or > 255) return Unknown("personnel.loyalty");
            if (change.ItemId is not int itemId || state.Items is null ||
                !state.Items.TryGetValue(itemId, out ItemState? item) ||
                !state.ItemDefinitions.TryGetValue(item.DefinitionId, out ItemDefinition? definition) ||
                definition.LoyaltyValue is not int itemValue)
                return Unknown("personnel.item_loyalty_value");
            if (itemValue is < 0 or > 255)
                throw new InvalidDataException("Original item loyalty parameter is outside its byte domain.");
            bool grant = change.Kind == PersonnelActionKind.GrantItem;
            if (!grant && current.ConfiscationPenalty is null) return Unknown("personnel.confiscation_penalty");
            int? changed = PersonnelLoyaltyMath.TryApply((byte)loyalty, (byte)itemValue,
                grant, grant ? 0 : current.ConfiscationPenalty!.Value, guards);
            if (changed is not int result || result is < 0 or > 255 || result > 100 && current.StoredLoyalty is null)
                return Unknown("personnel.loyalty_result");
            //The ordinary delta cannot encode a transition to/from a stored
            //sentinel. Its explicit byte facts remain authoritative instead.
            delta = loyalty <= 100 && result <= 100 ? result - loyalty : 0;
            if (current.StoredLoyalty.HasValue)
            { storedBefore = (byte)loyalty; storedAfter = (byte)result; }
        }
        DistrictState[] headquarters = current.ChargedDistrictId is int actualDistrict
            ? state.Districts.Values.Where(district => district.Id == actualDistrict && district.ForceId == force.Id).ToArray()
            : state.Districts.Values.Where(district => district.ForceId == force.Id &&
                (district.Number == 1 || force.RulerId is int ruler && district.LeaderId == ruler)).ToArray();
        // The source falls back to physical district slot zero. Generic Core IDs
        // do not retain that storage position, so missing or ambiguous HQ remains unknown.
        if (headquarters.Length != 1) return Unknown("personnel.headquarters_district");
        return new(true, new PersonnelQuote(headquarters[0].Id, null, books, 0, delta, current.Revision)
            { StoredLoyaltyBefore = storedBefore, StoredLoyaltyAfter = storedAfter });
    }

    private static PersonnelEvaluation Unknown(string key) => new(null, MissingRuleKey: key,
        Reason: "Current original personnel inputs are unresolved.");
}
