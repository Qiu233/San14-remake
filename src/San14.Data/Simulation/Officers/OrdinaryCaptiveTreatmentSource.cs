using San14.Core;

namespace San14.Data;

internal enum CaptiveTreatmentFeedback { Narrate, CopyFirst, Silent }

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>The ordinary captive wrappers share real Person operations,
    /// not the different Recruit/Search, banishment or captured-ruler commands.</summary>
    internal GameState ApplyOrdinaryCaptiveTreatmentCurrent(GameState state,
        SourceWorldContext current, int person, BaseReference home,
        ArmyReturnCaptiveTreatment choice, NativeMapSeasonDate date, long revision,
        Func<GameState, int> readDay, Func<TacticSourceSamplerWorld> readSampler,
        List<CampaignEffectRequest> effects, CaptiveTreatmentFeedback feedback = CaptiveTreatmentFeedback.Narrate,
        Action<TalentCommandPresentationRequest>? copyFirst = null,
        Func<GameState>? readWorking = null, Action<GameState>? publishWorking = null)
        => new OrdinaryCaptiveTreatmentSource(this, state, current, home, date, revision,
            readDay, readSampler, effects, feedback, copyFirst, readWorking, publishWorking).Apply(person, choice);

    private sealed class OrdinaryCaptiveTreatmentSource(ArmyCampaignRuleAdapter package,
        GameState input, SourceWorldContext current, BaseReference home, NativeMapSeasonDate date, long revision,
        Func<GameState, int> readDay, Func<TacticSourceSamplerWorld> readSampler,
        List<CampaignEffectRequest> effects, CaptiveTreatmentFeedback feedback = CaptiveTreatmentFeedback.Narrate,
        Action<TalentCommandPresentationRequest>? copyFirst = null,
        Func<GameState>? readWorking = null, Action<GameState>? publishWorking = null)
    {
        private GameState ownedState = input;
        private GameState state
        {
            get => readWorking?.Invoke() ?? ownedState;
            set { ownedState = value; publishWorking?.Invoke(value); }
        }
        private IReadOnlyList<NarrationVoiceRequest> lastVoices = [];
        private int Id(int p) => CaptiveNeed(current.ReadPersonIdentity(p), "Person10");
        private int P(string name) => CaptiveTreatmentParameters.Read(state, name);
        private ForceReference Captor => current.ReadForceTableReference(
            CaptiveNeed(current.ReadBaseForceSlot(home), "Base_B0")) ?? throw CaptiveMissing("captor_Force");
        private int ForceId(ForceReference force) => CaptiveNeed(force.PhysicalSlot, "Force_pointer");
        private int Ruler => (current.ReadForceRecord(ForceId(Captor))?.RawRuler10 is ushort raw
            ? raw <= 6000 ? current.ReadPersonLookupReference(raw) : current.ReadTransferPersonTableReference(0)
            : null)?.PhysicalSlot ?? throw CaptiveMissing("ruler_lookup");
        private bool Player(ForceReference force) => package.TalentForceIsPlayerCurrent(state, current, force);
        private ForceReference Original(int person) => current.ReadPersonForce(person) ?? throw CaptiveMissing("Person_Force");
        private int District => CaptiveNeed((current.ReadBaseDistrictReference(home) ?? throw CaptiveMissing("Base_District")).PhysicalSlot, "District_pointer");
        private string Text(int root, HistoryMessageOperands args)
        {
            var text = package.FormatNormalTurnReport(state, current, root, args);
            state = text.State; lastVoices = text.Voices; return text.Text;
        }
        private void Sound(int id) => effects.Add(new(null, null)
            { TalentCommandPresentation = new(TalentCommandPresentationOperation.SoundCue, id, null, null, null, null) });
        private void Narration(int person, string text)
        {
            if (lastVoices.Count > 1) throw CaptiveMissing("multiple_result_voices");
            var owner = state.Officers.GetValueOrDefault(Id(person)) ?? throw CaptiveMissing("result_speaker");
            var request = new TalentCommandPresentationRequest(TalentCommandPresentationOperation.OfficerText,
                null, Id(person), null, null, text)
            {
                Voice = lastVoices.FirstOrDefault() is { } voice ? SaveVoice(voice) : null,
                Speaker = new(Id(person), (owner.CurrentSurname ?? throw CaptiveMissing("speaker_surname")) +
                    (owner.CurrentGivenName ?? throw CaptiveMissing("speaker_given_name")), current.ReadPersonPortraitAppearance(person))
            };
            if (feedback == CaptiveTreatmentFeedback.Narrate) effects.Add(new(null, null) { TalentCommandPresentation = request });
            else if (feedback == CaptiveTreatmentFeedback.CopyFirst) copyFirst?.Invoke(request);
        }
        private void History(int category, string text, string? detail, int payload, int bit0 = 0, int bit3 = 0)
        {
            state = package.WriteHistory(state, current, new(category, District,
                current.ReadBaseCurrentHexIndex(home), readDay(state), text,
                detail ?? CampaignHistoryWriter.Supplemental(state), payload,
                MarkBit0: bit0, MarkBit3: bit3, MarkBit4: 1), effects);
        }
        private void FlushEffects()
        {
            if (effects.Count == 0) return;
            state = state with { CampaignEffectRequests = (state.CampaignEffectRequests
                ?? throw CaptiveMissing("effect_owner")).Append(CampaignEffectSource.ArmyBattleDefeat, revision, effects) };
            effects.Clear();
        }

        internal GameState Apply(int person, ArmyReturnCaptiveTreatment choice)
        {
            package._personRelationInputs.BindCurrent(() => state, current);
            current.MoraleWavePersonPostE7Reader = p => OwnedArmyMoraleWaveInputs.ReadPersonPostE7(state, current, p);
            current.RulerReleasePopularity1BEReader = force => state.NativeArmySimulation?.Forces.GetValueOrDefault(force)?.NormalAftermath?.Development1BE;
            //This is the shared ordinary-captive admission for both original
            //and replacement policies. Neither can alter the captured receiver.
            if (!CaptiveNeed(current.ReadPersonNativeValidity(person), "admission_Person_validity") ||
                CaptiveNeed(current.ReadPersonStatus(person), "admission_status") != 6)
                throw CaptiveMissing("admission_not_current_captive");
            var previousRefresh = current.TransferServiceRefresher;
            current.TransferServiceRefresher = (a, b, c) =>
                state = package.RefreshPersonnelMilestoneCurrent(state, current, a, b, c);
            try
            {
                switch (choice)
                {
                    case ArmyReturnCaptiveTreatment.Keep: Keep(person); break;
                    case ArmyReturnCaptiveTreatment.Recruit: Recruit(person); break;
                    case ArmyReturnCaptiveTreatment.Release: Release(person); break;
                    case ArmyReturnCaptiveTreatment.Execute: Execute(person); break;
                    default: throw CaptiveMissing("unknown_policy_choice");
                }
                FlushEffects();
                return state;
            }
            finally { current.TransferServiceRefresher = previousRefresh; }
        }

        private void Keep(int person)
        {
            current.WritePersonSecondLocation(person, Math.Min(CaptiveNeed(current.ReadBaseObjectLink(home), "keep_Base4E_second"), (ushort)1061));
            current.WritePersonFirstLocation(person, Math.Min(CaptiveNeed(current.ReadBaseObjectLink(home), "keep_Base4E_first"), (ushort)61));
            ClearSelfResentment(person);
        }

        //2F9FB0(...,force1,report1,null). This directly sets status/district,
        //credit, both positions and loyalty. It does not call271C20.
        private void Recruit(int person)
        {
            if ((Player(Captor) || Player(Original(person))) && feedback != CaptiveTreatmentFeedback.Silent)
            {
                Sound(Player(Captor) ? 4 : 6);
                var aftermath = current.ReadOfficerAftermath(person) ?? throw CaptiveMissing("recruit_resentment");
                int resentment = aftermath.Resentment?.TargetOfficerId ?? aftermath.SourceKnowledge?.UnmappedResentmentTarget
                    ?? throw CaptiveMissing("recruit_162");
                Narration(person, Text(resentment == Id(person) ? 0x2BB4 : 0x2B9E,
                    new(Person0: Id(person), Person1: Id(Ruler))));
            }
            if (Player(Captor) || Player(Original(person)))
            {
                int speaker = 5012;
                var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw CaptiveMissing("recruit_player");
                if (CaptiveNeed(current.ReadForceNativeValidity(player), "recruit_player_validity"))
                {
                    var adviser = package.ReadCaptureAdviserCurrent(state, current, ReadGrowthPlayerPrimaryDistrict(state, current));
                    int physical = CaptiveNeed(adviser.PhysicalSlot, "recruit_adviser_pointer");
                    if (CaptiveNeed(current.ReadPersonNativeValidity(physical), "recruit_adviser_validity")) speaker = Id(physical);
                }
                if (Player(Captor))
                {
                    string main = Text(0x2BC6, new(Person0: Id(person)));
                    string detail = Text(0x5351, new(Person0: speaker, Person1: Id(Ruler), Person2: Id(person), Force0: ForceId(Captor)));
                    History(2, main, detail, speaker, bit0: 1);
                }
                else if (Player(Original(person)))
                {
                    string main = Text(0x2BC7, new(Person0: Id(person), Force0: ForceId(Original(person)), Force1: ForceId(Captor)));
                    string detail = Text(0x5356, new(Person0: speaker, Person1: Id(Ruler), Person2: Id(person), Force0: ForceId(Captor)));
                    History(1, main, detail, 5012, bit0: 1, bit3: 1);
                }
            }
            current.WritePersonStatus(person, 4);
            var association = new List<PersonAssignmentPrecallResult>();
            PersonDistrictTransfer.Apply(person, District,
                ArmyOrdinaryReturnRuntime.CreateCurrentPersonDistrictTransfer(current, association, result =>
                { state = package.ApplyPersonAssociationRemovalReports(state, current, person, result, () => readDay(state), effects); }));
            if (association.Count != 0) state = PersonAssignmentPrecallInputs.PublishDeathTouched(state, current, person, association);
            int selector = CaptiveNeed(BattleRemakeCurrentSettings.ReadSelectorE58(state), "recruit_credit_selector");
            int stage = CaptiveNeed(BattleRemakeCurrentSettings.ReadInt32(state, selector == 0 ? 0x4C4 : 0x128), "recruit_credit_option") == 1 ? 2 : 1;
            var ranks = package._scenario.Snapshot.CreditRankParameters?.Ranks;
            if (ranks is not { Count: 7 }) throw CaptiveMissing("recruit_credit_ranks");
            int credit = Math.Clamp((int)ranks[stage].SignedThreshold, ranks[stage].SignedThreshold, ranks[6].SignedThreshold);
            Change(person, value => value with { MeritState = new(unchecked((short)credit), 0, stage, 0, 0, 0),
                ServiceStatus = OfficerServiceStatus.ServingOfficer, Role = OfficerRole.Officer });
            current.WritePersonFirstLocation(person, Math.Min(CaptiveNeed(current.ReadBaseObjectLink(home), "recruit_Base4E_first"), (ushort)61));
            current.SetMoraleWavePersonPostE7(person, 73);
            current.WritePersonSecondLocation(person, Math.Min(CaptiveNeed(current.ReadBaseObjectLink(home), "recruit_Base4E_second"), (ushort)1061));
            Change(person, value => value with { Appointment = OfficerAppointment.Synchronize(73) });
            state = package.ApplyRecruitInitialLoyaltyCurrent(state, current, person, new(Ruler));
            var native = state.NativeArmySimulation ?? throw CaptiveMissing("recruit_year_owner");
            var values = new Dictionary<string, int>(native.Parameters.Int32Values)
                { [OwnedArmyGovernorComparatorInputs.PersonWord166Key(person)] = Math.Clamp((int)date.Year, 0, 350) };
            state = state with { NativeArmySimulation = native with { Parameters = native.Parameters with { Int32Values = values } } };
            current.WriteArmyReturnPersonFlags196(person, (ushort)(CaptiveNeed(current.ReadArmyReturnPersonFlags196(person), "recruit_flags15E") | 1));
            ClearSelfResentment(person);
        }

        //2FA720 has no2704F0 relation decrement. Its27F820 consequences,
        //rank-scaled popularity and full295EE0 remain in source order.
        private void Execute(int person)
        {
            if ((Player(Original(person)) || Player(Captor)) && feedback != CaptiveTreatmentFeedback.Silent) Sound(24);
            ClearSelfResentment(person);
            state = package.ApplyRulerExecutionConsequencesCurrent(state, current, person, Captor);
            int force = ForceId(Captor);
            if (CaptiveNeed(current.ReadForceNativeValidity(Captor), "execute_Force_validity") && force != 0 && force is not (>= 46 and <= 51))
            {
                byte status = CaptiveNeed(current.ReadPersonStatus(person), "execute_status");
                int rank = 0;
                if (status != 1 && !PersonAtBandBase(person) &&
                    (CaptiveNeed(current.ReadArmyReturnPersonFlags196(person), "execute_flags") & 0x4000) == 0)
                    rank = CaptiveNeed(state.Officers.GetValueOrDefault(Id(person))?.MeritState?.RankStageId, "execute_credit_rank");
                int loss = status == 1 ? P("execute-ruler-popularity-loss") : unchecked(P("execute-popularity-per-rank") * Math.Max(1, rank));
                state = RulerCaptureReleaseSource.ApplyCurrentPopularityAdjustment(state, current, force, unchecked(-loss));
            }
            var pool = current.ReadOwnedCurrentListPool() ?? throw CaptiveMissing("execute_current_pool");
            //24 precedes295EE0's independently owned death reports. Do not
            //defer it behind those batches or behind the next captive.
            FlushEffects();
            TalentCommandPresentationRequest? copied = null;
            var result = new PersonDeathSource(package, state, current, pool,
                copyInitialText: feedback == CaptiveTreatmentFeedback.CopyFirst ? value => copied = value : null)
                .ApplyCombatDeath(person, new(Ruler), 2, true, feedback == CaptiveTreatmentFeedback.Narrate, true, false);
            state = result.State;
            if (copied is not null) copyFirst?.Invoke(copied);
            //A real ruler-choice suspension is not silently treated as a
            //completed batch. The enclosing return keeps its failure boundary.
            if (result.NativeReturn is null && feedback == CaptiveTreatmentFeedback.Narrate)
                throw CaptiveMissing("execute_pending_death_continuation");
        }

        //2FA900(...report1,null) supplies its own narration/history, then
        //2704F0 notification0, popularity and2E7890 notification0.
        private void Release(int person)
        {
            if ((Player(Captor) || Player(Original(person))) && feedback != CaptiveTreatmentFeedback.Silent)
                Narration(person, Text(0x2C2E, new(Person0: Id(person), Person1: Id(Ruler))));
            if (Player(Captor)) History(0, Text(0x2C40, new(Person0: Id(person))), null, 5012);
            else if (Player(Original(person)))
            {
                Sound(4);
                History(0, Text(0x2C41, new(Person0: Id(person), Force0: ForceId(Captor))), null, 5012);
            }
            var pool = current.ReadOwnedCurrentListPool() ?? throw CaptiveMissing("release_current_pool");
            FlushEffects();
            state = new MonthlyCampaignMaintenanceSource(package, state, current, pool)
                .ApplyRelation(Captor, Original(person), P("release-relation-gain"), 0);
            int force = ForceId(Captor);
            if (force != 0 && force is not (>= 46 and <= 51))
                state = RulerCaptureReleaseSource.ApplyCurrentPopularityAdjustment(state, current, force, P("release-popularity-gain"));
            state = package.ApplyCaptiveReleaseCurrent(state, current, person, date, () => readDay(state), effects, readSampler(), false);
            ClearSelfResentment(person);
        }

        private bool PersonAtBandBase(int person)
        {
            int force = CaptiveNeed(current.ReadPersonOldForceSlot(person), "rank_Force");
            int first = CaptiveNeed(current.ReadPersonFirstLocation(person), "rank_E2");
            return BattleRemakeCurrentRegionFactor.IsNativeBandPerson(Id(person)) &&
                first is >= 1 and <= 61 && force is not (>= 46 and <= 50);
        }
        private void Change(int person, Func<OfficerState, OfficerState> write)
        {
            int id = Id(person);
            state = state with { Officers = new(state.Officers)
                { [id] = write(state.Officers.GetValueOrDefault(id) ?? throw CaptiveMissing("Person_owner")) } };
        }
        private void ClearSelfResentment(int person)
        {
            var before = current.ReadOfficerAftermath(person) ?? throw CaptiveMissing("self_resentment_owner");
            int target = before.Resentment?.TargetOfficerId ?? before.SourceKnowledge?.UnmappedResentmentTarget ?? throw CaptiveMissing("self_162");
            if (target != Id(person)) return;
            current.SetOfficerAftermath(person, before with { Resentment = new(0, before.Resentment?.Intensity),
                SourceKnowledge = before.SourceKnowledge is { } residue ? residue with { UnmappedResentmentTarget = null } : null });
            state = current.PublishOfficerAftermath(state, Id(person));
        }
    }
}
