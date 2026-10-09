namespace San14.Data;

/// <summary>Native rigid mL2WT preparation from the three mL2W shader rows.
/// SC 97DAB0 transposes the position rows before the scalar inverse at 97C620;
/// TC equivalents are 97C920 and 97B490. No quaternion is normalized.</summary>
public static class UnitWorldTransformNormal
{
    /// <summary>Returns false when the separately rounded determinant equals
    /// positive or negative zero. That native branch leaves the previous
    /// destination untouched, so this overload returns no manufactured rows.</summary>
    public static bool TryFromPositionShaderRows(ReadOnlySpan<float> positionRows, out float[] normalRows)
    {
        RequireRows(positionRows);
        // 97C960..97C9F9 transpose source shader rows into native row vectors.
        float a = positionRows[0], b = positionRows[4], c = positionRows[8];
        float d = positionRows[1], e = positionRows[5], f = positionRows[9];
        float g = positionRows[2], h = positionRows[6], i = positionRows[10];

        // 97B504..97B55F: preserve each mulss/subss/addss rounding and order.
        float ei = e * i;
        float fh = f * h;
        float c0 = ei - fh;
        float ch = c * h;
        float bi = b * i;
        float c1 = ch - bi;
        float bf = b * f;
        float ce = c * e;
        float c2 = bf - ce;
        float determinant = a * c0;
        float second = d * c1;
        determinant = determinant + second;
        float third = g * c2;
        determinant = determinant + third;
        if (determinant == 0f)
        {
            normalRows = [];
            return false;
        }

        // The unordered determinant path also executes the original division.
        float reciprocal = 1f / determinant;
        float gf = g * f;
        float di = d * i;
        float c3 = gf - di;
        c3 = c3 * reciprocal;
        float ai = a * i;
        float gc = g * c;
        float c4 = ai - gc;
        c4 = c4 * reciprocal;
        float dc = d * c;
        float af = a * f;
        float c5 = dc - af;
        c5 = c5 * reciprocal;
        float dh = d * h;
        float ge = g * e;
        float c6 = dh - ge;
        c6 = c6 * reciprocal;
        float gb = g * b;
        float ah = a * h;
        float c7 = gb - ah;
        c7 = c7 * reciprocal;
        float ae = a * e;
        float db = d * b;
        float c8 = ae - db;
        c8 = c8 * reciprocal;

        // First 48 bytes of the native inverse destination are bound directly
        // as mL2WT[3]. All three w stores are literal positive zero.
        normalRows = [reciprocal * c0, reciprocal * c1, reciprocal * c2, 0f,
            c3, c4, c5, 0f, c6, c7, c8, 0f];
        return true;
    }

    public static float[] FromPositionShaderRows(ReadOnlySpan<float> positionRows)
    {
        if (!TryFromPositionShaderRows(positionRows, out float[] result))
            throw new InvalidDataException("Native normal determinant is zero; previous normal rows are required.");
        return result;
    }

    /// <summary>Preserves all twelve explicit previous normal words when the
    /// native determinant-zero branch performs no stores.</summary>
    public static float[] FromPositionShaderRows(ReadOnlySpan<float> positionRows,
        ReadOnlySpan<float> previousNormalRows)
    {
        RequireRows(previousNormalRows);
        return TryFromPositionShaderRows(positionRows, out float[] result) ? result : previousNormalRows.ToArray();
    }

    private static void RequireRows(ReadOnlySpan<float> rows)
    {
        if (rows.Length != 12)
            throw new InvalidDataException("Native rigid matrix preparation requires twelve float32 shader row words.");
    }
}
