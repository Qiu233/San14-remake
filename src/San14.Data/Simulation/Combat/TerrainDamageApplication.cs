using San14.Core;
using static San14.Data.TerrainDamageParameters;

namespace San14.Data;

/// <summary>Two ordered passes over one operation's actual Army registry and
/// candidate chain. The preceding native return survives invalid candidates.</summary>
internal sealed class TerrainDamageApplication
{
    private readonly ArmyCampaignRuleAdapter package;
    private readonly SourceWorldContext current;
    private readonly NativeArmyProgressStageMoment moment;
    private readonly WorldIndexContext pool;
    private readonly List<CampaignEffectRequest> requests = [];
    private readonly HashSet<int> changedPeople = [];
    private readonly ArmyTraitFeedback traits;
    private readonly ActorlessArmyDamageApplication damage;
    private GameState state;
    private bool personnelBound;

    internal TerrainDamageApplication(ArmyCampaignRuleAdapter package, GameState state,
        SourceWorldContext current, NativeArmyProgressStageMoment moment)
    {
        this.package = package; this.state = state; this.current = current; this.moment = moment;
        pool = current.ReadOwnedCurrentListPool() ?? throw Missing("shared_pool");
        traits = new(state, current, moment, requests, () => this.state, EnsurePersonnel);
        damage = new(package, current, moment, () => this.state, value => this.state = value,
            traits, EnsurePersonnel, PublishPeople, person => changedPeople.Add(person), requests, SamplerWorld);
        BindQueue();
    }

    internal GameState Run()
    {
        var candidates = current.CreateNearbyTemporary();
        try
        {
            SelectCandidates(candidates);
            //32D6E0 is reached once after the first pass, including when no
            //candidate survives. This is the actual pre-writer World hour.
            int hour = moment.PreWriterThirdDate.Hour;
            bool previousReturn = false;
            var node = current.ReadSkillTemporaryHead(candidates) ?? throw Missing("candidate_head");
            while (node.Identity.HasValue)
            {
                var entry = current.ReadRegistryNodePayload(new(node.Identity));
                var army = Army(entry);
                if (Need(current.ReadArmyUnitNativeValidity(army), "second_Army_validity"))
                {
                    int wounded = Need(current.ReadArmyReturnResourceFields(army)?.Wounded18, "pre_wounded18");
                    if (!previousReturn && wounded != 0)
                    {
                        ushort rawHex = Need(current.ReadArmyUnitRawHex2A(army), "pre_wounded_Army2A");
                        int sample = Sample(P(WoundedSamplerBound), rawHex, hour, 3, 0, 0);
                        int loss = unchecked(sample + P(WoundedBaseLoss));
                        if (loss > 0)
                        {
                            int before = Need(current.ReadArmyReturnResourceFields(army)?.Wounded18, "fresh_wounded18");
                            int after = Math.Clamp(unchecked(before - loss), 0, 20000);
                            current.WriteArmyReturnWounded(Need(army.PhysicalSlot, "pre_wounded_Army_pointer"), (ushort)after);
                            state = ArmyReturnImagePublication.Publish(state, current, []);
                        }
                    }
                    ushort readyHex = Need(current.ReadArmyUnitRawHex2A(army), "ready_Army2A");
                    int ready = unchecked(Sample(P(ReadySamplerBound), 1, readyHex, hour, 5, 0) + P(ReadyBaseLoss));
                    int reduction = 0;
                    var commander = Commander(army);
                    if (PersonValid(commander) && Need(BattleRemakeCurrentDamageBodyQueries.ReadFirstMatchingPermanentEffect(
                        current, Need(commander.PhysicalSlot, "effect56_Person_pointer"), 56), "effect56_first_match"))
                    {
                        int divisor = P(PermanentEffect56Divisor);
                        if (divisor == 0 || ready == int.MinValue && divisor == -1)
                            throw Missing("effect56_signed_division");
                        reduction = ready / divisor;
                    }
                    int requested = unchecked(ready - reduction);
                    previousReturn = damage.Apply(army, requested, target => requests.Add(new(null, null)
                    { TerrainDamagePresentation = new(TerrainDamagePresentationOperation.ArmyTargetViewRefresh, ArmyId(target), null) }));
                    //This is a per-item call, even after native true/Full,
                    //with zero/negative amounts or a repeated pending target.
                    requests.Add(new(null, null) { TerrainDamagePresentation =
                        new(TerrainDamagePresentationOperation.ArmyGrossDamage, ArmyId(army), requested) });
                }
                node = current.ReadHexListNodeNext(node) ?? throw Missing("candidate_next");
            }
        }
        finally
        {
            current.ClearNearbyTemporary(candidates, false);
            current.ClearNearbyTemporary(candidates, true);
        }
        state = ArmyReturnImagePublication.Publish(state, current, []);
        PublishPeople();
        if (personnelBound) state = state with { PersonnelCommands = current.ReadOwnedPersonCommandManager()
            ?? throw Missing("Person_manager_publication") };
        if (requests.Count != 0) state = state with { CampaignEffectRequests =
            (state.CampaignEffectRequests ?? throw Missing("effect_owner"))
                .Append(CampaignEffectSource.TerrainDamage, moment.CalendarCycleOrdinal, requests) };
        state = OwnedArmyLifecycleTargets.PublishCurrent(state.WithArmyLifecycle(
            OwnedArmyDestructorFlags.PublishCurrent(state.ReadArmyLifecycle()!, current) with { Indexes = pool.ExportOwnedState() }), current);
        return state with { ArmyMapOccupancy = current.ExportArmyMapOccupancy(),
            PendingCombatSettlements = current.OwnedDestructionQueue ?? throw Missing("manager140_publication") };
    }

