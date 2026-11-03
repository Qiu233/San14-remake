using San14.Core;

namespace San14.Data;

public enum RulerCaptureDecision { Release, ExecuteRequired, PlayerDecisionRequired }
public sealed record RulerCaptureDecisionReceipt(int PersonPhysical, ushort PersonId,
    int WinnerForcePhysical, int PriorForcePhysical, int WinnerRulerPhysical, ushort WinnerRulerId,
    int? FirstSample, int? ReleaseSample, int? ReleaseThreshold, byte NativeAiFlag, string Branch);
public sealed record RulerCaptureDecisionResult(RulerCaptureDecision? Decision,
    RulerCaptureDecisionReceipt? Receipt, string? UnresolvedOwner);
public sealed record RulerCaptureReleaseApplicationOwners(bool? NativePlayerForcePresent);
public sealed record RulerCaptureReleaseReceipt(RulerCaptureDecisionReceipt Decision,
    bool RelationshipWritten, byte? WinnerRelationAfter13, byte? PriorForceRelationAfter13,
    short? WinnerPopularityAfter1BE, ushort FirstAfterE2, int ReturnNativeReturn, int? ReturnCommandId, int? ReturnTravelDays);
public sealed record RulerCaptureReleaseResult(GameState? State,
    RulerCaptureReleaseReceipt? Receipt, string? UnresolvedOwner);

