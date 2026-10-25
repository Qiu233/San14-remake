using San14.Data.Formats;

namespace San14.Data;

/// <summary>A direct GPU birth request retains its actual declaration and
/// current emitter context. It is not an ordinary CPU-pool particle.</summary>
public sealed record G1EffectDirectBirthCommand(G1EffectRecord Generic,
    G1EffectParticleDefinition Definition,ReadOnlyMemory<byte> SourceContext,
    int ScheduledCount,float? ScheduledAmount);

/// <summary>The renderer's prepared direct-particle storage. Implementations
/// own real device buffers and execute the source compute/update pipeline.</summary>
public interface IG1EffectDirectParticleStorage : IDisposable
{
    bool IsPrepared { get; }
    bool IsAlive { get; }
    void QueueBirth(G1EffectDirectBirthCommand command);
    void Advance(float seconds);
}

public sealed partial class G1EffectGenericRuntime
{
    private IG1EffectDirectParticleStorage? directStorage;
    public bool HasDirectStorage=>directStorage?.IsPrepared==true;

    public void AttachDirectStorage(IG1EffectDirectParticleStorage storage)
    {
        ArgumentNullException.ThrowIfNull(storage);
        ObjectDisposedException.ThrowIf(disposed,this);
        if(!Declaration.UsesDirectBirth) throw new InvalidOperationException("Generic does not use direct particle storage.");
        if(directStorage is not null) throw new InvalidOperationException("Generic already owns direct particle storage.");
        directStorage=storage;
    }

    public void EmitDirect(G1EffectParticleDefinition definition,ReadOnlyMemory<byte> sourceContext,int scheduledCount)
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        if(!Declaration.UsesDirectBirth) throw new InvalidOperationException("Generic is not a direct-birth target.");
        if(!HasDirectStorage) return; // Native A9AD00 skips an absent prepared direct owner.
        directStorage!.QueueBirth(new(CurrentRecord,definition,sourceContext,scheduledCount,null));
    }

    public void EmitDirectFractional(G1EffectParticleDefinition definition,ReadOnlyMemory<byte> sourceContext,float scheduledAmount)
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        if(!Declaration.UsesDirectBirth) throw new InvalidOperationException("Generic is not a direct-birth target.");
        if(!HasDirectStorage) return;
        directStorage!.QueueBirth(new(CurrentRecord,definition,sourceContext,0,scheduledAmount));
    }
}