    private void SelectCandidates(HexListHandle candidates)
    {
        var descriptor = current.ReadRegistryDescriptor(RegistryKind.Army) ?? throw Missing("manager60");
        var node = current.ReadRegistryHead(descriptor) ?? throw Missing("manager60_head");
        while (node.Identity.HasValue)
        {
            var army = Army(current.ReadRegistryNodePayload(node));
            if (Need(current.ReadArmyUnitNativeValidity(army), "first_Army_validity") &&
                !Need(OwnedArmyDestructionCurrent.ReadArmyExcluded233560(state, current,
                    Need(army.PhysicalSlot, "first_Army_pointer")), "first_Army_excluded"))
            {
                _ = Terrain(army); //The first22B8C0 return is unused.
                if (Terrain(army).PhysicalSlot == 7)
                {
                    var person = Commander(army);
                    if (!PersonValid(person) || !HasPersonality77(Need(person.PhysicalSlot, "immunity_Person_pointer")))
                        current.AppendNearbyArmyTemporary(candidates, army);
                }
            }
            node = current.ReadRegistryNodeNext(node) ?? throw Missing("manager60_next");
        }
    }

    private SurfaceTypeReference Terrain(ArmyUnitReference army)
    {
        ushort raw = Need(current.ReadArmyUnitRawHex2A(army), "terrain_Army2A");
        var hex = current.ReadHexTableReference(raw < 48400 ? raw : 0) ?? throw Missing("terrain_Hex_pointer");
        int slot = 0;
        if (hex.PhysicalSlot.HasValue)
        {
            byte code = Need(current.ReadHexSurfaceCode(hex), "terrain_Hex10");
            if (code <= 30) slot = code;
        }
        return current.ReadSurfaceTypeTableReference(slot) ?? throw Missing("terrain_lookup_pointer");
    }
    private bool HasPersonality77(int physical)
    {
        for (int index = 0; index < 9; index++)
            if (Need(TacticRemakeCurrentTraits.ResolveCommanderPersonalityLookup(current, physical, index), "personality77_raw_key") == 77)
                return true;
        return false;
    }
    private TransferPersonReference Commander(ArmyUnitReference army)
    {
        ushort raw = Need(current.ReadArmyUnitCommanderId(army), "Commander12");
        return (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0))
            ?? throw Missing("Commander_pointer");
    }
    private bool PersonValid(TransferPersonReference person) => person.PhysicalSlot is int physical &&
        Need(current.ReadPersonNativeValidity(physical), "Person_validity");
    private static ArmyUnitReference Army(RegistryObjectReference? entry) => entry is null ? new(null) :
        entry.Kind == RegistryKind.Army ? new(entry.PhysicalSlot) : throw Missing("candidate_payload_kind");
    private int ArmyId(ArmyUnitReference army)
    {
        var arena = state.ReadArmyLifecycle() ?? throw Missing("Army_lifecycle");
        var receiver = arena.Receivers.Find(arena.Slots[Need(army.PhysicalSlot, "Army_pointer")].ReceiverId)
            ?? throw Missing("Army_receiver");
        return receiver.ActiveUnitId ?? receiver.PendingImage?.Id ?? throw Missing("Army_canonical_id");
    }
    private int P(string key) => Read(state, key);
    private int Sample(int bound, int divisor, int a, int b, int c, int d) => bound < 2 ? 0 :
        TacticEffectSourceSamplerMath.Sample(bound, divisor, a, b, c, d, SamplerWorld());
    private TacticSourceSamplerWorld SamplerWorld() => package.ReadCurrentEnvironmentSampler(state, moment, 24);
    private void BindQueue() => current.BindOwnedDestructionQueue(state.PendingCombatSettlements ?? throw Missing("manager140"), state);
    private void EnsurePersonnel()
    {
        if (personnelBound) return;
        if (current.ReadOwnedPersonCommandManager() is null)
        {
            if (state.PersonnelCommands is null) throw Missing("Person_manager");
            PersonReturnCommands.BindCurrent(state, current);
        }
        personnelBound = true;
    }
    private void PublishPeople()
    {
        if (changedPeople.Count == 0) return;
        var people = new Dictionary<int, OfficerState>(state.Officers);
        foreach (int physical in changedPeople)
        {
            int id = Need(current.ReadPersonIdentity(physical), "Person_publication_identity");
            var person = people.GetValueOrDefault(id) ?? throw Missing("Person_publication_owner");
            if (person.TraitActivation is not { DurationSlotCount: 9, ActiveSlots: not null }) throw Missing("full9_activation_owner");
            var activation = OfficerTraitActivationSourceAdapter.Read(current, physical);
            if (activation is not { DurationSlotCount: 9, ActiveSlots: not null }) throw Missing("lossless_activation");
            people[id] = person with { TraitActivation = activation };
        }
        state = state with { Officers = people };
    }
    private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("army.progress.terrain-damage." + field);
}