/// <summary>2F86F0 AI Lord decision and release relationship prefix. The
/// enclosing operation supplies its shared-current Home/Return kernel.</summary>
public static class RulerCaptureReleaseSource
{
    public const string Revision = "san14:ce-ruler-capture-release-source-20261007";
    public static RulerCaptureDecisionResult Decide(GameState state, SourceWorldContext current,
        int personPhysical, ForceReference winnerForce, TacticSourceSamplerWorld? sampler)
    {
        int winner = 0, original = 0, rulerPhysical = 0; ushort id = 0, rulerId = 0;
        int? firstSample = null, releaseSample = null, threshold = null; byte flag = 0;
        try {
            id = K(current.ReadPersonIdentity(personPhysical), "current.Lord_Person10");
            var force = current.ReadPersonForce(personPhysical) ?? throw Missing("current.Lord_original_Force22CB40");
            original = K(force.PhysicalSlot, "current.Lord_original_Force_physical");
            winner = K(winnerForce.PhysicalSlot, "current.Lord_winner_Force_physical");
            rulerId = current.ReadForceRecord(winner)?.RawRuler10 ?? throw Missing("current.Lord_winner_Force_ruler10");
            var ruler = rulerId <= 6000 ? current.ReadPersonLookupReference(rulerId) : current.ReadTransferPersonTableReference(0);
            rulerPhysical = ruler?.PhysicalSlot ?? throw Missing("current.Lord_winner_ruler_ID_lookup");
            if (Player(state, current, winnerForce)) return Done(RulerCaptureDecision.PlayerDecisionRequired, "player_806D00");
            if (K(OfficerBehaviorRules.MustReleaseAfterAiCapture(current.ReadPersonBehavior(personPhysical)), "current.Lord_flags15E"))
                return Done(RulerCaptureDecision.Release, "Lord_flags15E_bit9");
            firstSample = Draw(1);
            short ownTendency = W(personPhysical, 0x17A);
            if (ownTendency > 0 && (Rank(winner) == 10 || Rank(winner) == 1 && W(rulerPhysical, 0x17A) < 0))
                flag = firstSample < 50 ? (byte)1 : (byte)0;
            if (Rank(original) == 1) {
                if (ownTendency < 0 && rulerId != 952 && W(rulerPhysical, 0x17A) > 0 && firstSample < 50) flag = 1;
                if (Rank(original) == 1 && Rank(winner) == 1 && W(rulerPhysical, 0x176) + W(rulerPhysical, 0x16E) < 11 && firstSample < 50) flag = 1;
            }
            if (InList(rulerPhysical, personPhysical, 0xD0)) flag = 1;
            if (id == 987 && rulerId == 521 || id == 736 && rulerId is 521 or 35 or 551 ||
                id == 521 && rulerId is 736 or 987 || id == 35 && rulerId == 736) flag = 1;
            const ulong special = 0x210000000000001;
            int specialRuler = unchecked((ushort)(rulerId - 607));
            if (id == 132 && specialRuler < 58 && ((special >> specialRuler) & 1) != 0) flag = 1;
            int specialVictim = unchecked((ushort)(id - 607));
            if (specialVictim < 58 && ((special >> specialVictim) & 1) != 0 && W(rulerPhysical, 0x17A) >= 0) flag = 1;
            if (id == 932 && rulerId == 266) return Done(RulerCaptureDecision.ExecuteRequired, "special_932_266_execute");
            if (flag == 0) {
                if (Relation(winner, original) > P(SimulationParameterKeys.ArmyDestruction.RulerReleaseRelationThreshold)) flag = 2;
                if (Kin(personPhysical, rulerPhysical, 0xBC)) flag = 2;
                if (Kin(personPhysical, rulerPhysical, 0xBA)) flag = 2;
                if (Kin(personPhysical, rulerPhysical, 0xB4) || Kin(personPhysical, rulerPhysical, 0xB6)) flag = 2;
                if (Kin(rulerPhysical, personPhysical, 0xB4) || Kin(rulerPhysical, personPhysical, 0xB6)) flag = 2;
                if (InList(rulerPhysical, personPhysical, 0xC0)) flag = 2;
                if (Related276C30(rulerPhysical, personPhysical) && !InList(rulerPhysical, personPhysical, 0xD0))
                    return Done(RulerCaptureDecision.Release, "related276C30_release");
                if (flag == 0) {
                    threshold = Math.Clamp(W(rulerPhysical, 0x178) + 80, 60, 100);
                    releaseSample = Draw(10);
                    return Done(releaseSample < threshold ? RulerCaptureDecision.Release : RulerCaptureDecision.ExecuteRequired,
                        releaseSample < threshold ? "AI_release_sample" : "AI_execute_sample");
                }
            }
            return Done(flag == 2 ? RulerCaptureDecision.Release : RulerCaptureDecision.ExecuteRequired,
                flag == 2 ? "AI_related_or_Force_relation_release" : "AI_hostility_execute");
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }

        RulerCaptureDecisionResult Done(RulerCaptureDecision decision, string branch) => new(decision,
            new(personPhysical, id, winner, original, rulerPhysical, rulerId, firstSample, releaseSample, threshold, flag, branch), null);
        int Draw(int divisor) => TacticEffectSourceSamplerMath.Sample(100, divisor, id, rulerId, 0, 0,
            sampler ?? throw Missing("current.Lord_World450_45C"));
        short W(int physical, int field) => K(RulerCaptureReleaseInputs.ReadCurrentWord(state, physical, field), $"current.Lord_Person{field:X}");
        int P(string key) => K(ArmyDestructionParameterSeed.ReadCurrent(state, key), $"current.Lord_parameter{key}");
        byte Rank(int force) => K(current.ReadMoraleWaveForceRank47(force), "current.Lord_Force47");
        byte Relation(int a, int b) => ReadRelation(state, current, a, b);
        ushort R(int physical, int field) => ReadRelationWord(state, physical, field);
        bool Valid(int physical) => K(current.ReadPersonNativeValidity(physical), "current.Lord_relation_Person_validity");
        ushort Id(int physical) => K(current.ReadPersonIdentity(physical), "current.Lord_relation_Person10");
        bool Kin(int a, int b, int field)
        {
            if (!Valid(a) || !Valid(b) || Id(a) == Id(b)) return false;
            ushort value = R(a, field);
            return value != 0 && value == (field == 0xBC ? R(b, field) : Id(b));
        }
        bool InList(int a, int b, int offset)
        {
            if (!Valid(b)) return false;
            ushort other = Id(b);
            for (int i = 0; i < 8; i++) if (R(a, offset + i * 2) == other) return true;
            return false;
        }
        bool Related276C30(int a, int b) => PersonKinshipCurrent.Read(state, current, a, b);
    }

