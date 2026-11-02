using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// Body colour provenance, not a G1N palette selector or a full text preset.
internal enum NarrationBodyColour { OriginalNodeBody, TextPreset38Body, TextPreset39Body, TextPreset40Body }
internal sealed record NarrationColourRun(string Text, NarrationBodyColour Colour)
{
    internal int? BodyPresetIndex { get; init; }
    internal int? SourcePresetIndex => BodyPresetIndex ?? Colour switch
    {
        NarrationBodyColour.TextPreset38Body => 38,
        NarrationBodyColour.TextPreset39Body => 39,
        NarrationBodyColour.TextPreset40Body => 40,
        _ => (int?)null
    };
}
internal sealed record NarrationParagraphLine(string VisibleText, IReadOnlyList<NarrationColourRun> Runs);
internal sealed record NarrationParagraphPage(string SourceText, string WrappedSourceText,
    IReadOnlyList<NarrationParagraphLine> Lines, int MaximumRawWidth);

// The source body-colour controls and ordinary LF text are retained
// beside visible runs; colour7 carries its explicit text-preset provenance.
internal static class DeploymentNarrationParagraphPlan
{
    internal static IReadOnlyList<NarrationParagraphPage> Create(string expanded, Func<string,int> rawWidth, bool allowPresetColours = false)
    {
        ArgumentNullException.ThrowIfNull(rawWidth);
        return DeploymentNarrationPlainParagraph.SplitKnownColourPages(expanded, allowPresetColours).Select(page =>
        {
            string wrapped = DeploymentNarrationPlainParagraph.WrapOneKnownColourPage(page, rawWidth, allowPresetColours);
            var lines = Runs(wrapped, allowPresetColours);
            return new NarrationParagraphPage(page, wrapped, lines,
                lines.Max(line => rawWidth(line.VisibleText)));
        }).ToArray();
    }

    internal static IReadOnlyList<NarrationParagraphLine> Runs(string wrapped, bool allowPresetColours = false)
    {
        var result = new List<NarrationParagraphLine>();
        var runs = new List<NarrationColourRun>();
        var run = new StringBuilder();
        var visible = new StringBuilder();
        // Each original page is a separate text publication. Colour persists
        // across LF within that page, but starts at its original node colours.
        NarrationBodyColour colour = NarrationBodyColour.OriginalNodeBody;
        int? bodyPreset = null;
        for (int index = 0; index < wrapped.Length; index++)
        {
            char code = wrapped[index];
            if (code == '^')
            {
                FlushRun();
                var control = DeploymentNarrationPlainParagraph.ReadBodyColourControl(wrapped, index, allowPresetColours);
                bodyPreset = wrapped[index + 2] == '7' ? control.BodyPreset : null;
                colour = wrapped[index + 2] switch
                {'2'=>NarrationBodyColour.TextPreset38Body,'3'=>NarrationBodyColour.TextPreset40Body,'5'=>NarrationBodyColour.TextPreset39Body,
                    _=>NarrationBodyColour.OriginalNodeBody};
                index += control.Length - 1; continue;
            }
            if (code == '\n') { FlushLine(); continue; }
            visible.Append(code); run.Append(code);
        }
        FlushLine(); // Preserves an explicit trailing LF as an empty line.
        return result.AsReadOnly();

        void FlushRun()
        {
            if (run.Length == 0) return;
            runs.Add(new(run.ToString(), colour) { BodyPresetIndex = bodyPreset }); run.Clear();
        }
        void FlushLine()
        {
            FlushRun(); result.Add(new(visible.ToString(), runs.ToArray()));
            visible.Clear(); runs.Clear();
        }
    }
}
