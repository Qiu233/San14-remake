using System.Buffers.Binary;
using San14.Core;

namespace San14.Data;

public sealed partial class ArmyCampaignRuleAdapter
{
    // Independent signed World458 epoch. Calendar AdvancedMonths remains the
    // number of actual calendar-writer increments, including after a date jump.
    internal const string CollectionEventMonthOffsetKey = "collection-event:elapsed-month-offset";

    /// <summary>6354E0's preparation on the selected scenario's private, normally
    /// initialized world. All writers borrow one current world and list pool;
    /// this never enters the campaign or updates application unlock history.</summary>
    internal GameState ApplyCollectionEventWorldSetup(GameState state, CollectionEventPlaybackPlan plan)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(plan);
        state = InstallCollectionEventPlayerField(state);
        var world = CaptureOwnedArmyReturnWorld(state);
        var current = world.Context;
        var pool = world.Pool;
        var working = state;
        var expired = CollectionTemporary(current);
        try
        {
            if (plan.Date is { } date)
            {
                working = ApplyCollectionEventDate(working, date);
                // Capture the real registry before portrait/directive/ruler
                // writers. A subsequently changed status does not remove an
                // entry from this source-order deferred-death list.
                foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots()
                    ?? throw CollectionSetupMissing("date.Person_registry"))
                {
                    int person = CollectionNeed(entry, "date.Person_pointer");
                    byte status = CollectionNeed(current.ReadPersonStatus(person), "date.Person_status");
                    if (status == 9) continue;
                    ushort identity = CollectionNeed(current.ReadPersonIdentity(person), "date.Person_identity");
                    if (current.ResolveOfficerSource(identity) != person ||
                        !working.Officers.TryGetValue(identity, out var owner) || owner.Lifespan?.TerminalYear is not ushort deathYear)
                        throw CollectionSetupMissing("date.Person_death_year");
                    if (deathYear < date.Year && status != 1)
                        CollectionAppend(current, expired, new(RegistryKind.Person, person));
                }
            }

