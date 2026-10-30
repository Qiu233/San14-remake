using San14.Core;

namespace San14.Data;

internal sealed record TalentRecruitTrialResult(bool Accepted, int SampleAfter,
    int? Probability, string Branch);

public sealed partial class ArmyCampaignRuleAdapter
{
    //Paired TC/SC recruit trial. The operation owns current, its shared pool and any
    //final publication. This query has real temporary-list allocator effects.
    internal TalentRecruitTrialResult TryRecruitCurrent(GameState state,
        SourceWorldContext current, int targetPhysical, int executorPhysical,
        int savedSample, Func<TacticSourceSamplerWorld> readSampler)
    {
        if (Seed.Language is not ("TC" or "SC")) throw RecruitMissing("trial_edition_owner");
        return new RecruitTrial(this, state, current, targetPhysical, executorPhysical,
            savedSample, readSampler).Run();
    }

    private sealed class RecruitTrial(ArmyCampaignRuleAdapter package, GameState state,
        SourceWorldContext current, int target, int executor, int savedSample,
        Func<TacticSourceSamplerWorld> readSampler)
    {
        internal TalentRecruitTrialResult Run()
        {
            var executorForce = Force(executor);
            var sovereign = Ruler(executorForce);
            var sworn = Temporary();
            HexListHandle? children = null;
            try
            {
                FillSworn(sworn);
                children = Temporary();
                FillChildren(children); //The source builds this list even though it never queries it.
                var spouse = Lookup(Word(target, 0xBA));
                _ = Lookup(Word(target, 0xB4));
                _ = Lookup(Word(target, 0xB6));
                var swornLeader = Lookup(Word(target, 0xBC));
                var targetForce = Force(target);
                int cityCount = Valid(targetForce)
                    ? K(PersonArmyCaptureSource.ReadOwnedCityCount(current, Force(target)), "target_22AB60") : 0;
                targetForce = Force(target);
                if (Loyalty() > 99 || ResentmentIdentity() == Id(P(sovereign))) return Early(false, "loyalty_or_resentment");
                if (Status(target, 1) && cityCount > 0) return Early(false, "ruler_with_city");
                if (Valid(Force(target)) && cityCount > 0 && Valid(swornLeader) && Id(target) != Id(P(swornLeader)))
                    if (Ordinal(Force(target)) == Ordinal(Force(P(swornLeader)))) return Early(false, "sworn_leader_same_serving_force");
                if (Valid(swornLeader) && (Id(P(swornLeader)) == Id(P(sovereign)) || Id(P(swornLeader)) == Id(executor)))
                    return Early(true, "sworn_leader_is_recruiter");
                if (Valid(Force(target)) && cityCount > 0)
                    foreach (int other in Payloads(sworn))
                        if (Valid(other) && Ordinal(Force(other)) == Ordinal(Force(target))) return Early(false, "sworn_member_retains_force");
                if (Valid(Force(target)) && cityCount > 0 && Valid(spouse))
                    if (Ordinal(Force(P(spouse))) == Ordinal(Force(target))) return Early(false, "spouse_retains_force");
                if (Valid(swornLeader) && Valid(Force(P(swornLeader))) && Id(target) != Id(P(swornLeader)))
                    if (Ordinal(executorForce) != Ordinal(Force(P(swornLeader)))) return Early(false, "sworn_leader_other_force");
                if (Valid(spouse) && Valid(Force(P(spouse))))
                    if (Ordinal(executorForce) != Ordinal(Force(P(spouse)))) return Early(false, "spouse_other_force");
                if (Valid(spouse) && (Id(P(spouse)) == Id(P(sovereign)) || Id(P(spouse)) == Id(executor)))
                    return Early(true, "spouse_is_recruiter");
                if (Valid(swornLeader) && Ordinal(executorForce) == Ordinal(Force(P(swornLeader))))
                    return Early(true, "sworn_leader_recruiter_force");
                if (Valid(spouse) && Ordinal(executorForce) == Ordinal(Force(P(spouse))))
                    return Early(true, "spouse_recruiter_force");
                if (RawHit(target, sovereign, false) || RawHit(target, new(executor), false)) return Early(false, "dislikes_recruiter");
                if (Status(target, 6) && Valid(swornLeader) && Ordinal(executorForce) == Ordinal(Force(P(swornLeader))))
                    return Early(true, "captive_sworn_leader_force");
                if (Status(target, 6) && Valid(spouse) && Ordinal(executorForce) == Ordinal(Force(P(spouse))))
                    return Early(true, "captive_spouse_force");
                if (Status(target, 6))
                {
                    var father = Lookup(Word(target, 0xB4));
                    if (Valid(father) && Ordinal(executorForce) == Ordinal(Force(P(father)))) return Early(true, "captive_father_force");
                    var mother = Lookup(Word(target, 0xB6));
                    if (Valid(mother) && Ordinal(executorForce) == Ordinal(Force(P(mother)))) return Early(true, "captive_mother_force");
                }
                if (K(current.ReadAllegianceDisposition(target), "disposition16C") / 5 + Loyalty() > 95)
                    return Early(false, "disposition_loyalty_threshold");

                int difficulty = Difficulty();
                int charmCoefficient = Parameter("charm-trait-multiplier");
                int charm = 0;
                if (Valid(executor))
                {
                    charm = K(new TacticRemakeCurrentAbilities(current, package._scenario, package.Seed.Language,
                        SimulationRuleIdentity.ForLocale(package.Seed.Language)).ResolveDimension(state, Id(executor), 4,
                        (force, effect) => package._baseCapacityInputs.ReadPanelModifier(state, force, effect)), "executor_current_charm");
                    int traits = K(TacticRemakeCurrentTraits.CommanderEffectSum(current, executor, 0x3F), "executor_effect63_sum");
                    charm = unchecked(unchecked(traits * charm) * charmCoefficient) / 100 + charm;
                }
                int loyalty = Loyalty();
                if (Status(target, 5)) loyalty = difficulty == 0 ? 60 : difficulty == 1 ? 65 : 70;
                else if (Status(target, 6))
                {
                    if (cityCount == 1)
                    {
                        var home = BaseLookup.Find(K(current.ReadPersonFirstLocation(target), "captive_homeE2"), current);
                        if (!Valid(targetForce) || !K(current.ReadBaseNativeValidity(home), "captive_home_validity"))
                            loyalty = unchecked(K(current.ReadAllegianceDisposition(target), "captive_disposition") + 70);
                        else
                            foreach (var entry in current.ReadRegisteredBases() ?? throw RecruitMissing("Force_foothold_registry80"))
                            {
                                var at = entry ?? throw RecruitMissing("Force_foothold_payload");
                                int force = Ordinal(targetForce);
                                if (force is < 1 or > 51 || K(current.ReadBaseForceSlot(at), "foothold_ForceB0") != force) continue;
                                ushort location = K(current.ReadBaseObjectLink(at), "foothold_4E");
                                if (unchecked((uint)(location - 52)) > 9 && location == K(current.ReadBaseObjectLink(home), "home_4E"))
                                { loyalty = Math.Min(loyalty, 70); break; }
                            }
                    }
                    else if (cityCount == 0) loyalty = unchecked(K(current.ReadAllegianceDisposition(target), "captive_disposition") + 70);
                }
                int disposition = K(current.ReadAllegianceDisposition(target), "rate_disposition16C") / 5;
                if (Status(target, 5)) disposition = 0;
                int oldAffinity = 25;
                if (Valid(targetForce)) oldAffinity = Affinity(target, Ruler(targetForce));
                int newAffinity = Affinity(target, sovereign);
                int familyBonus = 0, likePenalty = 0, dislikeBonus = 0;
                if (Valid(targetForce))
                {
                    if (Parent(target, Ruler(targetForce)) || Parent(P(Ruler(targetForce)), new(target))) familyBonus = 15;
                    if (RawHit(target, Ruler(targetForce), true)) likePenalty = 15;
                    if (RawHit(target, Ruler(targetForce), false)) dislikeBonus = 15;
                }
                int captiveBonus = Status(target, 6) ? 15 : 0;
                int sample = TacticEffectSourceSamplerMath.Sample(100, 1, Id(executor), Id(target), 0, 0,
                    readSampler() ?? throw RecruitMissing("sampler_world"));
                if (savedSample != -1) sample = savedSample;
                int playerBonus = 0;
                if (PlayerOwns(executorForce))
                {
                    int mode = Difficulty();
                    playerBonus = Parameter(mode == 0 ? "player-easy-bonus" : mode == 1 ? "player-intermediate-bonus" : "player-hard-bonus");
                }
                int probability = unchecked((Math.Max(30, charm) * 3) / 5 +
                    ((oldAffinity - newAffinity) / 5 - unchecked(((disposition + 4) / 2 + 18) * loyalty * 5) / 100) +
                    (9 - sample / 5) * 5 - likePenalty + playerBonus + sample + captiveBonus + dislikeBonus + familyBonus);
                var advantage = PersonReturnPathSource.ReadForceAdvantage(state, current, executorForce, 10, 0);
                int advantageValue = advantage.Value ?? throw RecruitMissing(advantage.UnresolvedOwner ?? "Force_advantage10");
                int advantageCoefficient = Parameter("land-advantage-multiplier");
                int applyAdvantage = Status(target, 6) || Status(target, 5) && !Valid(targetForce) ? 1 : 0;
                float multiplier = disposition == -4 ? 1.5f : disposition == -3 ? 1.3f : disposition == -2 ? 1.1f : 1f;
                int scaled = Cvtt((float)unchecked(probability + unchecked(applyAdvantage * advantageCoefficient * advantageValue)) * multiplier);
                probability = scaled < 0 ? 0 : Math.Min(scaled, 100);
                return new(sample < probability, sample, probability, "source.numeric_trial");
            }
            finally
            {
                if (children is not null) Release(children);
                Release(sworn);
            }
        }

        private TalentRecruitTrialResult Early(bool accepted, string branch) => new(accepted, savedSample, null, "source." + branch);
        private void FillSworn(HexListHandle list)
        {
            current.ClearTemporaryHexList(list);
            ushort sworn = Word(target, 0xBC);
            if (sworn is < 1 or > 6000) return;
            foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw RecruitMissing("Person100_sworn_registry"))
            {
                int person = K(entry, "sworn_registry_payload");
                if (Id(person) != Id(target) && Word(person, 0xBC) == sworn) Append(list, person);
            }
        }
        private void FillChildren(HexListHandle list)
        {
            current.ClearTemporaryHexList(list);
            foreach (int? entry in current.ReadRegisteredPersonPhysicalSlots() ?? throw RecruitMissing("Person100_children_registry"))
            {
                int person = K(entry, "children_registry_payload");
                if (Word(person, 0xB4) == Id(target) || Word(person, 0xB6) == Id(target)) Append(list, person);
            }
        }
        private HexListHandle Temporary() => current.CreateTemporaryHexList() ?? throw RecruitMissing("temporary_descriptor");
        private void Append(HexListHandle list, int person)
        { var node = current.AllocateHexListNode(list); if (node.Identity.HasValue) current.SetPersonRelationListPayload(node, new(person)); }
        private IEnumerable<int> Payloads(HexListHandle list)
        {
            var node = current.ReadHexListHead(list) ?? throw RecruitMissing("temporary_head");
            while (node.Identity.HasValue)
            {
                yield return P(current.ReadPersonRelationListPayload(node) ?? throw RecruitMissing("temporary_payload"));
                node = current.ReadHexListNodeNext(node) ?? throw RecruitMissing("temporary_next");
            }
        }
        private void Release(HexListHandle list) { current.ClearTemporaryHexList(list); current.ReleaseTemporaryHexList(list); }
        private TransferPersonReference Lookup(ushort raw) => (raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0)) ?? throw RecruitMissing("Person_lookup");
        private TransferPersonReference Ruler(ForceReference force) => Lookup(K(current.ReadForceRecord(Ordinal(force))?.RawRuler10, "Force_ruler10"));
        private ForceReference Force(int person) => current.ReadPersonForce(person) ?? throw RecruitMissing("Person_Force22CB40");
        private int Ordinal(ForceReference force) => K(current.ReadForcePhysicalSlot(force), "Force_ordinal60");
        private int P(TransferPersonReference person) => K(person.PhysicalSlot, "Person_pointer");
        private bool Valid(TransferPersonReference person) => person.PhysicalSlot is int p && Valid(p);
        private bool Valid(int person) => K(current.ReadPersonNativeValidity(person), "Person_validity");
        private bool Valid(ForceReference force) => K(current.ReadForceNativeValidity(force), "Force_validity");
        private ushort Id(int person) => K(current.ReadPersonIdentity(person), "Person_ID10");
        private bool Status(int person, byte status) => K(current.ReadPersonStatus(person), "Person_statusE6") == status;
        private ushort Word(int person, int field) => K(OwnedPersonRelationInputs.ReadCurrentRelationIdentity(state, person, field), "Person_relation_" + field.ToString("X"));
        private byte Loyalty() => K(ArmyDestructionLoyalty.ReadCurrentRawE8(state, current, target, package._governorInputs), "Person_loyaltyE8");
        private ushort ResentmentIdentity() => checked((ushort)(current.ReadOfficerAftermath(target)?.Resentment?.TargetOfficerId
            ?? current.ReadOfficerAftermath(target)?.SourceKnowledge?.UnmappedResentmentTarget ?? throw RecruitMissing("resentment162")));
        private bool RawHit(int first, TransferPersonReference second, bool like)
        {
            if (!Valid(second)) return false;
            ushort id = Id(P(second));
            for (int i = 0; i < 8; i++)
                if (K(like ? OwnedPersonRelationInputs.ReadCurrentLikeIdentity(state, first, i) : OwnedPersonRelationInputs.ReadCurrentDislikeIdentity(state, first, i), "raw_relation_slot") == id) return true;
            return false;
        }
        private bool Parent(int child, TransferPersonReference parent)
        {
            if (!Valid(child) || !Valid(parent) || Id(child) == Id(P(parent))) return false;
            return Word(child, 0xB4) != 0 && Word(child, 0xB4) == Id(P(parent)) || Word(child, 0xB6) != 0 && Word(child, 0xB6) == Id(P(parent));
        }
        private int Affinity(int first, TransferPersonReference second)
        {
            if (!Valid(first) || !Valid(second)) return 0;
            int distance = Math.Abs(K(OwnedPersonRelationInputs.ReadCurrentAffinity(state, first), "first_BE") - K(OwnedPersonRelationInputs.ReadCurrentAffinity(state, P(second)), "second_BE"));
            return Math.Min(Math.Min(distance, 150 - distance), 75);
        }
        private bool PlayerOwns(ForceReference force)
        {
            var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw RecruitMissing("current_player_Force");
            int physical = Ordinal(player);
            return physical != 0 && Ordinal(force) == physical;
        }
        private int Difficulty() => K(BattleRemakeCurrentSettings.ReadInt32(state,
            K(BattleRemakeCurrentSettings.ReadSelectorE58(state), "settings_E58") == 0 ? 0x134 : 0x28), "difficulty");
        private int Parameter(string name) => TalentRecruitParameters.Read(state, name);
        private static int Cvtt(float value) => !float.IsFinite(value) || value < int.MinValue || value >= 2147483648f ? int.MinValue : (int)value;
        private static T K<T>(T? value, string owner) where T : struct => value ?? throw RecruitMissing(owner);
    }
    private static InvalidDataException RecruitMissing(string owner) => new("talent-recruit." + owner);
}

