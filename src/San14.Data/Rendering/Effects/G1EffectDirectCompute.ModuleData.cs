using System.Buffers.Binary;
using San14.Data.Formats;

namespace San14.Data;

/// <summary>A normal module's GPU object image. It is allocated once and retains the
/// serialized relative coordinate system. Prepared auxiliary fragments replace raw values;
/// subsequent frames upload their actual changed fields and the current direct generic.</summary>
public sealed class G1EffectDirectComputeModuleData
{
    private readonly G1EffectModule module;
    private readonly byte[] initial;
    private readonly int sourceOffset, sourceCapacity;
    public ReadOnlyMemory<byte> InitialBytes => initial;
    public int ByteOrigin => module.FileOffset;

    public G1EffectDirectComputeModuleData(G1EffectModule module, IEnumerable<G1EffectObjectDataPatch> preparedAuxiliaries)
    {
        ArgumentNullException.ThrowIfNull(module); ArgumentNullException.ThrowIfNull(preparedAuxiliaries);
        this.module = module;
        sourceOffset = G1EffectDirectComputePackage.Align(module.Data.Length);
        sourceCapacity = module.Records.Where(r => r.Table == G1EffectTable.Emitter).Select(r => r.ByteLength).DefaultIfEmpty(0).Max();
        initial = new byte[checked(sourceOffset + G1EffectDirectComputePackage.Align(sourceCapacity))];
        module.Data.CopyTo(initial);
        foreach (var fragment in preparedAuxiliaries)
        {
            int at = Offset(fragment.FileOffset, fragment.Bytes.Length);
            fragment.Bytes.CopyTo(initial.AsMemory(at));
        }
    }

    public G1EffectDirectBirthBinding BindBirth(G1EffectDirectBirthCommand command)
    {
        if (command.SourceContext.Length > sourceCapacity)
            throw new NotSupportedException("Direct source context is not an outer emitter owned by this module.");
        int definition = Offset(command.Definition.Record.FileOffset, command.Definition.Record.Bytes.Length);
        int generic = Offset(command.Generic.FileOffset, command.Generic.Bytes.Length);
        return new(sourceOffset, definition, generic,
            [new(G1EffectDirectComputePackage.ObjectBuffer, definition, command.Definition.Record.Bytes),
             new(G1EffectDirectComputePackage.ObjectBuffer, generic, command.Generic.Bytes),
             new(G1EffectDirectComputePackage.ObjectBuffer, sourceOffset, command.SourceContext)]);
    }

    public IReadOnlyList<G1EffectDirectUpload> FrameUploads(G1EffectGenericRuntime generic,
        IEnumerable<G1EffectObjectDataPatch> liveAuxiliaries)
    {
        var uploads = new List<G1EffectDirectUpload>
        { new(G1EffectDirectComputePackage.ObjectBuffer, Offset(generic.Record.FileOffset, generic.CurrentGenericBytes.Length), generic.CurrentGenericBytes) };
        foreach (var fragment in liveAuxiliaries)
            uploads.Add(new(G1EffectDirectComputePackage.ObjectBuffer,
                Offset(fragment.FileOffset, fragment.Bytes.Length), fragment.Bytes));
        return uploads.AsReadOnly();
    }

    public int GenericOffset(G1EffectGenericRuntime generic) => Offset(generic.Record.FileOffset, generic.CurrentGenericBytes.Length);
    private int Offset(int fileOffset, int length)
    {
        int at = checked(fileOffset - module.FileOffset);
        if (at < 0 || length < 0 || at > module.Data.Length - length)
            throw new InvalidDataException("Direct object fragment escapes its owning normal module.");
        return at;
    }
}