            working = new AnnualPersonPortraitTransitionSource(working, current).Run();
            foreach (var change in plan.WorldChanges)
            {
                switch (change)
                {
                    case CollectionEventTransferCity city when city.CityId is >= 1 and <= 51 && city.ForceId is >= 1 and <= 51:
                    {
                        var target = current.ReadCityTableReference(city.CityId)
                            ?? throw CollectionSetupMissing("city.City_pointer");
                        var force = current.ReadForceTableReference(city.ForceId)
                            ?? throw CollectionSetupMissing("city.Force_pointer");
                        var district = ForcePrimaryDistrictSource.Resolve(current, force);
                        if (district.PhysicalSlot is not (>= 1 and <= 51))
                            throw CollectionSetupMissing("city.inactive_Force_primary_District0");
                        var ruler = CollectionForceRuler(current, city.ForceId);
                        var effects = new List<CampaignEffectRequest>();
                        var operation = CreateCaptureDistrictOperationQueries(current, () => working,
                            value => working = value,
                            working.NativeMapSeason?.ThirdDate ?? throw CollectionSetupMissing("city.ThirdDate"), effects);
                        //6354E0 calls26D2E0(Base,primary,ruler,0,0), not the
                        //26DA10 combat/resource/territory wrapper.
                        TransferForceAbsorptionBaseDistrictCurrent(current, target, district, ruler, operation,
                            suppressMaintenance: false);
                        working = PersonReturnDistrictReorganizationSource.PublishCurrent(working, current, operation);
                        working = PersonReturnDistrictTransferSource.PublishAreaAssignments(working, current,
                            operation.ChangedAreas, operation.ChangedGates);
                        working = BaseCapturePublicationSource.Publish(working, current, pool, projectOwnedRoles: true);
                        if (effects.Count != 0)
                            throw CollectionSetupMissing("city.assignment_presentation_consumer");
                        break;
                    }
                    case CollectionEventReplaceRuler replacement when replacement.OfficerId is >= 1 and <= 6000 && replacement.ForceId is >= 1 and <= 51:
                    {
                        int person = CollectionPerson(current, replacement.OfficerId, "ruler.Person");
                        if (CollectionNeed(current.ReadPersonOldForceSlot(person), "ruler.Person_Force") != replacement.ForceId) break;
                        int oldRuler = CollectionNeed(CollectionForceRuler(current, replacement.ForceId).PhysicalSlot, "ruler.old_Person");
                        working = new PersonDeathSource(this, working, current, pool)
                            .ApplyCollectionRulerTransfer(oldRuler, person);
                        working = CollectionDeath(working, current, pool, oldRuler);
                        break;
                    }
                    case CollectionEventSetKingdomName name when name.KingdomDefinitionId is >= 1 and <= 100 && name.ForceId is >= 1 and <= 51:
                    {
                        var definition = _repository.LoadForceNameDefinitions(_scenario.FixedDataSource.SourcePath)
                            .Catalog.Definitions[name.KingdomDefinitionId];
                        byte index = (byte)Math.Clamp(definition.PhysicalSlot, 0, 100);
                        current.SetForceKingdomNameIndex(name.ForceId, index);
                        var force = working.Forces.GetValueOrDefault(name.ForceId)
                            ?? throw CollectionSetupMissing("kingdom.Force_owner");
                        working = working with { Forces = new(working.Forces)
                            { [force.Id] = force with { KingdomNameIndex = index, Name = definition.ShortName } } };
                        break;
                    }
                    case CollectionEventAttachOfficer attachment when attachment.OfficerId is >= 1 and <= 6000 && attachment.PatronOfficerId is >= 1 and <= 6000:
                    {
                        int person = CollectionPerson(current, attachment.OfficerId, "join.Person");
                        var related = current.ReadPersonLookupReference(attachment.PatronOfficerId)
                            ?? throw CollectionSetupMissing("join.related_Person");
                        working = new PersonAppearanceSource(this, working, current, pool)
                            .ApplyCollectionJoin(person, related);
                        break;
                    }
                    //The original table consumer silently ignores out-of-range
                    //operands; unknown directive types are never interpreted.
                    case CollectionEventTransferCity or CollectionEventReplaceRuler or
                        CollectionEventSetKingdomName or CollectionEventAttachOfficer:
                        break;
                    default: throw CollectionSetupMissing("directive.type");
                }
            }