    public static RulerCaptureReleaseResult Release(GameState state, SourceWorldContext current,
        RulerCaptureDecisionResult decision, NativeMapSeasonDate returnCommandDate,
        RulerCaptureReleaseApplicationOwners application,
        Func<GameState, int, PersonReturnCommandResult>? returnHome = null,
        Func<GameState, GameState>? presentation = null)
    {
        try {
            if (decision.Decision != RulerCaptureDecision.Release || decision.Receipt is not { } receipt)
                throw Missing(decision.UnresolvedOwner ?? (decision.Decision == RulerCaptureDecision.PlayerDecisionRequired
                    ? "branch.Lord_player_806D00_decision" : "branch.Lord_execute_27F820_295EE0"));
            var working = state;
            //32EFA0 returns the actual player Force; its reached22E930
            //primary-District call is not a TaskScene/service-presence gate.
            if (K(application.NativePlayerForcePresent, "current.Lord_player_Force_validity"))
                working = (presentation ?? throw Missing("current.Lord_release_presentation_history"))(working);
            int person = receipt.PersonPhysical, winner = receipt.WinnerForcePhysical, original = receipt.PriorForcePhysical;
            BindCurrent(working, current);
            bool relationWritten = false; byte? afterA = null, afterB = null; short? fame = null;
            if (K(current.ReadAllegianceDisposition(person), "current.Lord_allegiance_disposition") >= P(SimulationParameterKeys.ArmyDestruction.RulerReleasePersonRelationThreshold) && P(SimulationParameterKeys.ArmyDestruction.RulerReleaseRelationGain) != 0 &&
                K(current.ReadForceNativeValidity(new(winner)), "current.Lord_winner_Force_validity") &&
                K(current.ReadForceNativeValidity(new(original)), "current.Lord_original_Force_validity") && winner != original) {
                if (winner is >= 46 and <= 50 || original is >= 46 and <= 50) throw Missing("branch.Lord_release_special_Force27B0F0");
                int change = P(SimulationParameterKeys.ArmyDestruction.RulerReleaseRelationGain);
                afterA = (byte)Math.Clamp(unchecked(ReadRelation(working, current, winner, original) + change), 0, 200);
                afterB = (byte)Math.Clamp(unchecked(ReadRelation(working, current, original, winner) + change), 0, 200);
                working = WriteForce(working, winner, f => f with { Relations13 = ChangedRelations(f, original, afterA.Value) });
                working = WriteForce(working, original, f => f with { Relations13 = ChangedRelations(f, winner, afterB.Value) });
                current.SetRulerReleaseRelation13(winner, original, afterA); current.SetRulerReleaseRelation13(original, winner, afterB);
                relationWritten = true;
            }
            if (K(current.ReadForceNativeValidity(new(winner)), "current.Lord_popularity_Force_validity") && winner != 0 &&
                winner != 51 && winner is not (>= 46 and <= 50)) {
                fame = (short)Math.Clamp(unchecked(K(current.ReadRulerReleasePopularity1BE(winner), "current.Lord_winner_Force1BE") + P(SimulationParameterKeys.ArmyDestruction.RulerReleasePopularityGain)), -1000, 1000);
                working = WriteForce(working, winner, f => f with { Development1BE = fame });
                current.SetRulerReleasePopularity1BE(winner, fame);
            }
            if (returnHome is not null)
            {
                var applied = returnHome(working, person);
                working = applied.State ?? throw Missing(applied.UnresolvedOwner ?? "current.Lord_release_Home_operation");
                ushort destination = K(current.ReadPersonFirstLocation(person), "current.Lord_release_Home_E2");
                return new(working, new(receipt, relationWritten, afterA, afterB, fame, destination,
                    K(applied.NativeReturn, "current.Lord_release_Home_native_result"), applied.CommandId,
                    applied.TravelDays), null);
            }
            ushort second = K(current.ReadPersonSecondLocation(person), "current.Lord_release_PersonE4");
            ushort first = K(current.ReadPersonFirstLocation(person), "current.Lord_release_PersonE2");
            ushort atBase = second is >= 1 and <= 61 ? second : (ushort)0;
            if (atBase == first) throw Missing("branch.Lord_same_home_260310_District_reselection");
            first = Math.Min(first, (ushort)61);
            current.WritePersonFirstLocation(person, first);
            var ownForce = current.ReadPersonForce(person) ?? throw Missing("current.Lord_release_Person_Force");
            if (!K(current.ReadForceNativeValidity(ownForce), "current.Lord_release_Person_Force_validity"))
                throw Missing("branch.Lord_release265250_lost_Force");
            var home = BaseLookup.Find(first, current);
            if (!K(current.ReadBaseNativeValidity(home), "current.Lord_release_Home_validity") || current.ReadBaseForceSlot(home) != ownForce.PhysicalSlot)
                throw Missing("branch.Lord_release265250_lost_or_changed_Home");
            var start = PersonReturnCommands.ResolvePersonPosition(working, current, person, returnCommandDate);
            if (start.PhysicalSlot == current.ReadBaseCurrentHexIndex(home)) {
                current.WritePersonSecondLocation(person, Math.Min(K(current.ReadBaseObjectLink(home), "current.Lord_release_Home4E"), (ushort)1061));
                working = PublishPerson(working);
                return new(working, new(receipt, relationWritten, afterA, afterB, fame, first, 0, null, null), null);
            }
            var prepared = PersonReturnCommands.Prepare(working, current, person, returnCommandDate);
            if (prepared.Packet is not { } packet) throw Missing(prepared.UnresolvedOwner ?? "current.Lord_release2EA9A0_prepare");
            var returned = PersonReturnCommands.Apply(working, current, packet);
            working = returned.State ?? throw Missing(returned.UnresolvedOwner ?? "current.Lord_release2EA9A0_apply");
            working = PublishPerson(working);
            return new(working, new(receipt, relationWritten, afterA, afterB, fame, first, K(returned.NativeReturn, "current.Lord_Return_native_result"),
                returned.CommandId, returned.TravelDays), null);

            GameState PublishPerson(GameState image)
            {
                if (!image.Officers.TryGetValue(receipt.PersonId, out var officer) || current.ResolveOfficerSource(receipt.PersonId) != person ||
                    officer.LocationState is not { } location || image.ReadArmyLifecycle() is not { } arena)
                    throw Missing("current.Lord_saved_Person_owner");
                return OfficerLocationSourceAdapter.PublishPosition((image with { Officers = new(image.Officers) { [receipt.PersonId] = officer with {
                    LocationId = first, Behavior = current.ReadPersonBehavior(person) } } }).WithArmyLifecycle(arena with { Indexes = (current.ReadOwnedCurrentListPool() ?? throw Missing("current.Lord_primary_pool")).ExportOwnedState() }),
                    receipt.PersonId, current);
            }
        }
        catch (InvalidDataException error) { return new(null, null, error.Message); }
        int P(string key) => K(ArmyDestructionParameterSeed.ReadCurrent(state, key), $"current.Lord_parameter{key}");
    }

