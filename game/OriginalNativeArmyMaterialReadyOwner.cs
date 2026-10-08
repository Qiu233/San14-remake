using San14.Data;

/// <summary>Owns the source material's current full shader-table and texture
/// record subset. Actual registered resource lookup supplies CPU8/20/30 Data;
/// this owner never substitutes a missing local DOK object for runtime null.
/// It does not execute or certify the unrepresented parameter callbacks.</summary>
public sealed class OriginalNativeArmyMaterialReadyOwner
{
    private readonly OriginalNativeWorldShaderTableRegistry tables;
    private readonly OriginalNativeWorldShaderTableState? normal, cloth, staticNormal, staticCloth;
    private readonly OriginalNativeArmyMaterialTextureResource? regularData, renderTargetData, alternateData;
    private readonly OriginalArmyMaterialReadyState<OriginalNativeWorldShaderTableState, IOriginalNativeArmyMaterialTextureTypedRecord> fields = new();
    private bool retained = true;
    private int nextReadyStage = 1;
    private OriginalNativeArmyMaterialReadyOwner(LoadedOriginalArmyMaterialReadySource source,
        OriginalNativeWorldShaderTableRegistry actualTables, OriginalNativeArmyMaterialTextureRegistry actualTextures)
    {
        Source = source; tables = actualTables;
        // Resource import/allocation is before candidate publication/sorting.
        normal = source.NormalShader is { } n ? tables.RetainImportedTable(n) : null;
        cloth = source.ClothShader is { } c ? tables.RetainImportedTable(c) : null;
        staticNormal = source.StaticNormalShader is { } sn ? tables.RetainImportedTable(sn) : null;
        staticCloth = source.StaticClothShader is { } sc ? tables.RetainImportedTable(sc) : null;
        // These are the current loader's retained Data references. A later
        // directory addition/replacement does not silently change CPU8/20/30.
        regularData = actualTextures.LookupCurrentRegistered(source.RegularTextureGroupObjectId);
        renderTargetData = actualTextures.LookupCurrentRegistered(source.RenderTargetTextureGroupObjectId);
        alternateData = actualTextures.LookupCurrentRegistered(source.AlternateTextureGroupObjectId);
        RequireCurrent();
    }
    public LoadedOriginalArmyMaterialReadySource Source { get; }
    public bool HasActualRegularData=>regularData is not null;
    public bool HasActualRenderTargetData=>renderTargetData is not null;
    public bool HasActualAlternateData=>alternateData is not null;
    public OriginalNativeWorldShaderTableState? CurrentNormal78 => fields.CurrentNormal78;
    public OriginalNativeWorldShaderTableState? CurrentCloth80 => fields.CurrentCloth80;
    public IOriginalNativeArmyMaterialTextureTypedRecord? RegularTexturesC0 => fields.RegularC0;
    public IOriginalNativeArmyMaterialTextureTypedRecord? CurrentTexturesE0 => fields.CurrentTexturesE0;
    /// <summary>The retained D0 getter result after this material's actual
    /// stage2 ready. Null is the resulting owned field, not a DOK absence
    /// inference. A consuming SDF path must check its own typed record shape.</summary>
    public IOriginalNativeArmyMaterialTextureTypedRecord? ActualReadyRenderTargetD0
    {
        get
        {
            RequireCurrent();
            if(nextReadyStage!=3)throw new InvalidOperationException("Actual MaterialD0 requires completed stage2 ready fields.");
            return fields.RenderTargetD0;
        }
    }
    public string SourceEvidence => Source.SourceEvidence + ";actual-owned-current-registered-texture-Data;" +
        "full-table-registry-allocation-identity;D9D290-null-retains;not-whole-Material-ready";
    public static OriginalNativeArmyMaterialReadyOwner CreateLoaded(LoadedOriginalArmyMaterialReadySource source,
        OriginalNativeWorldShaderTableRegistry actualSharedTables, OriginalNativeArmyMaterialTextureRegistry actualTextures)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(actualSharedTables);
        ArgumentNullException.ThrowIfNull(actualTextures); return new(source, actualSharedTables, actualTextures);
    }
    /// <summary>Invoke at actual successful stage1 dependency completion.
    /// This writes the selected subset only, without fabricating the other
    /// material parameter records or a whole ready-phase completion receipt.</summary>
    public void ApplyReadyStage1Fields()
    {
        RequireCurrent();
        if (nextReadyStage != 1) throw new InvalidOperationException("Army material stage1 fields must follow this fresh import once.");
        fields.ApplyStage1ReadyFields(normal, cloth, staticNormal, staticCloth,
            regularData is not null, regularData?.ReadActualTypedRecord());
        nextReadyStage = 2;
    }
    public void ApplyReadyStage2Fields()
    {
        RequireCurrent();
        if (nextReadyStage != 2) throw new InvalidOperationException("Army material stage2 fields must follow its actual stage1 fields once.");
        fields.ApplyStage2ReadyFields(renderTargetData is not null, renderTargetData?.ReadActualTypedRecord(),
            alternateData is not null, alternateData?.ReadActualTypedRecord());
        nextReadyStage = 3;
    }
    public void ApplyCurrentMode(int actualMode)
    { RequireCurrent(); fields.ApplyCurrentMode(actualMode); }

    /// <summary>The prior table must already come from the actual dynamic
    /// replacement producer. This returns null for an actual null override;
    /// it never falls back to base or a neighbouring state18 provider.</summary>
    public OriginalNativeWorldShaderTableState? ReadWorldTableAfterActualPriorPublication(uint actualWorld2EC,
        OriginalNativeWorldShaderTableState? actualPriorTable, bool actualCopiedPrimitive, bool actualPrimitive50Present)
    {
        RequireCurrent();
        if (nextReadyStage != 3) throw new InvalidOperationException("World material fields require both actual ready-stage subsets.");
        if (actualPriorTable is not null) tables.RequireCurrent(actualPriorTable);
        return fields.SelectWorldTable(actualWorld2EC, actualPriorTable, actualCopiedPrimitive, actualPrimitive50Present);
    }
    public void RequireCurrent()
    {
        if (!retained) throw new InvalidDataException("Army current material fields were edited, retired or assigned by an unsupported writer.");
        if (normal is not null) tables.RequireCurrent(normal);
        if (cloth is not null) tables.RequireCurrent(cloth);
        if (staticNormal is not null) tables.RequireCurrent(staticNormal);
        if (staticCloth is not null) tables.RequireCurrent(staticCloth);
        regularData?.RequireRetained(); renderTargetData?.RequireRetained(); alternateData?.RequireRetained();
    }
    public void Invalidate(string actualMaterialWriter)
    { ArgumentException.ThrowIfNullOrWhiteSpace(actualMaterialWriter); retained = false; }
}
