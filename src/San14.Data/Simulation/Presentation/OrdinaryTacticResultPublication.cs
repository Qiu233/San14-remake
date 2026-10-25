using System.Collections.ObjectModel;
using San14.Core;

namespace San14.Data;

public enum OrdinaryTacticResultOperation { Effect, PositionalSound }

/// <summary>One ordered counter-zero presentation attempt. Effect admission and
/// sound admission are separate attempts, including when an effect pool is full.</summary>
public sealed record OrdinaryTacticResultEvent(int ActorIndex, int InstructionIndex,
    int MarkerIndex, int TacticId, int Opcode, TacticResultPresentation Target,
    NarrationSpatialVoicePosition Position, OrdinaryTacticResultOperation Operation,
    int? EffectId, int? SoundId, float Scale);

/// <summary>The four source group boundaries are ordered. Amounts are keyed by
/// campaign receiver identity, not ordered by a fabricated native address.
/// The host enables the number child, then starts senpo_num only for a positive
/// signed amount and an idle animation; it must not replace an active number.</summary>
public sealed record OrdinaryTacticNumberGroup(bool Healing,
    TacticResultTargetKind TargetKind, int Palette, IReadOnlyDictionary<int, uint> Amounts)
{
    public static int DisplayAmount(uint amount) => unchecked((int)amount);
}

public sealed record OrdinaryTacticResultPublication(long Sequence,
    IReadOnlyList<OrdinaryTacticResultEvent> Events,
    IReadOnlyList<OrdinaryTacticNumberGroup> NumberGroups);

