using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using San14.Data.Formats;

namespace San14.Data;

public readonly record struct G1EffectDirectComputeView(Matrix4x4 WorldToClip,
    Vector4 BillboardRight, Vector4 BillboardUp, Vector2 ClipScale,
    Vector4? ViewZ = null, float? Fade = null, float? AlphaScale = null);

/// <summary>Factory consumed by G1EffectInstance's normal direct-storage hook.
/// The supplied device owns real native programs/buffers and GPU liveness feedback;
/// the view provider reads the current game camera, rather than a captured frame.</summary>
public sealed class G1EffectDirectComputeFactory(IG1EffectDirectComputeDevice device,
    Func<G1EffectDirectComputeView> currentView,
    Action<G1EffectGenericRuntime, G1EffectDirectComputeStorage>? onCreated = null)
{
    private readonly ConditionalWeakTable<object, Dictionary<int, G1EffectDirectComputeStorage>> linkedOwners = new();

    public IG1EffectDirectParticleStorage? CreateStorage(G1EffectGenericRuntime generic)
    {
        ArgumentNullException.ThrowIfNull(generic);
        ArgumentNullException.ThrowIfNull(device); ArgumentNullException.ThrowIfNull(currentView);
        if (!generic.Declaration.UsesDirectBirth) return null;
        if (generic.Record.Type == 10)
        {
            object key = generic.DirectModuleInstanceKey ?? throw new InvalidOperationException("Linked direct generic has no live module identity.");
            int main = checked(generic.Record.FileOffset + 0x200 + BinaryPrimitives.ReadInt32LittleEndian(generic.CurrentGenericBytes.AsSpan(0x200)));
            return linkedOwners.TryGetValue(key, out var owners) && owners.TryGetValue(main, out var parent)
                ? new LinkedStorage(parent) : null;
        }
        if (generic.DirectPreparedObjectData is not { } prepared || generic.DirectLiveObjectData is not { } live)
            throw new InvalidOperationException("Direct generic has no prepared module object-data owner.");
        var data = new G1EffectDirectComputeModuleData(generic.DirectModuleDeclaration, prepared());
        int link = BinaryPrimitives.ReadInt32LittleEndian(generic.CurrentGenericBytes.AsSpan(0x15C));
        G1EffectParticleDeclaration? linked = null;
        if (link != 0)
        {
            int linkedAt = checked(generic.Record.FileOffset + 0x15C + link);
            var record = Find(generic.DirectModuleDeclaration.Records, linkedAt)
                ?? throw new InvalidDataException("Direct generic's linked declaration is absent.");
            linked = G1EffectParticleDeclaration.Read(record);
        }
        var package = new G1EffectDirectComputePackage(generic.Declaration, data.InitialBytes,
            data.ByteOrigin, generic.DirectEffectContext, linked);
        if (package.MissingKernels.Count != 0) return null;
        object? moduleKey = linked is null ? null : generic.DirectModuleInstanceKey ??
            throw new InvalidOperationException("Direct generic has no live module identity.");
        var storage = G1EffectDirectComputeStorage.Create(package, generic.DirectEffectContext.Random,
            device, data.BindBirth, seconds =>
            {
                G1EffectDirectComputeView view = currentView();
                // The admitted current controller paths do not read CameraConstBuf.
                // Do not invent opacity/camera values merely to fill an unused buffer.
                var constants = new List<G1EffectDirectConstantBinding>();
                if (view.ViewZ is { } z && view.Fade is { } fade && view.AlphaScale is { } alpha)
                {
                    byte[] camera = new byte[32];
                    float[] values = [z.X,z.Y,z.Z,z.W,fade,alpha];
                    for (int i = 0; i < values.Length; i++) BinaryPrimitives.WriteSingleLittleEndian(camera.AsSpan(i * 4), values[i]);
                    constants.Add(new(1, camera));
                }
                int genericOffset = data.GenericOffset(generic);
                return new(genericOffset, data.FrameUploads(generic, live()),
                    [package.Motion(seconds, genericOffset), package.Postprocess(genericOffset), package.ResetDraw(),
                        package.ExpandDraw(view.WorldToClip, view.BillboardRight, view.BillboardUp, view.ClipScale)],
                    constants.AsReadOnly());
            });
        if (storage is not null)
        {
            try
            {
                if (moduleKey is not null)
                    linkedOwners.GetValue(moduleKey, _ => [])[generic.Record.FileOffset] = storage;
                onCreated?.Invoke(generic, storage);
            }
            catch
            {
                if (moduleKey is not null && linkedOwners.TryGetValue(moduleKey, out var owners))
                    owners.Remove(generic.Record.FileOffset);
                storage.Dispose(); throw;
            }
        }
        return storage;
    }

    private static G1EffectRecord? Find(IReadOnlyList<G1EffectRecord> records, int fileOffset)
    {
        foreach (var record in records)
        {
            if (record.FileOffset == fileOffset && record.Table == G1EffectTable.Generic) return record;
            if (Find(record.Children, fileOffset) is { } child) return child;
        }
        return null;
    }

    private sealed class LinkedStorage(G1EffectDirectComputeStorage owner) : IG1EffectDirectParticleStorage
    {
        private bool disposed;
        public bool IsPrepared => !disposed && owner.IsPrepared;
        public bool IsAlive => !disposed && owner.IsAlive;
        public void QueueBirth(G1EffectDirectBirthCommand command) =>
            throw new NotSupportedException("A linked backing receives its actual main manager's queued births.");
        // The main package advances the linked buffer in its real shared GPU sequence.
        public void Advance(float seconds) { ObjectDisposedException.ThrowIf(disposed, this); }
        public void Dispose() => disposed = true;
    }
}
