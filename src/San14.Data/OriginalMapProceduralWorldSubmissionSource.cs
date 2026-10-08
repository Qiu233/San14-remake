using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace San14.Data;

/// <summary>BB2110's current Common fields. A present Aux14 must retain its
/// reached D1E410 overlay writer; saved FlagEX2 alone leaves placement off.</summary>
public readonly record struct OriginalProceduralWorldCommon(bool Present, uint Flags8,
    uint Flags14, uint Exclusion404, uint FrustumCullingType64);

public readonly record struct OriginalProceduralWorldViewDispatch(int ViewIndex,
    uint FirstMask, uint SecondMask, uint ShadowMask);

public sealed record OriginalProceduralTemplateWorldFields(int TemplateIndex,
    uint DefinitionObjectId, uint ConstructorFlags44, uint DeclaredExclusion78,
    int CasterFlags10C);

/// <summary>The finite PartsSet publisher supplies this primitive's source
/// category/ordinal and Hi/Mid/Low membership. Category is not its LOD.</summary>
public sealed record OriginalProceduralWorldPrimitive(int TemplateIndex, int Category,
    int SourcePrimitiveOrdinal, int SourceDrawIndex, int Lod);

public sealed record OriginalProceduralWorldInstanceStream(int TemplateIndex, int Lod,
    IReadOnlyList<OriginalProceduralInstance> Instances, byte[] Affine48Bytes);

public sealed record OriginalProceduralWorldSubmission(OriginalProceduralWorldPrimitive Primitive,
    OriginalProceduralWorldInstanceStream Stream)
{
    public int InstanceCount => Stream.Instances.Count;
    public bool EmptyType22Command => InstanceCount == 0;
}

/// <summary>Owned CPU publication of the normal BB8710 instancing path. This
/// preserves source insertion order and actual 48-byte rows. Graphics state,
/// current light wrappers and rendering remain their independent consumers.</summary>
public static class OriginalMapProceduralWorldSubmissionSource
{
    public const uint InstancingExclusion = 0xFFFFFFFD;
    public const uint InstancedCommandFlags = 0x80000000;
    public const string SourceEvidence = "CE:BB2110:View0,6;BB8710:9790..9977/9A51..9B25;" +
        "BBAFD7:B95A00(casterSign,0,0,FFFFFFFF,FFFFFFFD,0);" +
        "BBB9F5:template/block/element-order;BBBB09/BBBB4F:48B-copy;" +
        "BBC063:D619E0-type0B-count+24/flags0C.bit31;D4A6E0-empty-type22";

    /// <summary>Read only at the already imported scene boundary. Flag44 is
    /// retained for audit: the BB8710 instancing call does not apply V408 or
    /// the template's 328/32C/330/exclusion78 gates.</summary>
    public static OriginalProceduralTemplateWorldFields ReadTemplate(OriginalProceduralTemplate template)
    {
        var source = template.Declaration.DefinitionDocument;
        var definition = template.Declaration.Definition;
        var flags = OriginalMapModelNativeVisibilitySources.ReadWorldPqSourceFlags(source, definition.ObjectId);
        var exclusion = definition.Properties.Single(p => p.Index == 9 && p.NameHash == 0xC0C5A5B5);
        if (exclusion.TypeCode != 5 || exclusion.ElementCount != 1 || exclusion.RawValue.Length != 4)
            throw new InvalidDataException("Procedural template exclusion requires its original scalar property9.");
        return new(template.Index, definition.ObjectId, (uint)flags.Flag | ((uint)flags.DynamicFlag << 8),
            BinaryPrimitives.ReadUInt32LittleEndian(exclusion.RawValue),
            OriginalDeferredEarlyShadowCasterSource.ReadLoadedWorldCasterFlags10C(definition));
    }

    /// <summary>BB2110 exits on the first absent mask, including View0. It
    /// skips an unavailable View receiver and then continues to View6.</summary>
    public static IReadOnlyList<OriginalProceduralWorldViewDispatch> Dispatch(
        uint callerFirst, uint callerSecond, uint callerShadow,
        uint actualPresentViewMask, OriginalProceduralWorldCommon common)
    {
        if (common.Present && (common.Flags14 & 4) == 0) return [];
        var result = new List<OriginalProceduralWorldViewDispatch>(2);
        foreach (int view in new[] { 0, 6 })
        {
            uint bit = 1u << view;
            if (((callerFirst | callerSecond | callerShadow) & bit) == 0) break;
            if ((actualPresentViewMask & bit) != 0)
                result.Add(new(view, callerFirst & bit, callerSecond & bit, callerShadow & bit));
        }
        return result;
    }

