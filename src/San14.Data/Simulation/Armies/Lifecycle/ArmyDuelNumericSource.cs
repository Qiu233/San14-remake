using San14.Core;

namespace San14.Data;

public enum ArmyDuelParticipantOutcome { Undetermined = -1, Won = 0, Lost = 1, Draw = 2 }
public enum ArmyDuelPlayerOutcome { NotParticipating = -1, Won = 0, Lost = 1, Draw = 2 }
public enum ArmyDuelAftermath { NotEvaluated = -1, None = 0, Death = 1, Capture = 2 }
public enum ArmyDuelRoundAction { Unprepared = -1, Ordinary = 0, Critical = 1, Evaded = 2, Counter = 3 }

public sealed record ArmyDuelRound(int Number, int Damage, ArmyDuelRoundAction Action);
public sealed record ArmyDuelSideNumericResult(int PersonId, ArmyDuelParticipantOutcome Outcome,
    byte ReadinessBefore, byte ReadinessAfter, bool WriteReadiness,
    int PreparedInitialReadiness, int PreparedFinalReadiness,
    IReadOnlyList<int> ReadinessTrajectory, IReadOnlyList<ArmyDuelRound> Rounds);
public sealed record ArmyDuelNumericResult(ArmyDuelPlayerOutcome PlayerOutcome, bool InstantDefeat,
    ArmyDuelAftermath Aftermath, int? FirstStrikerPersonId, int? WinnerPersonId, int? LoserPersonId,
    ArmyDuelSideNumericResult Actor, ArmyDuelSideNumericResult Opponent,
    PersonArmyCaptureReceipt? CaptureReceipt);

/// <summary>TC373490 and its numeric consumers. The current context and its
/// primary list pool belong to the caller's uncommitted operation. No saved
/// Person, Army, service or random-stream state is published here.</summary>
public static partial class ArmyDuelNumericSource
{
    public static ArmyDuelNumericResult Evaluate(GameState state, SourceWorldContext current,
        ArmyDuelSelectedPair pair, NativeMapSeasonDate date, TacticSourceSamplerWorld sampler,
        Func<int, int, int?> effectiveAbility, CaptureApplicationOwners captureOwners)
        => new Evaluation(state, current, pair, date, sampler, effectiveAbility, captureOwners).Run();

    private sealed partial class Evaluation
    {
        private readonly GameState state;
        private readonly SourceWorldContext current;
        private readonly ArmyDuelSelectedPair pair;
        private readonly NativeMapSeasonDate date;
        private readonly TacticSourceSamplerWorld sampler;
        private readonly Func<int, int, int?> effectiveAbility;
        private readonly CaptureApplicationOwners captureOwners;
        private readonly Side[] sides;
        internal Evaluation(GameState state, SourceWorldContext current, ArmyDuelSelectedPair pair,
            NativeMapSeasonDate date, TacticSourceSamplerWorld sampler, Func<int, int, int?> effectiveAbility,
            CaptureApplicationOwners captureOwners)
        {
            this.state = state; this.current = current; this.pair = pair; this.date = date;
            this.sampler = sampler; this.effectiveAbility = effectiveAbility; this.captureOwners = captureOwners;
            sides = [new(pair.ActorPersonId, pair.ActorReadiness), new(pair.OpponentPersonId, pair.OpponentReadiness)];
        }
        private ArmyDuelPlayerOutcome playerOutcome = ArmyDuelPlayerOutcome.NotParticipating;
        private ArmyDuelAftermath aftermath = ArmyDuelAftermath.NotEvaluated;
        private bool instant;
        private int? firstStriker, winner, loser;
        private PersonArmyCaptureReceipt? captureReceipt;