    //The reached223D40/SC223FF0 leaf. Receiver gates and coefficient reads belong to the caller.
    internal static GameState ApplyCurrentPopularityAdjustment(GameState state, SourceWorldContext current,
        int force, int adjustment)
    {
        short before = K(current.ReadRulerReleasePopularity1BE(force), "current.popularity_Force1BE");
        short after = (short)Math.Clamp(unchecked((int)before + adjustment), -1000, 1000);
        GameState published = WriteForce(state, force, fields => fields with { Development1BE = after });
        current.SetRulerReleasePopularity1BE(force, after);
        return published;
    }

    public static void BindCurrent(GameState state, SourceWorldContext current)
    {
        for (int force = 0; force < 52; force++) {
            var fields = state.NativeArmySimulation?.Forces.GetValueOrDefault(force)?.NormalAftermath;
            current.SetRulerReleasePopularity1BE(force, fields?.Development1BE);
            for (int other = 0; other < 52; other++)
                current.SetRulerReleaseRelation13(force, other, fields?.Relations13 is { Length: 52 } bytes ? bytes[other] : null);
        }
    }
    internal static void BindMissingCurrent(GameState state, SourceWorldContext current)
    {
        if (current.HasCompleteRulerReleaseReceivers) return;
        for (int force = 0; force < 52; force++)
        {
            var fields = state.NativeArmySimulation?.Forces.GetValueOrDefault(force)?.NormalAftermath;
            current.BindMissingRulerReleaseFields(force, fields?.Development1BE, fields?.Relations13);
        }
    }
    private static byte ReadRelation(GameState state, SourceWorldContext current, int a, int b) =>
        current.TryReadRulerReleaseRelation13(a, b, out var value) ? K(value, "current.Lord_Force_relation13") :
        state.NativeArmySimulation?.Forces.GetValueOrDefault(a)?.NormalAftermath?.Relations13 is { Length: 52 } bytes && b is >= 0 and < 52
            ? bytes[b] : throw Missing("current.Lord_Force_relation13");
    private static ushort ReadRelationWord(GameState state, int person, int field) =>
        state.NativeArmySimulation?.Parameters.Int32Values is { } values && values.GetValueOrDefault(OwnedPersonRelationInputs.VersionKey) == 1 &&
        values.TryGetValue(OwnedPersonRelationInputs.PersonFieldKey(person, field), out int raw) && raw is >= 0 and <= 65535
            ? (ushort)raw : throw Missing($"current.Lord_relation_Person{field:X}");
    private static bool Player(GameState state, SourceWorldContext current, ForceReference force)
    {
        var player = OwnedCurrentPlayerForce.ReadCurrentReference(state, current) ?? throw Missing("current.Lord_player_Force3A");
        return player.PhysicalSlot is int id && id != 0 && id == force.PhysicalSlot;
    }
    private static GameState WriteForce(GameState state, int force, Func<NativeNormalArmyForceAftermath, NativeNormalArmyForceAftermath> write)
    {
        var native = state.NativeArmySimulation ?? throw Missing("current.Lord_saved_Force_owner");
        var row = native.Forces.GetValueOrDefault(force) ?? throw Missing("current.Lord_saved_Force_row");
        var fields = row.NormalAftermath ?? throw Missing("current.Lord_saved_Force13_1BE");
        return state with { NativeArmySimulation = native with { Forces = new(native.Forces) { [force] = row with { NormalAftermath = write(fields) } } } };
    }
    private static byte[] ChangedRelations(NativeNormalArmyForceAftermath fields, int other, byte value)
    { var bytes = fields.Relations13?.ToArray() ?? throw Missing("current.Lord_saved_Force13"); bytes[other] = value; return bytes; }
    private static T K<T>(T? value, string owner) where T : struct => value ?? throw Missing(owner);
    private static InvalidDataException Missing(string owner) => new(owner);
}

