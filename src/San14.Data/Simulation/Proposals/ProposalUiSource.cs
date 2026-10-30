using San14.Core;

namespace San14.Data;

internal sealed partial class ProposalLifecycleSource
{
    internal ProposalPage PublishPage()
    {
        var board = State.Proposals ?? throw Missing("board_owner");
        var visible = board.Slots.Skip(1).Where(e => e.Kind != ProposalKind.None && IsOfferVisible(e)).ToArray();
        var officers = visible.Where(e => IsOfficerProposal(e.Kind)).ToArray();
        var first = new List<ProposalRow>();
        if (officers.Length != 0) first.Add(PublishRow(officers[0], true));
        first.AddRange(visible.Where(e => !IsOfficerProposal(e.Kind)).Select(e => PublishRow(e, false)));
        int ruler = RulerId();
        return new(ForceId, Package.ProposalPersonName(State, ruler), first, officers.Select(e => PublishRow(e, false)).ToArray())
        { AvailableGold = AvailableGold, AvailableActionPoints = AvailableBooks, ActionPointCost = BookCostPerProposal };
    }

    private ProposalRow PublishRow(ProposalEntry entry, bool umbrella)
    {
        int person = PersonPhysical(entry.ProposerPersonId);
        var home = CityPersonCity(person);
        int rawHome = K(Current.ReadBaseObjectLink(home), "row_home");
        var summary = Summary(entry, umbrella);
        var nameIds = new int[] { 0, 0x25B, 0x2C3, 0x2C4, 0x2C5, 0x2C6, 0x2C9, 0x2CA, 0x2CB,
            0x2D0, 0x2D1, 0x2D2, 0x2D3, 0x2C7, 0x2C8, 0x2CC, 0x2CD, 0x2CE, 0x2CF, 0x2D4,
            0x2D5, 0x2D6, 0x2D7, 0x2D8, 0x2D9 };
        int name = umbrella ? 0x2DA : nameIds[(int)entry.Kind];
        if (!umbrella && entry.Kind == ProposalKind.DevelopmentUp)
        {
            int focus = K(Current.ReadAssignmentArea72(entry.Arguments.Parameter2), "row_development_focus");
            name = focus is >= 1 and <= 3 ? 0x2B2 + focus : 0;
        }
        return new(entry.SlotId, (int)entry.Kind, name == 0 ? string.Empty : Package.ReadHistoryDataText(1, name),
            summary, entry.ProposerPersonId, Package.ProposalPersonName(State, entry.ProposerPersonId),
            entry.Status != ProposalStatus.Offered, umbrella || CanAdopt(entry))
        {
            Cost = umbrella ? 0 : Cost(entry), HomeCityName = Package.ProposalBaseName(State, Current, rawHome),
            TargetName = entry.Kind is >= ProposalKind.DevelopmentUp and <= ProposalKind.Investment
                ? State.Areas.GetValueOrDefault(entry.Arguments.Parameter2)?.Name ?? throw Missing("row_target_Area_name") : null,
            DetailId = TargetHex(entry), Portrait = Package.ProposalPortrait(State, Current, person, entry.ProposerPersonId),
            OpensOfficerPage = umbrella
        };
    }