        internal ArmyDuelNumericResult Run()
        {
            int actor = Person(pair.ActorPersonId), opponent = Person(pair.OpponentPersonId);
            //373490 really repeats actor validity; selection has already
            //validated both final Armies and their current commanders.
            if (!Valid(actor) || !Valid(actor)) return Finish();
            var hex = PersonReturnCommands.ResolvePersonPosition(state, current, actor, date);
            if (hex.PhysicalSlot is not int actorHex) return Finish();
            int draw = Sample(100, Id(actor), Id(opponent), actorHex, 0);
            int actorStrength = Ability(actor, 1), opponentStrength = Ability(opponent, 1);
            int leading = actorStrength > opponentStrength ||
                actorStrength == opponentStrength && draw < P("equal-strength-actor-percent") ? 0 : 1;
            int following = 1 - leading;
            int first = leading == 0 ? actor : opponent, second = following == 0 ? actor : opponent;
            firstStriker = Id(first);
            sides[leading].PreparedInitial = Readiness(first);
            sides[following].PreparedInitial = Readiness(second);
            if (!InstantDefeat(first, second))
            {
                sides[following].Timeline[0] = Math.Clamp(unchecked(Readiness(second) - (Ability(first, 1) - Ability(second, 1))), 0, 100);
                sides[leading].Timeline[0] = sides[leading].PreparedInitial;
                var firstParameters = new int[4];
                var secondParameters = new int[4];
                PrepareParameters(first, second, firstParameters, secondParameters);
                PrepareRounds(first, second, leading, following, firstParameters, secondParameters);
                for (int round = 1; round <= 5; round++)
                {
                    int nextSecond = Math.Clamp(unchecked(sides[following].Timeline[round - 1] - sides[leading].Damage[round]), 0, 100);
                    int nextFirst = Math.Clamp(unchecked(sides[leading].Timeline[round - 1] - sides[following].Damage[round]), 0, 100);
                    sides[following].Timeline[round] = nextSecond;
                    sides[leading].Timeline[round] = nextFirst;
                    if (nextSecond == 0 || nextFirst == 0 || round == 5)
                    {
                        sides[leading].PreparedFinal = nextFirst;
                        sides[following].PreparedFinal = nextSecond;
                        break;
                    }
                }
                //These are plans for the real373490 writers, not publication.
                sides[0].Write = sides[1].Write = true;
                sides[0].After = (byte)Math.Clamp(sides[0].PreparedFinal, 0, 100);
                sides[1].After = (byte)Math.Clamp(sides[1].PreparedFinal, 0, 100);
                int firstRemaining = sides[leading].PreparedFinal, secondRemaining = sides[following].PreparedFinal;
                if (firstRemaining == secondRemaining)
                {
                    sides[0].Outcome = sides[1].Outcome = ArmyDuelParticipantOutcome.Draw;
                    playerOutcome = pair.PlayerSide is 0 or 1 ? ArmyDuelPlayerOutcome.Draw : ArmyDuelPlayerOutcome.NotParticipating;
                    return Finish();
                }
                SetWinner(firstRemaining > secondRemaining ? leading : following);
            }
            else
            {
                instant = true;
                SetWinner(leading);
                sides[following].Write = true;
                sides[following].After = 0;
                sides[following].PreparedFinal = sides[following].Timeline[0] = 0;
                //375CB0 precedes374030 only on the instantaneous branch.
                if (DeathChance(second, first, true))
                {
                    aftermath = ArmyDuelAftermath.Death;
                    return Finish();
                }
            }
            int defeated = Person(loser!.Value), victor = Person(winner!.Value);
            aftermath = ResolveAftermath(defeated, victor);
            return Finish();
        }

        private void SetWinner(int index)
        {
            sides[index].Outcome = ArmyDuelParticipantOutcome.Won;
            sides[1 - index].Outcome = ArmyDuelParticipantOutcome.Lost;
            winner = sides[index].PersonId; loser = sides[1 - index].PersonId;
            playerOutcome = pair.PlayerSide == index ? ArmyDuelPlayerOutcome.Won :
                pair.PlayerSide == 1 - index ? ArmyDuelPlayerOutcome.Lost : ArmyDuelPlayerOutcome.NotParticipating;
        }

        private ArmyDuelNumericResult Finish() => new(playerOutcome, instant, aftermath,
            firstStriker, winner, loser, sides[0].Freeze(), sides[1].Freeze(), captureReceipt);