public sealed partial class ArmyCampaignRuleAdapter
{
    /// <summary>171F50/1722E0's counter-zero publication. The retained markers
    /// already describe applied gameplay; this query neither applies Effects
    /// again, consumes a random draw, nor acknowledges a presentation.</summary>
    public AreaAdministrationQuery<OrdinaryTacticResultPublication> QueryOrdinaryTacticResults(
        GameState state, TacticPresentationRequest request, MainMapCameraTerrainSource? terrain = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            if (request.Kind != TacticPresentationKind.OrdinaryEffects)
                throw TacticResultMissing("unique_direction_host");
            if (request.Actors.Length is < 1 or > 11)
                throw TacticResultMissing("primary_and_ten_linked_actors");
            var current = CaptureOwnedArmyReturnWorld(state).Context;
            var events = new List<OrdinaryTacticResultEvent>();
            // Damage Army, damage Base, recovery Army, recovery Base. These
            // trees are shared across the primary and all ten linked actors.
            Dictionary<int, uint>[] numbers = [new(), new(), new(), new()];
            Dictionary<int, int>? armyPhysicalByReceiver = null;
            for (int actorIndex = 0; actorIndex < request.Actors.Length; actorIndex++)
            {
                var actor = request.Actors[actorIndex];
                if (actor.TacticId is < 0 or > 200) throw TacticResultMissing("Tactic_identity");
                var tactic = current.ReadSkillTacticReference(actor.TacticId)
                    ?? throw TacticResultMissing("Tactic_pointer");
                if (!(current.ReadSkillTacticValidity(tactic) ?? throw TacticResultMissing("Tactic_validity"))) continue;
                if (actor.InstructionResults is not { Length: 2 } markers)
                    throw TacticResultMissing("ordered_instruction_markers");
                for (int instruction = 0; instruction < 2; instruction++)
                {
                    int opcode = current.ReadTacticEffectOpcode(tactic, instruction);
                    if (opcode == 0) continue;
                    var row = markers[instruction] ?? throw TacticResultMissing("instruction_marker_row");
                    for (int markerIndex = 0; markerIndex < row.Length; markerIndex++)
                    {
                        var marker = row[markerIndex];
                        if ((uint)marker.HexId > 48400) throw TacticResultMissing("marker_Hex");
                        var position = ArmyMapSpeechVoiceGeometry.QueryPacketPosition(marker.HexId, terrain);
                        float squaredLength = position.X * position.X + position.Y * position.Y + position.Z * position.Z;
                        if (MathF.Abs((float)Math.Sqrt(squaredLength)) < 1.1920929e-5f) continue;
                        Publish(opcode, marker, position, actorIndex, instruction, markerIndex, actor.TacticId);
                        if (opcode is 1 or 20) Accumulate(opcode, marker);
                    }
                }
            }
            var groups = new OrdinaryTacticNumberGroup[4];
            for (int index = 0; index < groups.Length; index++)
                groups[index] = new(index >= 2,
                    index % 2 == 0 ? TacticResultTargetKind.ArmyReceiver : TacticResultTargetKind.BaseLocation,
                    index >= 2 ? 0x2E : 0x34, new ReadOnlyDictionary<int, uint>(numbers[index]));
            return AreaAdministrationQuery<OrdinaryTacticResultPublication>.Known(
                new(request.Sequence, events.AsReadOnly(), Array.AsReadOnly(groups)));

            void Publish(int opcode, TacticResultPresentation marker, NarrationSpatialVoicePosition position,
                int actorIndex, int instruction, int markerIndex, int tacticId)
            {
                switch (opcode)
                {
                    case 1:
                        int damageEffect = marker.Amount < P("large-damage-threshold") ? 16 : 17;
                        Effect(damageEffect, position, 2f);
                        Sound(damageEffect == 16 ? 180 : 181, position);
                        break;
                    case 2 or 3 or 4:
                        Effect(18 + opcode - 2, position, 1f);
                        Sound(182 + opcode - 2, position);
                        break;
                    case >= 5 and <= 9: Modifier(true, opcode - 5, true); break;
                    case >= 10 and <= 14: Modifier(false, opcode - 10, true); break;
                    case 15: Effect(31, position, 1f); break;
                    case 16: Effect(32, position, 1f); break;
                    case 17:
                        Effect(33, position, P("fire-scale-percent") / 100f);
                        Sound(187, position);
                        break;
                    case 18:
                        bool largeWallDamage = marker.Amount >= P("large-wall-damage-threshold");
                        Effect(largeWallDamage ? 35 : 34, position,
                            largeWallDamage ? P("large-wall-damage-scale-percent") / 100f : 1f);
                        break;
                    case 19: Effect(36, position, P("abnormal-clear-scale-percent") / 100f); break;
                    case 20: Effect(37, position, 1f); break;
                    case 21 or 22:
                        for (int dimension = 0; dimension < 5; dimension++) Modifier(opcode == 21, dimension, dimension == 0);
                        break;
                    case 23: Effect(67, position, 1f); break;
                    // The source switch has no default effect for other opcodes.
                }

                void Modifier(bool positive, int dimension, bool central)
                {
                    float angle = dimension * 72f * 0.017453292f;
                    var offset = new NarrationSpatialVoicePosition(position.X + MathF.Cos(angle) * 234f,
                        position.Y + 250f, position.Z - MathF.Sin(angle) * 234f);
                    Effect((positive ? 21 : 26) + dimension, offset, 1f);
                    Sound(positive ? 185 : 186, offset);
                    if (central) Effect(positive ? 65 : 66, position, 1f);
                }
                void Effect(int effect, NarrationSpatialVoicePosition at, float scale) => events.Add(new(
                    actorIndex, instruction, markerIndex, tacticId, opcode, marker, at,
                    OrdinaryTacticResultOperation.Effect, effect, null, scale));
                void Sound(int sound, NarrationSpatialVoicePosition at) => events.Add(new(
                    actorIndex, instruction, markerIndex, tacticId, opcode, marker, at,
                    OrdinaryTacticResultOperation.PositionalSound, null, sound, 1f));
            }

            void Accumulate(int opcode, TacticResultPresentation marker)
            {
                int group = opcode == 1 ? 0 : 2;
                int receiver;
                if (marker.TargetKind == TacticResultTargetKind.ArmyReceiver)
                {
                    if (armyPhysicalByReceiver is null)
                    {
                        var armySource = state.ReadArmyLifecycle() ?? throw TacticResultMissing("Army_receiver_owner");
                        armyPhysicalByReceiver = armySource.Slots.ToDictionary(slot => slot.ReceiverId.Value, slot => slot.PhysicalSlot);
                    }
                    if (!armyPhysicalByReceiver.TryGetValue(marker.TargetId, out int physical))
                        throw TacticResultMissing("Army_receiver_identity");
                    var army = current.ReadArmyUnitTableReference(physical < 501 ? physical : 0)
                        ?? throw TacticResultMissing("Army_pointer");
                    if (!(current.ReadArmyUnitNativeValidity(army) ?? throw TacticResultMissing("Army_validity"))) return;
                    receiver = marker.TargetId;
                }
                else if (marker.TargetKind == TacticResultTargetKind.BaseLocation)
                {
                    var at = BaseLookup.Find(marker.TargetId <= 61 ? marker.TargetId : 0, current);
                    if (!(current.ReadBaseNativeValidity(at) ?? throw TacticResultMissing("Base_validity"))) return;
                    receiver = current.ReadBaseObjectLink(at) ?? throw TacticResultMissing("Base_identity");
                    group++;
                }
                else return; // Field Object markers still emitted the particle above.
                numbers[group][receiver] = unchecked(numbers[group].GetValueOrDefault(receiver) + marker.Amount);
            }

            int P(string name)
            {
                string key = "presentation.tactics.results." + name;
                var parameters = state.NativeArmySimulation?.Parameters;
                if (parameters is null || parameters.Float32Values.ContainsKey(key) ||
                    !parameters.Int32Values.TryGetValue(key, out int value)) throw TacticResultMissing(key);
                return value;
            }
        }
        catch (InvalidDataException error)
        { return AreaAdministrationQuery<OrdinaryTacticResultPublication>.Unknown(error.Message); }
    }

    private static InvalidDataException TacticResultMissing(string owner)
        => new("tactic.result-publication." + owner);
}
