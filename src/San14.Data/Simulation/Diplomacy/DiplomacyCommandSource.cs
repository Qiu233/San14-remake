using San14.Core;

namespace San14.Data;

internal sealed record DiplomacySubmission(int ExecutorPhysical, BaseReference SourceBase,
    ForceReference Recipient, int NativeKind, int Books, int PaidGold, int PaidSupplies,
    int SourceTreasuryCityId, int TargetRulerOfficerId, int TargetCapitalCityId,
    int SourceDistrictId, int AllianceMonths, BaseReference SourceTreasuryBase,
    BaseReference TargetCapitalBase, BaseReference? RequestedTarget);

/// <summary>Real diplomatic submission and its resource owners. This leaf
/// does not allocate a task, debit resources, draw a result or publish reports.</summary>
public sealed partial class ArmyCampaignRuleAdapter
{
    internal DiplomacySubmission ReadDiplomacySubmissionCurrent(GameState state,
        SourceWorldContext current, PrepareDiplomaticOrder order, ForceState force, CommandContext context)
    {
        if (state.NativeArmySimulation?.Parameters is not { } parameters ||
            parameters.Language != Seed.Language ||
            !HasDiplomacyParameterProfile(parameters.Language, parameters.RuleVersion))
            throw DiplomacyCommandSource.Missing("submission_parameter_profile");
        int nativeKind = DiplomacyCommandSource.NativeKind(order.Kind);
        if (order.ForceId != force.Id || !state.Forces.ContainsKey(force.Id))
            throw DiplomacyCommandSource.Invalid("actor_Force");
        var own = current.ReadForceTableReference(force.Id is >= 0 and < 52 ? force.Id : 0)
            ?? throw DiplomacyCommandSource.Missing("actor_Force_pointer");
        var recipient = current.ReadForceTableReference(order.TargetForceId is >= 0 and < 52 ? order.TargetForceId : 0)
            ?? throw DiplomacyCommandSource.Missing("recipient_Force_pointer");
        if (!DiplomacyCommandSource.K(current.ReadForceNativeValidity(own), "actor_Force_validity") ||
            !DiplomacyCommandSource.K(current.ReadForceNativeValidity(recipient), "recipient_Force_validity") || own == recipient)
            throw DiplomacyCommandSource.Invalid("recipient_Force");
        if (!state.Officers.TryGetValue(order.ExecutorOfficerId, out var executor))
            throw DiplomacyCommandSource.Invalid("executor");
        int person = current.ResolveOfficerSource(order.ExecutorOfficerId)
            ?? throw DiplomacyCommandSource.Missing("executor_physical");
        if (!DiplomacyCommandSource.K(current.ReadPersonNativeValidity(person), "executor_validity"))
            throw DiplomacyCommandSource.Invalid("executor_validity");
        if (PersonCurrentBusySource.Read(current, person, DiplomacyCommandSource.Missing) ||
            PlayableOfficerAvailability.IsSourceBusy(state, executor) || executor.Deployed ||
            PlayableDiplomacy.IsOfficerOnMission(state, executor.Id) ||
            state.PreparedTalentOrders?.ContainsKey(executor.Id) == true ||
            state.PreparedOfficerMoves?.ContainsKey(executor.Id) == true ||
            state.PreparedDeployments.Values.Any(value => value.OfficerId == executor.Id))
            throw DiplomacyCommandSource.Invalid("executor_busy");
        if (DiplomacyCommandSource.K(current.ReadPersonStatus(person), "executor_E6") is < 1 or > 4 ||
            (DiplomacyCommandSource.K(current.ReadArmyReturnPersonFlags196(person), "executor_15E") & 1) != 0)
            throw DiplomacyCommandSource.Invalid("executor_unavailable");
        var actorForce = current.ReadPersonForce(person) ?? throw DiplomacyCommandSource.Missing("executor_Force");
        if (actorForce != own) throw DiplomacyCommandSource.Invalid("executor_ownership");
        ushort home = DiplomacyCommandSource.K(current.ReadPersonFirstLocation(person), "executor_E2");
        var sourceBase = BaseLookup.Find(home, current);
        if (sourceBase.Kind != BaseKind.City || home != order.SourceCityId ||
            !DiplomacyCommandSource.K(current.ReadBaseNativeValidity(sourceBase), "source_Base_validity"))
            throw DiplomacyCommandSource.Invalid("source_City");
        var district = current.ReadBaseDistrictReference(sourceBase)
            ?? throw DiplomacyCommandSource.Missing("source_Base_District");
        if (!DiplomacyCommandSource.K(current.ReadDistrictNativeValidity(district), "source_District_validity"))
            throw DiplomacyCommandSource.Invalid("source_District");
        int districtId = DiplomacyCommandSource.K(district.PhysicalSlot, "source_District_physical");
        if (DiplomacyCommandSource.K(current.ReadDistrictForceId(districtId), "source_District10") != force.Id)
            throw DiplomacyCommandSource.Invalid("source_ownership");
        if (context.DelegatedDistrictId is int delegated ? delegated != districtId :
            DiplomacyCommandSource.K(current.ReadDistrictNumber(districtId), "source_District11") != 1)
            throw DiplomacyCommandSource.Invalid("District_authority");
        if (order.GiftGold < 0 || order.GiftSupplies < 0)
            throw DiplomacyCommandSource.Invalid("negative_offer");
        //1EFD40 admits an actual Item or Area instead of money/food.
        if (order.GiftItemId is int itemId)
        {
            if (itemId is < 1 or > 201 || order.GiftGold != 0 || order.GiftSupplies != 0 || order.GiftAreaId is not null)
                throw DiplomacyCommandSource.Invalid("Item_offer");
            var item = current.ReadRegistryTableReference(RegistryKind.Item, itemId)
                ?? throw DiplomacyCommandSource.Missing("initial_Item_pointer");
            if (!DiplomacyCommandSource.K(current.ReadRegistryObjectNativeValidity(item), "initial_Item_validity") ||
                DiplomacyCommandSource.K(current.ReadCaptureItemOwner4A(itemId), "initial_Item4A") !=
                DiplomacyCommandSource.K(current.ReadPersonIdentity(new DiplomacyCommandSource(state, current).Ruler(force.Id)), "initial_ruler10"))
                throw DiplomacyCommandSource.Invalid("initial_Item_ownership");
        }
        if (order.GiftAreaId is int areaId)
        {
            if (order.GiftGold != 0 || order.GiftSupplies != 0 || order.GiftItemId is not null ||
                !ReadDiplomacyAreaOfferEligibilityCurrent(state, current, areaId, force.Id, order.TargetForceId))
                throw DiplomacyCommandSource.Invalid("initial_Area_offer");
        }
        if (nativeKind is 4 or 5 or 9 or 19 && order.GiftGold == 0 && order.GiftSupplies == 0 &&
            order.GiftItemId is null && order.GiftAreaId is null)
            throw DiplomacyCommandSource.Invalid("empty_offer");
        if (nativeKind == 5 && order.AllianceMonths <= 0)
            throw DiplomacyCommandSource.Invalid("alliance_duration");

        if(nativeKind==8)RequireReturnCaptivesSubmissionCurrent(state,current,order);
        else if(!order.RequestedCaptiveOfficerIds.IsEmpty)throw DiplomacyCommandSource.Invalid("unexpected_captives");
        if(nativeKind==7)RequireDemandSurrenderSubmissionCurrent(state,current,order);
        if(nativeKind==19)RequireExchangeSubmissionCurrent(state,current,order);
        var queries = new DiplomacyCommandSource(state, current);
        queries.RequireInteraction(force.Id, order.TargetForceId,requireIndependentRecipient:nativeKind is not (7 or 8));
        int treatyMonths = queries.TreatyMonths(force.Id, order.TargetForceId);
        if (nativeKind == 5 && ((long)treatyMonths + order.AllianceMonths > 48))
            throw DiplomacyCommandSource.Invalid("alliance_48_month_limit");
        if (nativeKind is 6 or 9 && treatyMonths == 0)
            throw DiplomacyCommandSource.Invalid("alliance_required");
        BaseReference? requested = null;
        if (nativeKind == 9)
        {
            if (order.TargetCityId is not int targetCityId || !state.Cities.ContainsKey(targetCityId))
                throw DiplomacyCommandSource.Invalid("request_target_City");
            requested = BaseLookup.Find(targetCityId, current);
            if (!DiplomacyCommandSource.K(current.ReadBaseNativeValidity(requested), "request_target_validity"))
                throw DiplomacyCommandSource.Invalid("request_target_validity");
            int victim = DiplomacyCommandSource.K(current.ReadBaseForceSlot(requested), "request_target_Force");
            if (queries.HasTreaty(order.TargetForceId, victim) || queries.HasTreaty(force.Id, victim))
                throw DiplomacyCommandSource.Invalid("request_target_allied");
        }
        else if (order.TargetCityId is not null)
            throw DiplomacyCommandSource.Invalid("unexpected_target_City");

        int targetRuler = DiplomacyCommandSource.K(current.ReadPersonIdentity(queries.Ruler(order.TargetForceId)),
            "recipient_ruler_Person10");
        var capital = queries.Capital(order.TargetForceId);
        int capitalLocation = DiplomacyCommandSource.K(current.ReadBaseObjectLink(capital), "recipient_capital4E");
        var treasury = queries.Capital(force.Id);
        int treasuryLocation = DiplomacyCommandSource.K(current.ReadBaseObjectLink(treasury), "treasury_Base4E");
        //The initializer debits22EA10(current source District Force), not
        //the executor's arbitrary source City. City/Gate kind is retained.
        int gold = ReadDiplomacyBaseResourceCurrent(current, treasury, BaseResourceKind.Gold);
        int supplies = ReadDiplomacyBaseResourceCurrent(current, treasury, BaseResourceKind.Supplies);
        if (gold < order.GiftGold || supplies < order.GiftSupplies)
            throw DiplomacyCommandSource.Invalid("insufficient_offer_resources");
        int books = DiplomacyCommandSource.ReadBooks(state, order.Kind);
        int available = state.Districts.GetValueOrDefault(districtId)?.OrdersRemaining
            ?? throw DiplomacyCommandSource.Missing("source_District14");
        if ((long)available - CommandBookAvailability.Reserved(state, districtId) < books)
            throw DiplomacyCommandSource.Invalid("insufficient_command_books");
        return new(person, sourceBase, recipient, nativeKind, books, order.GiftGold, order.GiftSupplies,
            treasuryLocation, targetRuler, capitalLocation, districtId,
            nativeKind == 5 ? order.AllianceMonths : 0, treasury, capital, requested);
    }

