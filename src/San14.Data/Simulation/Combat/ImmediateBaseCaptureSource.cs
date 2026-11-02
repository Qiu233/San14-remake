using San14.Core;

namespace San14.Data;

internal sealed record ImmediateBaseCaptureResult(GameState State, bool IsPaused,
    bool NativeTerminal, PeriodReportEntry[] Reports);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>One actual1745B0 Full128 call. The numeric caller has
    /// already applied its damage and admission test. This operation neither
    /// inserts a129th request nor drains or re-admits the existing queue.</summary>
    internal ImmediateBaseCaptureResult ApplyImmediateBaseCaptureCurrent(GameState state,
        SourceWorldContext current, BaseCaptureRequest request, NativeMapSeasonDate date,
        TacticSourceSamplerWorld? sampler, long revision, List<CampaignEffectRequest> precedingEffects,
        ArmyMapGroupingOwner? groups = null)
    {
        if (state.BaseCaptureContinuation is { } pending &&
            (pending.SettlementIndex != -1 || pending.Operation.Request != request))
            throw ImmediateBaseMissing("occupation_owner");
        if (CaptureArmyReturnContinuationSource.Read(state) is { CaptureTail: { } returnTail } &&
            (returnTail.SettlementIndex != -1 || returnTail.Request != request))
            throw ImmediateBaseMissing("return_capture_owner");
        state = FlushNormalCombatEffects(state, revision, precedingEffects);
        var pool = current.ReadOwnedCurrentListPool() ?? throw ImmediateBaseMissing("current_pool");
        if (!pool.OwnsArmyReturnProfile() || !ReferenceEquals(pool.ArmyReturnOwner, current))
            throw ImmediateBaseMissing("current_pool_owner");
        BindActorlessDestructionInputs(state, current);
        if (current.ReadOwnedPersonCommandManager() is null)
        {
            if (state.PersonnelCommands is null) throw ImmediateBaseMissing("personnel_owner");
            PersonReturnCommands.BindCurrent(state, current);
        }
        groups ??= CreateCaptureMapGroupingOwner();
        var effects = new List<CampaignEffectRequest>();
        PeriodReportEntry[] reports = [];
        bool terminal;
        if (request.HasNoWinner)
        {
            var neutral = ApplyNoWinnerBaseCaptureCurrent(state, current, request, date, sampler, effects, groups);
            state = neutral.State;
            terminal = neutral.NativeTerminal;
        }
        else if (request.WinnerUnitId is null && request.WinnerDistrictId is not null)
        {
            //A real Base source has a District but no Commander Army. The
            //capture predicate sees Person0 and cannot invent a ruler choice.
            state = ApplyBaseWinnerCaptureCurrent(state, current, request, date, sampler, effects, groups);
            terminal = ImmediateBaseHasTerminal(state);
        }
        else if (request.Target.Kind == SettlementBaseKind.City)
        {
            var capture = new OwnedCityCaptureStage(this).TryApplyCurrent(state, current, request, date, sampler, groups);
            state = capture.State ?? throw ImmediateBaseMissing(capture.UnresolvedOwner ?? "City2851B0");
            terminal = capture.NativeTerminal || ImmediateBaseHasTerminal(state);
            if (!terminal && state.BaseCaptureContinuation is null && !AutomaticEmperorCaptureLifecycle.HasCaptureTail(state) &&
                CaptureArmyReturnContinuationSource.Read(state) is null)
                reports = (capture.Receipt ?? throw ImmediateBaseMissing("City_completed_receipt")).Reports;
        }
        else if (request.Target.Kind == SettlementBaseKind.Gate)
        {
            var capture = new OwnedGateCaptureStage(this).TryApplyCurrent(state, current, request, date, sampler, groups);
            state = capture.State ?? throw ImmediateBaseMissing(capture.UnresolvedOwner ?? "Gate2851B0");
            terminal = capture.NativeTerminal || ImmediateBaseHasTerminal(state);
            if (!terminal && state.BaseCaptureContinuation is null && !AutomaticEmperorCaptureLifecycle.HasCaptureTail(state) &&
                CaptureArmyReturnContinuationSource.Read(state) is null)
                reports = (capture.Receipt ?? throw ImmediateBaseMissing("Gate_completed_receipt")).Reports;
        }
        else throw ImmediateBaseMissing("target_kind");
        if (state.BaseCaptureContinuation is { } suspended &&
            (suspended.SettlementIndex != -1 || suspended.Operation.Request != request))
            throw ImmediateBaseMissing("suspended_occupation_owner");
        state = AppendImmediateCombatEffects(state, revision, effects);
        if (reports.Length != 0)
        {
            int winner = request.WinnerUnitId ?? throw ImmediateBaseMissing("report_Army_identity");
            long reportOrdinal = state.NativeClock?.UpdateOrdinal ?? throw ImmediateBaseMissing("report_native_clock");
            state = state with { NativeSubdayReports = [.. state.NativeSubdayReports ?? [],
                .. reports.Select(report => new NativeSubdayReport(report.Kind, reportOrdinal, winner,
                    report.ForceId, report.LocationId ?? report.CityId, report.Amount))] };
        }
        //Reports are already published above. The returned list is diagnostic;
        //a resume publishes only its newly completed source tail once.
        return new(state, state.BaseCaptureContinuation is not null || AutomaticEmperorCaptureLifecycle.HasCaptureTail(state) ||
            CaptureArmyReturnContinuationSource.Read(state) is not null, terminal, reports);
    }

    internal ImmediateBaseCaptureResult ResumeImmediateBaseCaptureCurrent(GameState state,
        SourceWorldContext current, NativeMapSeasonDate date, long revision,
        List<CampaignEffectRequest> precedingEffects, ArmyMapGroupingOwner? groups = null)
    {
        var returnOwner = CaptureArmyReturnContinuationSource.Read(state);
        var operation = returnOwner?.CaptureTail?.Operation ?? state.BaseCaptureContinuation?.Operation ?? state.CityCaptureEmperorTail?.Operation;
        int position = returnOwner?.CaptureTail?.SettlementIndex ?? state.BaseCaptureContinuation?.SettlementIndex ?? state.CityCaptureEmperorTail?.SettlementIndex
            ?? state.BaseWinnerEmperorTail?.SettlementIndex ?? throw ImmediateBaseMissing("resume_occupation");
        var request = operation?.Request ?? state.BaseWinnerEmperorTail?.Request
            ?? throw ImmediateBaseMissing("resume_request");
        if (position != -1 || returnOwner is not null && returnOwner.Phase != ArmyReturnCaptivePhase.ReadyToFinishReturn ||
            state.BaseCaptureContinuation?.PendingCapturedRulerTreatment is not null ||
            state.PersonDeathContinuation is not null || state.AutomaticEmperorCapture?.PendingDecision is not null ||
            AutomaticEmperorCaptureLifecycle.IsPresentationPending(state))
            throw ImmediateBaseMissing("resume_actual_decision_pending");
        int? sampleDay = operation?.SampleDay ?? state.BaseWinnerEmperorTail?.SampleDay;
        int? sampleSalt = operation?.SampleSalt ?? state.BaseWinnerEmperorTail?.SampleSalt;
        TacticSourceSamplerWorld? sampler = sampleDay is int day && sampleSalt is int salt ? new(day, salt) : null;
        return ApplyImmediateBaseCaptureCurrent(state, current, request,
            date, sampler, revision, precedingEffects, groups);
    }

    private static bool ImmediateBaseHasTerminal(GameState state) =>
        state.NativePlayableTerminal is not null || state.CampaignTurnStop is not null;
    private static InvalidDataException ImmediateBaseMissing(string owner) => new("Base-capture.immediate." + owner);
}
