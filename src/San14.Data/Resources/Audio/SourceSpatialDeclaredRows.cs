namespace San14.Data;

/// <summary>126F370's first contribution to one decoded source row, with its
/// outer gain factored out. Main/alternate are the actual per-output panner
/// coefficients; neither is a device fold, category gain or authored curve.</summary>
internal static class SourceSpatialDeclaredRows
{
    /// <summary>1280810's LFE producer, after the main non-LFE panner has
    /// filled the logical output layout. Spatial mode is the declared mode
    /// being greater than zero; it is not the spatial distance weight.</summary>
    internal static void PrepareLfe(Span<float> main, uint mask, float spread,
        float sampleLfe, bool spatialMode)
    {
        if (main.Length is not (2 or 6 or 8))
            throw new InvalidDataException("spatial.sound.declared_row_layout");
        if (main.Length == 2) return;
        float lfe = 1f <= spread ? 0f : 1f - spread;
        if ((mask & 8u) != 0)
        {
            main[3] = lfe;
            return;
        }

        lfe = 1f <= spread ? 0f : (1f - spread) * sampleLfe;
        main[3] = lfe;
        if (!spatialMode || !(lfe > 0f)) return;
        float square = main[0] * main[0] + main[1] * main[1];
        square += main[2] * main[2];
        square += main[4] * main[4];
        square += main[5] * main[5];
        if (main.Length == 8)
        {
            square += main[6] * main[6];
            square += main[7] * main[7];
        }
        float norm = MathF.Sqrt(square);
        //These are the two literal source guards, including their distinct
        //unordered comparison behavior. No sum clamp is present in source.
        if (main.Length == 6 ? !(norm > 0f) : norm <= 0f)
        {
            main[3] = 0f;
            return;
        }
        float sum = main[0] + main[1];
        sum += main[2];
        sum += main[4];
        sum += main[5];
        if (main.Length == 8)
        {
            sum += main[6];
            sum += main[7];
        }
        main[3] = lfe * (norm / sum);
    }

    internal static void Write(Span<float> output, int speaker, uint mask,
        ReadOnlySpan<float> main, ReadOnlySpan<float> alternate,
        float directWeight, float spatialWeight, bool spatialMode)
    {
        if (output.Length is not (2 or 6 or 8))
            throw new InvalidDataException("spatial.sound.declared_row_layout");
        output.Clear();
        if ((uint)speaker >= 8) return;
        if (!spatialMode)
        {
            WriteDirect(output, speaker, mask, main);
            return;
        }

        //The spatial mode keeps only FL/FR for stereo. The six-output
        //branch has no cases6/7: it does not fold them into rear channels.
        if (output.Length == 2 && speaker >= 2 || output.Length == 6 && speaker >= 6) return;
        if (speaker == 3)
        {
            output[3] = Read(main, 3);
            return;
        }

        float primary = Read(main, speaker);
        if (!(primary > 0f)) return;
        if (spatialWeight > 0f && Read(alternate, speaker) > 0f)
        {
            Require(alternate, output.Length);
            if (directWeight > 0f)
            {
                output[speaker] = directWeight;
                for (int lane = 0; lane < output.Length; lane++)
                    if (lane != 3) output[lane] += spatialWeight * alternate[lane];

                //The eight-output first-contribution branch deliberately
                //normalizes using only0/1/2/4/5, although it also scales6/7.
                //Do not replace this with the later accumulation branch's
                //seven-lane normalization or a symmetric vector length.
                float square = output[0] * output[0] + output[1] * output[1];
                if (output.Length >= 6)
                {
                    square += output[2] * output[2];
                    square += output[4] * output[4];
                    square += output[5] * output[5];
                }
                float scale = primary / MathF.Sqrt(square);
                for (int lane = 0; lane < output.Length; lane++)
                    if (lane != 3) output[lane] *= scale;
            }
            else
            {
                //At the native far limit directWeight=0, spatialWeight=1.
                //This source branch does not multiply spatialWeight again.
                for (int lane = 0; lane < output.Length; lane++)
                    if (lane != 3) output[lane] = primary * alternate[lane];
            }
        }
        else output[speaker] = primary;

        if (output.Length >= 6 && (mask & 8u) == 0)
            output[3] = Read(main, 3) * primary;
    }

    private static void WriteDirect(Span<float> output, int speaker, uint mask, ReadOnlySpan<float> main)
    {
        if (output.Length == 2)
        {
            switch (speaker)
            {
                case 0: output[0] = 1f; break;
                case 1: output[1] = 1f; break;
                case 2: output[0] = output[1] = 0.70710677f; break;
                case 4: case 6: output[0] = 0.70710677f; break;
                case 5: case 7: output[1] = 0.70710677f; break;
            }
            return;
        }
        if (speaker == 3) { output[3] = 1f; return; }
        if (output.Length == 6 && speaker is 6 or 7)
        {
            output[speaker - 2] = 1f;
            //The source LFE read is the unused original6/7 row coefficient,
            //which remains zero in this freshly cleared first contribution.
            return;
        }
        output[speaker] = 1f;
        if ((mask & 8u) == 0) output[3] = Read(main, 3);
    }

    private static float Read(ReadOnlySpan<float> values, int lane)
    {
        Require(values, lane + 1);
        return values[lane];
    }
    private static void Require(ReadOnlySpan<float> values, int count)
    {
        if (values.Length < count) throw new InvalidDataException("spatial.sound.declared_row_coefficients");
    }
}
