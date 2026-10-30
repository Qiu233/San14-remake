using San14.Core;

namespace San14.Data;

internal sealed partial class ProposalLifecycleSource
{
    internal bool IsOfficerProposal(ProposalKind kind) => kind is >= ProposalKind.DevelopmentUp and <= ProposalKind.SpecialTraining;
    internal int Cost(ProposalEntry entry) => entry.Kind == ProposalKind.None ? 0 : entry.Arguments.Parameter0;
    internal int BookCostPerProposal => Rule("adoption-command-books", 1);
    internal int AvailableBooks => State.Districts.GetValueOrDefault(PrimaryDistrict)?.OrdersRemaining
        ?? throw Missing("primary_District_command_books");
    internal long AvailableGold => K(Current.ReadBaseResource(PayingCity, BaseResourceKind.Gold), "paying_City_gold");

    internal ForceReference PlayerForce => OwnedCurrentPlayerForce.ReadCurrentReference(State, Current)
        ?? throw Missing("current_player_Force");
    internal int PrimaryDistrict => K(ForcePrimaryDistrictSource.Resolve(Current, PlayerForce).PhysicalSlot,
        "primary_District_pointer");
    private BaseReference PayingCity
    {
        get
        {
            int force = K(PlayerForce.PhysicalSlot, "paying_Force_pointer");
            int ruler = K(Current.ReadForceRecord(force)?.RawRuler10, "paying_Force_ruler");
            int person = PersonPhysical(ruler);
            var home = BaseLookup.Find(K(Current.ReadPersonFirstLocation(person), "paying_ruler_home"), Current);
            return CurrentGateMapQueries.FindCity(home, Current);
        }
    }

    internal bool IsOfferVisible(ProposalEntry entry)
    {
        if (!K(Current.ReadForceNativeValidity(PlayerForce), "player_Force_validity") ||
            entry.SlotId == 0 || entry.Status == ProposalStatus.Empty) return false;
        int person = PersonPhysical(entry.ProposerPersonId);
        if (K(Current.ReadPersonDistrictSlot(person), "proposer_District") != PrimaryDistrict) return false;
        if (entry.Status == ProposalStatus.Offered &&
            (K(Current.ReadArmyReturnPersonFlags196(person), "proposer_flags") & 1) != 0) return false;
        return entry.Status switch
        {
            ProposalStatus.Offered or ProposalStatus.Pending => ValidHandler(entry),
            ProposalStatus.Executed => entry.Kind != ProposalKind.SecurityDown || IsCityValid(entry, true),
            _ => true
        };
    }

    internal bool CanAdopt(ProposalEntry entry) => entry.Kind != ProposalKind.None &&
        entry.Status == ProposalStatus.Offered && IsOfferVisible(entry) &&
        Cost(entry) <= AvailableGold && BookCostPerProposal <= AvailableBooks;

    private bool ValidHandler(ProposalEntry entry)
    {
        if (entry.Status == ProposalStatus.Offered)
        {
            if (!IsProposerEligible(entry.ProposerPersonId)) return false;
        }
        else if (entry.Status == ProposalStatus.Pending)
        {
            int person = PersonPhysical(entry.ProposerPersonId);
            var force = Current.ReadPersonForce(person) ?? throw Missing("pending_Person_Force");
            if (K(force.PhysicalSlot, "pending_Force_pointer") != K(PlayerForce.PhysicalSlot, "player_pointer") ||
                K(Current.ReadPersonStatus(person), "pending_Person_status") is < 2 or > 4) return false;
        }
        else return false;
        return entry.Kind switch
        {
            >= ProposalKind.DevelopmentUp and <= ProposalKind.SecurityDown or ProposalKind.BuyFood or ProposalKind.SellFood
                => IsCityValid(entry, false),
            >= ProposalKind.RecommendPerson and <= ProposalKind.FreePrisoner => IsPersonnelValid(entry, false),
            >= ProposalKind.MoraleUp and <= ProposalKind.RecoverWoundedSoldiers or
                >= ProposalKind.BefriendAlly and <= ProposalKind.FillTroops => IsArmyValid(entry, false),
            _ => false
        };
    }