    /// <summary>B95A00 instancing caller. Only caster sign suppresses the
    /// supplied shadow family. Common.bit11 and successful3340 apply next.</summary>
    public static OriginalProceduralWorldViewDispatch ResolveTemplateMasks(
        OriginalProceduralWorldViewDispatch dispatch, OriginalProceduralTemplateWorldFields template,
        uint actualSuccessful3340, OriginalProceduralWorldCommon common)
        => dispatch with {
            FirstMask = dispatch.FirstMask & actualSuccessful3340,
            SecondMask = dispatch.SecondMask & actualSuccessful3340,
            ShadowMask = template.CasterFlags10C < 0 || common.Present && (common.Flags8 & 0x800) == 0
                ? 0 : dispatch.ShadowMask & actualSuccessful3340
        };

    public static uint EffectiveExclusion(OriginalProceduralWorldCommon common)
        => InstancingExclusion | (common.Present ? common.Exclusion404 : 0);

    /// <summary>B95C40's first reached nonexcluded kind1 LightData becomes
    /// context+18. The caster selection uses the same ordered candidates
    /// and BB8710's literal zero first/second-family arguments.</summary>
    public static OriginalDeferredEarlyShadowCurrentCasterLightRecord? SelectFirstLight(
        OriginalDeferredEarlyShadowCurrentCasterLightRows actualLights, OriginalProceduralWorldCommon common)
        => actualLights.SelectFirstCurrentKind1(EffectiveExclusion(common));

    public static bool FirstCasterFamilyAllowed(byte actualF1C)
        => OriginalDeferredEarlyShadowCasterSource.ReadFirstCasterGate(actualF1C, false);
    public static bool SecondCasterFamilyAllowed(byte actualF1F)
        => (actualF1F & 0x80) == 0 && (actualF1F & 1) != 0; // zero caller becomes literal1 when F1F.bit7 is clear

    /// <summary>Finite Culling1/Scanning0 candidate order. The current-view
    /// selector remains responsible for the real per-block height/frustum
    /// test. Absolute region is visited first when a nonnegative caster
    /// template exists, then the rectangular region excludes its cells.</summary>
    public static IReadOnlyList<(int X, int Z)> CandidateBlockOrder(
        OriginalProceduralPlacementDeclaration declaration, OriginalProceduralCurrentView view,
        IReadOnlyList<OriginalProceduralTemplateWorldFields> templates)
    {
        if (view.Settings.CullingType450 != 1 || view.Settings.ScanningType454 != 0)
            throw new NotSupportedException("BB8710 ordered publication requires Culling1/Scanning0.");
        float width = declaration.FieldSizeX / declaration.BlocksX;
        int Index(float position, float offset, int count) => Math.Clamp((int)D(S(position, offset), width), 0, count);
        int ax0 = Index(S(view.Eye.X, declaration.AbsolutelyRenderDistanceH), declaration.FieldOffsetX, (int)declaration.BlocksX);
        int ax1 = Index(A(view.Eye.X, declaration.AbsolutelyRenderDistanceH), declaration.FieldOffsetX, (int)declaration.BlocksX);
        int az0 = Index(S(view.Eye.Z, declaration.AbsolutelyRenderDistanceH), declaration.FieldOffsetZ, (int)declaration.BlocksZ);
        int az1 = Index(A(view.Eye.Z, declaration.AbsolutelyRenderDistanceH), declaration.FieldOffsetZ, (int)declaration.BlocksZ);
        bool near = (declaration.AbsolutelyRenderDistanceH != 0 || declaration.AbsolutelyRenderDistanceV != 0) &&
            templates.Any(t => t.CasterFlags10C >= 0);
        int x0 = Math.Max(Index(S(view.Eye.X, declaration.ViewDistance), declaration.FieldOffsetX, (int)declaration.BlocksX),
            (int)D(S(view.FrustumMinimum.X, declaration.FieldOffsetX), width));
        int z0 = Math.Max(Index(S(view.Eye.Z, declaration.ViewDistance), declaration.FieldOffsetZ, (int)declaration.BlocksZ),
            (int)D(S(view.FrustumMinimum.Z, declaration.FieldOffsetZ), width));
        int x1 = Math.Min(Index(A(view.Eye.X, declaration.ViewDistance), declaration.FieldOffsetX, (int)declaration.BlocksX),
            (int)MathF.Ceiling(D(S(view.FrustumMaximum.X, declaration.FieldOffsetX), width)));
        int z1 = Math.Min(Index(A(view.Eye.Z, declaration.ViewDistance), declaration.FieldOffsetZ, (int)declaration.BlocksZ),
            (int)MathF.Ceiling(D(S(view.FrustumMaximum.Z, declaration.FieldOffsetZ), width)));
        var result = new List<(int, int)>();
        if (near) for (int z = az0; z < az1; z++) for (int x = ax0; x < ax1; x++) result.Add((x, z));
        for (int z = z0; z < z1; z++) for (int x = x0; x < x1; x++)
            if (!near || x < ax0 || x >= ax1 || z < az0 || z >= az1) result.Add((x, z));
        return result;
    }