public sealed partial class SourceWorldContext
{
    private readonly Dictionary<(int, int), byte?> rulerReleaseRelations13 = [];
    private readonly Dictionary<int, short?> rulerReleasePopularity1BE = [];
    internal bool HasCompleteRulerReleaseReceivers =>
        (rulerReleasePopularity1BE.Count == 52 || RulerReleasePopularity1BEReader is not null) &&
        rulerReleaseRelations13.Count == 52 * 52;
    internal void BindMissingRulerReleaseFields(int force, short? popularity, byte[]? relations)
    {
        Check(force, 52, "Lord popularity Force");
        if (RulerReleasePopularity1BEReader is null) rulerReleasePopularity1BE.TryAdd(force, popularity);
        for (int other = 0; other < 52; other++)
            rulerReleaseRelations13.TryAdd((force, other), relations is { Length: 52 } ? relations[other] : null);
    }
    public void SetRulerReleaseRelation13(int force, int other, byte? value)
    { Check(force, 52, "Lord relation Force"); Check(other, 52, "Lord relation target"); rulerReleaseRelations13[(force, other)] = value; }
    internal bool TryReadRulerReleaseRelation13(int force, int other, out byte? value) => rulerReleaseRelations13.TryGetValue((force, other), out value);
    public byte? ReadRulerReleaseRelation13(int force, int other) => rulerReleaseRelations13.GetValueOrDefault((force, other));
    public void SetRulerReleasePopularity1BE(int force, short? value)
    { Check(force, 52, "Lord popularity Force"); rulerReleasePopularity1BE[force] = value; }
    public Func<int, short?>? RulerReleasePopularity1BEReader { get; set; }
    public short? ReadRulerReleasePopularity1BE(int force) =>
        rulerReleasePopularity1BE.TryGetValue(force, out var value) ? value : RulerReleasePopularity1BEReader?.Invoke(force);
}