    private void ApplyHandler(ProposalEntry entry)
    {
        switch (entry.Kind)
        {
            case >= ProposalKind.DevelopmentUp and <= ProposalKind.SecurityDown:
            case ProposalKind.BuyFood or ProposalKind.SellFood: ApplyCity(entry); break;
            case >= ProposalKind.RecommendPerson and <= ProposalKind.FreePrisoner: ApplyPersonnel(entry); break;
            case >= ProposalKind.MoraleUp and <= ProposalKind.RecoverWoundedSoldiers:
            case >= ProposalKind.BefriendAlly and <= ProposalKind.FillTroops: ApplyArmy(entry); break;
            default: throw Missing("handler_kind");
        }
    }

    internal void Adopt(IReadOnlyList<int> slotIds)
    {
        var board = State.Proposals ?? throw Missing("board_owner");
        if (slotIds.Count == 0 || slotIds.Distinct().Count() != slotIds.Count)
            throw new InvalidDataException("proposal.selection");
        long total = 0;
        foreach (int slot in slotIds)
        {
            if (slot is <= 0 or >= ProposalBoard.SlotCount) throw new InvalidDataException("proposal.slot");
            var entry = board.Read(slot);
            if (entry.Status != ProposalStatus.Offered || !IsOfferVisible(entry))
                throw new InvalidDataException("proposal.not_adoptable");
            total += Cost(entry);
        }
        if (total > AvailableGold || AvailableBooks < BookCostPerProposal)
            throw new InvalidDataException("proposal.insufficient_resources");
        BeginResults(slotIds.Any(slot => IsOfficerProposal(board.Read(slot).Kind)));
        //41D320 debits the ruler's associated City before each actual handler.
        foreach (int slot in slotIds)
        {
            var entry = (State.Proposals ?? throw Missing("board_owner")).Read(slot);
            int cityId = K(PayingCity.PhysicalSlot, "paying_City_pointer");
            var city = State.Cities.GetValueOrDefault(cityId) ?? throw Missing("paying_City_owner");
            State = State with { Cities = new(State.Cities) { [cityId] = city with
                { Gold = Math.Clamp(unchecked(city.Gold - Cost(entry)), 0, BasePostLoad.ReadGoldCapacity(PayingCity, Current)) } } };
            RefreshCurrent();
            bool immediate = entry.Kind is >= ProposalKind.DevelopmentUp and <= ProposalKind.SecurityDown or
                >= ProposalKind.BuyFood and <= ProposalKind.FreePrisoner;
            if (immediate) ApplyHandler(entry);
            SetEntry(entry with { Status = immediate ? ProposalStatus.Executed : ProposalStatus.Pending });
            var person = State.Officers.GetValueOrDefault(entry.ProposerPersonId) ?? throw Missing("adopt_Person_owner");
            ushort flags = K(OfficerBehaviorSourceAdapter.EncodeFlags(person.Behavior), "adopt_Person_flags");
            State = State with { Officers = new(State.Officers) { [person.Id] = person with
                { Behavior = OfficerBehaviorSourceAdapter.WithSourceFlags(person.Behavior, (ushort)(flags | 1)) } } };
            RefreshCurrent();
        }
        //The Officer multi-selection is one adoption command, with one book
        //debit after its ordered handler calls, rather than one UI order per row.
        State = Package.SpendProposalBooks(State, Current, PrimaryDistrict, BookCostPerProposal);
        RefreshCurrent();
        FlushResults();
    }

    internal void ExecutePending()
    {
        _ = State.Proposals ?? throw Missing("board_owner");
        for (int slot = 1; slot < ProposalBoard.SlotCount; slot++)
        {
            var entry = State.Proposals!.Read(slot);
            if (entry.Kind == ProposalKind.None || entry.Status != ProposalStatus.Pending || !IsOfferVisible(entry)) continue;
            //The source performs the second vslot5 check after the visibility
            //check. A failed outer check retains the pending offer unchanged.
            if (!ValidHandler(entry)) SetEntry(entry with { Status = ProposalStatus.Failed });
            else
            {
                ApplyHandler(entry);
                SetEntry(entry with { Status = ProposalStatus.Executed });
            }
            RefreshCurrent();
        }
    }

    private void SetEntry(ProposalEntry entry)
    {
        var board = (State.Proposals ?? throw Missing("board_owner")).Copy();
        board.Slots[entry.SlotId] = entry;
        State = State with { Proposals = board };
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal GameState SpendProposalBooks(GameState state, SourceWorldContext current, int districtId, int amount)
        => SpendStrategicCommandBooksCurrent(state, current, districtId, amount);
}