    /// <summary>Build once per current view after SelectBlock. Actual source
    /// caps are50000; this finite actual scene stays below them. A future
    /// over-cap scene requires the native clip/start policy before emission.</summary>
    public static IReadOnlyList<OriginalProceduralWorldSubmission> Prepare(
        IReadOnlyList<(OriginalProceduralBlock Block, IReadOnlyList<OriginalProceduralLodSlice> Slices)> actualBlocksInSourceOrder,
        IReadOnlyList<OriginalProceduralWorldPrimitive> actualPartsPrimitives)
    {
        var instances = new Dictionary<(int Template, int Lod), List<OriginalProceduralInstance>>();
        foreach (var (block, slices) in actualBlocksInSourceOrder)
        foreach (var slice in slices.OrderBy(s => s.ElementIndex).ThenBy(s => s.Lod))
        {
            if (slice.Lod is < 0 or > 2) throw new InvalidDataException("Procedural LOD exceeds its native3-slot directory.");
            var bank = block.Banks.Single(b => b.ElementIndex == slice.ElementIndex && b.TemplateIndex == slice.TemplateIndex);
            if (slice.Start < 0 || slice.Count < 0 || slice.Start > bank.Instances.Count - slice.Count)
                throw new InvalidDataException("Procedural slice exceeds its generated48B bank.");
            var key = (slice.TemplateIndex, slice.Lod);
            if (!instances.TryGetValue(key, out var stream)) instances[key] = stream = new();
            for (int i = 0; i < slice.Count; i++) stream.Add(bank.Instances[slice.Start + i]);
        }
        var streams = new Dictionary<(int Template, int Lod), OriginalProceduralWorldInstanceStream>();
        foreach (var primitive in actualPartsPrimitives)
        {
            var key = (primitive.TemplateIndex, primitive.Lod);
            if (streams.ContainsKey(key)) continue;
            var stream = instances.GetValueOrDefault(key) ?? new();
            if (stream.Count > 50000) throw new NotSupportedException("Procedural stream exceeds the actual finite50000 cap profile.");
            var bytes = new byte[checked(stream.Count * 48)];
            for (int i = 0; i < stream.Count; i++) stream[i].Affine.Write(bytes.AsSpan(i * 48, 48));
            streams.Add(key, new(primitive.TemplateIndex, primitive.Lod, stream.AsReadOnly(), bytes));
        }
        var preparedTemplates = streams.Values.Where(s => s.Instances.Count != 0).Select(s => s.TemplateIndex).ToHashSet();
        return actualPartsPrimitives.Where(p => preparedTemplates.Contains(p.TemplateIndex))
            .OrderBy(p => p.TemplateIndex).ThenBy(p => p.Category).ThenBy(p => p.SourcePrimitiveOrdinal)
            .Select(p => new OriginalProceduralWorldSubmission(p, streams[(p.TemplateIndex, p.Lod)])).ToArray();
    }
    [MethodImpl(MethodImplOptions.NoInlining)] private static float A(float a, float b) => a + b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float S(float a, float b) => a - b;
    [MethodImpl(MethodImplOptions.NoInlining)] private static float D(float a, float b) => a / b;
}