    internal int RulerId() => K(Current.ReadMoraleWaveRuler22EAC0(PlayerForce), "player_ruler");
    internal int TargetHex(ProposalEntry entry)
    {
        int target = entry.Arguments.Parameter2;
        if (entry.Kind is ProposalKind.SecurityDown or ProposalKind.RecommendPerson)
            return K(Current.ReadAreaHexIndex(new(target < 501 ? target : 0)), "target_Area_hex");
        if (entry.Kind is >= ProposalKind.MoraleUp and <= ProposalKind.RecoverWoundedSoldiers or >= ProposalKind.BefriendAlly)
        {
            var army = ProposalArmy(target);
            if (K(Current.ReadArmyUnitNativeValidity(army), "target_Army_validity")) return ProposalArmyHex(army);
        }
        if (entry.Kind is ProposalKind.Exploit or ProposalKind.Requisition or ProposalKind.BuyFood or ProposalKind.SellFood)
            return Current.ReadBaseCurrentHexIndex(CityReference(target));
        if (entry.Kind is ProposalKind.RepairEndurance or ProposalKind.Banquet)
            return Current.ReadBaseCurrentHexIndex(CityBase(target));
        if (entry.Kind == ProposalKind.CurePerson)
        {
            var date = State.NativeMapSeason?.ThirdDate ?? throw Missing("target_date");
            return K(PersonReturnCommands.ResolvePersonPosition(State, Current, PersonPhysical(target), date).PhysicalSlot, "target_Person_hex");
        }
        if (entry.Kind == ProposalKind.FreePrisoner)
        {
            var force = Current.ReadForceTableReference(entry.Arguments.Parameter3 < 52 ? entry.Arguments.Parameter3 : 0)
                ?? throw Missing("target_Force");
            if (K(Current.ReadForceNativeValidity(force), "target_Force_validity"))
                return Current.ReadBaseCurrentHexIndex(PersonnelCapital(force));
        }
        int proposer = PersonPhysical(entry.ProposerPersonId);
        if (IsOfficerProposal(entry.Kind))
        {
            int area = CityAssignedArea(proposer);
            if (K(Current.ReadAreaNativeValidity(new(area)), "target_assigned_Area_validity"))
                return K(Current.ReadAreaHexIndex(new(area)), "target_assigned_Area_hex");
        }
        return Current.ReadBaseCurrentHexIndex(CityBase(K(Current.ReadPersonFirstLocation(proposer), "target_home")));
    }

