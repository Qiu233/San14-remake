namespace San14.Data;

/// <summary>The source fresh NUL parser produces no glyph records. The
/// original explicit-image job still clears its actual material primary RT.
/// This is a completed zero-glyph producer, separate from an unknown image
/// that has only been cleared.</summary>
public sealed class LoadedArmyFlagEmptyPaneLayout
{
    public ArmyFlagPaneGlyph SourceText { get; }
    public ArmyFlagPaneColorSelection SourceColors { get; }
    public int ExplicitSourcePaneKind { get; }
    public int NativeLayoutWidth => 128;
    public int NativeLayoutHeight => 64;
    public int NativeGlyphRecordCount => 0;
    public bool PaneStateFidelityVerified => false;
    public string NativeSourceProvenance => "SC AD7C35 NUL -> AD7CDD without glyph call; " +
        "fresh1062AFF recordcount+6C=0; B0953B/1063380/1063390; " +
        "1062B19/1062D13 empty bounds; 106363E..1063652 ->106381A before quad; " +
        "B09E70 explicit primary force-tint clear remains complete zero-glyph output";

    internal LoadedArmyFlagEmptyPaneLayout(ArmyFlagPaneGlyph text,
        ArmyFlagPaneColorSelection colors,int kind)
        =>(SourceText,SourceColors,ExplicitSourcePaneKind)=(text,colors,kind);
}

public static class ArmyFlagEmptyPaneLayout
{
    public static LoadedArmyFlagEmptyPaneLayout Build(LoadedArmyFlagFontSource font,
        ArmyFlagPaneGlyph sourceText,ArmyFlagPaneColorSelection sourceColors,
        int explicitSourcePaneKind,bool explicitOwnedFreshNulTextRoute)
    {
        ArgumentNullException.ThrowIfNull(font);ArgumentNullException.ThrowIfNull(sourceText);
        ArgumentNullException.ThrowIfNull(sourceColors);
        if(!explicitOwnedFreshNulTextRoute||(uint)explicitSourcePaneKind>2||
            sourceText.NativeCopyUnitCount!=2||sourceText.TwoUtf16Units!="\0\0"||
            string.IsNullOrWhiteSpace(sourceText.CallerSource)||string.IsNullOrWhiteSpace(sourceColors.CallerSource))
            throw new InvalidDataException("Source empty flag pane requires the explicit fresh NUL route, copied two zero units, original colors and pane kind0..2.");
        if(!font.FreshHostDefaultFontFidelityVerified||font.ExplicitCommonArrayIndex!=0)
            throw new InvalidDataException("Source empty flag pane requires the owned fresh Host default font context.");
        return new(sourceText,sourceColors,explicitSourcePaneKind);
    }
}