        private bool InstantDefeat(int first, int second)
        {
            if (HasEffect(second, 31) && Age(second) >= P("effect31-age-base") || HasEffect(second, 30)) return false;
            float firstStrength = Ability(first, 1), secondStrength = Ability(second, 1);
            int effect = SumEffect(first, 30);
            int ageBonus = AgeBonus(first);
            float factor = 1.5f;
            int difficulty = Setting(Advanced() ? 0x134 : 0x28);
            var force = current.ReadPersonForce(first) ?? throw Missing("instant_Force_pointer");
            if (!Need(current.ReadForceNativeValidity(force), "instant_Force_validity")) return false;
            if (difficulty == 0) factor = 1.0f;
            if (IsPlayerForce(force)) factor = difficulty switch { 1 => 0.8f, 2 => 0.6f, 3 => 0.5f, _ => factor };
            if (firstStrength < P("instant-minimum-strength")) return false;
            //SSE operations and the intervening cvttss2si are kept separate.
            float numerator = firstStrength * firstStrength;
            float otherSquared = secondStrength * secondStrength;
            float chance = ((37.0f - (secondStrength * 74.0f * secondStrength) / (numerator + otherSquared)) * numerator / otherSquared) * factor;
            int contribution = ToInt(chance * (((float)ageBonus + P("effect30-scale") * (float)effect) / 100.0f));
            int probability = Math.Clamp(ToInt((float)contribution + chance), 0, P("instant-probability-cap"));
            return Sample(100, ToInt(firstStrength), ToInt(secondStrength), 0, 0) < probability;
        }

        private void PrepareParameters(int first, int second, int[] a, int[] b)
        {
            int firstStrength = Ability(first, 1), secondStrength = Ability(second, 1);
            int scale = P("effect30-scale"), difference = unchecked(firstStrength - secondStrength);
            int firstEffect = SumEffect(first, 30), secondEffect = SumEffect(second, 30);
            int secondPercent = unchecked(secondEffect * scale);
            int firstBonus = AgeBonus(first), secondBonus = AgeBonus(second);
            int firstBase = firstStrength / P("strength-divisor"), secondBase = secondStrength / P("strength-divisor");
            int delta = difference / P("difference-divisor");
            b[1] = Math.Clamp(secondBase, 0, P("critical-cap"));
            int firstAdjusted = unchecked(firstBase + difference);
            a[1] = Math.Clamp(firstAdjusted, 0, P("critical-cap"));
            //The cached ASM uses CVTDQ2PS and DIVSS, despite the decompiler's
            //unsuffixed100.0 notation. Keep this percentage single precision.
            float secondMultiplier = (float)unchecked(secondPercent + secondBonus) / 100.0f;
            int secondAttack = unchecked(ToInt((float)unchecked(secondBase - delta) * secondMultiplier) - delta + secondBase);
            b[0] = Math.Clamp(secondAttack, 5, 100);
            float firstMultiplier = (float)unchecked(firstBonus + scale * firstEffect) / 100.0f;
            a[0] = Math.Clamp(unchecked(ToInt((float)firstBase * firstMultiplier) + firstBase), 5, 100);
            b[2] = Math.Clamp(secondBase, 0, P("following-evade-cap"));
            a[2] = Math.Clamp(firstAdjusted, 0, 30);
            a[3] = Sample(Math.Clamp(unchecked(firstBase + 1), 0, 40), Id(first), Age(first), 0, 0);
            if (difference <= P("following-counter-difference"))
                b[3] = Sample(Math.Clamp(unchecked(secondBase + 1), 0, 40), Id(second), Age(second), 0, 0);
        }

