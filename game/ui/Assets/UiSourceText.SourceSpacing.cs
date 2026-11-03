using San14.Data;

internal partial class UiSourceText
{
    // B12950 retains source flags6/7. AC2D60/ACC3E0 consume them into
    // compact spacing before ADBFD0/ADB880 performs ordinary width fitting.
    internal void ConfigureSourceScaledSpacing(MapFont font, MapFontProfile source)
    {
        var drawing = source with
        {
            HorizontalSpace = (source.RawFlags & 0x40) != 0
                ? unchecked((short)BattleUnitSourceMath.TruncateSingleToInt32(source.HorizontalSpace * source.ScaleX))
                : source.HorizontalSpace,
            VerticalSpace = (source.RawFlags & 0x80) != 0
                ? unchecked((short)BattleUnitSourceMath.TruncateSingleToInt32(source.VerticalSpace * source.ScaleY))
                : source.VerticalSpace,
            RawFlags = source.RawFlags & ~0xC0u
        };
        // Other source options remain present for the normal strict consumer.
        Configure(font, drawing);
    }
}
