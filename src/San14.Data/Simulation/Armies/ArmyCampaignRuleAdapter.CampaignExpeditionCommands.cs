using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter : ICampaignExpeditionExecution
{
    /// <summary>772630: allocate the real expedition before the ordered make
    /// packets, call their shared1EC980 with parameter2=0, assign13C only
    /// after each complete creator, then debit the current player's primary
    /// district once through the actual32EF60 payer.</summary>
    CommandResult ICampaignExpeditionExecution.Launch(GameState state, ForceState force,
        LaunchCampaignOrder order, CommandContext context)
    {
        try
        {
            ValidateCampaignDeploymentScope(state, force.Id, order.ProvinceId, order.DistrictId,
                order.Armies.Select(row => row.SourceLocationId).ToArray());
            var owner = state.CampaignExpeditions ?? throw CampaignLaunchMissing("current_controller");
            var counters = owner.ProvinceSequences ?? throw CampaignLaunchMissing("current_Province2F");
            if (!counters.TryGetValue(order.ProvinceId, out int sequence))
                throw CampaignLaunchMissing("current_Province2F_" + order.ProvinceId);
            var date = state.NativeMapSeason?.ThirdDate ?? throw CampaignLaunchMissing("actual_World_date");
            int year = unchecked((short)date.Year), month = unchecked((sbyte)date.Month);

            //The native scan retains the last physical receiver if every
            //nonzero slot is occupied. The domain stores only active15!=0
            //records; absent entries are known reset receivers, not unknown.
            int campaign = Enumerable.Range(1, 50).FirstOrDefault(id => !owner.Expeditions.ContainsKey(id));
            if (campaign == 0) campaign = 50;
            bool alreadyStarted = owner.Expeditions.Values.Any(row => row.ProvinceId == order.ProvinceId &&
                !row.IsWithdrawing && row.StartYear == year && row.StartMonth == month);
            if (!alreadyStarted) sequence = Math.Min(sequence + 1, 100);
            var records = new Dictionary<int, CampaignExpedition>(owner.Expeditions)
            {
                [campaign] = new(campaign, order.ProvinceId, Math.Min(sequence, 100),
                    Math.Clamp(year, 0, 350), Math.Clamp(month, 1, 12), false)
            };
            var working = state with { CampaignExpeditions = owner with
            {
                Expeditions = records,
                ProvinceSequences = new(counters) { [order.ProvinceId] = sequence }
            } };

            //The accepted CUnitData order is independent of home Base order.
            //Do not regroup it by City or feed it through PrepareDeploy.
            foreach (var packet in order.Armies)
            {
                var row = packet.Unit;
                var current = CaptureDeploymentPersonSelectionCurrent(working);
                var sourceBase = BaseLookup.Find(packet.SourceLocationId, current);
                int sourceForce = CampaignLaunchNeed(current.ReadBaseForceSlot(sourceBase), "source_Force");
                int sourceDistrict = CampaignLaunchNeed(current.ReadBaseDistrictSlot(sourceBase), "source_District");
                var source = new DeploymentSource(packet.SourceLocationId, sourceForce, sourceDistrict,
                    current.ReadBaseCurrentHexIndex(sourceBase),
                    CampaignLaunchNeed(current.ReadBaseResource(sourceBase, BaseResourceKind.Troops), "source_troops"));
                working = ArmyIdentityAllocation.Initialize(working);
                int createdId = working.ArmyIdentities!.Candidate;
                //The common method contains the full creator, troop and gold
                //writers, registration, governor, grouping and final route.
                var created = CommitDeploymentUnitsCurrent(working, source, [row], context, debitCommandBook: false);
                if (created.State is not { } afterCreation) return created;
                working = afterCreation;
                var army = working.Units.GetValueOrDefault(createdId) ?? throw CampaignLaunchMissing("created_Army_owner");
                int physical = army.NativeArmyOrigin?.NativeSlotIndex ?? throw CampaignLaunchMissing("created_Army_receiver");
                byte membership = checked((byte)campaign);
                var arena = working.ReadArmyLifecycle() ?? throw CampaignLaunchMissing("created_Army_arena");
                var slots = arena.Slots.ToArray();
                slots[physical] = slots[physical] with { MapUiByte13C = membership };
                working = (working with { Units = new(working.Units)
                    { [army.Id] = army with { NativeMapUiByte13C = membership } } })
                    .WithArmyLifecycle(arena with { Source = arena.Source with { Slots = slots } });
            }

            //32EF60 reads the current player Force, then its primary District.
            //The district selected for the make list is not this payer.
            var paymentCurrent = CaptureOwnedArmyReturnWorld(working).Context;
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(working, paymentCurrent)
                ?? throw CampaignLaunchMissing("payment_current_player");
            var payer = ForcePrimaryDistrictSource.Resolve(paymentCurrent, player);
            int payerId = CampaignLaunchNeed(payer.PhysicalSlot, "payment_primary_District");
            var district = working.Districts.GetValueOrDefault(payerId)
                ?? throw CampaignLaunchMissing("executing_District_owner");
            int books = district.OrdersRemaining ?? throw CampaignLaunchMissing("executing_District14");
            int cost = ReadCampaignLaunchCommandBooks(working);
            return CommandResult.Accept(working with { Districts = new(working.Districts)
                { [district.Id] = district with { OrdersRemaining = Math.Clamp(unchecked(books - cost), 0, 200) } } });
        }
        catch (InvalidDataException error)
        { return CommandResult.RuleUnavailable("campaign.expedition.launch", error.Message); }
    }

    internal static int ReadCampaignLaunchCommandBooks(GameState state)
    {
        var bank = state.NativeArmySimulation?.Parameters ?? throw CampaignLaunchMissing("parameter_owner");
        const string key = "campaign-expedition:launch-command-books";
        if (bank.Float32Values.ContainsKey(key)) throw CampaignLaunchMissing("command_books_type");
        if (bank.Int32Values.TryGetValue(key, out int saved)) return saved;
        //Immutable TC1A49538=1, read by6D768F and772839. A different
        //language/profile cannot inherit this selected catalog value.
        if (bank.Language == "TC" && bank.RuleVersion == SimulationRuleIdentity.ForLocale("TC")) return 1;
        throw CampaignLaunchMissing("command_books");
    }

    private static T CampaignLaunchNeed<T>(T? value, string field) where T : struct =>
        value ?? throw CampaignLaunchMissing(field);
    private static InvalidDataException CampaignLaunchMissing(string field) =>
        new("campaign.expedition.launch." + field);
}