        private void PrepareRounds(int first, int second, int leading, int following, int[] a, int[] b)
        {
            if (!Valid(first) || !Valid(second)) return;
            int difference = unchecked(Ability(first, 1) - Ability(second, 1));
            int absoluteDifference = difference < 0 ? unchecked(-difference) : difference;
            for (int round = 1; round <= 5; round++)
            {
                int minimum = P("damage-minimum"), maximum = P("damage-maximum");
                int firstDamage = minimum;
                if (a[0] > minimum)
                    firstDamage = Math.Clamp(unchecked(Sample(unchecked(a[0] - minimum + 1), Id(first), Ability(first, 4), round, 0) + minimum), minimum, maximum);
                sides[leading].Damage[round] = firstDamage;
                minimum = P("damage-minimum"); maximum = P("damage-maximum");
                int secondDamage = minimum;
                if (b[0] > minimum)
                    secondDamage = Math.Clamp(unchecked(Sample(unchecked(b[0] - minimum + 1), Id(second), Age(first), round, 0) + minimum), minimum, maximum);
                sides[following].Damage[round] = secondDamage;
                sides[leading].Actions[round] = sides[following].Actions[round] = ArmyDuelRoundAction.Ordinary;
                int criticalA = Sample(100, Id(first), Id(second), round, 1);
                int criticalB = Sample(100, Id(first), Id(second), round, 20);
                if (criticalA < a[1]) { sides[leading].Actions[round] = ArmyDuelRoundAction.Critical; sides[leading].Damage[round] = unchecked(sides[leading].Damage[round] * 2); }
                if (criticalB < b[1]) { sides[following].Actions[round] = ArmyDuelRoundAction.Critical; sides[following].Damage[round] = unchecked(sides[following].Damage[round] * 2); }
                int evadeA = Sample(100, Ability(first, 2), Ability(second, 2), round, 1);
                int evadeB = Sample(100, Ability(first, 2), Ability(second, 2), round, 20);
                int counterB = Sample(100, Id(first), Ability(second, 1), round, 0);
                int counterA = Sample(100, Id(second), Ability(first, 1), round, 0);
                bool aCritical = sides[leading].Actions[round] == ArmyDuelRoundAction.Critical;
                bool bCritical = sides[following].Actions[round] == ArmyDuelRoundAction.Critical;
                if (aCritical && bCritical) continue;
                if (aCritical)
                {
                    if (absoluteDifference > P("critical-counter-difference") || b[3] <= counterB) sides[following].Damage[round] = 0;
                    else { sides[following].Actions[round] = ArmyDuelRoundAction.Counter; sides[following].Damage[round] = unchecked(sides[following].Damage[round] * 2); sides[leading].Damage[round] = 0; }
                }
                else if (bCritical)
                {
                    if (a[3] <= counterA) sides[leading].Damage[round] = 0;
                    else { sides[leading].Actions[round] = ArmyDuelRoundAction.Counter; sides[leading].Damage[round] = unchecked(sides[leading].Damage[round] * 2); sides[following].Damage[round] = 0; }
                }
                else if (a[2] < b[2])
                {
                    if (evadeA < b[2]) { sides[following].Actions[round] = ArmyDuelRoundAction.Evaded; sides[leading].Damage[round] = 0; }
                    else if (evadeB < a[2]) { sides[leading].Actions[round] = ArmyDuelRoundAction.Evaded; sides[following].Damage[round] = 0; }
                }
                else
                {
                    if (evadeB < a[2]) { sides[leading].Actions[round] = ArmyDuelRoundAction.Evaded; sides[following].Damage[round] = 0; }
                    else if (evadeA < b[2]) { sides[following].Actions[round] = ArmyDuelRoundAction.Evaded; sides[leading].Damage[round] = 0; }
                }
            }
        }

        private sealed class Side(int personId, byte before)
        {
            internal int PersonId { get; } = personId;
            internal byte Before { get; } = before;
            internal byte After = before;
            internal bool Write;
            internal int PreparedInitial = 100, PreparedFinal = 100;
            internal ArmyDuelParticipantOutcome Outcome = ArmyDuelParticipantOutcome.Undetermined;
            internal readonly int[] Timeline = [100, 100, 100, 100, 100, 100];
            internal readonly int[] Damage = new int[6];
            internal readonly ArmyDuelRoundAction[] Actions = Enumerable.Repeat(ArmyDuelRoundAction.Unprepared, 6).ToArray();
            internal ArmyDuelSideNumericResult Freeze() => new(PersonId, Outcome, Before, After, Write, PreparedInitial,
                PreparedFinal, Array.AsReadOnly((int[])Timeline.Clone()), Array.AsReadOnly(Enumerable.Range(1, 5)
                    .Select(i => new ArmyDuelRound(i, Damage[i], Actions[i])).ToArray()));
        }
    }

    private static int ToInt(float value) => value is >= int.MinValue and < 2147483648f ? (int)value : int.MinValue;
    private static T Need<T>(T? value, string field) where T : struct => value ?? throw Missing(field);
    private static InvalidDataException Missing(string field) => new("current.DuelOpportunity_numeric_" + field);
}