internal static class TalentRecruitParameters
{
    //TC/SC loader-zero scalars. Each edition's own PE virtual data tail
    //initializes these integers; the surrounding scalar owner has only reads,
    //with no writes or escaped address in the qualified offline references.
    //No executable, original process or proof scan is used by the game.
    internal static readonly IReadOnlyDictionary<string, int> LoaderDefaults = new Dictionary<string, int>
    {
        ["player-hard-bonus"] = 0,
        ["initial-loyalty-signed-threshold"] = 0,
    };

    internal static string Key(string name) => name switch
    {
        "charm-trait-multiplier" => TalentSearchSource.Trait63ScaleKey,
        "search-join-ability-merit" => "talent-recruit:employ-success-ability-count-scale",
        "search-join-merit-base" => "talent-recruit:employ-success-merit-base",
        _ => "talent-recruit:" + name
    };
    internal static int Read(GameState state, string name)
    {
        string key = Key(name);
        if (state.NativeArmySimulation?.Parameters is { Language: "TC" or "SC" } bank &&
            !bank.Float32Values.ContainsKey(key))
        {
            if (bank.Int32Values.TryGetValue(key, out int value)) return value;
            //Saved integers take precedence. Both supported editions have
            //independently proved immutable initialization values for these keys.
            if (LoaderDefaults.TryGetValue(name, out value)) return value;
        }
        throw new InvalidDataException("talent-recruit.current_parameter_" + key);
    }
}