    /// <summary>Independently qualified fresh TC/SC costs are installed here. Relationship
    /// and pending-mission initialization belong to their typed owners.</summary>
    public GameState InstallFreshDiplomacyRules(GameState state)
    {
        if (Seed.Language is not ("TC" or "SC")) return state;
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } || state.Units.Count != 0 ||
            state.NativeArmySimulation is not { } simulation || simulation.Parameters.Language != Seed.Language ||
            simulation.Parameters.RuleVersion != SimulationRuleIdentity.ForLocale(Seed.Language))
            throw DiplomacyCommandSource.Missing("unused_fresh_allocation");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        foreach (var kind in new[] { DiplomaticMissionKind.Goodwill, DiplomaticMissionKind.Alliance,
            DiplomaticMissionKind.CancelAlliance, DiplomaticMissionKind.RequestAttack,
            DiplomaticMissionKind.ReturnCaptives,DiplomaticMissionKind.DemandSurrender,DiplomaticMissionKind.Exchange })
        {
            string key = DiplomacyCommandSource.BookKey(kind);
            if (values.ContainsKey(key) || simulation.Parameters.Float32Values.ContainsKey(key))
                throw DiplomacyCommandSource.Missing("fresh_cost_conflict");
            //Each edition's actual switch selects its own seven .data
            //Int32 fields; every independently read cost is exactly1.
            values.Add(key, 1);
        }
        var installed = state with { NativeArmySimulation = simulation with
            { Parameters = simulation.Parameters with { Int32Values = values } } };
        return SynchronizeDiplomacyProjection(InstallFreshDiplomacyCommandRewardParameters(
            InstallFreshDiplomacyRequestTerminationParameters(InstallFreshDiplomacyCounterParameters(
                InstallFreshDiplomacyNpcParameters(InstallFreshExtendedDiplomacyRules(InstallFreshDiplomacyNumericParameters(installed)))))));
    }
}