    private string Summary(ProposalEntry entry, bool umbrella)
    {
        var args = entry.Arguments;
        int ruler = RulerId(), target = args.Parameter2, root;
        var operands = new HistoryMessageOperands(Person0: entry.ProposerPersonId, Person1: ruler,
            Integers: [Cost(entry), 0, 0, 0, 0]);
        if (umbrella) root = 0x4EFF;
        else switch (entry.Kind)
        {
            case ProposalKind.DevelopmentUp:
                int focus = K(Current.ReadAssignmentArea72(target), "summary_focus");
                root = focus switch { 0 or 1 => 0x4E21, 2 => 0x4E29, 3 => 0x4E31, _ => throw Missing("summary_focus_range") };
                operands = operands with { Area0 = target }; break;
            case ProposalKind.SecurityUp: root = 0x4E39; operands = operands with { Area0 = target }; break;
            case ProposalKind.Investment: root = 0x4E41; operands = operands with { Area0 = target }; break;
            case ProposalKind.Conscription: root = 0x4E49; operands = operands with { Base0 = (ushort)target }; break;
            case ProposalKind.SpecialTraining: root = 0x4E51; operands = operands with { Base0 = (ushort)target }; break;
            case ProposalKind.RepairEndurance: root = 0x4E79; operands = operands with { Base0 = (ushort)target }; break;
            case ProposalKind.Exploit: root = 0x4E81; operands = operands with { Base0 = (ushort)(target < 52 ? target : 0), Integers = [0,0,0,0,0] }; break;
            case ProposalKind.Requisition: root = 0x4E89; operands = operands with { Base0 = (ushort)(target < 52 ? target : 0), Integers = [0,0,0,0,0] }; break;
            case ProposalKind.SecurityDown:
                root = 0x4E61; int force = CityAreaForce(target);
                var forceRef = Current.ReadForceTableReference(force) ?? throw Missing("summary_enemy_Force");
                operands = operands with { Area0 = target, Force0 = force, Person2 = K(Current.ReadMoraleWaveRuler22EAC0(forceRef), "summary_enemy_ruler") }; break;
            case ProposalKind.BuyFood: case ProposalKind.SellFood:
                root = entry.Kind == ProposalKind.BuyFood ? 0x4E91 : 0x4E99;
                operands = operands with { Base0 = (ushort)target, Integers = [args.Parameter3,args.Parameter4,0,0,0] }; break;
            case ProposalKind.RecommendPerson:
                root = 0x4EA1; operands = operands with { Base0 = K(Current.ReadAreaCitySlot(new(target)), "summary_recommend_City") }; break;
            case ProposalKind.Banquet: root = 0x4EA9; operands = operands with { Base0 = (ushort)target }; break;
            case ProposalKind.CurePerson: case ProposalKind.FreePrisoner:
                root = entry.Kind == ProposalKind.CurePerson ? 0x4EB1 : 0x4EB9;
                operands = operands with { Person2 = target, Base0 = K(Current.ReadPersonFirstLocation(PersonPhysical(target)), "summary_target_home"),
                    Force0 = entry.Kind == ProposalKind.FreePrisoner ? args.Parameter3 : null,
                    Integers = entry.Kind == ProposalKind.FreePrisoner ? [0,0,0,0,0] : operands.Integers }; break;
            default:
                var army = ProposalArmy(target); int hex = ProposalArmyHex(army);
                int area = K(Current.ReadHexAreaSlot(new(hex < 48400 ? hex : 0)), "summary_Army_Area");
                root = entry.Kind switch { ProposalKind.MoraleUp => 0x4E59, ProposalKind.MoraleDown => 0x4E69,
                    ProposalKind.RecoverWoundedSoldiers => 0x4E71, ProposalKind.BefriendAlly => 0x4EC1,
                    ProposalKind.StunEnemy => 0x4EC9, ProposalKind.ConfuseEnemy => 0x4ED1,
                    ProposalKind.BurnEnemy => 0x4ED9, ProposalKind.ExtinguishFriendlyFire => 0x4EE1,
                    ProposalKind.FillTroops => 0x4EE9, _ => throw Missing("summary_kind") };
                operands = operands with { Army0 = army, Area0 = area,
                    Force0 = entry.Kind is ProposalKind.MoraleDown or ProposalKind.StunEnemy or ProposalKind.ConfuseEnemy or ProposalKind.BurnEnemy ? ProposalArmyForce(army) : null,
                    Base0 = entry.Kind == ProposalKind.FillTroops ? (ushort)args.Parameter3 : null }; break;
        }
        //Both summary entry points install a per-offer stream and restore App.
        var date = State.NativeMapSeason?.ThirdDate ?? throw Missing("summary_date");
        uint seed = unchecked((uint)(((int)entry.Kind + entry.ProposerPersonId * 10) * 10000 +
            (date.Day + (date.Year * 30 + date.Month) * 12) * 24 + date.Hour));
        var local = State with { CampaignAppRandom32 = new(seed) };
        return Package.FormatProposalReportCurrent(local, Current, root, operands).Text;
    }
}

public sealed partial class ArmyCampaignRuleAdapter
{
    internal OfficerPortraitRoute? ProposalPortrait(GameState state, SourceWorldContext current, int physical, int id)
        => current.ReadPersonPortraitAppearance(physical) is ushort appearance
            ? ReadOfficerPortraitAppearance(state, current, _definitions, id, appearance, ArmyMapUiSources.Create()) : null;
    internal string ProposalBaseName(GameState state, SourceWorldContext current, int raw)
    {
        var receiver = BaseLookup.Find(raw, current);
        int id = receiver.PhysicalSlot ?? throw new InvalidDataException("proposal.Base_pointer");
        return receiver.Kind switch
        {
            BaseKind.City => state.Cities.GetValueOrDefault(id)?.Name ?? throw new InvalidDataException("proposal.City_name"),
            BaseKind.Gate => state.Gates.GetValueOrDefault(id + 51)?.Name ?? throw new InvalidDataException("proposal.Gate_name"),
            _ => throw new InvalidDataException("proposal.Base_name")
        };
    }
}
