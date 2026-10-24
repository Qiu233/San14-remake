using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// Original ordinary narration layout after message expansion. The caller supplies
// original Face1 raw-advance width, before the final whole-page width fit.
internal static class DeploymentNarrationPlainParagraph
{
    private static readonly HashSet<char> Head = new(new int[] {
        33,37,41,44,46,58,59,63,93,125,162,176,8217,8221,8240,8242,8243,8451,
        12289,12290,12293,12297,12299,12301,12303,12305,12309,12443,12444,12445,
        12446,12526,12533,12534,12540,12541,12542,65281,65285,65289,65292,65294,
        65306,65307,65311,65341,65373,65377,65379,65380,65381,65383,65384,65385,
        65386,65387,65388,65389,65390,65391,65392,65438,65439,65504
    }.Select(value => (char)value));
    private static readonly HashSet<char> Tail = new(new int[] {
        36,40,91,92,123,163,165,8216,8220,12296,12298,12300,12302,12304,12308,
        65284,65288,65339,65371,65378,65505,65509
    }.Select(value => (char)value));

    internal static IReadOnlyList<string> SplitOriginalPages(string expanded)
    {
        RequirePlain(expanded);
        return SplitPages(expanded);
    }

    internal static IReadOnlyList<string> SplitKnownColourPages(string expanded)
    {
        RequireKnownColours(expanded);
        return SplitPages(expanded);
    }

    private static IReadOnlyList<string> SplitPages(string expanded)
    {
        var pages = new List<string>();
        int start = 0, lf = 0;
        for (int index = 0; index < expanded.Length; index++)
        {
            if (expanded[index] != '\n' || ++lf <= 2) continue;
            pages.Add(expanded[start..index]);
            start = index + 1; lf = 0;
        }
        // The source creates one initial page even for an empty message,
        // but does not append another page after a final third LF.
        if (start < expanded.Length || pages.Count == 0) pages.Add(expanded[start..]);
        return pages.AsReadOnly();
    }

    internal static string WrapOneOriginalPage(string page, Func<string,int> rawWidth)
    {
        RequirePlain(page);
        return WrapPage(page, rawWidth);
    }

    internal static string WrapOneKnownColourPage(string page, Func<string,int> rawWidth)
    {
        RequireKnownColours(page);
        return WrapPage(page, rawWidth);
    }

    // 30D290 is also used by the region officer's two-line bubble. Its
    // caller supplies the scaled whole-string wrap measure, independently
    // of the final width-fit measure used to render the resulting lines.
    internal static string WrapKnownColourParagraph(string paragraph, Func<string,int> wrapWidth,
        int width, int softBreaks)
    {
        RequireKnownColours(paragraph);
        return WrapPage(paragraph,wrapWidth,width,softBreaks);
    }

    private static string WrapPage(string page, Func<string,int> rawWidth, int width=672, int softBreaks=2)
    {
        ArgumentNullException.ThrowIfNull(rawWidth);
        int trailingStart = page.Length;
        while (trailingStart > 0 && page[trailingStart - 1] == '\n') trailingStart--;
        int budget = softBreaks - (page.Count(value => value == '\n') - (page.Length - trailingStart));
        if (budget <= 0) return page;
        var output = new StringBuilder();
        var line = new StringBuilder();
        char previous = '\0';
        bool overflowScan = false;
        int cursor = 0;
        while (cursor < page.Length || overflowScan)
        {
            char current = cursor < page.Length ? page[cursor] : '\0';
            if (current == '\n')
            {
                if (cursor == trailingStart)
                {
                    if (budget > 0) output.Append(page.AsSpan(cursor));
                    break;
                }
                output.Append('\n'); line.Clear(); cursor++; overflowScan = false;
                continue;
            }
            if (!overflowScan && budget > 0 && rawWidth(line.ToString()) > width)
                overflowScan = true;
            if (current == '^')
            {
                // 30D290 retains the source control atom in the output,
                // but not in its raw-width buffer or previous-glyph state.
                output.Append(page.AsSpan(cursor, 3)); cursor += 3;
                continue;
            }
            if (overflowScan)
            {
                if (!Head.Contains(current) && !Tail.Contains(previous))
                {
                    if (current is ' ' or '\u3000') { cursor++; continue; }
                    output.Append('\n'); line.Clear(); budget--; overflowScan = false;
                    // The candidate remains unconsumed. This includes the
                    // source's terminal NUL after a kinsoku continuation.
                    continue;
                }
                if (current == '\0')
                    throw new InvalidOperationException("Terminal forbidden-tail overflow is outside the admitted plain paragraph slice.");
            }
            if (current == '\0') break;
            output.Append(current); line.Append(current); previous = current; cursor++;
        }
        return output.ToString();
    }

    // Measure maximum AFTER wrapping, then compute one profile for ALL lines.
    // The UI must disable per-line fitting after applying that shared profile.
    internal static int WholePageRawWidth(string wrappedPage, Func<string,int> rawWidth) =>
        wrappedPage.Split('\n').Max(rawWidth);

    private static void RequirePlain(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Any(code => code is '^' or '\0' or '\r' || char.IsControl(code) && code != '\n'))
            throw new InvalidOperationException("Narration requires an admitted plain LF message; formatting/control input remains unresolved.");
    }

    internal static void RequireKnownColours(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        for (int index = 0; index < value.Length; index++)
        {
            char code = value[index];
            if (code == '^')
            {
                if (index + 2 >= value.Length || value[index + 1] != '0' ||
                    value[index + 2] is not ('0' or '2' or '3' or '5') ||
                    index + 3 < value.Length && value[index + 3] == '~')
                    throw new InvalidOperationException("Narration colour command is unresolved.");
                index += 2; continue;
            }
            if (code is '\0' or '\r' || char.IsControl(code) && code != '\n')
                throw new InvalidOperationException("Narration control input is unresolved.");
        }
    }
}