internal sealed class DiplomacyCommandSource(GameState state, SourceWorldContext current)
{
    internal static int NativeKind(DiplomaticMissionKind kind) => kind switch
    {
        DiplomaticMissionKind.Goodwill => 4, DiplomaticMissionKind.Alliance => 5,
        DiplomaticMissionKind.CancelAlliance => 6, DiplomaticMissionKind.RequestAttack => 9,
        DiplomaticMissionKind.ReturnCaptives=>8,DiplomaticMissionKind.DemandSurrender=>7,DiplomaticMissionKind.Exchange=>19,
        _ => throw Invalid("kind")
    };
    internal static string BookKey(DiplomaticMissionKind kind) => kind switch
    {
        DiplomaticMissionKind.Goodwill => "simulation.diplomacy.goodwill-command-books",
        DiplomaticMissionKind.Alliance => "simulation.diplomacy.alliance-command-books",
        DiplomaticMissionKind.CancelAlliance => "simulation.diplomacy.cancel-alliance-command-books",
        DiplomaticMissionKind.RequestAttack => "simulation.diplomacy.request-attack-command-books",
        DiplomaticMissionKind.ReturnCaptives=>"simulation.diplomacy.return-captives-command-books",
        DiplomaticMissionKind.DemandSurrender=>"simulation.diplomacy.demand-surrender-command-books",
        DiplomaticMissionKind.Exchange=>"simulation.diplomacy.exchange-command-books",
        _ => throw Invalid("kind")
    };
    internal static int ReadBooks(GameState state, DiplomaticMissionKind kind)
    {
        string key = BookKey(kind);
        var parameters = state.NativeArmySimulation?.Parameters ?? throw Missing("cost_parameters");
        if(parameters.Float32Values.ContainsKey(key))throw Missing(key);
        if(!parameters.Int32Values.TryGetValue(key,out int value))
        {
            //Only these independently qualified TC/SC command-cost rules can be
            //absent in an older world. Existing four-kind knowledge is unchanged.
            if(parameters.Language is ("TC" or "SC") && parameters.RuleVersion==SimulationRuleIdentity.ForLocale(parameters.Language) &&
                kind is DiplomaticMissionKind.ReturnCaptives or DiplomaticMissionKind.DemandSurrender or DiplomaticMissionKind.Exchange)value=1;
            else throw Missing(key);
        }
        if (value < 0) throw Missing("negative_command_books");
        return value;
    }
    internal int Ruler(int force)
    {
        ushort id = current.ReadForceRecord(force)?.RawRuler10 ?? throw Missing("Force_ruler10");
        return K((current.ReadPersonLookupReference(id <= 6000 ? id : 0)
            ?? throw Missing("ruler_pointer")).PhysicalSlot, "ruler_physical");
    }
    internal BaseReference Capital(int force)
    {
        ushort home = K(current.ReadPersonFirstLocation(Ruler(force)), "ruler_E2");
        var result = BaseLookup.Find(home, current);
        if (!K(current.ReadBaseNativeValidity(result), "capital_validity")) throw Invalid("invalid_capital");
        return result;
    }
    internal void RequireInteraction(int own, int other,bool requireIndependentRecipient=true)
    {
        if (requireIndependentRecipient && ForceByte(other, 0x194) != 0) throw Invalid("dependent_recipient");
        if (CoalitionBlocks(own, other)) throw Invalid("coalition_pair");
        bool IsPlayer(int force) => K(current.ReadForcePhysicalSlot(OwnedCurrentPlayerForce.ReadCurrentReference(state, current)
            ?? throw Missing("player_Force")), "player_Force_physical") == force;
        if (IsPlayer(other) && (Flags178(own) & 1) != 0 || IsPlayer(own) && (Flags178(other) & 1) != 0)
            throw Invalid("player_interaction_disabled");
    }
    internal int TreatyMonths(int own, int other) => Relation(own, other) & 0x7F;
    //2C3CA0 returns true for the same Force or an actual protected pair.
    //It is distinct from the combat hostility helper2CA4D0.
    internal bool HasTreaty(int first, int second)
    {
        var a = current.ReadForceTableReference(first is >= 0 and < 52 ? first : 0)
            ?? throw Missing("treaty_first_Force");
        var b = current.ReadForceTableReference(second is >= 0 and < 52 ? second : 0)
            ?? throw Missing("treaty_second_Force");
        if (!K(current.ReadForceNativeValidity(a), "treaty_first_validity") ||
            !K(current.ReadForceNativeValidity(b), "treaty_second_validity")) return false;
        if (a == b) return true;
        if (CoalitionBlocks(first, second)) return false;
        if ((Relation(first, second) & 0x7F) != 0) return true;
        if ((ForceByte(first, 0x12) & 2) != 0 && (ForceByte(second, 0x12) & 2) != 0) return true;
        return ForceByte(first, 0x194) == second || ForceByte(second, 0x194) == first;
    }
    internal bool CoalitionBlocks(int first, int second)
    {
        int player = state.NativeArmySimulation?.World.SelectedForce1656 ?? throw Missing("World1656");
        return player == first && (ForceByte(second, 0x12) & 2) != 0 ||
            (ForceByte(first, 0x12) & 2) != 0 && player == second;
    }
    private ushort Flags178(int force)
    {
        if (state.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(force, out var reset) == true)
            return (ushort)(K(reset[0x178], "Force_reset178") | K(reset[0x179], "Force_reset179") << 8);
        return state.NativeArmySimulation?.Forces.GetValueOrDefault(force)?.DiplomacyFlags178
            ?? throw Missing("Force_DiplomacyFlags178");
    }
    private byte ForceByte(int force, int field)
    {
        if (state.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(force, out var reset) == true)
            return K(reset[field], "Force_reset_" + field.ToString("X"));
        var owner = state.NativeArmySimulation?.Forces.GetValueOrDefault(force) ?? throw Missing("Force_current");
        return (field == 0x12 ? owner.Flags12 : owner.Flags194) ?? throw Missing("Force_" + field.ToString("X"));
    }
    private byte Relation(int first, int second)
    {
        if (second is < 0 or >= 52) throw Invalid("relation_Force");
        if (state.NativeForceUnregisterCurrent?.ForceResetImages.TryGetValue(first, out var reset) == true)
            return K(reset[0xEA + second], "Force_reset_relation");
        return state.NativeArmySimulation?.Forces.GetValueOrDefault(first)?.RelationsEA is { Length: 52 } values
            ? values[second] : throw Missing("Force_RelationsEA");
    }
    internal static T K<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    internal static InvalidDataException Missing(string field) => new("diplomacy.submission." + field);
    internal static ArgumentException Invalid(string field) => new("diplomacy.submission." + field);
}
