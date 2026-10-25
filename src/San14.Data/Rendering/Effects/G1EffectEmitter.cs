using San14.Data.Formats;

namespace San14.Data;

/// <summary>One source emission attempt. Count is the scheduled batch count.
/// When any non-direct definition remains, the source samples a shape once per
/// batch and offers that sample to each such definition. Count is not a number
/// of successfully allocated particles.</summary>
public readonly record struct G1EffectEmitterStep(bool Enabled, float Rate, float EmissionAmount,
    int Count, float Residue, float ElapsedAfter, float DeltaSeconds, uint Flags)
{
    public bool SpreadBirthsAcrossStep => (Flags & 0x40) != 0 && DeltaSeconds > 0;
    public bool AdvanceNewParticles => (Flags & 0x100) != 0;

    /// <summary>Original reverse birth order. For spread births, this age also
    /// controls motion compensation unless the target particle has flag 1.</summary>
    public float BirthOffsetSeconds(int remainingIndex)
    {
        if ((uint)remainingIndex >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(remainingIndex));
        if (!SpreadBirthsAcrossStep) return 0;
        float spacing = DeltaSeconds / EmissionAmount;
        return remainingIndex * spacing + Residue * spacing;
    }

    public float InitialParticleAgeSeconds(int remainingIndex) =>
        BirthOffsetSeconds(remainingIndex) + (AdvanceNewParticles ? DeltaSeconds : 0);
}

/// <summary>CPU scheduling of an outer G1FX emitter. Shape sampling, particle
/// initialization and live-pool ownership belong to the supplied birth consumer;
/// preparing or advancing this scheduler alone is not visual completion.</summary>
public sealed class G1EffectEmitter(G1EffectEmitterDeclaration declaration)
{
    private float rate = declaration.InitialRate;
    private float elapsed;
    private float residue;
    private uint flags = declaration.Flags | 8;

    public G1EffectEmitterDeclaration Declaration => declaration;
    public float Rate => rate;
    public float ElapsedSeconds => elapsed;
    public float Residue => residue;
    public bool EmissionEnabled => (flags & 0x10) != 0;

    public void SetEmissionEnabled(bool enabled) => flags = enabled ? flags | 0x10U : flags & ~0x10U;

    public void Reset()
    {
        // Reset preserves the current enable bit, as does the source emitter.
        flags |= 8;
        rate = declaration.InitialRate;
        elapsed = 0;
        residue = 0;
    }

    /// <summary>Runs controller and residue math without sampling a shape or
    /// changing the owned emitter. The supported type 2 scalar's native
    /// initialization is a no-op, so its declaration is evaluated directly.</summary>
    public G1EffectEmitterStep PrepareUpdate(float deltaSeconds, float moduleEmissionScale)
    {
        if (!EmissionEnabled) return new(false, rate, 0, 0, residue, elapsed, deltaSeconds, flags);
        float nextRate = rate;
        float end = elapsed + deltaSeconds;
        foreach (var controller in declaration.Controllers)
        {
            if (controller.TargetOffset != 0x6C || controller.TimeOffset != 8 || controller.ScaleOffset != 0x70)
                throw new NotSupportedException("G1FX emitter controller field binding is not implemented.");
            if (controller.Type == 2)
            {
                nextRate = (controller.LinearScalar ??
                    throw new NotSupportedException("G1FX emitter scalar type is not implemented."))
                    .Evaluate(elapsed, declaration.InitialRate);
                continue;
            }
            if (controller.Type != 14)
                throw new NotSupportedException($"G1FX emitter controller {controller.Type} is not implemented.");
            float value = 0;
            foreach (var key in controller.Keys)
            {
                // A key on the beginning boundary fires; a key on the ending
                // boundary waits for the next accepted source update.
                if (end <= key.Time) break;
                if (elapsed <= key.Time) value += key.Value;
            }
            nextRate = value * declaration.InitialRate;
        }
        float amount = (flags & 7) switch
        {
            0 => nextRate,
            1 => deltaSeconds * nextRate,
            2 => throw new NotSupportedException("G1FX shape-dependent emission rate is not implemented."),
            _ => nextRate
        };
        if ((flags & 0x20) != 0) amount *= moduleEmissionScale;
        if (!declaration.EmissionModifier.IsEmpty)
            throw new NotSupportedException("G1FX emission modifier evaluator is not implemented.");
        float accumulated = amount + residue;
        int count = (int)accumulated;
        float remainder = accumulated - count;
        return new(true, nextRate, amount, count, remainder, end, deltaSeconds, flags);
    }

    /// <summary>Whether the source emitter can still produce particles. The
    /// supplied predicate reports an allocated target particle pool, including
    /// an empty pool; it must not substitute a nonzero live-particle count.</summary>
    public bool CanEmit(Func<G1EffectRecord, bool> hasAllocatedParticlePool)
    {
        ArgumentNullException.ThrowIfNull(hasAllocatedParticlePool);
        bool hasTarget = false;
        foreach (var definition in declaration.Definitions)
        {
            if ((definition.Particle.Flags & 0x200) == 0 ||
                hasAllocatedParticlePool(definition.Particle))
            {
                hasTarget = true;
                break;
            }
        }
        if (!hasTarget) return false;
        float threshold = (flags & 7) == 1 ? 0.0011920928955078125f : 0.000011920928955078125f;
        if (rate > threshold || elapsed == 0) return true;
        foreach (var controller in declaration.Controllers)
        {
            if (controller.TargetOffset != 0x6C) continue;
            if (controller.TimeOffset != 8)
                throw new NotSupportedException("G1FX emitter controller time binding is not implemented.");
            bool canContinue = controller.Type switch
            {
                14 when controller.Keys.Count > 0 => controller.Keys[^1].Time >= elapsed,
                2 => (controller.LinearScalar ??
                    throw new NotSupportedException("G1FX emitter scalar type is not implemented."))
                    .CanContinue(elapsed),
                _ => throw new NotSupportedException($"G1FX emitter controller {controller.Type} lifetime is not implemented.")
            };
            if (canContinue) return true;
        }
        return false;
    }

    /// <summary>The consumer must execute the actual shape/direct-birth/pool
    /// operations, including the zero-count shape 11 path where applicable.
    /// Returning from a callback that only records metadata does not implement
    /// particle emission. A missing consumer cannot advance the emitter.</summary>
    public G1EffectEmitterStep Advance(float deltaSeconds, float moduleEmissionScale,
        Action<G1EffectEmitterDeclaration, G1EffectEmitterStep> actualBirthConsumer)
    {
        ArgumentNullException.ThrowIfNull(actualBirthConsumer);
        var step = PrepareUpdate(deltaSeconds, moduleEmissionScale);
        if (!step.Enabled) return step;
        actualBirthConsumer(declaration, step);
        rate = step.Rate;
        residue = step.Residue;
        elapsed = step.ElapsedAfter;
        return step;
    }
}
