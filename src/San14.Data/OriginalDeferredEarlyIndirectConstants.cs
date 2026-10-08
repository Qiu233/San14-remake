using System.Buffers.Binary;

namespace San14.Data;

public sealed record OriginalDeferredEarlyIndirectConstantWrite(string Name, int Offset, int Bytes);

/// <summary>Named current CPU writes in the selected physical layout. Other
/// declared variables are explicit retained caller bytes, never zero-filled
/// substitutes. This packet has no draw/UAV/texture completion authority.</summary>
public sealed class OriginalDeferredEarlyIndirectConstantPacket
{
    internal OriginalDeferredEarlyIndirectConstantPacket(OriginalDeferredEarlyIndirectProgram program,
        OriginalDeferredEarlyIndirectCurrentCamera camera, OriginalDeferredEarlyIndirectPatternMapWrite pattern,
        byte[] bytes, IReadOnlyList<OriginalDeferredEarlyIndirectConstantWrite> writes, IReadOnlyList<string> retained)
        => (Program, Camera, Pattern, Bytes, Writes, RetainedVariables) = (program, camera, pattern, bytes, writes, retained);
    public OriginalDeferredEarlyIndirectProgram Program { get; }
    public OriginalDeferredEarlyIndirectCurrentCamera Camera { get; }
    public OriginalDeferredEarlyIndirectPatternMapWrite Pattern { get; }
    public ReadOnlyMemory<byte> Bytes { get; }
    public IReadOnlyList<OriginalDeferredEarlyIndirectConstantWrite> Writes { get; }
    public IReadOnlyList<string> RetainedVariables { get; }
    public bool AllDeclaredVariablesWritten => RetainedVariables.Count == 0;
    public string SourceEvidence => Program.Owner.SourceEvidence + ";" + Camera.SourceEvidence + ";" +
        Pattern.SourceEvidence + ";mW2Pt/fPtn-current-PatternMap;mP2W-current-Camera;actual2939-ordinal8=fDOFade;" +
        "physical-RDEF-name-layout;retained-padding-and-other-caller-bytes;CPU-packet-not-GPU-receipt";
}

public static class OriginalDeferredEarlyIndirectConstants
{
    public static OriginalDeferredEarlyIndirectConstantPacket Prepare(
        OriginalDeferredEarlyIndirectProgram actualSelectedProgram,
        OriginalDeferredEarlyIndirectCurrentCamera actualCurrentCamera,
        OriginalDeferredEarlyIndirectPatternMapWrite actualCurrentPatternMap,
        ReadOnlySpan<byte> actualCompleteModuleGlobals)
    {
        ArgumentNullException.ThrowIfNull(actualSelectedProgram); ArgumentNullException.ThrowIfNull(actualCurrentCamera);
        ArgumentNullException.ThrowIfNull(actualCurrentPatternMap);
        actualSelectedProgram.Owner.RequireProgram(actualSelectedProgram);
        actualCurrentCamera.RequireCurrent(actualCurrentCamera.Frame);
        if (actualCurrentPatternMap.State.Source.Locale != actualCurrentCamera.Frame.Camera.ExecutableLocale ||
            actualCurrentPatternMap.WorldToPointMatrix.Length != 64 || actualCurrentCamera.ProjectionToWorldMatrix.Length != 64 ||
            !float.IsFinite(actualCurrentPatternMap.Pattern))
            throw new InvalidDataException("Indirect68 current PatternMap and Camera are from different native profiles or incomplete writers.");
        var buffers = actualSelectedProgram.PixelDefinitions.Buffers.Where(b => b.Name.Text == "$Globals").ToArray();
        if (buffers.Length != 1 || actualCompleteModuleGlobals.Length != buffers[0].ByteSize)
            throw new InvalidDataException("Indirect68 requires its actual selected full module globals bytes.");
        var cb = buffers[0]; var bytes = actualCompleteModuleGlobals.ToArray();
        var writes = new List<OriginalDeferredEarlyIndirectConstantWrite>();
        void Write(string name, ReadOnlySpan<byte> value)
        {
            var variables = cb.Variables.Where(v => v.Name.Text == name).ToArray();
            if (variables.Length != 1 || variables[0].ByteSize != value.Length)
                throw new InvalidDataException("Indirect68 physical RDEF differs for native parameter " + name);
            int at = checked((int)variables[0].StartOffset);
            value.CopyTo(bytes.AsSpan(at, value.Length)); writes.Add(new(name, at, value.Length));
        }
        Write("mW2Pt", actualCurrentPatternMap.WorldToPointMatrix.Span);
        Span<byte> scalar = stackalloc byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(scalar, actualCurrentPatternMap.Pattern); Write("fPtn", scalar);
        Write("mP2W", actualCurrentCamera.ProjectionToWorldMatrix.Span);
        BinaryPrimitives.WriteUInt32LittleEndian(scalar, actualCurrentCamera.Camera3BCWord); Write("fDOFade", scalar);
        var names = writes.Select(w => w.Name).ToHashSet(StringComparer.Ordinal);
        return new(actualSelectedProgram, actualCurrentCamera, actualCurrentPatternMap, bytes,
            writes.AsReadOnly(), Array.AsReadOnly(cb.Variables.Select(v => v.Name.Text).Where(n => !names.Contains(n)).ToArray()));
    }
}