            if (plan.PlayerRulerId != 0)
            {
                var player = current.ReadCurrentPlayerForce() ?? throw CollectionSetupMissing("player.current_Force");
                int currentRuler = CollectionNeed(current.ReadMoraleWaveRuler22EAC0(player), "player.current_ruler");
                if (plan.PlayerRulerId != currentRuler)
                {
                    int person = CollectionPerson(current, plan.PlayerRulerId, "player.selected_Person");
                    byte force = CollectionNeed(current.ReadPersonOldForceSlot(person), "player.selected_Force");
                    if (force != 0)
                    {
                        var simulation = working.NativeArmySimulation ?? throw CollectionSetupMissing("player.parameter_owner");
                        if (OwnedCurrentPlayerForce.ReadRawForce3A(working) is null)
                            throw CollectionSetupMissing("player.byte3A_owner");
                        byte raw = (byte)Math.Clamp((int)force, 0, 51);
                        working = working with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with
                            { Int32Values = new(simulation.Parameters.Int32Values) { [OwnedCurrentPlayerForce.RawForce3AKey] = raw } } } };
                        current.CurrentPlayerForce = current.ReadForceTableReference(raw)
                            ?? throw CollectionSetupMissing("player.selected_Force_pointer");
                    }
                }
            }

            var node = current.ReadHexListHead(expired) ?? throw CollectionSetupMissing("death.list_head");
            while (node.Identity.HasValue)
            {
                var entry = current.ReadRegistryNodePayload(new(node.Identity))
                    ?? throw CollectionSetupMissing("death.list_payload");
                if (entry.Kind != RegistryKind.Person) throw CollectionSetupMissing("death.list_kind");
                working = CollectionDeath(working, current, pool, CollectionNeed(entry.PhysicalSlot, "death.Person"));
                node = current.ReadHexListNodeNext(node) ?? throw CollectionSetupMissing("death.list_next");
            }
            working = OwnedArmyReturnCurrentFields.PublishCurrent(working, current);
            working = OwnedArmyLifecycleDetachedInputs.PublishCurrent(working, current);
            working = PersonAssignmentPrecallInputs.PublishCurrent(working, current);
        }
        finally { CollectionRelease(current, expired); }
        return working.WithArmyLifecycle((working.ReadArmyLifecycle() ?? throw CollectionSetupMissing("lifecycle")) with
            { Indexes = pool.ExportOwnedState() });
    }

    private GameState ApplyCollectionEventDate(GameState state, CollectionEventDate date)
    {
        var season = state.NativeMapSeason ?? throw CollectionSetupMissing("date.calendar");
        var simulation = state.NativeArmySimulation ?? throw CollectionSetupMissing("date.simulation");
        var epoch = simulation.World.SamplerEpoch ?? throw CollectionSetupMissing("date.day_epoch");
        var events = state.CampaignEventWorld ?? throw CollectionSetupMissing("date.period_epoch");
        var values = new Dictionary<string, int>(simulation.Parameters.Int32Values);
        if (!values.TryGetValue(CollectionEventMonthOffsetKey, out int month))
        {
            //Fresh preparation consumes the same actual scenario serializer:
            //TC338EC0 stores450/454/458/random/45C at80/84/88/92/96.
            if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
                season.AdvancedCalendarSteps != 0 || _scenario.Snapshot.WorldParameters is not { } source ||
                source.RawBytes.Length < 92 || epoch.SourceScenarioUri != _scenario.ScenarioSource.ResourceUri ||
                epoch.SourceWorldSha256 != source.WorldSha256)
                throw CollectionSetupMissing("date.month_epoch");
            month = BinaryPrimitives.ReadInt32LittleEndian(source.RawBytes.AsSpan(88, 4));
        }
        var before = season.ThirdDate;
        int delta = unchecked((((date.Year - before.Year) * 12 - before.Month + date.Month) * 30 - before.Day) + date.Day);
        values[CollectionEventMonthOffsetKey] = unchecked(month + delta / 30);
        return state with
        {
            Date = new(date.Year, date.Month, date.Day),
            NativeMapSeason = season with { ThirdDate = new(checked((short)date.Year), checked((sbyte)date.Month), checked((sbyte)date.Day), 0) },
            CampaignEventWorld = events with { ElapsedPeriodOffset = unchecked(events.ElapsedPeriodOffset + delta / 10) },
            NativeArmySimulation = simulation with
            {
                World = simulation.World with { SamplerEpoch = epoch with { Day450 = unchecked(epoch.Day450 + delta) } },
                Parameters = simulation.Parameters with { Int32Values = values }
            }
        };
    }

    private static GameState InstallCollectionEventPlayerField(GameState state)
    {
        if (OwnedCurrentPlayerForce.ReadRawForce3A(state) is not null) return state;
        var simulation = state.NativeArmySimulation ?? throw CollectionSetupMissing("player.fresh_parameter_owner");
        if (state.NativeClock is not { UpdateOrdinal: 0, UpdatesIntoPeriod: 0 } ||
            state.NativeMapSeason is not { AdvancedCalendarSteps: 0 } ||
            simulation.Parameters.Int32Values.ContainsKey(OwnedCurrentPlayerForce.VersionKey) ||
            simulation.Parameters.Int32Values.ContainsKey(OwnedCurrentPlayerForce.RawForce3AKey))
            throw CollectionSetupMissing("player.fresh_load_owner");
        //337860 ->331F50 ->336750 ->326230 resets World.byte3A to0.
        //The subsequent static338EC0 serializer skips3A, transferring the
        //third date and then3C. This is the loaded title-preview field;
        //the new-game Person confirmation setter is not invoked here.
        return state with { NativeArmySimulation = simulation with { Parameters = simulation.Parameters with
            { Int32Values = new(simulation.Parameters.Int32Values)
                { [OwnedCurrentPlayerForce.VersionKey] = 1, [OwnedCurrentPlayerForce.RawForce3AKey] = 0 } } } };
    }

    private GameState CollectionDeath(GameState state, SourceWorldContext current, WorldIndexContext pool, int person)
    {
        var result = new PersonDeathSource(this, state, current, pool).ApplyCombatDeath(person, new(null), 0,
            destroyAssociatedArmy: false, report: false, history: false, suppressReports: true);
        if (result.NativeReturn is null) throw CollectionSetupMissing("death.native_return");
        return result.State;
    }

    private static TransferPersonReference CollectionForceRuler(SourceWorldContext current, int force)
    {
        int id = CollectionNeed(current.ReadForceRecord(force)?.RawRuler10, "Force.ruler10");
        return (id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))
            ?? throw CollectionSetupMissing("Force.ruler_pointer");
    }
    private static int CollectionPerson(SourceWorldContext current, int id, string name) =>
        CollectionNeed((id <= 6000 ? current.ReadPersonLookupReference(id) : current.ReadTransferPersonTableReference(0))?.PhysicalSlot, name);
    private static T CollectionNeed<T>(T? value, string name) where T : struct => value ?? throw CollectionSetupMissing(name);
    private static InvalidDataException CollectionSetupMissing(string name) => new("collection.event.world_setup." + name);

    private static HexListHandle CollectionTemporary(SourceWorldContext current)
    {
        bool locked = CollectionNeed(current.ReadRegistryLockEnabled(), "list.lock_enabled");
        if (locked) current.EnterRegistryLock();
        try { return current.CreateTemporaryHexList() ?? throw CollectionSetupMissing("list.descriptor"); }
        finally { if (locked) current.ExitRegistryLock(); }
    }
    private static void CollectionAppend(SourceWorldContext current, HexListHandle list, RegistryObjectReference value)
    {
        bool locked = CollectionNeed(current.ReadRegistryLockEnabled(), "list.append_lock");
        if (locked) current.EnterRegistryLock();
        try
        {
            var node = current.AllocateRegistryNode(new(list.Identity)) ?? throw CollectionSetupMissing("list.node");
            if (node.Identity.HasValue) current.WriteRegistryNodePayload(node, value);
        }
        finally { if (locked) current.ExitRegistryLock(); }
    }
    private static void CollectionRelease(SourceWorldContext current, HexListHandle list)
    {
        bool locked = CollectionNeed(current.ReadRegistryLockEnabled(), "list.release_lock");
        if (locked) current.EnterRegistryLock();
        try { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
        finally { if (locked) current.ExitRegistryLock(); }
    }

    internal sealed partial class PersonAppearanceSource
    {
        internal GameState ApplyCollectionJoin(int person, TransferPersonReference related)
        {
            if (CanJoin(related) && Join(person, related))
                _ = ObjectRegistration.Register(new(RegistryKind.Person, person), current);
            return latest;
        }
    }

    internal sealed partial class PersonDeathSource
    {
        internal GameState ApplyCollectionRulerTransfer(int oldRuler, int newRuler)
        {
            int force = Known(Force(oldRuler).PhysicalSlot, "collection_Force");
            int? doctrine = latest.Forces.GetValueOrDefault(force)?.DoctrineId;
            var retained = Temporary();
            try { ApplyRulerSuccessionTransfer(oldRuler, newRuler, retained); }
            finally { ClearRelease(retained); }
            //6354E0 does not call the separate26D140 governance/doctrine
            //tail. Force type remains independently owned after28DFB0.
            var owner = latest.Forces.GetValueOrDefault(force) ?? throw Missing("collection_Force_owner");
            return latest with { Forces = new(latest.Forces) { [force] = owner with { DoctrineId = doctrine } } };
        }
    }
}
