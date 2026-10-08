using System.Buffers.Binary;

namespace San14.Data;

public readonly record struct OriginalWorldMainDisplayColorFormat(int NativeFormat, bool Srgb);

/// <summary>Native24B viewport getter/setter ordering is WHXY followed by
/// raw min/max depth. The display entry overwrites WHXY and preserves depth.</summary>
public readonly record struct OriginalWorldMainDisplayViewport(int Width, int Height, int X, int Y,
    uint MinimumDepthBits, uint MaximumDepthBits)
{
    public static OriginalWorldMainDisplayViewport ReadNative24B(ReadOnlySpan<byte> value)
    {
        if (value.Length != 24) throw new InvalidDataException("Display viewport requires the native24B WHXY/depth input.");
        return new(BinaryPrimitives.ReadInt32LittleEndian(value), BinaryPrimitives.ReadInt32LittleEndian(value[4..]),
            BinaryPrimitives.ReadInt32LittleEndian(value[8..]), BinaryPrimitives.ReadInt32LittleEndian(value[12..]),
            BinaryPrimitives.ReadUInt32LittleEndian(value[16..]), BinaryPrimitives.ReadUInt32LittleEndian(value[20..]));
    }
    public void WriteNative24B(Span<byte> destination)
    {
        if (destination.Length != 24) throw new InvalidDataException("Display viewport requires the native24B WHXY/depth output.");
        BinaryPrimitives.WriteInt32LittleEndian(destination, Width);
        BinaryPrimitives.WriteInt32LittleEndian(destination[4..], Height);
        BinaryPrimitives.WriteInt32LittleEndian(destination[8..], X);
        BinaryPrimitives.WriteInt32LittleEndian(destination[12..], Y);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[16..], MinimumDepthBits);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[20..], MaximumDepthBits);
    }
}

public sealed record OriginalWorldMainDisplayEntryRecipe(OriginalWorldMainDisplayViewport Viewport,
    bool ScissorEnabled, int ScissorX, int ScissorY, int ScissorWidth, int ScissorHeight,
    int ColorAttachmentCount, int TargetStackIndex, uint Host3F0, uint Host3F4, int ViewIndex3F8,
    string SourceEvidence);

public sealed record OriginalWorldMainDisplayExitRecipe(int TargetStackIndex,
    uint Host3F0, uint Host3F4, int ViewIndex3F8, string SourceEvidence);

/// <summary>Immutable source loaded once per native generation. It does not
/// supply device surfaces or claim the Windows device.v2C8 implementation.</summary>
public sealed class LoadedOriginalWorldMainDisplayParentSource
{
    internal LoadedOriginalWorldMainDisplayParentSource(string locale, string exe, string text,
        int entry, int refresh, int exit, int pop, string evidence)
        => (Locale, ExecutableSha256, DecodedTextSha256, EntryRva, RefreshRva, ExitRva, PopRva, SourceEvidence) =
            (locale, exe, text, entry, refresh, exit, pop, evidence);
    public string Locale { get; }
    public string ExecutableSha256 { get; }
    public string DecodedTextSha256 { get; }
    public int EntryRva { get; }
    public int RefreshRva { get; }
    public int ExitRva { get; }
    public int PopRva { get; }
    public string SourceEvidence { get; }

    public OriginalWorldMainDisplayColorFormat ResolveColorFormat(uint sourceDxgiFormat)
        => sourceDxgiFormat switch { 29 => new(0, true), 87 => new(1, false), 91 => new(1, true), _ => new(0, false) };
    public int ResolveDepthFormat(uint sourceDxgiFormat)
        => sourceDxgiFormat switch { 20 => 108, 45 => 5, 55 => 20, _ => 78 };

    /// <summary>Source959620: after real refresh and successful one-color row
    /// push, XY=0,WH=physical wrapper50/54; depth and scissor-enable are retained.</summary>
    public OriginalWorldMainDisplayEntryRecipe PrepareEntry(int physicalWidth, int physicalHeight,
        OriginalWorldMainDisplayViewport retainedViewport, bool retainedScissorEnabled, int priorTargetStackIndex)
    {
        if (physicalWidth <= 0 || physicalHeight <= 0 || priorTargetStackIndex < -1)
            throw new InvalidDataException("Display entry needs a positive actual physical extent and current stack index.");
        var viewport = retainedViewport with { Width = physicalWidth, Height = physicalHeight, X = 0, Y = 0 };
        return new(viewport, retainedScissorEnabled, 0, 0, physicalWidth, physicalHeight, 1,
            checked(priorTargetStackIndex + 1), 0, 0, 0, SourceEvidence + ";real-count1-outer-push;retained-depth/scissor-enable");
    }

    /// <summary>Main.vD0 after the whole graph: BF8610 resets Host fields;
    /// 964E80 clears the actual current row and decrements its retained index.</summary>
    public OriginalWorldMainDisplayExitRecipe PrepareExit(int actualCurrentTargetStackIndex)
    {
        if (actualCurrentTargetStackIndex < 0) throw new InvalidOperationException("No actual display row to pop.");
        return new(actualCurrentTargetStackIndex - 1, 0, 0, 0,
            SourceEvidence + ";whole-graph-return;Main-vD0-context-reset;actual-outer-pop");
    }
}

public static class OriginalWorldMainDisplayParentSource
{
    public static LoadedOriginalWorldMainDisplayParentSource Load(OriginalGameRepository repository, string locale)
    {
        ArgumentNullException.ThrowIfNull(repository);
        return OriginalDeferredSamplerSources.LoadWorldMainDisplayParentSource(repository.SourceDirectory, locale);
    }
}
