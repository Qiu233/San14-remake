using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// Body colour provenance, not a G1N palette selector or a full text preset.
internal enum NarrationBodyColour { OriginalNodeBody, TextPreset38Body, TextPreset39Body, TextPreset40Body }
internal sealed record NarrationColourRun(string Text, NarrationBodyColour Colour);
internal sealed record NarrationParagraphLine(string VisibleText, IReadOnlyList<NarrationColourRun> Runs);
internal sealed record NarrationParagraphPage(string SourceText, string WrappedSourceText,
    IReadOnlyList<NarrationParagraphLine> Lines, int MaximumRawWidth);

// Only the source's ^02/^03/^05/^00 body colours and ordinary LF
// text are admitted; raw source controls are retained beside visible runs.
internal static class DeploymentNarrationParagraphPlan
{
    internal static IReadOnlyList<NarrationParagraphPage> Create(string expanded, Func<string,int> rawWidth)
    {
        ArgumentNullException.ThrowIfNull(rawWidth);
        return DeploymentNarrationPlainParagraph.SplitKnownColourPages(expanded).Select(page =>
        {
            string wrapped = DeploymentNarrationPlainParagraph.WrapOneKnownColourPage(page, rawWidth);
            var lines = Runs(wrapped);
            return new NarrationParagraphPage(page, wrapped, lines,
                lines.Max(line => rawWidth(line.VisibleText)));
        }).ToArray();
    }

    internal static IReadOnlyList<NarrationParagraphLine> Runs(string wrapped)
    {
        var result = new List<NarrationParagraphLine>();
        var runs = new List<NarrationColourRun>();
        var run = new StringBuilder();
        var visible = new StringBuilder();
        // Each original page is a separate text publication. Colour persists
        // across LF within that page, but starts at its original node colours.
        NarrationBodyColour colour = NarrationBodyColour.OriginalNodeBody;
        for (int index = 0; index < wrapped.Length; index++)
        {
            char code = wrapped[index];
            if (code == '^')
            {
                FlushRun();
                colour = wrapped[index + 2] switch
                {'2'=>NarrationBodyColour.TextPreset38Body,'3'=>NarrationBodyColour.TextPreset40Body,'5'=>NarrationBodyColour.TextPreset39Body,
                    _=>NarrationBodyColour.OriginalNodeBody};
                index += 2; continue;
            }
            if (code == '\n') { FlushLine(); continue; }
            visible.Append(code); run.Append(code);
        }
        FlushLine(); // Preserves an explicit trailing LF as an empty line.
        return result.AsReadOnly();

        void FlushRun()
        {
            if (run.Length == 0) return;
            runs.Add(new(run.ToString(), colour)); run.Clear();
        }
        void FlushLine()
        {
            FlushRun(); result.Add(new(visible.ToString(), runs.ToArray()));
            visible.Clear(); runs.Clear();
        }
    }
}
